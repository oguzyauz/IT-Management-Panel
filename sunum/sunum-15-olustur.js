// Ahmet Bey icin genis durum sunumu: ekran goruntuleri + teknik detay + yasanan hatalar.
// Tema ilk sunumla ayni (uygulamanin kendi renkleri).
const pptxgen = require('pptxgenjs');
const path = require('path');

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';           // 13.33 x 7.5
pres.author = 'Lara Karaahmet';
pres.title = 'Mail Tabanlı IT Yönetim Paneli — Durum Sunumu';

const C = {
  deep:   '1B4965',
  mid:    '2E6E8E',
  light:  '62B6CB',
  amber:  'C9722B',
  green:  '2E7D32',
  red:    'B3261E',
  purple: '5B4B8A',
  tint:   'EEF4F7',
  tintWarm:'FBF1E8',
  tintGreen:'ECF4ED',
  ink:    '1A2027',
  muted:  '5A6872',
  white:  'FFFFFF',
};

const F = { head: 'Cambria', body: 'Calibri' };

const M = 0.6;
const W = 13.33;
const CONTENT_W = W - 2 * M;
const SS = (name) => path.join(__dirname, 'ekran', name);

// ---------------------------------------------------------------- yardımcılar

function titleSlide(s, kicker, title, sub) {
  s.background = { color: C.deep };
  s.addText(kicker, {
    x: M, y: 1.75, w: CONTENT_W, h: 0.35,
    fontFace: F.body, fontSize: 13, color: C.light, charSpacing: 3, bold: true, margin: 0,
  });
  s.addText(title, {
    x: M, y: 2.2, w: CONTENT_W, h: 1.6,
    fontFace: F.head, fontSize: 44, bold: true, color: C.white, margin: 0,
  });
  s.addText(sub, {
    x: M, y: 3.9, w: CONTENT_W, h: 0.9,
    fontFace: F.body, fontSize: 16, color: 'B9D4E2', margin: 0,
  });
}

function header(s, title, sub) {
  s.addText(title, {
    x: M, y: 0.42, w: CONTENT_W, h: 0.62,
    fontFace: F.head, fontSize: 32, bold: true, color: C.deep, margin: 0,
  });
  if (sub) {
    s.addText(sub, {
      x: M, y: 1.02, w: CONTENT_W, h: 0.38,
      fontFace: F.body, fontSize: 13.5, color: C.muted, margin: 0,
    });
  }
}

function card(s, x, y, w, h, opts) {
  const o = opts || {};
  s.addShape(pres.ShapeType.roundRect, {
    x, y, w, h, rectRadius: 0.08,
    fill: { color: o.fill || C.tint },
    line: { color: o.line || 'DCE7ED', width: 1 },
  });
}

// Motif: dolu daire içinde numara/işaret — her slaytta tekrar eder.
function badge(s, x, y, label, color, size) {
  const d = size || 0.42;
  s.addShape(pres.ShapeType.ellipse, {
    x, y, w: d, h: d,
    fill: { color: color || C.deep }, line: { color: color || C.deep, width: 0 },
  });
  s.addText(label, {
    x, y, w: d, h: d,
    fontFace: F.body, fontSize: d > 0.5 ? 15 : 13, bold: true, color: C.white,
    align: 'center', valign: 'middle', margin: 0,
  });
}

function cardText(s, x, y, w, head, body, opts) {
  const o = opts || {};
  s.addText(head, {
    x, y, w, h: 0.32,
    fontFace: F.body, fontSize: o.headSize || 14, bold: true, color: o.headColor || C.deep,
    margin: 0, valign: 'top',
  });
  s.addText(body, {
    x, y: y + 0.36, w, h: o.bodyH || 1.0,
    fontFace: F.body, fontSize: o.bodySize || 12, color: C.muted,
    margin: 0, valign: 'top', lineSpacingMultiple: 1.12,
  });
}

/** Ekran görüntüsü: hafif gölge, her çağrıda taze options nesnesi. */
function shot(s, file, x, y, w, h) {
  s.addImage({
    path: file, x, y, w, h,
    shadow: { type: 'outer', angle: 90, blur: 12, offset: 3, color: '9AAEB8', opacity: 0.45 },
  });
}

function stat(s, x, y, w, value, label, color) {
  s.addText(String(value), {
    x, y, w, h: 0.8,
    fontFace: F.head, fontSize: 42, bold: true, color: color || C.deep,
    align: 'center', margin: 0,
  });
  s.addText(label, {
    x, y: y + 0.78, w, h: 0.45,
    fontFace: F.body, fontSize: 11.5, color: C.muted, align: 'center', margin: 0,
  });
}

