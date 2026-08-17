# IT Yönetim Paneli — Kullanım Kılavuzu

Bu belge uygulamayı **kullanacak** kişiler içindir; kod bilgisi gerektirmez.

---

## 1. Bu panel ne yapar, ne yapmaz

**Yapar**

- Service Desk ticket maillerini posta kutularından otomatik okur ve ayrıştırır.
- Ticket'ları tek ekranda listeler; kimin üzerinde, kaç gündür açık gösterir.
- Yönetici ticket atar, ekip durumunu günceller, yönetici bunu anında görür.
- Haftalık ofis / home office / izin planını tek ekranda toplar.
- Yöneticinin onayıyla hatırlatma maili gönderir.

**Yapmaz**

- **Tixbox'a hiçbir şey yazmaz.** Buradaki durumlar yalnızca bu panelin takip
  durumudur. Tixbox'taki kayıt değişmez.
- **SLA veya hedef tarih hesaplamaz.** Tixbox'ta bu veri yok. Panel yalnızca
  "kaç gündür açık" ve "kaç gündür güncellenmedi" bilgisini gösterir.
- Onaysız mail göndermez.

---

## 2. Kurulum

### 2.1 Uygulamayı açma

1. Size gönderilen `IT-Yonetim-Paneli.zip` dosyasına sağ tıklayın →
   **"Tümünü ayıkla"** (Extract All).
   ZIP'in içinden doğrudan çalıştırmayın; açılmaz.
2. Ayıklanan klasördeki **`Baslat.cmd`** dosyasına çift tıklayın.
3. Siyah bir pencere açılır. Sunucu hazır olunca tarayıcı kendiliğinden açılır.
   İlk açılışta veritabanı oluşturulduğu için 10–30 saniye sürebilir.

> "Windows bilgisayarınızı korudu" uyarısı çıkarsa: **Daha fazla bilgi** →
> **Yine de çalıştır**. Uygulama imzalı olmadığı için bu uyarı normaldir.

Uygulamayı kapatmak için siyah pencereyi kapatın.

### 2.2 İlk kurulum — yönetici parolası

İlk açılışta **"İlk kurulum"** ekranı gelir.

1. Yönetici e-postasını kontrol edin (hazır gelir).
2. Kendi belirleyeceğiniz parolayı iki kez girin. **En az 8 karakter.**
3. **Kurulumu tamamla** deyin. Doğrudan panele girersiniz.

Bu ekran yalnızca bir kez görünür. Sonraki açılışlarda e-posta + parola sorulur.

---

## 3. Gmail'i bağlama

Sol menü → **Yönetim** → **Posta kutuları**

1. Okunacak adresi yazıp **Ekle** deyin.
2. Adresin yanındaki **Yetkilendir** düğmesine basın.
3. Tarayıcıda Google onay ekranı açılır. İlgili hesabı seçin.
4. **"Google bu uygulamayı doğrulamadı"** uyarısı çıkarsa
   **Gelişmiş** → **… uygulamasına git** deyin.
   Bu uyarı, uygulamanın Google incelemesinden geçmemiş olmasından kaynaklanır.
   Uygulama postalarınızı yalnızca **okur**, değiştirmez ve silmez.
5. Onay verdikten sonra **Mailleri şimdi oku** ile ilk okumayı yapın.

Her posta kutusu için bu adımlar ayrı ayrı tekrarlanır. Sonrasında mailler
arka planda otomatik okunur (varsayılan: 5 dakikada bir).

**Önemli:** Onay penceresi, uygulamanın **çalıştığı bilgisayarda** açılır.
Ağdan bağlanan bir kullanıcı "Yetkilendir" derse pencere kendi ekranında
değil, sunucu bilgisayarında açılır.

### Neden birden fazla kutu?

Ticket maili çoğu zaman bir gruba gider. Bir ticket yöneticinin kutusuna hiç
düşmeden bir çalışanın kutusunda olabilir. Birden fazla kutu eklenirse hepsi
okunur ama **tek ticket kaydı** açılır; listede "Okunduğu kutu" sütununda
hangi kutulardan geldiği yazar.

---

## 4. Kullanıcılar

**Yönetim** → **Kullanıcılar**

### Kullanıcı ekleme

1. **Kullanıcı ekle** → e-posta, ad soyad, ünvan, rol ve bir **başlangıç
   parolası** girin.
2. Bu parolayı kişiye iletin.
3. Kişi ilk girişinde kendi parolasını belirlemek zorundadır — böylece siz
   parolayı bilmeye devam etmezsiniz.

### Roller

| Rol | Ne görür / ne yapar |
|-----|---------------------|
| **Çalışan** | Yalnızca kendine atanmış ticket'ları görür, durumunu günceller, not ekler |
| **Yönetici** | Tüm ticket'lar, atama, hatırlatma, ekip takvimi, yönetim ekranı |
| **Sistem yöneticisi** | Yöneticinin yaptığı her şey |

### Parola sıfırlama

Kullanıcı parolasını unutursa **Parola sıfırla** ile yeni bir başlangıç
parolası verin. Kişinin açık oturumları kapanır ve ilk girişinde parolayı
değiştirmesi istenir.

