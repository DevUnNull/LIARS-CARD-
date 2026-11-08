#!/usr/bin/env bash
# Đợi cloudflared in URL vào tunnel.log, ghi vào server.json rồi push lên GitHub để client tự lấy.
set -euo pipefail
cd "$(dirname "$0")/../.."
LOG=Back_End/deploy/tunnel.log

echo "Đang đợi Cloudflare cấp URL..."
for _ in $(seq 1 60); do
  URL=$(grep -oE "https://[a-z0-9-]+\.trycloudflare\.com" "$LOG" 2>/dev/null | tail -1 || true)
  [ -n "$URL" ] && break
  sleep 1
done
[ -n "${URL:-}" ] || { echo "Không lấy được URL, xem cửa sổ cloudflared"; exit 1; }
echo "URL: $URL"

sed -i -E "s#\"apiBaseUrl\": *\"[^\"]*\"#\"apiBaseUrl\": \"$URL\"#" server.json
if git diff --quiet -- server.json; then echo "URL không đổi"; exit 0; fi
git commit -m "chore: update server URL" -- server.json
# main trên GitHub thường có commit mới của nhóm, phải kéo về trước khi push
git pull --rebase --autostash origin main || { echo "Pull bị xung đột, xử lý git rồi chạy lại"; exit 1; }
git push origin HEAD:main
echo "Đã đẩy lên GitHub. Client mới mở game sau vài phút sẽ dùng URL này."
