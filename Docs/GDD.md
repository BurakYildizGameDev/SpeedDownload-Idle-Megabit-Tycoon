# SpeedDownload Idle / Megabit Tycoon — Oyun Tasarım Dokümanı (GDD)

**Sürüm:** 2.3 (Sprite'lar ölçüldü; kapsam yalnızca eldeki varlıklarla uygulanabilir hale getirildi — bkz. Bölüm 15)
**Platform:** Mobil (Android / iOS), Unity Engine
**Tür:** 2D Clicker / Idle / Simulation
**Görsel Stil:** Retro-Modern UI (Windows 98/XP nostaljisi + analog/dijital kadran karışımı)
**Asset durumu:** 82 sprite'ın tamamı hazır ve işlenmiş (eksik yok).

---

## 1. Vizyon ve Özet

Oyuncu, bir **Speed Limiter** (araç devir göstergesine benzeyen dairesel bir hız kadranı) üzerinden tıklayarak/dokunarak internet hızını yükseltir. Kazanılan anlık megabit gücüyle dosyalar indirilir, indirilen her dosya oyun içi para kazandırır. Bu para ile altyapı (bağlantı türü), donanım ve yazılım yükseltmeleri satın alınarak hem tıklama gücü hem taban (idle) hız artırılır. Oyun; **bps seviyesinden başlayıp Tbps ve ötesine uzanan devasa bir hız-ölçek ilerlemesi** üzerine kuruludur — her ölçek atlaması yeni bir "çağ" hissi verir.

---

## 2. Temel Oynanış Döngüsü (Core Loop)

1. **Giriş:** Oyuncu ekrana dokunur.
2. **Reaksiyon:** İbre yükselir (ör. 12 bps → 47 bps → 98 bps).
3. **Süreç:** Anlık hıza bağlı olarak aktif dosyanın indirme çubuğu dolar.
4. **Sonuç:** Dosya %100 olunca para kazanılır, otomatik sonraki dosyaya geçilir.
5. **Gelişim:** Kazanılan para ile yükseltme satın alınır.
6. **Ölçeklenme:** Belirli bir hız eşiğine ulaşınca yeni **Bağlantı Kademesi** açılır (bkz. Bölüm 3), yeni dosya türleri ve yeni kadran görselleri devreye girer.

---

## 3. Bağlantı Evrimi — Hız Kademeleri Sistemi

Oyunun omurgası budur: hız, **bps → Kbps → Mbps → Gbps → Tbps** şeklinde, her kademede önce aynı birimde x10 büyüyüp sonra birim atlayarak ilerler. Bu, hem matematiksel olarak tutarlı hem de oyuncuya sürekli "yeni bir çağa geçtim" hissi veren bir yapıdır.

| # | Kademe Adı | Aralık | Bağlantı Teması | Açılan Dosya Türü (Bölüm 5) |
|---|---|---|---|---|
| 0 | **Sinyal Kulübesi** (Prolog) | 1 – 100 bps | Telsiz / Mors sinyali | Ping testi, boş `.txt` |
| 1 | **Telefon Hattı** | 100 – 1.000 bps | İlk çevirmeli bağlantı | Tier 1: Küçük metinler (KB) |
| 2 | **Dial-up 56K** | 1 – 100 Kbps | Klasik "bip-vzzzt" modem sesi | Tier 2: Görseller (JPEG/PNG) |
| 3 | **Gelişmiş Dial-up / ISDN** | 100 – 1.000 Kbps | Çift hatlı bağlantı | Tier 2-3 geçiş dosyaları |
| 4 | **ADSL Çağı** | 1 – 100 Mbps | Ev interneti patlaması | Tier 3: Ses/Müzik (MP3/WAV) |
| 5 | **VDSL / Erken Fiber** | 100 – 1.000 Mbps | Şehir altyapısı yenileniyor | Tier 4: Yazılım/Video (MP4/EXE) |
| 6 | **Tam Fiber** | 1 – 100 Gbps | Optik kablo çağı | Tier 5: Medya/Film (MKV/ISO) |
| 7 | **Kuantum Hat** | 100 – 1.000 Gbps | Deneysel altyapı | Tier 6: Büyük Veri (TAR.GZ) |
| 8 | **Veri Merkezi Ağ Geçidi** | 1+ Tbps | Uçtan uca kurumsal hat | Tier 6+: PB arşivler |
| 9+ | **Sonsuz Kademe** (Endgame) | 100 Tbps → 1 Pbps → ... | Prosedürel, mizahi isimler (ör. "Karadelik Yönlendirici") | Prosedürel üretilen mega dosyalar |

**Kural:** Her kademe kendi içinde iki alt-aşamaya ayrılır — "1-100" ve "100-1.000" aynı birimde — ardından bir sonraki kademe otomatik olarak yeni birime geçer (1.000 bps = 1 Kbps gibi). Kademe 9 ve sonrası, oyunun **sonsuz/prestij döngüsü** için prosedürel olarak üretilir (bkz. Bölüm 7).

Her kademe geçişinde:
- Speed Limiter kadranının görsel teması değişir (ör. dial-up'ta çevirici disk görünümü, fiber'de dijital LED kadran, kuantum'da holografik arayüz).
- Yeni bir "Bağlantı Türü" satın alınabilir hale gelir (bkz. Bölüm 6).
- Arka plan müziği ve renk paleti kademeyle birlikte evrilir.

---

## 4. Speed Limiter (Kadran) Mekaniği

- **Yapı:** Ekran merkezinde dairesel kadran; değerler o anki kademenin birimindedir (bps/Kbps/Mbps/Gbps/Tbps).
- **Tıklama girdisi:** Her dokunuş ibreyi belirli bir ivmeyle ileri fırlatır; bırakılınca ibre yumuşak fizikle taban hıza geri düşer.
- **Redline (Kırmızı Bölge):** `dial_redline_overlay.png` ölçüldü — yay tam olarak **0°…+89°** (saat 12 → saat 3) arasını kaplıyor. Bu nedenle ibre süpürmesi **−90° … +90°** (180°, altta simetrik boşluk) olarak sabitlendi ve redline **eşiği t = 0,50**'dir (GDD v2.2'deki "son %15" değeri sanat eseriyle uyumsuzdu; bkz. Bölüm 15.4).
  Risk/ödül niyetini korumak için bonus **rampalıdır**: `redlineMult = lerp(1.0, 2.0, (t − 0.5) / 0.5)` → t=0,50'de 1,0× · t=0,75'te 1,5× · t=1,00'da **2,0×**. Yani tam Turbo yalnızca overheat eşiğinin hemen dibinde kazanılır.
- **Overheat (Aşırı Isınma):** İbre %100'de 2 saniyeden fazla kalırsa "Modem Aşırı Isındı!" uyarısı çıkar, bağlantı 2 saniye tamamen kopar (0 hız). Risk/ödül dengesi burada kurulur.
- **Kademeye özgü fizik:** Üst kademelerde (Gbps/Tbps) ibre daha "ağır" hareket eder — bu da yeni yükseltmelerin (ör. "Tepki Hızlandırıcı") anlamlı hissettirilmesini sağlar.

---

## 5. Dosya İndirme Sistemi

Aktif dosyanın simgesi, adı, boyutu ve indirme çubuğu ekranın üst/orta kısmında gösterilir.

**Formül:** `Kalan Süre (sn) = Kalan Veri (bit) / Anlık Hız (bit/sn)`

### Dosya/Ödül Tablosu (Kademelere Bağlı)

| Dosya Tier | Örnek Dosyalar | Boyut Aralığı | Ödül Aralığı | Açık Olduğu Kademe |
|---|---|---|---|---|
| 1 | `hello_world.txt`, `odev_v2.docx` | 10 KB – 250 KB | $1 – $5 | 1 |
| 2 | `avatar.jpeg`, `wallpaper_hd.png` | 1,5 MB – 8 MB | $25 – $110 | 2-3 |
| 3 | `favorite_song.mp3`, `podcast_ep1.wav` | 5 MB – 120 MB | $350 – $1.200 | 4 |
| 4 | `funny_cat.mp4`, `game_setup.exe` | 450 MB – 15 GB | $8.500 – $95.000 | 5 |
| 5 | `movie_4k.mkv`, `os.iso` | 60 GB – 250 GB | $650.000 – $3.200.000 | 6 |
| 6 | `datacenter_backup.tar.gz`, `internet_archive.dat` | 50 TB – 1 PB | $45.000.000 – $1.000.000.000 | 7-8 |
| 7+ | Prosedürel mega arşivler | 10 PB+ | Prosedürel ölçekli | 9+ (Sonsuz Kademe) |

Dosya tamamlanınca tamamlama efekti + retro bildirim sesi çalar, para bakiyeye eklenir, sıradaki dosya otomatik başlar.

---

## 6. Ekonomi ve Yükseltmeler

### 6.1 Aktif Yükseltmeler (Tıklama Gücü)
- **Modeme Vurmak:** Tıklama başına Mbps artışını yükseltir.
- **Harici Fan:** Redline toleransını artırır, overheat süresini geciktirir.
- **DNS Ayarı:** %5 ihtimalle "Kritik Hız Patlaması" (5x anlık hız) tetikler.

### 6.2 Pasif Yükseltmeler (Idle & Taban Hız)
- **Bağlantı Türü:** Bölüm 3'teki kademelere paralel satın alınır (Dial-up → ADSL → VDSL → Fiber → Kuantum Hat → Veri Merkezi Hattı).
- **Download Manager:** Oyun kapalıyken arka planda indirmeye devam eder (offline kazanç).
- **Premium Sunucu Üyeliği:** Dosya başı temel ödülü kalıcı yüzde ile artırır.

### 6.3 Temel Formüller

```
Tıklama Gücü (CP)      = BaseCP × (1 + 0.15 × YükseltmeSeviyesi) × PrestijÇarpanı
İbre Hedef Hızı         = min(KademeMaksHız, MevcutHız + CP × TıklamaKatsayısı)
Taban Hız (Idle)        = BağlantıTürüBazHızı × (1 + 0.10 × PasifYükseltmeSeviyesi)
Yükseltme Maliyeti(n)   = TemelMaliyet × 1.15^n
Redline Turbo           = AnlıkHız × 2.0  (kırmızı bölgedeyken)
Offline Kazanç          = PasifGelir/sn × min(GeçenSüre, KapasiteSüresi) × OfflineVerimlilik
```

- `OfflineVerimlilik`: başlangıçta %50 / 8 saat tavan; "Download Manager Pro" yükseltmesiyle %100 / 24 saate çıkar.

---

## 7. Prestij Sistemi — "Hat Değişimi"

Kademe 8 (Tbps eşiği) sonrası oyuncu, o ana kadar biriktirdiği toplam kazanca göre **Fiber Kredisi** (kalıcı prestij para birimi) kazanarak baştan başlayabilir:

```
FiberKredisi Kazancı   = floor(√(ToplamYaşamBoyuKazanç / PrestijEşiği))
Kalıcı Gelir Çarpanı   = 1 + 0.02 × ToplamFiberKredisi
```

Bu sistem, oyunun tekrar oynanabilirliğini sağlar: her "Hat Değişimi" sonrası oyuncu daha hızlı ilerler ve nihayetinde **Sonsuz Kademe**'ye (Bölüm 3, #9+) ulaşır — burada kademeler prosedürel olarak (Pbps, Ebps, Zbps... mizahi isimlerle) sonsuza uzanır.

---

## 8. Rastgele Olaylar ve Engeller

- **Komşu Wi-Fi Çaldı:** Hız rastgele %40 düşer; "Şifreyi Değiştir" butonuna 3 kez basarak düzeltilir.
- **Kota Aşımı:** Belirli veri miktarından sonra hız taban seviyeye çekilir; küçük harcama veya ödüllü reklamla sıfırlanır.
- **Gece Tarifesi (Happy Hour):** 45 saniye boyunca tüm kazançlar 3x.

---

## 9. UI/UX Düzeni

- **Üst Panel:** Bakiye, anlık hız (kademe birimiyle), aktif dosya adı + %.
- **Orta Panel:** Speed Limiter kadranı (kademeye göre tema değişir).
- **Alt Panel:** Yükseltme sekmeleri (Altyapı, Donanım, Yazılım) + Prestij sekmesi.

## 10. Ses Tasarımı

- Tıklama: hafif mekanik klavye/buton sesi.
- İbre yükselme: motor devri gibi yükselen synth tonu.
- İndirme bitişi: nostaljik OS bildirim sesi.
- Arka plan: kademeye göre evrilen düşük tempolu synthwave/chiptune.

> **Uygulama notu (v2.3):** Projede **hiç ses varlığı yok** (`Assets/Audio` boş). Bu bölüm, klip beklemeden
> **prosedürel üretimle** hayata geçirilir: `AudioManager`, `AudioClip.Create` ile çalışma anında dalga formu
> sentezler (tıklama = kare dalga 880 Hz · ibre = hıza bağlı pitch'li testere · bitiş = C-E-G arpej ·
> overheat = alçalan kare dalga + gürültü · kademe atlama = yükselen 5 nota). Chiptune estetiğine birebir uyar.
> **BGM** slot olarak hazır ama boştur; gerçek klip sağlandığında tek satırda bağlanır, kod değişmez.

## 11. Monetizasyon

- **Ödüllü reklam (opsiyonel, zorunlu değil):** 2x kazanç, kota sıfırlama, offline kazancı ikiye katlama.
- **IAP:** Fiber Kredisi paketleri, reklamsız tek seferlik satın alma, kozmetik kadran temaları.
- Zorunlu banner/geçiş reklamı yok — oyun akışını bölmeyecek şekilde tasarlanır.

> **Uygulama notu (v2.3):** Projede reklam/IAP SDK'sı **kurulu değil**. Bu bölüm `IAdService` arayüzü +
> `StubAdService` ile uygulanır: sahte ödüllü reklam paneli var olan sprite'larla kurulur
> (`ui_panel_bg` + `ui_icon_reward_ad` + `ui_icon_close` + 3 sn geri sayım), internetsiz tam çalışır.
> Gerçek SDK (AdMob / Unity Ads) sonradan **tek sınıf** değişimiyle takılır; oyun kodu etkilenmez.

---

## 12. Gelecek Fikirler (Geliştirilebilecekler)

1. **Çoklu indirme kuyruğu:** Aynı anda birden fazla dosyayı sıraya koyup bant genişliği paylaşımı stratejisi eklemek.
2. **Liderlik tablosu:** Haftalık "En Yüksek Hız" yarışması ile sosyal rekabet katmanı.
3. **Mini-etkileşimler:** Sahte CAPTCHA çözme veya "buffering" anları gibi kısa nostaljik mikro-etkileşimler.
4. **Sınırlı süreli temalar:** Black Friday / yılbaşı gibi dönemsel kadran ve dosya temaları.

---

## 13. Teknik Uygulama Notları (Unity)

### 13.1 Veri-Odaklı Mimari (ScriptableObject bazlı)

```
Assets/
  Scripts/
    Core/          -> GameManager, SpeedController, DownloadController
    Systems/        -> EconomyManager, PrestigeManager, SaveManager, AudioManager
    UI/             -> HUDController, UpgradePanelController, DialThemeSwitcher
    Data/           -> ConnectionTierSO, FileDataSO, UpgradeSO (ScriptableObject tanımları)
  ScriptableObjects/ -> Her kademe (Tier0..Tier8+) ve her dosya için .asset veri dosyaları
  Sprites/           -> KULLANICI TARAFINDAN SAĞLANIR (bkz. 13.2 — 82 dosya hazır)
  Audio/
  Prefabs/
  Scenes/
```

### 13.2 Önemli Not — Görsel Varlıklar (Assets)

> **Bu proje için tüm PNG/sprite/ikon/arka plan görselleri kullanıcı (proje sahibi) tarafından sağlanır.**
> Claude Code / Unity MCP bu görselleri **üretmemeli**, `Assets/Sprites/` altına yerleştirilen dosyaları **referans olarak kullanmalıdır**. Görsel eksikse placeholder bir kutu/renk ile geçici gösterim yapılabilir, ancak nihai sanat kullanıcıdan gelecektir.

### 13.3 Görsel Varlık Durumu (güncel)

Sprite listesindeki **82 görselin tamamı üretildi ve oyuna hazır** hale getirildi
(dama deseni yerine gerçek alfa kanalı, kırpma + ortalama, hedef çözünürlük, palet sıkıştırma —
toplam 394 MB'tan 2,5 MB'a indi). Klasör yapısı ve import ayarları için
`Asset_Integration_Guide.md` Bölüm 0-2'ye bak.

| Kategori | Hazır | Çözünürlük |
|---|---|---|
| `Dial/` (9 kademe zemini + ibre + redline + tik halkası) | 12 | 512×512, şeffaf |
| `FX/` | 7 | 512×512, şeffaf |
| `FileIcons/` | 13 | 512×512, şeffaf |
| `UpgradeIcons/` (8 bağlantı + 5 yükseltme) | 13 | 512×512, şeffaf |
| `UI/` (ikonlar + buton/panel/bar) | 19 | 512×512 ve oranlı geniş formlar |
| `Events/` | 4 | 512×512 + banner 512×115 |
| `Backgrounds/` | 9 | 1024×576, opak |
| `Branding/` | 2 | logo 512×512 / app icon 1024×1024 |
| `Mascot/` | 3 | 512×512, şeffaf |

Eksik sprite yoktur. Kadran ibresi (`dial_needle`, 512×86) dahil her varlık yerindedir;
Bölüm 4'teki ibre mekaniği doğrudan kurulabilir.

> **Ölçüm notu (v2.3):** `dial_needle` yalnızca **%3,5 opak** — yani içi dolu bir ibre değil, **ince bir kontur
> çizimi** (navy, RGB 39·53·76). Kademe 6/7/8 kadranları koyu lacivert zeminli olduğu için ibre bu kademelerde
> **görünmez kalırdı**. Çözüm: `SpriteSolidTint` shader'ı sprite'ın RGB'sini yok sayıp yalnızca alfasını maske
> olarak kullanır ve `ConnectionTierSO.needleColor` ile boyar. Yeni sprite gerekmez.
> Ayrıntılı ölçüm tablosu için Entegrasyon Rehberi Bölüm 8'e, bilinen iki ayrıntı için Bölüm 7'ye bak.

---

## 14. Claude Code + Unity MCP ile Geliştirme Rehberi

Bu bölüm, bu GDD'yi **Claude Code CLI** üzerinden Unity'ye bağlayıp oyunu adım adım geliştirmek isteyenler içindir.

### Adım 1 — Ön Koşullar
- Unity Hub üzerinden Unity 2022 LTS veya üstü bir sürüm kurulu olmalı, boş bir 2D proje oluşturulmalı.
- Node.js 18+ (npm kurulumu tercih edilecekse) veya doğrudan native installer.

### Adım 2 — Claude Code CLI Kurulumu

```bash
# macOS / Linux / WSL (önerilen, native installer)
curl -fsSL https://claude.ai/install.sh | bash

# Windows PowerShell
irm https://claude.ai/install.ps1 | iex

# Alternatif: npm ile (Node.js 18+ gerektirir)
npm install -g @anthropic-ai/claude-code
```

Kurulum sonrası doğrulama ve ilk giriş:

```bash
claude --version
claude   # ilk çalıştırmada tarayıcı üzerinden Anthropic hesabınla giriş yaparsın
```

> Not: Claude Code'u kullanmak için bir Claude aboneliği (Pro/Max/Team/Enterprise) veya Console (API) hesabı gerekir; ücretsiz plan Claude Code'u içermez.

### Adım 3 — Unity MCP Kurulumu

En yaygın kullanılan çözüm **"MCP for Unity" (CoplayDev/unity-mcp)** paketidir:

1. Unity'de **Window → Package Manager → + → Add package from git URL...** seç.
2. Şu adresi yapıştır: `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`
3. Paket içeri aktarıldıktan sonra **Window → MCP for Unity** penceresini aç, kurulum sihirbazını takip et (Python/`uv` gerekebilir, sihirbaz eksikse kurulumunu yönlendirir).
4. **"Configure All Detected Clients"** butonuna basarak Claude Code'u otomatik yapılandır (ya da listeden manuel Claude Code'u seç).
5. Unity projesi açıkken, proje kök dizininde terminalden `claude` komutunu çalıştır. Unity Editor'de bir **"Pending Connection"** bildirimi çıkacak — onu **Accept** ile onayla.

