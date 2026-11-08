@echo off
rem Mở server cho mọi người chơi: backend + Cloudflare tunnel (API) + kiểm tra playit (UDP).
cd /d "%~dp0"
"C:\Program Files\playit_gg\bin\playit.exe" start >nul 2>&1
start "Backend - giu cua so nay mo" cmd /c run-server.cmd
call run-tunnel.cmd