/** Ekran görüntüsü slaytı: solda görsel, sağda açıklama listesi. */
function shotSlide(s, opts) {
  header(s, opts.title, opts.sub);
  shot(s, opts.image, M, 1.62, opts.imgW, opts.imgH);

  const px = M + opts.imgW + 0.45;
  const pw = CONTENT_W - opts.imgW - 0.45;

  let y = 1.62;
  opts.points.forEach(([h, b]) => {
    cardText(s, px, y, pw, h, b, { headSize: 13.5, bodySize: 11.5, bodyH: 0.95 });
    y += 1.35;
  });

  if (opts.footnote) {
    s.addText(opts.footnote, {
      x: px, y: 6.55, w: pw, h: 0.7,
      fontFace: F.body, fontSize: 10.5, color: C.amber, margin: 0, valign: 'top',
      italic: true, lineSpacingMultiple: 1.05,
    });
  }
}

// ================================================================ 1. Kapak
{
  const s = pres.addSlide();
  titleSlide(s,
    'DURUM SUNUMU',
    'Mail Tabanlı IT Yönetim Paneli',
    'Service Desk ticket maillerini otomatik okuyup tek ekranda toplayan iç kullanım uygulaması');
  s.addText('Lara Karaahmet  ·  Staj Projesi  ·  Ağustos 2026', {
    x: M, y: 6.4, w: CONTENT_W, h: 0.4,
    fontFace: F.body, fontSize: 12, color: '8FB6C8', margin: 0,
  });
  s.addNotes(
    'Bugun gosterecegim sey calisan bir uygulama, tasarim degil. Kendi Gmail kutumdan ' +
    'gercek ticket maillerini okuyor. Once ne yaptigini, sonra ne YAPMADIGINI anlatacagim.');
}

// ================================================================ 2. Sorun
{
  const s = pres.addSlide();
  header(s, 'Çözmeye çalıştığımız sorun',
    'Ticket bilgisi mailde dağınık duruyor; kimin üzerinde ne var, tek ekranda görünmüyor');

  const items = [
    ['Mailler dağınık', 'Ticket bildirimleri gruba gidiyor. Bir ticket müdürün kutusuna hiç düşmeden bir çalışanın kutusunda kalabiliyor.'],
    ['Takip elle yapılıyor', 'Kimin üzerinde kaç iş var, hangisi uzun süredir açık — bu bilgi hiçbir yerde toplu durmuyor.'],
    ['Hatırlatma zaman alıyor', 'Kime ne hatırlatılacağı tek tek mail geçmişinden çıkarılıyor.'],
    ['Kim nerede belirsiz', 'Ofis / home office / izin bilgisi ayrı yerlerde; atama yaparken kimin müsait olduğu bilinmiyor.'],
  ];

  let y = 1.75;
  items.forEach(([head, body], i) => {
    card(s, M, y, CONTENT_W, 1.05);
    badge(s, M + 0.3, y + 0.32, String(i + 1), C.mid);
    s.addText(head, {
      x: M + 0.95, y: y + 0.18, w: 3.0, h: 0.34,
      fontFace: F.body, fontSize: 15, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 4.05, y: y + 0.2, w: CONTENT_W - 4.4, h: 0.7,
      fontFace: F.body, fontSize: 12.5, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.1,
    });
    y += 1.2;
  });

  s.addNotes('Bu dort maddeyi sizinle konustugumuz ihtiyactan cikardim. Hicbiri Tixbox eksigi degil.');
}

// ================================================================ 3. Ne yapıyor
{
  const s = pres.addSlide();
  header(s, 'Panel ne yapıyor', 'Mailden ekrana, elle işlem olmadan');

  const steps = [
    ['Okur', 'Tanımlı posta kutularını 5 dakikada bir tarar'],
    ['Ayrıştırır', 'Konu ve gövdeden ticket alanlarını çıkarır'],
    ['Eşleştirir', 'Aynı ticket birden çok kutuda olsa tek kayıt açar'],
    ['Gösterir', 'Dashboard, atama, durum, hatırlatma'],
  ];

  const cw = 2.68, gap = 0.45;
  let x = M;
  steps.forEach(([head, body], i) => {
    card(s, x, 1.8, cw, 1.9);
    badge(s, x + 0.28, 2.05, String(i + 1), C.deep);
    s.addText(head, {
      x: x + 0.28, y: 2.62, w: cw - 0.56, h: 0.35,
      fontFace: F.head, fontSize: 18, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: x + 0.28, y: 3.0, w: cw - 0.56, h: 0.6,
      fontFace: F.body, fontSize: 12, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.1,
    });
    x += cw + gap;
  });

  card(s, M, 4.05, CONTENT_W, 2.45, { fill: C.white });
  const caps = [
    ['Ticket listesi ve arama', 'Numara, talep eden, uygulama, açıklama ve posta kutusunda arama'],
    ['Atama ve durum takibi', 'Çalışan durumu günceller, müdür anında görür'],
    ['Hatırlatma maili', 'Önizleme → onay → gönderim; onaysız gönderilmez'],
    ['Haftalık çalışma takvimi', 'Ofis / home office / izin planı tek ekranda'],
    ['Elle ticket ekleme', 'Maili düşmemiş Tixbox kaydı numarasıyla girilir'],
    ['Denetim kaydı', 'Kim, ne zaman, neyi değiştirdi'],
  ];
  caps.forEach(([head, body], i) => {
    const col = i % 3, row = Math.floor(i / 3);
    cardText(s, M + 0.35 + col * 4.05, 4.32 + row * 1.05, 3.7, head, body,
      { headSize: 13, bodySize: 11.5, bodyH: 0.55 });
  });

  s.addNotes('Ust siradaki dort adim otomatik. Alttaki alti yetenek de sizin ekranda yaptiklariniz.');
}

