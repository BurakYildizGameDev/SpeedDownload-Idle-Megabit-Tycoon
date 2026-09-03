# SpeedDownload Idle / Megabit Tycoon — Uygulama Planı

**Sürüm:** 2.0 — sprite'lar ölçüldü, tüm kararlar kapatıldı
**Güncelleme:** 2026-08-03
**Kaynak:** `Docs/GDD.md` (v2.3), `Docs/Asset_Integration_Guide.md` (v2.2)
**İlke:** Bu plan **yalnızca elimizde olan 82 sprite + kod** ile uygulanabilir. Yeni görsel, ses veya font varlığına bağımlılık **yoktur**.

---

## 0. Projenin Mevcut Durumu

| Öğe | Durum |
|---|---|
| Unity | **6000.5.0f1** |
| Pipeline | URP 17.5.0, **2D Renderer** |
| Paketler | Input System 1.19.0 · uGUI 2.5.0 · TextMesh Pro · 2D Sprite |
| Sprite | **82/82** — `Assets/Sprites/`, klasör isimleri rehberle birebir |
| Script | **Yok** |
| Sahne | Sadece `SampleScene.unity` (boş şablon) |
| Import ayarı | **Uygulanmamış** (maxSize 2048, pivot Center, ASTC override yok) |

---

## 1. Ölçüm Sonuçları — Planın Dayandığı Sert Veriler

Sprite'ların alfa kanalı piksel piksel tarandı. Aşağıdaki değerler tahmin değil, **ölçüm**. (Yöntem: `System.Drawing` + `LockBits`, alfa eşiği 128.)

### 1.1 Kadran geometrisi

| Ölçüm | Değer | Sonuç |
|---|---|---|
| `dial_redline_overlay` açısal kaplama | **0° … +89°** (saat 12 → saat 3) | Redline yayı tam olarak sağ-üst çeyrek |
| `dial_redline_overlay` yarıçap bandı | 0,730 … 0,854 (normalize) | İbre ucu bu banda değmeli |
| `dial_tick_marks` | **360° tam halka**, r 0,716 … 0,880 | Ölçek başlangıç/bitişi hakkında bilgi vermiyor → sweep'i biz tanımlıyoruz |
| `dial_needle` opak oran | **%3,5** | İbre **içi boş kontur** — dolu değil |
| `dial_needle` opak bbox | x 14…496, y 2…83 | Pivot x=51,2 (0,1×512) tabandaki dairenin merkezinde ✔ |
| Pivot → uç mesafesi | **444,8 px** | Ölçekleme hesabı için taban |

### 1.2 UI 9-slice (ölçülmüş border değerleri)

| Sprite | Boyut | Opak bbox | Opak % | Border (L,B,R,T) |
|---|---|---|---|---|
| `ui_button_normal/_pressed/_disabled` | 512×237 | x15–496, y7–229 | 82,5 | **70, 60, 70, 60** |
| `ui_panel_bg` | 512×308 | x14–497, y9–272 | 80,5 | **30, 45, 30, 30** (alt 35 px gölge) |
| `ui_progressbar_frame` | 512×167 | x14–497, y5–161 | **21,5** | **75, 20, 75, 20** |
| `ui_progressbar_fill` | 512×128 | x15–496, y4–123 | 83,5 | **55, 10, 55, 10** |
| `event_happyhour_banner` | 512×115 | x15–496, y4–111 | 65,3 | 9-slice **yok** (Single) |

`ui_progressbar_frame` **gerçekten içi boş** (%21,5 opak). İç boşluk: **x 21…490, y 11…150**.
→ Fill objesi frame'in **içine**, `left=21, right=22, top=11, bottom=16` px offset ile yerleşir.

### 1.3 Kadran yüzü parlaklığı

| Kademe | Yüz rengi | İbre kontrastı |
|---|---|---|
| 0, 1 | Krem / beyaz (245,241,211) | Navy kontur **okunur** |
| 2, 3, 4, 5 | Orta ton | Sınırda |
| 6, 7, 8 | **Koyu lacivert / siyah**, neon cyan tikler | Navy kontur **görünmez** ❗ |

Bu, aşağıdaki 2.2'deki ibre çözümünü zorunlu kılıyor.

---

## 2. Elimizdekiyle Kapatılan 7 Karar

Önceki plan sürümünde "sana soracağım" diye bırakılan her madde burada **kesin bir çözüme** bağlandı. Hiçbiri yeni varlık gerektirmiyor.

