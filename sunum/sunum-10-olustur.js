// 10 slaytlık güncel durum sunumu (Ahmet Bey için).
// Tema, ilk sunumdaki (sunum-olustur.js) görsel dille aynı: uygulamanın kendi renkleri.
const pptxgen = require('pptxgenjs');

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
  bg:     'FFFFFF',
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

function card(s, x, y, w, h, opts) {
  const o = opts || {};
  s.addShape(pres.ShapeType.roundRect, {
    x, y, w, h, rectRadius: 0.08,
    fill: { color: o.fill || C.tint },
    line: { color: o.line || 'DCE7ED', width: 1 },
  });
}

// Motif: dolu daire içinde numara/işaret — her slaytta tekrar eder.
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
  // valign: 'top' şart — kutu içerikten yüksekse metin ortalanıp başlıktan kopuyor.
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
    'DURUM SUNUMU',
    'Mail Tabanlı IT Yönetim Paneli',
    'Service Desk ticket maillerini otomatik okuyup tek ekranda toplayan iç kullanım uygulaması');

  s.addText('Lara Karaahmet  ·  Staj Projesi  ·  Ağustos 2026', {
    x: M, y: 6.4, w: CONTENT_W, h: 0.4,
    fontFace: F.body, fontSize: 12, color: '8FB6C8', margin: 0,
  });

  s.addNotes(
    'Bugün gösterecegim sey calisan bir uygulama, tasarim degil. ' +
    'Kendi Gmail kutumdan gercek ticket maillerini okuyor. ' +
    'Once ne yaptigini, sonra ne YAPMADIGINI anlatacagim — ikincisi daha onemli.');
}

// ================================================================ 2. Problem
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

  s.addNotes(
    'Bu dort maddeyi sizinle konustugumuz ihtiyactan cikardim. ' +
    'Hicbiri Tixbox eksigi degil — Tixbox kaydi tutuyor, biz takibi kolaylastiriyoruz.');
}

// ================================================================ 3. Ne yapıyor
{
  const s = pres.addSlide();
  header(s, 'Panel ne yapıyor', 'Mailden ekrana, elle işlem olmadan');

  // Akış: 4 adım
  const steps = [
    ['Okur', 'Tanımlı posta kutularını 5 dakikada bir tarar'],
    ['Ayrıştırır', 'Konu ve gövdeden ticket alanlarını çıkarır'],
    ['Eşleştirir', 'Aynı ticket birden çok kutuda olsa tek kayıt açar'],
    ['Gösterir', 'Dashboard, atama, durum, hatırlatma'],
  ];

  // 4 kart + 3 boşluk, içerik genişliğine tam otursun: 4*2.68 + 3*0.45 = 12.07 ≤ 12.13
  const cw = 2.68;
  const gap = 0.45;
  let x = M;
  steps.forEach(([head, body], i) => {
    card(s, x, 1.85, cw, 1.9);
    badge(s, x + 0.28, 2.1, String(i + 1), C.deep);
    s.addText(head, {
      x: x + 0.28, y: 2.68, w: cw - 0.56, h: 0.35,
      fontFace: F.head, fontSize: 18, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: x + 0.28, y: 3.05, w: cw - 0.56, h: 0.6,
      fontFace: F.body, fontSize: 12, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.1,
    });
    x += cw + gap;
  });

  // Altta: kapsam içi yetenekler
  card(s, M, 4.1, CONTENT_W, 2.5, { fill: C.white, line: 'DCE7ED' });

  const caps = [
    ['Ticket listesi ve arama', 'Numara, talep eden, uygulama, açıklama ve posta kutusunda arama'],
    ['Atama ve durum takibi', 'Çalışan durumu günceller, müdür anında görür'],
    ['Hatırlatma maili', 'Önizleme → onay → gönderim; onaysız gönderilmez'],
    ['Haftalık çalışma takvimi', 'Ofis / home office / izin planı tek ekranda'],
    ['Elle ticket ekleme', 'Maili düşmemiş Tixbox kaydı numarasıyla girilir'],
    ['Denetim kaydı', 'Kim, ne zaman, neyi değiştirdi'],
  ];

  caps.forEach(([head, body], i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    cardText(s, M + 0.35 + col * 4.05, 4.4 + row * 1.05, 3.7, head, body,
      { headSize: 13.5, bodySize: 11.5, bodyH: 0.55 });
  });

  s.addNotes(
    'Ust siradaki dort adim otomatik; kimse bir sey yapmadan calisiyor. ' +
    'Alttaki alti yetenek de sizin ekranda yaptiklariniz.');
}

