<#
    Taşınabilir paket üretir.

    Çıktı klasörü tek başına çalışır: hedef makinede .NET, Node.js veya SQL Server
    kurulu olması gerekmez. Veritabanı klasörün içinde tek bir SQLite dosyasıdır.

    Kullanım:  powershell -ExecutionPolicy Bypass -File scripts\paket-olustur.ps1
#>

$ErrorActionPreference = 'Stop'

$repo    = Split-Path $PSScriptRoot -Parent
$api     = Join-Path $repo 'backend\src\ItCockpit.Api'
$web     = Join-Path $repo 'frontend'
$wwwroot = Join-Path $api 'wwwroot'
$output  = Join-Path $repo 'paket\IT-Yonetim-Paneli'

# Kullanıcı dizinine kurulan araçlar PATH'e alınır.
if (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe") {
    $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
}
if (Test-Path "$env:LOCALAPPDATA\nodejs-portable\node.exe") {
    $env:PATH = "$env:LOCALAPPDATA\nodejs-portable;$env:PATH"
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Write-Host ''
Write-Host '  [1/4] Arayuz derleniyor...' -ForegroundColor Cyan
Push-Location $web
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw "Arayuz derlemesi basarisiz (exit $LASTEXITCODE)" }
}
finally { Pop-Location }

Write-Host '  [2/4] Arayuz API icine kopyalaniyor...' -ForegroundColor Cyan
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item (Join-Path $web 'dist\*') $wwwroot -Recurse -Force

Write-Host '  [3/4] API self-contained yayinlaniyor (bu adim uzun surer)...' -ForegroundColor Cyan

# Cikti klasoru kullanimda olabilir: birisi paketi bu klasorden calistirdiysa icinde
# canli veritabani ve Google jetonlari durur. Silmek onlari yok eder.
$liveDb = Join-Path $output 'it-cockpit.db'
if (Test-Path $liveDb) {
    Write-Host ''
    Write-Host '  [DUR] Cikti klasorunde CANLI VERITABANI var:' -ForegroundColor Red
    Write-Host "        $liveDb"
    Write-Host ''
    Write-Host '  Bu klasor birileri tarafindan kullaniliyor. Devam edilirse kullanicilar,'
    Write-Host '  parolalar, ticketlar ve Google onaylari silinir.'
    Write-Host ''
    Write-Host '  Yapilacak: klasoru baska bir yere tasiyin ya da yedekleyin,'
    Write-Host '  sonra bu betigi tekrar calistirin.'
    Write-Host ''
    throw 'Cikti klasorunde canli veritabani bulundu; paket uretimi durduruldu.'
}

if (Test-Path $output) { Remove-Item $output -Recurse -Force }

