# Play Store Yayın Kontrol Listesi

**Sürüm:** 1.0
**Tarih:** 2026-08-14
**Durum:** Projenin gerçek ayarları taranarak hazırlandı — tahmin değil, ölçüm.

---

## 0. Özet — Kalan Tek Sert Bloker

**Güncelleme (2026-08-14):** Üç blokerdan ikisi kapandı, dördü de kalite sorunu çözüldü.

| # | Bloker | Durum |
|---|---|---|
| 1 | **İmzalama anahtarı (keystore) yok** | ⛔ **AÇIK** — parola gerektirir, senin yapman gerek (Bölüm 1.1) |
| 2 | AAB değil APK üretiliyor | ✅ Kapandı — `buildAppBundle = true` |
| 3 | Target SDK "Auto" | ✅ Kapandı — **35**'e pinlendi |

Doğrulanan çıktı (`Verify Mobile Player Settings`):

```
yönelim   : Portrait          minSdk    : 26
android   : com.speeddownload.megabittycoon
backend   : IL2CPP            targetSdk : 35
mimari    : ARMv7, ARM64      çıktı     : AAB
simge     : logo_icon_appstore
keystore  : YOK (yayın için şart)
```

**Mimari kararı verildi:** ARMv7 + ARM64. Yalnız ARM64 de kabul edilirdi ama eski/ucuz
cihazları kapsam dışı bırakırdı. AAB ile dağıtım yapıldığı için bu ek mimari oyuncunun
**indirme boyutunu büyütmez** — Play Store cihaza yalnızca kendi mimarisini gönderir.

> ⚠ **Target API 35 doğrulanmalı.** Google Play'in minimum target API zorunluluğu her yıl
> yükseliyor. Yüklemeden önce Play Console'dan güncel değeri kontrol et; gerekiyorsa
> `PlayerSettingsTool.TargetSdk` sabitini yükselt ve aracı tekrar çalıştır.

Bölüm 3'teki dört kalite sorunu da çözüldü — ayrıntı için `PLAN.md` Bölüm 29-35.

---

## 1. SERT BLOKERLAR

### 1.1 İmzalama anahtarı yok — EN KRİTİK

```
ProjectSettings.asset:278   AndroidKeystoreName:        (boş)
ProjectSettings.asset:291   androidUseCustomKeystore: 0
```

`PlayerSettingsTool.cs:48` bunu bilinçli olarak `false` bırakmış (geliştirme sırasında
doğru karar). Yayın için değişmeli.

**Yapılacak:**
1. Unity → **Player Settings → Publishing Settings → Keystore Manager → Create New**
2. Keystore ve key alias parolalarını belirle
3. `.keystore` dosyasını **projenin dışına** al, git'e koyma

> ⚠ **Bu dosyayı kaybedersen uygulamayı bir daha asla güncelleyemezsin.** Yeni anahtarla
> imzalanmış build, Play Store tarafından "farklı uygulama" sayılır. En az iki ayrı yerde
> (harici disk + şifreli bulut) yedekle. Parolaları da ayrı sakla.
>
> Google Play App Signing'i etkinleştirirsen Google upload key'i yönetir ve bu risk azalır —
> ilk yüklemede bu seçeneği aç.

### 1.2 AAB üretilmeli, APK değil — ✅ ÇÖZÜLDÜ

`EditorUserBuildSettings.buildAppBundle = true` artık `4. Apply Mobile Player Settings`
tarafından ayarlanıyor ve `Verify` tarafından denetleniyor.

Kökteki eski `SpeedDownload Idle Megabit Tycoon.apk` (31,9 MB) artık geçersiz bir çıktı —
yayın için yeniden build alınmalı. Ek fayda: AAB ile Play Store cihaza özel paket üretir,
oyuncunun indirme boyutu düşer.

### 1.3 Target SDK "Auto" — ✅ ÇÖZÜLDÜ

`PlayerSettingsTool.TargetSdk` sabiti ile **API 35**'e pinlendi
(`ProjectSettings.asset` doğrulandı: `AndroidTargetSdkVersion: 35`).

> Bu değer Google'ın zorunluluğu yükseldikçe elle güncellenmeli. Sabit tek yerde:
> `PlayerSettingsTool.cs` içindeki `TargetSdk`. Değiştirdikten sonra
> `4. Apply Mobile Player Settings` çalıştır.

---

## 2. Mimari Tutarsızlığı — ✅ ÇÖZÜLDÜ

Araç ARMv7+ARM64 ayarlıyordu ama projede yalnızca ARM64 yazılıydı (`AndroidTargetArchitectures: 2`)
— yani araç hiç çalıştırılmamıştı. `4. Apply Mobile Player Settings` çalıştırıldı,
proje ayarı artık **3** (ARMv7 + ARM64) ve araçla tutarlı.

