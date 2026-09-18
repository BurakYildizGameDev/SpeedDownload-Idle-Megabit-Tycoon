# Play Store Yayın Kontrol Listesi

**Sürüm:** 1.0 (versionCode 1)
**Güncelleme:** 2026-09-17 — kapalı test öncesi tam denetim
**Durum:** Projenin gerçek ayarları ve **derlenmiş AAB'nin içi** taranarak hazırlandı — tahmin değil, ölçüm.

> Önceki sürüm (2026-08-14) `RELEASE_CHECKLIST_2026-08-14_yedek.md` olarak saklandı.
> O belge AdMob entegrasyonundan **önce** yazılmıştı ve reklam/izin bölümleri artık geçersiz.

---

## 0. Özet — Kod tarafında sert bloker kalmadı

| # | Eski bloker | Durum |
|---|---|---|
| 1 | İmzalama anahtarı (keystore) yok | ✅ **Kapandı** — oluşturuldu ve bağlandı |
| 2 | AAB değil APK üretiliyor | ✅ Kapandı — 30 MB AAB mevcut |
| 3 | Target SDK "Auto" | ✅ Kapandı — **36**'ya pinlendi |
| 4 | Dört kalite sorunu (titreşim, çift ses, ısı göstergesi, ağaç) | ✅ Kapandı |

**Kalan iş Play Console tarafında** (Bölüm 4). Kodda kapalı testi engelleyen bir şey yok.

---

## 1. Doğrulanmış Proje Ayarları

`ProjectSettings.asset` üzerinden okundu:

`Verify Mobile Player Settings` çıktısı (2026-09-17, sorun bulunmadı):

```
paket adı   : com.speeddownload.megabittycoon
sürüm       : 1.0  /  versionCode 1
minSdk      : 26 (Android 8.0)      targetSdk : 36
backend     : IL2CPP                mimari    : ARMv7 + ARM64
yönelim     : Portre kilitli        safe area : açık
keystore    : kurulu                alias     : burak1234
çıktı       : AAB
```

**Keystore konumu:**
`C:/Users/Burak/Desktop/Keystore/Speed Dowland Idle Megabit Tycoon/Speed Dowland Idle Megabit Tycoon.keystore`

> ⚠ **Bu dosyayı kaybedersen uygulamayı bir daha asla güncelleyemezsin.** Şu an tek kopya
> masaüstünde — yani disk arızasında oyun kalıcı olarak güncellenemez hale gelir.
> **Bugün** en az iki ayrı yere yedekle (harici disk + şifreli bulut), parolayı ayrı sakla.
> İlk yüklemede Play App Signing'i aç — o zaman Google upload key'i yönetir ve risk azalır.

### 1.1 ✅ Mimari — ARMv7 geri açıldı (2026-09-17)

Denetimde proje ayarı `AndroidTargetArchitectures: 2` (yalnız ARM64) çıktı; önceki belge
"ARMv7 + ARM64 yapıldı" diyordu, yani belge ile gerçek uyuşmuyordu. **Değer 3'e alındı.**

- `PlayerSettingsTool.cs:89` → `AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64`
- `ProjectSettings.asset:277` → `AndroidTargetArchitectures: 3`
- Araç ile proje ayarı artık tutarlı; `Verify` temiz geçiyor

ARM64-only bilinçli bir karardı: doğrudan dağıtılan **APK'da** ölçüm yapılmıştı ve 123,3 MB'lık
paketin 43,1 MB'ı ikinci mimari kopyasıydı. Ama dağıtım AAB'ye geçtiği için o gerekçe düştü —
**AAB'de Play cihaza yalnızca kendi mimarisini gönderir, oyuncunun indirme boyutu büyümez.**
Maliyet yalnızca build süresi ve yüklenen .aab dosyasının boyutu.

> Kazancın sınırlı olduğunu bil: minSdk 26 (Android 8.0, 2017) zaten 32-bit-only cihazların
> büyük kısmını dışarıda bırakıyor. Yine de AAB'de maliyet oyuncuya yansımadığı için açık
> bırakmak güvenli taraf.

---

## 2. Reklamlar — ÖNEMLİ DEĞİŞİKLİK

**Eski belge "uygulama reklam içermiyor işaretle" diyordu. Bu artık YANLIŞ.**
17 Ağustos'ta Google Mobile Ads SDK 11.3.0 entegre edildi.

### 2.1 Mevcut durum: test reklamları (doğru karar)

```
useTestAdUnitIds : 1  (sahnede serileştirilmiş)
AAB manifest app id : ca-app-pub-3940256099942544~3347511713  → Google resmi TEST kimliği
```