# Not: PublishTrimmed kullanilmiyor. EF Core ve ASP.NET Core yansima (reflection) ile
# calistigi icin trimming calisma aninda kirilmalara yol acar; boyut kazanci bu riski hak etmiyor.
& dotnet publish (Join-Path $api 'ItCockpit.Api.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:SatelliteResourceLanguages=en `
    -o $output

if ($LASTEXITCODE -ne 0) { throw "dotnet publish basarisiz (exit $LASTEXITCODE)" }

Write-Host '  [4/4] Paket ayarlari ve baslatici yaziliyor...' -ForegroundColor Cyan

# Son kullanici kurulumu:
#   - SQLite: veritabani klasorun icinde tek dosya, SQL Server gerekmez
#   - Auth = Local: parola ile giris. Ilk acilista yonetici parolasini belirler.
#   - Gmail = Google: gercek posta kutusu okunur. Kutular arayuzden eklenir ve
#     yetkilendirilir; appsettings'e kutu yazilmaz.
#   - Hangfire acik (bellek ici): mailler arka planda periyodik okunur.
$settings = [ordered]@{
    ConnectionStrings = [ordered]@{ Sqlite = 'Data Source=it-cockpit.db' }
    Database          = [ordered]@{ Provider = 'Sqlite'; MigrateOnStartup = $true; SeedOnStartup = $true }
    Auth              = [ordered]@{ Provider = 'Local' }
    # MailboxAddress acikca bosaltilir: temel appsettings.json'daki ornek adres
    # devralinirsa, yetkilendirilmemis oldugu icin kullanici daha hicbir sey
    # yapmadan ilk okumada hata gorur.
    Gmail             = [ordered]@{
        Provider            = 'Google'
        MailboxAddress      = ''
        Mailboxes           = @()
        InitialLookbackDays = 30
    }
    Reminders         = [ordered]@{ Provider = 'Mock' }
    Hangfire          = [ordered]@{ EnableServer = $true; UseMemoryStorage = $true }
}

$settings | ConvertTo-Json -Depth 5 |
    Set-Content (Join-Path $output 'appsettings.Production.json') -Encoding UTF8

# Gelistirme ayarlari pakete sizmamali (gercek kutu adresleri, SQL Server yolu vb.)
Remove-Item (Join-Path $output 'appsettings.Development.json') -Force -ErrorAction SilentlyContinue

# Token deposu KOPYALANMAZ: bu dosyalar gelistirme makinesindeki hesaplarin
# Gmail erisim jetonlaridir. Kullanici kendi onayini kendi makinesinde verir.
Remove-Item (Join-Path $output 'token-store') -Recurse -Force -ErrorAction SilentlyContinue

# OAuth istemci tanimi. Desktop app istemcisinde client_secret gizli kabul edilmez
# (Google'in kendi dokumani boyle tanimlar); yine de kullanicinin postasina erisim
# ancak o kullanici tarayicida onay verdikten sonra mumkun olur.
$credentials = Join-Path $api 'credentials.json'
if (Test-Path $credentials) {
    Copy-Item $credentials $output -Force
}
else {
    Write-Host '  UYARI: credentials.json bulunamadi.' -ForegroundColor Yellow
    Write-Host '         Paket calisir ama gercek Gmail baglanamaz; Yonetim ekrani'
    Write-Host '         dosyanin nasil ekleneceğini adim adim gosterir.'
}

$launcher = @'
@echo off
setlocal
title IT Yonetim Paneli

rem Calisma dizini bu dosyanin klasoru olmali: SQLite veritabani ve wwwroot
rem goreli yollarla bulunuyor.
rem NOT: "cd /d %~dp0" KULLANILMAZ. %~dp0 ters boluyle bittigi icin tirnak
rem kacisi bozulur ve dizin degismez; exe bulunamaz (hata 9009).
pushd "%~dp0."
set "KOK=%~dp0"
set ASPNETCORE_ENVIRONMENT=Production

echo.
echo   IT Yonetim Paneli
echo   ------------------------------------------------------------
echo.

rem 1) ZIP icinden calistirma kontrolu. Windows zip icerigini klasor gibi
rem    gosterir; oradan calistirilirsa yanindaki dosyalar bulunamaz.
if not exist "%KOK%ItCockpit.Api.exe" (
  echo   [HATA] ItCockpit.Api.exe bulunamadi.
  echo.
  echo   Muhtemel sebep: ZIP dosyasini ACMADAN, icinden calistirdiniz.
  echo   Yapmaniz gereken:
  echo     1^) ZIP dosyasina sag tiklayin
  echo     2^) "Tumunu ayikla" ^(Extract All^) secin
  echo     3^) Ayiklanan klasordeki Baslat.cmd dosyasini calistirin
  echo.
  pause
  exit /b 1
)

rem 2) Internetten indirilen dosyalarda Windows "engellendi" isareti birakir
rem    ve uygulama acilmaz. Isaret kaldiriliyor.
echo   Dosyalar hazirlaniyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path . -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue" >nul 2>&1

rem 3) Bos port bul (5080 doluysa sirayla dene)
set PORT=
for %%p in (5080 5081 5082 5083 5090) do (
  if not defined PORT (
    netstat -ano | findstr /r /c:":%%p .*LISTENING" >nul 2>&1
    if errorlevel 1 set PORT=%%p
  )
)
if not defined PORT (
  echo   [HATA] Uygun bir port bulunamadi.
  pause
  exit /b 1
)

echo   Adres: http://localhost:%PORT%
echo   Bu pencereyi kapatirsaniz uygulama durur.
echo.
echo   Ilk calistirmada veritabani olusturulur, bu 10-30 saniye surebilir.
echo   Tarayici HAZIR OLUNCA kendiliginden acilacak, beklemeniz yeterli.
echo.

rem Tarayici, sunucu gercekten cevap verdiginde acilir. Onceden acilirsa
rem "bu sayfaya ulasilamiyor" hatasi goruluyordu.
rem NOT: Buraya BORU (^|) YAZILMAZ. Cmd, tirnak icindeki ^ isaretini kacis
rem olarak islemez; PowerShell "^" karakterini Invoke-WebRequest'e konumsal
rem argüman sanip her turda hata firlatir ve tarayici hic acilmaz.
rem Ciktiyi susturmak icin "$null =" atamasi kullanilir.
start "" /min powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$d=(Get-Date).AddSeconds(180); do { try { $null = Invoke-WebRequest 'http://localhost:%PORT%/health' -TimeoutSec 2 -UseBasicParsing; Start-Process 'http://localhost:%PORT%'; exit 0 } catch { Start-Sleep -Milliseconds 1000 } } while ((Get-Date) -lt $d); exit 1"