// ================================================================ 4. Ne YAPMIYOR
{
  const s = pres.addSlide();
  header(s, 'Panel ne yapmıyor', 'Baştan konuşulması gereken sınırlar');

  const nos = [
    ['Tixbox\'a yazmaz', 'Panel Tixbox üzerinde ticket açmaz, güncellemez, kapatmaz. Buradaki durumlar yalnızca yönetim panelindeki takip durumudur. Bu uyarı her ekranda yazılı.'],
    ['SLA veya hedef tarih üretmez', 'Tixbox\'tan SLA verisi gelmiyor. Uydurma bir hedef tarih yanlış yönlendirir; onun yerine "kaç gündür açık" ve "kaç gündür güncellenmedi" gösteriliyor. Eşikler ayarlardan değiştirilebilir.'],
    ['Onaysız mail göndermez', 'Her hatırlatma önce önizlenir, siz onaylamadan gönderilmez.'],
  ];

  let y = 1.8;
  nos.forEach(([head, body]) => {
    card(s, M, y, CONTENT_W, 1.5, { fill: C.tintWarm, line: 'EBD9C6' });
    badge(s, M + 0.32, y + 0.5, '✕', C.amber);
    s.addText(head, {
      x: M + 1.0, y: y + 0.22, w: CONTENT_W - 1.4, h: 0.36,
      fontFace: F.body, fontSize: 16, bold: true, color: C.amber, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 1.0, y: y + 0.62, w: CONTENT_W - 1.4, h: 0.75,
      fontFace: F.body, fontSize: 12.5, color: C.ink, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.15,
    });
    y += 1.65;
  });

  s.addNotes(
    'Bu slaydi bilerek one aldim. Panelin Tixbox\'i degistirmedigini herkesin bilmesi lazim. ' +
    'SLA konusunu ozellikle soyluyorum: veri olmadigi icin uydurmadik.');
}

// ================================================================ 5. Dashboard (ekran)
{
  const s = pres.addSlide();
  shotSlide(s, {
    title: 'Dashboard',
    sub: 'Sabah açıp bakılacak tek ekran — gerçek verilerle',
    image: SS('dashboard.png'),
    imgW: 8.3, imgH: 5.19,
    points: [
      ['Sekiz metrik kartı', 'Açık, atanmamış, devam eden, uzun süredir açık; bugün kim ofiste, home office, izinli ve plan göndermeyenler.'],
      ['Bugünkü ekip durumu', 'Atama yaparken kimin müsait olduğu aynı ekranda.'],
      ['Atanmamış ticket\'lar', 'Listeden çıkmadan doğrudan buradan atama yapılır.'],
      ['Mailleri şimdi oku', '5 dakikalık otomatik okumayı beklemeden elle tetikleme.'],
    ],
  });
  s.addNotes(
    'Ekrandaki sayilar gercek: 5 acik ticket, hepsi atanmamis, hepsi uzun suredir acik. ' +
    'Ekip takvimi henuz doldurulmadigi icin ofis/home office sayilari sifir gorunuyor.');
}

// ================================================================ 6. Ticket listesi (ekran)
{
  const s = pres.addSlide();
  shotSlide(s, {
    title: "Ticket listesi",
    sub: 'Arama, filtre, atama — ve kaydın hangi kutudan geldiği',
    image: SS('ticketlar.png'),
    imgW: 8.3, imgH: 5.19,
    points: [
      ['"Okunduğu kutu" sütunu', 'Aynı mail iki kutuya düştüyse ikisi de yazar. Çoklu kutu okumaya geçtikten sonra kaydın nereden geldiği belirsizdi; bunu siz istemiştiniz.'],
      ['Arama', 'Ticket no, talep eden, uygulama, açıklama ve posta kutusu üzerinde çalışır.'],
      ['Yaş durumu', '"Uzun süredir açık" / "Güncelleme bekliyor". Eşikler 2/5/7 gün, ayarlardan değiştirilir.'],
      ['Elle ticket ekle', 'Maili düşmemiş kayıt numarasıyla girilir; aynı numara iki kez eklenemez.'],
    ],
  });
  s.addNotes(
    'Bu ekranda dikkat cekmek istedigim sutun "Okundugu kutu". ' +
    'Ayni ticket hem benim hem Dilara\'nin kutusunda; tek kayit acildi ama ikisi de yaziyor.');
}