### Pasifleştirme

Kullanıcılar **silinmez** — geçmiş atamalar ve durum geçmişi korunmak zorunda.
**Pasifleştir** dediğinizde kişi giriş yapamaz, açık oturumu anında düşer ve
yeni ticket alamaz; geçmiş kayıtlarda görünmeye devam eder.

Son yöneticiyi pasifleştiremezsiniz; önce başka bir yönetici tanımlayın.

---

## 5. Ekibin aynı panele bağlanması

Veriler, uygulamanın çalıştığı bilgisayarda durur. Ekibin **aynı** verileri
görmesi için uygulama **tek bir bilgisayarda** çalışmalı, diğerleri tarayıcıdan
bağlanmalıdır.

1. `Baslat.cmd` yerine **`Paylas.cmd`** dosyasını çalıştırın.
2. Ekranda yazan adresi (örnek: `http://192.168.1.25:5080`) ekibe verin.
3. O bilgisayar açık ve uygulama çalışır durumda olmalı.

İlk seferde Windows Güvenlik Duvarı izin isteyebilir; **Özel ağlar** için izin
verin. Gerekirse PowerShell'i **yönetici olarak** açıp:

```
New-NetFirewallRule -DisplayName "IT Yonetim Paneli" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow -Profile Domain,Private
```

> Herkes ZIP'i kendi bilgisayarında açarsa **veriler paylaşılmaz** — her biri
> ayrı bir veritabanı oluşturur ve atamalar karşılıklı görünmez.

---

## 6. Günlük kullanım

### Yönetici

- **Dashboard** — açık ticket sayıları, atanmamışlar, dikkat gerektirenler,
  bugün kim ofiste, ekipten gelen son güncellemeler.
- **Ticket'lar** — arama, filtre, atama. Satıra tıklayınca detay açılır.
  - Arama kutusu ticket numarası, talep eden, uygulama, açıklama **ve posta
    kutusu** üzerinde çalışır.
  - **Elle ticket ekle** — maili panele düşmemiş bir Tixbox kaydını numarasıyla
    girer. Tixbox'ta ticket **açmaz**.
- **Hatırlatma gönder** — önizleme → onay → gönderim. Onaysız gönderilmez.

### Çalışan

- **Ticket'larım** — size atanmış kayıtlar. Durumu güncelleyin, not ekleyin;
  yöneticiniz dashboard'da görür. Durumu geriye de alabilirsiniz.
- **Çalışma planım** — haftalık ofis / home office / izin planı.

---

## 7. Yedekleme

Bütün veriler uygulama klasöründeki **`it-cockpit.db`** dosyasındadır.

Yedek almak için uygulamayı **kapatın** ve şu dosyaları kopyalayın:

- `it-cockpit.db`
- `it-cockpit.db-wal` (varsa)
- `it-cockpit.db-shm` (varsa)

Geri yüklemek için aynı dosyaları yerine koyup tekrar başlatın.
Sıfırdan başlamak için bu dosyaları silin — **tüm veriler gider.**

Klasörü başka yere taşırsanız üç dosyayı da birlikte taşıyın.

---

## 8. Sık karşılaşılan durumlar

| Belirti | Sebep / çözüm |
|---------|----------------|
| "Bu sayfaya ulaşılamıyor" | Sunucu henüz açılmamış. Siyah pencere açıksa birkaç saniye bekleyip sayfayı yenileyin. |
| "Sunucuya ulaşılamıyor" | Siyah pencere kapanmış. `Baslat.cmd` ile tekrar açın. |
| "E-posta veya parola hatalı" | Parola yanlış, ya da hesabınıza henüz parola tanımlanmamış. Yöneticinizden sıfırlamasını isteyin. |
| "Çok fazla hatalı deneme" | Hesap 15 dakika kilitlendi. Süre dolunca kendiliğinden açılır. |
| "Oturumunuz sona ermiş" | Oturum süresi 14 gün. Tekrar giriş yapın. |
| Ağdan bağlananlar göremiyor | `Baslat.cmd` yerine `Paylas.cmd` kullanılmalı; güvenlik duvarı izni gerekebilir. |
| Posta kutusunda "Son hata" yazıyor | Kutu yetkilendirilmemiş olabilir. **Yetkilendir** düğmesine basın. |
| Mailler okunmuyor | Yönetim → Posta kutuları → **Mailleri şimdi oku** ile deneyin; "Sıradaki adım" satırındaki uyarıyı okuyun. |

---

## 9. Bilinmesi gerekenler

- Panel Tixbox'a **hiçbir şey yazmaz**. Her ekranda bu uyarı görünür.
- Parolalar veritabanında **açık saklanmaz** (PBKDF2 ile özetlenir).
- Oturum bilgileri sunucu tarafında tutulur; bir kullanıcı pasifleştirildiğinde
  açık oturumu anında düşer.
- Denetim kayıtları (`AuditLogs`) hiçbir koşulda silinmez.
- Uygulama internete açılmak için tasarlanmadı; **şirket içi ağda** kullanın.
