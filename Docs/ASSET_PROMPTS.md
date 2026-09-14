# Asset Üretim Prompt'ları — Gemini

**Sürüm:** 1.0
**Tarih:** 2026-08-14
**Kapsam:** `Docs/UPGRADE_TREE.md` için gereken **31 yeni sprite**
**Kaynak stil:** Mevcut 82 sprite'ın ölçülmüş sanat yönü (bkz. Bölüm 1)

---

## 0. Nasıl Kullanılır

Her prompt iki parçadan oluşur:

```
[STİL BLOĞU]  +  [KONU CÜMLESİ]
```

**Stil bloğu (Bölüm 1) her prompt'ta birebir aynı kalır** — mevcut 82 sprite ile tutarlılık
buna bağlı. Konu cümlesi ise her sprite'a özel (Bölüm 3-6).

> **Neden bu kadar katı:** Play Store'a çıkacak bir oyunda tek bir farklı stildeki ikon
> tüm arayüzü "amatör" gösterir. Mevcut ikonlar kalın navy konturlu, düz dolgulu, gölgesiz.
> Gemini'ye serbest bırakırsan gradient, gölge ve 3D render üretir — o ikonlar diğer 82'nin
> yanında yamalı durur.

### Üretim akışı

1. Bölüm 1'deki stil bloğunu kopyala.
2. Sonuna Bölüm 3-6'dan istediğin konu cümlesini ekle.
3. Gemini'ye ver, **1:1 kare** çıktı iste (UI parçaları hariç — onların oranı Bölüm 6'da).
4. Çıktıyı Bölüm 7'deki işlem hattından geçir.
5. Bölüm 8'deki kontrol listesini uygula.

---

## 1. STİL BLOĞU — Her Prompt'un Başına Kopyala

```
Flat vector icon illustration, retro-modern UI style, 1:1 square canvas.

STYLE RULES (strict):
- Bold uniform outline, deep navy color #273548, consistent stroke weight
  (roughly 12px at 512x512 resolution) on every edge of every shape.
- Flat solid fills ONLY. Absolutely no gradients, no drop shadows, no glow,
  no ambient occlusion, no 3D rendering, no bevel, no texture, no noise.
- Limited palette: neutral gray #A8ACB8 as the main body color, off-white
  #FCFCFC for light surfaces, and blue #4A90E2 used sparingly as a single
  accent on the most important detail only.
- Single centered object filling about 85% of the canvas, generous even margin
  on all four sides. Front-facing or very slight three-quarter view.
- Clean geometric shapes, smooth curves, no hand-drawn wobble, no sketch lines,
  no cross-hatching.
- No text, no numbers, no letters, no logos, no watermark, no signature,
  no sparkle decorations, no corner ornaments.
- Background: solid pure magenta #FF00FF, completely flat and uniform,
  used as a chroma-key layer. Do NOT draw a checkerboard pattern.
  Do NOT place any object or shadow on the background.

SUBJECT:
```

### Neden macenta zemin?

Önceki turda Gemini "transparent background" talimatını yanlış yorumlayıp **dama desenini
gerçek piksel olarak boyamıştı** (bkz. `Asset_Integration_Guide.md` Bölüm 0). Düz beyaz zemin
de sorunlu, çünkü bazı ikonlarda kırık beyaz dolgu var (`icon_conn_adsl` gibi) — zeminle
karışır.

