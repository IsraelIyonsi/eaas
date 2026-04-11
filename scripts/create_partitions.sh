#!/bin/bash
# =============================================================================
# EaaS Partition Creator
# Creates monthly partitions for emails, inbound_emails, email_events
# Usage: ./create_partitions.sh <months_ahead> [database_url]
# Example: ./create_partitions.sh 6
#          ./create_partitions.sh 3 "postgresql://eaas:password@localhost:5432/eaas"
# =============================================================================

set -euo pipefail

MONTHS_AHEAD="${1:-3}"
DATABASE_URL="${2:-${DATABASE_URL:-postgresql://eaas:password@localhost:5432/eaas}}"

TABLES=("emails" "inbound_emails" "email_events")

echo "=== EaaS Partition Creator ==="
echo "Creating partitions for next ${MONTHS_AHEAD} months"
echo "Database: ${DATABASE_URL%%@*}@***"
echo ""

for i in $(seq 0 $((MONTHS_AHEAD - 1))); do
    TARGET_DATE=$(date -d "+${i} months" +%Y-%m-01 2>/dev/null || date -v+${i}m +%Y-%m-01)
    YEAR=$(echo "$TARGET_DATE" | cut -d'-' -f1)
    MONTH=$(echo "$TARGET_DATE" | cut -d'-' -f2)
    MONTH_INT=$((10#$MONTH))

    # Calculate next month boundary
    if [ "$MONTH_INT" -eq 12 ]; then
        NEXT_YEAR=$((YEAR + 1))
        NEXT_MONTH="01"
    else
        NEXT_YEAR=$YEAR
        NEXT_MONTH=$(printf "%02d" $((MONTH_INT + 1)))
    fi

    START_DATE="${YEAR}-${MONTH}-01"
    END_DATE="${NEXT_YEAR}-${NEXT_MONTH}-01"

    for TABLE in "${TABLES[@]}"; do
        PARTITION_NAME="${TABLE}_${YEAR}_${MONTH}"

        SQL="
        DO \$\$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relname = '${PARTITION_NAME}'
                AND n.nspname = 'public'
            ) THEN
                EXECUTE 'CREATE TABLE ${PARTITION_NAME} PARTITION OF ${TABLE} FOR VALUES FROM (''${START_DATE}'') TO (''${END_DATE}'')';
                RAISE NOTICE 'Created partition: ${PARTITION_NAME}';
            ELSE
                RAISE NOTICE 'Partition ${PARTITION_NAME} already exists, skipping.';
            END IF;
        END
        \$\$;
        "

        echo "  [${TABLE}] ${YEAR}-${MONTH} ... "
        psql "$DATABASE_URL" -c "$SQL" 2>&1 | grep -E "NOTICE|ERROR" || true
    done
    echo ""
done

echo "=== Partition creation complete ==="

# Show current partition summary
echo ""
echo "Current partitions:"
psql "$DATABASE_URL" -c "
SELECT
    parent.relname AS table_name,
    child.relname AS partition_name,
    pg_size_pretty(pg_total_relation_size(child.oid)) AS size,
    pg_stat_get_live_tuples(child.oid) AS live_rows
FROM pg_inherits
JOIN pg_class parent ON pg_inherits.inhparent = parent.oid
JOIN pg_class child ON pg_inherits.inhrelid = child.oid
WHERE parent.relname IN ('emails', 'inbound_emails', 'email_events')
ORDER BY parent.relname, child.relname;
"
