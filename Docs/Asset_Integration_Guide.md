# SpeedDownload Idle / Megabit Tycoon — Asset Entegrasyon Rehberi

**Sürüm:** 2.2 — 82/82 sprite hazır **ve alfa kanalları ölçüldü**. Tahminî değerlerin yerini ölçüm aldı (bkz. Bölüm 8).

Bu dosya `GDD.md` ve `Sprite_List.md` ile birlikte kullanılır. Amacı: Gemini ile ürettiğin PNG'leri Unity projesine koyduktan sonra, **Claude Code + Unity MCP'nin bunları nasıl algılayıp bağlaması gerektiğini** net kurallarla tarif etmek. Bu dosyayı proje kök dizinine `Asset_Integration_Guide.md` olarak koy; Claude Code'a görev verirken referans ver.

---

## 0. Mevcut Durum — Assetler İşlendi

Gemini çıktıları işlendi ve oyuna hazır hale getirildi. Yapılanlar:

- **İsimlendirme:** Ham `gorsel_N.png` dosyaları içeriğine bakılarak sprite listesindeki gerçek adlara çevrildi.
- **Arka plan:** Gemini "transparent PNG" talimatını yanlış yorumlayıp **dama desenini gerçek piksel olarak boyamıştı**; bu desen algoritmik olarak gerçek alfa kanalına çevrildi. Yumuşak geçişler (glow, duman, halka parlaması) yarı saydam olarak korundu.
- **Boyut:** Kırpılıp ortalandı ve hedef çözünürlüklere indirildi (aşağıdaki tablo).
- **Sıkıştırma:** 256 renkli palet PNG; kodlanan dosya geri okunup alfa sadakati doğrulanıyor. Toplam **394 MB → 2,5 MB**.
- **Temizlik:** Gemini'nin köşe sparkle/watermark izleri ve istenmeyen gri gölge haleleri silindi.

| | Adet |
|---|---|
| Sprite listesinde tanımlı | 82 |
| **Hazır ve işlenmiş** | **82 — eksik yok** |

İlk turda 70 sprite üretilmişti; kalan 12'si sonradan üretilip aynı işlem hattından geçirildi.
İkinci turda tuval oranları doğru geldi (ikonlar 1:1, ibre 4:1, arka planlar 16:9) ve köşe
sparkle izi çıkmadı; beşi düz beyaz zeminli, dördü dama desenli geldiği için işlem hattı
her iki girdi türünü de karşılayacak şekilde genişletildi.

> Ham `gorsel_N.png` dosyaları ve 13 adet birebir kopya, orijinal klasörde dokunulmadan duruyor — işlenmiş sürümde bir sorun görürsen kaynağa dönebilirsin.

---

## 1. Klasör Yapısı — PNG'leri Tam Buraya Koy

İşlenmiş dosyalar zaten aşağıdaki yapıda hazır; klasörü olduğu gibi `Assets/` altına kopyalaman yeterli:

```
Oyun Spriteleri/Sprites/     ->  kopyala  ->  Assets/Sprites/
    Dial/           <- dial_tier0..8_*.png, dial_redline_overlay.png, dial_tick_marks.png
    FX/             <- fx_*.png
    FileIcons/      <- icon_file_*.png
    UpgradeIcons/   <- icon_conn_*.png, icon_upgrade_*.png
    UI/             <- ui_*.png
    Events/         <- event_*.png
    Backgrounds/    <- bg_*.png
    Branding/       <- logo_*.png
    Mascot/         <- mascot_*.png
    _rapor.csv            <- dosya bazında kaynak/boyut/sıkıştırma raporu
```

> Dosya adını değiştirme. Claude Code, GDD ve prompt listesindeki isimlerle scriptlerde doğrudan referans verecek — isim uyuşmazsa `AssetDatabase` üzerinden bulamaz.

---

## 2. Unity Import Ayarları (Kategoriye Göre)

Her klasör için Claude Code'un (Unity MCP üzerinden, `Texture Importer` ayarlarını script ile) uygulaması gereken ayarlar:

Aşağıdaki tablo **dosyaların gerçek çözünürlükleriyle** güncellenmiştir:

| Klasör / dosya | Gerçek boyut | Texture Type | Sprite Mode | PPU | Max Size | Compression | Mip Maps | Filter | Alpha Is Transparency |
|---|---|---|---|---|---|---|---|---|---|
| `Dial/` (9 zemin) | 512×512 | Sprite (2D and UI) | Single | 100 | 512 | ASTC 6x6 (mobil) | Kapalı | Bilinear | Açık |
| `Dial/dial_redline_overlay`, `dial_tick_marks` | 512×512 | Sprite (2D and UI) | Single | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `FX/` | 512×512 | Sprite (2D and UI) | Single | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `FileIcons/`, `UpgradeIcons/`, `Events/` ikonları | 512×512 | Sprite (2D and UI) | Single | 100 | 256 * | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `UI/` ikonları (`ui_icon_*`, `ui_tab_*`) | 512×512 | Sprite (2D and UI) | Single | 100 | 256 * | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `UI/ui_button_normal / _pressed / _disabled` | 512×237 | Sprite (2D and UI) | Single + **Border** | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `UI/ui_panel_bg` | 512×308 | Sprite (2D and UI) | Single + **Border** | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `UI/ui_progressbar_fill` | 512×128 | Sprite (2D and UI) | Single + **Border** | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `Events/event_happyhour_banner` | 512×115 | Sprite (2D and UI) | Single | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `Backgrounds/` | 1024×576 (16:9) | Sprite (2D and UI) veya Default | Single | 100 | 1024 | ASTC 8x8 (dosya boyutu için) | Kapalı | Bilinear | Kapalı (opak) |
| `Branding/logo_main.png` | 512×512 | Sprite (2D and UI) | Single | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |
| `Branding/logo_icon_appstore.png` | 1024×1024 | Unity sprite değil — Player Settings > Icon alanına atanır | — | — | 1024 | Platform varsayılanı | — | — | Kapalı (opak) |
| `Mascot/` | 512×512 | Sprite (2D and UI) | Single | 100 | 512 | ASTC 6x6 | Kapalı | Bilinear | Açık |

\* İkonlar 512×512 kaydedildi ama HUD'da küçük görüneceklerse **Max Size = 256** yeterli ve bellekte 4 kat tasarruf sağlar. Büyük gösterilen bir ikon varsa (ör. yükseltme kartı görseli) onu 512'de bırak.

Tüm kategorilerde ortak kural: **Mip Maps kapalı** (2D UI/oyun objesi olduğu için gereksiz, bellek israfı), **Wrap Mode = Clamp**, **Read/Write Enabled kapalı** (performans).

**9-slice not (v2.2 — ölçüldü):** Border değerleri artık tahminî değil. Alfa kanalından çıkarılan opak sınır kutusu ve köşe yarıçaplarına göre:

| Sprite | Boyut | Opak bbox | Opak % | **Border (L, B, R, T)** |
|---|---|---|---|---|
| `ui_button_normal` / `_pressed` / `_disabled` | 512×237 | x 15–496, y 7–229 | 82,5 | **70, 60, 70, 60** |
| `ui_panel_bg` | 512×308 | x 14–497, y 9–272 | 80,5 | **30, 45, 30, 30** (alt kenarda 35 px gölge var) |
| `ui_progressbar_frame` | 512×167 | x 14–497, y 5–161 | **21,5** | **75, 20, 75, 20** |
| `ui_progressbar_fill` | 512×128 | x 15–496, y 4–123 | 83,5 | **55, 10, 55, 10** |
| `event_happyhour_banner` | 512×115 | x 15–496, y 4–111 | 65,3 | 9-slice **yok** — Single kullan |

**Progress bar kurulumu:** `ui_progressbar_frame` gerçekten **içi boş bir çerçeve** (yalnızca %21,5 opak).
Ölçülen iç boşluk: **x 21…490, y 11…150**. Fill objesi frame'in **içine** şu offset'lerle yerleşir:

```
ProgressBar (512×167)
 ├─ Frame  ui_progressbar_frame   Image Type = Sliced, border 75,20,75,20
 └─ Fill   ui_progressbar_fill    offset  left=21  right=22  top=11  bottom=16
                                  Image Type = Filled, Horizontal, Origin = Left
```