// ================================================================ 7. Ticket detayı (ekran)
{
  const s = pres.addSlide();
  shotSlide(s, {
    title: 'Ticket detayı',
    sub: 'Satıra tıklayınca açılan çekmece',
    image: SS('ticket-detay.png'),
    imgW: 8.3, imgH: 5.19,
    points: [
      ['Atama ve durum', 'Sorumlu seçilir, durum değiştirilir, not eklenir — hepsi bu çekmeceden.'],
      ['Mail kaynakları', 'Ticket kaç mailden geldi, hangi kutuda bulundu, orijinal gönderen kim.'],
      ['Durum geçmişi', 'Kim, ne zaman, hangi geçişi yaptı ve notu ne.'],
      ['Tixbox bağlantısı', 'Orijinal kayda tek tıkla gidilir — yeni sekmede, salt görüntüleme.'],
    ],
  });
  s.addNotes('Cekmece listeden cikmadan aciliyor; sirayla ticket incelerken sayfa degistirmek gerekmiyor.');
}

// ================================================================ 8. Widget (ekran) — hafif ton
{
  const s = pres.addSlide();
  header(s, 'Masaüstü özet kutusu', 'Paneli açmadan, ekranın köşesinde duran küçük pencere');

  shot(s, SS('widget.png'), M + 0.5, 1.7, 3.5, 4.33);

  card(s, M + 4.6, 1.7, CONTENT_W - 4.6, 4.33, { fill: C.white });

  const pts = [
    ['Üç şey gösterir', 'Kaç ticket atanmamış, kaç tanesi uzun süredir açık, bugün kaç kişi ofiste. Fazlası yok — köşede duracak bir kutuda tablo istemedik.'],
    ['Kendi kendine yenilenir', 'Dakikada bir günceller. Panel açık olmasa da çalışır.'],
    ['Uygulama gibi durur', 'Tarayıcıdan "Bu siteyi uygulama olarak yükle" denince adres çubuğu olmayan küçük bir pencere olur; görev çubuğuna sabitlenebilir.'],
  ];
  let y = 2.0;
  pts.forEach(([h, b]) => {
    cardText(s, M + 4.95, y, CONTENT_W - 5.3, h, b, { headSize: 14, bodySize: 12, bodyH: 1.0 });
    y += 1.4;
  });

  s.addText('Adres:  http://localhost:5080/widget', {
    x: M + 4.95, y: 6.25, w: CONTENT_W - 5.3, h: 0.4,
    fontFace: F.body, fontSize: 12, color: C.mid, bold: true, margin: 0,
  });

  s.addNotes(
    'Bu kucuk kutuyu "gun boyu acik kalsin" diye yaptik. ' +
    'Ilk surumde calisan hesabiyla acildiginda 403 hatasi veriyordu — rol bilinmeden istek ' +
    'atiyordu, duzeltildi.');
}

// ================================================================ 9. Yönetim ekranı (ekran)
{
  const s = pres.addSlide();
  shotSlide(s, {
    title: 'Yönetim ekranı',
    sub: 'Posta kutuları, kullanıcılar ve ayarlar — dosya düzenlemeden',
    image: SS('yonetim-kutular.png'),
    imgW: 8.3, imgH: 5.19,
    points: [
      ['Posta kutuları', 'Adres eklenir, "Yetkilendir" ile Google onayı verilir. Yeşil tik yetkilendirilmiş demek; son okuma ve hata durumu altında yazar.'],
      ['Kullanıcılar', 'Ekleme, rol seçimi, parola sıfırlama, aktif/pasif. Kullanıcı silinmez — geçmiş atamalar korunur.'],
      ['Ayarlar', 'Yaş eşikleri, okuma sıklığı, takvim kuralları buradan değişir.'],
      ['Baştan tara', 'Kutu bağlı ama eski mailler gelmiyorsa okuma penceresini sıfırlar.'],
    ],
    footnote: 'Bu işlemler önceden yalnızca teknik arayüzden veya dosya düzenleyerek yapılabiliyordu.',
  });
  s.addNotes(
    'Bu ekran son eklenen parcalardan biri. Onceden posta kutusu eklemek icin ayar dosyasi ' +
    'duzenlemek gerekiyordu; artik ekrandan yapiliyor.');
}

