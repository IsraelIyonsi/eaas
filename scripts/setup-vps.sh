#!/usr/bin/env bash
# =============================================================================
# EaaS VPS Provisioning Script
# Target: Ubuntu 22.04 LTS on Hetzner CX22 (2 vCPU, 4GB RAM, 40GB SSD)
# Run as root on a fresh VPS: bash setup-vps.sh
# =============================================================================

set -euo pipefail

# --- Configuration ---
APP_USER="eaas"
APP_DIR="/opt/eaas"
BACKUP_DIR="${APP_DIR}/backups"
LOG_DIR="${APP_DIR}/logs"
SSH_PORT=22

echo "============================================"
echo " EaaS VPS Provisioning"
echo " $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo "============================================"

# --- 1. System Update ---
echo ""
echo "[1/10] Updating system packages..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get upgrade -y -qq
apt-get install -y -qq \
  curl \
  wget \
  gnupg \
  lsb-release \
  ca-certificates \
  apt-transport-https \
  software-properties-common \
  ufw \
  fail2ban \
  logrotate \
  cron \
  unzip \
  htop \
  ncdu

echo "  System packages updated."

# --- 2. Install Docker + Docker Compose ---
echo ""
echo "[2/10] Installing Docker..."

# Remove old versions
apt-get remove -y -qq docker docker-engine docker.io containerd runc 2>/dev/null || true

# Add Docker GPG key and repository
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  tee /etc/apt/sources.list.d/docker.list > /dev/null

apt-get update -qq
apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Configure Docker daemon
cat > /etc/docker/daemon.json <<'DOCKER_CONF'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  },
  "default-ulimits": {
    "nofile": {
      "Name": "nofile",
      "Hard": 65536,
      "Soft": 65536
    }
  },
  "live-restore": true
}
DOCKER_CONF

systemctl enable docker
systemctl restart docker

echo "  Docker installed: $(docker --version)"
echo "  Docker Compose: $(docker compose version)"

# --- 3. Configure Firewall (UFW) ---
echo ""
echo "[3/10] Configuring firewall..."

ufw --force reset
ufw default deny incoming
ufw default allow outgoing
ufw allow ${SSH_PORT}/tcp comment "SSH"
ufw allow 80/tcp comment "HTTP"
ufw allow 443/tcp comment "HTTPS"
ufw --force enable

echo "  Firewall enabled. Allowed ports: ${SSH_PORT}, 80, 443"

# --- 4. Create Application User ---
echo ""
echo "[4/10] Creating application user '${APP_USER}'..."

if id "${APP_USER}" &>/dev/null; then
  echo "  User '${APP_USER}' already exists."
else
  useradd -m -s /bin/bash -G docker "${APP_USER}"
  echo "  User '${APP_USER}' created and added to docker group."
fi

# --- 5. SSH Hardening ---
echo ""
echo "[5/10] Hardening SSH..."

SSHD_CONFIG="/etc/ssh/sshd_config"
cp "${SSHD_CONFIG}" "${SSHD_CONFIG}.bak.$(date +%s)"

# Apply hardened settings
sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin no/' "${SSHD_CONFIG}"
sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' "${SSHD_CONFIG}"
sed -i 's/^#\?PubkeyAuthentication.*/PubkeyAuthentication yes/' "${SSHD_CONFIG}"
sed -i 's/^#\?ChallengeResponseAuthentication.*/ChallengeResponseAuthentication no/' "${SSHD_CONFIG}"
sed -i 's/^#\?UsePAM.*/UsePAM yes/' "${SSHD_CONFIG}"
sed -i 's/^#\?X11Forwarding.*/X11Forwarding no/' "${SSHD_CONFIG}"
sed -i 's/^#\?MaxAuthTries.*/MaxAuthTries 3/' "${SSHD_CONFIG}"
sed -i 's/^#\?ClientAliveInterval.*/ClientAliveInterval 300/' "${SSHD_CONFIG}"
sed -i 's/^#\?ClientAliveCountMax.*/ClientAliveCountMax 2/' "${SSHD_CONFIG}"