**PNG formatı notu:** Dosyalar 256 renkli palet (PNG-8 + alfa) olarak kaydedildi. Unity içeri aktarırken kendi doku formatına çevirdiği için bu tamamen sorunsuzdur; sadece diskteki/git'teki boyutu küçültür.

---

## 3. Kadran (Dial) Katman Kurulumu — En Kritik Kısım

Her bağlantı kademesi tek bir görsel değil, **üst üste binen 3-4 ayrı katmandan** oluşur. Sıra (alttan üste, Z-order):

```
DialRoot (Empty GameObject, kademe değişince tüm alt objeler swap edilir)   512x512
 ├─ DialBackplate   <- Unity yerleşik "Knob" sprite'ı, koyu tint, ölçek 0.90
 │                     (tier1'in şeffaf merkezi ve disk delikleri için — bkz. Bölüm 7)
 ├─ Background      <- dial_tierX_*_bg.png (statik zemin, tüm kademeler 512x512)
 ├─ TickMarksOverlay (opsiyonel) <- dial_tick_marks.png, tint rengi koda göre değişebilir
 ├─ RedlineOverlay  <- dial_redline_overlay.png (aynı 512x512 canvas, background ile TAM hizalı, ekstra rotasyon/hesap GEREKMEZ çünkü arc zaten görselin sağ-üst köşesinde önceden konumlandırılmış — sadece background'ın üstüne aynı boyutta bindir)
 └─ NeedlePivot (Empty GameObject, tam merkezde konumlanır)
     └─ NeedleSprite <- dial_needle.png (512x86, yatay yatık, uç sağda)
                        RectTransform 236 x 40  (ölçek 0.461 — bkz. Bölüm 3.2)
                        Material: SpriteSolidTint, renk = tier.needleColor
```

### 3.0. Katman hizalaması — ölçülmüş değerler

Dokuz kadran zemininin hepsi aynı çerçeveye kırpıldı: **daire, 512 px tuvalin %94,5'ini kaplıyor** ve tam ortalanmış durumda. Yani kademeler arası geçişte kadran zıplamaz, hepsi birebir üst üste oturur.

Overlay'ler ise **kırpılmadı** (orijinal tuval oranı korundu ki arc/tik konumları bozulmasın):

| Dosya | İçeriğin tuvale oranı | Background üzerine bindirirken |
|---|---|---|
| `dial_tierX_*_bg.png` | %94,5 | referans, ölçek 1.0 |
| `dial_tick_marks.png` | %87,3 | tik halkasını daireye oturtmak için ölçek **≈ 1,08** |
| `dial_redline_overlay.png` | %48,6 (yay, sağ-üstte) | ölçek 1.0 — tuval merkezine göre konumlanmıştır |

`RedlineOverlay` ve `TickMarksOverlay` GameObject'lerini `Background` ile **aynı boyutta ve aynı merkezde** yerleştir; tik halkası için yukarıdaki 1,08 ölçeği uygula (ya da sadece `dial_tick_marks` kullanmayıp zeminlerdeki hazır tikleri kullan — dokuz zeminin hepsinde tik halkası zaten çizili).

### 3.1. Neden ayrı katmanlar?
- `Background` hiç değişmez, sadece kademe değişince swap edilir.
- `RedlineOverlay` sabit — ileride redline oranını (%15 gibi) değiştirmek istersen sadece bu tek görseli değiştirirsin, 9 zemin görselini yeniden üretmen gerekmez.
- `NeedleSprite`, `SpeedController` scripti tarafından her frame `transform.rotation` ile döndürülür — GDD Bölüm 4'teki formüllere göre (`İbre Hedef Hızı` formülü).

### 3.2. Needle (ibre) pivot ayarı — dikkat edilecek nokta