"%KOK%ItCockpit.Api.exe" --urls http://localhost:%PORT%

set CODE=%ERRORLEVEL%
popd
echo.
if not "%CODE%"=="0" (
  echo   ------------------------------------------------------------
  echo   [HATA] Uygulama beklenmedik sekilde kapandi. Kod: %CODE%
  echo.
  echo   Yukaridaki mesaji IT sorumlunuza iletin.
  echo   ------------------------------------------------------------
)
pause
'@
Set-Content (Join-Path $output 'Baslat.cmd') $launcher -Encoding ASCII

# Ag paylasimi: uygulama tum arayuzlere baglanir, ekip tarayicidan girer.
# Baslat.cmd yalnizca localhost'a baglandigi icin ayri bir dosya gerekiyor.
$share = @'
@echo off
setlocal
title IT Yonetim Paneli - ag uzerinden paylas

pushd "%~dp0."
set "KOK=%~dp0"
set ASPNETCORE_ENVIRONMENT=Production
set PORT=5080

echo.
echo   IT Yonetim Paneli - AG PAYLASIMI
echo   ------------------------------------------------------------
echo.
echo   Uygulama bu bilgisayarda calisir, ekip tarayicidan baglanir.
echo   Bu pencere kapaninca herkesin erisimi kesilir.
echo.

if not exist "%KOK%ItCockpit.Api.exe" (
  echo   [HATA] ItCockpit.Api.exe bulunamadi.
  echo   ZIP'i once "Tumunu ayikla" ile acmaniz gerekiyor.
  echo.
  pause
  exit /b 1
)

echo   Ekibe verilecek adres^(ler^):
powershell -NoProfile -Command "Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object { '      http://' + $_.IPAddress + ':%PORT%' }"
echo.
echo   Karsi taraf baglanamiyorsa guvenlik duvari kuralini acin.
echo   PowerShell'i YONETICI olarak acip:
echo.
echo      New-NetFirewallRule -DisplayName "IT Yonetim Paneli" -Direction Inbound -Protocol TCP -LocalPort %PORT% -Action Allow -Profile Domain,Private
echo.
echo   Paylasimi bitirince kaldirmak icin:
echo.
echo      Remove-NetFirewallRule -DisplayName "IT Yonetim Paneli"
echo.
echo   ------------------------------------------------------------
echo.

start "" /min powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$d=(Get-Date).AddSeconds(180); do { try { $null = Invoke-WebRequest 'http://localhost:%PORT%/health' -TimeoutSec 2 -UseBasicParsing; Start-Process 'http://localhost:%PORT%'; exit 0 } catch { Start-Sleep -Milliseconds 1000 } } while ((Get-Date) -lt $d); exit 1"

rem 0.0.0.0: yalnizca localhost degil, agdaki tum arayuzler dinlenir.
"%KOK%ItCockpit.Api.exe" --urls http://0.0.0.0:%PORT%

set CODE=%ERRORLEVEL%
popd
echo.
if not "%CODE%"=="0" (
  echo   [HATA] Uygulama beklenmedik sekilde kapandi. Kod: %CODE%
)
pause
'@
Set-Content (Join-Path $output 'Paylas.cmd') $share -Encoding ASCII

$readme = @'
IT Yonetim Paneli
=================

ONEMLI - ONCE ZIP'I AYIKLAYIN
-----------------------------
ZIP dosyasina sag tiklayin, "Tumunu ayikla" (Extract All) secin.
ZIP'in icinden dogrudan calistirmayin, acilmaz.

Ayikladiktan sonra klasordeki Baslat.cmd dosyasina cift tiklayin.
Sunucu hazir olunca tarayici kendiliginden acilir.


ILK ACILIS
----------
1) Karsiniza "Ilk kurulum" ekrani gelir.
2) Yonetici e-postasini ve kendi belirleyeceginiz parolayi girin.
   Parola en az 8 karakter olmali. Bu parolayi kimseyle paylasmayin.
3) Kurulum bitince panele girmis olursunuz.


GMAIL'I BAGLAMA
---------------
Sol menuden "Yonetim" -> "Posta kutulari":
1) Okunacak posta kutusunun adresini yazip "Ekle" deyin.
2) Yanindaki "Yetkilendir" dugmesine basin.
3) Tarayicida Google onay ekrani acilir. Hesabi secin.
4) "Google bu uygulamayi dogrulamadi" uyarisi cikarsa
   "Gelismis" (Advanced) -> "... uygulamasina git" secin.
   Bu uyari, uygulamanin Google tarafindan incelenmemis olmasindan
   kaynaklanir; uygulama postalarinizi yalnizca OKUR, degistirmez.
