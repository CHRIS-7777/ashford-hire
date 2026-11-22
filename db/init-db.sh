#!/bin/bash
# One-shot initialiser: waits for SQL Server, then loads the schema and seed data once.
set -e
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
[ -x "$SQLCMD" ] || SQLCMD=/opt/mssql-tools/bin/sqlcmd

for i in $(seq 1 90); do
  if $SQLCMD -C -S db -U sa -P "$SA_PASSWORD" -Q "SELECT 1" -b -o /dev/null 2>/dev/null; then
    break
  fi
  echo "waiting for SQL Server ($i/90)..."
  sleep 2
done

EXISTS=$($SQLCMD -C -S db -U sa -P "$SA_PASSWORD" -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('AshfordHire') IS NULL THEN 0 ELSE 1 END" | tr -d '[:space:]')
if [ "$EXISTS" = "1" ]; then
  echo "AshfordHire already initialised - nothing to do"
  exit 0
fi

$SQLCMD -C -S db -U sa -P "$SA_PASSWORD" -b -i /init/init.sql \
  -v APP_DB_PASSWORD="$APP_DB_PASSWORD" RO_DB_PASSWORD="$RO_DB_PASSWORD"
echo "AshfordHire initialised"