# Copy root SSH keys to app user so they can SSH in
if [ -d /root/.ssh ]; then
  mkdir -p "/home/${APP_USER}/.ssh"
  cp /root/.ssh/authorized_keys "/home/${APP_USER}/.ssh/authorized_keys" 2>/dev/null || true
  chown -R "${APP_USER}:${APP_USER}" "/home/${APP_USER}/.ssh"
  chmod 700 "/home/${APP_USER}/.ssh"
  chmod 600 "/home/${APP_USER}/.ssh/authorized_keys" 2>/dev/null || true
fi

# Allow app user to sudo without password (for deployments)
echo "${APP_USER} ALL=(ALL) NOPASSWD: ALL" > "/etc/sudoers.d/${APP_USER}"
chmod 440 "/etc/sudoers.d/${APP_USER}"

systemctl restart sshd

echo "  SSH hardened: root login disabled, password auth disabled, key auth only."

# --- 6. Create Directory Structure ---
echo ""
echo "[6/10] Creating directory structure..."

mkdir -p "${APP_DIR}"
mkdir -p "${BACKUP_DIR}/daily"
mkdir -p "${BACKUP_DIR}/weekly"
mkdir -p "${LOG_DIR}"
mkdir -p "${APP_DIR}/nginx"
mkdir -p "${APP_DIR}/certbot/conf"
mkdir -p "${APP_DIR}/certbot/www"

chown -R "${APP_USER}:${APP_USER}" "${APP_DIR}"

echo "  Directories created at ${APP_DIR}/"

# --- 7. Install Certbot for Let's Encrypt TLS ---
echo ""
echo "[7/10] Installing Certbot..."

apt-get install -y -qq certbot

echo "  Certbot installed: $(certbot --version 2>&1)"
echo ""
echo "  To obtain a certificate, run:"
echo "    certbot certonly --standalone -d mail.yourdomain.com --agree-tos -m you@yourdomain.com"
echo "  Or use the Docker Nginx + Certbot approach with webroot:"
echo "    certbot certonly --webroot -w ${APP_DIR}/certbot/www -d mail.yourdomain.com"

# Set up auto-renewal cron
cat > /etc/cron.d/certbot-renew <<'CERTBOT_CRON'
# Renew Let's Encrypt certificates twice daily
0 0,12 * * * root certbot renew --quiet --deploy-hook "docker exec eaas-nginx nginx -s reload" >> /opt/eaas/logs/certbot.log 2>&1
CERTBOT_CRON

echo "  Certbot auto-renewal cron configured."

# --- 8. Set Up Log Rotation ---
echo ""
echo "[8/10] Configuring log rotation..."