// ================================================================ 4. Ne YAPMIYOR
{
  const s = pres.addSlide();
  header(s, 'Panel ne yapmıyor', 'Baştan konuşulması gereken sınırlar');

  const nos = [
    ['Tixbox\'a yazmaz', 'Panel Tixbox üzerinde ticket açmaz, güncellemez, kapatmaz. Buradaki durumlar yalnızca yönetim panelindeki takip durumudur. Bu uyarı her ekranda yazılı.'],
    ['SLA veya hedef tarih üretmez', 'Tixbox\'tan SLA verisi gelmiyor. Uydurma bir hedef tarih göstermek yanlış yönlendirir; onun yerine "kaç gündür açık" ve "kaç gündür güncellenmedi" gösteriliyor. Eşikler ayarlardan değiştirilebilir.'],
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
    'Bu slaydi bilerek one aldim. Panelin Tixbox\'i degistirmedigini herkesin bilmesi lazim, ' +
    'yoksa iki yerde farkli durum olusur. ' +
    'SLA konusunu da ozellikle soyluyorum: veri olmadigi icin uydurmadik.');
}

// ================================================================ 5. Müdür ekranı
{
  const s = pres.addSlide();
  header(s, 'Müdür ekranı', 'Günlük bakılacak tek yer');

  // Sol: dashboard bölümleri
  card(s, M, 1.75, 6.0, 4.6);
  s.addText('Dashboard', {
    x: M + 0.35, y: 1.98, w: 5.3, h: 0.4,
    fontFace: F.head, fontSize: 20, bold: true, color: C.deep, margin: 0, valign: 'top',
  });

  const dash = [
    'Açık, atanmamış, devam eden, uzun süredir açık sayıları',
    'Atanmamış ticket\'lar — doğrudan buradan atama',
    'Dikkat gerektirenler: uzun süredir açık veya veri uyumsuzluğu',
    'Ekipten gelen son güncellemeler: kim, hangi ticket, hangi geçiş',
    'Bugün kim ofiste, kim home office, kim izinli',
    'Çalışan bazlı açık iş sayısı',
    'Gelecek hafta planını göndermeyenler',
    '"Mailleri şimdi oku" — beklemeden elle tetikleme',
  ];

  s.addText(dash.map((t, i) => ({
    text: t,
    options: { bullet: true, breakLine: i !== dash.length - 1 },
  })), {
    x: M + 0.35, y: 2.5, w: 5.3, h: 3.5,
    fontFace: F.body, fontSize: 12.5, color: C.ink, margin: 0, valign: 'top',
    paraSpaceAfter: 8,
  });

  // Sağ: ticket listesi özellikleri
  card(s, M + 6.4, 1.75, CONTENT_W - 6.4, 4.6, { fill: C.white });
  s.addText('Ticket listesi', {
    x: M + 6.75, y: 1.98, w: CONTENT_W - 7.1, h: 0.4,
    fontFace: F.head, fontSize: 20, bold: true, color: C.deep, margin: 0, valign: 'top',
  });

  const feats = [
    ['Okunduğu kutu', 'Kaydın hangi posta kutusundan geldiği sütunda yazar; aynı mail iki kutuda ise ikisi de görünür'],
    ['Arama', 'Ticket no, talep eden, uygulama, açıklama ve posta kutusu'],
    ['Yaş durumu', '"Uzun süredir açık" / "Güncelleme bekliyor" — SLA değil'],
    ['Elle ticket ekleme', 'Numara kuralı mailden gelenle aynı; aynı numara iki kez eklenemez'],
  ];

  let fy = 2.5;
  feats.forEach(([h, b]) => {
    cardText(s, M + 6.75, fy, CONTENT_W - 7.1, h, b,
      { headSize: 13.5, bodySize: 11.5, bodyH: 0.62 });
    fy += 1.0;
  });

  s.addNotes(
    'Dashboard sabahlari bakilacak ekran. ' +
    '"Okundugu kutu" sutununu siz istemistiniz — coklu kutu okumaya gectikten sonra ' +
    'bir kaydin nereden geldigi belirsizdi.');
}

