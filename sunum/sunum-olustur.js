const pptxgen = require('pptxgenjs');

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';           // 13.3 x 7.5
pres.author = 'Lara Karaahmet';
pres.title = 'Mail Tabanlı IT Yönetim Paneli — Durum Raporu';

// Uygulamanın kendi teması: sunum anlattığı ürünle aynı dili konuşsun.
const C = {
  deep:   '1B4965',   // baskın
  mid:    '2E6E8E',
  light:  '62B6CB',
  amber:  'C9722B',   // sorun / dikkat
  green:  '2E7D32',
  bg:     'FFFFFF',
  tint:   'EEF4F7',
  tintWarm:'FBF1E8',
  ink:    '1A2027',
  muted:  '5A6872',
  white:  'FFFFFF',
};

const F = { head: 'Cambria', body: 'Calibri' };

const M = 0.6;                 // kenar boşluğu
const W = 13.33;
const CONTENT_W = W - 2 * M;

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
    x: M, y: 0.45, w: CONTENT_W, h: 0.62,
    fontFace: F.head, fontSize: 34, bold: true, color: C.deep, margin: 0,
  });
  if (sub) {
    s.addText(sub, {
      x: M, y: 1.08, w: CONTENT_W, h: 0.4,
      fontFace: F.body, fontSize: 14, color: C.muted, margin: 0,
    });
  }
}

// Motif: her karta sol üstte dolu daire içinde numara/işaret
function card(s, x, y, w, h, opts) {
  const o = opts || {};
  s.addShape(pres.ShapeType.roundRect, {
    x, y, w, h, rectRadius: 0.08,
    fill: { color: o.fill || C.tint },
    line: { color: o.line || 'DCE7ED', width: 1 },
  });
}

function badge(s, x, y, label, color) {
  s.addShape(pres.ShapeType.ellipse, {
    x, y, w: 0.42, h: 0.42,
    fill: { color: color || C.deep }, line: { color: color || C.deep, width: 0 },
  });
  s.addText(label, {
    x, y, w: 0.42, h: 0.42,
    fontFace: F.body, fontSize: 13, bold: true, color: C.white,
    align: 'center', valign: 'middle', margin: 0,
  });
}

function cardText(s, x, y, w, head, body, opts) {
  const o = opts || {};
  // valign: 'top' şart — kutu yüksekliği içerikten büyük olduğunda metin dikeyde
  // ortalanıp başlıktan kopuyor.
  s.addText(head, {
    x, y, w, h: 0.35,
    fontFace: F.body, fontSize: o.headSize || 15, bold: true, color: o.headColor || C.deep,
    margin: 0, valign: 'top',
  });
  s.addText(body, {
    x, y: y + 0.4, w, h: o.bodyH || 1.1,
    fontFace: F.body, fontSize: o.bodySize || 12.5, color: C.muted,
    margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });
}

function stat(s, x, y, w, value, label, color) {
  s.addText(String(value), {
    x, y, w, h: 0.85,
    fontFace: F.head, fontSize: 46, bold: true, color: color || C.deep,
    align: 'center', margin: 0,
  });
  s.addText(label, {
    x, y: y + 0.85, w, h: 0.5,
    fontFace: F.body, fontSize: 12, color: C.muted, align: 'center', margin: 0,
  });
}

// ================================================================ 1. Kapak
{
  const s = pres.addSlide();
  titleSlide(s,
    'DURUM RAPORU',
    'Mail Tabanlı IT Yönetim Paneli',
    'Service Desk ticket maillerini otomatik okuyup yönetilebilir hale getiren iç uygulama\nLara Karaahmet · Ağustos 2026');

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 5.5, w: 5.0, h: 0.95, rectRadius: 0.08,
    fill: { color: '17405A' }, line: { color: '2E6E8E', width: 1 },
  });
  s.addText('Tixbox\'a hiçbir yazma işlemi yapılmaz.\nPanel yalnızca okur ve takip eder.', {
    x: M + 0.25, y: 5.62, w: 4.5, h: 0.7,
    fontFace: F.body, fontSize: 12.5, color: 'CFE3EC', margin: 0, lineSpacingMultiple: 1.1,
  });

  s.addNotes('Amaç: ne yapıldığını, neyin gerçekten çalıştığını ve neyin açık kaldığını göstermek. Ürün Tixbox\'ın yerine geçmiyor; Tixbox\'a hiçbir şey yazmıyor.');
}