cat > /etc/logrotate.d/eaas <<'LOGROTATE_CONF'
/opt/eaas/logs/*.log {
    daily
    missingok
    rotate 14
    compress
    delaycompress
    notifempty
    create 0640 eaas eaas
    sharedscripts
    postrotate
        # Signal Docker containers to reopen log files if needed
        docker kill --signal=USR1 $(docker ps -q) 2>/dev/null || true
    endscript
}
LOGROTATE_CONF

echo "  Log rotation configured: daily, 14 days retention, compressed."

# --- 9. Set Up PostgreSQL Backup Cron ---
echo ""
echo "[9/10] Setting up database backup cron..."

# Copy backup script
cat > "${APP_DIR}/scripts/backup-db.sh" <<'BACKUP_SCRIPT'
#!/usr/bin/env bash
# PostgreSQL Backup Script for EaaS
# Usage: backup-db.sh [daily|weekly]

set -euo pipefail

BACKUP_TYPE="${1:-daily}"
APP_DIR="/opt/eaas"
BACKUP_DIR="${APP_DIR}/backups/${BACKUP_TYPE}"
TIMESTAMP=$(date +%Y-%m-%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/eaas_${TIMESTAMP}.sql.gz"
LOG_PREFIX="[$(date -u '+%Y-%m-%d %H:%M:%S UTC')]"

# Load environment variables
if [ -f "${APP_DIR}/.env" ]; then
  set -a
  source "${APP_DIR}/.env"
  set +a
fi

DB_USER="${POSTGRES_USER:-eaas}"
DB_NAME="${POSTGRES_DB:-eaas}"
DB_CONTAINER="eaas-postgres"

echo "${LOG_PREFIX} Starting ${BACKUP_TYPE} backup..."

# Create backup directory if missing
mkdir -p "${BACKUP_DIR}"

# Dump and compress
docker exec "${DB_CONTAINER}" pg_dump -U "${DB_USER}" "${DB_NAME}" | gzip > "${BACKUP_FILE}"

BACKUP_SIZE=$(du -sh "${BACKUP_FILE}" | cut -f1)
echo "${LOG_PREFIX} Backup created: ${BACKUP_FILE} (${BACKUP_SIZE})"

# Rotate old backups
if [ "${BACKUP_TYPE}" = "daily" ]; then
  RETENTION_DAYS=7
elif [ "${BACKUP_TYPE}" = "weekly" ]; then
  RETENTION_DAYS=28
else
  RETENTION_DAYS=7
fi

DELETED=$(find "${BACKUP_DIR}" -name "eaas_*.sql.gz" -mtime +${RETENTION_DAYS} -delete -print | wc -l)
echo "${LOG_PREFIX} Rotated ${DELETED} old ${BACKUP_TYPE} backup(s) (retention: ${RETENTION_DAYS} days)"

echo "${LOG_PREFIX} ${BACKUP_TYPE^} backup complete."
BACKUP_SCRIPT

chmod +x "${APP_DIR}/scripts/backup-db.sh"
mkdir -p "${APP_DIR}/scripts"
# Move the script we just created (it's already in the right place)

# Set up cron jobs
cat > /etc/cron.d/eaas-backup <<BACKUP_CRON
# EaaS PostgreSQL Backups
# Daily at 02:00 UTC
0 2 * * * ${APP_USER} ${APP_DIR}/scripts/backup-db.sh daily >> ${LOG_DIR}/backup.log 2>&1
# Weekly on Sunday at 03:00 UTC
0 3 * * 0 ${APP_USER} ${APP_DIR}/scripts/backup-db.sh weekly >> ${LOG_DIR}/backup.log 2>&1
BACKUP_CRON

echo "  Backup cron jobs configured."
echo "    Daily:  02:00 UTC, retain 7 days"
echo "    Weekly: 03:00 UTC (Sunday), retain 28 days"

# --- 10. Configure Fail2Ban ---
echo ""
echo "[10/10] Configuring Fail2Ban..."

cat > /etc/fail2ban/jail.local <<'FAIL2BAN_CONF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5
backend = systemd

[sshd]
enabled = true
port = ssh
filter = sshd
logpath = /var/log/auth.log
maxretry = 3
bantime = 3600
FAIL2BAN_CONF

systemctl enable fail2ban
systemctl restart fail2ban

echo "  Fail2Ban enabled: SSH brute-force protection active."

# --- Summary ---
echo ""
echo "============================================"
echo " VPS Provisioning Complete"
echo "============================================"
echo ""
echo " Next steps:"
echo "  1. Copy docker-compose.yml to ${APP_DIR}/"
echo "  2. Copy .env with production secrets to ${APP_DIR}/.env"
echo "  3. Set file permissions: chmod 600 ${APP_DIR}/.env"
echo "  4. Obtain TLS certificate:"
echo "     certbot certonly --standalone -d mail.yourdomain.com"
echo "  5. Pull and start services:"
echo "     cd ${APP_DIR} && docker compose pull && docker compose up -d"
echo "  6. Verify: curl http://localhost:5000/health"
echo ""
echo " SSH access (after this session):"
echo "   ssh ${APP_USER}@<vps-ip>"
echo ""
echo " WARNING: Root login is now disabled."
echo " Ensure you can SSH as '${APP_USER}' before closing this session."
echo "============================================"
