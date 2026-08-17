// Sunum icin uygulamadan ekran goruntusu alir.
// Kurulu Edge kullanilir (puppeteer kendi Chromium'unu indirmesin diye puppeteer-core).
//
// NOT: Veriler yuklenmeden yakalamak, MUI iskelet (skeleton) kutularini fotograflar.
// Bu yuzden her sayfada gercek bir icerik gorunene kadar beklenir.
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = 'http://localhost:5097';
const OUT = path.join(__dirname, 'ekran');

// Mock kimlik acikken token dogrudan verilebiliyor; parola bilinmeden goruntu alinir.
const MANAGER = 'mock:11111111-1111-1111-1111-111111111111';

/** Sayfada verilen metin gorunene kadar bekler — iskelet kutulari fotograflanmasin. */
async function waitForText(page, text, timeout = 20000) {
  await page.waitForFunction(
    (t) => document.body && document.body.innerText.includes(t),
    { timeout }, text);
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });

  const browser = await puppeteer.launch({
    executablePath: EDGE,
    headless: 'new',
    // --no-proxy-server: sirket proxy'si loopback'i de yakalayabiliyor.
    args: ['--hide-scrollbars', '--no-proxy-server'],
    defaultViewport: { width: 1440, height: 900, deviceScaleFactor: 2 },
  });

  const page = await browser.newPage();
  await page.evaluateOnNewDocument((t) => {
    localStorage.setItem('it-cockpit.token', t);
  }, MANAGER);

  const shots = [
    ['dashboard', '/manager/dashboard', 'Toplam açık ticket'],
    ['ticketlar', '/manager/tickets', 'Okunduğu kutu'],
    ['takvim', '/manager/team-schedule', 'Ekip'],
    ['calisan', '/employee/my-tickets', 'Ticket'],
  ];

  for (const [name, route, marker] of shots) {
    await page.goto(BASE + route, { waitUntil: 'networkidle2', timeout: 60000 });
    await waitForText(page, marker);
    await new Promise((r) => setTimeout(r, 2500));   // sayilar ve animasyonlar otursun
    await page.screenshot({ path: path.join(OUT, `${name}.png`) });
    console.log('alindi:', name);
  }

  // Yonetim: posta kutulari sekmesi
  await page.goto(BASE + '/manager/admin', { waitUntil: 'networkidle2', timeout: 60000 });
  await waitForText(page, 'Kullanıcılar');
  await new Promise((r) => setTimeout(r, 1500));
  await page.screenshot({ path: path.join(OUT, 'yonetim-kullanicilar.png') });
  console.log('alindi: yonetim-kullanicilar');

  await page.evaluate(() => {
    const tabs = [...document.querySelectorAll('button[role="tab"]')];
    if (tabs[1]) tabs[1].click();
  });
  await new Promise((r) => setTimeout(r, 2500));
  await page.screenshot({ path: path.join(OUT, 'yonetim-kutular.png') });
  console.log('alindi: yonetim-kutular');

  // Ticket detayi: ilk satira tiklanir, cekmece acilir
  await page.goto(BASE + '/manager/tickets', { waitUntil: 'networkidle2', timeout: 60000 });
  await waitForText(page, 'Okunduğu kutu');
  await new Promise((r) => setTimeout(r, 1500));
  await page.evaluate(() => {
    const row = document.querySelector('tbody tr');
    if (row) row.click();
  });
  await new Promise((r) => setTimeout(r, 2500));
  await page.screenshot({ path: path.join(OUT, 'ticket-detay.png') });
  console.log('alindi: ticket-detay');

  // Widget: masaustu ozet kutusu — kucuk pencere olcusunde
  const widget = await browser.newPage();
  await widget.evaluateOnNewDocument((t) => {
    localStorage.setItem('it-cockpit.token', t);
  }, MANAGER);
  await widget.setViewport({ width: 420, height: 520, deviceScaleFactor: 2 });
  await widget.goto(BASE + '/widget', { waitUntil: 'networkidle2', timeout: 60000 });
  await waitForText(widget, 'atanmamış');
  await new Promise((r) => setTimeout(r, 2000));
  await widget.screenshot({ path: path.join(OUT, 'widget.png') });
  console.log('alindi: widget');

  // Giris ekrani: token yok
  const login = await browser.newPage();
  await login.setViewport({ width: 1440, height: 900, deviceScaleFactor: 2 });
  await login.goto(BASE + '/login', { waitUntil: 'networkidle2', timeout: 60000 });
  await new Promise((r) => setTimeout(r, 2000));
  await login.screenshot({ path: path.join(OUT, 'giris.png') });
  console.log('alindi: giris');

  await browser.close();
})();