// ================================================================ 2. Başlangıç
{
  const s = pres.addSlide();
  header(s, 'Başlarken elimizde ne vardı', 'Projenin girdileri ve baştan bilinen kısıtlar');

  const items = [
    ['1', 'Tek bir gerçek mail', 'Örnek ticket açılış maili — üstelik iletilmiş (forward) hâlde. Tüm ayrıştırma tasarımı bu tek örneğe dayanıyordu.', C.deep],
    ['2', 'Eski analiz dokümanı', '103 sayfalık SRS. Yapay zekâ ağırlıklı, sprint raporu okuyan geniş bir kapsam öngörüyordu.', C.mid],
    ['3', 'Tixbox erişimi yok', 'API yok, veritabanı yok. Tek veri kaynağı Gmail\'e düşen mailler.', C.amber],
  ];

  let x = M;
  const cw = (CONTENT_W - 0.6) / 3;
  items.forEach(([n, h, b, col]) => {
    card(s, x, 1.9, cw, 2.6);
    badge(s, x + 0.3, 2.15, n, col);
    cardText(s, x + 0.3, 2.8, cw - 0.6, h, b, { bodyH: 1.5 });
    x += cw + 0.3;
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 4.95, w: CONTENT_W, h: 1.35, rectRadius: 0.08,
    fill: { color: C.tintWarm }, line: { color: 'E8D4BF', width: 1 },
  });
  s.addText([
    { text: 'Buradaki asıl risk: ', options: { bold: true, color: C.ink } },
    { text: 'tek örnekten yola çıkan bir ayrıştırıcı, gerçek kutuya bağlanınca kırılır. Bu yüzden erken aşamada gerçek Gmail\'e bağlanmayı öncelik yaptım — ve gerçekten de üç ayrı hata çıktı (bkz. slayt 7).', options: { color: C.muted } },
  ], {
    x: M + 0.3, y: 5.2, w: CONTENT_W - 0.6, h: 0.95,
    fontFace: F.body, fontSize: 13, margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });

  s.addNotes('Tek gerçek örnekle çalışmanın riski buydu. Bu yüzden mümkün olan en erken anda gerçek maillere bağlandım.');
}

// ================================================================ 3. Kapsam kararı
{
  const s = pres.addSlide();
  header(s, 'Kapsamı bilerek daralttım', 'Eski SRS ile bu projenin farkı');

  // sol: çıkarılanlar
  card(s, M, 1.7, (CONTENT_W - 0.4) / 2, 4.35, { fill: C.tintWarm, line: 'E8D4BF' });
  s.addText('Kapsam dışı bırakılanlar', {
    x: M + 0.35, y: 1.95, w: (CONTENT_W - 0.4) / 2 - 0.7, h: 0.4,
    fontFace: F.body, fontSize: 16, bold: true, color: C.amber, margin: 0,
  });
  s.addText([
    { text: 'Yapay zekâ / LLM analiz katmanı', options: { bullet: true, breakLine: true } },
    { text: 'Sprint raporu (PDF/Drive) okuma ve OCR', options: { bullet: true, breakLine: true } },
    { text: 'Rapor tutarsızlık kontrolleri', options: { bullet: true, breakLine: true } },
    { text: 'SLA / hedef tarih hesabı', options: { bullet: true, breakLine: true } },
    { text: 'Einstein numarası', options: { bullet: true } },
  ], {
    x: M + 0.35, y: 2.45, w: (CONTENT_W - 0.4) / 2 - 0.7, h: 2.6,
    fontFace: F.body, fontSize: 13.5, color: C.ink, margin: 0, paraSpaceAfter: 10,
  });

  // sağ: yapılanlar
  const rx = M + (CONTENT_W - 0.4) / 2 + 0.4;
  card(s, rx, 1.7, (CONTENT_W - 0.4) / 2, 4.35);
  s.addText('Yapılanlar', {
    x: rx + 0.35, y: 1.95, w: (CONTENT_W - 0.4) / 2 - 0.7, h: 0.4,
    fontFace: F.body, fontSize: 16, bold: true, color: C.deep, margin: 0,
  });
  s.addText([
    { text: 'Gmail\'den filtreli mail okuma', options: { bullet: true, breakLine: true } },
    { text: 'Deterministik ayrıştırma (regex, LLM yok)', options: { bullet: true, breakLine: true } },
    { text: 'Forward zarfı çözümleme', options: { bullet: true, breakLine: true } },
    { text: 'Atama, durum takibi, dahili not', options: { bullet: true, breakLine: true } },
    { text: 'Onaylı hatırlatma maili', options: { bullet: true, breakLine: true } },
    { text: 'Hibrit çalışma takvimi', options: { bullet: true } },
  ], {
    x: rx + 0.35, y: 2.45, w: (CONTENT_W - 0.4) / 2 - 0.7, h: 2.9,
    fontFace: F.body, fontSize: 13.5, color: C.ink, margin: 0, paraSpaceAfter: 10,
  });

  s.addText('Sebep: Tixbox verisi olmadan LLM\'in ekleyeceği değer, getireceği belirsizlikten azdı. Yapısal alanlar zaten regex ile %100 çıkarılabiliyor.', {
    x: M, y: 6.35, w: CONTENT_W, h: 0.5,
    fontFace: F.body, fontSize: 12.5, color: C.muted, italic: true, margin: 0,
  });

  s.addNotes('Eski SRS\'in merkezinde LLM vardı. Elimizde Tixbox verisi olmayınca LLM\'in katacağı değer, getireceği belirsizliğin altında kalıyordu. Konu satırı zaten yapısal.');
}

