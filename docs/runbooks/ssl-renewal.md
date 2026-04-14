# SSL certificate renewal runbook

Production site `sendnex.xyz` uses Let's Encrypt certificates managed by
`certbot` on the VPS (`178.104.141.21`). Certificates are valid for 90 days;
`certbot.timer` attempts renewal twice daily and renews once a cert has less
than 30 days remaining.

## Automatic renewal flow

1. `certbot.timer` (systemd) fires and runs `certbot renew`.
2. Certbot uses the HTTP-01 challenge served by nginx via `/.well-known/acme-challenge/`.
3. On successful renewal, certbot executes every script in
   `/etc/letsencrypt/renewal-hooks/deploy/`.
4. Our hook `reload-nginx.sh` runs `docker exec eaas-nginx nginx -t` and,
   if the config is valid, `nginx -s reload`. nginx then serves the new cert
   with zero downtime.

The hook source lives in the repo at
`infrastructure/letsencrypt/reload-nginx.sh` — keep it there as the source of
truth. Never edit the copy on the VPS directly.

## One-time provisioning

Run locally from the repo root:

```bash
VPS_HOST=root@178.104.141.21 ./scripts/provision-ssl-hooks.sh
```

This copies the hook to `/etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh`
on the VPS with `0755` permissions owned by `root`.

## Verification

After provisioning, verify the entire renewal path without actually renewing:

```bash
ssh root@178.104.141.21 'certbot renew --dry-run'
```

Expected output ends with `Congratulations, all simulated renewals succeeded`
and the hook log line `nginx reloaded successfully` in journald:

```bash
ssh root@178.104.141.21 'journalctl -t letsencrypt-deploy-hook --since "10 minutes ago"'
```

## Manual renewal (emergency)

If the timer has failed and a cert is about to expire:

```bash
ssh root@178.104.141.21
certbot renew --force-renewal
# Hook will fire automatically; verify:
docker exec eaas-nginx nginx -T | grep ssl_certificate
```

## Rollback

If a renewal ships a broken cert and nginx fails the config test, the hook
will NOT reload nginx and the old cert remains live. To recover:

1. Inspect the broken cert: `certbot certificates`
2. Roll back to the previous lineage: `certbot rollback --checkpoints 1`
3. Manually reload: `docker exec eaas-nginx nginx -s reload`

## Monitoring

Add the following to the uptime dashboard:

- TLS expiry check on `https://sendnex.xyz` — alert at 14 days.
- systemd timer state on VPS: `systemctl list-timers certbot.timer`.