`dial_needle.png` prompt'u ibreyi "geniş taban solda, ince uç sağda, yatay yatan" şekilde tanımlıyor. Bu yüzden Unity'de:
- Sprite Editor'da **Custom Pivot** seç, pivot noktasını görselin **sol-orta** kısmına (yaklaşık x=0.1, y=0.5) ayarla — böylece obje döndüğünde dönme ekseni ibrenin tabanında olur, ortasında değil.
- `NeedlePivot` GameObject'i tam olarak dial'ın merkez göbeğiyle (hub cap'in tam ortası) çakışacak şekilde konumlandırılmalı.
- Rotasyon: 0° = ibre sola yatık (taban konumu), oyun mantığında `minSpeed` karşılığı; kadranın kaç derece döneceği (GDD'deki kırmızı bölgeye denk gelen açı) `SpeedController` içinde bir `float minAngle, maxAngle` ile tanımlanmalı.

#### v2.2 ölçüm sonuçları — ibre hakkında üç kritik bulgu

**1. Pivot 0.1 değeri doğrulandı.** Opak sınır kutusu x 14…496; tabandaki dairenin merkezi x ≈ 51,2 px
= tam olarak `0.1 × 512`. Rehberdeki değer birebir doğru, değiştirme.

**2. İbrenin ölçeği hesaplandı.** Pivot → uç mesafesi **444,8 px**. Redline yayı r = 0,730…0,854
bandında olduğuna göre ucun r ≈ 0,80'e değmesi gerekir:
```
ölçek = (0,80 × 256) / 444,8 = 0,461
```
→ 512 birimlik kadranda `NeedleSprite` RectTransform = **236 × 40**.
Ölçeklemezsen ibre kadranın 1,7 katı uzunlukta olur ve tuvalin dışına taşar.

**3. İbre içi BOŞ — yalnızca %3,5 opak.** Sprite dolu bir ibre değil, ince bir **kontur çizimi**
(navy, RGB 39·53·76). Kademe 6/7/8 kadran zeminleri koyu lacivert/siyah olduğu için ibre bu
kademelerde **görünmez kalır**. Ayrıca `Image.color` ile tint de işe yaramaz: çarpımsal tint koyu
konturu daha da koyulaştırır.

> **Çözüm:** `SpeedDownload/SpriteSolidTint` shader'ı — sprite'ın RGB'sini yok sayar, **yalnızca alfa
> kanalını maske olarak** kullanır ve `_Color` ile boyar. Böylece ibre her zeminde istenen parlaklıkta
> görünür. Renk `ConnectionTierSO.needleColor` alanından gelir: açık kadranlarda (0-1) koyu kırmızı,
> koyu kadranlarda (6-8) neon turuncu / macenta / sarı. Yeni sprite üretmeye gerek yoktur.

### 3.3. RedlineOverlay'i dinamik yapmak istersen (opsiyonel, ileri seviye)
Statik overlay yeterliyse yukarıdaki gibi doğrudan bindirmek en basit yöntem. İleride redline oranını kod ile ayarlanabilir yapmak istersen: `Image` component + `Image Type = Filled, Fill Method = Radial 360` kullanarak `fillAmount` ile arc uzunluğunu runtime'da kontrol edebilirsin — bu durumda `dial_redline_overlay.png` yerine düz bir renk/gradient dokusu kullanılır ve şekil tamamen kodla çizilir. GDD'nin ilk sürümü için statik overlay yeterli.

---

## 4. ScriptableObject Bağlama

`ConnectionTierSO` (GDD Bölüm 13.1) içine şu alanlar eklenmeli, her `.asset` dosyasında ilgili sprite atanmalı:

```csharp
[CreateAssetMenu(fileName = "ConnectionTier", menuName = "SpeedDownload/ConnectionTier")]
public class ConnectionTierSO : ScriptableObject
{
    public int tierIndex;
    public string tierName;          // "Sinyal Kulübesi", "Telefon Hattı" vb.
    public float minSpeed, maxSpeed; // GDD Bölüm 3 tablosundaki aralık
    public string unit;              // "bps", "Kbps", "Mbps" vb.
    public Sprite dialBackground;    // Assets/Sprites/Dial/dial_tierX_*.png
    public Sprite connectionIcon;    // Assets/Sprites/UpgradeIcons/icon_conn_*.png
    public Sprite roomBackground;    // Assets/Sprites/Backgrounds/bg_*.png (varsa)
}
```

Aynı mantık `FileDataSO` ve `UpgradeSO` için de geçerli — her birinde ilgili `icon_file_*.png` / `icon_upgrade_*.png` alanı olmalı.

---

## 5. Claude Code'a Görev Verme Şablonu

Assetleri klasörlere koyduktan sonra Claude Code'a şu şekilde bir görev verebilirsin (kopyala-yapıştır, dosya adlarını elindeki gerçek dosyalarla değiştir):

> "Asset_Integration_Guide.md ve GDD.md dosyalarını oku. Assets/Sprites/Dial/ altına dial_tier0_signal_bg.png, dial_needle.png, dial_redline_overlay.png ve dial_tick_marks.png dosyalarını koydum. Bölüm 3'teki katman hiyerarşisine göre Kademe 0 için bir DialRoot prefabı oluştur: Background, RedlineOverlay ve NeedlePivot > NeedleSprite objelerini kur, needle'ın pivot noktasını sol-orta olacak şekilde Sprite Editor'dan ayarla. Import ayarlarını Bölüm 2'deki tabloya göre uygula."

### Önerilen sıralama (bir önceki GDD Bölüm 14, Adım 6 ile uyumlu):
1. Önce sadece `Dial/` klasörünü bağla → kadran prefabı + needle rotasyon scripti test edilsin.
2. Sonra `FileIcons/` + `UpgradeIcons/` → ekonomi/yükseltme ekranı bağlansın.
3. Sonra `UI/` genel ikonlar → HUD tamamlansın.
4. Sonra `Backgrounds/` + `Branding/` → sahneler ve ana menü.
5. En son `FX/`, `Events/`, `Mascot/` → parlatma/polish aşaması.

Assetlerin tamamı hazır olsa bile klasör klasör bağlamak, tek seferde 70 dosya fırlatmaktan çok daha az hataya yol açar.

---

## 6. Kontrol Listesi

82 dosyanın tamamı için aşağıdaki maddeler **işlem sırasında kontrol edildi ve geçti**:

- [x] Dosya adı sprite listesindeki isimle birebir aynı.
- [x] Klasör doğru (`Sprites/<Kategori>/`).
- [x] Şeffaflık doğru: Backgrounds (9) ve `logo_icon_appstore` opak, diğer 72 dosya gerçek alfa kanallı.
- [x] Her sprite macenta zemine bindirilerek delik kontrolü yapıldı; kalan boşluklar yalnızca tasarım gereği olanlar (halka içi, kilit sapı, kablo boşluğu, boş progress bar çerçevesi).
- [x] Köşedeki sparkle/watermark izleri silindi.
- [x] Kadran zeminlerinde gömülü ibre/rakam yok; dokuz zemin aynı çerçevede.

**Yeni bir sprite ürettiğinde** aynı listeyi tekrar uygula — ham Gemini çıktısı dama desenli ve ~5 MB gelir, işlenmeden Unity'ye koyma.

---

## 7. Bilinmesi Gereken İki Ayrıntı

- **`dial_tier1_phoneline_bg`**: Kadranın ortası açıktır (şeffaf). Üretimde model çevirmeli telefon diskini dolu bir daire değil **halka** olarak çizmiş; kaynağın kendisinde de orta bölge boştur. Kadranın arkasına bir zemin koymazsan oda arka planı ortadan görünür — istemiyorsan `DialRoot` altına en alta düz renkli bir daire (Image) ekle.
  **v2.2 eki:** Ölçümde görüldü ki yalnızca merkez değil, **çevirmeli diskin boncuklarının (parmak deliklerinin) içi de şeffaf**. Bu aslında doğru: gerçek bir rotary disk delikleri deliktir. Çözüm aynı — `DialBackplate` olarak Unity'nin **yerleşik `Knob` sprite'ı** (dolu daire, motorla gelir, ek varlık gerektirmez) koyu tint'le en alta konur. Deliklerden koyu zemin görünür, doğru görünüm elde edilir.
- **`dial_tier7_quantum_bg`**: Cam çerçevede çok hafif bir doku izi kalmıştır (saydam bezel, kaynakta dama üzerine çizilmiş). Oyun içinde 512 px'e küçülünce fark edilmez; rahatsız ederse bu tek sprite yeniden üretilebilir.

---

## 8. Ölçülmüş Değerler (v2.2)

Aşağıdaki değerler PNG'lerin alfa kanalı piksel piksel taranarak elde edildi
(`System.Drawing` + `LockBits`, alfa eşiği 128). Tahmin değildir; scriptlerde doğrudan kullanılabilir.

### 8.1 Kadran geometrisi

| Ölçüm | Değer |
|---|---|
| `dial_redline_overlay` açısal kaplama | **0° … +89°** (saat 12 → saat 3), tek parça yay |
| `dial_redline_overlay` yarıçap bandı | 0,730 … 0,854 (tuval yarıçapına normalize) |
| `dial_tick_marks` | **360° tam halka** (boşluk yok), r 0,716 … 0,880 |
| `dial_needle` opak oran | **%3,5** (kontur çizimi) |
| `dial_needle` opak bbox | x 14…496, y 2…83 |
| `dial_needle` pivot → uç | **444,8 px** |

**Bundan türetilen ibre süpürmesi:**
```
minAngle = -90°   (saat 9)   -> t = 0
maxAngle = +90°   (saat 3)   -> t = 1
redline  = 0° … +90°         -> t = 0,50 … 1,00
```
`dial_tick_marks` tam halka olduğu için ölçeğin nerede başlayıp bittiğine dair bilgi vermiyor;
süpürme, **redline yayının konumuna göre** simetrik olacak şekilde (altta boşluk) tanımlandı.
GDD v2.2'deki "%15 redline" değeri bu sanat eseriyle uyumsuzdu — GDD v2.3 Bölüm 15.4'e bak.

### 8.2 Kadran yüzü parlaklığı (ibre rengi seçimi için)

| Kademe | Yüz | İbre kontrastı |
|---|---|---|
| 0, 1 | krem/beyaz (245·241·211) | navy kontur okunur |
| 2–5 | orta ton | sınırda |
| 6, 7, 8 | koyu lacivert/siyah, neon cyan tikler | navy kontur **görünmez** → `SpriteSolidTint` şart |

### 8.3 UI 9-slice
Bkz. Bölüm 2 sonundaki tablo.

### 8.4 Ölçümün tekrarı
Bu değerler yeniden doğrulanmak istenirse, alfa taraması `Docs/` dışında tutulan geçici bir
PowerShell scriptiyle yapıldı; yöntem: `Bitmap` → `LockBits(Format32bppArgb)` → alfa baytı (offset +3)
eşiği 128. `GetPixel` ile nokta doğrulaması yapıldı, iki yöntem birebir uyuştu.

---

## 9. İkinci Dalga — Gelişim Ağacı Assetleri (v2.3, 2026-08-14)

`Docs/UPGRADE_TREE.md` ile gelen **32 yeni sprite** bu bölümde ele alınır.
Üretim prompt'ları: **`Docs/ASSET_PROMPTS.md`**.

### 9.1 Neden yeni bir dalga gerekti

Oyunda 6 gerçek yükseltme vardı (kalan 8'i zorunlu bağlantı). Gelişim ağacı 36 karta
çıkarılıyor; her yeni kartın bir ikonu olmalı. Ayrıca oyunun en büyük tasarım açığı olan
**ısı göstergesi** için iki UI parçası gerekiyor.

### 9.2 Dağılım

| Kategori | Adet | Klasör | Öncelik |
|---|---|---|---|
| Yükseltme ikonları (kademe 2-5) | 12 | `UpgradeIcons/` | 1 |
| Yükseltme ikonları (kademe 6-8) | 9 | `UpgradeIcons/` | 2 |
| Prestij yetenek ikonları | 6 | `UpgradeIcons/` | 3 |
| Yetenek ağacı düğüm çerçevesi | 1 | `UI/` | 3 |
| UI parçaları (ısı göstergesi ×2, dosya kartı, rozet) | 4 | `UI/` | **En acil** |
| | **32** | | |

### 9.3 Birinci dalgadan farklı olan tek şey: zemin rengi

Birinci turda "transparent background" istendi ve Gemini **dama desenini gerçek piksel
olarak boyadı** (bkz. Bölüm 0). İkinci dalgada bunun yerine **düz macenta `#FF00FF`**
zemin isteniyor.

**Neden macenta:**
- Hiçbir sprite'ta kullanılmıyor → temiz chroma-key
- Düz beyaz zemin sorunlu olurdu: bazı ikonlarda kırık beyaz dolgu var
  (`icon_conn_adsl` gibi), zeminle karışırdı
- Dama deseni riski tamamen ortadan kalkar

İşlem hattı buna göre bir adım kazanıyor (Bölüm 9.4, adım 1-2).

### 9.4 İşlem hattı — birinci dalgayla aynı, tek ekle

| Adım | İşlem |
|---|---|
| 1 | **Chroma-key** — macenta pikseller alfa=0, tolerans ~%8 |
| 2 | **Halo temizliği** — kenarlardaki macenta saçağını sil |
| 3 | Kırp + ortala |
| 4 | Yeniden boyutlandır (ikon 512², rozet 256², kart 512×420) |
| 5 | Palet sıkıştırma (PNG-8, 256 renk + alfa) |
| 6 | Kodlanan dosyayı geri okuyup alfa sadakatini doğrula |

Adım 3-6 birinci dalgayla birebir aynı; yalnızca 1-2 macentaya göre ayarlandı.

### 9.5 Import ayarları

Bölüm 2 tablosuna eklenen satırlar:

| Dosya | Boyut | Texture Type | Mode | Max Size | Alpha Is Transparency |
|---|---|---|---|---|---|
| `UpgradeIcons/icon_upgrade_*` (21 yeni) | 512×512 | Sprite (2D and UI) | Single | **256** | Açık |
| `UpgradeIcons/icon_prestige_*` (6 yeni) | 512×512 | Sprite (2D and UI) | Single | **256** | Açık |
| `UI/ui_node_frame` | 512×512 | Sprite (2D and UI) | Single | 512 | Açık |
| `UI/ui_heat_gauge` | 512×512 | Sprite (2D and UI) | Single | 512 | Açık |
| `UI/ui_heat_fill` | 512×512 | Sprite (2D and UI) | Single | 512 | Açık |
| `UI/ui_card_file_choice` | 512×420 | Sprite (2D and UI) | Single + **Border** | 512 | Açık |
| `UI/ui_badge_milestone` | 256×256 | Sprite (2D and UI) | Single | 256 | Açık |

Ortak kurallar değişmedi: **PPU 100 · Mip Maps kapalı · Wrap Clamp · Bilinear ·
Read/Write kapalı · Android+iOS ASTC 6×6**.

`Tools/SpeedDownload/1. Apply Sprite Import Settings` **idempotent** — yeni dosyaları
klasöre koyup komutu tekrar çalıştır, mevcut 82'ye dokunmaz.

### 9.6 Isı göstergesinin kurulumu (en kritik yeni asset)

İki parçalı: statik çerçeve + radyal dolgu.

```
DialRoot
 └─ HeatGauge          512×512, kadranla aynı merkez, DialRoot'un ÜSTÜNDE
     ├─ Track          ui_heat_gauge   statik
     └─ Fill           ui_heat_fill    Image Type   = Filled
                                       Fill Method  = Radial 360
                                       Fill Origin  = Left
                                       Clockwise    = true
```

`ui_heat_fill` **düz beyaz** çizilir; renk kodla verilir
(`t<0.5` yeşil · `t<0.8` sarı · `t≥0.8` kırmızı), doluluk
`SpeedController.OverheatProgress` alanından gelir.

İki sprite'ın **yarıçapı ve kalınlığı birebir aynı olmalı** — biri diğerinin içine
oturacak. Üretimden sonra üst üste bindirip kontrol et; kayma varsa dolgu çerçeveden taşar.

### 9.7 Kontrol listesi

Bölüm 6'daki liste geçerli, şu üç madde eklenir:

- [ ] Macenta piksel kalmamış, kenarlarda macenta saçağı yok
- [ ] Kontur rengi **#273548**, kalınlık mevcut ikonlarla aynı
- [ ] Mevcut bir ikonun yanına koyulup göz kontrolü yapıldı — **yamalı durmuyor**

Play Store'a çıkacak bir üründe tek bir farklı stildeki ikon tüm arayüzü amatör gösterir.
Bu son madde diğerlerinden daha önemli.
