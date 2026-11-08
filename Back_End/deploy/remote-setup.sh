#!/usr/bin/env bash
# Chạy trên VM (deploy.sh tự gọi). Idempotent: chạy lại nhiều lần không sao.
set -euo pipefail

# Image Ubuntu của Oracle chặn mọi cổng bằng iptables, kể cả khi Security List đã mở
for rule in "-p tcp --dport 5000" "-p udp --dport 9050"; do
  iptables -C INPUT $rule -j ACCEPT 2>/dev/null || iptables -I INPUT $rule -j ACCEPT
done
command -v netfilter-persistent >/dev/null || { apt-get update -qq; DEBIAN_FRONTEND=noninteractive apt-get install -y iptables-persistent; }
netfilter-persistent save

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