// ================================================================ 10. Çalışan tarafı (ekran)
{
  const s = pres.addSlide();
  shotSlide(s, {
    title: 'Çalışan tarafı',
    sub: 'Ekip kendi işini kendi günceller, siz dashboard\'da görürsünüz',
    image: SS('calisan.png'),
    imgW: 8.3, imgH: 5.19,
    points: [
      ['Yalnızca kendi kayıtları', 'Çalışan başkasının ticket\'ını göremez. Bu kısıt sunucuda zorlanır; istemci filtreyi değiştirerek kapsamı genişletemez.'],
      ['Durumu ileri ve geri alabilir', 'Yanlışlıkla kapatılan ticket yeniden açılabilir.'],
      ['Otomatik atama', 'Ticket maili tek kişiye gelmişse müdür ataması beklenmez; listede "otomatik atandı" yazar.'],
      ['Çalışma planı', 'Haftalık ofis / home office / izin planı; gönderim sonrası müdür onayına düşer.'],
    ],
  });
  s.addNotes(
    'Ekran bos gorunuyor cunku su an hicbir ticket atanmamis durumda. ' +
    'Atama yapildigi anda burada gorunur.');
}

// ================================================================ 11. Teknik mimari
{
  const s = pres.addSlide();
  header(s, 'Teknik yapı', 'Katmanlı mimari — bağımlılıklar tek yönlü');

  // Katman zinciri
  const layers = [
    ['Api', 'HTTP uçları, kimlik doğrulama, zamanlanmış işler', C.deep],
    ['Infrastructure', 'Gmail, veritabanı, mail gönderimi', C.mid],
    ['Application', 'İş kuralları, ayrıştırma, servisler', C.light],
    ['Domain', 'Varlıklar ve durum makinesi — dışa bağımlılığı yok', C.purple],
  ];
  let x = M;
  const lw = 2.86;
  layers.forEach(([name, desc, color], i) => {
    card(s, x, 1.75, lw, 1.55, { fill: C.white });
    badge(s, x + 0.25, 1.98, String(i + 1), color);
    s.addText(name, {
      x: x + 0.25, y: 2.5, w: lw - 0.5, h: 0.3,
      fontFace: F.body, fontSize: 14.5, bold: true, color: color, margin: 0, valign: 'top',
    });
    s.addText(desc, {
      x: x + 0.25, y: 2.82, w: lw - 0.5, h: 0.45,
      fontFace: F.body, fontSize: 10.5, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.05,
    });
    if (i < 3) {
      s.addText('→', {
        x: x + lw + 0.02, y: 2.35, w: 0.35, h: 0.4,
        fontFace: F.body, fontSize: 18, color: 'A9BFCB', align: 'center', margin: 0,
      });
    }
    x += lw + 0.37;
  });

  // Teknoloji kartları
  const tech = [
    ['Arayüz', 'React 18 · TypeScript · Vite\nMUI · TanStack Query\nReact Hook Form + Zod'],
    ['Sunucu', '.NET 8 · ASP.NET Core\nEF Core 8 · Hangfire\nSerilog · Swagger'],
    ['Veri', 'SQL Server (kurumsal)\nSQLite (taşınabilir paket)\n21 tablo · denetim kaydı'],
    ['Entegrasyon', 'Gmail API (salt okuma)\nOAuth2 · kutu başına onay\nMock sağlayıcı ile test'],
  ];
  let tx = M;
  tech.forEach(([head, body]) => {
    card(s, tx, 3.65, lw, 2.0, { fill: C.tint });
    s.addText(head, {
      x: tx + 0.25, y: 3.85, w: lw - 0.5, h: 0.32,
      fontFace: F.body, fontSize: 13.5, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: tx + 0.25, y: 4.2, w: lw - 0.5, h: 1.3,
      fontFace: F.body, fontSize: 11, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.2,
    });
    tx += lw + 0.37;
  });

  s.addText(
    'Gmail erişimi bir arayüzün arkasında: gerçek Gmail ve sahte (Mock) sağlayıcı yer değiştirebiliyor. ' +
    'Testler internet olmadan, gerçek kutuya dokunmadan çalışıyor.', {
    x: M, y: 5.95, w: CONTENT_W, h: 0.6,
    fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top',
    lineSpacingMultiple: 1.1,
  });

  s.addNotes(
    'Katmanli mimarinin faydasi su: Domain katmani hicbir seye bagli degil, ' +
    'is kurallari veritabanindan ve Gmail\'den bagimsiz test edilebiliyor.');
}