### 2.1 Kademe 3 ve 5'in oda arka planı yok
**Çözüm:** Komşu kademenin odası + `ConnectionTierSO.roomTint` ile hafif renk kayması.
Kademe 3 → `bg_tier1_2_retro_room` soğuk mavi tint (ISDN "çift hat" hissi).
Kademe 5 → `bg_tier4_modern_room` açık cyan tint (fiber'e geçiş).
Oda zaten arka planda ve kadran ön planda olduğu için tekrar fark edilmez.

### 2.2 İbre içi boş kontur + koyu kadranlarda görünmez ❗
**Çözüm:** `SpeedDownload/SpriteSolidTint` shader'ı (≈15 satır, kod — varlık değil).
Sprite'ın **RGB'sini yok sayar, yalnızca alfasını maske olarak kullanır** ve `_Color` ile boyar.
Böylece ibre her kademede istenen renkte, parlak ve dolu görünür.
`ConnectionTierSO.needleColor` alanı: açık kadranlarda koyu kırmızı, koyu kadranlarda neon turuncu/cyan.
Ek bonus: aynı shader `fx_*` efektlerinde renk varyasyonu için de kullanılır.

### 2.3 İbre ölçeği belirsizdi
**Çözüm (hesaplandı):** Uç, redline bandının ortasına (r ≈ 0,80) değmeli.
`ölçek = (0,80 × 256) / 444,8 = **0,461**`
Kadran 512 birimse → `NeedleSprite` RectTransform = **236 × 40**.

### 2.4 Redline %15 vs. sanat eserinde 90°
**Sorun:** GDD §4 redline'ı "son %15" diyor; çizilen yay ise 90°'lik bir çeyrek.
**Çözüm:** Sanata sadık kalınıyor — sweep **-90° … +90°** (180°, alt tarafta simetrik boşluk, klasik gösterge görünümü). Redline yayı 0°'da başladığı için `redlineThreshold = 0.50`.
GDD'nin *risk/ödül niyetini* korumak için bonus **rampalı**:
`redlineMult = lerp(1.0, 2.0, (t - 0.5) / 0.5)`
→ t=0,5'te 1,0× · t=0,75'te 1,5× · t=1,0'da tam 2,0×.
Yani gerçek 2× yalnızca overheat uçurumunun hemen dibinde. `Flat` mod da config'te mevcut.

### 2.5 Ses varlığı yok
**Çözüm:** `AudioManager`, klipleri **çalışma anında prosedürel üretir** (`AudioClip.Create`) — chiptune estetiğine birebir uyuyor:
- Tıklama: 8 ms kare dalga, 880 Hz
- İbre yükselme: hıza bağlı pitch'li testere dalgası (sürekli, döngülü)
- İndirme bitişi: iki tonlu arpej (C-E-G), 200 ms
- Overheat: 120 Hz alçalan kare dalga + gürültü
- Kademe atlama: yükselen 5 nota
Arka plan müziği (BGM) slot olarak hazır, boş; gerçek klip verirsen tek satırda bağlanır.

### 2.6 Retro font yok
**Çözüm:** TMP varsayılanı (LiberationSans) + retro his **materyal ayarıyla**: outline 0,15, softness 0, `ALL CAPS`, letter spacing +8, tabular figures (sayılar zıplamasın).
Rakamlar için `Mono` benzeri hizalama TMP `<mspace=0.6em>` etiketiyle sağlanır — bakiye/hız güncellenirken karakter genişliği sabit kalır.

### 2.7 Reklam / IAP SDK yok
**Çözüm:** `IAdService` arayüzü + `StubAdService`. Sahte reklam paneli **var olan sprite'larla** kurulur: `ui_panel_bg` + `ui_icon_reward_ad` + `ui_icon_close` + 3 sn geri sayım. Tam işlevsel, internetsiz çalışır. Gerçek SDK sonradan tek sınıf değişimiyle takılır.

### 2.8 (Ek) Sonsuz kademe (9+) kadran zemini yok
**Çözüm:** `dial_tier8_datacenter_bg` + `SpeedDownload/HueShift` shader'ı. Kademe indeksi × 137,5° (altın açı) ile hue döndürülür → her sonsuz kademe farklı renkte. Oda: `bg_tier9_abstract_space`.

### 2.9 (Ek) `dial_tier1_phoneline_bg` delikli
Merkez **ve** çevirmeli disk boncuklarının içi şeffaf (kaynakta parmak delikleri alfaya çevrilmiş).
**Çözüm:** `DialRoot` altına en alta `DialBackplate` — Unity'nin **yerleşik `Knob` sprite'ı** (dolu daire, motorla gelir) koyu renk tint'li. Dokuz kademede de durur, sadece tier1'de görünür. Rotary telefon deliklerinden koyu zemin görünmesi zaten doğru görünüm.

---

## 3. Mimari Kararlar

### 3.1 Hız birimi: `double`, **bit/saniye**
1 bps → 1e12+ ve prestij sonrası ötesi. `float` 1e7'de hassasiyet kaybeder, `long` Ebps'te taşar. `double` ~1e308'e gider. Para da `double`. Görüntüleme tek noktadan `NumberFormatter` ile.

### 3.2 Kadran ölçeği: **logaritmik**
GDD §3 aralıkları dönüşümlü 2 dekat (1→100 bps) ve 1 dekat (100→1000 bps). Lineer haritalamada 2 dekatlık kademelerde ibre zamanın %90'ında dibe yapışır.
`t = (log10(hız) − log10(min)) / (log10(max) − log10(min))`
`ConnectionTierSO.useLogarithmicScale` ile kademe bazında kapatılabilir.

### 3.3 Veri: ScriptableObject + Editor üreteci
9 kademe + ~22 dosya + 13 yükseltme = **44 `.asset`**. Elle kurmak yavaş ve hataya açık. `Tools/SpeedDownload/Generate Data Assets` hepsini GDD tablolarındaki değerlerle üretir, sprite'ları `AssetDatabase` ile bağlar. Var olan asset'in üstüne yazmaz.

### 3.4 Tek sahne
`Assets/Scenes/Game.unity`. Ana menü `CanvasGroup` overlay'i. Idle oyunda sahne yükleme gereksiz singleton zinciri demek. `SampleScene` dokunulmadan kalır.

### 3.5 Bağımlılık yönü
`Core` (saf mantık) ← `Systems` ← `UI`. UI asla state yazmaz; `event` dinler, komut çağırır.

### 3.6 Assembly Definition
`SpeedDownload.Runtime.asmdef` + `SpeedDownload.Editor.asmdef` — MCP ile iteratif çalışırken derleme süresini kısaltır.

---

## 4. Klasör ve Dosya Yapısı

```
Assets/
  Scripts/
    Core/       GameManager · GameState · SpeedController · DownloadController
    Systems/    EconomyManager · TierManager · PrestigeManager · SaveManager
                GameEventManager · AudioManager · FXManager · AdService
    Data/       ConnectionTierSO · FileDataSO · UpgradeSO · GameConfigSO · GameDatabaseSO
    UI/         HUDController · DialView · UpgradePanelController · UpgradeCardView
                PrestigePanelController · EventBannerController · MascotController
                MainMenuController · TabGroupController · RewardAdOverlay
    Util/       NumberFormatter · SimplePool · SpriteRefs · ProceduralAudio
    Editor/     SpriteImportTool · DataAssetGenerator · SceneBuilder
  Shaders/      SpriteSolidTint.shader · HueShift.shader
  ScriptableObjects/
    Tiers/ Files/ Upgrades/ GameConfig.asset GameDatabase.asset
  Prefabs/      Dial/DialRoot · UI/UpgradeCard · FX/*
  Scenes/Game.unity
  Sprites/      (mevcut 82 dosya — DEĞİŞTİRİLMEYECEK)
```

> **Kural (GDD §13.2):** Hiçbir aşamada sprite üretilmez/değiştirilmez.

---

## 5. Kademe → Görsel Eşlemesi

| # | Ad | Aralık (bit/sn) | Kadran | Bağlantı ikonu | Oda | İbre rengi |
|---|---|---|---|---|---|---|
| 0 | Sinyal Kulübesi | 1 – 1e2 | `dial_tier0_signal_bg` | — | `bg_tier0_signal_room` | koyu kırmızı |
| 1 | Telefon Hattı | 1e2 – 1e3 | `dial_tier1_phoneline_bg` | `icon_conn_phoneline` | `bg_tier1_2_retro_room` | koyu kırmızı |
| 2 | Dial-up 56K | 1e3 – 1e5 | `dial_tier2_dialup_bg` | `icon_conn_dialup` | `bg_tier1_2_retro_room` | kırmızı |
| 3 | ISDN | 1e5 – 1e6 | `dial_tier3_isdn_bg` | `icon_conn_isdn` | `bg_tier1_2_retro_room` +mavi tint | kırmızı |
| 4 | ADSL | 1e6 – 1e8 | `dial_tier4_adsl_bg` | `icon_conn_adsl` | `bg_tier4_modern_room` | turuncu |
| 5 | VDSL | 1e8 – 1e9 | `dial_tier5_vdsl_bg` | `icon_conn_vdsl` | `bg_tier4_modern_room` +cyan tint | turuncu |
| 6 | Tam Fiber | 1e9 – 1e11 | `dial_tier6_fiber_bg` | `icon_conn_fullfiber` | `bg_tier6_hightech_room` | **neon turuncu** |
| 7 | Kuantum | 1e11 – 1e12 | `dial_tier7_quantum_bg` | `icon_conn_quantum` | `bg_tier7_quantum_lab` | **neon macenta** |
| 8 | Veri Merkezi | 1e12 – 1e14 | `dial_tier8_datacenter_bg` | `icon_conn_datacenter` | `bg_tier8_server_room` | **neon sarı** |
| 9+ | Sonsuz | ×100 / kademe | tier8 + HueShift | `icon_conn_datacenter` +tint | `bg_tier9_abstract_space` | hue döngüsü |

---

## 6. `DialRoot.prefab` — Ölçülmüş Değerlerle

```
DialRoot                       512 × 512
 ├─ DialBackplate    yerleşik "Knob" sprite, koyu tint, ölçek 0,90
 ├─ Background       dial_tierX_*_bg      ölçek 1,00   (kademeyle swap)
 ├─ TickMarksOverlay dial_tick_marks      ölçek 1,08   (varsayılan KAPALI *)
 ├─ RedlineOverlay   dial_redline_overlay ölçek 1,00   (rotasyon YOK)
 └─ NeedlePivot      (tam merkez, boş GO)  -> rotasyon burada
     └─ NeedleSprite dial_needle  236 × 40  pivot (0,1 · 0,5)
                     Material: SpriteSolidTint, renk = tier.needleColor
```
\* Dokuz zeminde tik halkası zaten çizili (Rehber §3.0). Obje kurulur ama `SetActive(false)` başlar.

**İbre açı haritası (ölçüme dayalı):**
```
minAngle = -90°   (saat 9)      t = 0
maxAngle = +90°   (saat 3)      t = 1
redline  = +0°..+90°            t = 0,50 .. 1,00
```

---

## 7. Çekirdek Sistemler

### 7.1 SpeedController (GDD §4)
```
currentSpeed += ClickPower × clickCoefficient × prestigeMult      // tıklama
currentSpeed  = baseSpeed + (currentSpeed-baseSpeed) × exp(-decay×dt)
currentSpeed  = min(currentSpeed, tier.maxSpeed)
needleAngle  -> SmoothDamp(hedef, tier.needleInertia)             // ağırlık hissi
```
- **Redline:** `t ≥ 0,50` → rampalı 1,0× → 2,0× (bkz. 2.4). Aktifken `RedlineOverlay` nabız gibi parlar + `fx_turbo_flame`.
- **Overheat:** `t ≥ 0,995` kesintisiz `overheatLimit` sn (2,0 + 0,5/Fan sv.) → hız 0, girdi 2 sn kilitli, `fx_overheat_flash` + `fx_overheat_smoke` + `mascot_overheated` + toast.
- **Kritik (DNS):** `critChance` ile `currentSpeed ×= 5`, `fx_critical_burst`.

### 7.2 DownloadController (GDD §5)
```
effectiveSpeed = currentSpeed × redlineMult × happyHour × quota × wifiSteal
progressBits  += effectiveSpeed × dt
```
Bitince: ödül = `baseReward × (1+premium) × prestijMult × happyHour` → `fx_coin_burst`, prosedürel bildirim sesi, `mascot_happy` 1 sn, sıradaki dosya havuzdan rastgele (art arda aynı gelmez).

### 7.3 EconomyManager (GDD §6.3)
```
Cost(n)    = baseCost × 1,15^n
ClickPower = baseCP × (1 + 0,15×lvl) × prestigeMult
BaseSpeed  = tier.baseSpeed × (1 + 0,10×passiveLvl)
Offline    = passiveIncome/sn × min(elapsed, cap) × efficiency
```
Sekmeler: **Altyapı** = 8 bağlantı · **Donanım** = hammer, fan · **Yazılım** = dns, downloadmanager, premiumserver.

### 7.4 SaveManager
`persistentDataPath/save.json`, `JsonUtility`. Autosave 15 sn + `OnApplicationPause` + `OnApplicationQuit`. `lastSaveUtcTicks` ile offline kazanç; `elapsed < 0` (saat geri alma) → 0. `saveVersion` alanı migration için.

### 7.5 PrestigeManager (GDD §7)
Kademe 8'de açılır. `credits = floor(sqrt(lifetime / 1e9))`, çarpan `1 + 0,02 × toplam`.
Sıfırlanır: para, yükseltmeler, kademe. Korunur: Fiber Kredisi, toplam kazanç, ayarlar.

### 7.6 GameEventManager (GDD §8)
Min 90 sn ara, ağırlıklı seçim:
- **Komşu Wi-Fi** → `×0,6`, `event_neighbor_wifi_icon`; `event_password_lock_icon` 3 tık ile düzelir
- **Kota** → hız tabana, `event_quota_icon`; ödeme veya ödüllü reklam sıfırlar
- **Gece Tarifesi** → 45 sn ×3, `event_happyhour_banner` üstten kayar

---

## 8. Sahne Hiyerarşisi (`Game.unity`)

```
[GameManager]        GameManager · EconomyManager · TierManager · PrestigeManager
                     SaveManager · GameEventManager · AudioManager · FXManager
[SpeedController] · [DownloadController]

Main Camera          Orthographic, URP 2D
RoomBackground       SpriteRenderer <- bg_*  (kademeyle çapraz fade + tint)

Canvas (Overlay, 1080×1920 portre, Match 0.5)
 ├─ SafeArea
 │   ├─ TopPanel      ui_panel_bg (border 30,45,30,30)
 │   │   ├─ BalanceGroup   ui_icon_balance + TMP
 │   │   ├─ SpeedGroup     ui_icon_speed + TMP
 │   │   └─ FileGroup      icon_file_* + ad + ProgressBar + %
 │   ├─ DialArea      -> DialRoot.prefab
 │   ├─ MascotAnchor  mascot_idle / _happy / _overheated
 │   ├─ BottomPanel
 │   │   ├─ TabBar     ui_tab_infrastructure/_hardware/_software/_prestige
 │   │   └─ TabContent ScrollView -> UpgradeCard
 │   ├─ EventBanner · ToastLayer · FXLayer
 │   └─ TopBarButtons ui_icon_settings · ui_icon_sound_on/off · ui_icon_close
 └─ MainMenuOverlay   bg_mainmenu + logo_main + BAŞLA
```

**ProgressBar kurulumu (ölçülmüş):**
```
ProgressBar (512×167)
 ├─ Frame  ui_progressbar_frame   (border 75,20,75,20)
 └─ Fill   ui_progressbar_fill    offset L21 R22 T11 B16
           Image Type = Filled, Horizontal, Origin Left
```

---

## 9. Import Ayarları — Tek Komutla

`Tools/SpeedDownload/1. Apply Sprite Import Settings` (idempotent):

| Kural | Uygulama |
|---|---|
| Tümü | `Sprite (2D and UI)` · Single · PPU 100 · **Mip Maps kapalı** · Wrap **Clamp** · Bilinear · Read/Write kapalı |
| `Dial/`, `FX/`, `Mascot/`, `logo_main` | Max **512**, `alphaIsTransparency = true` |
| `FileIcons/`, `UpgradeIcons/`, `ui_icon_*`, `ui_tab_*`, `event_*_icon` | Max **256** (4× bellek tasarrufu) |
| `ui_button_*`, `ui_panel_bg`, `ui_progressbar_*` | Max 512 + **ölçülmüş border** (Bölüm 1.2) |
| `event_happyhour_banner` | Max 512, border yok |
| `Backgrounds/` | Max 1024, `alphaIsTransparency = false` |
| `dial_needle` | **Custom pivot (0,1 · 0,5)** |
| `logo_icon_appstore` | Sprite değil → Player Settings > Icon |
| Platform override | **Android + iOS: ASTC 6×6** (Backgrounds ASTC 8×8). Editor/Standalone `Compressed` kalır — ASTC yalnız mobilde anlamlı |

---

## 10. Uygulama Fazları

Her fazın sonunda **Play** testi; onaylamadan sonrakine geçilmez.

| Faz | İçerik | Test kriteri |
|---|---|---|
| **0** ✅ | Klasörler · 2 asmdef · 2 shader · `NumberFormatter` · `SpriteImportTool` çalıştırılır | **TAMAMLANDI** — 82/82 doğrulandı (bkz. Bölüm 15) |
| **1** ✅ | 5 SO tipi + `DataAssetGenerator` | **TAMAMLANDI** — 49 `.asset`, tüm sprite'lar bağlı (bkz. Bölüm 16) |
| **2** ✅ | `DialRoot.prefab` · `DialView` · `SpeedController` · sahne iskeleti | **KOD TAMAM** — sahne kuruldu, Play temiz; ibre/redline/overheat testi sende (bkz. Bölüm 17) |
| **3** ✅ | `DownloadController` · dosya DB · üst HUD | **KOD TAMAM** — döngü Play'de doğrulandı: 3 dosya bitti, para geldi, sıradaki başladı (bkz. Bölüm 18) |
| **4** ✅ | `EconomyManager` · yükseltme kartları · 3 sekme | **KOD TAMAM** — maliyet eğrisi 1,15^n birebir, satın alma ve kilitler doğrulandı (bkz. Bölüm 19) |
| **5** ✅ | `TierManager` — 9 kademe, kadran+oda+birim swap | **KOD TAMAM** — 9 kademe yüründü, birim zinciri doğru, ibre süzülüyor (bkz. Bölüm 20) |
| **6** ✅ | `SaveManager` + offline kazanç | **KOD TAMAM** — kapat-aç durum birebir korundu, offline raporu çıktı (bkz. Bölüm 21) |
| **7** ✅ | `PrestigeManager` + sonsuz kademe (HueShift) | **KOD TAMAM** — prestij döngüsü birebir, 9+ üretimi ve hue kayması çalışıyor (bkz. Bölüm 22) |
| **8** ✅ | `GameEventManager` + banner UI | **KOD TAMAM** — 3 olay da doğrulandı, şifre 3 tıkla çözülüyor (bkz. Bölüm 23) |
| **9** ✅ | `FXManager` (7 FX havuzu) · `MascotController` · ana menü · ayarlar | **KOD TAMAM** — havuz 3500 çağrıda büyümedi; menü/ayarlar çalışıyor (bkz. Bölüm 24) |
| **10** ✅ | `ProceduralAudio` · `StubAdService` · mobil Player Settings · app icon | **KOD TAMAM** — portre kilitli, IL2CPP+ARM64, simge atandı; build senin makinende (bkz. Bölüm 25) |

---

## 11. Başlangıç Komutları ve Çalışma Akışı

### 11.1 Her oturum öncesi (senin tarafında)
1. Unity Editor **açık** olsun (proje yüklü).
2. **Window → MCP for Unity** → durum **Connected** olmalı.
3. **Play mode'dan çık** — Play'deyken script derlemesi ve asset yazımı bloklanır.
4. Konsolu temizle (Clear) — yeni hataları ayırt edebilmek için.

### 11.2 Bana vereceğin komutlar

| Ne zaman | Yaz |
|---|---|
| Fazı başlat | `Faz 0 başla` · `Faz 1 başla` … |
| Hata varsa | `Konsolda şu hata var: <yapıştır>` |
| Görsel kontrol | `Sahnenin ekran görüntüsünü al` |
| Denge ayarı | `overheatLimit 3 saniye olsun` |
| Fazı onayla | `Faz 2 tamam, devam` |
| Geri al | `Son değişikliği geri al` |

### 11.3 Unity menü komutları (Faz 0'da ben oluşturacağım)

```
Tools/SpeedDownload/1. Apply Sprite Import Settings
Tools/SpeedDownload/2. Generate Data Assets
Tools/SpeedDownload/3. Build Game Scene
Tools/SpeedDownload/Reset Save Data
Tools/SpeedDownload/Open Save Folder
```

### 11.4 Terminal komutları (opsiyonel, ileri seviye)

Kayıt dosyasını incelemek:
```powershell
Get-Content "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\SpeedDownload Idle Megabit Tycoon\save.json"
```

Kaydı silmek (sıfırdan test):
```powershell
Remove-Item "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\SpeedDownload Idle Megabit Tycoon\save.json"
```

Komut satırından Android build (Faz 10):
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -quit -batchmode `
  -projectPath "C:\Users\Burak\Desktop\Application\SpeedDownload Idle Megabit Tycoon" `
  -executeMethod SpeedDownload.Editor.BuildTool.BuildAndroid -logFile build.log
```

---

## 12. Denge Başlangıç Değerleri (`GameConfig.asset`)

| Parametre | Değer | Not |
|---|---|---|
| `baseClickPower` | 8 bit/sn | |
| `clickCoefficient` | 1,0 | |
| `speedDecayRate` | 1,8 /sn | üst kademelerde 1,2 |
| `needleInertia` (t0→t8) | 0,05 → 0,22 sn | "ağır ibre" hissi |
| `minAngle / maxAngle` | **−90° / +90°** | ölçüme dayalı |
| `redlineThreshold` | **0,50** | çizilen yayın başlangıcı |
| `redlineBonusMode` | **Ramp** | 1,0× → 2,0× |
| `overheatLimit` | 2,0 sn | +0,5 / Fan sv. |
| `overheatPenalty` | 2,0 sn | tam kesinti |
| `critChance / critMult` | 0,05 / 5,0 | DNS sv.1 |
| `upgradeCostGrowth` | 1,15 | |
| `offlineEfficiency` | 0,50 · 8 sa | Pro → 1,00 · 24 sa |
| `prestigeThreshold` | 1e9 | |
| `eventMinInterval` | 90 sn | |

---

## 13. Riskler

| Risk | Önlem |
|---|---|
| Redline %50 çok cömert olabilir | Rampalı bonus (2.4) + Play testinde `redlineThreshold` tek değerle ayarlanır |
| Prosedürel ses "ucuz" durabilir | Slot'lar hazır; gerçek klip gelince tek satır değişir |
| `double` hassasiyeti sonsuz kademede | Prestij sıfırlıyor; 1e100 üstünde bilimsel gösterim |
| Her frame UI string → GC | HUD 10 Hz, `StringBuilder` + önbellek; her frame yalnız ibre rotasyonu |
| 9-slice border'lar görsel doğrulanmadı | Faz 0'da `SceneView_Capture` ile ekran görüntüsü alınıp kontrol edilecek |
| Kademe geçişinde kadran zıplaması | 9 zemin aynı çerçevede (%94,5) — swap'ta yalnız sprite değişir |

---

## 14. Faz 0'ın Değiştireceği Dosyalar (onay için)

Faz 0 başladığında **82 `.png.meta` dosyası** değişecek (import ayarları). Bu:
- Geri alınabilir — Unity'de sprite'lara sağ tık → Reimport ile varsayılana döner,
- PNG'lerin **kendisine dokunmaz**, sadece Unity'nin import ayarlarını yazar.

Ayrıca oluşturulacak yeni klasörler: `Assets/Scripts`, `Assets/Shaders`, `Assets/ScriptableObjects`, `Assets/Prefabs`.

---

## 15. Faz 0 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

```
Assets/Scripts/SpeedDownload.Runtime.asmdef        -> Unity.TextMeshPro, Unity.InputSystem, UnityEngine.UI
Assets/Scripts/Editor/SpeedDownload.Editor.asmdef  -> Editor-only
Assets/Scripts/Util/NumberFormatter.cs             -> hiz/para/boyut/süre/yüzde biçimleme
Assets/Scripts/Editor/SpriteImportTool.cs          -> Apply + Verify menüleri
Assets/Scripts/Editor/ShaderMaterialTool.cs        -> materyal üretimi + shader derleme doğrulaması
Assets/Shaders/SpriteSolidTint.shader              -> 9 property
Assets/Shaders/HueShift.shader                     -> 10 property
Assets/Materials/NeedleSolidTint.mat
Assets/Materials/DialHueShift.mat
```

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Assembly derlemesi | `SpeedDownload.Runtime.dll` + `SpeedDownload.Editor.dll` ✅ |
| Import uygulaması | **82/82 doku güncellendi** |
| `Verify Sprite Import Settings` | **"82 dokunun tamamı beklenen ayarlarda"** ✅ |
| `dial_needle` pivot | `spritePivot {0.1, 0.5}`, `alignment: 9` (Custom) ✅ |
| `ui_button_normal` border | `{70, 60, 70, 60}` ✅ |
| `ui_progressbar_frame` border | `{75, 20, 75, 20}` ✅ |
| Backgrounds | maxSize 1024, `alphaIsTransparency: 0`, mipmap kapalı ✅ |
| İkonlar | maxSize 256 ✅ |
| Android / iOS override | Dial+UI **ASTC_6x6** (format 50), Backgrounds **ASTC_8x8** (format 51) ✅ |
| `logo_icon_appstore` | Default type, mobil override yok (Player Settings > Icon için) ✅ |
| Shader derlemesi | `ShaderUtil.ShaderHasError` → ikisi de temiz ✅ |

### Yol boyunca çıkan tek hata
`ShaderUtil.GetShaderPropertyCount` Unity 6'da yok; doğrusu `ShaderUtil.GetPropertyCount`. Düzeltildi.

### Menü komutları (artık kullanılabilir)
```
Tools/SpeedDownload/0. Create Shader Materials
Tools/SpeedDownload/1. Apply Sprite Import Settings
Tools/SpeedDownload/Verify Sprite Import Settings
```
`2. Generate Data Assets` ve `3. Build Game Scene` sırasıyla Faz 1 ve Faz 2'de eklenecek.

---

## 16. Faz 1 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

```
Assets/Scripts/Data/ConnectionTierSO.cs   + SpeedToNormalized / NormalizedToSpeed
Assets/Scripts/Data/FileDataSO.cs         + IsAvailableAt
Assets/Scripts/Data/UpgradeSO.cs          + UpgradeType(6) / UpgradeTab(3) / CostAtLevel
Assets/Scripts/Data/GameConfigSO.cs       tüm denge sabitleri
Assets/Scripts/Data/GameDatabaseSO.cs     + önbellekli GetFilesForTier / PickRandomFile
Assets/Scripts/Editor/DataAssetGenerator.cs
```

### Üretilen varlıklar — 49 adet

| Klasör | Adet |
|---|---|
| `ScriptableObjects/` | 2 (GameConfig, GameDatabase) |
| `ScriptableObjects/Tiers/` | 10 (Tier0–Tier8 + sonsuz şablon) |
| `ScriptableObjects/Files/` | 24 (23 dosya + prosedürel şablon) |
| `ScriptableObjects/Upgrades/` | 13 (8 bağlantı + 2 donanım + 3 yazılım) |

**Doğrulama:** GameDatabase içinde **49 GUID referansı** — config + 9 kademe + 24 dosya + 13 yükseltme + 2 şablon. Eksik/null yok. Sprite bağlantıları `Tier0` üzerinde noktasal doğrulandı (`dialBackground`, `roomBackground` bağlı; `connectionIcon` bilinçli olarak boş — kademe 0 prolog).

### ⚠ GDD'den bilinçli sapma: dosya boyutları türetiliyor

GDD Bölüm 5'teki sabit boyut tablosu düşük kademelerde tutarsızdı:
`odev_v2.docx` 250 KB = 2.048.000 bit; kademe 1'in tavan hızı 1.000 bps → **34 dakikalık indirme**.

**Çözüm:** boyutlar kademe hızından türetiliyor —
```
refSpeed = 10^(log10(min) + 0.6 × (log10(max) − log10(min)))
sizeBits = refSpeed × fileTargetSeconds × sizeMult
```
`fileTargetSeconds` ve `fileReferenceSpeedT` `GameConfig`'te; boyutlar tek değer değiştirilerek yeniden dengelenebilir.
Üst kademelerde sonuç GDD'ye zaten yakın (kademe 6 → 13–66 GB, GDD 60–250 GB).

### ⚠ Bağlantı maliyetleri de türetiliyor

`cost(t) = ortalamaÖdül(t−1) × 25`. Böylece **her kademe ~25 dosyalık gelire mal olur** — mutlak sayılar
kademeler arası eşit büyümese bile ilerleme temposu sabit kalır.

### Üretilen denge tablosu (özet)

| Kademe | Referans hız | Dosya süreleri | Sonraki bağlantı |
|---|---|---|---|
| 0 Sinyal Kulübesi | 15.8 bps | 4–12 sn | $8.8 |
| 1 Telefon Hattı | 398 bps | 6–24 sn | $58 |
| 2 Dial-up 56K | 15.8 Kbps | 6–30 sn | $1K |
| 3 ISDN | 398 Kbps | 8–19 sn | $3.2K |
| 4 ADSL | 15.8 Mbps | 7–31 sn | $26K |
| 5 VDSL | 398 Mbps | 6–33 sn | $1.2M |
| 6 Tam Fiber | 15.8 Gbps | 7–36 sn | $64M |
| 7 Kuantum | 398 Gbps | 9–24 sn | $1.6B |
| 8 Veri Merkezi | 15.8 Tbps | 10–26 sn | — (prestij) |

Yükseltmeler: Modeme Vurmak $4 (sınırsız) · Harici Fan $60 (max 10) · DNS $250 (max 5) · Premium Sunucu $1.2K (sınırsız) · Download Manager $2K (max 2).

### Menü komutları (eklenenler)
```
Tools/SpeedDownload/2. Generate Data Assets              -> üstüne yazmaz
Tools/SpeedDownload/2b. Regenerate Data Assets (OVERWRITE) -> onay ister, sıfırlar
```

---

## 17. Faz 2 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

**Runtime — `Assets/Scripts/Core/`**
| Dosya | İş |
|---|---|
| `GameManager.cs` | Tek giriş noktası. `Instance`, `Database`, `Config`, `CurrentTier`, `TierChanged` olayı, `SetTier()`. `[DefaultExecutionOrder(-100)]` ile diğer her şeyden önce uyanır. |
| `SpeedController.cs` | Speed Limiter fiziği. Üstel sönümleme, redline rampası, overheat sayacı. Tüm hızlar `double` bit/sn. |

**Runtime — `Assets/Scripts/UI/`**
| Dosya | İş |
|---|---|
| `DialView.cs` | İbre açısı, kademeye göre kadran/ibre rengi swap'i, redline yanıp sönmesi. |
| `ClickCatcher.cs` | Tam ekran görünmez tıklama alanı (`IPointerDownHandler`). |
| `RoomBackgroundView.cs` | Oda arka planı "cover" ölçekleme + `roomTint` cross-fade. |
| `DebugHud.cs` | 10 Hz sayısal panel + `1-9` kademe kısayolları. Faz 3'te gerçek HUD gelince kaldırılacak. |

**Editor**
| Dosya | İş |
|---|---|
| `SceneBuilder.cs` | `Tools/SpeedDownload/3. Build Game Scene`. `Game.unity` + `DialRoot.prefab` sıfırdan üretir, sonra bağlantıları doğrular. |

**Üretilen varlıklar**
- `Assets/Scenes/Game.unity` (build listesine eklendi, sıra 0)
- `Assets/Prefabs/Dial/DialRoot.prefab` (7 nesne)

### Sahne yapısı

```
Game.unity
├── Main Camera          orthographic · SolidColor #0D0D12 · z=-10
├── [Game]               GameManager + SpeedController
├── Canvas               ScreenSpaceOverlay · 1080x1920 · match 0.5
│   ├── RoomBackground   3413x1920 (cover) · RoomBackgroundView
│   ├── ClickCatcher     stretch · alpha 0 · raycastTarget AÇIK
│   ├── DialArea         y=+240
│   │   └── DialRoot     512x512  [prefab]
│   │       ├── DialBackplate    461 px · builtin Knob · #171A21
│   │       ├── Background       512 px · dial_tier0_signal_bg
│   │       ├── TickMarksOverlay 553 px · KAPALI (kadranlarda zaten çizili)
│   │       ├── RedlineOverlay   512 px · alpha 0.85
│   │       └── NeedlePivot      0x0 · burası döner
│   │           └── NeedleSprite 236x40 · pivot(0.1, 0.5) · NeedleSolidTint
│   └── DebugPanel       620x640 sol-alt · ui_panel_bg 9-slice
└── EventSystem          InputSystemUIInputModule · InputSystem_Actions
```

Kadran katmanlarının **hepsinde `raycastTarget = false`**; tıklama alttaki `ClickCatcher`'a düşer. Tek raycast hedefi o.

### Açı haritası (ölçüme dayalı)

`dial_redline_overlay` yayı tam olarak 0°..+89° (saat 12 → saat 3) kapsıyor, bu yüzden süpürme **-90°..+90°**:

```
saatYönündeTepedenAçı = Lerp(-90, +90, t)
zRotasyon             = 90 - saatYönündeTepedenAçı
```

| t | saat yönü | z | kadran konumu |
|---|---|---|---|
| 0.0 | -90° | 180° | saat 9 (sol) |
| 0.5 | 0° | 90° | saat 12 (tepe) — **redline başlangıcı** |
| 1.0 | +90° | 0° | saat 3 (sağ) |

Redline eşiği t≥0.50 = saat 12, yani çizili yayın tam başlangıcı. Sanat ile matematik birebir örtüşüyor.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme (Runtime + Editor) | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` kendi doğrulaması (11 referans) | ✅ hepsi bağlı |
| Eksik görsel | ✅ 0 |
| Play mode | ✅ oyun kodundan tek log yok |
| İbre başlangıç açısı (canlı ölçüm) | ✅ z = 180.000 — t=0 ile birebir |
| EventSystem UI aksiyonları | ✅ Point/Click/Navigate/Submit/Cancel bağlı |

### Yol boyunca çıkan üç hata

1. **`SaveAsPrefabAssetAndConnect` dönüş değeri prefab ASSET'idir, sahne örneği değil.**
   Dönüş değerine `SpeedController` bağlamaya çalışınca Unity sessizce yok sayıyordu (bir prefab asset'i sahne nesnesine referans veremez). Çözüm: bağlamayı orijinal sahne nesnesi (`dialRoot`) üzerinden yap — o zaten prefab'a bağlanmış durumda kalıyor.

2. **`NewScene(Single)` kullanılmayan asset'leri boşaltıyor.**
   `GameDatabase` referansı sahne açılmadan ÖNCE alınırsa yok edilmiş bir sarmalayıcı elde ediliyor: alan doğru guid ile serileşiyor ama `== null` **true** dönüyor. Bu ikili davranış hatayı sinsi yapıyordu — dosya doğru görünüyor, kod çalışmıyor. Çözüm: referansı `NewScene`'den sonra al.

3. **`Graphic` soyut sınıf**, `[RequireComponent(typeof(Graphic))]` çalışmaz. `Image` ile değiştirildi.

Ayrıca `FindFirstObjectByType` → `FindAnyObjectByType` ve `ShaderUtil.GetPropertyCount` → `shader.GetPropertyCount()` obsolete uyarıları temizlendi.

### Senin Play testin

`Assets/Scenes/Game.unity` aç, **Play**:

| Ne yapılacak | Beklenen |
|---|---|
| Hiçbir şey yapma | İbre sol dipte (saat 9), hız 1 bps |
| Ekrana hızlı tıkla | İbre saat yönünde fırlıyor, bırakınca yumuşak düşüyor |
| İbreyi saat 12'nin sağına çıkar | Panelde `<<< REDLINE`, çarpan 1.00x'ten 2.00x'e doğru tırmanıyor |
| Tam gazda ~2 sn tut | `!!! MODEM AŞIRI ISINDI !!!`, ibre sıfıra düşüyor, 2 sn sonra dönüyor |
| `1`–`9` tuşları | Kadran ve oda değişiyor; **6/7/8'de ibre koyu zeminde görünür kalmalı** (SpriteSolidTint testi) |

Kritik olan sonuncusu: `dial_needle.png` %3.5 opak, lacivert (39,53,76) bir dış çizgi. Tier 6/7/8 kadranları da lacivert. Alpha-maskeli shader olmasaydı ibre o üç kademede kaybolurdu.

### Menü komutları (eklenenler)
```
Tools/SpeedDownload/3. Build Game Scene   -> sahneyi ve prefab'i SIFIRDAN kurar
```
⚠ Bu komut `Game.unity`'yi tamamen yeniden yazar. Faz 3'ten itibaren sahneye elle eklenen her şey kaybolur — o yüzden yeni nesneler `SceneBuilder`'a eklenmeli, sahneye elle değil.

---

## 18. Faz 3 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

**Runtime — `Assets/Scripts/Core/`**
| Dosya | İş |
|---|---|
| `DownloadController.cs` | `ilerlemeBit += EffectiveSpeed × SpeedMultiplier × dt`. Dosya bitince ödül → cüzdan, artan bit devreder, havuzdan yeni dosya. |
| `Wallet.cs` | Bakiye + toplam kazanç. `Add` · `TrySpend` · `CanAfford` · `Restore`. `BalanceChanged` olayı. |

**Runtime — `Assets/Scripts/UI/`**
| Dosya | İş |
|---|---|
| `TopHudView.cs` | Bakiye · hız · dosya simgesi/adı/boyutu · % · kalan süre. |
| `ProgressBarView.cs` | Çerçeve + dolgu; `SmoothDamp` ile yumuşak ilerleme. |
| `SafeAreaFitter.cs` | `Screen.safeArea`'yı anchor'a çevirir — çentikli telefonlarda üst HUD kesilmez. |

`GameManager`'a `Download` ve `Money` erişimcileri eklendi. `SceneBuilder` üst paneli ve `SafeArea` katmanını üretiyor.

### Güncellenen sahne yapısı

```
Canvas
├── RoomBackground     tam ekran (SafeArea DIŞINDA — çentiğin altına uzanmalı)
├── ClickCatcher       tam ekran (SafeArea DIŞINDA — her yere dokunulabilmeli)
└── SafeArea           SafeAreaFitter
    ├── TopPanel       1080-40 x 420, tepede · TopHudView
    │   ├── BalanceGroup  sol yarı: ui_icon_balance + tutar
    │   ├── SpeedGroup    sağ yarı: ui_icon_speed + anlık hız
    │   ├── FileIcon      112x112
    │   ├── FileNameText / FileSizeText
    │   ├── ProgressBar   Frame (9-slice) + Fill (Filled/Horizontal)
    │   └── PercentText (sol) · EtaText (sağ)
    ├── DialArea       y=+150  (Faz 2'de +240'tı — üst panele yer açıldı)
    │   └── DialRoot   [prefab]
    └── DebugPanel     sol-alt
```

Kadran merkezi **+240 → +150**'ye indi. Üst panel tepeden 540'a kadar iniyor, kadranın üst kenarı +406'da kalıyor; çentik yüzünden SafeArea daralsa bile iki blok çakışmıyor.

### İlerleme çubuğu — ölçülen oyuk neden her boyutta doğru

`ui_progressbar_frame` gerçekten içi boş (%21,5 opak). Oyuk: soldan 21, sağdan 22, üstten 11, alttan 16 px.
9-slice kenarları **(75, 20, 75, 20)** olduğu için bu dört değer de kenar bölgesinin *içinde* kalıyor — kenarlar esnemediğinden oyuk her genişlikte aynı piksel mesafede duruyor. Yani dolgu offset'leri sabit yazılabiliyor, çubuk 960 px'e gerilse de oturuyor.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (24 referans) | ✅ hepsi bağlı |
| Eksik görsel | ✅ 0 |
| Tıklama → hız → redline | ✅ 200 tıklama → 100 bps tavan, t=1.000, redline tam **2x**, etkin **200 bps** |
| Dosya tamamlama | ✅ 3 dosya bitti |
| Para akışı | ✅ `ping_test $0.2` + `bos_belge $0.5` → bakiye **$0.7** |
| Sıradaki dosya | ✅ otomatik başladı, art arda aynı dosya gelmedi |
| Artan bit devri | ✅ tamamlamadan sonra 91,9 bit yeni dosyaya taşındı |
| HUD metinleri | ✅ `$0.7` · `200 bps` · `ping_test.txt` · `10 B` · `%48` · `<1 sn kaldi` |
| Play mode hataları | ✅ oyun kodundan tek log yok |

### Test yöntemi hakkında bir not

Unity penceresi odakta değilken Play mode kare işlemiyor (`Time.frameCount` 1'de takılı kaldı). Doğrulama `EditorApplication.Step()` ile kare kare ilerletilerek yapıldı; hızlandırmak için `Time.timeScale` geçici olarak 40'a çekildi. İkisi de yalnızca çalışma anı; **proje ayarı değişmedi**.

40× hızda çubuk hedefi `%0` görünüyordu — her karede bir dosya bittiği için. `timeScale` 1'e dönünce çubuk normal takip etti (ilerleme %8,8 → hedef %6,7, bir kare gecikme). Bu gecikme `TopHudView.Update`'in `DownloadController.Update`'ten önce çalışmasından kaynaklanıyor ve 60 fps'te 16 ms — görünmez.

### Bilinçli tasarım kararları

1. **Artan bit devreder.** Dosya bitince fazlalık sıfırlanmıyor, sonrakine yazılıyor. Yüksek hızda tek karede birden çok dosya bitebildiği için aksi halde kazanç kaybolurdu. Kare başına tamamlama **32** ile sınırlı; kalan sonraki kareye devrediyor, yani donma da kayıp da yok.
2. **Kademe değişince dosya yenilenir** — ama yalnızca mevcut dosya yeni kademede artık çıkmıyorsa. Kademe 8'den 0'a düşünce 3,8 Tbit'lik dosya asla bitmezdi.
3. **Yüzde ve kalan süre çubuğun ALTINDA**, üstünde değil. Dolgu renginin parlaklığını görmeden üstüne yazı koymak kontrast kumarı olurdu.
4. **Çarpan kancaları şimdiden var:** `SpeedMultiplier` (Faz 8 olayları) ve `RewardMultiplier` (Faz 4 premium, Faz 7 prestij) 1.0 varsayılanıyla duruyor — sonraki fazlar yalnızca değer yazacak.

### Senin Play testin

`Assets/Scenes/Game.unity` aç, **Play**:

| Ne yapılacak | Beklenen |
|---|---|
| Bekle | `ping_test.txt` yavaş doluyor (1 bps, ~76 sn) |
| Ekrana hızlı tıkla | Çubuk hızlanıyor, kalan süre düşüyor |
| Dosya bitince | Bakiye artıyor, yeni dosya adı+simgesi geliyor, çubuk sıfırlanıyor |
| `1`–`9` tuşları | Kademe değişiyor; dosya havuzu ve boyutlar o kademeye uyuyor |

Bakılacak: üst panelin **çentiğe girmemesi**, ilerleme çubuğunun dolgusunun **çerçevenin oyuğuna tam oturması** (taşmamalı, boşluk kalmamalı).

---

## 19. Faz 4 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

**Runtime — `Assets/Scripts/Core/`**
| Dosya | İş |
|---|---|
| `EconomyManager.cs` | Yükseltme seviyeleri, maliyet, kilit, satın alma ve etkilerin `SpeedController`/`DownloadController`'a uygulanması. |

**Runtime — `Assets/Scripts/UI/`**
| Dosya | İş |
|---|---|
| `UpgradeCardView.cs` | Tek kart: ad, açıklama, seviye, maliyet, kilit rozeti, satın alma butonu. |
| `UpgradeListView.cs` | Seçili sekmenin kartlarını üretir; bakiye/satın alma/kademe değişiminde tazeler. |
| `TabBarView.cs` | Altyapı · Donanım · Yazılım sekmeleri. |

**Üretilen varlık:** `Assets/Prefabs/UI/UpgradeCard.prefab`

### Etki formülleri (uygulanan)

```
Maliyet(n)              = baseCost × costGrowth^n
TıklamaGücü             = baseClickPower × (1 + Σ sv×etki[ClickPower]) × prestijÇarpanı
KritikŞans              = baseCritChance + Σ sv×etki[CritChance]
ExtraOverheatTolerance  = Σ sv×etki[OverheatTolerance]
ÖdülÇarpanı             = (1 + Σ sv×etki[RewardMultiplier]) × prestijÇarpanı
```

Bağlantı yükseltmeleri (`type == Connection`) `maxLevel 1`, `costGrowth 1` — tek seferlik. Satın alınınca `GameManager.SetTier(connectionTierIndex)` çağrılıyor, o da `TierChanged` yayınlıyor, o da yeni kilitleri açıyor.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (40+ referans, prefab dahil) | ✅ hepsi bağlı |
| Maliyet eğrisi | ✅ $4 → 4,60 → 5,29 → 6,0835 → 6,9960 — `1,15^n` ile **birebir** |
| Tıklama gücü etkisi | ✅ 5 seviye sonra 8 → **14 bps** (`8×(1+0,15×5)`) |
| Bağlantı satın alma | ✅ kademe 0 → 1, sonra tekrar alınamıyor (`IsMaxed`) |
| Kilit kapıları | ✅ 13 yükseltmenin `requiredTierIndex` durumu doğru |
| Fan etkisi | ✅ sv2 → `ExtraOverheatTolerance = 1,0 sn` |
| Kart butonu | ✅ `onClick` → gerçek satın alma (kademe 1 → 2, −$58) |
| Kart durumları | ✅ `MAKS` / `Sv 0 / 1` / `KADEME 2` + kilit rozeti + `interactable` |
| Sekme değişimi | ✅ 8 / 2 / 3 kart |
| Yerleşim | ✅ Content 1591 px Viewport 1591 px içinde — taşma yok; içerik 1714 px > 610 px, kaydırma çalışıyor |

### Kararlar ve gerekçeleri

1. **Alt panel raycast'i YAKALIYOR, üst HUD yakalamıyor.** Kaydırma ve kart tıklaması için alt panelin olayları alması şart. Sonuç: üst panele dokunmak hız kazandırır, alt panele dokunmak kazandırmaz — tıklama alanı kadranın olduğu orta bölge.
2. **Sekme sprite'ları tek durumda** geldiği için seçili sekme renk (%55 gri → beyaz) ve ölçekle (×1,12) ayrışıyor. Yeni görsel üretmeye gerek kalmadı.
3. **Buton durumları Unity'nin SpriteSwap geçişiyle** — `ui_button_normal/_pressed/_disabled` üçlüsü zaten elimizde, kod hiç sprite değiştirmiyor.
4. **Hata ayıklama paneli artık `F1` ile açılıp kapanıyor**, varsayılan kapalı. Alt panel o bölgeyi kapladığı için görünürlük `SetActive` yerine `CanvasGroup.alpha` ile yönetiliyor — panel kapalıyken de `1-9` kısayolları çalışsın diye.

### ⚠ GDD ile açık kalan bir nokta

GDD §6.3'teki `Taban Hız = BağlantıTürüBazHızı × (1 + 0,10 × PasifYükseltmeSeviyesi)` formülünün **karşılığı yok**: veri setindeki 13 yükseltmenin hiçbiri "pasif hız" tipinde değil (GDD §6.2 pasif olarak Bağlantı, Download Manager ve Premium Sunucu'yu sayıyor; ilki zaten `tier.baseSpeed`'i yükseltiyor, diğer ikisi offline ve ödül etkiliyor).

Uydurma bir çarpan eklemek denge kurmak değil, denge **icat etmek** olurdu. Bu yüzden `BaseSpeed = tier.baseSpeed` bırakıldı ve `GameConfig.passiveSpeedPerLevel` şimdilik kullanılmıyor. Idle geliri kademe verisinden geliyor.

Karar sende: ya (a) böyle kalsın — idle gelir kademeye bağlı, tıklama baskın; ya da (b) Faz 5'te "Pasif Bağlantı Yükseltmesi" diye yeni bir yükseltme tipi ekleyelim. Faz 5 zaten kademe geçişlerini ele alacağı için doğal yeri orası.

### Test yöntemi hakkında not

Kare işlenmediği için sekme değişiminde kart sayısı 16/18/21 göründü — `Destroy()` kare sonuna ertelendiği için silinmiş kartlar hiyerarşide duruyordu. Bir `EditorApplication.Step()` sonrası sayı **3**'e (Yazılım sekmesi) düştü. Gerçek çalışmada bu bir karelik durum.

### Senin Play testin

`Assets/Scenes/Game.unity` aç, **Play**:

| Ne yapılacak | Beklenen |
|---|---|
| Alt panele bak | Altyapı sekmesi açık, 8 bağlantı kartı, ilki alınabilir gerisi kilitli |
| Listeyi kaydır | Yumuşak kaydırma, kartlar çerçeveden taşmıyor |
| Tıklayarak $8.8 biriktir | "Telefon Hatti" kartı sönükten canlıya dönüyor (maliyet yazısı kırmızı → yeşil) |
| Karta bas | Kademe 1'e çıkıyor, kadran + oda değişiyor, kart `MAKS` oluyor, Harici Fan kilidi açılıyor |
| Donanım / Yazılım sekmeleri | 2 ve 3 kart geliyor |
| `Modeme Vurmak` al | Tıklama başına hız artışı gözle fark edilir olmalı |
| `F1` | Hata ayıklama paneli açılıp kapanıyor |

Bakılacak: kart metinlerinin **taşmaması** (uzun açıklamalar sağdaki maliyet bloğuna girmemeli) ve kilitli kartların yeterince **sönük** görünmesi.

---

## 20. Faz 5 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan / değişen dosyalar

| Dosya | İş |
|---|---|
| `Core/TierManager.cs` | **YENİ.** Kademe ilerlemesinin sahibi: `HighestTierReached`, `HasNextTier`, `NextConnectionUpgrade`, `DescribeRange`, geçiş penceresi, `TierAdvanced` olayı. |
| `Data/UpgradeSO.cs` | `UpgradeType.PassiveSpeed` eklendi. |
| `Core/SpeedController.cs` | `PassiveSpeedMultiplier` + `RecalculateBaseSpeed()`. |
| `Core/EconomyManager.cs` | Pasif hız çarpanını uyguluyor. |
| `UI/DialView.cs` | Kademe geçişi süzülmesi + **ibre açı kelepçesi**. |
| `UI/TopHudView.cs` | Kademe rozeti (simge + ad + aralık). |
| `Editor/DataAssetGenerator.cs` | `Upg_HatBakimi` tanımı. |

**Üretilen varlık:** `Upg_HatBakimi.asset` (14. yükseltme; mevcut 49 varlığa dokunulmadı)

### Pasif hız yükseltmesi — senin (b) seçimin

GDD §6.3'ün `TabanHız = kademeBazHızı × (1 + 0,10 × pasifSeviye)` formülünün artık gerçek bir karşılığı var:

| Alan | Değer |
|---|---|
| Ad | **Hat Bakımı** |
| Sekme | Altyapı |
| Maliyet | $25, ×1,15 |
| Maks seviye | 20 (tavanda ×3,0 taban hız) |
| Etki | seviye başına +%10 |
| Simge | `icon_upgrade_dns` (yeniden kullanım — yeni görsel üretilmedi) |

Altyapı sekmesine kondu çünkü o sekme aksi halde yalnızca kilitli bağlantı kartlarından oluşuyordu; arada alınabilecek bir şey yoktu.

**Kademe payı ile yükseltme payı ayrı tutuldu:**
`SpeedController` kademe payını (`tier.baseSpeed`) bilir, `EconomyManager` yalnızca çarpanı yazar. Böylece `TierChanged` dinleyicilerinin çalışma sırası önemsiz — hangisi önce koşarsa koşsun sonuç aynı.

### Kademe geçişinde ibre neden zıplıyordu

Her kademenin `minSpeed`'i bir öncekinin `maxSpeed`'ine **eşit** (0: 1–100, 1: 100–1.000, 2: 1.000–100.000 …). Yani kademe atlayınca aynı hız kadranın sağ ucundan sol ucuna taşınıyor — bu kasıtlı, yeni kademe daha geniş bir dünya demek. Ama `t` 1,0'dan 0,0'a düşünce ibre normal ataletle (0,05–0,22 sn) **180 dereceyi ~0,25 saniyede savuruyordu**.

**Çözüm:** `DialView` kademe değişiminde 1,2 sn'lik bir pencere açıyor, atalet `tierChangeInertia = 0,45`'e çıkıyor ve pencere boyunca normale doğru harmanlanıyor. İbre savrulmak yerine süzülüyor, sonunda da aniden "canlanmıyor".

### ⚠ Yol boyunca çıkan gerçek hata: ibre kadran yüzünü terk edebiliyordu

Aşırı `dt` ile test ederken ibrenin hedefine yaklaşmak yerine **ondan uzaklaştığı** görüldü (132° → 87° → 83° → 80°, hedef 148°).

Sebep: `_currentAngle` sınırsız bir `float` ve `SmoothDamp` ham açı uzayında çalışıyor. Büyük bir `dt`'de (kare takılması, GC duraklaması, sahne yüklemesi) hedefi aşıp 180°'yi geçiyor; `localEulerAngles` sarması yüzünden de **makul ama yanlış** bir açı olarak geri okunuyor. Yani hata sessizce yanlış görüntü üretiyordu.

**Çözüm:** süpürme sınırları config'ten türetilip (`AngleForT(0)` … `AngleForT(1)` → 0°…180°) `_currentAngle` her karede kelepçeleniyor. Test `dt`'si abartılıydı ama sebep gerçek: gerçek telefonda 0,5 sn'lik bir takılma aynı şeyi yapardı.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (45+ referans) | ✅ hepsi bağlı |
| 9 kademe yürüyüşü | ✅ hepsinde taban→`t=0,00`, tavan→`t=1,00` |
| Birim zinciri | ✅ bps → Kbps → Mbps → Gbps → Tbps (GDD §3 ile birebir) |
| Dosya havuzu kademeyi takip ediyor | ✅ `ping_test` → `odev_v2` → `avatar` → … → `galactic_census` |
| `HighestTierReached` | ✅ 8; kademe 8'de `HasNextTier == false` |
| Pasif hız formülü | ✅ sv 0–5 → ×1,00…×1,50 taban hız, beklenenle birebir |
| Çarpan kademe değişiminde korunuyor | ✅ kademe 4'te 1 Mbps → **1,5 Mbps** |
| İbre süzülmesi | ✅ geçiş penceresi açılıyor, ibre savrulmuyor |
| İbre kelepçesi | ✅ aşırı `dt` stresinde bile 0–180 aralığında kaldı |
| Play mode hataları | ✅ yok |

### Senin Play testin

`Assets/Scenes/Game.unity` aç, **Play**:

| Ne yapılacak | Beklenen |
|---|---|
| Üst panele bak | Kademe rozeti: simge + "Sinyal Kulubesi" + "1 bps - 100 bps" |
| Altyapı sekmesi | **Hat Bakımı** kartı ($25) bağlantı kartlarının yanında |
| Hat Bakımı al | Hiç tıklamadan da ibrenin dipten biraz yükseldiğini gör |
| $8.8 biriktir, Telefon Hattı al | Kadran + oda + rozet değişiyor, **ibre savrulmadan süzülerek** sola iniyor |
| `1`–`9` ile kademeleri gez | Birim bps→Kbps→Mbps→Gbps→Tbps ilerliyor, ibre her seferinde süzülüyor |

Asıl bakılacak: kademe atlarken **ibre zıplamıyor**, süzülüyor. Bir de kademe rozetindeki simge her kademede değişmeli (kademe 0'ın kendi simgesi yok — orada boş kalması normal).

---

## 21. Faz 6 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

| Dosya | İş |
|---|---|
| `Core/SaveData.cs` | **YENİ.** `SaveData` (JSON şeması) + `OfflineResult` (rapor verisi). |
| `Core/SaveManager.cs` | **YENİ.** Kayıt/yükleme, autosave, offline kazanç hesabı. |
| `UI/OfflineReportView.cs` | **YENİ.** "Tekrar hoş geldin" paneli. |
| `Editor/SaveTools.cs` | **YENİ.** Kaydı göster / sil / **saatini geri al**. |
| `Data/GameDatabaseSO.cs` | `FindUpgrade(ad)` · `FindFile(ad)` — kayıttaki adı varlığa çevirmek için. |
| `Core/DownloadController.cs` | `EstimateIdleIncomePerSecond()`. |

Kayıt yeri: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/SpeedDownload Idle Megabit Tycoon/save.json`

### Kayıt şeması (v1)

```json
{
  "saveVersion": 1,
  "lastSaveUtcTicks": 639213621939148388,
  "balance": 4996802.12,
  "lifetimeEarnings": 5000000.0,
  "currentTierIndex": 3,
  "highestTierReached": 3,
  "currentFileName": "File_EkranGoruntusu",
  "fileProgressBits": 694009.44,
  "completedCount": 0,
  "upgrades": [ { "name": "Upg_ModemeVurmak", "level": 7 }, ... ]
}
```

Yükseltmeler **asset adıyla** anahtarlanıyor, dizi indeksiyle değil — veri tabanına yeni yükseltme eklenince (Faz 5'teki Hat Bakımı gibi) eski kayıtlar bozulmuyor.

### Offline kazanç (GDD §6.3)

```
kazanç = boştaGelir/sn × min(geçenSüre, tavan) × verim
boştaGelir/sn = TabanHız × ağırlıklıOrtalama(ödül_i / boyut_i) × ödülÇarpanı
```

| Download Manager | Verim | Tavan |
|---|---|---|
| sv 0 (alınmadı) | — | offline kazanç **yok** (GDD §6.2) |
| sv 1 | %50 | 8 saat |
| sv 2 (Pro) | %100 | 24 saat |

### Doğrulama sonuçları

**Kapat-aç turu** — kademe 3, 6 yükseltme, $5M ile kaydedildi, Play kapatılıp yeniden açıldı:

| Alan | Kaydedilen | Yüklenen |
|---|---|---|
| kademe / en yüksek | 3 / 3 | ✅ 3 / 3 |
| bakiye / toplam kazanç | $5M / $5M | ✅ $5M / $5M |
| Modeme Vurmak | sv7 | ✅ sv7 |
| Hat Bakımı | sv3 | ✅ sv3 |
| Download Manager | sv1 | ✅ sv1 |
| Bağlantı 1/2/3 | 1/1/1 | ✅ 1/1/1 |
| tıklama gücü (türetilmiş) | 16,4 bps | ✅ 16,4 bps |
| taban hız (türetilmiş) | 130 Kbps | ✅ 130 Kbps |
| aktif dosya | `File_EkranGoruntusu` | ✅ aynı |

**Offline hesabı** (kademe 3, boşta gelir $2,99/sn):

| Senaryo | Geçen | Sayılan | Kazanç |
|---|---|---|---|
| sv1, 1 saat | 1 sa | 1 sa | $5.39K |
| sv1, 4 saat | 4 sa | 4 sa | $21.6K |
| sv1, 8 saat | 8 sa | 8 sa | $43.1K |
| sv1, **48 saat** | 2 g | **8 sa** (tavan) | $43.1K |
| **Pro**, 24 saat | 1 g | 1 g | $259K |
| **Pro**, 48 saat | 2 g | **1 g** (tavan) | $259K |
| **Gelecek tarihli kayıt** | <1 sn | <1 sn | **$0** |
| **sv0 (alınmadı)**, 48 saat | 2 g | — | **$0** |

Rapor paneli gerçekten açıldı: alfa 1,00, raycast engelliyor, `+$21.6K`, "4 sa yoktun."

### Kararlar ve gerekçeleri

1. **Önce geçici dosyaya yaz, sonra taşı.** Yazma sırasında uygulama ölürse eldeki kayıt bozulmasın. Autosave 15 sn'de bir çalıştığı için bu risk gerçek.
2. **`OnApplicationPause` de kaydediyor.** Mobilde "çıkış" çoğu zaman budur; `OnApplicationQuit` hiç gelmeyebilir.
3. **İleri sürümlü kayıt yoksayılıyor.** `saveVersion` bizimkinden büyükse okumaya çalışmak sessizce yanlış durum üretirdi.
4. **Yükleme sırası: yükseltmeler → kademe → para → dosya → offline.** Offline hesabı taban hıza ve ödül çarpanına bağlı; bunlar oturmadan hesaplanırsa eksik çıkardı.
5. **`SaveManager` çalışma sırası 100** (geride). `DownloadController.Start` rastgele bir dosya seçiyor; kayıt onun üzerine yazabilsin diye.
6. **Rapor yalnızca gerçekten para kazanıldıysa açılıyor.** Her açılışta "0 kazandın" demek bilgi değil, gürültü olurdu.

### Yol boyunca düzeltilen bir tuzak

`OfflineResult.WasCapped` yalnızca süreye bakıyordu. Download Manager alınmamışken `creditedSeconds` 0 kalıyor ve 48 saatlik bir aradan sonra bu **"tavan doldu"** gibi görünüyordu — oysa sebep tavan değil, yükseltmenin hiç olmaması. Panel o durumda zaten açılmadığı için görünür bir etkisi yoktu ama sonraki fazlar için hazır bir tuzaktı. `WasCapped` artık kazanç şartı da arıyor.

### Yeni menü komutları

```
Tools/SpeedDownload/Save/Show Save File            -> yolu + içeriği konsola yazar
Tools/SpeedDownload/Save/Delete Save File          -> onay ister, siler
Tools/SpeedDownload/Save/Rewind Save Clock 4 Hours -> offline testi icin saati geri alir
Tools/SpeedDownload/Save/Rewind Save Clock 48 Hours
```

Saat geri alma olmadan offline kazancı test etmek gerçekten saatlerce beklemeyi gerektirirdi.

### Senin Play testin

Test kaydını sildim, temiz başlıyorsun.

| Ne yapılacak | Beklenen |
|---|---|
| Play → biraz tıkla, yükseltme al → Play'i kapat | Sessizce kaydeder |
| Play'e tekrar bas | Bakiye, kademe, yükseltmeler yerinde |
| `Tools/SpeedDownload/Save/Show Save File` | JSON'u konsolda gör |
| Download Manager'ı al (kademe 3 gerekiyor, $2K) | — |
| Play'i kapat → `Rewind Save Clock 4 Hours` → Play | **"TEKRAR HOŞ GELDİN"** paneli + kazanç |
| `TAMAM`'a bas | Panel kayboluyor, oyun devam ediyor |
| `Rewind Save Clock 48 Hours` → Play | Panelde "tavan doldu" yazmalı, kazanç 8 saatlik |

Not: Download Manager kademe 3 şartlı olduğu için offline raporunu görmek oyunun ortalarına kadar ilerlemeyi gerektiriyor — bu kasıtlı (GDD §6.2 offline kazancı o yükseltmeye bağlıyor). Test için hızlıca oraya gitmek istersen `1`–`9` kısayollarıyla kademe atlayabilirsin.

---

## 22. Faz 7 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan / değişen dosyalar

| Dosya | İş |
|---|---|
| `Core/PrestigeManager.cs` | **YENİ.** Fiber Kredisi, kalıcı çarpan, Hat Değişimi. |
| `UI/PrestigePanelView.cs` | **YENİ.** 4. sekmedeki Hat Değişimi ekranı. |
| `Data/GameDatabaseSO.cs` | Sonsuz kademe / dosya / bağlantı yükseltmesi üretimi + ad çözümleme. |
| `UI/DialView.cs` | Kademe 9+ için HueShift materyali. |
| `UI/TabBarView.cs` | 4. sekme (Prestij) — liste ile prestij sayfası arasında geçiş. |
| `UI/UpgradeListView.cs` | Altyapı sekmesine üretilmiş bağlantı kartını ekliyor. |
| `Core/SaveData.cs` · `SaveManager.cs` | **Kayıt sürümü 2**: `fiberCredits`. |
| `Core/GameManager.cs` | `MaxTierIndex` — sonsuz kademe varken üst sınır açık. |

### Prestij formülleri (GDD §7)

```
kazanılacakKredi = floor( sqrt( toplamKazanç / 1e9 ) )
kalıcıÇarpan     = 1 + 0,02 × toplamFiberKredisi
```

Sıfırlanır: para, yükseltmeler, kademe, aktif dosya.
Korunur: **Fiber Kredisi, toplam yaşam boyu kazanç**.

Toplam kazancın korunması kasıtlı: bir sonraki prestijde kazanılacak kredi hep *toplam* kazanca baktığı için her tur bir öncekinin üzerine biner.

### Sonsuz kademeler (9+) — PLAN §2.8

Her kademe bir öncekinin **×100'ü**. Dosya boyutu ve ödülü aynı katsayıyla büyüdüğü için bir dosyanın tamamlanma süresi sabit kalıyor; yalnızca sayılar büyüyor.

| # | Ad | Aralık | Hue |
|---|---|---|---|
| 9 | Karadelik Yönlendirici | 100 Tbps – 10 Pbps | 0,382 |
| 10 | Solucan Deliği Hattı | 10 Pbps – 1 Ebps | 0,764 |
| 11 | Kuasar Omurgası | 1 Ebps – 100 Ebps | 0,146 |
| 12 | Nebula Anahtarı | 100 Ebps – 10 Zbps | 0,528 |
| 13 | Pulsar Rölesi | 10 Zbps – 1 Ybps | 0,910 |
| 14 | Galaktik Veriyolu | 1 Ybps – 100 Ybps | 0,292 |
| 15 | Entropi Kanalı | 100 Ybps – 1e28 bps | 0,674 |
| 16 | Kuantum Köpüğü Hattı | 1e28 – 1e30 bps | 0,056 |

Hue adımı altın açı (0,381966) olduğu için ardışık kademeler renkte olabildiğince uzak düşüyor — 8 kademe boyunca hiç tekrar yok. İsimler 8'lik listeden dönüyor, ikinci turda sonuna sayı ekleniyor.

**Yeni görsel üretilmedi:** kadran zemini `dial_tier8_datacenter_bg`'nin hue'su kaydırılmış hali, oda `bg_tier9_abstract_space`, bağlantı simgesi `icon_conn_datacenter`.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (55+ referans) | ✅ hepsi bağlı |
| Prestij kilidi | ✅ kademe 0'da kapalı, kademe 8'de açık |
| Kredi hesabı | ✅ $25B toplam → √25 = **5 kredi** |
| Çarpan | ✅ 1 + 0,02×5 = **1,10x** |
| Sıfırlama | ✅ para $0, yükseltmeler sv0, kademe 0 |
| Koruma | ✅ toplam kazanç $25B, Fiber Kredisi 5 |
| Çarpanın etkisi | ✅ tıklama gücü 8 → **8,8 bps** |
| Ekran kalıcı açık | ✅ sıfırlamadan sonra da (kredi > 0) |
| Sonsuz kademe üretimi | ✅ 9–16 doğru aralık, isim, hue |
| Aynı indeks = aynı nesne | ✅ önbellek çalışıyor |
| Kadran materyali | ✅ kademe 9/12'de HueShift, kademe 3/8'de varsayılan |
| Üretilmiş bağlantı yükseltmesi | ✅ kademe 9'da "Solucan Deliği Hattı" $60B, alınınca kademe 10 |
| Sekme geçişi | ✅ 4 sekme, prestij sayfası listeyi gizliyor |
| Kayıt turu (sonsuz kademeden) | ✅ kademe 10, FK 5, çarpan 1,1x, aktif dosya geri geldi |

### ⚠ Yol boyunca çıkan gerçek hata: üretilmiş varlıklar kayıttan geri gelmiyordu

Sonsuz kademe dosyaları (`File_Infinite10`) ve bağlantı yükseltmeleri (`Upg_InfiniteConn10`) çalışma anında üretiliyor, yani `files[]` / `upgrades[]` dizilerinde **yok**. `FindFile` / `FindUpgrade` yalnızca o dizilere baktığı için ikisi de `null` dönüyordu:

- **Dosya:** ilerleme sıfırlanıp yeni dosya seçiliyordu (sessiz ilerleme kaybı).
- **Yükseltme:** satın alınmış bağlantı seviyesi **kaybediliyordu** — oyuncu zaten bulunduğu kademenin bağlantısını tekrar satın alabilir, parayı boşa harcardı.

Düzeltme: iki arama da adın önekini çözüp (`File_Infinite`, `Upg_InfiniteConn`) ilgili üreticiye yönlendiriyor. Önekler artık üretim ve çözümleme tarafında **aynı sabitten** geliyor ki isimlendirme ikiye ayrılamasın.

### Kayıt sürümü 2

`fiberCredits` eklendi. v1 kayıtları sorunsuz yükleniyor (alan 0 gelir). İleri sürümlü kayıtlar hâlâ reddediliyor.

### Senin Play testin

Test kaydını sildim, temiz başlıyorsun. Prestije normal oyunla ulaşmak uzun sürer; kısayol:

| Ne yapılacak | Beklenen |
|---|---|
| `1`–`9` ile kademe 8'e çık | Prestij sekmesi (4.) anlamlı içerik gösterir |
| Prestij sekmesine bas | Fiber Kredisi, çarpan, "Şimdi değiştirsen: +N FK" |
| Toplam kazanç $1B'nin altındaysa | Buton "HENÜZ OLMAZ", kaç para gerektiği yazar |
| $1B+ topla → **HAT DEĞİŞTİR** | Her şey sıfırlanır, kredi kalır, çarpan artar |
| Tekrar kademe 8'e çık, bağlantı al | Kademe 9: **Karadelik Yönlendirici** |
| Kadrana bak | Aynı tier8 kadranı ama **rengi kaymış** |
| Altyapı sekmesi | "Solucan Deliği Hattı" kartı üretilmiş halde duruyor |

Asıl bakılacak: kademe 9+'da kadranın hue kaymış hali **okunabilir mi** — ibre ve tik işaretleri kaybolmamalı. Renk her kademede değiştiği için birkaç kademe ilerleyip kontrol etmekte fayda var.

---

## 23. Faz 8 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan / değişen dosyalar

| Dosya | İş |
|---|---|
| `Core/GameEventManager.cs` | **YENİ.** Zamanlayıcı, ağırlıklı seçim, 3 olay, çözme yolları. |
| `UI/EventBannerView.cs` | **YENİ.** Üstten kayan olay banneri. |
| `Core/SpeedController.cs` | `SpeedCeiling` — Kota olayı ibreyi tabanda tutuyor. |
| `Core/DownloadController.cs` | `EventRewardMultiplier` — kalıcı çarpandan ayrı. |
| `Core/SaveData.cs` · `SaveManager.cs` | **Kayıt sürümü 3**: aktif olay. |

### Üç olay (GDD §8)

| Olay | Etki | Çözüm | Süre |
|---|---|---|---|
| **Komşu Wi-Fi Çaldı** | hız ×0,6 | "Şifreyi Değiştir" **3 tık** | çözülene kadar |
| **Kota Aşıldı** | hız tavanı = taban hız (tıklamak işe yaramaz) | bedel öde (~30 sn'lik boşta gelir) veya ödüllü reklam | çözülene kadar |
| **Gece Tarifesi** | tüm kazançlar ×3 | — | 45 sn |

Aynı anda tek olay çalışır. Süreli olan kendiliğinden biter; engeller oyuncu çözene kadar durur. İlk 60 saniyede olay çıkmaz — oyuncu daha ne olduğunu anlamadan cezalandırılmasın.

### İki çarpan neden ayrıldı

`DownloadController` artık iki ayrı ödül çarpanı tutuyor:

- `RewardMultiplier` — **kalıcı**: Premium Sunucu + prestij. `EconomyManager` yazar.
- `EventRewardMultiplier` — **geçici**: Gece Tarifesi. `GameEventManager` yazar.

Tek alan olsaydı `EconomyManager.ApplyStats()` her çağrıldığında (yükseltme alımı, kademe değişimi) aktif Gece Tarifesi'ni sessizce siler, ya da tersi olurdu. Ödüle uygulanan `TotalRewardMultiplier` ikisinin çarpımı.

Offline hesabı bilerek yalnızca **kalıcı** çarpanı kullanıyor — oyuncu yokken Gece Tarifesi işlemiyor.

### Kota neden çarpan değil, tavan

"Hız taban seviyeye çekilir" bir çarpanla ifade edilemez (sonuç anlık hıza bağlı olurdu). `SpeedController.SpeedCeiling` eklendi; Kota bunu `BaseSpeed`'e çekiyor. Sonuç: ibre dibe iniyor ve **tıklamak hiçbir şey yapmıyor** — testte 200 tıklama hızı taban seviyede bıraktı. Tavan hem `Update`'te hem `RegisterClick`'te uygulanıyor.

### Aktif olay neden kaydediliyor

Engeller kayda yazılmasaydı oyuncu **uygulamayı kapatıp açarak cezadan kaçabilirdi**. Kayıt sürümü 3 ile olay türü, kalan süre, kalan tık ve kota bedeli saklanıyor; açılışta etki yeniden uygulanıyor.

Test: Wi-Fi olayında 1 tık yapıldı (kalan 2), kaydedildi, Play kapatılıp açıldı → olay **hâlâ aktif**, kalan tık **2**, hız çarpanı **0,6**.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (70+ referans) | ✅ hepsi bağlı |
| Wi-Fi: hız çarpanı | ✅ 0,6 uygulandı |
| Wi-Fi: 3 tıkla çözülme | ✅ 3 → 2 → 1 → 0, olay bitti, çarpan 1'e döndü |
| Kota: tavan | ✅ taban hıza çekildi |
| Kota: tıklama etkisiz | ✅ 200 tıklama sonrası hâlâ tabanda |
| Kota: bedel ödeme | ✅ $110 düştü, tavan sınırsıza döndü |
| Gece Tarifesi: ödül | ✅ 1 → **3x**, hız çarpanı etkilenmedi |
| Gece Tarifesi: süre | ✅ 45 sn, geri sayıyor |
| Banner metinleri | ✅ üç olayın da başlık/gövde/buton metni doğru |
| Banner görselleri | ✅ `event_quota_icon` · `event_neighbor_wifi_icon` · `event_happyhour_banner` |
| Tık sayacı butonda | ✅ "SIFREYI DEGISTIR (3)" → "(2)" |
| Gece Tarifesi'nde buton gizli | ✅ (çözülecek bir şey yok) |
| Olay kaydı | ✅ kapat-aç sonrası ceza ve tık ilerlemesi korundu |

### Yerleşim değişikliği

Banner kadranın **üstünde** duruyor (+430 .. +320). Redline yayını (saat 12 → 3) kapatmaması için kadran merkezi **+120'den +80'e** indi, alt panel 760 → 720 px oldu. Yeni dikey bütçe:

```
ust panel   +940 .. +440   (500)
olay banner +430 .. +320   (110, ustten kayarak girer)
kadran      +336 .. -176   (512, merkez +80)
alt panel   -220 .. -940   (720)
```

### Bilinçli bırakılan bir esneklik

Gece Tarifesi kayıttan **kalan süresiyle aynen** geri geliyor; aradan geçen gerçek süre düşülmüyor. Yani oyuncu tarifenin ortasında çıkıp ertesi gün dönse kalan 28 saniyeyi yine kullanır. Bunu kasıtlı bıraktım: kazanç aynı (45 sn'lik 3x zaten hakkıydı), yalnızca *ne zaman* kullanacağı değişiyor — buna karşılık oyuncunun tarifeyi haksızca kaybetmemesi daha önemli.

### Senin Play testin

Test kaydını sildim. Olaylar ilk 60 sn + 90–180 sn arayla kendiliğinden çıkar; beklemek istemezsen `F1` panelini açıp bekleyebilir ya da doğal akışta oynayabilirsin.

| Ne yapılacak | Beklenen |
|---|---|
| Oyna, olay bekle | Banner üstten kayarak girer |
| **Komşu Wi-Fi** çıkarsa | Hız gözle görülür düşer; butona 3 kez bas, sayaç 3→2→1, sonra banner kayboluyor |
| **Kota** çıkarsa | İbre dibe iniyor, **tıklamak işe yaramıyor**; bedeli ödeyince normale dönüyor |
| **Gece Tarifesi** çıkarsa | Banner zemini `event_happyhour_banner`, geri sayım işliyor, para 3 kat hızlı artıyor |
| Ceza sırasında Play'i kapat-aç | Ceza **devam ediyor**, tık ilerlemesi korunuyor |

Bakılacak: bannerın kadranın üst kısmını **kapatıp kapatmadığı** — redline yayının görünür kalması gerekiyor.

---

## 24. Faz 9 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

| Dosya | İş |
|---|---|
| `UI/FXManager.cs` | **YENİ.** 7 efektin havuzu, tetikleme ve animasyon. |
| `UI/MascotController.cs` | **YENİ.** Maskot ruh hali + boşta salınım. |
| `UI/MainMenuView.cs` | **YENİ.** `bg_mainmenu` + `logo_main` + BAŞLA perdesi. |
| `UI/SettingsView.cs` | **YENİ.** Ses aç/kapa, ana menüye dön. |

### FX havuzu — 37 örnek, 7 tür

| Efekt | Tetikleyici | Havuz |
|---|---|---|
| `fx_click_ripple` | her tıklama | 12 |
| `fx_critical_burst` | kritik patlama (DNS) | 4 |
| `fx_coin_burst` | dosya tamamlanma | 6 |
| `fx_turbo_flame` | redline'dayken 0,32 sn'de bir | 6 |
| `fx_overheat_flash` | aşırı ısınma | 2 |
| `fx_overheat_smoke` | aşırı ısınma | 3 |
| `fx_confetti` | prestij | 4 |

Havuz boyutları efektin ne sıklıkta üst üste binebileceğine göre: tıklama en sık, aşırı ısınma en seyrek.

**Turbo alevi** sürekli bir nesne değil, redline boyunca 0,32 sn'de bir havuzdan kısa parlama — titrek alev etkisi veriyor ve ayrı bir kod yolu gerektirmiyor.

### Tahsisatsız tasarım

- Tüm örnekler `Start`'ta üretilir; oyun sırasında `Instantiate` **yok**.
- Havuz **sabit boyutlu**; doluysa en eski örnek geri dönüştürülür. Büyüme yok → dizi yeniden tahsisi de yok.
- `Play()` yolunda `new` (referans tipi), kapanış (closure), LINQ, string işlemi ve kutulama yok. Yalnızca struct kopyaları (`Vector2/3`, `Color`, `Quaternion`, `FXDef`) ve dizi indeksleme.
- Durum, bileşen aramak yerine **paralel dizilerde** tutuluyor.
- Boştaki örneklerde `Image.enabled = false` → çizim maliyeti de yok.

### ⚠ Ölçüm hakkında dürüst not

Test ölçütü "GC allocation'sız oynuyor" idi. **Bayt düzeyinde ölçemedim**, çünkü:

- `GC.GetTotalMemory` bu Mono derlemesinde nursery'yi raporlamıyor. Kontrol denemem bunu kanıtladı: kasıtlı olarak 3500 kez `new float[4]` (~56 KB) yaptım, ölçüm **0 bayt** dedi. Ölçüm aleti ölü.
- Unity Profiler MCP aracı `ArgumentOutOfRangeException` ile hata verdi (yakalanmış kare olmadığı için).

Bu yüzden **ölçebildiğim ve asıl önemli olan değişmezi** doğruladım:

| Kontrol | Sonuç |
|---|---|
| 3500 `Play()` çağrısı (7 tür × 500) sonrası havuz boyutu | ✅ **37 → 37**, hiç büyümedi |
| `Play()` gövdesinde tahsisat yapan yapı | ✅ yok (okuyarak doğrulandı) |

Havuzun büyümemesi, tek gerçek tahsisat kaynağının (yeni `GameObject`) kapalı olduğunu gösteriyor. Bayt düzeyinde kesin rakam istersen Unity'nin kendi Profiler penceresini açıp Deep Profile ile bakman gerekir — bu ortamdan güvenilir sayı çıkaramıyorum. **Bunu doğrulanmış saymıyorum, doğrulanabilir saymıyorum.**

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (95+ referans) | ✅ hepsi bağlı |
| FX havuzu kurulumu | ✅ 37 örnek önceden üretildi |
| FX havuzu büyümesi | ✅ 3500 çağrıda sabit kaldı |
| Maskot başlangıç | ✅ `mascot_idle` |
| Ana menü açılışta | ✅ görünür, raycast engelliyor |
| BAŞLA | ✅ perdeyi kapatıyor |
| Ayarlar açılışta | ✅ gizli (alfa 0) |
| Dişli butonu | ✅ paneli açıyor |
| Ses aç/kapa | ✅ simge `sound_on` ↔ `sound_off`, `AudioListener.volume` 1 ↔ 0 |
| ANA MENÜ butonu | ✅ kaydediyor + menüyü geri açıyor |
| Play mode hataları | ✅ oyun kodundan yok |

### Kararlar

1. **Ayarlar `PlayerPrefs`'te, kayıt dosyasında değil.** Prestij veya kayıt silme ses tercihini sıfırlamamalı.
2. **Ana menü ayrı sahne değil, `CanvasGroup` perdesi** (PLAN §3.4). Oyun arkada zaten çalışıyor; BAŞLA bir yükleme değil, perdenin açılması.
3. **Maskot önceliği: aşırı ısınma > sevinç > boşta.** Overheat sırasında mutlu yüz göstermek yanlış geri bildirim olurdu.
4. **FX katmanında `blocksRaycasts = false`** — efektler asla tıklamayı yakalamamalı.
5. **Ayarlardan "ANA MENÜ"ye basınca kaydediliyor** — oyuncu oradan uygulamayı kapatabilir.

### Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Play | Ana menü perdesi: arka plan + logo + BAŞLA |
| BAŞLA | Perde yumuşakça açılıyor, oyun görünüyor |
| Ekrana tıkla | Her tıklamada halka efekti |
| Redline'a çık | Kadranın altında titrek turbo alevi |
| Tam gazda 2 sn kal | Aşırı ısınma flaşı + duman, **maskot surat asıyor** |
| Dosya bitir | Para efekti yukarı süzülüyor, **maskot 1 sn seviniyor** |
| Sağ üstteki dişli | Ayarlar açılıyor; ses simgesi basınca değişiyor |
| ANA MENÜ | Kaydedip menüye dönüyor |

Bakılacak: efektlerin **konumu** — tıklama halkası ve alev şu an sabit noktalarda (kadran merkezi / altı) çıkıyor, parmağın değdiği yerde değil. Dokunma konumunu efekte taşımak `ClickCatcher`'dan pozisyon geçirmeyi gerektiriyor; gerekli görürsen Faz 10'da eklerim.

---

## 25. Faz 10 — Tamamlanma Kaydı (2026-08-03)

### Oluşturulan dosyalar

| Dosya | İş |
|---|---|
| `Core/ProceduralAudio.cs` | **YENİ.** Kare dalga, sweep, arpej, döngülü testere sentezi. |
| `Core/AudioManager.cs` | **YENİ.** Klipleri bir kez üretir, oyun olaylarına bağlar. |
| `Core/IAdService.cs` | **YENİ.** Ödüllü reklam arayüzü. |
| `UI/StubAdService.cs` | **YENİ.** Sahte reklam paneli (3 sn geri sayım). |
| `Editor/PlayerSettingsTool.cs` | **YENİ.** Mobil ayarlar + uygulama simgesi + doğrulama. |

### Prosedürel ses (PLAN §2.5)

Ses varlığı yok ve proje kuralı gereği üretilmeyecek — ama chiptune estetiği zaten sentezle birebir örtüşüyor, dolayısıyla bu bir ödün değil.

| Ses | Sentez |
|---|---|
| Tıklama | 880 Hz kare dalga, 80 ms, hızlı sönüm |
| Kritik | 880-1174-1568 Hz arpej |
| Dosya bitişi | **Do-Mi-Sol** (523-659-784 Hz) arpej |
| Aşırı ısınma | 260→90 Hz alçalan kare dalga + %35 gürültü |
| Kademe atlama | 5 notalık yükselen arpej |
| Satın alma | 1318 Hz kısa bip |
| İbre uğultusu | **döngülü testere**, hıza göre pitch 0,6→2,4 |

Uğultu her kare yeni klip çalmak yerine **tek döngülü kaynağın modülasyonu** — hem ucuz hem kulağa sürekli geliyor. Döngü uzunluğu tam periyot katına yuvarlanıyor ki ek yerinde tıkırtı olmasın.

BGM yuvası hazır ve boş; gerçek klip verirsen tek alan ataması yeterli.

### Sahte reklam (PLAN §2.7)

`IAdService` arayüzü + `StubAdService`. Oyun yalnızca arayüze konuşuyor; gerçek SDK geldiğinde **tek sınıf değişir**, çağrı yerleri değişmez. `GameManager.Ads` somut sınıfı değil arayüzü arıyor.

Panel var olan sprite'larla: `ui_panel_bg` + `ui_icon_reward_ad` + `ui_icon_close` + geri sayım. Süre dolmadan kapatma **çalışmıyor** (ödüllü reklamın sözleşmesi bu), kapat simgesi o sırada soluk.

Kota olayının banner'ına ikinci bir yol olarak bağlandı (GDD §8: "ödeme veya ödüllü reklam").

### Mobil Player Settings

| Ayar | Değer | Neden |
|---|---|---|
| Yönelim | **Portrait kilitli** | GDD §12; ters portre de kapalı |
| Paket adı | `com.speeddownload.megabittycoon` | Android + iOS |
| Android minSdk | **26** | Unity 6'nın desteklediği en düşük seviye |
| Scripting backend | **IL2CPP** | Google Play 64-bit şartı |
| Mimari | **ARMv7 + ARM64** | ARM64 olmadan Play reddeder |
| iOS min | 12.0 | — |
| vSync / hedef fps | kapalı / 60 | Idle oyunda vSync gereksiz pil yakar |
| Simge | `logo_icon_appstore` (1024×1024) | Android 6, iOS 9 boyut yuvası dolduruldu |

Elle ayarlanan Player Settings zamanla kayar ve gerekçesi kaybolur; tek komutla uygulanabilir olması hem tekrarlanabilir hem de nedeni kodda yazılı.

### Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması (105+ referans) | ✅ hepsi bağlı |
| AudioSource kurulumu | ✅ 3 kaynak (oneShot + döngülü uğultu + müzik) |
| Sentez gerçekten ses üretiyor mu | ✅ 9261 örnek, tepe genlik 0,450, %100 sessiz değil |
| Uğultu döngüsü | ✅ 11000 örnek / 0,249 sn / 44100 Hz, loop açık |
| `IAdService` çözümleme | ✅ `StubAdService` bulundu, arayüz üzerinden |
| Reklam paneli açılış | ✅ alfa 0 → 1, raycast engelliyor |
| **Süre dolmadan kapatma** | ✅ **reddedildi** — panel açık kaldı, ödül verilmedi |
| Süre dolunca | ✅ "ODUL HAZIR", kapat simgesi %35 → %100 |
| Ödül → kota kapanması | ✅ kota `None`, hız tavanı sınırsıza döndü |
| Player Settings doğrulaması | ✅ Portrait · IL2CPP · ARMv7+ARM64 · simge atandı |

### Yol boyunca çıkan iki hata

1. **iOS 9 simge yuvası bekliyor, 1 verilmişti.** Unity "incorrect number of icons for iPhone: 9 expected while 1 were specified" dedi ve simgeyi **atamadı**. Düzeltme: `GetIconSizes` ile platformun beklediği sayı okunup dizi o kadar tekrarla dolduruluyor (Android 6, iOS 9).
2. **minSdk 24 Unity 6'da artık desteklenmiyor** — "will become an error on a next release" uyarısı geldi. 26'ya çekildi.

### ⚠ Android build senin makinende alınmalı

Faz 10'un test ölçütü "Android build alınıyor" idi. **Build'i ben alamadım**: Android build desteği (SDK/NDK/JDK) ve IL2CPP derleme zinciri bu ortamdan doğrulanamıyor, ayrıca ilk IL2CPP derlemesi uzun sürer. Ayarların doğruluğunu doğruladım, çıktının kendisini **doğrulanmış saymıyorum**.

Sende yapılacak:
```
File > Build Profiles > Android > Switch Platform
(Unity Hub'da Android Build Support + SDK/NDK + OpenJDK kurulu olmalı)
Build
```
Hata çıkarsa bana getir.

### Menü komutları (tam liste)

```
Tools/SpeedDownload/0. Create Shader Materials
Tools/SpeedDownload/1. Apply Sprite Import Settings
Tools/SpeedDownload/Verify Sprite Import Settings
Tools/SpeedDownload/2. Generate Data Assets
Tools/SpeedDownload/2b. Regenerate Data Assets (OVERWRITE)
Tools/SpeedDownload/3. Build Game Scene
Tools/SpeedDownload/4. Apply Mobile Player Settings
Tools/SpeedDownload/Verify Mobile Player Settings
Tools/SpeedDownload/Save/Show Save File
Tools/SpeedDownload/Save/Delete Save File
Tools/SpeedDownload/Save/Rewind Save Clock 4 Hours
Tools/SpeedDownload/Save/Rewind Save Clock 48 Hours
```

Sıfırdan kurulum sırası: **0 → 1 → 2 → 3 → 4**

### Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Play | Ana menü; BAŞLA'ya bas |
| Ekrana tıkla | Her tıklamada **bip** sesi |
| İbreyi yükselt | Uğultunun **perdesi yükseliyor** |
| Dosya bitir | Do-Mi-Sol jingle |
| Tam gazda 2 sn kal | Alçalan gürültülü aşırı ısınma sesi |
| Ayarlardan sesi kapat | Her şey susuyor |
| Kota olayı çıkınca | Banner'da para butonunun yanında **reklam simgesi** |
| Reklam simgesine bas | Panel açılıyor, 3 sn sayıyor, **erken kapatılamıyor** |
| Sayaç bitince kapat | Kota kalkıyor, hız normale dönüyor |

---

## 26. Ana Menü Kaldırıldı (2026-08-03)

Oyuncu oyuna **doğrudan girmeli** — BAŞLA perdesi kaldırıldı.

| Değişiklik | Detay |
|---|---|
| `UI/MainMenuView.cs` | **silindi** |
| `SceneBuilder.CreateMainMenu` | **silindi** (bg_mainmenu + logo_main + BAŞLA) |
| Ayarlardaki "ANA MENÜ" butonu | **silindi** — dönülecek perde kalmadı |
| `SettingsView` | `mainMenu` / `menuButton` alanları kaldırıldı |
| Kaydetme | "ANA MENÜ"de yapılıyordu; artık **ayarların KAPAT'ında** |

Ayarlar paneli artık: başlık · ses aç/kapa · kapat.

### Doğrulama

| Kontrol | Sonuç |
|---|---|
| `MainMenu` / `StartButton` / `MenuButton` sahnede | ✅ üçü de **yok** |
| Açılışta raycast engelleyen perde | ✅ **yok** |
| İlk karede tıklama çalışıyor mu | ✅ hız **1 → 9 bps** (ClickCatcher doğrudan yanıt verdi) |
| Ayarlar: dişli → aç | ✅ |
| Ayarlar: ses aç/kapa | ✅ simge + `AudioListener.volume` 1 ↔ 0 |
| Ayarlar: kapat → kaydet | ✅ kayıt dosyası yazıldı |
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması | ✅ tüm referanslar bağlı, eksik görsel yok |

### Kullanılmayan kalan iki sprite

`bg_mainmenu.png` ve `logo_main.png` artık hiçbir yerde kullanılmıyor. Silmedim (proje kuralı: görseller senin), ama istersen logoyu üst HUD'a veya açılış ekranına koyabiliriz.

---

## 27. Test Aracı ve Ortam Sınırı

`Editor/SoakTestTool.cs` eklendi — `Tools/SpeedDownload/Test/Run Soak Test (~60 s)`.

**Neden gerekti:** Unity penceresi odakta değilken Play mode **kare işlemiyor** (`Time.frameCount` 2'de takılı kalıyor). Bu, MCP üzerinden zamana bağlı hiçbir şeyin (indirme ilerlemesi, overheat sayacı, olay zamanlayıcısı, autosave) doğrudan test edilemeyeceği anlamına geliyor.

Araç `EditorApplication.update` üzerinden `QueuePlayerLoopUpdate()` çağırarak oyun döngüsünü pompalıyor.

**⚠ Ama güvenilir değil:** `QueuePlayerLoopUpdate` çağrıları birleştiriliyor. İki çalıştırmada 3600 istek → bir kez **36**, bir kez **2** gerçek kare üretti. Araç bu yüzden artık gerçek motor karesini sayıyor ve istenen karelerin dörtte birinden azı işlendiyse **"GEÇERSİZ TEST"** uyarısı veriyor — sessizce yeşil ışık yakmak yerine.

### Bu oturumda MCP ile gerçekten doğrulanabilenler

- Derleme, sahne kurulumu, tüm referans bağlantıları
- Doğrudan çağrılabilen her şey: tıklama → hız, satın alma, prestij, olay başlat/çöz, kayıt/yükleme, offline hesabı, reklam akışı
- Kare gerektirmeyen UI durumları (alfa, raycast, metin, simge)

### Doğrulanamayanlar — senin Play testine kalıyor

- İbrenin **görsel** akışkanlığı ve kademe geçişindeki süzülme
- Overheat'in 2 sn kesintisiz tam gazla tetiklenmesi
- Olayların 90–180 sn aralıkla kendiliğinden çıkması
- Efektlerin gerçek görünümü, maskot geçişleri
- Seslerin kulaktaki hâli
- Android build çıktısı

---

## 28. Faz 11-16 — Gelişim Ağacı Genişletmesi (Play Store Öncesi)

**Kaynak:** `Docs/UPGRADE_TREE.md` · **Assetler:** `Docs/ASSET_PROMPTS.md`
**Tarih:** 2026-08-14

### 28.0 Neden bu fazlar var

Faz 0-10 GDD'yi eksiksiz uyguladı — ama GDD'nin kendisinde gelişim ağacı yetersiz
tanımlanmıştı. Oyunda **6 gerçek yükseltme** var (kalan 8'i zorunlu bağlantı satın alması).
Oyuncu 3. kademede tüm içeriği görüyor ve kalan 6 kademe boyunca aynı 6 kartı satın alıyor.

Play Store'a çıkmadan önce kapatılması gereken açık bu. Ayrıntılı teşhis:
`UPGRADE_TREE.md` Bölüm 0.

### 28.1 Faz tablosu

| Faz | İçerik | Sprite bağımlılığı | Test kriteri |
|---|---|---|---|
| **11** ✅ | Isı göstergesi HUD'a | `ui_heat_gauge` + `ui_heat_fill` | **KOD TAMAM** — bkz. Bölüm 29 |
| **12** ✅ | Kilometre taşı sistemi | `ui_badge_milestone` | **KOD TAMAM** — bkz. Bölüm 29 |
| **13** ✅ | 13 yeni `UpgradeType` + kademe 2-5 kartları | Öncelik 1 (12 ikon) | **KOD TAMAM** — bkz. Bölüm 30 |
| **14** ✅ | Otomasyon (AutoClick, QueueAutomation) | Öncelik 2'den 3 ikon | **KOD TAMAM** — bkz. Bölüm 33 |
| **15** ✅ | Prestij yetenek ağacı + eşik kademe 6'ya | Öncelik 3 (7 varlık) | **KOD TAMAM** — bkz. Bölüm 34 |
| **16** ✅ | Paralel slotlar, torrent geliri, üst kademe kartları | Öncelik 2 kalanı | **KOD TAMAM** — bkz. Bölüm 35 |

**Faz 11 ve 12 sprite beklemeden başlayabilir** — 11 için geçici beyaz halka, 12 zaten
çoğunlukla kod.

### 28.2 Faz 11 — Isı Göstergesi (en yüksek öncelik)

**Sorun:** `SpeedController.OverheatProgress` hesaplanıyor ama yalnızca `DebugHud`'da
gösteriliyor (`DebugHud.cs:126`). Gerçek oyun HUD'ında yok. Oyuncu overheat'e ne kadar
yakın olduğunu **patlayana kadar göremiyor** — risk/ödül mekaniğinin tüm geri bildirimi
eksik.

**Kurulum:**
```
DialRoot
 └─ HeatGauge          512×512, kadranla aynı merkez, DialRoot'un ÜSTÜNDE
     ├─ Track          ui_heat_gauge   (statik çerçeve)
     └─ Fill           ui_heat_fill    Image Type = Filled
                                       Fill Method = Radial 360
                                       Fill Origin = Left, Clockwise = true
```

`fillAmount = SpeedController.OverheatProgress`, renk eşikleri:
`t < 0.5` yeşil · `0.5 ≤ t < 0.8` sarı · `t ≥ 0.8` kırmızı.

`SceneBuilder.cs`'e eklenir — sahneye elle ekleme (Bölüm 17 uyarısı geçerli).

### 28.3 Faz 12 — Kilometre Taşı Sistemi

`UpgradeSO`'ya iki alan:
```csharp
public int[] milestoneLevels = { 10, 25, 50, 100 };
public float milestoneMultiplier = 2f;
```

`EconomyManager.TotalEffect` içinde seviye başına etki, aşılan kilometre taşı sayısı kadar
`milestoneMultiplier` ile çarpılır. Kart üzerinde bir sonraki eşiğe ilerleme çubuğu.

**Neden bu kadar değerli:** 12 sınırsız yükseltme × 4 eşik = **48 yeni hedef anı**,
tek bir yeni sprite gerektirmeden. Şu an "Modeme Vurmak" 40. seviyede 39'dan farksız
hissettiriyor.

### 28.4 Faz 13 — Yeni Tipler ve Kartlar

`UpgradeType` enum'una 13 yeni değer (tam liste `UPGRADE_TREE.md` Bölüm 7).
`UpgradeTab` enum'una `Prestige = 3`.

Kartlar `DataAssetGenerator`'a eklenir — elle `.asset` kurma. Mevcut komut zaten
üstüne yazmıyor, yani yeni kartlar eklenip komut tekrar çalıştırılabilir:
```
Tools/SpeedDownload/2. Generate Data Assets
```

⚠ `EconomyManager.ApplyStats` şu an 6 tipi işliyor; 13 yeni tip için genişletilmeli.
Bazıları yalnızca yeni alan yazacak (`RedlineBonus` → `SpeedController`), bazıları yeni
davranış gerektiriyor (`ParallelSlots` → `DownloadController` çoklu dosya desteği).

### 28.5 Bilinen kod açıkları (bu fazlarda kapatılacak)

Tarama sırasında bulunan, gelişim ağacıyla ilgili gerçek sorunlar:

| Konu | Durum | Faz |
|---|---|---|
| `FileCollectionPanelView` **boş kabuk** — `unlockedCount` hep 0, grid hiç doldurulmuyor, bonus hep "+0%" | Özellik tanıtılmış ama işlevsiz | 16 |
| Isı göstergesi yalnızca `DebugHud`'da | Oyuncuya görünmüyor | 11 |
| `GameEventManager.ApplySpeedCeilingToBase` anlık `BaseSpeed`'i sabitliyor — kota aktifken bağlantı satın alınırsa yeni hız uygulanmıyor | Gerçek bug | 13 |
| İki ayrı ses sistemi aynı anda aktif (`Core.AudioManager` + `Audio.ProceduralAudioManager`), tıklamada iki klik sesi üst üste biniyor | Duyulabilir kalite sorunu | 11 |
| `HapticManager` — `LightTap`/`MediumImpact`/`HeavyImpact` üçü de birebir aynı `Handheld.Vibrate()` (~250 ms sabit). Her tıklamada çağrılıyor → clicker'da telefon sürekli titriyor | Pil + UX sorunu | 11 |
| `DialView.UpdateRedlineGlow` "screen shake" diye ibre pivotunu kaydırıyor — sarsılan ekran değil ibrenin kökü | Görsel hata | 11 |

---

## 29. Faz 11-12 — Tamamlanma Kaydı (2026-08-14)

### 29.0 Önce kapatılan iki kalite sorunu

| Sorun | Çözüm |
|---|---|
| **Her tıklamada titreşim** — `LightTap`/`MediumImpact`/`HeavyImpact` üçü de aynı `Handheld.Vibrate()` (~250 ms tam cihaz titreşimi), `FXManager` her tıklamada çağırıyordu | `HapticManager` yeniden yazıldı: API 26+ `VibrationEffect` ile gerçek kısa haptic (10/25/45 ms, genlik 60/130/210), 60 ms bekleme süresi, `PlayerPrefs` okuması önbelleğe alındı. Normal tıklamada titreşim **kaldırıldı** — yalnızca kritik vuruş ve overheat |
| **İki ses sistemi aynı anda** — `Core.AudioManager` ve `Audio.ProceduralAudioManager` sahnede birlikte, her tıklamada iki klip üst üste | `ProceduralAudioManager` **silindi**. 5 çağrı yeri temizlendi (`DialView`, `UpgradeCardView`, `TierUpModalView`, `OfflineEarningsModalView`, `FloatingEventController`). UI'nin sese ihtiyacı olduğu iki yer için `GameManager.Audio` → `AudioManager.PlayReward()` eklendi |

**Uyarı sesi yeniden tasarlandı.** Eski sistem redline'a *girince* bip çalıyordu; ama redline
kadranın sağ yarısının tamamı (t ≥ 0,50) olduğu için oyuncu zamanın çoğunu orada geçiriyor ve
bip sürekli ötüyordu. Yeni ölçüt redline değil **ısı sayacının doluluğu**: %50'yi geçince başlar,
sayaç doldukça aralık 0,55 sn'den 0,12 sn'ye iner ve pitch yükselir (Geiger sayacı hissi).

### 29.1 Faz 11 — Isı göstergesi

**Yeni dosya:** `Assets/Scripts/UI/HeatGaugeView.cs`
**Sahne:** `DialRoot` altına `HeatGauge` (Track + Fill), ölçek `DialSize × 1,10`

#### ⚠ Sprite ölçümü bir varsayımı çürüttü

Prompt "üst yarım halka" istiyordu ve kod `arcSpan = 0,5` (180°) varsayıyordu.
Alfa taraması bunun **yanlış** olduğunu gösterdi:

| Sprite | Yarıçap (normalize) | Açısal kapsam |
|---|---|---|
| `ui_heat_gauge` | 0,492 … 0,956 | **−138,2° … +138,7°** (277°) |
| `ui_heat_fill` | 0,522 … 0,901 | **−129,0° … +129,3°** (258°) |

Yani yay yarım halka değil, altta dar açıklığı olan bir **at nalı**. İyi haber: dolgunun
yarıçap bandı kanalın tam içine oturuyor, hizalama sorunsuz.

**Sonuç:** `Radial360` dolgusu 0…1 arasında sürülemiyor. Origin `Bottom` (saat 6) seçilip
doluluk **[0,142 … 0,859]** aralığına eşlendi:
```
180° -> -129°  =  51°  bos   -> 51/360  = 0,142
-129° -> +129° = 258,3°      -> biter     0,859
```
Bu iki sayı `HeatGaugeView.fillMin` / `fillMax` alanlarında ve sprite yeniden üretilirse
yeniden ölçülmeli.

Renk: `t<0,5` yeşil→sarı, `t≥0,5` sarı→kırmızı. Overheat sırasında tam kırmızı yanıp söner.
Boştayken gösterge tamamen saydam (`fadeInBelow = 0,04`).

### 29.2 Faz 12 — Kilometre taşı sistemi

**Veri:** `UpgradeSO.milestoneLevels = {10, 25, 50, 100}` + `milestoneMultiplier = 2`
ve `MilestonesReached` / `MilestoneMultiplierAt` / `NextMilestone`.

**Ekonomi:** `EconomyManager.TotalEffect` artık
`seviye × etkiPerSeviye × kilometreTasiCarpani`. Çarpan **tüm seviyelere** uygulanır —
eşiğe ulaşmak hissedilir bir sıçrama olsun diye.

**UI:** `UpgradeCardView`'a rozet + çarpan yazısı (`x2`, `x4`). Sınırsız yükseltmelerde
seviye metni artık bir sonraki eşiği gösteriyor: `Sv 7 ▸ 10`.

**Taşıma aracı:** `Tools/SpeedDownload/5. Migrate Upgrade Assets (Milestones)` (idempotent)
ve `Verify Upgrade Milestones`.

Doğrulanan durum:

| Yükseltme | maxLevel | Kilometre taşı |
|---|---|---|
| `Upg_ModemeVurmak` | ∞ | açık — sv10 **2x**, sv25 **4x**, sv50 **8x** |
| `Upg_PremiumSunucu` | ∞ | açık |
| `Upg_HatBakimi` | 20 | açık |
| `Upg_HariciFan` | 10 | açık (tam maks'ta 2x) |
| `Upg_DnsAyari` | 5 | **kapalı** — tavan ilk eşiğin altında |
| `Upg_DownloadManager` | 2 | **kapalı** — tavan ilk eşiğin altında |
| 8 bağlantı | 1 | **kapalı** — tek seferlik |

Ulaşılamayacak bir eşiği kartta göstermek, gelmeyecek bir hedef vaat etmek olurdu.

### 29.3 Assetler

32 yeni sprite `Assets/Resources/` altına konmuştu — orası Unity'nin özel klasörü ve
oradaki her şey referans edilmese bile build'e girer. Doğru yerlerine taşındılar:
`UpgradeIcons/` (27) ve `UI/` (5). Ölçüm: hepsi 512×512, gerçek alfa kanallı,
**macenta kalıntısı yok**.

`SpriteImportTool`'a dört yeni kural eklendi: `ui_heat_gauge` / `ui_heat_fill` /
`ui_node_frame` / `ui_card_file_choice` → 512 (varsayılan `ui_*` kuralı 256 verirdi ve
ısı göstergesinin yay kenarları bulanıklaşırdı). `ui_badge_milestone` ve tüm
`icon_upgrade_*` / `icon_prestige_*` → 256.

### 29.4 Doğrulama

| Kontrol | Sonuç |
|---|---|
| Derleme (Runtime + Editor) | ✅ 0 hata |
| `SceneBuilder` kendi doğrulaması | ✅ "Sahne kuruldu, eksik görsel yok" |
| `DialRoot.prefab` | ✅ `HeatGauge` + `HeatGaugeView` bağlı |
| `UpgradeCard.prefab` | ✅ `MilestoneBadge` + `MilestoneText` bağlı |
| Import ayarları | ✅ heat/node/card 512, badge+ikonlar 256, ASTC 6×6 |
| Kilometre taşı çarpanları | ✅ sv9=1x · sv10=2x · sv25=4x · sv50=8x |

### 29.5 Senin Play testin

`Assets/Scenes/Game.unity` aç, **Play**:

| Ne yapılacak | Beklenen |
|---|---|
| Hızlı tıklayıp ibreyi tepede tut | Kadranın etrafında halka beliriyor, yeşil→sarı→kırmızı doluyor |
| Isı %50'yi geçince | Bip sesi başlıyor, sayaç doldukça hızlanıyor |
| Overheat'e kadar bekle | Halka tam kırmızı yanıp sönüyor, ceza bitince sıfırlanıyor |
| Elini çek | Halka geri boşalıyor ve tamamen kayboluyor |
| Tıklarken telefonu hisset | Titreşim **yok** (yalnızca kritik vuruşta ve overheat'te) |
| Tek tıklama | **Tek** klik sesi (eskiden iki taneydi) |
| "Modeme Vurmak"ı 10 seviyeye çıkar | Kartta rozet + `x2` beliriyor, seviye yazısı `Sv 7 ▸ 10` şeklinde |

**Bakılacak en kritik iki şey:** ısı halkasının kadranla hizası (taşmamalı, boşluk kalmamalı)
ve dolumun soldan sağa simetrik ilerlemesi.

---

## 30. Faz 13 + Dil Düzeltmesi — Tamamlanma Kaydı (2026-08-14)

### 30.0 Dil hatası — sözlük vardı ama kullanılmıyordu

Beş dil destekleniyor ama arayüzün önemli bir kısmı **koda gömülü Türkçe** yazıyordu.
En kötüsü: bazılarının sözlükte karşılığı **zaten vardı**, sadece çağrılmıyordu.

| Yer | Önce | Sonra |
|---|---|---|
| `UpgradeCardView` | `"KADEME 3"`, `"MAKS"`, `"Sv 5"` | `tier_req` · `upgrade_max` · `upgrade_level` |
| `EventBannerView` | `"KOMSU WI-FI CALDI"`, `"KOTA ASILDI"`, `"GECE TARIFESI"` + aksiyon metinleri | 8 yeni anahtar, 5 dil |
| `OfflineReportView` | `"TEKRAR HOS GELDIN"` + gövde metni | 6 yeni anahtar, 5 dil |
| `StubAdService` | `"REKLAM · odul icin..."`, `"ODUL HAZIR"` | 3 yeni anahtar, 5 dil |

**Kök sebep ve önlemi:** her çağrı yeri
`Instance != null ? Instance.Get(k, f) : f` yazmak zorundaydı; bu tekrar yüzünden
geliştirici çoğu yerde kısayolu seçip metni koda gömmüş. `LocalizationManager`'a statik
`T(key, fallback)` ve `TF(key, fallback, args)` eklendi — artık tek satır.
`TF` bozuk bir `{0}` içeren çeviride oyunu düşürmeyip fallback'e dönüyor.

Ayrıca `EventBannerView` artık `LanguageChanged`'e abone: banner ekrandayken dil
değiştirilirse eski dilde donup kalmıyor.

### 30.1 Faz 13 — 13 yeni tip

`UpgradeType` 7'den **20'ye** çıktı. Numaralar kayıt dosyasına serileştiği için sabit.

| Tip | Etki | Durum |
|---|---|---|
| `OverheatPenaltyReduction` | Overheat cezası kısalır (taban 0,25 sn) | ✅ Faz 13 |
| `OverheatSoftFail` | Overheat'te taban hızın bir kısmı korunur | ✅ Faz 13 |
| `RedlineBonus` | Redline tepe çarpanı yükselir | ✅ Faz 13 |
| `RedlineThreshold` | Redline eşiği düşer (taban 0,10) | ✅ Faz 13 |
| `CompletionBonus` | Dosya başı ek ödül | ✅ Faz 13 |
| `FileSizeReduction` | Dosya boyutu küçülür (taban %10) | ✅ Faz 13 |
| `EventResistance` | Olumsuz olay şansı düşer | ✅ Faz 13 |
| `ParallelSlots` · `SeedIncome` · `VirusImmunity` · `QuotaBypass` | — | Faz 16 |
| `AutoClick` · `QueueAutomation` | — | Faz 14 |

`UpgradeTab.Prestige = 3` eklendi (Faz 15 için).

**Sınırlar bilinçli:** ceza sıfırlanamıyor, yumuşak düşüş 1'i aşamıyor, redline eşiği
0,10'un altına inemiyor, sıkıştırma %90'ı geçemiyor. Aksi halde bu yükseltmeler kendi
mekaniklerini yok ederdi.

**Olay direnci yalnızca ceza olaylarını seyreltiyor** — Gece Tarifesi bir ödül olduğu için
ağırlığına dokunulmadı. Yan etki hoş: yükseltme aldıkça ödül olaylarının payı kendiliğinden
artıyor.

### 30.2 Kartlar: 6 → 20

`DataAssetGenerator`'a 14 yeni kart eklendi. Üretim: **14 yeni asset, 50 korundu**.
`GameDatabase.upgrades` = **28 referans** (20 kart + 8 bağlantı), hepsinde ikon bağlı.

| Kademe | Kart sayısı | Kartlar |
|---|---|---|
| 0 | 2 | Hat Bakımı · Modeme Vurmak |
| 1 | 1 | Harici Fan |
| 2 | 4 | DNS Ayarı · Premium Sunucu · **Sinyal Yükselteci** · **Isı Macunu** |
| 3 | 4 | Download Manager · **Cat6 Kablo** · **Sıkıştırma** · **IXP** |
| 4 | 3 | **UPS** · **Yönlü Anten** · **Reklam Engelleyici** |
| 5 | 2 | **NVMe SSD** · **Sıvı Soğutma** |
| 6 | 2 | **Overclock Aracı** · **Omurga Peering** |
| 7 | 1 | **YZ Optimizer** |
| 8 | 1 | **Kuantum İşlemci** |

Kademe 1, 7 ve 8 hâlâ tek kartlı — otomasyon (Faz 14) ve paralel slot/torrent (Faz 16)
oraya gelecek.

`Upg_HatBakimi` artık kendi ikonunu kullanıyor (`icon_upgrade_linemaintenance`); daha önce
DNS kartının ikonunu paylaşıyordu.

Yeni kartların **İngilizce isim ve açıklamaları** sözlüğe eklendi — 30.0'daki hatanın
tekrarlanmaması için.

### 30.3 Kilometre taşı dağılımı (otomatik)

Taşıma aracı tavanı ilk eşiğin (10) altında kalan kartlarda sistemi kapatıyor:

**Açık (9):** Modeme Vurmak · Premium Sunucu · Hat Bakımı (20) · Harici Fan (10) ·
Sinyal Yükselteci (15) · NVMe SSD (10) · Kuantum İşlemci (∞) · Omurga Peering (∞) ·
YZ Optimizer (∞)

**Kapalı (11):** tavanı 10'un altında olanlar + 8 bağlantı.

### 30.4 Doğrulama

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata |
| Üretim | ✅ 14 yeni / 50 korundu |
| `GameDatabase.upgrades` | ✅ 28 referans |
| İkon bağlantıları | ✅ hepsi bağlı, eksik sprite yok |
| Kademe kilitleri | ✅ tabloya birebir uyuyor |
| Kilometre taşı mantığı | ✅ tavan < 10 olanlarda kapalı |

### 30.5 Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Dili İngilizce yap | Kartlarda `Lv 5`, `MAX`, `Req. Tier: 3` — **Türkçe kelime kalmamalı** |
| Olay çıkmasını bekle | Banner seçili dilde |
| Kademe 2'ye çık | Donanım'da Sinyal Yükselteci + Isı Macunu beliriyor |
| Kademe 3-4'e çık | Her kademede en az 2 yeni kart |
| Isı Macunu al, overheat ol | Ceza gözle görülür kısalıyor |
| UPS al, overheat ol | İndirme tamamen durmuyor, yavaşlıyor |
| Sıkıştırma al | Dosya boyutu küçülüyor, dosyalar daha hızlı bitiyor |

---

## 31. Oynanış Cilası — Tamamlanma Kaydı (2026-08-14)

Faz planının dışında, oyunun "yetersiz hissettirmesine" doğrudan katkı veren altı eksik.
Hiçbiri yeni sprite gerektirmedi; hepsi mevcut sistemlere bağlandı.

### 31.1 Gelir hızı göstergesi ($/sn)

Bir idle oyunun en temel geri bildirimi eksikti: oyuncu bir yükseltme aldığında işe yarayıp
yaramadığını göremiyordu. İlginç olan, hesabın **zaten yazılı** olmasıydı —
`EstimateIdleIncomePerSecond` offline kazanç için vardı.

Ortak `AverageRewardPerBit()` çıkarıldı; üzerine `CurrentIncomePerSecond` eklendi. Farkı:
taban hız yerine **fiili** hızı (redline ve olay çarpanları dahil) ve geçici ödül
çarpanlarını da sayar. Yani tıkladıkça değer yükseliyor.

Üst HUD'da bakiyenin sağında, daha küçük fontla (ikincil bilgi hiyerarşisi). Satır 1
(28–100 px) ile satır 2 (112–170 px) arasında yeni bir satıra yer yoktu.

### 31.2 Kadranda hedef bölge işareti

Redline çarpanı tepede en yükseğe çıkıyor ama t=0,995'te overheat başlıyor — yani en verimli
oyun ikisinin arasında. Bu bilgi mekanikte vardı, **tamamen görünmezdi**.

`DialRoot` altına ibreninkiyle aynı pivot mantığında bir işaret (`sweetSpotT = 0,90`).
İbre işarete `±0,06` yaklaşınca yeşile dönüyor. Sprite yok: sprite'sız bir `Image` düz
dörtgen çiziyor, renk koddan geliyor.

### 31.3 Toplu satın alma (x1 / x10 / MAKS)

Sınırsız yükseltmeleri elli kez tıklamak eziyetti. Maliyet geometrik dizi olduğu için
kapalı formülle çözüldü, döngü kurulmadı:

```
toplam = baseCost × r^n × (r^k − 1) / (r − 1)        (r ≠ 1)
k      = log_r(1 + bütçe × (r−1) / ilkMaliyet)       (MAKS için)
```

Kayan nokta hatası bir fazla gösterebildiği için sonuç doğrulanıp kırpılıyor.
Bağlantılar tek seferlik olduğundan toplu alımdan muaf.

Mod `UpgradeCardView.BulkMode` üzerinde paylaşılıyor; sekme çubuğunun altındaki tek düğme
tüm kartları etkiliyor. Toplu moddayken kart **gerçekte ödenecek tutarı** gösteriyor —
birim fiyat göstermek yanıltıcı olurdu.

### 31.4 Kartta birikmiş etki

Başlıktaki etiket seviye **başına** kazancı gösteriyordu; oyuncunun göremediği şey birikmiş
toplamdı. Özellikle kilometre taşı aşıldığında (etki ikiye katlanır) bunun görünmesi gerek.
Açıklama satırı `Setup`'tan `Refresh`'e taşındı ki seviye değiştikçe güncellensin.

### 31.5 Yüzen ödül yazısı

`FXManager` dosya bitince para efekti patlatıyordu ama **rakam yoktu** — "ne kazandım?"
cevapsız kalıyordu. Sprite havuzuyla aynı desende bir metin havuzu eklendi. Para biçimlemesi
kaçınılmaz olarak string üretir; dosya tamamlama seyrek olduğu için kabul edilebilir
(tıklama yolunda olsaydı olmazdı).

### 31.6 İlk dokunuş ipucu

Oyun ana menüsüz, doğrudan kadranla başlıyor. Bir mobil oyuncu ne yapacağını birkaç saniyede
anlamazsa uygulamayı kapatır. Üç tıklamaya kadar nabız gibi atan bir ipucu; sonra kalıcı
olarak kayboluyor.

Durum `PlayerPrefs`'te, kayıt dosyasında **değil** — prestij veya "kaydı sil" bunu
sıfırlamamalı, oyuncu oynamayı zaten öğrendi. `blocksRaycasts = false`, yani tıklama
altındaki `ClickCatcher`'a geçiyor.

### 31.7 Yeni çeviri anahtarları

`upgrade_current` · `bulk_max` · `unit_second` · `onboarding_tap` — beş dilde.

### 31.8 Doğrulama

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| `SceneBuilder` doğrulaması | ✅ "Sahne kuruldu, eksik görsel yok" |
| Sahnede yeni bileşenler | ✅ `BulkBuyToggleView` · `OnboardingHintView` · `IncomeText` |
| `DialRoot.prefab` | ✅ `SweetSpotPivot` + `SweetSpotMark` |

### 31.9 Bilinen ufak eksik

Prestij sekmesine geçildiğinde toplu alım düğmesi görünür kalıyor (orada kart yok, işlevsiz).
Zararsız ama `TabBarView`'a bir gizleme satırı eklenebilir.

### 31.10 Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Oyunu ilk kez aç | Kadranın üstünde "HIZLANMAK İÇİN DOKUN", 3 tıklamada kayboluyor |
| Tıkla | Bakiyenin sağındaki $/sn değeri yükseliyor |
| İbreyi yavaşça yukarı taşı | Kadran kenarındaki işaret ibre üzerine gelince yeşile dönüyor |
| Dosya bitir | "+$X" yazısı yukarı süzülüyor |
| x10'a bas, bir yükseltme al | Tek dokunuşta 10 seviye, maliyet toplam tutarı gösteriyor |
| MAKS'a bas | Bütçenin izin verdiği kadarını alıyor, bakiye eksiye düşmüyor |

---

## 32. Arka Plan Müziği — Prosedürel (2026-08-14)

GDD §10 "arka plan müziği kademeyle birlikte evrilir" diyordu ama `AudioManager`'daki
BGM yuvası boştu; oyunda yalnızca SFX vardı. Sessiz bir mobil oyun ilk saniyelerde "ucuz"
hissettirir ve bu doğrudan mağaza yorumuna döner.

**Karar: sentez.** Hazır klip 9 ayrı parça + APK'da birkaç MB demekti; sentez **0 byte**
ekliyor, telif riski taşımıyor ve tema (retro/chiptune) zaten bu estetiğin kendisi.

### 32.1 Üretim

`Assets/Scripts/Core/ProceduralMusic.cs` — bas + arpej + ritim aynı tampona toplanıyor.

- **Akor ilerlemesi:** A minör `i – VI – III – VII`, her akor bir bar, 4 barda tekrar.
  Döngü 8 bar, yani ilerleme iki tur dönüyor.
- **Bas:** kök notanın iki oktav altı, üçgen dalga, 1. ve 3. vuruş uzun (yürüyen bas).
- **Arpej:** akor notalarını yukarı-aşağı dolaşıyor — düz yukarı diziden daha az yorucu.
  Vuruş başındaki notalar biraz daha güçlü, böylece ritim hissi doğuyor.
- **Ritim:** kick (frekansı hızla düşen sinüs) 1. ve 3. vuruşta, hi-hat (kısa gürültü) 1/8'de.
- Her notada attack/release zarfı var; olmazsa dalga sıfırdan başlamayıp hoparlörde
  "klik" duyuluyor. Döngü uzunluğu tam bar katına yuvarlanıyor.
- Katmanlar üst üste binince tavanı aşabildiği için kırpma yerine **tepe normalizasyonu**
  uygulanıyor — kırpma distorsiyon olarak duyulurdu.

### 32.2 Kademeye göre evrilme

| Grup | Kademe | BPM | Arpej | Ritim | Dalga |
|---|---|---|---|---|---|
| 0 | 0–1 | 78 | 1/8 | yok | kare |
| 1 | 2–3 | 92 | 1/8 | var | kare |
| 2 | 4–5 | 104 | 1/16 | var | kare |
| 3 | 6–7 | 118 | 1/16 | var | testere |
| 4 | 8+ | 130 | 1/16 | var | testere |

Kademe 0-1 bilinçli olarak yalnız ve seyrek (sinyal kulübesi hissi); üst kademelerde tempo,
yoğunluk ve parlaklık artıyor.

**Müzik kademe GRUBUNA göre değişiyor**, her kademede değil — her seferinde yeniden
sentezlemek hem gereksiz hem de duyulabilir bir kesinti olurdu.

### 32.3 Bellek ve geçiş

Bir döngü 44,1 kHz'de birkaç MB tutuyor; dokuz kademeyi birden bellekte tutmak anlamsız.
**Tek klip tutuluyor**, kademe grubu değişince yenisi üretilip eskisi `Destroy` ediliyor
(çalışma anında üretilen klipler asset değil, elle bırakılmaları gerekiyor).

Geçiş çapraz: önce kıs, değiştir, sonra aç (`musicCrossfade` varsayılan 1,2 sn).

### 32.4 Gerçek klip eklenirse

`AudioManager.backgroundMusic` yuvasına bir klip atanırsa **o** çalınır ve prosedürel üretim
tamamen devre dışı kalır. Yani bu karar geri döndürülebilir: kod değişmeden gerçek müziğe
geçilebilir.

### 32.5 Dinleme aracı

`Tools/SpeedDownload/6. Export Music Preview (WAV)` — beş grubun döngüsünü proje kökündeki
`MusicPreview/` klasörüne 16-bit PCM WAV olarak yazar. Klasör **Assets dışında**, yani
Unity import etmiyor ve APK'ya girmiyor.

Ölçülen çıktı:

| Dosya | Süre | BPM | Tepe | RMS |
|---|---|---|---|---|
| `grup0_sinyal_telefon` | 24,6 sn | 78 | 0,42 | 0,167 |
| `grup1_dialup_isdn` | 20,9 sn | 92 | 0,50 | 0,146 |
| `grup2_adsl_vdsl` | 18,5 sn | 104 | 0,50 | 0,132 |
| `grup3_fiber_kuantum` | 16,3 sn | 118 | 0,50 | 0,109 |
| `grup4_veri_merkezi` | 14,8 sn | 130 | 0,55 | 0,120 |

Tepe 0,42–0,55: kırpma yok. RMS 0,11–0,17: sessiz değil.

### 32.6 Bilinen sınırlar

- **Döngü 8 bar.** Uzun oturumlarda tekrar hissedilebilir; bar sayısı `Settings.bars` ile
  artırılabilir (bellek ve üretim süresi karşılığında).
- **Üst kademelerde RMS biraz düşüyor** (0,167 → 0,109): yoğun arpej tepeyi yükseltip
  ortalama enerjiyi düşürüyor. Fark edilirse gruplara özel `masterVolume` ile dengelenebilir.
- Sentez bir bestecinin yazdığı kadar zengin olmaz; ortamı taşıyan bir katman, akılda
  kalan bir tema değil.

---

## 33. Faz 14 — Otomasyon (2026-08-14)

Bir idle oyunun en tatmin edici eşiği "artık bunu elle yapmıyorum" anıdır. Oyuncu 10. saatte
de 1. saatteki gibi tıklıyordu.

### 33.1 Otomatik tıklama

`SpeedController.AutoClicksPerSecond` — `EconomyManager` yazar.

**Sessiz tıklama.** `RegisterClick`'e `silent` parametresi eklendi. Otomatik tıklamada hız
artışı ve kritik şansı aynen işler ama **hiçbir olay yayılmaz**. Aksi halde saniyede birkaç
kez klik sesi çalar ve ekranda dalga efekti patlardı — otomasyon oyuncuyu rahatlatmak yerine
boğardı. Geri bildirim zaten ibrenin kendiliğinden yükselmesi.

**Kesirli birikim.** 2,5 tık/sn'de bir karede 0,04 tık oluşur; yuvarlamak otomasyonu tamamen
yok ederdi. Birikimli sayaç kullanılıyor. Kare atlamalarında biriken onlarca tıkın tek karede
patlamaması için kare başına 8 tık sınırı, taşan kısım 4'te kırpılıyor.

Overheat sırasında otomasyon duruyor (mevcut erken çıkış zaten kapsıyor).

### 33.2 Otomatik kuyruk

Mevcut sistemde dosya zaten otomatik seçiliyordu (rastgele) — yani "otomatik seçer" tek
başına bir vaat değildi. Yükseltmenin anlamı **en kârlıyı seçmek** olarak tanımlandı:

| Seviye | Davranış |
|---|---|
| 0 | Rastgele (varsayılan) |
| 1 | En kârlı dosya, ama art arda aynı gelmez |
| 2 | Her zaman en kârlı dosya |

`GameDatabaseSO.PickBestFile` kriteri **ödül / boyut** oranı: indirme süresi boyutla doğru
orantılı olduğu için bu oran doğrudan "saniyede kazanılan para" demek.

### 33.3 Kartlar (+3, toplam 23 gerçek yükseltme)

| Kart | Sekme | Kademe | Maks | Etki |
|---|---|---|---|---|
| **Mekanik Klavye** | Donanım | **5** | 8 | +0,5 tık/sn |
| **Otomatik Kuyruk** | Yazılım | 6 | 2 | seviye kademesi |
| **Makro Script** | Yazılım | 7 | 6 | +0,8 tık/sn |

Tam yükseltmede **8,8 tık/sn** otomasyon.

**Kademe 5'e çekildi** (UPGRADE_TREE'de 6 yazıyordu): otomasyon anını kademe 6'ya bırakmak
oyuncuyu gereğinden uzun süre tıklatıyordu.

**Sadeleştirme:** UPGRADE_TREE'de Makro Script "otomatik tıklama hızı +%40" diyordu. Aynı
`TotalEffect` havuzunda toplanan iki farklı birim (adet ve yüzde) tutarsız sonuç verirdi;
ikisi de tık/sn olarak tanımlandı.

### 33.4 Kartta birim düzeltmesi

`FormatTotalEffect` `AutoClick`'i oran sanıp "+%400" gösterirdi. Artık "+4 tık/sn".
`QueueAutomation` seviye kademesi olduğu için sayı göstermiyor (yanıltırdı).
Yeni çeviri anahtarı `unit_clicks_per_sec`, beş dilde.

### 33.5 İkon paylaşımı

`icon_upgrade_macro` artık Makro Script'in; YZ Optimizer `icon_upgrade_cpu`'ya taşındı ve
Kuantum İşlemci ile paylaşıyor. İkisi de "işlemci / zeka" temasında, farklı sekmelerde ve
farklı kademelerde olduğu için yan yana görünmüyorlar. **Yeni sprite gerekirse tek eksik bu.**

### 33.6 Güncel kademe dağılımı

```
t0: 2   t1: 1   t2: 4   t3: 4   t4: 3   t5: 3   t6: 3   t7: 2   t8: 1
```

23 gerçek yükseltme + 8 bağlantı = **31 kart**. Kademe 1 ve 8 hâlâ tek kartlı; Faz 16
(paralel slot, torrent, antivirüs, VPN) oraya gelecek.

### 33.7 Doğrulama

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ 0 hata, 0 uyarı |
| Üretim | ✅ 3 yeni / 64 korundu |
| Kart yapılandırması | ✅ kademe/tavan/etki/ikon tabloya uygun |
| Kilometre taşı | ✅ üçünde de kapalı (tavan < 10) |

### 33.8 Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Kademe 5'e çık, Mekanik Klavye al | Elini çektiğinde ibre kendiliğinden yükseliyor |
| Otomasyon çalışırken dinle | **Klik sesi yok**, ekranda dalga efekti yok |
| Birkaç seviye daha al | İbrenin dinlenme yüksekliği artıyor |
| Kademe 6'da Otomatik Kuyruk al | Hep aynı yüksek kârlı dosya tipi geliyor |
| Overheat ol | Otomasyon ceza boyunca duruyor, sonra devam ediyor |

En kritik kontrol: otomasyon açıkken **sesin rahatsız edici olmaması**. Sessiz tıklama
tam olarak bunun için var.

---

## 34. Faz 15 — Prestij Yetenek Ağacı (2026-08-14)

Prestijin tek karşılığı kredi başına +%2 gelirdi — sayı çarpanı tek başına kimseyi ikinci
tura sokmaz. Fiber Kredisi artık **harcanabilir** bir kaynak.

### 34.1 Önce bir dil hatası kapatıldı

Prestij panelinde beş çeviri anahtarı **sözlükte hiç yoktu**: `prestige_boost_label`,
`prestige_pending_prefix`, `prestige_locked_info`, `prestige_threshold_info`,
`prestige_ready_info`. Hepsi İngilizce fallback'e düşüyordu — Türkçe oynayan biri
"Format now: +0 FP" görüyordu. Ayrıca "FP" birimi koda gömülüydü.

Beş anahtar + `prestige_unit` beş dile eklendi (TR "FK", ES "PF", RU "ОБ").

### 34.2 Fiber Kredisi ekonomisi

Ayrı bir yönetici kurmak yerine **aynı kart/seviye altyapısı** kullanılıyor; yalnızca
ödeme yolu farklı: `tab == UpgradeTab.Prestige` olan kartlar parayla değil FK ile alınır
(`EconomyManager.UsesFiberCredits`). Kart üzerinde maliyet para biçimleyicisiyle değil
"3 FK" olarak gösteriliyor.

**Prestijde korunma:** `ResetUpgrades` artık prestij yeteneklerini elemiyor. FK ile alınan
bir şeyin her turda yeniden satın alınması sistemin amacını bozardı.

### 34.3 Altı yetenek

| Yetenek | Tip | FK | Maks | Etki |
|---|---|---|---|---|
| **Hızlı Başlangıç** | `StartingTier` | 3 | 3 | Prestij sonrası +1 kademeden başla |
| **Kalıcı Güç** | `ClickPower` | 4 | 5 | +%20 tıklama gücü |
| **Offline Ustası** | `OfflineEfficiency` | 4 | 4 | +%10 offline verimi |
| **Kredi Faizi** | `PrestigeCreditBonus` | 6 | 5 | +%20 kazanılan FK |
| **Soğuk Başlangıç** | `OverheatTolerance` | 5 | 3 | +1 sn redline süresi |
| **Kritik Ustası** | `CritChance` | 5 | 4 | +%3 kritik şansı |

Üçü mevcut tipleri yeniden kullanıyor (ClickPower, OverheatTolerance, CritChance) — yani
`TotalEffect` üzerinden hiç ek kod olmadan çalışıyorlar. Üçü için yeni tip eklendi
(20, 21, 22).

Maliyet yine `1,15^n` ile büyüyor: üst seviyeler birkaç Hat Değişimi biriktirmeyi
gerektiriyor. FK kıt bir kaynak olmalı.

**UPGRADE_TREE'den sapma:** "Kalıcı Arşiv" koleksiyon sistemine bağlıydı, o hâlâ boş kabuk
(Faz 16). Yerine **Kalıcı Güç** kondu — aynı maliyet bandı, mevcut sisteme oturuyor.

### 34.4 Offline verimi tavanı

`SaveManager`'da yetenek verime ekleniyor ama **tavan 1,0**: offline kazanç aktif oynamayı
geçemez, yoksa oyunu kapatmak en iyi strateji olurdu.

### 34.5 Prestij eşiği 8 → 6

Oyuncu döngüyü bir kere görmeden bırakırsa prestij sistemi hiç var olmamış demektir.
`GameConfigSO` varsayılanı değişti; mevcut `GameConfig.asset` için taşıma aracına idempotent
bir adım eklendi (`prestigeUnlockTier: 8 → 6` doğrulandı).

### 34.6 Prestij sekmesi düzeni

Sekme artık **hem** Hat Değişimi panelini **hem** yetenek kartlarını gösteriyor:
panel üstte sabit 340 px, kartlar altta kaydırmalı. `TabBarView` liste alanının üst
kenarını sekmeye göre kaydırıyor. Açıklama metni 200 → 96 px'e indirildi ki panel sığsın.

Toplu alım düğmesi prestij sekmesinde gizleniyor — FK ile alınan kartlarda anlamsız.

### 34.7 Yol boyunca çıkan bir tuzak

Play mode açıkken Unity **script derlemesini ve asset yazımını bloklar** (PLAN Bölüm 11.1).
Bu süre boyunca `Editor.log`'daki hata kayıtları eski derlemeden kalıyor, yani "derleme
temiz" raporu **yanıltıcı** olabiliyor. İki `Generate Data Assets` çağrısı sessizce
"Oluşturulan: 0" döndü.

Ayrıca çeviri metinlerindeki `\n` kaçışları toplu düzenleme sırasında gerçek satır sonuna
dönüşüp 15 string literal'i bölmüştü — Play mode derlemeyi blokladığı için hata geç fark
edildi. Onarıldı ve doğrulandı.

**Ders:** menü komutu çalıştırmadan önce `IsPlaying` kontrol edilmeli; derleme doğrulaması
için log yerine `Library/ScriptAssemblies/*.dll` zaman damgası daha güvenilir.

### 34.8 Doğrulama

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ DLL kaynaktan sonra üretildi, hata yok |
| Üretim | ✅ 6 yeni / 67 korundu — toplam **37 kart** |
| Yetenek yapılandırması | ✅ hepsi `tab=3`, tipler doğru, ikonlar bağlı |
| Prestij eşiği | ✅ `prestigeUnlockTier: 6` |
| Sahne | ✅ "Sahne kuruldu, eksik görsel yok" |

### 34.9 Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Prestij sekmesini aç | Üstte Hat Değişimi paneli, altında 6 yetenek kartı |
| Dili değiştir | Panelde İngilizce metin kalmamalı; birim TR'de "FK" |
| Kademe 6'ya çık | Prestij artık açık (eskiden 8 gerekiyordu) |
| Prestij yap, yetenek al | FK düşüyor, kart seviyesi artıyor |
| Tekrar prestij yap | **Yetenekler korunuyor**, diğer yükseltmeler sıfırlanıyor |
| Hızlı Başlangıç alıp prestij yap | Kademe 0 yerine 1'den başlıyorsun |

---

## 35. Faz 16 — Koleksiyon, Paralel İndirme, Torrent (2026-08-14)

### 35.1 İndirilenler Arşivi — yarım özellik tamamlandı

`FileCollectionPanelView` Faz 9'da yazılmıştı ama iki eksiği vardı:
sayaç hep 0 / bonus hep "+%0" yazıyordu **ve panel sahneye hiç eklenmemişti** —
yani özellik kodda duruyor, oyunda görüntüsü yoktu.

**Eklenen:** `CollectionManager` — tamamlanan **benzersiz** dosyaları tutar
(`HashSet<string>`), her 3 dosyada +%2 kalıcı taban hız verir. Prestijde
**sıfırlanmaz**: "topladıkların sende kalır" hissi, idle oyunlarda oturumu uzatan
en ucuz mekaniktir.

Bonus `EconomyManager.ApplyStats` içinde `PassiveSpeed` havuzuna giriyor; yeni bir
dosya eşiği geçirdiğinde `ApplyStats` yeniden çağrılıyor.

**Kayıt:** `SaveData` v3 → **v4**, `collectedFiles` listesi. Yükleme sırasında
yükseltmelerden **önce** geri yükleniyor — taban hız bonusu verdiği için `ApplyStats`
onu görmüş olmalı.

**UI:** Ayarlar ile aynı desende bir overlay. Sağ üstte arşiv düğmesi (ikon:
`icon_prestige_archive`, prestij yeteneğiyle paylaşılıyor), ortada panel, içinde
kaydırılabilir simge ızgarası — indirilen dosyalar renkli, inmemişler silik.
Hücreler bir kez üretiliyor, tazelemede yalnızca renk değişiyor.

Yeni bileşen: `OverlayToggleView` (basit aç/kapa + alfa geçişi).

### 35.2 Paralel indirme

`DownloadController.ExtraSlots`. Ek slotlar **ekranda görünmez**: üst HUD tek dosya
gösterecek şekilde kurulu ve oraya ikinci bir çubuk sıkıştırmak paneli boğardı.
Oyuncu kazancı yüzen "+$" yazısından ve $/sn göstergesinden görüyor.

Her ek slot tam hızda değil, **%60 verimle** çalışıyor — aksi halde iki slot geliri
birebir ikiye katlar ve diğer tüm yükseltmeleri anlamsızlaştırırdı. Slotlar Otomatik
Kuyruk yükseltmesini de kullanıyor (en kârlı dosyayı seçiyorlar).

### 35.3 Torrent geliri

Boşta gelirin bir yüzdesi kadar ek kazanç — oyunu açık bırakmayı anlamlı kılan mekanik.

**Saniyede bir toplu ekleniyor.** Her karede `Wallet.Add` çağırmak `BalanceChanged`
olayını tetikler, o da görünürdeki tüm yükseltme kartlarını yeniden çizerdi.

### 35.4 VPN ve bir yan bug düzeltmesi

VPN Tüneli kota olayının hız tavanını etkisizleştiriyor (olay yine çıkar, bedeli durur).

Bunu eklerken **taramada bulduğum eski bir bug da kapandı**: `ApplySpeedCeilingToBase`
tavanı anlık `BaseSpeed`'e sabitliyordu. Kota aktifken bağlantı satın alınırsa taban hız
büyüyor ama tavan eski değerde kalıyordu — oyuncu yükseltmeyi alıyor, hiçbir şey
değişmiyordu. Artık `RefreshQuotaCeiling` her `ApplyStats`'ta tavanı tazeliyor.

### 35.5 Dört yeni kart (toplam 41)

| Kart | Sekme | Kademe | Maks | Etki |
|---|---|---|---|---|
| **RAM Yükseltmesi** | Donanım | 5 | 1 | +1 paralel slot |
| **Torrent İstemcisi** | Yazılım | 5 | 10 | +%4 boşta gelir |
| **İkinci Hat** | Altyapı | 6 | 2 | +1 paralel slot |
| **VPN Tüneli** | Yazılım | 7 | 1 | Kota hızı kesmez |

**Güncel dağılım:**
```
t0:2  t1:1  t2:4  t3:4  t4:3  t5:5  t6:4  t7:3  t8:1
```
27 gerçek yükseltme + 6 prestij yeteneği + 8 bağlantı = **41 kart**.
(Faz 13 başlangıcında 6 gerçek yükseltme vardı.)

### 35.6 Yapılmayan iki madde — gerekçesiyle

**Antivirüs (`VirusImmunity`)** — UPGRADE_TREE'de vardı ama **virüs olayı oyunda yok**.
Bağışıklık kartı, var olmayan bir tehdide karşı koruma satmak olurdu. Önce
`GameEventManager`'a virüs olayı eklenmeli; tip numarası (17) ileride kullanılmak üzere
enum'da duruyor.

**Uydu Yedeklemesi** — `OfflineCapacity` tipini kullanacaktı ama o tip Download
Manager'ın Pro kontrolü için **seviye kademesi** olarak kullanılıyor
(`TotalLevel(...) >= 2`). İkinci bir kart eklemek o kontrolü sessizce bozardı; ayrı bir
`OfflineCapHours` tipi gerekiyor.

### 35.7 Doğrulama

| Kontrol | Sonuç |
|---|---|
| Derleme | ✅ DLL kaynaktan sonra üretildi, hata yok |
| Üretim | ✅ 4 yeni / 73 korundu — **41 kart** |
| Sahne | ✅ "Sahne kuruldu, eksik görsel yok" |
| Yeni bileşenler | ✅ `CollectionManager` · `FileCollectionPanelView` · `OverlayToggleView` · arşiv düğmesi + overlay |

### 35.8 Senin Play testin

| Ne yapılacak | Beklenen |
|---|---|
| Sağ üstteki arşiv düğmesine bas | Overlay açılıyor, dosya ızgarası görünüyor |
| Birkaç dosya indir | İndirilenler renkleniyor, sayaç artıyor |
| 3 dosya topla | Bonus "+%2" oluyor, taban hız artıyor |
| Oyunu kapat-aç | Arşiv korunuyor |
| Prestij yap | Arşiv **sıfırlanmıyor** |
| Kademe 5'te RAM al | $/sn belirgin artıyor (ikinci dosya arka planda iniyor) |
| Torrent al, elini çek | Bakiye tıklamadan da artıyor |
