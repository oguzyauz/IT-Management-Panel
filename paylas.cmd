@echo off
setlocal
title IT Yonetim Paneli - Ag uzerinden paylas

rem Uygulamayi yerel agdaki diger bilgisayarlardan erisilebilir hale getirir.
rem Yalnizca 5173 disari acilir; API (5080) disari acilmaz - arayuz sunucusu
rem /api isteklerini kendi uzerinden 5080'e yonlendirir.

if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\nodejs-portable\node.exe" set "PATH=%LOCALAPPDATA%\nodejs-portable;%PATH%"

set "ASPNETCORE_ENVIRONMENT=Development"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "KOK=%~dp0"

echo.
echo   ============================================================
echo    DIKKAT: Bu GELISTIRME modudur ^(Auth:Provider = Mock^).
echo    Parola sorulmaz; giris ekraninda kullanici listeden secilir,
echo    yani bu adresi verdiginiz herkes Ahmet Yilmaz olarak girebilir.
echo.
echo    Ekiple gercek kullanim icin tasinabilir paketteki Paylas.cmd
echo    dosyasini kullanin ^(parola ile giris acik^):
echo       powershell -ExecutionPolicy Bypass -File scripts\paket-olustur.ps1
echo   ============================================================
echo.

powershell -NoProfile -Command "try { Invoke-WebRequest 'http://localhost:5173' -TimeoutSec 2 -UseBasicParsing | Out-Null; exit 0 } catch { exit 1 }"
if not errorlevel 1 (
  echo   [UYARI] 5173 portu zaten kullanimda. Once baslat.cmd ile acilan
  echo           pencereleri kapatin, sonra bu dosyayi calistirin.
  echo.
  pause
  exit /b 1
)

start "IT Cockpit - API" cmd /k pushd %KOK%backend ^&^& dotnet run --project src\ItCockpit.Api --urls http://localhost:5080 --no-launch-profile
start "IT Cockpit - Arayuz (ag)" cmd /k pushd %KOK%frontend ^&^& npm run dev:lan

echo   Sunucular baslatiliyor...
powershell -NoProfile -Command "$d=(Get-Date).AddSeconds(180); do { try { Invoke-WebRequest 'http://localhost:5173' -TimeoutSec 2 -UseBasicParsing | Out-Null; exit 0 } catch {}; Start-Sleep -Milliseconds 2000 } while ((Get-Date) -lt $d); exit 1"
if errorlevel 1 goto :hata

echo.
echo   Paylasilacak adres:
powershell -NoProfile -Command "Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object { '      http://' + $_.IPAddress + ':5173' }"
echo.
echo   Karsi taraf baglanamiyorsa guvenlik duvari kuralini acmaniz gerekir.
echo   PowerShell'i YONETICI olarak acip su komutu calistirin:
echo.
echo      New-NetFirewallRule -DisplayName "IT Cockpit (5173)" -Direction Inbound -Protocol TCP -LocalPort 5173 -Action Allow -Profile Domain,Private
echo.
echo   Paylasimi bitirdiginizde kurali kaldirmak icin:
echo.
echo      Remove-NetFirewallRule -DisplayName "IT Cockpit (5173)"
echo.
ping -n 6 127.0.0.1 >nul
exit /b 0

:hata
echo.
echo   [HATA] Sunucular acilamadi. Acilan pencerelerdeki mesaja bakin.
echo.
pause
exit /b 1