// ================================================================ 12. Mail nasıl okunuyor
{
  const s = pres.addSlide();
  header(s, 'Bir mail nasıl ticket oluyor', 'Altı adım, hepsi deterministik — yapay zekâ yok');

  const pipeline = [
    ['Dar sorgu', 'Gönderen + konu kalıbı ile Gmail sorgusu. Gelen kutusunun tamamı taranmaz.'],
    ['Zarfı aç', 'İletilen mailde en içteki orijinal mail bulunur; açılış tarihi oradan alınır.'],
    ['Konuyu ayrıştır', 'Ticket no, talep eden, uygulama, öncelik konu satırından çıkarılır.'],
    ['Gövdeyi oku', 'Kategori, açıklama, Tixbox bağlantısı gövdeden alınır.'],
    ['Doğrula', 'Konu ile gövde çelişirse ticket yine açılır ama uyarı işaretlenir.'],
    ['Tekrarı ele', '4 aşamalı kontrol: mesaj kimliği, ticket no, thread, konu+tarih.'],
  ];

  let x = M, y = 1.72;
  pipeline.forEach(([head, body], i) => {
    const col = i % 3, row = Math.floor(i / 3);
    const cw = 3.92, ch = 1.5;
    const cx = M + col * (cw + 0.24);
    const cy = 1.72 + row * (ch + 0.25);
    card(s, cx, cy, cw, ch, { fill: C.white });
    badge(s, cx + 0.25, cy + 0.22, String(i + 1), C.mid);
    s.addText(head, {
      x: cx + 0.82, y: cy + 0.26, w: cw - 1.1, h: 0.3,
      fontFace: F.body, fontSize: 14, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: cx + 0.25, y: cy + 0.72, w: cw - 0.5, h: 0.65,
      fontFace: F.body, fontSize: 11, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.1,
    });
  });

  // Örnek konu satırı
  card(s, M, 5.3, CONTENT_W, 1.55, { fill: C.deep, line: C.deep });
  s.addText('Konu satırından çıkarılanlar', {
    x: M + 0.35, y: 5.48, w: CONTENT_W - 0.7, h: 0.3,
    fontFace: F.body, fontSize: 12, bold: true, color: C.light, margin: 0, valign: 'top',
  });
  s.addText('New Ticket n. I260729_000144 for Turcan, Merve about ERP TR - N/A - Priority: 2', {
    x: M + 0.35, y: 5.82, w: CONTENT_W - 0.7, h: 0.32,
    fontFace: 'Courier New', fontSize: 12.5, color: C.white, margin: 0, valign: 'top',
  });
  s.addText(
    'Numara I260729_000144  ·  Tür: Incident (I ile başlıyor)  ·  Talep eden: Merve Turcan  ·  ' +
    'Uygulama: ERP TR  ·  Öncelik: 2', {
    x: M + 0.35, y: 6.22, w: CONTENT_W - 0.7, h: 0.4,
    fontFace: F.body, fontSize: 11.5, color: 'B9D4E2', margin: 0, valign: 'top',
  });

  s.addNotes(
    '"Turcan, Merve" otomatik olarak "Merve Turcan" haline getiriliyor. ' +
    'Yapay zeka kullanmadik: kalip sabit oldugu icin kural tabanli okuma hem hizli hem ongorulebilir.');
}