// ================================================================ 4. Akış
{
  const s = pres.addSlide();
  header(s, 'Sistem ne yapıyor', 'Mailden panele giden yol');

  const steps = [
    ['Gmail', 'Yalnızca izinli gönderen ve konu kalıbına uyan mailler okunur. Kutunun tamamı taranmaz.'],
    ['Ayrıştırma', 'Ticket no, talep eden, uygulama, öncelik, açılış tarihi, açıklama ve Tixbox bağlantısı çıkarılır.'],
    ['Tekilleştirme', 'Aynı ticket birden fazla kutuda veya iletmede olsa tek kayıt açılır.'],
    ['Panel', 'Atanmamışlar, uzun süredir açık olanlar, iş yükü ve ekip durumu tek ekranda.'],
  ];

  const cw = (CONTENT_W - 3 * 0.42) / 4;
  let x = M;
  steps.forEach(([h, b], i) => {
    card(s, x, 1.9, cw, 2.75);
    badge(s, x + 0.28, 2.15, String(i + 1), i === 3 ? C.green : C.deep);
    cardText(s, x + 0.28, 2.8, cw - 0.56, h, b, { bodyH: 1.7, bodySize: 12 });
    if (i < 3) {
      s.addText('→', {
        x: x + cw + 0.02, y: 3.0, w: 0.38, h: 0.4,
        fontFace: F.body, fontSize: 20, color: C.light, bold: true,
        align: 'center', valign: 'middle', margin: 0,
      });
    }
    x += cw + 0.42;
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 5.05, w: CONTENT_W, h: 1.35, rectRadius: 0.08,
    fill: { color: C.deep }, line: { color: C.deep, width: 0 },
  });
  s.addText('Ticket maili bir kişiye gelmişse doğrudan o kişiye atanır. Gruba gelmişse atanmamış düşer ve atamayı siz yaparsınız.', {
    x: M + 0.35, y: 5.32, w: CONTENT_W - 0.7, h: 0.85,
    fontFace: F.body, fontSize: 14.5, color: C.white, margin: 0, lineSpacingMultiple: 1.15,
  });

  s.addNotes('Dört adım. Kritik nokta: kişiye özel gelen ticket için müdürün atama yapmasını beklemiyoruz.');
}

// ================================================================ 5. Gerçek sonuçlar
{
  const s = pres.addSlide();
  header(s, 'Gerçek sonuçlar', 'Uydurma örnek değil — gerçek Gmail kutusundan okunan gerçek ticket\'lar');

  const rows = [
    [
      { text: 'Ticket no', options: { bold: true, color: C.white, fill: { color: C.deep } } },
      { text: 'Talep eden', options: { bold: true, color: C.white, fill: { color: C.deep } } },
      { text: 'Uygulama', options: { bold: true, color: C.white, fill: { color: C.deep } } },
      { text: 'Öncelik', options: { bold: true, color: C.white, fill: { color: C.deep }, align: 'center' } },
      { text: 'Açılış', options: { bold: true, color: C.white, fill: { color: C.deep } } },
    ],
    ['I260805_000073', 'Derin Sen', 'BA-FF Business Applications TR', 'P2', '05.08.2026 10:35'],
    ['I260804_000220', 'Saduman Aydan', 'ERP TR', 'P3', '04.08.2026 15:21'],
    ['I260804_000212', 'Emre Goktas', 'BA Business Applications TR', 'P2', '04.08.2026 15:15'],
    ['I260804_000202', 'Esra Sasmaz', 'BA Business Applications TR', 'P3', '04.08.2026 11:20'],
    ['I260729_000144', 'Merve Turcan', 'ERP TR', 'P2', '29.07.2026 11:47'],
  ];

  s.addTable(rows, {
    x: M, y: 1.75, w: CONTENT_W,
    colW: [2.35, 2.1, 4.25, 1.0, 2.43],
    fontFace: F.body, fontSize: 12.5, color: C.ink,
    border: { type: 'solid', color: 'DCE7ED', pt: 1 },
    rowH: 0.5, valign: 'middle',
    fill: { color: 'FFFFFF' },
  });

  let x = M;
  const sw = (CONTENT_W - 2 * 0.4) / 3;
  [['5', 'mail okundu', C.deep], ['5', 'ticket oluştu', C.green], ['0', 'hata / uyarı', C.green]].forEach(([v, l, col]) => {
    card(s, x, 5.05, sw, 1.7);
    stat(s, x, 5.25, sw, v, l, col);
    x += sw + 0.4;
  });

  s.addNotes('Bu tablo sunumun en önemli slaytı: sistem gerçek kutunuzdaki gerçek maillerle çalıştı. İsimler de düzeltilmiş hâlde — mailde "Sasmaz, Esra" yazıyor, panelde "Esra Sasmaz".');
}