**Macenta (#FF00FF) hiçbir sprite'ta kullanılmıyor**, bu yüzden temiz bir chroma-key
sağlıyor ve alfaya çevirmesi tek satırlık iş.

---

## 2. Referans — Mevcut Stilin Ölçülmüş Özellikleri

Yeni assetlerin uyması gereken, mevcut sprite'lardan çıkarılmış değerler:

| Özellik | Değer |
|---|---|
| Kontur rengi | **#273548** (navy, RGB 39·53·76) |
| Kontur kalınlığı | ~12 px @ 512×512, tüm kenarlarda eşit |
| Ana gövde dolgusu | **#A8ACB8** (nötr gri) |
| Açık yüzey | **#FCFCFC** (kırık beyaz) |
| Vurgu rengi | **#4A90E2** (mavi) — sadece tek bir detayda |
| Gölge / gradient | **Yok** |
| Nesnenin tuvali kaplaması | ~%85, eşit kenar boşluğu |
| Çözünürlük | 512×512 (UI parçaları hariç) |

**Vurgu rengi disiplini:** `icon_upgrade_dns`'te sadece dünya küresi mavi, dişli gri.
`icon_upgrade_fan`'da hiç mavi yok. Yani mavi **zorunlu değil** — ikonun en anlamlı tek
detayında kullan, ya da hiç kullanma. İki renkten fazlası stili bozar.

---

## 3. ÖNCELİK 1 — Kademe 2-5 Kartları (12 ikon)

Oyuncunun ilk yarım saatte göreceği içerik. **Önce bunları üret.**

| # | Dosya adı | Konu cümlesi (stil bloğunun sonuna ekle) |
|---|---|---|
| 1 | `icon_upgrade_amplifier.png` | `A signal amplifier device: a small horizontal rectangular box with a short vertical antenna rising from its top edge, and three concentric curved signal arcs radiating from the antenna tip. Gray body, blue accent on the signal arcs only.` |
| 2 | `icon_upgrade_thermalpaste.png` | `A thermal paste syringe tube lying at a slight diagonal, with a small dollop of paste squeezed out near the nozzle. Gray tube body, off-white paste dollop, blue accent on the plunger cap.` |
| 3 | `icon_upgrade_cat6.png` | `A single ethernet network cable coiled into one smooth loop, with one RJ45 connector plug clearly visible at the end pointing toward the viewer. Gray cable, off-white connector housing, blue accent on the connector clip.` |
| 4 | `icon_upgrade_ups.png` | `An uninterruptible power supply unit: a chunky upright rectangular box with a small battery symbol on its front face and one power button. Gray casing, off-white front panel, blue accent on the battery symbol.` |
| 5 | `icon_upgrade_antenna.png` | `A directional satellite dish antenna on a short tripod stand, angled up and to the right, with a small feed horn at the focal point. Gray dish and stand, blue accent on the feed horn.` |
| 6 | `icon_upgrade_ram.png` | `A single stick of computer RAM memory seen from the front: a horizontal rectangular circuit board with four small chips in a row along it and a notched row of contact pins along the bottom edge. Gray board, off-white chips, blue accent on the contact pin strip.` |
| 7 | `icon_upgrade_compression.png` | `A file folder being compressed by a mechanical vise clamping it from both sides, the folder squeezed narrow in the middle. Gray vise, off-white folder, blue accent on the vise handle.` |
| 8 | `icon_upgrade_adblock.png` | `A shield with a rectangular advertisement banner behind it, the banner crossed out by a single bold diagonal slash. Gray shield, off-white banner, blue accent on the diagonal slash.` |
| 9 | `icon_upgrade_torrent.png` | `Two thick vertical arrows side by side inside a rounded square frame, the left arrow pointing up and the right arrow pointing down, representing upload and download. Gray frame, off-white arrow bodies, blue accent on the upward arrow only.` |
| 10 | `icon_upgrade_ixp.png` | `A network exchange node: one large central circle connected by straight lines to six smaller circles arranged evenly around it. Gray circles and lines, blue accent on the central circle only.` |
| 11 | `icon_upgrade_secondline.png` | `A single telephone cable that splits into two separate cables at a Y-shaped junction, each branch ending in a small phone jack connector. Gray cables, off-white connectors, blue accent on the junction point.` |
| 12 | `icon_upgrade_linemaintenance.png` | `A screwdriver crossed diagonally over a short length of telephone cable, forming an X composition. Gray screwdriver shaft and cable, off-white screwdriver handle, blue accent on the screwdriver tip.` |

> **12 numara neden gerekli:** "Hat Bakımı" yükseltmesi oyunda **var** ama kendi ikonu yok,
> başka bir kartın ikonunu paylaşıyor. Bu bir eksiklik, yeni özellik değil.

---

## 4. ÖNCELİK 2 — Üst Kademe Kartları (9 ikon)

Kademe 6-8 içeriği. Öncelik 1 bittikten sonra üret.

| # | Dosya adı | Konu cümlesi |
|---|---|---|
| 13 | `icon_upgrade_ssd.png` | `An NVMe solid state drive stick seen from the front: a small elongated horizontal circuit board with two flat memory chips and a screw notch at one end. Gray board, off-white chips, blue accent on the connector edge.` |
| 14 | `icon_upgrade_keyboard.png` | `A compact mechanical keyboard seen from a slight three-quarter angle above, showing rows of square keycaps, with one single keycap lifted and floating just above its slot. Gray keyboard body, off-white keycaps, blue accent on the lifted keycap.` |
| 15 | `icon_upgrade_watercooling.png` | `A liquid cooling radiator: a rectangular finned radiator block with two flexible tubes looping out from one side and curving back. Gray radiator fins, off-white tubes, blue accent on the coolant visible in the tubes.` |
| 16 | `icon_upgrade_cpu.png` | `A computer processor chip seen from directly above: a square chip package with a smaller square die in the center and short contact legs extending from all four sides. Gray chip body, off-white legs, blue accent on the central die.` |
| 17 | `icon_upgrade_antivirus.png` | `A shield with a small stylized virus organism in front of it, the virus drawn as a circle with six short spikes. Gray shield, off-white virus body, blue accent on the shield border.` |
| 18 | `icon_upgrade_autoqueue.png` | `A vertical stack of three document files with a circular clockwise arrow looping around the stack. Gray documents, off-white document faces, blue accent on the circular arrow.` |
| 19 | `icon_upgrade_macro.png` | `A terminal window with a title bar and three lines of abstract code shown as simple horizontal bars of varying length, plus a cursor block at the end of the last line. Gray window frame, off-white window body, blue accent on the cursor block. No readable text.` |
| 20 | `icon_upgrade_vpn.png` | `A padlock centered inside a tunnel opening drawn as three nested arch shapes receding into the distance. Gray tunnel arches, off-white padlock body, blue accent on the padlock shackle.` |
| 21 | `icon_upgrade_overclock.png` | `A speedometer gauge with its needle pushed far into the right zone, and a single small flame shape rising from the top edge of the gauge housing. Gray gauge housing, off-white gauge face, blue accent on the needle.` |

> **21 numara için not:** Alev şeklini **mavi** yap, kırmızı değil. Kırmızı yalnızca kadranın
> kendi redline overlay'inde kullanılıyor; ikonlarda kırmızı kullanmak kartı "uyarı" gibi
> gösterir.

---

## 5. ÖNCELİK 3 — Prestij Yetenek Ağacı (6 ikon + 1 çerçeve)

| # | Dosya adı | Konu cümlesi |
|---|---|---|
| 22 | `icon_prestige_quickstart.png` | `A simple rocket seen from the side, tilted diagonally upward to the right, with three short straight speed lines trailing behind it. Gray rocket body, off-white fins, blue accent on the exhaust flame.` |
| 23 | `icon_prestige_archive.png` | `An archive storage box with its lid slightly raised, and a circular wax seal stamp on the front face of the box. Gray box, off-white lid, blue accent on the wax seal.` |
| 24 | `icon_prestige_offline.png` | `A crescent moon with a small downward download arrow positioned in front of its lower curve. Gray crescent moon, blue accent on the download arrow.` |
| 25 | `icon_prestige_interest.png` | `A stack of three coins with a single upward trending arrow rising diagonally from behind the top coin. Gray coins, off-white coin faces, blue accent on the upward arrow.` |
| 26 | `icon_prestige_coldstart.png` | `A six-pointed snowflake centered in front of a small rectangular modem box with one short antenna. Gray modem, off-white modem face, blue accent on the snowflake.` |
| 27 | `icon_prestige_themes.png` | `A painter's color palette shape holding four separate round paint blobs, with a single brush laid diagonally across it. Gray palette board, off-white brush handle, blue accent on one of the four paint blobs.` |

### 27b. Yetenek ağacı düğüm çerçevesi

**`ui_node_frame.png` — 512×512, 1:1**

Bu bir ikon değil, ikonların içine oturacağı **boş çerçeve**. Ortası tamamen şeffaf kalmalı.

```
[STİL BLOĞU]

SUBJECT: An empty hexagonal frame border, drawn as a hexagon outline with a
second slightly smaller hexagon outline nested inside it, forming a double-line
border ring. The entire center area inside the inner hexagon must be pure
magenta #FF00FF background — completely empty, no icon, no fill, no decoration.
Gray frame ring between the two hexagon outlines. Six small circular studs, one
at each corner of the outer hexagon.
```

> **Kritik:** Merkez **şeffaf** olmalı. İşlem hattında macenta alfaya çevrilince ortası
> boşalır ve altına yetenek ikonu konur. Kilitli/açık durumu **kod tarafında tint** ile
> ayrılır (gri = kilitli, mavi = açık) — ikinci bir sprite üretme.

---

## 6. UI PARÇALARI (4 varlık) — Farklı Oran ve Kurallar

Bunlar ikon değil. **Oranları farklı**, stil bloğundaki "1:1 square canvas" ve "single
centered object" satırlarını konuya göre değiştir.

### 6.1 `ui_heat_gauge.png` — Isı Göstergesi Çerçevesi (512×512)

**Projedeki en kritik eksik görsel.** Oyuncu şu an overheat'e ne kadar yakın olduğunu
göremiyor; risk/ödül mekaniğinin tamamı buna bağlı.

```
Flat vector UI element, retro-modern style, 1:1 square canvas, 512x512.

STYLE RULES (strict):
- Bold uniform outline, deep navy #273548, roughly 10px stroke weight.
- Flat solid fills only. No gradients, no shadows, no glow, no 3D.
- Palette: neutral gray #A8ACB8 only. No blue accent on this asset.
- No text, no numbers, no tick labels, no watermark.
- Background: solid pure magenta #FF00FF, completely flat.
  Do NOT draw a checkerboard pattern.

SUBJECT: An empty semicircular gauge track, forming the TOP HALF of a ring only
(a 180 degree arc from the 9 o'clock position, over the top, to the 3 o'clock
position). The arc is a thick hollow channel with a navy outline on both its
inner and outer edge, and gray fill between them. The channel has flat rounded
caps at both ends. The entire center of the circle and the whole bottom half of
the canvas must remain pure magenta background — draw nothing there. The arc
sits near the outer edge of the canvas, leaving a small even margin.
```

### 6.2 `ui_heat_fill.png` — Isı Göstergesi Dolgusu (512×512)

**Aynı yayın dolu hali.** Renk geçişini (yeşil→sarı→kırmızı) kod yapacak, o yüzden
**düz beyaz** çiz.

```
Flat vector UI element, 1:1 square canvas, 512x512.

STYLE RULES (strict):
- NO outline at all on this asset. Solid shape only.
- Completely flat pure white #FFFFFF fill. No gradient, no shadow, no texture.
- Background: solid pure magenta #FF00FF, completely flat.

SUBJECT: A solid semicircular arc band forming the TOP HALF of a ring only
(180 degrees, from the 9 o'clock position over the top to the 3 o'clock
position), with flat rounded caps at both ends. It must sit at exactly the same
radius and thickness as a gauge channel it will be layered inside. The center of
the circle and the entire bottom half of the canvas must remain pure magenta.
```

> **Unity kurulumu:** `Image Type = Filled`, `Fill Method = Radial 360`,
> `Fill Origin = Left`, `Clockwise = true`. Renk `Color` alanından kodla verilir:
> `t<0.5` yeşil → `t<0.8` sarı → `t≥0.8` kırmızı. Doluluk `SpeedController.OverheatProgress`.

### 6.3 `ui_card_file_choice.png` — Dosya Seçim Kartı (512×420)

Üç seçenekli dosya kuyruğu için kart çerçevesi. **9-slice uyumlu olmalı.**

```
Flat vector UI panel, retro-modern style, canvas aspect ratio 512x420 (portrait).

STYLE RULES (strict):
- Bold uniform outline, deep navy #273548, roughly 10px stroke weight.
- Flat solid fills only. No gradients, no shadows, no 3D, no texture.
- Palette: off-white #FCFCFC panel face, neutral gray #A8ACB8 for the header strip.
- No text, no numbers, no icons, no watermark.
- Background: solid pure magenta #FF00FF, completely flat.

SUBJECT: An empty rectangular UI card panel with softly rounded corners,
occupying the full canvas with a small even margin. It has a solid gray
horizontal header strip across its top, separated from the body by a single
navy line. The body below the header is flat off-white and completely empty.
The corners are simple and symmetrical. The border thickness is identical on
all four sides so the panel can be stretched as a nine-slice sprite.
```

> **9-slice:** Üretim sonrası köşe yarıçapını ölç, border değerlerini ona göre gir
> (`Asset_Integration_Guide.md` Bölüm 2 yöntemi). Kenar kalınlığının dört tarafta eşit
> olması **şart** — yoksa kart gerildiğinde köşeler bozulur.

### 6.4 `ui_badge_milestone.png` — Kilometre Taşı Rozeti (256×256)

Kart köşesine oturacak küçük rozet.

```
Flat vector UI badge, retro-modern style, 1:1 square canvas, 256x256.

STYLE RULES (strict):
- Bold uniform outline, deep navy #273548, roughly 8px stroke weight
  (scaled for the smaller 256px canvas).
- Flat solid fills only. No gradients, no shadows, no glow, no 3D.
- Palette: neutral gray #A8ACB8 body, blue #4A90E2 accent on the star only.
- No text, no numbers, no watermark.
- Background: solid pure magenta #FF00FF, completely flat.

SUBJECT: A circular badge with a scalloped notched outer edge like a seal or
medal, with a single five-pointed star centered inside it. Gray badge disc,
blue star. Simple and symmetrical, filling about 90% of the canvas.
```

---

## 7. Üretim Sonrası İşlem Hattı

Ham Gemini çıktısı **doğrudan Unity'ye konmaz**. Mevcut 82 sprite şu hattan geçti,
yeni 31'i de aynısından geçmeli:

| Adım | İşlem | Neden |
|---|---|---|
| 1 | **Chroma-key** — macenta (#FF00FF) pikselleri alfa=0 yap. Tolerans ~%8, kenarlarda yumuşak geçiş bırak | Şeffaflık |
| 2 | **Halo temizliği** — kenarlarda kalan macenta saçağını sil (alfa<255 olan piksellerde renk taşması) | Macenta hale kalmasın |
| 3 | **Kırp + ortala** — opak sınır kutusunu bul, içeriği tuvale ortala | Kademeler arası hizalama |
| 4 | **Yeniden boyutlandır** — ikonlar 512×512, rozet 256×256, kart 512×420 | Hedef çözünürlük |
| 5 | **Palet sıkıştırma** — PNG-8, 256 renk + alfa | Dosya boyutu (394 MB → 2,5 MB oranı) |
| 6 | **Doğrulama** — kodlanan dosyayı geri oku, alfa sadakatini karşılaştır | Sıkıştırma alfayı bozmasın |

> **Sparkle/watermark kontrolü:** Önceki turda Gemini köşelere parıltı izi ve imza koymuştu.
> Her çıktının **dört köşesini** büyütüp kontrol et.

---

## 8. Kontrol Listesi — Her Yeni Sprite İçin

`Asset_Integration_Guide.md` Bölüm 6'daki liste, yeni assetler için:

- [ ] Dosya adı bu dokümandaki isimle **birebir** aynı (scriptler bu adla arayacak)
- [ ] Doğru klasörde (`UpgradeIcons/`, `UI/`)
- [ ] Zemin gerçek alfa kanalı — macenta piksel kalmamış
- [ ] Kenarlarda macenta saçağı/hale yok
- [ ] Kontur rengi #273548, kalınlık diğer ikonlarla aynı
- [ ] Gradient / gölge / 3D efekt yok
- [ ] Mavi vurgu en fazla **tek** detayda
- [ ] Köşelerde sparkle / watermark / imza yok
- [ ] Metin veya rakam yok
- [ ] Macenta zemine bindirip delik kontrolü yapıldı (istenmeyen şeffaf bölge var mı)
- [ ] Mevcut bir ikonun yanına koyup göz kontrolü yapıldı — **yamalı durmuyor**

---

## 9. Unity Import Ayarları — Yeni Assetler

`Asset_Integration_Guide.md` Bölüm 2 tablosuna eklenecek satırlar:

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

`Tools/SpeedDownload/1. Apply Sprite Import Settings` idempotent olduğu için, yeni
dosyaları klasöre koyup komutu tekrar çalıştırman yeterli — mevcut 82'yi bozmaz.

---

## 10. Üretim Özeti

| Öncelik | Adet | İçerik | Ne zaman gerekli |
|---|---|---|---|
| **1** | 12 | Kademe 2-5 yükseltme ikonları | Faz C — ağacın gövdesi |
| **2** | 9 | Kademe 6-8 yükseltme ikonları | Faz D-F |
| **3** | 7 | Prestij ikonları + düğüm çerçevesi | Faz E |
| **UI** | 4 | Isı göstergesi (2), dosya kartı, rozet | **Faz A-B — en acil** |
| | **32** | **toplam** | |

> **Sıralama tavsiyesi:** Isı göstergesinin iki parçasını (`ui_heat_gauge`, `ui_heat_fill`)
> **ilk üret**. Kod tarafı onu beklemeden başlayabilir ama oyunun en büyük tasarım açığı o.