> Alternatif olarak Unity 6'nın kendi dahili **AI beta MCP Server**'ı da (Project Settings → AI → Unity MCP) kullanılabilir; adımlar benzerdir.

### Adım 4 — Bu GDD'yi ve Assetleri Projeye Ekle

- Bu dosyayı proje kök dizinine `GDD.md` (veya `Docs/GDD.md`) olarak koy.
- `Sprite_List.md` ve `Asset_Integration_Guide.md` dosyalarını da aynı yere koy.
- İşlenmiş sprite klasörünü (`Oyun Spriteleri/Sprites/`) olduğu gibi `Assets/Sprites/` altına kopyala — alt klasör isimleri (`Dial`, `FX`, `UI`, ...) rehberdeki yapıyla birebir aynıdır (Bölüm 13.3).
- `_EKSIK_SPRITELAR.md` ve `_rapor.csv` dosyalarını `Assets/` altına kopyalama; onları proje kökünde `Docs/` içinde tut.

### Adım 5 — İlk Prompt Önerisi

Claude Code'u proje kök dizininde başlattıktan sonra, örnek ilk komut:

> "GDD.md dosyasını oku. Bölüm 3 ve 4'e göre, Kademe 0-1 (bps aralığı) için Speed Limiter kadranının temel fizik scriptini, ilgili ScriptableObject veri yapılarını (ConnectionTierSO, FileDataSO) ve basit bir sahne kurulumunu oluştur. Sprite'lar Assets/Sprites altında olacak, onları placeholder üretmeden referans olarak kullan; eksikse geçici renkli kutu bırak."