// ================================================================ 6. Forward tarihi
{
  const s = pres.addSlide();
  header(s, 'En kritik ayrıntı: hangi tarih?', 'İletilmiş mailde açılış tarihi, ileten kişinin tarihi değildir');

  card(s, M, 1.8, (CONTENT_W - 0.5) / 2, 2.75, { fill: C.tintWarm, line: 'E8D4BF' });
  s.addText('Yanlış olan', {
    x: M + 0.35, y: 2.0, w: (CONTENT_W - 0.5) / 2 - 0.7, h: 0.35,
    fontFace: F.body, fontSize: 14, bold: true, color: C.amber, margin: 0,
  });
  s.addText('30 Temmuz 2026  13:33', {
    x: M + 0.35, y: 2.42, w: (CONTENT_W - 0.5) / 2 - 0.7, h: 0.6,
    fontFace: F.head, fontSize: 28, bold: true, color: C.ink, margin: 0,
  });
  s.addText('Duygu Keydal\'ın maili ilettiği an. Dış zarfın tarihi bu — ticket\'ın açılışıyla ilgisi yok.', {
    x: M + 0.35, y: 3.08, w: (CONTENT_W - 0.5) / 2 - 0.7, h: 0.9,
    fontFace: F.body, fontSize: 12.5, color: C.muted, margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });

  const rx = M + (CONTENT_W - 0.5) / 2 + 0.5;
  card(s, rx, 1.8, (CONTENT_W - 0.5) / 2, 2.75, { fill: 'EAF3EC', line: 'C8DFCC' });
  s.addText('Doğru olan', {
    x: rx + 0.35, y: 2.0, w: (CONTENT_W - 0.5) / 2 - 0.7, h: 0.35,
    fontFace: F.body, fontSize: 14, bold: true, color: C.green, margin: 0,
  });
  s.addText('29 Temmuz 2026  11:47', {
    x: rx + 0.35, y: 2.42, w: (CONTENT_W - 0.5) / 2 - 0.7, h: 0.6,
    fontFace: F.head, fontSize: 28, bold: true, color: C.ink, margin: 0,
  });
  s.addText('Ticket\'ın Tixbox\'ta gerçekten açıldığı an. Forward bloğunun içindeki orijinal zarftan alınır.', {
    x: rx + 0.35, y: 3.08, w: (CONTENT_W - 0.5) / 2 - 0.7, h: 0.9,
    fontFace: F.body, fontSize: 12.5, color: C.muted, margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 4.95, w: CONTENT_W, h: 1.7, rectRadius: 0.08,
    fill: { color: C.tint }, line: { color: 'DCE7ED', width: 1 },
  });
  s.addText('Neden önemli', {
    x: M + 0.35, y: 5.15, w: CONTENT_W - 0.7, h: 0.32,
    fontFace: F.body, fontSize: 14, bold: true, color: C.deep, margin: 0,
  });
  s.addText('Yanlış tarih, "kaç gündür açık" hesabını da yanlış yapar. Bir ticket 7 gündür açıkken 6 gün görünür ve dikkat gerektirenler listesine geç düşer. Zincirli iletmelerde (siz ← Duygu ← Service Desk) sistem en içteki zarfı bulur.', {
    x: M + 0.35, y: 5.52, w: CONTENT_W - 0.7, h: 0.9,
    fontFace: F.body, fontSize: 13, color: C.muted, margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });

  s.addNotes('Bu ayrım tüm yaşlandırma hesabının temeli. Zincirli forward testini de yazdım: siz Duygu\'nun ilettiği maili bana ilettiğinizde bile doğru tarih çıkıyor.');
}

// ================================================================ 7. Gerçek kutuya bağlanınca
{
  const s = pres.addSlide();
  header(s, 'Gerçek Gmail\'e bağlanınca çıkan hatalar', 'Fixture testleri %100 geçiyordu — gerçek veri üç ayrı kusuru ortaya çıkardı');

  const bugs = [
    ['Katlanmış başlık satırı',
     'Gmail uzun konu satırını ~78 karakterde ikiye böler. Ayrıştırıcı konuyu yarım okuyup maili reddediyordu.',
     'Katlanan satırlar birleştiriliyor.'],
    ['Forward\'ları kaçıran sorgu',
     'Gmail sorgusu yalnızca dış göndereni eşliyordu. İletilmiş mailde dış gönderen ileten kişi olduğu için o mailler hiç gelmezdi.',
     'Adres serbest metin olarak da aranıyor.'],
    ['Kültüre bağlı tarih',
     'Tarih filtresi Türkçe kültürde "2026.07.06" üretiyordu; Gmail bunu kabul etmez.',
     'Biçimlendirme kültürden bağımsız.'],
  ];

  let y = 1.8;
  bugs.forEach(([h, b, fix], i) => {
    card(s, M, y, CONTENT_W, 1.52);
    badge(s, M + 0.3, y + 0.28, String(i + 1), C.amber);
    s.addText(h, {
      x: M + 0.95, y: y + 0.17, w: 4.3, h: 0.35,
      fontFace: F.body, fontSize: 14.5, bold: true, color: C.deep, margin: 0,
    });
    s.addText(b, {
      x: M + 0.95, y: y + 0.55, w: 6.4, h: 0.65,
      fontFace: F.body, fontSize: 12, color: C.muted, margin: 0, valign: 'top', lineSpacingMultiple: 1.1,
    });
    s.addText(fix, {
      x: M + 7.6, y: y + 0.42, w: CONTENT_W - 8.0, h: 0.5,
      fontFace: F.body, fontSize: 12.5, color: C.green, bold: true, margin: 0,
    });
    y += 1.68;
  });

  s.addText('Üçü de gerçek veriyle karşılaşmadan görülemezdi. Her biri için fixture ve regresyon testi eklendi.', {
    x: M, y: 6.72, w: CONTENT_W, h: 0.45,
    fontFace: F.body, fontSize: 12.5, color: C.muted, italic: true, margin: 0,
  });

  s.addNotes('Buradaki mesaj: erken gerçek veriye bağlanmak karşılığını verdi. Üç hata da sessizce veri kaybettiriyordu — hiçbiri çökme değildi.');
}