Kapalı testte test birimleriyle kalmak **doğru**. Gerçek birimlerle test etmek kendi
reklamına tıklama sayılır ve AdMob hesabının askıya alınmasına yol açar.

- [ ] Üretime çıkmadan **önce** gerçek AdMob birimlerine geç (`AdMobService` inspector'ında
      `Use Test Ad Unit Ids` kutusunu kaldır + `GoogleMobileAdsSettings` app id'sini değiştir)

### 2.2 Play Console beyanları — bunlar artık zorunlu

- [ ] **"Uygulama reklam içeriyor" → EVET** işaretle (SDK pakette, test reklamı da reklamdır)
- [ ] **Data Safety formu** — AdMob reklam kimliği topluyor, beyan et
- [ ] **Gizlilik politikası URL'si** — reklam SDK'sı olduğu için artık gerçekten zorunlu

### 2.3 İzinler — eski belgedeki liste geçersiz

Derlenmiş AAB'nin manifestinden okunan **gerçek** izinler:

| İzin | Kaynak |
|---|---|
| `INTERNET` | Unity + AdMob |
| `VIBRATE` | Haptic |
| `ACCESS_NETWORK_STATE` | AdMob |
| `ACCESS_ADSERVICES_AD_ID` | **AdMob — reklam kimliği. Data Safety'yi bu belirliyor.** |
| `ACCESS_ADSERVICES_ATTRIBUTION` | AdMob |
| `ACCESS_ADSERVICES_TOPICS` | AdMob |
| `WAKE_LOCK`, `FOREGROUND_SERVICE`, `BIND_JOB_SERVICE`, `DUMP` | Play Services |

Eski belgedeki "İzinler: INTERNET, VIBRATE — aşırı izin yok" satırı artık doğru değil.
Bu izinlerin hepsi AdMob'un normal gereksinimi, kaldırılamaz.

---

## 3. Doğrulanmış — Bunlar Sorunsuz

| Konu | Bulgu |
|---|---|
| Derleme | Oyun kaynaklı **0 hata, 0 uyarı** |
| İçerik | 41 yükseltme, 10 kademe, 24 dosya, 6 prestij yeteneği |
| Birim testi | 29 test |
| Debug HUD | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` ile korunuyor, yayında `enabled = false` |
| Kayıt şifreleme | `link.xml` + somut tip referansları ile IL2CPP budamasına karşı iki katmanlı koruma |
| Safe area | `androidRenderOutsideSafeArea: 1` + `SafeAreaFitter` |
| Uygulama simgesi | `logo_icon_appstore.png` 1024² |
| Ödüllü reklam mantığı | Denetlendi — doğru (Bölüm 3.2) |

> ⚠ **Kökteki 30 Ağustos tarihli AAB artık güncel değil.** Mimari ARMv7+ARM64'e çıkarıldığı
> için **yeniden build alınmalı**. Build süresi uzayacak (IL2CPP iki mimari derliyor).

### 3.1 Font / çok dil desteği — denetlendi, sorun yok

5 dil var (EN/TR/DE/ES/RU). Metinlerin tamamı `LiberationSans SDF` kullanıyor ve bu asset
**statik** atlas (250 glif, yalnız Latin-1). Tek başına bakıldığında Türkçe `ğ ı ş Ğ İ Ş` ve
tüm Kiril alfabesi eksik görünüyor — **ama değil.**

Asset kendi `m_FallbackFontAssetTable`'ında `LiberationSans SDF - Fallback` (Dynamic mod,
kaynak `LiberationSans.ttf`, çoklu atlas açık) tanımlı. TTF'in içinde 2294 kod noktası var;
Türkçe'nin 6 özel harfi ve Kiril'in 64/64'ü mevcut.

Doğrulama (2026-09-17):
- `TMP_FontAssetUtilities.GetCharacterFromFontAsset` ile 14/14 karakter **çözüldü**
- Kameraya render edilip gözle kontrol edildi: `Hızlı Başlangıç`, `Ağ Geçidi, Işık, Şifre`,
  `ĞÜŞİÖÇ ğüşiöç`, `РУССКИЙ ЯЗЫК`, `Straße Übertragung`, `¡Conexión rápida!` — hepsi temiz
- Derlenmiş AAB'nin `data.unity3d` dosyasında hem `LiberationSans` TTF'i hem
  `LiberationSans SDF - Fallback` asset'i **mevcut** → zincir cihazda da çalışır

> Not: `Screenshots/adpanel_test.png` (18 Ağustos cihaz görüntüsü) hiç yazı göstermiyor.
> O görüntü 30 Ağustos'taki son çalışmalardan öncesine ait. Cihaz testinde metinlerin
> göründüğünü ayrıca teyit et (Bölüm 5).

### 3.2 Ödüllü reklam akışı — kod denetlendi, doğru

`AdMobService.ShowRealRewarded` incelendi. Cihaz testinde aranacak davranışların hepsi
kodda doğru kurulmuş:

| Senaryo | Davranış |
|---|---|
| Reklam tam izlendi | `ad.Show(reward => earned = true)` → kapanışta `finish(earned)` → **ödül verilir** |
| Reklam erken kapatıldı | `earned` false kalır → `onSkipped` → **ödül verilmez** ✅ |
| Gösterim hatası | `OnAdFullScreenContentFailed` → `finish(false)` |
| Show istisnası | `try/catch` → `finish(false)` |
| Çift callback | `finished` guard'ı ikinci çağrıyı yutar |
| **Uçak modu / yükleme hatası** | `RealRewardedUsable` false → stub fallback, yoksa `onSkipped` → **kilitlenme yok** ✅ |
| Yükleme başarısız | `RetryInterval = 10f` ile 10 sn'de bir yeniden denenir |

Kodda düzeltilecek bir şey çıkmadı. Yine de gerçek cihazda bir kez doğrula (Bölüm 5) —
kod doğru olsa da SDK'nın cihazdaki davranışı ancak orada görülür.

---

## 4. Play Console Tarafı — Kalan Gerçek İş

- [ ] **Geliştirici hesabı** (tek seferlik $25)
- [ ] **Gizlilik politikası URL'si** — zorunlu (reklam SDK'sı var)
- [ ] **Data Safety formu** — reklam kimliği toplanıyor (Bölüm 2.2)
- [ ] **Reklam beyanı: "içeriyor" = EVET**
- [ ] **İçerik derecelendirme anketi**
- [ ] **Hedef kitle ve içerik** beyanı
- [ ] **Mağaza görselleri:** simge 512×512, feature graphic 1024×500, en az 2 portre telefon
      ekran görüntüsü
- [ ] **Mağaza metinleri:** kısa açıklama (80 karakter), tam açıklama (4000)
- [ ] **Kapalı test şartı** — yeni kişisel geliştirici hesapları için Google
      **12 test kullanıcısı / 14 gün kesintisiz** kapalı test istiyor. Güncel değeri
      Play Console'dan doğrula ve takvimini buna göre kur.

> Her yeni yükleme için `versionCode` artırılmalı. Şu an 1.

---

## 5. Gerçek Cihaz Testi

Unity penceresi odakta değilken Play mode kare işlemiyor; zamana bağlı hiçbir şey editörden
doğrulanamaz. Bunlar cihazda test edilmeli:

- [ ] **Metinler görünüyor** (Bölüm 3.1 notu — özellikle Türkçe ve Rusça dilinde)
- [ ] Overheat 2 sn tam gazda tetikleniyor, ceza sonrası normale dönüyor
- [ ] Olaylar 90-180 sn aralıkla kendiliğinden çıkıyor
- [ ] Autosave çalışıyor; uygulamayı kapat-aç → durum korunuyor
- [ ] Offline kazanç doğru hesaplanıyor (cihaz saatini ileri alarak test et)
- [ ] Çentikli bir cihazda üst HUD kesilmiyor
- [ ] Düşük seviye bir cihazda kare hızı kabul edilebilir
- [ ] Ses kapatma gerçekten susturuyor
- [ ] Titreşim rahatsız edici değil
- [ ] Uygulama arka plana alınıp geri gelince durum bozulmuyor
- [ ] **Ödüllü reklam akışı**: test reklamı yükleniyor, izlenince ödül veriliyor,
      yarıda kapatılınca ödül verilmiyor
- [ ] Reklam yüklenemediğinde (uçak modu) oyun kilitlenmiyor

---

## 6. Önerilen Sıra

| Adım | İş | Süre |
|---|---|---|
| 1 | **Keystore'u yedekle** (iki ayrı yere) | 15 dk |
| 2 | **Yeniden build al** (mimari değişti, mevcut AAB geçersiz) | 20-40 dk |
| 3 | Gerçek cihaz testi (Bölüm 5) | 1 gün |
| 4 | Play Console varlıklarını hazırla (Bölüm 4) | 1 gün |
| 5 | Kapalı teste yükle, süreyi bekle | Google'ın şartına göre |
| 6 | Gerçek reklam birimlerine geç, üretime çık | |