### Adım 6 — İteratif Çalışma Sırası (Önerilen)

GDD'nin tamamını tek seferde istemek yerine, sırayla ilerlemek daha sağlıklı sonuç verir:

1. Speed Limiter fizik mekaniği (Bölüm 4) + tek kademe (bps) test sahnesi
2. Download/Progress sistemi (Bölüm 5) ve dosya veri tabanı
3. Ekonomi, yükseltmeler, formüller (Bölüm 6)
4. Kademe geçiş sistemi + tüm kademeler (Bölüm 3)
5. Prestij sistemi (Bölüm 7)
6. UI/UX ekranları (Bölüm 9) — bu noktada gerçek sprite'ları eklemen gerekecek
7. Rastgele olaylar (Bölüm 8), ses (Bölüm 10), monetizasyon (Bölüm 11)

Her adımdan sonra Unity Editor'de **Play** ile test et, Claude Code'a bir sonraki adıma geçmeden önce hataları/geri bildirimi ilet.

---

## 15. v2.3 — Eldeki Varlıklarla Kapsam Kararları

Sprite'ların alfa kanalı piksel piksel ölçüldükten sonra, GDD'nin **yeni varlık üretmeden** uygulanabilmesi
için aşağıdaki kararlar alındı. Hiçbiri yeni PNG, ses klibi veya font gerektirmez.
Uygulama ayrıntıları: `Docs/PLAN.md` Bölüm 1-2.