// ================================================================ 6. Çalışan ekranı
{
  const s = pres.addSlide();
  header(s, 'Çalışan tarafı', 'Ekip kendi işini kendi günceller, siz görürsünüz');

  const cols = [
    ['Ticket\'larım', [
      'Yalnızca kendine atanmış kayıtlar',
      'Durumu ileri ve geri alabilir',
      'Not ekleyebilir',
      'Yaptığı değişiklik müdür dashboard\'una düşer',
    ], C.deep],
    ['Çalışma planım', [
      'Haftalık ofis / home office / izin planı',
      'Gönderim sonrası müdür onayına düşer',
      'Müdür gerekirse override edebilir',
      'Resmî tatiller takvimde işaretli',
    ], C.mid],
    ['Otomatik atama', [
      'Ticket maili tek kişiye gelmişse o kişiye atanır',
      'Müdür ataması beklenmez',
      'Listede "otomatik atandı" ibaresi çıkar',
      'Müdür yine görür ve yeniden atayabilir',
    ], C.light],
  ];

  let x = M;
  const cw = 4.0;
  // Kart yüksekliği içeriğe göre: 4.4 iken altta yarım slayt boşluk kalıyordu.
  cols.forEach(([title, items, color], i) => {
    card(s, x, 1.8, cw, 3.35, { fill: C.white });
    badge(s, x + 0.35, 2.08, String(i + 1), color);
    s.addText(title, {
      x: x + 0.35, y: 2.62, w: cw - 0.7, h: 0.45,
      fontFace: F.head, fontSize: 18, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(items.map((t, j) => ({
      text: t,
      options: { bullet: true, breakLine: j !== items.length - 1 },
    })), {
      x: x + 0.35, y: 3.15, w: cw - 0.7, h: 1.85,
      fontFace: F.body, fontSize: 12, color: C.muted, margin: 0, valign: 'top',
      paraSpaceAfter: 8,
    });
    x += cw + 0.35;
  });

  s.addNotes(
    'Buradaki kritik nokta: calisan yalnizca kendi kayitlarini gorur ve bu kisit ' +
    'sunucu tarafinda zorlanir. Arayuzde dugme gizlemek guvenlik degil.');
}

// ================================================================ 7. Kurulum
{
  const s = pres.addSlide();
  header(s, 'Kurulum ve kullanım', 'Kurulum gerektirmeyen tek klasör');

  const steps = [
    ['Aç', 'ZIP\'i ayıklayıp Baslat.cmd\'ye çift tıklayın. .NET, Node.js, SQL Server gerekmez — hepsi klasörün içinde.'],
    ['Parolanı belirle', 'İlk açılışta yönetici parolasını siz koyarsınız. Sonraki girişlerde e-posta + parola sorulur.'],
    ['Gmail\'i bağla', 'Yönetim → Posta kutuları → adresi ekleyin, "Yetkilendir" deyin, Google onayını verin.'],
    ['Ekibi ekle', 'Yönetim → Kullanıcılar. Başlangıç parolasını iletirsiniz, kişi ilk girişte kendi parolasını belirler.'],
  ];

  let y = 1.8;
  steps.forEach(([head, body], i) => {
    card(s, M, y, 7.4, 1.05);
    badge(s, M + 0.3, y + 0.32, String(i + 1), C.deep);
    s.addText(head, {
      x: M + 0.95, y: y + 0.16, w: 6.2, h: 0.32,
      fontFace: F.body, fontSize: 14.5, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 0.95, y: y + 0.48, w: 6.2, h: 0.5,
      fontFace: F.body, fontSize: 11.5, color: C.muted, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.05,
    });
    y += 1.2;
  });

  // Sağ: ekip erişimi
  card(s, M + 7.8, 1.8, CONTENT_W - 7.8, 4.45, { fill: C.tint });
  s.addText('Ekibin aynı panele bağlanması', {
    x: M + 8.1, y: 2.05, w: CONTENT_W - 8.4, h: 0.7,
    fontFace: F.head, fontSize: 17, bold: true, color: C.deep, margin: 0, valign: 'top',
  });
  s.addText(
    'Veriler uygulamanın çalıştığı bilgisayarda durur. Ekibin aynı verileri görmesi için ' +
    'uygulama tek bilgisayarda çalışır, diğerleri tarayıcıdan bağlanır.\n\n' +
    'Baslat.cmd yerine Paylas.cmd çalıştırılır; ekranda çıkan adres ekibe verilir.\n\n' +
    'Herkes ZIP\'i kendi bilgisayarında açarsa veriler paylaşılmaz — her biri ayrı ' +
    'veritabanı oluşturur.', {
    x: M + 8.1, y: 2.85, w: CONTENT_W - 8.4, h: 3.2,
    fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top',
    lineSpacingMultiple: 1.15,
  });

  s.addNotes(
    'Kurulum gerektirmemesi bilincli bir tercihti: IT\'den kurulum izni beklemeden ' +
    'denenebilsin diye. Son maddeyi ozellikle vurgulayin — herkes kendi kopyasini ' +
    'acarsa atamalar birbirine gorunmez.');
}

// ================================================================ 8. Güvenlik
{
  const s = pres.addSlide();
  header(s, 'Erişim ve veri güvenliği', 'Panel ekibe açıldığı için tek koruma katmanı');

  const rows = [
    ['Parola ile giriş', 'Parolalar açık saklanmaz; PBKDF2 ile özetlenir. 5 hatalı denemeden sonra hesap 15 dakika kilitlenir.'],
    ['Rol ayrımı', 'Çalışan yalnızca kendi ticket\'larını görür. Bu kısıt sunucu tarafında zorlanır; istemci filtreyi değiştirerek kapsamı genişletemez.'],
    ['Oturum kontrolü', 'Oturumlar sunucuda tutulur. Bir kullanıcıyı pasifleştirdiğinizde açık oturumu anında düşer.'],
    ['Gmail yalnızca okunur', 'Uygulama maili okur; silmez, değiştirmez, göndermez. Her posta kutusu sahibi kendi onayını kendi verir.'],
    ['Denetim kaydı', 'Atama, durum değişikliği, kullanıcı işlemleri kaydedilir ve silinmez.'],
  ];

  // 5 satır + alttaki not: son kart 6.3'ten önce bitmeli, yoksa not kartın üstüne biner.
  let y = 1.7;
  rows.forEach(([head, body]) => {
    card(s, M, y, CONTENT_W, 0.85, { fill: C.tintGreen, line: 'D3E4D6' });
    badge(s, M + 0.3, y + 0.22, '✓', C.green);
    s.addText(head, {
      x: M + 0.95, y: y + 0.12, w: 3.0, h: 0.32,
      fontFace: F.body, fontSize: 14, bold: true, color: C.deep, margin: 0, valign: 'top',
    });
    s.addText(body, {
      x: M + 4.05, y: y + 0.13, w: CONTENT_W - 4.4, h: 0.6,
      fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top',
      lineSpacingMultiple: 1.05,
    });
    y += 0.95;
  });

  s.addText(
    'Not: Çalışan posta kutularının okunması, tek yöneticinin kutusunu okumaktan farklı bir ' +
    'yetki seviyesidir. Filtre dar tutuldu (gönderen + konu kalıbı) ve her çalışan kendi ' +
    'onayını kendisi veriyor; yine de KVKK açısından İK/hukuk onayı gerekiyor.', {
    x: M, y: 6.55, w: CONTENT_W, h: 0.7,
    fontFace: F.body, fontSize: 11.5, color: C.amber, margin: 0, valign: 'top',
    italic: true, lineSpacingMultiple: 1.05,
  });

  s.addNotes(
    'Alttaki nota dikkat cekin. Teknik olarak hazir ama KVKK onayi sizin ve IK\'nin karari. ' +
    'Bu onay alinmadan calisan kutularini okumaya baslamamak lazim.');
}

// ================================================================ 9. Bugünkü durum
{
  const s = pres.addSlide();
  header(s, 'Bugünkü durum', 'Gerçek Gmail kutularıyla çalışıyor');

  card(s, M, 1.8, CONTENT_W, 1.75, { fill: C.tint });
  stat(s, M + 0.4, 2.05, 2.6, '223', 'otomatik test', C.deep);
  stat(s, M + 3.3, 2.05, 2.6, '2', 'bağlı posta kutusu', C.mid);
  stat(s, M + 6.2, 2.05, 2.6, '5', 'okunan gerçek ticket', C.green);
  stat(s, M + 9.1, 2.05, 2.6, '0', 'Tixbox\'a yazma', C.amber);

  card(s, M, 3.8, 6.0, 2.6, { fill: C.white });
  s.addText('Tamamlananlar', {
    x: M + 0.35, y: 4.0, w: 5.3, h: 0.4,
    fontFace: F.head, fontSize: 18, bold: true, color: C.green, margin: 0, valign: 'top',
  });
  const done = [
    'Mail okuma, ayrıştırma, forward desteği',
    'Çoklu posta kutusu + otomatik atama',
    'Atama, durum, not, hatırlatma akışı',
    'Haftalık çalışma takvimi',
    'Parolalı giriş ve yönetim ekranı',
    'Kurulum gerektirmeyen paket',
  ];
  s.addText(done.map((t, i) => ({
    text: t, options: { bullet: true, breakLine: i !== done.length - 1 },
  })), {
    x: M + 0.35, y: 4.5, w: 5.3, h: 1.8,
    fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top', paraSpaceAfter: 6,
  });

  card(s, M + 6.4, 3.8, CONTENT_W - 6.4, 2.6, { fill: C.white });
  s.addText('Yol boyunca çıkan ve düzeltilen hatalar', {
    x: M + 6.75, y: 4.0, w: CONTENT_W - 7.1, h: 0.4,
    fontFace: F.head, fontSize: 18, bold: true, color: C.deep, margin: 0, valign: 'top',
  });
  const fixed = [
    'Forward mailde konu satırı bölünüyordu',
    'Tarih Türkçe kültürde yanlış biçimleniyordu',
    'Saat dilimi 3 saat kayıyordu',
    'Başarısız okuma eski mailleri atlatıyordu',
    'Yetki kontrolleri sessizce düşüyordu',
  ];
  s.addText(fixed.map((t, i) => ({
    text: t, options: { bullet: true, breakLine: i !== fixed.length - 1 },
  })), {
    x: M + 6.75, y: 4.5, w: CONTENT_W - 7.1, h: 1.8,
    fontFace: F.body, fontSize: 12, color: C.ink, margin: 0, valign: 'top', paraSpaceAfter: 6,
  });

  s.addNotes(
    'Sagdaki listeyi bilerek koydum. Bu hatalarin hepsi hata mesaji VERMEYEN turdendi; ' +
    'uygulamayi gercek kosullarda calistirinca ortaya ciktilar. ' +
    'Her biri icin tekrar olmasin diye test yazildi.');
}

// ================================================================ 10. Sonraki adımlar
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
    ['Google Cloud projesi', 'Şu an kişisel bir Google projesi kullanılıyor; kullanıcıya "doğrulanmamış uygulama" uyarısı çıkıyor. Menarini IT kendi projesini açarsa bu uyarı kalkar. Kod değişikliği gerekmiyor.'],
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