5) Onay verdikten sonra "Mailleri simdi oku" ile ilk okumayi yapin.

Her posta kutusu icin bu adimlar ayri ayri tekrarlanir.
Sonrasinda mailler arka planda otomatik okunur.


KULLANICI EKLEME
----------------
"Yonetim" -> "Kullanicilar" -> "Kullanici ekle".
Belirlediginiz baslangic parolasini kisiye iletin; ilk girisinde
kendi parolasini belirlemesi istenir.

Roller:
  Calisan  - yalnizca kendine atanan ticket'lari gorur ve durum gunceller
  Yonetici - tum ticket'lar, atama, hatirlatma, ekip takvimi
  Sistem yoneticisi - tum yetkiler


EKIBIN AYNI PANELE BAGLANMASI
-----------------------------
Uygulama kimin bilgisayarinda calisiyorsa veriler orada durur.
Ekibin ayni verileri gormesi icin uygulamayi TEK bilgisayarda calistirin
ve digerleri tarayicidan baglansin:

1) Baslat.cmd yerine Paylas.cmd dosyasini calistirin.
2) Ekranda yazan adresi (ornek: http://192.168.1.25:5080) ekibe verin.
3) O bilgisayar acik ve uygulama calisir durumda olmali.

Ilk seferde Windows Guvenlik Duvari izin isteyebilir; "Ozel aglar" icin
izin verin.


"WINDOWS BILGISAYARINIZI KORUDU" UYARISI
----------------------------------------
Uygulama imzali olmadigi icin Windows uyarabilir.
"Daha fazla bilgi" (More info) -> "Yine de calistir" (Run anyway).
Antivirus engellerse IT sorumlunuza danisin.


YEDEKLEME
---------
Butun veriler klasordeki it-cockpit.db dosyasindadir.
Yedek almak icin uygulamayi KAPATIN ve su uc dosyayi kopyalayin:
  it-cockpit.db
  it-cockpit.db-wal   (varsa)
  it-cockpit.db-shm   (varsa)

Geri yuklemek icin ayni dosyalari yerine koyup tekrar baslatin.
Sifirdan baslamak icin bu dosyalari silin.


BU KLASOR KENDI KENDINE YETERLIDIR
----------------------------------
  - .NET kurulumu gerekmez
  - Node.js gerekmez
  - SQL Server gerekmez


BILINMESI GEREKENLER
--------------------
- Bu panel Tixbox'a HICBIR SEY YAZMAZ. Buradaki durumlar yalnizca
  yonetim panelindeki takip durumudur.
- Panel "SLA" veya hedef tarih hesaplamaz; Tixbox'ta bu veri yok.
  Yalnizca "kac gundur acik / kac gundur guncellenmedi" gosterilir.
- Hatirlatma maili yoneticinin acik onayi olmadan gonderilmez.

Kapatmak: acilan siyah pencereyi kapatin.
'@
Set-Content (Join-Path $output 'OKUBENI.txt') $readme -Encoding UTF8

# Ayrintili kilavuz da pakete girer; OKUBENI hizli baslangic, bu ise tam anlatim.
$guide = Join-Path $repo 'docs\kullanim-kilavuzu.md'
if (Test-Path $guide) {
    Copy-Item $guide (Join-Path $output 'KULLANIM-KILAVUZU.md') -Force
}

$size = [math]::Round((Get-ChildItem $output -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)

Write-Host '  [5/5] Zip olusturuluyor...' -ForegroundColor Cyan
$zip = Join-Path (Split-Path $output -Parent) 'IT-Yonetim-Paneli.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $output -DestinationPath $zip -CompressionLevel Optimal

$zipSize = [math]::Round((Get-Item $zip).Length / 1MB, 1)

Write-Host ''
Write-Host "  Klasor : $output  ($size MB)" -ForegroundColor Green
Write-Host "  ZIP    : $zip  ($zipSize MB)" -ForegroundColor Green
Write-Host ''

if ($zipSize -gt 25) {
    Write-Host '  NOT: ZIP 25 MB ustunde, Gmail eke sigmaz.' -ForegroundColor Yellow
    Write-Host '       Gmail dosyayi otomatik olarak Drive baglantisina cevirir,'
    Write-Host '       ya da USB / ortak klasor ile verebilirsiniz.'
    Write-Host ''
}