### 15.1 Kademe 3 ve 5'in oda arka planı yok
Dokuz arka plandan biri `bg_loading`, biri `bg_mainmenu` olduğu için oyun içi oda sayısı yedi.
**Karar:** Kademe 3 → `bg_tier1_2_retro_room` (soğuk mavi tint), Kademe 5 → `bg_tier4_modern_room`
(açık cyan tint). Tint, `ConnectionTierSO.roomTint` alanından gelir.

### 15.2 İbre içi boş kontur
Bkz. Bölüm 13.3 ölçüm notu. **Karar:** `SpriteSolidTint` shader'ı + kademe başına `needleColor`.

### 15.3 İbre ölçeği
Ölçüm: pivot (0,1·0,5) → uç mesafesi **444,8 px**. Ucun redline bandının ortasına (r ≈ 0,80) değmesi için
**ölçek = 0,461** → 512 birimlik kadranda `NeedleSprite` = **236 × 40**.

### 15.4 Redline oranı — GDD v2.2 ile sanat eseri uyumsuzluğu
v2.2 "son %15" diyordu; ölçülen yay 90°'lik bir çeyrek. **Karar:** sanata sadık kalındı —
süpürme −90°…+90°, eşik t=0,50, bonus **rampalı** (bkz. Bölüm 4). Böylece hem görsel doğru,
hem de tam 2× yalnızca overheat sınırında kazanılıyor.