// ================================================================ 8. Tekilleştirme
{
  const s = pres.addSlide();
  header(s, 'Aynı ticket iki kez açılmaz', 'Mail bir gruba gidiyor, herkes iletiyor — kayıt tek kalmalı');

  const keys = [
    ['Gmail mesaj kimliği', 'Aynı mail yeniden okundu'],
    ['Ticket numarası', 'Aynı ticket başka kutudan geldi'],
    ['Kaynak istek no', 'Konu değişmiş olsa bile eşleşir'],
    ['Konu + açılış anı', 'Son güvenlik ağı'],
  ];

  const cw = (CONTENT_W - 3 * 0.35) / 4;
  let x = M;
  keys.forEach(([h, b], i) => {
    card(s, x, 1.85, cw, 2.15);
    badge(s, x + 0.28, 2.03, String(i + 1), C.deep);
    cardText(s, x + 0.28, 2.6, cw - 0.56, h, b, { bodyH: 0.9, headSize: 13.5, bodySize: 11.5 });
    x += cw + 0.35;
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 4.5, w: CONTENT_W, h: 2.1, rectRadius: 0.08,
    fill: { color: C.tint }, line: { color: 'DCE7ED', width: 1 },
  });
  s.addText('Gerçek testte doğrulandı', {
    x: M + 0.35, y: 4.72, w: CONTENT_W - 0.7, h: 0.35,
    fontFace: F.body, fontSize: 15, bold: true, color: C.deep, margin: 0,
  });
  s.addText([
    { text: 'Aynı ticket iki farklı kaynaktan geldi: fixture\'daki kopya ve sizin Menarini\'den kendinize ilettiğiniz gerçek mail. ', options: { color: C.muted } },
    { text: 'İkinci ticket açılmadı', options: { bold: true, color: C.ink } },
    { text: ' — mail yalnızca ikinci kaynak kaydı olarak eklendi. İkisi de aynı açılış tarihini verdi, üstelik biri iki katmanlı forward\'dı.', options: { color: C.muted } },
  ], {
    x: M + 0.35, y: 5.12, w: CONTENT_W - 0.7, h: 1.3,
    fontFace: F.body, fontSize: 13, margin: 0, valign: 'top', lineSpacingMultiple: 1.2,
  });

  s.addNotes('Dört aşamalı kontrol. Sıra önemli: en ucuz ve en kesin olandan başlıyor.');
}

// ================================================================ 9. Roller
{
  const s = pres.addSlide();
  header(s, 'Kim ne yapabiliyor', 'Yetkiler sunucu tarafında zorlanır — arayüze güvenilmez');

  const rows = [
    [
      { text: 'İşlem', options: { bold: true, color: C.white, fill: { color: C.deep } } },
      { text: 'Müdür', options: { bold: true, color: C.white, fill: { color: C.deep }, align: 'center' } },
      { text: 'Çalışan', options: { bold: true, color: C.white, fill: { color: C.deep }, align: 'center' } },
    ],
    ['Tüm ticket\'ları görme', 'Evet', 'Yalnızca kendine atananlar'],
    ['Çalışana atama / yeniden atama', 'Evet', 'Hayır'],
    ['Devam ediyor / Tamamlandı yapma', 'Evet', 'Evet — kendi ticket\'ında'],
    ['Geri alma (beklemeye al, yeniden aç)', 'Evet', 'Evet — kendi ticket\'ında'],
    ['Atanmamışa geri verme, arşivleme', 'Evet', 'Hayır'],
    ['Hatırlatma maili gönderme', 'Evet', 'Hayır'],
    ['Çalışma planı gönderme', 'Evet', 'Evet'],
  ];

  s.addTable(rows, {
    x: M, y: 1.75, w: CONTENT_W,
    colW: [5.6, 2.5, 4.03],
    fontFace: F.body, fontSize: 12.5, color: C.ink,
    border: { type: 'solid', color: 'DCE7ED', pt: 1 },
    rowH: 0.47, valign: 'middle',
    align: 'left',
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 5.95, w: CONTENT_W, h: 1.0, rectRadius: 0.08,
    fill: { color: C.tint }, line: { color: 'DCE7ED', width: 1 },
  });
  s.addText('Çalışanın yaptığı her durum değişikliği, notuyla birlikte müdür panelindeki "Ekipten gelen güncellemeler" bölümüne düşer.', {
    x: M + 0.35, y: 6.15, w: CONTENT_W - 0.7, h: 0.6,
    fontFace: F.body, fontSize: 13, color: C.muted, margin: 0,
  });

  s.addNotes('Kapsam daraltması sunucuda. Çalışan istemciden başkasının kimliğini gönderse bile sonuç boş dönüyor; 16 test bunu koruyor.');
}