// ================================================================ 13. Yaşadıklarımız (eğlenceli ton)
{
  const s = pres.addSlide();
  header(s, 'Yol boyunca yakaladığımız sinsi hatalar',
    'Hepsinin ortak özelliği: hiçbiri hata mesajı vermiyordu');

  const bugs = [
    ['Saat 3 saat geriye kayıyordu', 'Mailde 11:47, ekranda 08:47', 'Veritabanı saat dilimi bilgisini tutmuyor; okurken UTC işareti kayboluyordu.'],
    ['Tarih filtresi bozuluyordu', '"2026/07/06" yerine "2026.07.06"', 'Türkçe kültürde "/" ayırıcısı "." oluyor. Gmail sorgusu sessizce boş dönüyordu.'],
    ['Gerçek mail reddediliyordu', 'Testlerde çalışıyor, Gmail\'de çalışmıyor', 'Gmail uzun konu satırını ikiye bölüyor; ayrıştırıcı yarısını okuyordu.'],
    ['Eski mailler atlanıyordu', 'Kutu bağlandı ama 0 ticket', 'Başarısız okuma da "buraya kadar okudum" damgasını ilerletiyordu.'],
    ['Yetki kontrolleri düşüyordu', 'Giriş başarılı, her ekran "kullanıcı bulunamadı"', 'Kimlik, atanmadan önce yakalanıyordu — istek boyunca boş kalıyordu.'],
  ];

  let y = 1.72;
  bugs.forEach(([title, symptom, cause], i) => {
    card(s, M, y, CONTENT_W, 0.92, { fill: i % 2 === 0 ? C.tint : C.white });
    badge(s, M + 0.28, y + 0.25, String(i + 1), C.red);
    s.addText(title, {
      x: M + 0.9, y: y + 0.13, w: 3.5, h: 0.3,
      fontFace: F.body, fontSize: 13.5, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(symptom, {
      x: M + 0.9, y: y + 0.45, w: 3.5, h: 0.3,
      fontFace: F.body, fontSize: 10.5, color: C.red, margin: 0, valign: 'top', italic: true,
    });
    s.addText(cause, {
      x: M + 4.6, y: y + 0.26, w: CONTENT_W - 4.9, h: 0.5,
      fontFace: F.body, fontSize: 11.5, color: C.ink, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.05,
    });
    y += 1.0;
  });

  s.addText(
    'Beşi de uygulamayı gerçek koşullarda çalıştırınca ortaya çıktı — geliştirme ortamında hepsi "çalışıyor" görünüyordu.', {
    x: M, y: 6.75, w: CONTENT_W, h: 0.4,
    fontFace: F.body, fontSize: 12, color: C.amber, margin: 0, italic: true,
  });

  s.addNotes(
    'Bu slayti bilerek koydum. Calisan kod yazmak bir seviye; ' +
    'hata mesaji vermeyen hatalari bulmak baska bir seviye. ' +
    'Ozellikle dorduncusu: kutu bagliydi, yesil tik vardi, hata yoktu ama ticket gelmiyordu.');
}

// ================================================================ 14. Test yaklaşımı
{
  const s = pres.addSlide();
  header(s, 'Nasıl emin oluyoruz', 'Her hata için, bir daha olmasın diye bir test');

  card(s, M, 1.75, CONTENT_W, 1.6, { fill: C.tint });
  stat(s, M + 0.4, 1.98, 2.6, '223', 'otomatik test', C.deep);
  stat(s, M + 3.3, 1.98, 2.6, '< 1 dk', 'tamamı çalışıyor', C.mid);
  stat(s, M + 6.2, 1.98, 2.6, '0', 'internet gerektiren test', C.green);
  stat(s, M + 9.1, 1.98, 2.6, '0', 'gerçek kutuya dokunan', C.green);

  const areas = [
    ['Ayrıştırma', 'Konu kalıpları, iletilen mailler, bozuk veri, uyarı üretimi'],
    ['Yetki', 'Çalışan kapsamı, istemcinin filtreyi değiştirmesi, rol kontrolleri'],
    ['Kimlik', 'Parola özeti, hesap kilitleme, oturum düşürme, ilk kurulum'],
    ['Okuma', 'Çoklu kutu, tekrar koruması, hata sonrası pencere davranışı'],
    ['Takvim', 'Haftalık plan, onay akışı, kilit saati, resmî tatiller'],
    ['Hatırlatma', 'Önizleme, onay zorunluluğu, gönderim kaydı'],
  ];

  areas.forEach(([head, body], i) => {
    const col = i % 3, row = Math.floor(i / 3);
    const cx = M + col * 4.05;
    const cy = 3.6 + row * 1.35;
    card(s, cx, cy, 3.85, 1.15, { fill: C.white });
    s.addText(head, {
      x: cx + 0.28, y: cy + 0.16, w: 3.3, h: 0.3,
      fontFace: F.body, fontSize: 13.5, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: cx + 0.28, y: cy + 0.5, w: 3.3, h: 0.55,
      fontFace: F.body, fontSize: 11, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.1,
    });
  });

  s.addText(
    'Gmail sahte bir sağlayıcıyla değiştirilebildiği için testler internet olmadan, ' +
    'gerçek posta kutusuna hiç dokunmadan çalışıyor.', {
    x: M, y: 6.5, w: CONTENT_W, h: 0.5,
    fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top',
  });

  s.addNotes(
    'Yaklasim su: bir hata ciktiginda once o hatayi yakalayan testi yaziyoruz, ' +
    'sonra duzeltiyoruz. Boylece ayni hata sessizce geri gelemiyor.');
}

// ================================================================ 15. Kurulum ve güvenlik
{
  const s = pres.addSlide();
  header(s, 'Kurulum ve erişim güvenliği', 'Kurulum gerektirmeyen tek klasör, parola ile giriş');

  // Sol: kurulum adımları
  const steps = [
    ['Aç', 'ZIP\'i ayıklayıp Baslat.cmd\'ye çift tıklayın. .NET, Node.js, SQL Server gerekmez.'],
    ['Parolanı belirle', 'İlk açılışta yönetici parolasını siz koyarsınız.'],
    ['Gmail\'i bağla', 'Yönetim → Posta kutuları → Ekle → Yetkilendir.'],
    ['Ekibi ekle', 'Kişi ilk girişinde kendi parolasını belirler.'],
  ];
  let y = 1.75;
  steps.forEach(([head, body], i) => {
    card(s, M, y, 6.0, 1.0, { fill: C.tint });
    badge(s, M + 0.28, y + 0.29, String(i + 1), C.deep);
    s.addText(head, {
      x: M + 0.9, y: y + 0.15, w: 4.9, h: 0.3,
      fontFace: F.body, fontSize: 14, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 0.9, y: y + 0.46, w: 4.9, h: 0.48,
      fontFace: F.body, fontSize: 11, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.05,
    });
    y += 1.13;
  });

  // Sağ: güvenlik
  const sec = [
    ['Parolalar açık saklanmaz', 'PBKDF2 ile özetlenir; 5 hatalı denemede 15 dakika kilit.'],
    ['Rol ayrımı sunucuda', 'Çalışan yalnızca kendi kayıtlarını görür.'],
    ['Oturum sunucuda tutulur', 'Kullanıcı pasifleştirilince oturumu anında düşer.'],
    ['Gmail yalnızca okunur', 'Silmez, değiştirmez, göndermez. Her kutu sahibi kendi onayını verir.'],
  ];
  let sy = 1.75;
  sec.forEach(([head, body]) => {
    card(s, M + 6.35, sy, CONTENT_W - 6.35, 1.0, { fill: C.tintGreen, line: 'D3E4D6' });
    badge(s, M + 6.63, sy + 0.29, '✓', C.green);
    s.addText(head, {
      x: M + 7.25, y: sy + 0.15, w: CONTENT_W - 7.6, h: 0.3,
      fontFace: F.body, fontSize: 14, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 7.25, y: sy + 0.46, w: CONTENT_W - 7.6, h: 0.48,
      fontFace: F.body, fontSize: 11, color: C.ink, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.05,
    });
    sy += 1.13;
  });

  s.addText(
    'Ekibin aynı verileri görmesi için uygulama tek bilgisayarda çalışır, diğerleri tarayıcıdan bağlanır ' +
    '(Baslat.cmd yerine Paylas.cmd). Herkes kendi kopyasını açarsa veriler paylaşılmaz.', {
    x: M, y: 6.35, w: CONTENT_W, h: 0.6,
    fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top',
    lineSpacingMultiple: 1.1,
  });

  s.addNotes(
    'Son cumleyi vurgulayin: herkes ZIP\'i kendi bilgisayarinda acarsa atamalar birbirine gorunmez. ' +
    'Tek kurulum, digerleri tarayicidan.');
}

// ================================================================ 16. Kararlar
{
  const s = pres.addSlide();
  s.background = { color: C.deep };

  s.addText('Sizden beklenen kararlar', {
    x: M, y: 0.7, w: CONTENT_W, h: 0.7,
    fontFace: F.head, fontSize: 34, bold: true, color: C.white, margin: 0,
  });
  s.addText('Teknik taraf hazır; bu üç madde karar bekliyor', {
    x: M, y: 1.4, w: CONTENT_W, h: 0.4,
    fontFace: F.body, fontSize: 14, color: 'B9D4E2', margin: 0,
  });

  const asks = [
    ['KVKK / İK onayı', 'Çalışan posta kutularının okunması için hukuk ve İK onayı gerekiyor. Onay gelene kadar yalnızca sizin kutunuz okunabilir.'],
    ['Google Cloud projesi', 'Şu an kişisel bir Google projesi kullanılıyor; kullanıcıya "doğrulanmamış uygulama" uyarısı çıkıyor. Menarini IT kendi projesini açarsa uyarı kalkar. Kod değişikliği gerekmiyor.'],
    ['Kimin bilgisayarında çalışacak', 'Panel tek bilgisayarda çalışıp ekibe açılmalı. Sizin makineniz mi, yoksa IT bir sunucu mu ayıracak?'],
  ];

  let y = 2.2;
  asks.forEach(([head, body], i) => {
    s.addShape(pres.ShapeType.roundRect, {
      x: M, y, w: CONTENT_W, h: 1.1, rectRadius: 0.08,
      fill: { color: '235777' }, line: { color: '2E6E8E', width: 1 },
    });
    badge(s, M + 0.32, y + 0.34, String(i + 1), C.light);
    s.addText(head, {
      x: M + 1.0, y: y + 0.18, w: 3.6, h: 0.34,
      fontFace: F.body, fontSize: 15, bold: true, color: C.white, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 4.7, y: y + 0.18, w: CONTENT_W - 5.1, h: 0.8,
      fontFace: F.body, fontSize: 12, color: 'CFE2EC', margin: 0, valign: 'top',
      lineSpacingMultiple: 1.1,
    });
    y += 1.3;
  });

  s.addText('Uygulama şu an çalışıyor — isterseniz hemen gösterebilirim.', {
    x: M, y: 6.5, w: CONTENT_W, h: 0.5,
    fontFace: F.body, fontSize: 14, color: C.light, margin: 0, italic: true,
  });

  s.addNotes(
    'Uc maddeyi de karar olarak biraktim, oneri olarak degil. ' +
    'Ikincisi en kolayi: IT bir Google Cloud projesi acsa credentials.json degisecek, baska bir sey degil. ' +
    'Sunum bitince canli demo teklif edin.');
}

pres.writeFile({ fileName: 'IT-Yonetim-Paneli-Sunum.pptx' })
  .then(() => console.log('Yazildi: IT-Yonetim-Paneli-Sunum.pptx'));
