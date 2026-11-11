#!/usr/bin/env bash
# Chạy trên VM (deploy.sh tự gọi). Idempotent: chạy lại nhiều lần không sao.
# Dùng: sudo bash remote-setup.sh <IP_public_VM>
set -euo pipefail
IP="${1:?Thiếu IP public của VM}"
DOMAIN="${IP//./-}.sslip.io"   # sslip.io trỏ tên này về đúng IP → Caddy xin được chứng chỉ HTTPS miễn phí

# e2-micro chỉ có 1GB RAM: thêm 1GB swap để publish/khởi động không bị OOM
if [ ! -f /swapfile ]; then
  fallocate -l 1G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

command -v caddy >/dev/null || { apt-get update -qq; DEBIAN_FRONTEND=noninteractive apt-get install -y caddy; }
cat > /etc/caddy/Caddyfile <<EOF
$DOMAIN {
    reverse_proxy 127.0.0.1:5000
}
EOF
systemctl enable caddy
systemctl reload caddy || systemctl restart caddy

id liarscard >/dev/null 2>&1 || useradd --system --no-create-home liarscard
systemctl stop liarscard 2>/dev/null || true
rm -rf /opt/liarscard && mkdir -p /opt/liarscard
tar -xzf /tmp/app.tgz -C /opt/liarscard
chmod +x /opt/liarscard/Back_End
chown -R liarscard /opt/liarscard

install -m 600 -o root /tmp/server.env /etc/liarscard.env
cp /tmp/liarscard.service /etc/systemd/system/liarscard.service
systemctl daemon-reload
systemctl enable --now liarscard
rm -f /tmp/app.tgz /tmp/server.env

sleep 3
systemctl --no-pager status liarscard | head -15
echo "API: https://$DOMAIN   UDP: $IP:9050"