// ================================================================ 10. Takvim
{
  const s = pres.addSlide();
  header(s, 'Hibrit çalışma takvimi', 'Bugün kim ofiste, kim evde, kim izinli — sormadan görünür');

  const left = [
    ['Çalışan', 'Gelecek haftanın 5 iş günü için ofis / home office / izin seçer ve gönderir. Cuma 17:00\'de kilitlenir.'],
    ['Müdür', 'Haftalık matriste tüm ekibi görür, onaylar veya reddeder, gerektiğinde bir günü değiştirir.'],
  ];

  let y = 1.8;
  left.forEach(([h, b], i) => {
    card(s, M, y, 6.2, 2.05);
    badge(s, M + 0.3, y + 0.28, String(i + 1), C.deep);
    cardText(s, M + 0.95, y + 0.28, 4.9, h, b, { bodyH: 1.3 });
    y += 2.3;
  });

  card(s, M + 6.6, 1.8, CONTENT_W - 6.6, 4.3, { fill: C.tintWarm, line: 'E8D4BF' });
  s.addText('3 gün ofis / 2 gün home office', {
    x: M + 6.95, y: 2.05, w: CONTENT_W - 7.3, h: 0.4,
    fontFace: F.body, fontSize: 15, bold: true, color: C.amber, margin: 0,
  });
  s.addText('Kural ihlal edilse bile gönderim engellenmez — yalnızca işaretlenir ve müdür onay ekranında görür.', {
    x: M + 6.95, y: 2.5, w: CONTENT_W - 7.3, h: 0.9,
    fontFace: F.body, fontSize: 12.5, color: C.muted, margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });
  s.addText('Bilerek böyle: sistemin insanı bloke etmesi yerine yöneticiye görünür kılması tercih edildi. Resmî tatiller kural dışıdır.', {
    x: M + 6.95, y: 3.95, w: CONTENT_W - 7.3, h: 1.4,
    fontFace: F.body, fontSize: 12.5, color: C.muted, italic: true, margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });

  s.addText('Plan göndermeyenlerin sayısı dashboard\'da kart olarak görünür; tek tıkla takvim ekranına gidilir.', {
    x: M, y: 6.45, w: CONTENT_W, h: 0.5,
    fontFace: F.body, fontSize: 12.5, color: C.muted, margin: 0,
  });

  s.addNotes('2026 Türkiye resmî tatilleri yüklü. Dinî bayram tarihleri hicri takvime bağlı olduğu için doğrulanmalı — açık konular listesinde.');
}

// ================================================================ 11. SLA yerine yaşlandırma
{
  const s = pres.addSlide();
  header(s, 'Sahte SLA üretmedim', 'Tixbox\'tan hedef tarih gelmiyor — uydurmak yerine ölçülebilir bir kural koydum');

  card(s, M, 1.8, 5.9, 2.85, { fill: C.tintWarm, line: 'E8D4BF' });
  s.addText('Elimizde olmayan', {
    x: M + 0.35, y: 2.0, w: 5.2, h: 0.35,
    fontFace: F.body, fontSize: 14, bold: true, color: C.amber, margin: 0,
  });
  s.addText([
    { text: 'SLA süreleri', options: { bullet: true, breakLine: true } },
    { text: 'Hedef / termin tarihi', options: { bullet: true, breakLine: true } },
    { text: 'Çözüm süresi taahhüdü', options: { bullet: true } },
  ], {
    x: M + 0.35, y: 2.45, w: 5.2, h: 1.5,
    fontFace: F.body, fontSize: 13.5, color: C.ink, margin: 0, paraSpaceAfter: 8,
  });

  card(s, M + 6.3, 1.8, CONTENT_W - 6.3, 2.85);
  s.addText('Bunun yerine', {
    x: M + 6.65, y: 2.0, w: CONTENT_W - 6.65, h: 0.35,
    fontFace: F.body, fontSize: 14, bold: true, color: C.deep, margin: 0,
  });
  s.addText([
    { text: '2 gündür güncellenmedi → "Güncelleme bekliyor"', options: { bullet: true, breakLine: true } },
    { text: '5 gündür açık → "Uzun süredir açık"', options: { bullet: true, breakLine: true } },
    { text: '7 gündür açık → kritik', options: { bullet: true } },
  ], {
    x: M + 6.65, y: 2.45, w: CONTENT_W - 7.0, h: 1.5,
    fontFace: F.body, fontSize: 13, color: C.ink, margin: 0, paraSpaceAfter: 8,
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 4.95, w: CONTENT_W, h: 1.7, rectRadius: 0.08,
    fill: { color: C.deep }, line: { color: C.deep, width: 0 },
  });
  s.addText('Arayüzde hiçbir yerde "SLA" veya "gecikme" yazmaz.', {
    x: M + 0.35, y: 5.25, w: CONTENT_W - 0.7, h: 0.4,
    fontFace: F.body, fontSize: 16, bold: true, color: C.white, margin: 0,
  });
  s.addText('Eşikler ayar ekranından değiştirilebilir. Gerçek SLA verisi bir gün gelirse bu kural onunla değiştirilir — ama o güne kadar sistem bilmediği bir şeyi biliyormuş gibi davranmıyor.', {
    x: M + 0.35, y: 5.7, w: CONTENT_W - 0.7, h: 0.85,
    fontFace: F.body, fontSize: 13, color: 'CFE3EC', margin: 0, valign: 'top', lineSpacingMultiple: 1.15,
  });

  s.addNotes('Bu bilinçli bir karardı. Sahte bir hedef tarih üretmek, yöneticinin yanlış kararına yol açardı. Eşikler ayarlanabilir.');
}