### 15.5 Ses varlığı yok
**Karar:** prosedürel sentez (bkz. Bölüm 10 uygulama notu).

### 15.6 Retro font yok
**Karar:** TMP varsayılanı + materyal ayarı (outline 0,15 · softness 0 · ALL CAPS · letter spacing +8).
Sayaçlar `<mspace>` ile sabit genişlikte — bakiye/hız güncellenirken rakamlar zıplamaz.

### 15.7 Reklam / IAP SDK yok
**Karar:** `IAdService` + `StubAdService` (bkz. Bölüm 11 uygulama notu).

### 15.8 Sonsuz kademe (9+) kadran zemini yok
**Karar:** `dial_tier8_datacenter_bg` + `HueShift` shader'ı; kademe indeksi × 137,5° (altın açı) ile hue
döndürülür → her sonsuz kademe farklı renk. Oda: `bg_tier9_abstract_space`.

### 15.9 `dial_tier1_phoneline_bg` delikli
Merkez **ve** çevirmeli disk boncuklarının içi şeffaf (kaynakta parmak delikleri alfaya çevrilmiş).
**Karar:** `DialRoot` altına en alta `DialBackplate` — Unity'nin yerleşik `Knob` sprite'ı, koyu tint.
Rotary telefon deliklerinden koyu zemin görünmesi zaten doğru görünüm.