---

## 3. Yayın Kalitesini Doğrudan Etkileyen Sorunlar — ✅ HEPSİ ÇÖZÜLDÜ

| Sorun | Durum |
|---|---|
| Titreşim her tıklamada | ✅ Gerçek kısa haptic + tıklamadan kaldırıldı (`PLAN.md` 29.0) |
| İki ses sistemi çakışıyor | ✅ `ProceduralAudioManager` kaldırıldı (`PLAN.md` 29.0) |
| Isı göstergesi görünmüyor | ✅ Faz 11 (`PLAN.md` 29.1) |
| Gelişim ağacı yetersiz (6 kart) | ✅ **41 karta** çıktı (`PLAN.md` 30, 33-35) |

Ek olarak yayın kalitesini yükselten, plan dışı işler: gelir hızı göstergesi, kadranda
hedef işareti, toplu satın alma, yüzen ödül yazısı, ilk dokunuş ipucu (`PLAN.md` 31),
prosedürel müzik (32), koleksiyon arşivi (35).

Aşağıdaki özgün teşhis kayıt olarak duruyor.

### 3.1 Titreşim her tıklamada — telefon sürekli titriyor

`FXManager.cs:336` her tıklamada `HapticManager.LightTap()` çağırıyor.
`HapticManager.cs:22-59`'da üç seviyenin (`LightTap` / `MediumImpact` / `HeavyImpact`)
**üçü de birebir aynı**: `Handheld.Vibrate()`.

Android'de `Handheld.Vibrate()` ~250 ms sabit süreli, tüm cihazı titreten bir çağrıdır.
Bir clicker oyununda saniyede 5-10 tıklama → titreşimler üst üste biner.

**Sonuç:** pil tüketimi ve "telefonum sürekli titriyor" yorumları.

**Yapılacak:**
- Tıklama başına titreşimi **kaldır** (yalnızca kritik vuruş / overheat / kademe atlamada kalsın)
- Ya da Android `VibrationEffect` API'siyle gerçek kısa haptic (10-20 ms) uygula
- `IsHapticsEnabled` getter'ı her çağrıda `PlayerPrefs.GetInt` okuyor — cache'le

### 3.2 İki ses sistemi aynı anda çalışıyor

Sahnede **iki bağımsız ses yöneticisi** aktif (`Game.unity:5920` ve `:5957`):

| Sistem | Ne yapıyor |
|---|---|
| `Core.AudioManager` | Olaylara abone: tıklama, kritik, overheat, dosya bitişi, satın alma, kademe + ibre uğultusu |
| `Audio.ProceduralAudioManager` | `DialView`, `UpgradeCardView`, `TierUpModalView`, `OfflineEarningsModalView`'den çağrılıyor |

İkisi de aynı olaylar için ses çalıyor → **her tıklamada iki farklı klik sesi**, satın almada
iki ses, kademe atlamada iki ses üst üste biniyor.

**Yapılacak:** Birini seç, diğerini sahneden kaldır. `Core.AudioManager` daha eksiksiz
(ibre uğultusu var, olay tabanlı), ama `ProceduralAudioManager` `SettingsView.SoundEnabled`
kontrolü yapıyor. Tercihen `Core.AudioManager` kalsın, ses kapatma zaten
`AudioListener.volume` üzerinden global çalışıyor (`SettingsView.cs:145`).

### 3.3 Isı göstergesi oyuncuya görünmüyor

`SpeedController.OverheatProgress` hesaplanıyor ama yalnızca `DebugHud.cs:126`'da.
Oyuncu overheat'e ne kadar yakın olduğunu **patlayana kadar göremiyor**.

Oyunun risk/ödül mekaniğinin tamamı bu geri bildirime bağlı. Şu haliyle overheat
"adaletsiz sürpriz" olarak algılanır.

**Yapılacak:** `PLAN.md` Bölüm 28.2 (Faz 11). Sprite'lar: `ASSET_PROMPTS.md` Bölüm 6.1-6.2.

### 3.4 Gelişim ağacı yetersiz

6 gerçek yükseltme var. Oyuncu 3. kademede tüm içeriği görüyor. Idle oyunlarda en sık
gelen olumsuz yorum "içerik bitiyor" — bu haliyle o yorumu alırsın.

**Yapılacak:** `UPGRADE_TREE.md`, `PLAN.md` Bölüm 28.

---

## 4. Doğrulanmış — Bunlar Zaten Doğru

Taramada sorun çıkmayan ayarlar:

| Ayar | Değer | Durum |
|---|---|---|
| Scripting backend | IL2CPP | ✅ 64-bit şartı |
| ARM64 | Açık | ✅ Play Store şartı |
| Paket adı | `com.speeddownload.megabittycoon` | ✅ |
| Min SDK | 26 (Android 8.0) | ✅ Makul kapsam |
| Yönelim | Portre kilitli | ✅ |
| Safe area | `androidRenderOutsideSafeArea: 1` + `SafeAreaFitter` | ✅ Çentikli ekranlar |
| Uygulama simgesi | `logo_icon_appstore.png` 1024² | ✅ Atanmış |
| Sürüm | `1.0` / versionCode `1` | ✅ İlk yayın |
| İzinler | INTERNET, VIBRATE | ✅ Aşırı izin yok |
| Derleme | 0 hata, 0 uyarı | ✅ Konsol temiz |

---

## 5. Play Console Tarafı — Kod Dışı Gereksinimler

Bunlar projede değil, Play Console'da yapılır:

- [ ] **Geliştirici hesabı** (tek seferlik $25)
- [ ] **Kapalı test süreci** — Google, yeni kişisel geliştirici hesapları için üretim
      yayınından önce belirli sayıda test kullanıcısıyla belirli bir süre kapalı test
      şartı koyuyor. Güncel gereksinimi Play Console'dan doğrula; **takvimini buna göre kur**,
      son anda fark edilirse yayını haftalarca geciktirir.
- [ ] **Gizlilik politikası URL'si** — zorunlu. Oyun veri toplamıyor olsa bile bir sayfa gerekiyor.
- [ ] **Data Safety formu** — oyun ağ kullanmıyor; INTERNET izni Unity varsayılanı.
      Reklam SDK'sı eklemeyi planlamıyorsan bu izni kaldırmayı düşün, formu basitleştirir.
- [ ] **İçerik derecelendirme anketi**
- [ ] **Mağaza görselleri:** uygulama simgesi 512×512, feature graphic 1024×500,
      en az 2 telefon ekran görüntüsü (portre)
- [ ] **Mağaza metinleri:** kısa açıklama (80 karakter), tam açıklama (4000)
- [ ] **Hedef kitle ve içerik** beyanı
- [ ] **Reklam beyanı** — `StubAdService` sahte reklam gösteriyor. Gerçek reklam SDK'sı
      yoksa "uygulama reklam içermiyor" işaretle. Sonradan gerçek SDK eklersen beyanı güncelle.

---

## 6. Yayın Öncesi Test

`PLAN.md` Bölüm 27'de belirtildiği gibi, Unity penceresi odakta değilken Play mode kare
işlemiyor — bu yüzden zamana bağlı hiçbir şey MCP üzerinden doğrulanamadı.
**Aşağıdakiler gerçek cihazda test edilmeli:**

- [ ] Overheat 2 sn tam gazda tetikleniyor, ceza sonrası normale dönüyor
- [ ] Olaylar 90-180 sn aralıkla kendiliğinden çıkıyor
- [ ] Autosave çalışıyor; uygulamayı kapat-aç → durum korunuyor
- [ ] Offline kazanç doğru hesaplanıyor (cihaz saatini ileri alarak test et)
- [ ] Çentikli bir cihazda üst HUD kesilmiyor
- [ ] Düşük seviye bir cihazda kare hızı kabul edilebilir
- [ ] Ses kapatma gerçekten susturuyor (Bölüm 3.2 çözüldükten sonra)
- [ ] Titreşim rahatsız edici değil (Bölüm 3.1 çözüldükten sonra)
- [ ] Uygulama arka plana alınıp geri gelince durum bozulmuyor
- [ ] APK/AAB boyutu makul (şu an 31,9 MB APK — AAB ile düşecek)

---

## 7. Önerilen Sıra

| Adım | İş | Süre |
|---|---|---|
| 1 | Bölüm 3'teki 4 kalite sorununu çöz (titreşim, çift ses, ısı göstergesi, ağaç) | Ana iş |
| 2 | Keystore oluştur + **yedekle** | 15 dk |
| 3 | AAB'ye geç, target SDK pinle, mimari kararını ver | 15 dk |
| 4 | `4. Apply Mobile Player Settings` + `Verify` çalıştır | 5 dk |
| 5 | Gerçek cihaz testi (Bölüm 6) | 1 gün |
| 6 | Play Console varlıklarını hazırla (Bölüm 5) | 1 gün |
| 7 | Kapalı teste yükle, süreyi bekle | Google'ın şartına göre |
| 8 | Üretime çık | |

> **Not:** 1. adım en uzun iş ama en değerlisi. Bölüm 3'teki sorunlarla yayınlarsan
> ilk yorumlar "telefonum titriyor", "ses bozuk", "içerik yok" olur — ve ilk yorumlar
> sıralamayı kalıcı etkiler. Kritik eşik dediğin yer tam olarak burası.