// ================================================================ 12. Teknik özet
{
  const s = pres.addSlide();
  header(s, 'Teknik özet', 'Modular monolith · .NET 8 · React 18 · SQL Server');

  const stats = [
    ['10.714', 'satır C#', C.deep],
    ['3.969', 'satır TypeScript', C.mid],
    ['150', 'otomatik test', C.green],
    ['19', 'veritabanı tablosu', C.deep],
  ];
  const sw = (CONTENT_W - 3 * 0.35) / 4;
  let x = M;
  stats.forEach(([v, l, col]) => {
    card(s, x, 1.75, sw, 2.0);
    stat(s, x, 2.05, sw, v, l, col);
    x += sw + 0.35;
  });

  const layers = [
    ['Domain', 'Kurallar, durum geçiş matrisi, ticket numarası doğrulama'],
    ['Application', 'Mail ayrıştırıcı, servisler — veritabanı ve HTTP bağımsız'],
    ['Infrastructure', 'EF Core, Gmail istemcisi, zamanlanmış iş'],
    ['API + Arayüz', 'Uçlar, yetkilendirme, React paneli'],
  ];

  let y = 4.3;
  layers.forEach(([h, b]) => {
    s.addText(h, {
      x: M, y, w: 2.6, h: 0.32,
      fontFace: F.body, fontSize: 13.5, bold: true, color: C.deep, margin: 0,
    });
    s.addText(b, {
      x: M + 2.7, y, w: CONTENT_W - 2.7, h: 0.32,
      fontFace: F.body, fontSize: 12.5, color: C.muted, margin: 0,
    });
    y += 0.58;
  });

  s.addText('Ayrıştırıcı hiçbir servise bağımlı değil; tek başına test edilebiliyor. Testlerin çoğu onu koruyor.', {
    x: M, y: 6.75, w: CONTENT_W, h: 0.45,
    fontFace: F.body, fontSize: 12.5, color: C.muted, italic: true, margin: 0,
  });

  s.addNotes('Mikroservis yok, message broker yok. Ekip küçük, karmaşıklık gereksizdi.');
}

// ================================================================ 13. Gerçek / mock
{
  const s = pres.addSlide();
  header(s, 'Neyin gerçek, neyin geçici olduğu', 'Olduğundan iyi göstermemek için açıkça ayırıyorum');

  const rows = [
    [
      { text: 'Bileşen', options: { bold: true, color: C.white, fill: { color: C.deep } } },
      { text: 'Durum', options: { bold: true, color: C.white, fill: { color: C.deep } } },
      { text: 'Not', options: { bold: true, color: C.white, fill: { color: C.deep } } },
    ],
    ['Mail ayrıştırma', 'Gerçek', 'Gerçek maillerinizle doğrulandı'],
    ['Gmail\'den okuma', 'Gerçek', 'Kişisel kutuya bağlı ve çalışıyor'],
    ['Veritabanı, panel işlemleri', 'Gerçek', 'SQL Server, tam işlevsel'],
    ['Yetkilendirme, denetim kaydı', 'Gerçek', 'Sunucu tarafında zorlanıyor'],
    ['Kullanıcı girişi', 'Geçici', 'Parola yok; listeden kullanıcı seçiliyor'],
    ['Hatırlatma maili gönderimi', 'Geçici', 'Gerçekten gönderilmiyor, dosyaya yazılıyor'],
    ['Menarini kutularını okuma', 'Kapalı', 'Workspace yöneticisi uygulamayı engelliyor'],
  ];

  s.addTable(rows, {
    x: M, y: 1.75, w: CONTENT_W,
    colW: [4.3, 2.2, 5.63],
    fontFace: F.body, fontSize: 12.5, color: C.ink,
    border: { type: 'solid', color: 'DCE7ED', pt: 1 },
    rowH: 0.5, valign: 'middle',
  });

  s.addShape(pres.ShapeType.roundRect, {
    x: M, y: 6.1, w: CONTENT_W, h: 0.95, rectRadius: 0.08,
    fill: { color: C.tintWarm }, line: { color: 'E8D4BF', width: 1 },
  });
  s.addText('Geçici olan üç maddenin üçü de ayar değişikliğiyle açılıyor; kod tarafı hazır ve derleniyor.', {
    x: M + 0.35, y: 6.3, w: CONTENT_W - 0.7, h: 0.55,
    fontFace: F.body, fontSize: 13, color: C.ink, margin: 0,
  });

  s.addNotes('Dürüstlük slaytı. Giriş ve mail gönderimi tek ayarla gerçeğe döner. Menarini kutuları yönetici onayına bağlı.');
}

