@echo off
setlocal
title IT Yonetim Paneli - Baslatici

rem .NET 8 SDK ve Node.js bu makinede kullanici dizinine kuruldu (yonetici izni gerekmesin diye),
rem bu yuzden PATH'e burada ekleniyorlar. Makine geneline kurulurlarsa bu satirlar zararsizdir.
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\nodejs-portable\node.exe" set "PATH=%LOCALAPPDATA%\nodejs-portable;%PATH%"

set "ASPNETCORE_ENVIRONMENT=Development"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "KOK=%~dp0"

echo.
echo   IT Yonetim Paneli baslatiliyor...
echo   Iki pencere acilacak: API ve arayuz. Kapatmak icin o pencereleri kapatin.
echo.

rem Zaten calisiyorsa yeniden baslatma, dogrudan tarayiciyi ac.
powershell -NoProfile -Command "try { Invoke-WebRequest 'http://localhost:5173' -TimeoutSec 2 -UseBasicParsing | Out-Null; exit 0 } catch { exit 1 }"
if not errorlevel 1 (
  echo   Uygulama zaten calisiyor, tarayici aciliyor.
  start http://localhost:5173
  ping -n 4 127.0.0.1 >nul
  exit /b 0
)

sc query MSSQLSERVER | find "RUNNING" >nul 2>&1
if errorlevel 1 echo   [UYARI] MSSQLSERVER servisi calismiyor gorunuyor. Veritabani hatasi alirsaniz once onu baslatin.

rem Not: cmd /k icindeki komutta ic ice tirnak kullanilmaz, aksi halde satir bozulur.
start "IT Cockpit - API" cmd /k pushd %KOK%backend ^&^& dotnet run --project src\ItCockpit.Api --urls http://localhost:5080 --no-launch-profile
start "IT Cockpit - Arayuz" cmd /k pushd %KOK%frontend ^&^& npm run dev

echo   Sunucularin acilmasi bekleniyor (ilk calistirmada 1-2 dakika surebilir)...
powershell -NoProfile -Command "$d=(Get-Date).AddSeconds(180); do { try { Invoke-WebRequest 'http://localhost:5173' -TimeoutSec 2 -UseBasicParsing | Out-Null; try { Invoke-RestMethod 'http://localhost:5080/health' -TimeoutSec 2 | Out-Null; exit 0 } catch {} } catch {}; Start-Sleep -Milliseconds 2000 } while ((Get-Date) -lt $d); exit 1"

if errorlevel 1 goto :hata

start http://localhost:5173
echo.
echo   Hazir:       http://localhost:5173
echo   API/Swagger: http://localhost:5080/swagger
echo.
ping -n 6 127.0.0.1 >nul
exit /b 0

:hata
echo.
echo   [HATA] Sunucular acilamadi. Acilan iki pencerede yazan hata mesajina bakin.
echo          En sik sebep: SQL Server kapali.
echo.
pause
exit /b 1

