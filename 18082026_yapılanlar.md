# SpeedDownload Idle Megabit Tycoon — 18.08.2026 Çalışma Raporu

Unity 6000.5.0f1 · URP 2D · IL2CPP / Android

Bu oturumda bildirilen 3 sorun (reklam paneli, kapatma butonu, kayıt güvenliği)
çözüldü; ardından proje baştan sona incelenip **sessizce ölü olan 5 özellik**
daha bulunup canlandırıldı, bir **ekonomi sömürüsü** kapatıldı ve yeni asset'lerin
bozuk import ayarları düzeltildi. Son bölümde soak testi çalışır hale getirildi
ve **Başarımlar / Günlük Görevler / Holo** sistemleri sıfırdan yazıldı.

Derleme temiz, 31 birim testi geçiyor, soak testi 3600/3600 karede sıfır hata,
Play Mode'da uçtan uca doğrulandı.

---

## İçindekiler

1. [Bildirilen sorunlar](#1-bildirilen-sorunlar)
2. [Kayıt (JSON) güvenliği](#2-kayıt-json-güvenliği)
3. [Analizde bulunan ek hatalar](#3-analizde-bulunan-ek-hatalar)
4. [Soak testi — çalışmıyordu](#4-soak-testi--çalışmıyordu)
5. [Yeni sistemler: Başarımlar, Günlük Görevler, Holo](#5-yeni-sistemler-başarımlar-günlük-görevler-holo)
6. [Ölçümler ve doğrulama](#6-ölçümler-ve-doğrulama)
7. [Değişen dosyalar](#7-değişen-dosyalar)
8. [Bilinmesi gerekenler](#8-bilinmesi-gerekenler)
9. [Yapılmayanlar / açık kalanlar](#9-yapılmayanlar--açık-kalanlar)

---

## 1. Bildirilen sorunlar

### 1.1 "Test videosu çıkmıyor" + "kapatma butonu çalışmıyor"

**Belirti:** Ödüllü reklam açılıyor ama ekranda oynayan bir şey yok; kapatma
butonuna basmak hiçbir şey yapmıyor.

**Kök neden — üç ayrı kusur:**

`StubAdService.OnClose()` sayaç dolmadan **koşulsuz geri dönüyordu**:

```csharp
void OnClose()
{
    if (!_rewardGranted) return;   // 30 saniye boyunca HİÇBİR ŞEY
    ...
}
```

30 saniye boyunca butona basmak hiçbir geri bildirim vermiyordu. Üstelik simge
`%35` saydamdı — yani "devre dışı" değil **"bozuk"** görünüyordu. Oyuncu için
bunun adı tam olarak "kapatma butonu çalışmıyor".

İkincisi, `IAdService` sözleşmesinde `onSkipped` var ama **panelden ulaşılamıyordu**:
ödülü reddetmek isteyen oyuncunun beklemek dışında seçeneği yoktu. Gerçek ödüllü
reklamlar böyle çalışmaz — erken kapatabilirsin, ödülü alamazsın.

Üçüncüsü, panelde sabit bir simge ve bir sayıdan başka hiçbir şey yoktu.
Ekranda bir şey **oynamadığı** için oyuncu "reklam çıkmadı" diye okuyordu.

**Çözüm — yeni sözleşme:**

| Süre | Kapatma butonu | Metin |
|---|---|---|
| 0–3 sn | Kilitli (alfa 0,30) | "Kapatmak için 3 sn bekle." + titreşim |
| 3 sn – sayaç sonu | **Atlama** (alfa 0,75) | "REKLAM · şimdi kapatırsan ödülü kaybedersin" |
| Sayaç dolunca | **Ödülü al** (alfa 1,00) | "ÖDÜL HAZIR" → "Kapatarak ödülünü al." |

Ek olarak panele dolan bir ilerleme çubuğu ve nabız atan simge eklendi; sahte
reklam süreleri 30 sn → 15 sn'ye indirildi.

**Kritik nokta:** artık erken basış **sessiz kalmıyor**. Butonun neden
çalışmadığını söylemek, çalışmasını sağlamak kadar önemliydi.

### 1.2 Reklam paneli katman sırası

`SceneBuilder`'daki yorum "Reklam paneli her şeyin üstünde" diyordu ama
`CreateAdPanel`, `CreateSettings` ve `CreateCollection`'dan **önce**
çağrılıyordu — yani UGUI sıralamasında onların **altında** kalıyordu.
Overlay'lerden biri açıkken reklam gösterilse panel arkada kalır ve oyuncunun
ekranında hiçbir şey olmamış gibi görünürdü. Sıra düzeltildi; `AdPanel` artık
Canvas'ın son çocuğu.

### 1.3 AdMob gerçek SDK yolu — çifte geri çağrı ve kalıcı kilit

`ShowReal` içinde SDK'nın hem `OnAdFullScreenContentClosed` hem
`OnAdFullScreenContentFailed` çağrılarını birden yollaması durumunda:

- `ad.Destroy()` iki kez çağrılıyordu,
- çağıran taraf hem ödülü hem "atlandı" haberini alıyordu (boost butonunda:
  ödül verilir, hemen ardından görünüm "atlandı" diye tazelenir).

Ayrıca `ad.Show()` istisna fırlatırsa `_showingReal` sonsuza dek `true` kalıyor,
`IsShowing` hep `true` dönüyor ve **saglayıcı bir daha asla reklam göstermiyordu**.

Tek seferlik `finish` koruması + `Show` çevresine `try/catch` eklendi.

---

## 2. Kayıt (JSON) güvenliği

Üç ayrı açık vardı:

| Açık | Etki |
|---|---|
| İmza kontrolü `saveVersion >= 6 && signature dolu` şartına bağlıydı | **`signature` satırını silmek doğrulamayı tamamen atlatıyordu** |
| İmza yalnızca 7 skaler alanı kapsıyordu | **Tüm yükseltmeleri 9999 yapmak geçerli imzayla geçiyordu** |
| Dosya düz metindi | Not defteriyle açılıp değiştirilebiliyordu |

Ayrıca HMAC anahtarı koda gömülü sabit bir metindi (adı `DeviceSalt` olmasına
rağmen cihaza bağlı değildi) ve yüklemede hiçbir değer doğrulaması yoktu.

### 2.1 `SaveCrypto.cs` (yeni)

AES-256-CBC ile şifreleme, ardından **encrypt-then-MAC** ile HMAC-SHA256 imzası.
Anahtarlar PBKDF2 (20.000 tur) ile üç kaynaktan türetiliyor:

```
uygulama sırrı + kuruluma özel kimlik (PlayerPrefs) + cihaz kimliği
```

Dosya biçimi: `SDMT1:` + `Base64( iv[16] | mac[32] | ciphertext )`

**Neden `SystemInfo.deviceUniqueIdentifier` tek başına değil:** Android'de o
değer işletim sistemi güncellemesiyle değişebiliyor ve değiştiği an oyuncunun
**kendi** kaydı açılamaz hale gelirdi. PlayerPrefs ile kayıt dosyası aynı
uygulama deposunda yaşıyor — biri silinirse diğeri de silinir, yani hep uyumlu
kalırlar. Cihaz kimliği yine karışıma giriyor: kaydı başka telefona kopyalamak
için PlayerPrefs dosyasını da taşımak gerekiyor.

### 2.2 `SaveData.Sanitize()`

Diskten gelen **her** değer kullanılmadan önce makul aralığa çekiliyor. Şifreleme
tek başına yetmez: eski bir kayıt, yarım yazılmış bir dosya veya ileride çıkacak
bir hata da anlamsız değerler üretebilir.

En somut örnek: `eventRemaining = Infinity` gelirse Gece Tarifesi'nin **3x ödül
çarpanı hiç bitmiyordu**.

### 2.3 Katı doğrulama + eski kayıtların taşınması

- v7+ bir kayıt **şifresiz** görünüyorsa "elle yazılmış" kabul edilip reddediliyor
  ve `.corrupt`'a alınıyor (silinmiyor — destek talebinde okunabilsin).
- İmzası geçersiz kayıt **yüklenmiyor**. Eski sürümdeki en büyük açık buydu:
  doğrulama başarısız olsa bile veri kullanılabiliyordu.
- v6 ve öncesi düz JSON kayıtlar bir kez daha okunuyor ve **hemen** şifreli
  biçime taşınıyor (otomatik kaydı beklemek, tam o aralıkta kapatılan uygulamada
  dosyayı korumasız bırakırdı).

### 2.4 Dürüstçe sınırları

Sunucu olmayan bir oyunda anahtar her zaman istemcidedir; yeterince kararlı biri
IL2CPP ikilisini çözüp sırrı çıkarabilir. Amaç mükemmel koruma değil, **gerçekçi**
olan: dosyayı bir metin editörüyle değiştirmeyi imkânsız kılmak ve başka bir
cihazdan kopyalanan kaydı reddetmek. Hile vakalarının neredeyse tamamı bu iki
yoldan geliyor.

`SaveTools` editör aracı da şifrelemeyi bilecek şekilde güncellendi — yoksa
"Rewind Save Clock" düz JSON yazıp geliştiricinin kendi kaydını karantinaya
aldırırdı.

---

## 3. Analizde bulunan ek hatalar

### 3.1 Acil ısı tahliye butonu hiç tıklanamıyordu

`HeatGaugeView.EnsureVentButton()` butonu `DialArea` alt-Canvas'ı içinde
üretiyor. `SceneBuilder` o Canvas'ı **bilerek** `GraphicRaycaster`'sız kuruyor:
kadran katmanlarının hepsi `raycastTarget = false`, dokunuşlar arkadaki
`ClickCatcher`'a geçsin diye. Sonradan buraya eklenen tahliye butonu o kuralın
kurbanı olmuş — ekranda görünüyor, basılıyor, ama tıklama **hiç ulaşmıyordu**.

`EnsureRaycaster()` eklendi. Kadran katmanlarının raycast'i kapalı olduğu için
raycaster eklemek onları etkilemiyor; yalnızca bu buton tıklanabilir hale geliyor.

Canlı raycast testiyle doğrulandı: `hit[0] = EmergencyVentButton`.

### 3.2 `ui_icon_warning_heat` hiç kullanılmıyordu

`warningIcon` alanı ve `_warningSprite` yükleniyordu ama **hiçbir kod onlara
dokunmuyordu** — özellik yarım bırakılmış, görsel boşa üretilmişti.

Gösterge tek dilde konuşuyordu: **renk**. Kırmızı-yeşil renk körlüğü erkeklerin
~%8'inde var; onlar için gösterge sadece "dolan bir yay" ve tehlike eşiği hiç
okunmuyordu. Isı arttıkça hızlanan bir ritimle yanıp sönen uyarı üçgeni devreye
alındı — renkten bağımsız ikinci bir kanal.

### 3.3 Yüzen ödül balonu: görünmez ama tıklanabilir

`FloatingEventController.Build()` içinde sıra tersti:

```csharp
img.sprite = iconSprite;
img.enabled = iconSprite != null;
if (iconSprite == null) iconSprite = Resources.Load<Sprite>(...);  // ÇOK GEÇ
```

`Build()` yalnızca bir kez çalıştığı için sonuç kalıcıydı: sprite bağlanmamışsa
balon sonsuza dek **görünmez ama tıklanabilir** kalıyordu — ekranda dolaşan
görünmez bir düğme. Yükleme öne alındı, ayrıca görünmez balonun hiç ekrana
salınmaması için `Spawn()`'a koruma eklendi.

### 3.4 Maskot asla uyuyamıyordu

`OnFileCompleted` boşta sayacını sıfırlıyordu. Dosyalar oyun açık durdukça
sürekli bittiği için sayaç saniyede birkaç kez sıfırlanıyor, 45 saniyelik eşik
**asla** aşılamıyor ve `mascot_sleeping` görseli hiç görünmüyordu.

"Boşta" ölçüsü oyuncunun dokunmamasıdır, dosya tamamlanması değil. Sayaç artık
`SpeedController.Clicked` olayına bağlı, eşik dokümandaki gibi 60 sn.

### 3.5 Ekonomi sömürüsü: geri alma → bedava prestij kredisi

`EconomyManager.TryUndo()` iadeyi `Wallet.Add()` ile yapıyordu. `Add()` ise
`LifetimeEarnings`'i artırıyor ve prestij kredisi ondan hesaplanıyor:

```
kredi = floor(sqrt(LifetimeEarnings / eşik))
```

Yani **"MAKS al → geri al → tekrar al" döngüsü hiç para kazanmadan toplam kazancı
şişirip bedava Fiber Kredisi üretiyordu.**

Ayrı bir `Wallet.Refund()` eklendi (bakiyeye ekler, toplam kazanca dokunmaz).
Ayrıca `TryUndo`'nun `isFiberCredit` dalı hiç kaydedilmediği için **ölüydü** —
yanlışlıkla 10 kredi harcayan oyuncunun geri dönüşü yoktu; o da bağlandı.

### 3.6 Yeni asset'ler yanlış import edilmişti

`Assets/Resources/` altındaki 12 PNG Unity varsayılanıyla girmiş:

- `SpriteImportMode = Multiple` (tek alt-sprite'a otomatik kırpılmış)
- `maxTextureSize 2048`, Android/iOS ASTC override'ı **yok**
- `ui_speech_bubble` 9-slice olarak kullanılıyor ama border'ı 0

`SpriteImportTool` yalnızca `Assets/Sprites`'ı tarıyordu. Bir aracın "tüm
sprite'ları düzenler" demesi ama bir klasörü atlaması, atlanan klasörün sessizce
bozuk kalması demek. Araç `Resources`'ı da kapsayacak şekilde genişletildi →
**130/130 doku doğrulamadan geçiyor.**

Konuşma balonunu alfa kanalından ölçtüm (1035×512, kuyruk sol altta, gövde alt
kenarı y=73). Bu boyutta 9-slice mantıklı değil — kenarlar çizim genişliğine
yakın. Sprite kendi oranında (`Simple`, 260×129) çiziliyor artık.

### 3.7 Maskot replikleri 5 dilde de Türkçe'ydi

`MascotController` içinde sabit Türkçe diziler duruyordu: Almanca veya Rusça
oynayan biri, oyunun geri kalanı kendi dilindeyken maskotun Türkçe konuştuğunu
görüyordu. 17 replik `LocalizationManager`'a taşındı ve 5 dile yerelleştirildi
(birebir çeviri değil — mizah çevrilmez, **yerelleştirilir**).

### 3.8 `ui_badge_gold_crown` her tazelemede boşa yükleniyordu

`FileCollectionPanelView.Refresh()` her çağrıda `Resources.Load` yapıp sonucu
**hiç kullanmıyordu** — arşiv her değiştiğinde bir yükleme. Tek sefere indirildi
ve rozet gerçekten çiziliyor (tam koleksiyonda başlığın yanında altın taç).

---

## 4. Soak testi — çalışmıyordu

Araç kendini doğru şekilde "GEÇERSİZ" ilan ediyordu ama **hiç çalıştırılamıyordu**:
3600 kare isteğinin ~15'i gerçek kareye dönüşüyordu.

**Sebep pompalama değildi.** `GameBootstrapper.Awake()` açılışta
`Application.runInBackground = false` yazıyor (mobilde pil için doğru bir tercih).
Editör odakta değilken bu ayar oyun döngüsünü tamamen durduruyor —
`QueuePlayerLoopUpdate` çağrıları da boşa gidiyor.

**Çözüm:**
- Soak süresince arka planda çalışma açılıyor, bitince **eski değerine** geri
  konuyor (oyunun pil davranışı bir test aracının yan etkisiyle kalıcı değişmemeli).
- İlerleme artık **gerçek motor karesi** ile ölçülüyor, "istek sayısı" ile değil.
  Eski sayaç hiçbir kare işlenmese bile doluyor ve testin bittiği izlenimini veriyordu.
- Tıklama enjeksiyonu oyun saatine bağlandı (editör tick hızı değişken).

```
[SoakTest] tamamlandi — 3600 kare, 95,3 sn duvar saati
  oyun ici sure   : 125,0 sn
  tamamlanan dosya: 2
  kayit dosyasi   : VAR (autosave calisti)
  gercek motor karesi: 3600 / 3600 hedef
```

Sıfır hata, sıfır uyarı.

> **Not:** Sayılar düşük çünkü soak testi yükseltme satın almıyor, dolayısıyla
> kademe 0'da kalıyor. 0 olay da beklenen: olayların 60 sn'lik başlangıç
> muafiyeti var. Testin değeri "denge ölçmek" değil, **60+ saniye kesintisiz
> oynanışta istisna/sızıntı çıkmaması**.

---

## 5. Yeni sistemler: Başarımlar, Günlük Görevler, Holo

`ui_icon_trophy`, `ui_icon_star_holo` ve `ui_icon_daily_quest` hiçbir koddan
referanslanmıyordu — dokümandaki sistemler yazılmamıştı. Üçü de kuruldu.

### 5.1 `PlayerStats` — tek sayaç kaynağı

Başarımlar ve görevler aynı ölçülere bakıyor ("kaç dosya indirdin", "kaç kez
tıkladın"). Her biri kendi sayacını tutsaydı aynı olay iki yerde dinlenir, ikisi
zamanla ayrışır ve **"görevde 9/10 ama başarımda 10/10"** gibi açıklanamaz
durumlar çıkardı.

11 ölçü, `long` (uzun oynanışlarda tıklama milyonları geçebiliyor; `int` 2,1
milyarda taşar ve taşan sayaç başarımları geri alırdı). Prestijde **sıfırlanmaz**
— başarım "bu hesabın tarihi" demektir.

Kayıt **ad** ile yapılıyor, dizi indeksi ile değil: enum'a ortadan yeni bir değer
eklendiğinde indeksler kayar ve "500 tıklama" birden "500 prestij" olurdu.

### 5.2 Başarımlar (`ui_icon_trophy`)

20 başarım, 6 kategoride. Ödül **Fiber Kredisi** — para vermek üst kademelerde
anlamsız kalırdı (kademe 8'de bir başarımın vereceği para bir saniyelik gelirin
altında olur).

İlerleme **kaydedilmiyor**, sayaçlardan yeniden hesaplanıyor. İkisini birden
yazmak zamanla ayrışabilen iki gerçek yaratırdı.

**En kritik detay:** kayıttan yükleme sırasında sayaçlar tek seferde geri konuyor
ve o anda daha önce açılmış her başarım yeniden "açıldı" sayılırdı — oyuncu her
açılışta aynı kutlamayı ve aynı krediyi tekrar alırdı. Yükleme yolunda ödül
verilmiyor, yalnızca sessiz senkron yapılıyor.

### 5.3 Günlük Görevler (`ui_icon_daily_quest`)

Günde 3 görev, 10'luk havuzdan **gün numarasıyla belirlenimli** seçiliyor.
Kayda görev *tanımı* yazılmıyor, yalnızca "hangi gündeyiz" — böylece kayıt küçük
kalıyor ve görev listesi değiştiğinde eski kayıtlar bozulmuyor.

**Ölçüm gün başındaki değere göre.** "15 dosya indir" görevi toplam sayaca
bakarsa, 900 dosya indirmiş oyuncuda görev daha açılır açılmaz tamamlanmış
görünürdü. Gün başında sayaçların anlık görüntüsü alınıyor, ilerleme fark
üzerinden ölçülüyor.

Diğer kararlar:
- **Ödül boşta gelire oranlı**, sabit para değil (sabit ödül kademe 2'de cömert,
  kademe 8'de görünmez olurdu).
- **Ödül elle alınıyor.** Kendiliğinden verilen ödül, oyuncunun görevi
  tamamladığını fark etmemesi demek. HUD butonunda bekleyen ödül noktası var.
- **Saat geri alınırsa görevler yenilenmiyor** — ileri-geri oynatarak sınırsız
  ödül almak engellendi.
- **"Reklam izle" havuzda bilinçli olarak YOK**: günlük görevi reklama bağlamak,
  isteğe bağlı olması gereken bir şeyi zorunlulaştırırdı (GDD Bölüm 11).

### 5.4 Holo nadir düşüşler (`ui_icon_star_holo`)

Her tamamlanan dosyada %5 şansla Holo düşüyor; ikramiye dosyanın **kendi ödülüne**
oranlı (5x) — kademe fark etmeksizin "bu indirme özeldi" hissi aynı kalıyor.
Arşivde yıldızla işaretleniyor.

Zar her tamamlamada atılıyor, yalnızca ilkinde değil: tek seferlik olsaydı mekanik
oyunun ilk yarım saatinde biter, sonrasında hiçbir şey hissettirmezdi.

### 5.5 UI: tek panel

Kupa düğmesi ust HUD'da (-304, -30), reklam düğmesinin solunda. İkisi ayrı overlay
olabilirdi ama sağ üst köşede beşinci bir düğme parmakla isabet edilemeyecek kadar
sık bir sıra oluştururdu. Üstelik ikisi de "ilerlemem nerede?" sorusuna cevap
veriyor; aynı yerde durmaları doğru.

---

## 6. Ölçümler ve doğrulama

### 6.1 Reklam akışı (Play Mode)

```
Panel acildi          -> showing=True  CanCloseNow=False
Kapatma raycast       -> hit[0] = CloseButton
Erken kapatma         -> showing=True  caption='Kapatmak için 3 sn bekle.'
Sayac dolunca         -> 'ÖDÜL HAZIR'  ilerleme=1,00  alfa=1,00
Kapatma               -> showing=False (panel kapandi, odul verildi)
```

### 6.2 Kayıt güvenliği

```
Dosya:  SDMT1:YNMQBJBNrjqppkpLOrzOl0RzrxV72RSDB7sIvmEW9vq7kmPgC...
'balance' düz metinde geçiyor mu? False
'123456' düz metinde geçiyor mu?  False
Tek bayt değiştirilmiş dosya      -> REDDEDİLDİ (imza geçersiz)
Elle yazılmış 999 milyarlık kayıt -> REDDEDİLDİ, .corrupt'a alındı
Eski düz-JSON v6 kayıt            -> yüklendi (55555) + şifreli biçime taşındı
```

### 6.3 Holo oranı

```
indirilen dosya = 32.480
Holo düşme      = 1.623   (%5,00 — hedef %5)
```

### 6.4 Kalıcılık (kaydet → yeniden yükle)

| Alan | Kayıt öncesi | Yükleme sonrası |
|---|---|---|
| dosya sayacı | 32.481 | 32.481 |
| tıklama | 100 | 100 |
| Holo düşme | 1.623 | 1.623 |
| başarım | 6 | 6 |
| Fiber Kredisi | 17 | **17** (katlanmadı) |
| görev 1 alındı | True | True |

### 6.5 Testler

- **31 birim testi** geçiyor (11 yeni: kripto, sanitize, refund, stats, başarım,
  görev, localization)
- **Soak testi**: 3600/3600 gerçek motor karesi, 0 hata
- **130/130** doku import doğrulamasından geçiyor
- Sahne `VerifyBindings` temiz
- 5 dilde 85 metin anahtarı kontrol edildi, eksik yok

---

## 7. Değişen dosyalar

### Yeni

| Dosya | Amaç |
|---|---|
| `Core/SaveCrypto.cs` | AES-256 + HMAC kayıt şifreleme |
| `Core/PlayerStats.cs` | 11 ölçülük tek sayaç kaynağı |
| `Core/AchievementManager.cs` | 20 başarım |
| `Core/DailyQuestManager.cs` | Günlük 3 görev |
| `UI/TrophyPanelView.cs` | Kupa + görev paneli |
| `UI/QuestBadgeView.cs` | HUD'da bekleyen ödül noktası |

### Değişen

| Dosya | Değişiklik |
|---|---|
| `UI/StubAdService.cs` | Baştan yazıldı: atlama yolu, ilerleme çubuğu, geri bildirim |
| `Core/AdMobService.cs` | Çifte geri çağrı koruması, `Show` try/catch |
| `UI/BoostAdButtonView.cs` | Atlama işleme, görünürlük tazeleme, reklam sayacı |
| `Core/SaveManager.cs` | Şifreleme, eski kayıt taşıma, katı doğrulama, yeni sistemler |
| `Core/SaveData.cs` | v8, `Sanitize()`, `StatEntry`, yeni alanlar |
| `Core/Wallet.cs` | `Refund()`, NaN/Infinity korumaları |
| `Core/EconomyManager.cs` | Undo sömürüsü, fiber undo, yükseltme sayacı |
| `Core/PrestigeManager.cs` | `RefundCredits()`, prestij sayacı |
| `Core/CollectionManager.cs` | Holo sistemi |
| `Core/SpeedController.cs` | Tıklama/ısınma/tahliye sayaçları |
| `Core/GameEventManager.cs` | Çözülen olay sayacı |
| `Core/DownloadController.cs` | Kalıcı dosya sayacı |
| `Core/FloatingEventController.cs` | Sprite sırası, görünmez balon koruması, sinyal sayacı |
| `Core/GameManager.cs` | Yeni sistem referansları |
| `Core/LocalizationManager.cs` | `TF` null koruması, maskot replikleri, kupa metinleri |
| `UI/HeatGaugeView.cs` | Raycaster düzeltmesi, uyarı ikonu, `OnDestroy` |
| `UI/MascotController.cs` | Uyku düzeltmesi, yerelleştirme, balon oranı |
| `UI/FileCollectionPanelView.cs` | Taç rozeti, Holo yıldızı, ölü yükleme |
| `UI/OfflineReportView.cs`, `UI/EventBannerView.cs` | Reklam sayacı |
| `Editor/SceneBuilder.cs` | Reklam paneli sırası + ilerleme çubuğu, kupa paneli, doğrulama |
| `Editor/SpriteImportTool.cs` | `Resources` klasörü kapsama |
| `Editor/SaveTools.cs` | Şifreleme farkındalığı, kurtarma dosyası silme, kimlik sıfırlama |
| `Editor/SoakTestTool.cs` | `runInBackground`, gerçek kare ölçümü |
| `Editor/Tests/UnitTests.cs` | 11 yeni test |
| `Assets/Resources/*.png.meta` | 12 dosya: Single mod, doğru boyut, ASTC |
| `Assets/Scenes/Game.unity` | Yeniden kuruldu |

---

## 8. Bilinmesi gerekenler

### 8.1 Kayıt sürümü 8

Eski kayıtlar (v6 düz JSON, v7 şifreli) sorunsuz yükleniyor ve yeni biçime
taşınıyor. **v7+ bir kayıt şifresiz görünürse reddediliyor** — bu bilinçli:
"düz JSON yaz, imzayı boş bırak" hilesi kalıcı bir arka kapı olurdu.

### 8.2 Şifreleme kimliği PlayerPrefs'te

`sd_install_id` ve `sd_save_salt` anahtarları silinirse **mevcut kayıt açılamaz**.
Bu kasıtlı (başka cihazın kaydını reddetmenin yolu bu), ama test ederken dikkat:
`Tools/SpeedDownload/Save/Reset Encryption Identity` bunu bilerek yapıyor.

### 8.3 Yeni bir başarım/görev eklerken

- **Başarım kimliği kayda yazılıyor — ASLA değiştirme.** Değiştirilirse oyuncunun
  o başarımı kilitlenir.
- Yeni bir `StatType` eklerken `PlayerStats.Count` artırılmalı.
- Yeni bir maskot repliği eklerken hem sözlüğe satır hem `LocalizationManager`
  içindeki sayaca artış gerekiyor.

### 8.4 `LocalizationManager.TF` artık hiçbir koşulda istisna fırlatmıyor

Bu, bu oturumda bulunan **en sinsi hataydı**. `TF` null desende
`ArgumentNullException` fırlatıyordu (kod yalnızca `FormatException` yakalıyordu).
O çeviri çağrısı bir UI tazelemesinin içindeydi, tazeleme de kayıt yüklemesinin
tetiklediği bir olaydan geliyordu — istisna yığını tırmanıp **`SaveManager.Load()`'u
yarıda kesiyordu** ve sonrasındaki her şey yüklenmeden kalıyordu.

Çeviri bir metin işidir; hiçbir koşulda oyunun durumunu bozmamalı. Regresyon
testi eklendi.

---

## 9. Yapılmayanlar / açık kalanlar

### 9.1 Kupa panelinin görsel yerleşimi gözle kontrol edilmedi

`TrophyPanelView` satırları çalışma zamanında üretiyor. Mantık ve bağlantılar
doğrulandı (3 görev satırı, 20 başarım satırı, metinler doluyor) ama panel
düzenini ekranda görmedim — Unity'yi ön plana alamadığım için görüntü alamadım.
**Özellikle görev bloğu ile başarım listesi arasındaki boşluğu bir kez gözle
kontrol edin.**

### 9.2 Gerçek AdMob yolu yalnızca cihazda doğrulanabilir

Editörde `UNITY_EDITOR` tanımlı olduğu için gerçek SDK dalı derlenmiyor; sahte
panele devrediliyor. `ShowReal` düzeltmeleri (çifte geri çağrı, `Show` istisnası)
kod incelemesiyle yapıldı, **APK'da test edilmedi**.

Yayına çıkmadan önce `AdMobService.AndroidRewardedUnitId` ve
`GoogleMobileAdsSettings` içindeki App ID **mutlaka** gerçek değerlerle
değiştirilmeli — ikisi de test kimliğiyle yayınlamak Google'ın hesabı askıya
almasıyla sonuçlanır.

### 9.3 Soak testi denge ölçmüyor

Test yükseltme satın almadığı için kademe 0'da kalıyor. İstisna/sızıntı
avlamak için iyi, **denge doğrulamak için değil**. Kademe atlayan bir soak
senaryosu ileride eklenebilir.

### 9.4 Dokümandaki diğer asset'ler

`16082026.md` içindeki `mascot_rage_laser`, `mascot_sleeping`,
`mascot_sweating_panic`, `ui_speech_bubble`, `ui_icon_undo`,
`ui_icon_vent_emergency`, `ui_icon_warning_heat`, `ui_badge_gold_crown`,
`event_overdrive_flame` — **hepsi artık kullanımda.** Dokümanda listelenip de
kullanılmayan asset kalmadı.

---

*Rapor: 18.08.2026*
