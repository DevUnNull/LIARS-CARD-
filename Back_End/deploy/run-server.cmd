@echo off
rem Chạy backend trên máy này cho playit.gg: API :5000 (TCP), LiteNetLib :9050 (UDP).
rem Nạp biến môi trường từ server.env (Mongo Atlas + JWT key).
cd /d "%~dp0"
if not exist server.env (echo Thieu server.env & pause & exit /b 1)
for /f "usebackq eol=# tokens=1,* delims==" %%a in ("server.env") do set "%%a=%%b"
dotnet run --project ..\Back_End.csproj -c Release --no-launch-profile --urls http://127.0.0.1:5000
pause
