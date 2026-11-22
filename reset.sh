#!/usr/bin/env bash
# Reset the lab to a clean state: removes containers (and all data) and rebuilds from scratch.
set -euo pipefail
cd "$(dirname "$0")"
echo "[*] Stopping and removing Ashford Hire containers..."
docker compose down -v --remove-orphans
echo "[*] Rebuilding and starting (the database needs ~30-60s on first boot)..."
docker compose up -d --build
echo "[*] Waiting for the portal..."
PORT=$(grep -E '^LAB_PORT=' .env | cut -d= -f2)
for i in $(seq 1 90); do
  if curl -fs "http://127.0.0.1:${PORT:-8100}/healthz" >/dev/null 2>&1; then
    echo "[+] Ready: http://$(hostname -I | awk '{print $1}'):${PORT:-8100}/"
    exit 0
  fi
  sleep 2
done
echo "[!] Portal did not come up in time. Check: docker compose logs web"
exit 1
