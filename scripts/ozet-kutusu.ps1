<#
    Masaüstü özet kutusunu açar.

    Tarayıcıyı "uygulama modunda" (adres çubuğu, sekme yok) küçük bir pencerede başlatır,
    ardından Win32 SetWindowPos ile pencereyi "her zaman üstte" yapar. Chromium tabanlı
    tarayıcıların kendi bayrağı bunu sunmadığı için pencere yönetimi işletim sistemi
    tarafında yapılır.
#>

$ErrorActionPreference = 'Stop'

$Url    = 'http://localhost:5173/widget'
$Width  = 300
$Height = 380

# --- Uygulama ayakta mı? -----------------------------------------------------------------
try {
    Invoke-WebRequest 'http://localhost:5173' -TimeoutSec 3 -UseBasicParsing | Out-Null
}
catch {
    Write-Host ''
    Write-Host '  [HATA] Uygulama calismiyor (http://localhost:5173).' -ForegroundColor Red
    Write-Host '         Once baslat.cmd dosyasini calistirin.'
    Write-Host ''
    exit 1
}

# --- Tarayıcıyı bul ----------------------------------------------------------------------
$candidates = @(
    "$env:ProgramFiles (x86)\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
)

$browser = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $browser) {
    Write-Host '  [HATA] Edge veya Chrome bulunamadi.' -ForegroundColor Red
    exit 1
}

# Ayrı bir kullanıcı profili kullanılmaz: özet kutusunun oturum bilgisi (localStorage)
# ana uygulamayla aynı olmalı, aksi hâlde yeniden giriş istenir.
$args = @(
    "--app=$Url"
    "--window-size=$Width,$Height"
    '--window-position=40,40'
)

Write-Host "  Ozet kutusu aciliyor ($([System.IO.Path]::GetFileName($browser)))..."
$proc = Start-Process -FilePath $browser -ArgumentList $args -PassThru

# --- Pencereyi her zaman üstte yap -------------------------------------------------------
if (-not ('Win32Window' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;

public class Win32Window
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE     = 0x0001;
    public const uint SWP_NOMOVE     = 0x0002;
    public const uint SWP_SHOWWINDOW = 0x0040;
}
'@
}

# Tarayıcı penceresini oluşturana kadar bekle. Edge/Chrome mevcut bir örneğe devrederse
# başlattığımız süreç hemen kapanabilir; o durumda başlıktan pencereyi ararız.
$handle = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(25)

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 700

    if (-not $proc.HasExited) {
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $handle = $proc.MainWindowHandle; break }
    }

    # Başlık eşleşmesi bilinçli olarak ASCII: pencere başlığı "IT Yönetim Paneli" olduğu için
    # 'Paneli' parçası hem yeterince ayırt edici hem de kodlama sorunlarından etkilenmez.
    $found = Get-Process -Name msedge, chrome -ErrorAction SilentlyContinue |
             Where-Object { $_.MainWindowTitle -like '*Paneli*' } |
             Select-Object -First 1

    if ($found) { $handle = $found.MainWindowHandle; break }
}

if ($handle -ne [IntPtr]::Zero) {
    [Win32Window]::SetWindowPos(
        $handle, [Win32Window]::HWND_TOPMOST, 0, 0, 0, 0,
        [Win32Window]::SWP_NOMOVE -bor [Win32Window]::SWP_NOSIZE -bor [Win32Window]::SWP_SHOWWINDOW) | Out-Null

    Write-Host '  Hazir. Pencere her zaman ustte.' -ForegroundColor Green
}
else {
    # Pencere bulunamadıysa kutu yine de açıktır, sadece üstte sabitlenemedi.
    Write-Host '  Ozet kutusu acildi, ancak "her zaman ustte" ayarlanamadi.' -ForegroundColor Yellow
}

Start-Sleep -Seconds 2