### 15.11 Dosya boyutları sabit değil, kademe hızından türetiliyor
Bölüm 5'teki boyut tablosu düşük kademelerde uygulanamazdı: `odev_v2.docx` 250 KB = 2.048.000 bit,
kademe 1'in **tavan** hızı 1.000 bps → tek dosya 34 dakika sürerdi.
**Karar:** boyut = `referansHız × fileTargetSeconds × dosyaÇarpanı`, referans hız kademe aralığının
logaritmik %60'ı. Böylece her dosya ~4–36 sn'de biter. Üst kademelerde sonuç GDD'nin verdiği aralığa
zaten yakın çıkıyor (kademe 6 → 13–66 GB, tabloda 60–250 GB).
Bölüm 5'teki ödül sütunu **korundu**; yalnızca boyutlar türetiliyor.

### 15.12 Bağlantı maliyetleri ödüllerden türetiliyor
`maliyet(t) = ortalamaÖdül(t−1) × 25`. Her kademe ~25 dosyalık gelire mal olur; ödül tablosundaki
düzensiz büyüme (×3,7 ile ×43 arası) ilerleme temposunu bozmaz.

### 15.10 `ui_progressbar_frame` gerçekten içi boş
Ölçüm: **%21,5 opak**, iç boşluk x 21…490 / y 11…150.
**Karar:** Fill objesi frame'in **içine** `left=21 · right=22 · top=11 · bottom=16` px offset ile yerleşir;
`Image Type = Filled, Horizontal, Origin Left`.
