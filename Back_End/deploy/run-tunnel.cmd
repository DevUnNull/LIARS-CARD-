@echo off
rem Mở API localhost:5000 ra Internet qua Cloudflare Quick Tunnel, rồi tự cập nhật server.json trên GitHub.
cd /d "%~dp0"
taskkill /F /IM cloudflared.exe >nul 2>&1
del tunnel.log >nul 2>&1
start "cloudflared - giu cua so nay mo" "C:\Program Files (x86)\cloudflared\cloudflared.exe" tunnel --no-autoupdate --url http://127.0.0.1:5000 --logfile "%~dp0tunnel.log"
"C:\Program Files\Git\bin\bash.exe" publish-url.sh
pause
