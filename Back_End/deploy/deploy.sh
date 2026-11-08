#!/usr/bin/env bash
# Build backend và đẩy lên VM Oracle (Ubuntu). Chạy lại mỗi lần muốn cập nhật code.
# Dùng: bash Back_End/deploy/deploy.sh <IP_VM> <duong_dan_ssh_key> [linux-x64|linux-arm64] [user_ssh]
set -euo pipefail

HOST="${1:?Thiếu IP VM}"
KEY="${2:?Thiếu đường dẫn private key SSH}"
RID="${3:-linux-x64}"     # Azure B1s / Oracle E2 = linux-x64, Oracle Ampere A1 = linux-arm64
USR="${4:-azureuser}"     # Azure = azureuser, Oracle/AWS Ubuntu = ubuntu
DIR="$(cd "$(dirname "$0")" && pwd)"
SSH="ssh -i $KEY -o StrictHostKeyChecking=accept-new $USR@$HOST"

[ -f "$DIR/server.env" ] || { echo "Thiếu $DIR/server.env (copy từ server.env.example)"; exit 1; }

# Self-contained: VM không cần cài .NET runtime
rm -rf "$DIR/out"
dotnet publish "$DIR/../Back_End.csproj" -c Release -r "$RID" --self-contained true -o "$DIR/out"
tar -czf "$DIR/app.tgz" -C "$DIR/out" .

scp -i "$KEY" "$DIR/app.tgz" "$DIR/server.env" "$DIR/liarscard.service" "$DIR/remote-setup.sh" "$USR@$HOST:/tmp/"
$SSH "sudo bash /tmp/remote-setup.sh"

rm -rf "$DIR/out" "$DIR/app.tgz"
echo "Xong. Thử: curl http://$HOST:5000/api/auth/me  (401 là server đã chạy)"