// ================================================================ 14. Engeller
{
  const s = pres.addSlide();
  header(s, 'Karşılaşılan engeller', 'Kod dışı, izin kaynaklı — çözümü bende değil');

  const blocks = [
    ['Google Workspace kısıtı',
     'Menarini hesabıyla OAuth denemesi "access_not_configured" veriyor. Yönetici doğrulanmamış üçüncü taraf uygulamalara izin vermiyor.',
     'IT onayı gerekiyor'],
    ['Proje açma yetkisi yok',
     'Menarini hesabı Google Cloud\'da proje açamıyor. Geliştirme için kişisel hesap kullanıldı.',
     'Kalıcı çözüm kurumsal proje'],
    ['Yönetici izni gerektiren kurulumlar',
     '.NET ve Node.js kurulumları UAC onayı istedi; kullanıcı dizinine kurularak aşıldı.',
     'Aşıldı'],
  ];

  let y = 1.8;
  blocks.forEach(([h, b, tag], i) => {
    card(s, M, y, CONTENT_W, 1.62, { fill: i === 2 ? 'EAF3EC' : C.tintWarm, line: i === 2 ? 'C8DFCC' : 'E8D4BF' });
    badge(s, M + 0.32, y + 0.32, String(i + 1), i === 2 ? C.green : C.amber);
    s.addText(h, {
      x: M + 1.0, y: y + 0.2, w: 5.4, h: 0.35,
      fontFace: F.body, fontSize: 14.5, bold: true, color: C.deep, margin: 0,
    });
    s.addText(b, {
      x: M + 1.0, y: y + 0.58, w: 7.6, h: 0.7,
      fontFace: F.body, fontSize: 12, color: C.muted, margin: 0, valign: 'top', lineSpacingMultiple: 1.1,
    });
    s.addText(tag, {
      x: M + 8.8, y: y + 0.48, w: CONTENT_W - 9.2, h: 0.45,
      fontFace: F.body, fontSize: 12.5, bold: true, color: i === 2 ? C.green : C.amber,
      align: 'right', margin: 0,
    });
    y += 1.8;
  });

  s.addText('Alternatif yol: çalışanlar ticket maillerini tek bir kutuya yönlendirirse yönetici onayı gerekmeden çalışır — ve kimsenin posta kutusuna erişilmez.', {
    x: M, y: 6.95, w: CONTENT_W, h: 0.45,
    fontFace: F.body, fontSize: 12.5, color: C.muted, italic: true, margin: 0,
  });

  s.addNotes('Üçüncüsü çözüldü. İlk ikisi izin meselesi. Yönlendirme alternatifi hem daha hızlı hem KVKK açısından daha rahat.');
}

// ================================================================ 15. Açık konular
{
  const s = pres.addSlide();
  s.background = { color: C.deep };

  s.addText('Açık konular ve kararınızı bekleyenler', {
    x: M, y: 0.7, w: CONTENT_W, h: 0.7,
    fontFace: F.head, fontSize: 34, bold: true, color: C.white, margin: 0,
  });

  const items = [
    ['Çalışan kutuları okunsun mu?', 'Teknik olarak hazır. Ancak hukuk/İK onayı ve çalışan aydınlatması gerekir. Alternatif: mailleri tek kutuya yönlendirmek.'],
    ['Gerçek giriş açılsın mı?', 'Google girişi kodda hazır. Açılırsa parolasız erişim biter. Adresiniz "ext." alt alanında olduğu için domain listesi güncellenmeli.'],
    ['Nerede çalışacak?', 'Şu an geliştirme makinesinde. Ekip kullanımı için tek bir sunucuda çalışması gerekir.'],
    ['Yaşlandırma eşikleri doğru mu?', '2 / 5 / 7 gün varsayıldı. Sizin beklentinizle örtüşüyor mu?'],
  ];

  let y = 1.75;
  items.forEach(([h, b], i) => {
    s.addShape(pres.ShapeType.roundRect, {
      x: M, y, w: CONTENT_W, h: 1.12, rectRadius: 0.08,
      fill: { color: '17405A' }, line: { color: '2E6E8E', width: 1 },
    });
    badge(s, M + 0.32, y + 0.35, String(i + 1), C.light);
    s.addText(h, {
      x: M + 1.0, y: y + 0.16, w: CONTENT_W - 1.4, h: 0.34,
      fontFace: F.body, fontSize: 15, bold: true, color: C.white, margin: 0,
    });
    s.addText(b, {
      x: M + 1.0, y: y + 0.52, w: CONTENT_W - 1.4, h: 0.5,
      fontFace: F.body, fontSize: 12.5, color: 'B9D4E2', margin: 0, lineSpacingMultiple: 1.1,
    });
    y += 1.24;
  });

  s.addText('Sistem şu anda çalışır durumda ve gerçek maillerinizle test edildi. Yukarıdaki dört madde teknik değil, karar konusudur.', {
    x: M, y: 6.75, w: CONTENT_W, h: 0.45,
    fontFace: F.body, fontSize: 13, color: C.light, italic: true, margin: 0,
  });

  s.addNotes('Kapanış. Dört madde de benim karar veremeyeceğim konular; teknik taraf hazır.');
}

pres.writeFile({ fileName: 'IT-Yonetim-Paneli-Durum-Raporu.pptx' })
  .then(f => console.log('Yazildi:', f));





