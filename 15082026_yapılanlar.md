# SpeedDownload Idle Megabit Tycoon — 15.08.2026 Çalışma Raporu

Unity 6000.5.0f1 · URP 2D · IL2CPP / Android

Bu oturumda bildirilen 10 sorun çözüldü, ardından proje baştan sona analiz
edilip 5 hata daha bulunup düzeltildi. Derleme temiz, birim testleri geçiyor,
Play Mode duman testleri yapıldı.

---

## İçindekiler

1. [Bildirilen sorunlar](#1-bildirilen-sorunlar)
2. [Analizde bulunan ek hatalar](#2-analizde-bulunan-ek-hatalar)
3. [Ölçümler: öncesi / sonrası](#3-ölçümler-öncesi--sonrası)
4. [Değişen dosyalar](#4-değişen-dosyalar)
5. [Bilinmesi gerekenler](#5-bilinmesi-gerekenler)
6. [Yapılmayanlar / açık kalanlar](#6-yapılmayanlar--açık-kalanlar)

---

## 1. Bildirilen sorunlar

### 1.1 Belge dil hatası — metinler başka dile dönmüyor

**Belirti:** Dil değiştirilince yükseltme adları ve dosya adları İngilizce
kalıyor; `odev_v2.docx` gibi Türkçe belge adları hiç çevrilmiyor.

**Kök neden:** `LocalizationManager` her dili **ayrı bir blokta** tutuyordu.
İngilizce blokta 41 yükseltmenin tamamı vardı ama Almanca/İspanyolca/Rusça
bloklarında yalnızca **ilk 6 yükseltme** kayıtlıydı. `Get()` eksik anahtarı
sessizce İngilizceye düşürdüğü için hata görünmüyordu — sadece "çeviri
yokmuş gibi" davranıyordu.

**Çözüm:** Sözlük, her anahtarı **tek satırda 5 dille birden** kaydeden bir
tabloya çevrildi:

```csharp
Add("upgrade_buy", "BUY", "SATIN AL", "KAUFEN", "COMPRAR", "КУПИТЬ");

Upg("Upg_KuantumIslemci",
    "Quantum Processor", "Processes packets in parallel...",
    "Quantenprozessor",  "Verarbeitet Pakete parallel...",
    "Procesador Cuántico","Procesa paquetes en paralelo...",
    "Квантовый процессор","Обрабатывает пакеты параллельно...");
```

Artık bir dil eksik bırakılamıyor — derleme hatası verir. Kapsam: 41 yükseltme
adı + açıklaması, 8 kademe adı, Türkçe adlı 6 dosya, tüm arayüz metinleri,
olay/prestij/offline/reklam metinleri.

Ayrıca koda gömülü kalmış metinler sözlüğe taşındı:

| Yer | Öncesi | Sonrası |
|---|---|---|
| `UpgradeCardView.GetGainTag` | Sadece TR/EN gömülü | `gain_speed`, `gain_click`, `gain_heat`, `gain_crit`, `gain_reward` |
| `SettingsView` | `"AÇIK"` / `"ON"` gömülü | `settings_on` / `settings_off` |
| `SettingsView` | `"DİL: TÜRKÇE"` gömülü | `settings_language` + `LocalizationManager.NativeName()` |
| Offline raporu butonu | `"TAMAM"` gömülü | `offline_claim` |

### 1.2 Hangi yükseltme açıksa onun dili değişiyor

Ayrı bir hata değil, 1.1'in görünen yüzüydü: DE/ES/RU'da çevirisi olan 6
yükseltme (Hat Bakımı, Modeme Vurmak, Harici Fan, DNS Ayarı, Premium Sunucu,
Download Manager) başlangıç yükseltmeleriydi. Yani "ilk tip" çevriliyor,
gerisi çevrilmiyordu.

Ek olarak `SettingsView`'da eksik olan `LanguageChanged` aboneliği eklendi —
panel açıkken dil değiştirilince kendi metinlerini de tazeliyor.

### 1.3 Titreşim çalışmıyor

**Kök neden (ilk hipotez yanlıştı):** APK'nın manifesti açılıp incelendi,
`android.permission.VIBRATE` **zaten vardı**. Gerçek sebepler:

1. Süreler/genlikler hissedilemeyecek kadar küçüktü: **10 ms / 255'te 60**.
   Telefon titreşim motorunun dönmeye başlaması bile ~20 ms sürüyor.
2. Titreşim yalnızca **kritik vuruş** ve **overheat**'e bağlıydı. Normal
   oynayışta hiç tetiklenmiyordu.
3. Android 12+ bazı ROM'larda `getSystemService("vibrator")` boş bir sarmalayıcı
   döndürüyor; doğru yol `VibratorManager`.

**Çözüm — `HapticManager` yeniden yazıldı:**

- Android 10+ (API 29) cihazlarda üreticinin **kalibre ettiği** efektler
  kullanılıyor: `EFFECT_TICK` / `EFFECT_CLICK` / `EFFECT_HEAVY_CLICK`.
  Ham süre/genlik tahmininden her zaman daha iyi hissettiriyor.
- API 31+ için `VibratorManager.getDefaultVibrator()` yolu eklendi.
- `hasVibrator()` kontrolü eklendi — motoru olmayan cihazda hiç uğraşmıyor.
- Yedek değerler hissedilir seviyeye çekildi: 18/25/40/65 ms, 110–255 genlik.
- Titreşim noktaları genişletildi:

| Olay | Şiddet |
|---|---|
| Kadrana dokunma | `ClickTick` (kendi bekleme süresi: sn'de en fazla ~8) |
| Satın alma | `MediumImpact` |
| Kritik vuruş | `HeavyImpact` |
| Overheat | `HeavyImpact` |
| Kademe atlama | `HeavyImpact` |
| Ayar düğmeleri | `LightTap` |

Tıklama titreşimi bilinçli olarak **ayrı** bir bekleme süresi taşıyor
(`MinClickInterval = 0.12s`), böylece hızlı tıklamada motor kesintisiz
çalışmıyor ve pil yanmıyor.

### 1.4 Çok CPU/GPU tüketiyor, telefonu ısıtıyor

İki yapısal sorun bulundu.

**(a) `DialView` her karede kadran kökünün `localScale`'ini yazıyordu.**

```csharp
// ÖNCESİ — her karede, koşulsuz
_punchScale = Mathf.Lerp(_punchScale, 1f, Time.deltaTime * 18f);
transform.localScale = Vector3.one * _punchScale;
```

Bu bir UI kökü. uGUI'da bir transformu oynatmak, bağlı olduğu Canvas'ın
**tüm geometrisini** yeniden ördürür. Üstelik `Lerp` hedefe asla tam
ulaşmadığı için bu durum **hiç bitmiyordu** — oyun bomboş dururken bile
saniyede 60 kez tüm arayüz yeniden inşa ediliyordu.

**(b) Tüm arayüz tek bir Canvas'taydı.**

İlerleme çubuğu her karede doluyor, üst HUD saniyede 12 kez metin yazıyor,
ibre dönüyor, her dosya bitiminde tüm yükseltme kartları tazeleniyordu — ve
bunların her biri diğerlerinin tamamını yeniden ördürüyordu.

**Çözüm:**

Arayüz 8 alt Canvas'a bölündü. Artık kadran dönerken kartlar, kartlar
tazelenirken kadran yeniden örülmüyor:

| Katman | Alt Canvas | Raycaster | Neden |
|---|---|---|---|
| `TopPanel` | ✔ | — | sn'de 12 metin + her kare ilerleme çubuğu |
| `DialArea` | ✔ | — | ibre sürekli dönüyor |
| `BottomPanel` | ✔ | ✔ | her bakiye değişiminde kartlar |
| `FXLayer` | ✔ | — | efektler her karede ölçek/konum/alfa |
| `Mascot` | ✔ | ✔ | boşta bile salınıyor |
| `EventBanner` | ✔ | ✔ | kayarak giriyor, geri sayıyor |
| `OnboardingHint` | ✔ | — | nabız gibi atıyor |
| `FloatingEventLayer` | ✔ | ✔ | balon ekranı geçiyor |

Asimptotik yakınsayan (hedefe hiç oturmayan) tüm yazımlar kelepçelendi:

- `DialView` — ibre açısı, punch ölçeği, hedef bölge işareti
- `HeatGaugeView` — ısı dolgusu
- `MascotController` — salınım (piksel altı hareket atlanıyor) ve zıplama ölçeği
- `AudioManager` — uğultu pitch/volume (0.01 eşiği)

**Gizli DebugHud:** panel alfa 0 olduğu için görünmüyordu ama `Update`'i
çalışmaya devam ediyor, **saniyede 10 kez ~500 karakterlik bir metin üretip
TMP'ye yazıyordu**. TMP her `SetText`'te mesh'i yeniden örer ve Canvas'ı
kirletir. Yayın sürümünde tamamen kapatıldı, editörde de yalnızca panel
görünürken çalışıyor.

**Kart tazeleme:** `Wallet.BalanceChanged` her dosya tamamlanmasında tetikleniyor
(yüksek hızda saniyede onlarca). Her seferinde tüm kartların 4'er metnini
yeniden yazmak yerine istekler biriktirilip **6 Hz**'de işleniyor.

**URP ayarları** (mobil GPU bant genişliği + gölgeleyici varyantı):

| Ayar | Öncesi | Sonrası |
|---|---|---|
| `m_SupportsHDR` | 1 | 0 |
| `m_MainLightShadowsSupported` | 1 | 0 |
| `m_AnyShadowsSupported` | 1 | 0 |
| `m_MixedLightingSupported` | 1 | 0 |
| `m_SupportsLightCookies` | 1 | 0 |
| `m_SupportsTerrainHoles` | 1 | 0 |
| `m_EnableLODCrossFade` | 1 | 0 |
| `m_SupportDataDrivenLensFlare` | 1 | 0 |
| `m_SupportScreenSpaceLensFlare` | 1 | 0 |
| gölge/cookie atlas çözünürlükleri | 2048 | 256 |

**Kare hızı:** 60 FPS **korundu** (talep edildiği gibi). Eklenen tek şey: 60
saniye boyunca ekrana **hiç** dokunulmazsa 30'a iniyor, ilk dokunuşta anında
geri çıkıyor. Ayrıca `Screen.sleepTimeout = NeverSleep` koşulsuz uygulanıyordu;
artık yalnızca son 2 dakika içinde dokunulduysa ekran açık tutuluyor. Ekranın
kendisi bir telefonu ısıtan en büyük kalem.

### 1.5 APK çıktısı çok fazla

Mevcut APK açılıp ölçüldü. **Açılmış toplam 123,3 MB** — bildirilen "yüklenince
120 MB" ile birebir örtüşüyor:

| Bileşen | Boyut |
|---|---|
| `lib/arm64-v8a` | 56,4 MB |
| `lib/armeabi-v7a` | 43,1 MB ← ikinci kopya |
| `assets/bin` | 16,7 MB |
| `classes.dex` | 6,4 MB |
| diğer | 0,7 MB |

Yani paketin üçte birinden fazlası, 2019 öncesi 32-bit cihazlar için taşınan
ikinci bir native kopyaydı.

**Uygulananlar:**

| Değişiklik | Kazanç |
|---|---|
| `AndroidTargetArchitectures: 3 → 2` (yalnız ARM64) | −43,1 MB |
| IL2CPP `OptimizeSpeed → OptimizeSize` | `libil2cpp.so` 33 MB'tan belirgin düşüş |
| `AndroidMinifyRelease: 0 → 1` (R8 kapalıydı) | `classes.dex` 6,4 MB'tan düşüş |
| 24 kullanılmayan paket kaldırıldı | `libunity.so` küçülür |
| `stripEngineCode` + `ManagedStrippingLevel.High` doğrulandı | — |

**Kaldırılan paketler:** visualscripting, timeline, multiplayer.center,
2d.animation, 2d.aseprite, 2d.psdimporter, 2d.spriteshape, 2d.tilemap,
2d.tilemap.extras, 2d.tooling, modules.ai, modules.assetbundle,
modules.director, modules.imageconversion, modules.particlesystem,
modules.physics2d, modules.physicscore2d, modules.screencapture,
modules.tilemap, modules.unityanalytics, modules.unitywebrequestassetbundle,
modules.unitywebrequestaudio, modules.unitywebrequesttexture,
modules.vectorgraphics.

**Korunanlar (gerekli):** androidjni (HapticManager), jsonserialize
(SaveManager), audio, ui, uielements, imgui, animation, accessibility,
adaptiveperformance, inputsystem, URP, ugui.

> Doku sıkıştırması zaten doğruydu — `BuildSizeOptimizerTool` sprite'lara ASTC
> uygulamıştı, ek bir şey yapılmadı.

### 1.6 Offline kazanç sistemi yok

**Kök neden:** Sistem koddaydı ama `SaveManager.cs`'te şu satır vardı:

```csharp
// Download Manager alinmadan offline kazanc yok (GDD 6.2).
if (result.managerLevel <= 0) return result;
```

Yükseltme alınmadan kazanç sıfır; kazanç sıfır olunca rapor penceresi de
hiç açılmıyordu (`if (!result.HasEarnings) return;`). Sonuç: oyuncu sistemin
**varlığından bile haberdar olmuyordu**.

**Çözüm — üç kademeli yapı:**

| Kademe | Verim | Tavan |
|---|---|---|
| Temel (yükseltmesiz) | %25 | 2 saat |
| Download Manager sv1 | %50 | 8 saat |
| Download Manager sv2 (Pro) | %100 | 24 saat |

`GameConfigSO`'ya `offlineEfficiencyBase` ve `offlineCapHoursBase` eklendi.
Rapor penceresi artık hangi kademede olduğunu **ve** bir sonrakinin ne
vereceğini yazıyor (`offline_basic_hint`, `offline_dm_hint`,
`offline_pro_hint`, `offline_pro_active` — beş dilde).

### 1.7 Sol üstte küçük, işlevi anlaşılmayan kutu

Kademe rozetiydi: 50 px'lik bir ikon, kademe 0'da sprite'ı bile yok
(`GetConnectionUpgrade(0)` → null).

**Çözüm:**

- Rozet 50 → **74 px** büyütüldü (`TierRowHeight` 58 → 74).
- Arkasına `ui_node_frame.png` çerçevesi kondu — bu sprite üretilmiş ama
  hiçbir yerde kullanılmıyordu.
- Yanına **"KADEME 3 · Tam Fiber"** yazısı geldi (`tier_badge` anahtarı,
  5 dilde).
- Kademe 0 için yedek simge tanımlandı; sprite'sız `Image` beyaz kare çizdiği
  için rozet artık hiçbir kademede boş görünmüyor.

### 1.8 Kullanılmayan / oyuna işlenmeyen assetler

118 sprite tarandı, 7'si referanssızdı. Bağlananlar:

| Asset | Nereye bağlandı |
|---|---|
| `Vibration On.png` / `Vibration Of.png` | Ayarlar → titreşim düğmesi |
| `ui_node_frame.png` | Kademe rozeti çerçevesi |
| `bg_loading.png` | Açılış (splash) ekranı arka planı |
| `logo_main.png` | Açılış ekranı logosu |
| `fx_critical_burst.png` | Yüzen ödül balonu simgesi (yeni) |

Ölü kod temizliği:

- `OfflineEarningsModalView.cs` **silindi** — `OfflineReportView` zaten
  sahnede bağlıydı, bu ikinci bir kopyaydı ve hiç kullanılmıyordu.
- `TierUpModalView.cs` yazılmıştı ama **sahnede hiç oluşturulmuyordu**.
  Artık `CreateTierUpModal()` ile sahneye giriyor: yeni kademenin adını,
  simgesini ve hız aralığını gösteren kutlama penceresi. Kayıttan yüklerken
  bastırılıyor (yoksa her açılışta "yeni çağa geçtin" derdi).

### 1.9 Ekonomi mantığı yanlış hissettiriyor

İki ayrı hata bulundu.

#### (a) Tıklama gücü ölçek hatası — kritik

Tıklama **mutlak bit/sn** ekliyordu ve kademe indeksine göre **logaritmik**
ölçekleniyordu:

```csharp
double tierScale = Math.Max(1.0, Math.Log10(currentTier.minSpeed));
double basePower = _config.baseClickPower * tierScale;   // 8, 16, 24 ... 96
```

Ama kademe hızları **üstel** büyüyor. Kademe 8'de kadran **1e12 – 1e14**
arasında; oraya tıklama başına ~96 bit eklemek ibreyi kımıldatmıyordu.

Yani oyunun **merkez mekaniği kademe 3-4'ten sonra ölü bir düğmeye**
dönüşüyordu. Oyun kendiliğinden akan bir ekrana düşüyordu.

**Çözüm:** Tıklama gücü artık kademenin **taban hızına oran**:

```csharp
// SpeedController
public double ClickPowerRatio { get; set; } = 0.3;
public double ClickPower => BaseSpeed * ClickPowerRatio;

// EconomyManager
_speed.ClickPowerRatio = _config.clickPowerRatio
                       * (1.0 + TotalEffect(UpgradeType.ClickPower))
                       * PrestigeMultiplier;
```

Böylece tıklamanın kadran üzerindeki etkisi **her kademede aynı** hissediliyor.
Play Mode'da doğrulandı: kademe 8'de 20 tıklama ibreyi t=0,324 → t=0,449'a
çıkarıyor (öncesinde t hiç değişmiyordu).

Kart etiketi de düzeltildi: `(+0,15 bps/tık)` yerine `(+%15 Tık)`.

#### (b) Kademe geçişleri — asıl "yanlış hissettiren" kısım

Canlı asset'ler ölçüldü. Her bağlantı yükseltmesi önceki kademenin
**~5 dakikalık gelirine** mal oluyor (sabit ve iyi ayarlanmış). Ama
karşılığında verdiği:

| Geçiş | Eski | Yeni |
|---|---|---|
| T0→T1 | ×4,0 | ×9,2 |
| T1→T2 | ×15,0 | ×10,3 |
| **T2→T3 (ISDN)** | **×3,7** | **×11,4** |
| T3→T4 | ×6,4 | ×12,9 |
| **T4→T5 (VDSL)** | **×42,9** | **×14,3** |
| **T5→T6 (Fiber)** | **×50,0** | **×16,0** |
| T6→T7 | ×30,0 | ×18,0 |
| T7→T8 | ×13,3 | ×20,6 |

Aynı fiyat, bambaşka sonuçlar. ISDN için 5 dakika biriktirip gelirini üçte
bir artırıyordun; aynı 5 dakikayı VDSL'e verdiğinde kırk katlıyordun. Emek
verilmiş kademe görselleri saniyeler içinde geçiliyor, ISDN ise duvar gibi
duruyordu.

**Çözüm:** Ödül tabanı düzgün ve hafifçe hızlanan bir eğriye oturtuldu. İlk ve
son değerler korundu (başlangıç ve son oyun maliyetleri yerinde kalsın diye),
ve **her yükseltmenin "kaç saniyelik gelire mal olduğu" korunacak şekilde**
tüm maliyetler birlikte ölçeklendi:

| Kademe | Eski ödül tabanı | Yeni | Maliyet çarpanı |
|---|---|---|---|
| T0 | 0,5 | 0,5 | ×1,00 |
| T1 | 2 | 4,6 | ×2,30 |
| T2 | 30 | 47 | ×1,57 |
| T3 | 110 | 540 | ×4,91 |
| T4 | 700 | 7 000 | ×10,00 |
| T5 | 30 000 | 100 000 | ×3,33 |
| T6 | 1,5e6 | 1,6e6 | ×1,07 |
| T7 | 4,5e7 | 2,9e7 | ×0,64 |
| T8 | 6,0e8 | 6,0e8 | ×1,00 |

Yani **tempo aynı kaldı, sadece kademeler arası sıçramalar düzleşti.**
Doğrulama sonrası bağlantı maliyetleri 211–510 saniyelik gelir bandında,
donanım/yazılım yükseltmeleri 96–605 saniye bandında.

**Ayrıca:** `DataAssetGenerator`'daki `ConnectionCostFactor` **150**'ydi ama
sahadaki asset'ler **24**'e ayarlanmıştı (elle düzeltilmiş). "Regenerate"
çalıştırılsaydı oyun sessizce **sekiz kat yavaşlayacaktı**. Katsayı ölçülen
gerçek değere çekildi.

---

## 2. Analizde bulunan ek hatalar

Bildirilen sorunlar bittikten sonra tüm runtime/editor scriptleri, veri
assetleri ve ayarlar baştan sona gözden geçirildi. Beş gerçek hata çıktı.

### 2.1 Prestij yetenekleri PARAYLA satın alınabiliyordu — istismar

`UpgradeCardView.BulkMode` **static** ve sekmeler arası paylaşılıyor.
`EconomyManager.TryBuyBulk` ise doğrudan `_wallet.TrySpend(cost)` çağırıyordu.

**İstismar yolu:** Yükseltmeler sekmesinde MAKS seç → prestij sekmesine geç →
yeteneğe tıkla. Yetenek Fiber Kredisi yerine **parayla** alınıyordu. Yetenek
maliyetleri 3-6 FK olduğu için bir miktar parası olan herkes tüm kalıcı
yetenekleri bedavaya alabiliyordu.

**Çözüm:** `GetMaxAffordable` artık prestij yeteneklerinde bütçeyi Fiber
Kredisi'nden okuyor; `TryBuyBulk` ise bu yükseltmeleri para birimini doğru
seçen tekil satın alma yoluna yönlendiriyor.

Doğrulandı: 1e12 para ile 10 adet toplu alım denendi → **0 alındı, 0 para
harcandı**.

### 2.2 Arşiv, sonsuz kademe dosyalarını sayıyordu — sınırsız bonus

Kademe 9+ mega arşivleri çalışma anında üretiliyor ve her sonsuz kademe
**yeni bir ad** alıyor (`File_Infinite9`, `File_Infinite10`, …).
`CollectionManager` bunları da arşive ekliyordu:

1. Arşiv bonusu (her 3 dosyada +%2 taban hız) **sınırsız büyüyordu**.
2. Panel yalnızca sabit dosyaları çizdiği için sayaç **"26/23"** gibi imkânsız
   değerler gösterebiliyordu.
3. Kayıt dosyası sınırsız şişiyordu.

**Çözüm:** `IsArchivable()` kontrolü eklendi — arşive yalnızca veri tabanındaki
elle tasarlanmış dosyalar giriyor. Eski kayıtlar yüklenirken de ayıklanıyor.

Doğrulandı: kademe 9'da 200 tıklama sonrası arşiv sayacı **0 → 0**.

### 2.3 Kart bonusları "%100"de kilitleniyordu

`UpgradeCardView`, birikmiş bonusu göstermek için
`NumberFormatter.FormatPercent()` kullanıyordu — ama o fonksiyon ilerleme
çubuğu için yazılmış ve **0-100 arasına kırpıyor**.

Sonuç: 25 seviye Modeme Vurmak gerçekte **%1500** ederken kart **"+%100"**
yazıyordu. Oyuncu satın aldığı şeylerin biriktiğini göremiyordu — doğrudan
"ekonomi yanlış hissettiriyor" şikâyetini besleyen bir hata.

**Çözüm:** Kelepçesiz `FormatBonusPercent()` eklendi (100.000 üstünde
kısaltmaya geçiyor: "+%1.5M") ve kartlarda o kullanılıyor.

Doğrulandı: `FormatPercent(15.0) = 100%` · `FormatBonusPercent(15.0) = 1500%`.

### 2.4 "Sinyal Yakalandı" balonu tamamen ölüydü

`FloatingEventController` sahnedeydi ve `Update`'i çalışıyordu ama **üç ayrı
kopukluk** vardı:

1. `canvasRect` hiçbir zaman atanmıyordu. Bileşen `[Game]` nesnesinde duruyor,
   o da Canvas'ın **altında değil** — dolayısıyla
   `GetComponentInParent<Canvas>()` null dönüyor ve `SpawnEvent()` her
   seferinde ilk satırda geri dönüyordu. **Balon hiç çıkmadı.**
2. Simge `Resources.Load<Sprite>("Sprites/UI/ui_icon_wifi")` ile aranıyordu ama
   projede o yolda bir `Resources` klasörü yok. Balon çıkabilseydi bile beyaz
   bir kare olurdu.
3. Ödül olarak verilen `ActiveMultiplier`'ı **hiçbir sistem okumuyordu**. Yani
   "5x hız" ödülü hiçbir şey yapmıyordu.

**Çözüm:** Üç kopukluk da kapatıldı.

- SceneBuilder `FloatingEventLayer` (kendi Canvas + GraphicRaycaster) oluşturup
  bağlıyor. FX katmanına konamazdı: orada `blocksRaycasts` kapalı, balona
  dokunulamazdı.
- Simge `fx_critical_burst.png`; sprite yoksa `Image` devre dışı bırakılıyor
  (beyaz kare çizmesin).
- `DownloadController.BoostMultiplier` eklendi ve boost gerçekten uygulanıyor.
  `SpeedMultiplier`'dan **ayrı** tutuluyor: onun sahibi `GameEventManager` ve
  olay bitince 1.0'a geri yazıyor; aynı alan paylaşılsaydı biten bir olay,
  devam eden bir boost'u sessizce silerdi.
- Nakit ödülü artık ham dosya ödülüne değil oyuncunun gerçek gelir hızına
  bağlı (45 saniyelik boşta gelir).

### 2.5 Maskot her karede ana Canvas'ı kirletiyordu

`MascotController.Update` her karede hem `anchoredPosition` hem `localScale`
yazıyordu; salınım sinüsü hiç durmuyor ve zıplama `Lerp`'i hedefe hiç
oturmuyordu. 1.4(a)'daki `DialView` hatasının aynısı.

**Çözüm:** Piksel altı hareket atlanıyor (0,25 px eşik), zıplama ölçeği 1'e
snap'leniyor ve maskot kendi alt Canvas'ına alındı.

**Bonus:** `EventBannerView` Gece Tarifesi sırasında **her karede** `Refresh()`
çağırıyordu, oysa ekranda yalnızca tam saniye görünüyor. Artık yalnızca
gösterilen saniye değişince tazeleniyor (saniyede 60 → 1).

---

## 3. Ölçümler: öncesi / sonrası

### Paket boyutu

| | Öncesi | Sonrası (beklenen) |
|---|---|---|
| APK (sıkıştırılmış) | 49,5 MB | ~25 MB |
| Cihazda kurulu | ~123 MB | ~50-55 MB |
| native kütüphaneler | 99,5 MB (2 mimari) | 56,4 MB → daha az (1 mimari + OptimizeSize) |
| `classes.dex` | 6,4 MB | R8 ile küçülür |

### Kademe geliri (referans hızda, $/sn)

| Kademe | Öncesi | Sonrası | Öncekine oran |
|---|---|---|---|
| 0 | 0,0417 | 0,0417 | — |
| 1 | 0,167 | 0,383 | ×9,2 |
| 2 | 2,50 | 3,96 | ×10,3 |
| 3 | 9,17 | 45,0 | ×11,4 |
| 4 | 58,3 | 582 | ×12,9 |
| 5 | 2 500 | 8 333 | ×14,3 |
| 6 | 125 000 | 133 300 | ×16,0 |
| 7 | 3,75e6 | 2,41e6 | ×18,0 |
| 8 | 5,00e7 | 4,96e7 | ×20,6 |

### Tıklamanın kadran üzerindeki etkisi (kademe 8, 20 tıklama)

| | Öncesi | Sonrası |
|---|---|---|
| Normalize konum `t` | değişmiyor | 0,324 → 0,449 |
| Tıklama gücü | ~96 bit/sn | 345 Gbit/sn (taban hızın oranı) |

### Her karede Canvas kirleten yerler

| Kaynak | Öncesi | Sonrası |
|---|---|---|
| `DialView` punch ölçeği | her kare, sonsuza kadar | yalnızca tıklamadan sonra, oturana kadar |
| `DialView` ibre açısı | her kare | yalnızca açı değişince |
| `DialView` hedef işareti | her kare | bir kez |
| `HeatGaugeView` dolgu | her kare | yalnızca ısı değişince |
| `MascotController` | her kare (konum + ölçek) | piksel altı hareket atlanıyor |
| `DebugHud` (görünmez) | sn'de 10 kez ~500 karakter TMP | yayında hiç |
| `EventBannerView` (Gece Tarifesi) | sn'de 60 kez | sn'de 1 kez |
| Yükseltme kartları | her dosya tamamlanmasında | 6 Hz'e toplanmış |
| Canvas sayısı | 1 (her şey birbirini kirletiyor) | 9 (izole) |

---

## 4. Değişen dosyalar

### Runtime — `Assets/Scripts/Core/`

| Dosya | Değişiklik |
|---|---|
| `LocalizationManager.cs` | Tamamen yeniden yazıldı — 5 dilli tablo yapısı |
| `HapticManager.cs` | Tamamen yeniden yazıldı — kalibre efektler, VibratorManager |
| `GameBootstrapper.cs` | Tamamen yeniden yazıldı — boşta kare hızı, ekran uyanıklığı |
| `FloatingEventController.cs` | Tamamen yeniden yazıldı — 3 kopukluk kapatıldı |
| `SpeedController.cs` | `ClickPowerRatio` eklendi, `ClickPower` türetilmiş oldu |
| `EconomyManager.cs` | Tıklama oranı; Fiber Kredisi istismarı kapatıldı |
| `DownloadController.cs` | `BoostMultiplier` eklendi |
| `SaveManager.cs` | Üç kademeli offline kazanç |
| `SaveData.cs` | `OfflineResult`'a `efficiency` / `capHours` |
| `CollectionManager.cs` | `IsArchivable()` — üretilmiş dosyalar arşive girmiyor |
| `AudioManager.cs` | Uğultu pitch/volume eşikli yazım |

### Runtime — `Assets/Scripts/UI/`

| Dosya | Değişiklik |
|---|---|
| `DialView.cs` | Punch/ibre/işaret kelepçelendi |
| `HeatGaugeView.cs` | Dolgu snap'lendi |
| `MascotController.cs` | Salınım ve ölçek kelepçelendi |
| `DebugHud.cs` | Yayında devre dışı, gizliyken metin üretmiyor |
| `UpgradeListView.cs` | Kart tazeleme 6 Hz'e toplandı |
| `UpgradeCardView.cs` | Etiketler sözlükten; `FormatBonusPercent` |
| `TopHudView.cs` | Kademe rozeti metni + çerçeve/yedek simge bağı |
| `SettingsView.cs` | `LanguageChanged` aboneliği, ON/OFF sözlükten |
| `OfflineReportView.cs` | Üç kademeli durum satırı |
| `TierUpModalView.cs` | Simge desteği, kayıt bastırması |
| `EventBannerView.cs` | Geri sayım saniyede 1 kez |
| `FXManager.cs` | Tıklama/satın alma/kademe titreşimleri |
| `OfflineEarningsModalView.cs` | **Silindi** (ölü kod) |

### Editor — `Assets/Scripts/Editor/`

| Dosya | Değişiklik |
|---|---|
| `SceneBuilder.cs` | `MakeSubCanvas()`, kademe rozeti, `CreateTierUpModal()`, `CreateFloatingEventLayer()`, titreşim ikonları |
| `PlayerSettingsTool.cs` | ARM64, OptimizeSize, R8, splash görselleri |
| `DataAssetGenerator.cs` | Yeni ödül eğrisi, `ConnectionCostFactor` 150→24, maliyetler |

### Veri / ayarlar

| Dosya | Değişiklik |
|---|---|
| `Assets/ScriptableObjects/GameConfig.asset` | `clickPowerRatio`, offline taban değerleri |
| `Assets/ScriptableObjects/Files/*.asset` | 20 dosyanın `baseReward`'ı |
| `Assets/ScriptableObjects/Upgrades/*.asset` | 32 yükseltmenin `baseCost`'u |
| `Assets/Settings/UniversalRP.asset` | 12 mobil optimizasyon |
| `ProjectSettings/ProjectSettings.asset` | Mimari, R8, IL2CPP codegen |
| `Packages/manifest.json` | 24 paket kaldırıldı |
| `Assets/Scenes/Game.unity` | Yeniden üretildi |

---

## 5. Bilinmesi gerekenler

- **Kayıt dosyasını sil.** Maliyetler ve ödüller değişti; eski kayıtla test
  etmek yanıltır. `Tools/SpeedDownload/Save/Delete Save File`.

- **Ekonomi doğrudan asset'lere uygulandı**, `Regenerate (OVERWRITE)`
  çalıştırılmadı — elle yapılmış diğer Inspector ayarları duruyor. Generator
  da aynı sayıları üretecek şekilde güncellendi, yani ileride "Regenerate"
  çalıştırırsan sonuç tutarlı olur.

- **Çıktı AAB'ye ayarlı** (`EditorUserBuildSettings.buildAppBundle = true`).
  Play Store için doğru. Test APK'sı alacaksan Build Settings'ten
  "Build App Bundle"ı kapat.

- **ARMv7 kapalı.** Play'e AAB ile çıkarken geri açmak istersen
  `PlayerSettingsTool.cs`'te tek satır: AAB cihaza yalnızca kendi mimarisini
  gönderdiği için indirme boyutunu büyütmez.

- **Yedekler** oturum scratchpad'inde: `Game.unity.bak`,
  `ScriptableObjects.bak/`, `manifest.json.bak`.

- **Font desteği doğrulandı.** `LiberationSans SDF` statik atlas kullanıyor
  ama dinamik bir fallback'e (`LiberationSans SDF - Fallback`,
  `m_AtlasPopulationMode: 1`) bağlı. Kiril, Türkçe ve İspanyolca karakterler
  çalışma anında üretiliyor — Rusça metinler Play Mode'da doğrulandı.

---

## 6. Yapılmayanlar / açık kalanlar

**Hâlâ kullanılmayan 3 asset** — karşılık gelen özellik olmadığı için:

| Asset | Sebep |
|---|---|
| `icon_upgrade_antivirus.png` | Antivirüs yükseltmesi tanımlı değil. `UpgradeType.VirusImmunity` kodda var ama kart yok ve virüs olayı da yok. |
| `ui_card_file_choice.png` | Dosya seçme ekranı yok (dosyalar otomatik seçiliyor). |
| `bg_mainmenu.png` | Ana menü tasarım gereği yok (oyun doğrudan başlıyor). |

**Ölçülmemiş:** APK boyutu tahmini, ölçülen bileşen boyutlarından hesaplandı;
gerçek rakam için yeni bir build almak gerekiyor.

**Denenmemiş:** Yeni ekonomi eğrisi matematiksel olarak doğrulandı ama gerçek
cihazda uçtan uca oynanmadı. Kademe geçiş hissini teyit etmek için bir oturum
oynamak faydalı olur.

**Sonsuz kademeler (9+):** T8→T9 ve sonrası ×100 adımlarla ilerliyor
(`infiniteTierSpeedFactor`). Sabit kademelerin yeni eğrisi ×20,6'da bitiyor,
yani orada bir sıçrama var. Tasarım gereği mi yoksa düzeltilmeli mi, ayrı bir
karar.

---

*Rapor tarihi: 15.08.2026*
