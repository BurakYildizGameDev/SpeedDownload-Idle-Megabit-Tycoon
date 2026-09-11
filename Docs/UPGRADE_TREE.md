# Gelişim Ağacı Revizyonu — "Yetersiz" Hissinin Çözümü

**Sürüm:** 1.0
**Tarih:** 2026-08-14
**Kaynak:** `Docs/GDD.md` (v2.3), `Docs/PLAN.md` (v2.0)
**Hedef:** 6 gerçek yükseltme → **36 yükseltme + kilometre taşı sistemi**

---

## 0. Teşhis — Neden Yetersiz Hissettiriyor

Mevcut durumun sert sayıları:

| Ölçüm | Şu an | Olması gereken |
|---|---|---|
| Gerçek yükseltme (bağlantı hariç) | **6** | 30+ |
| Yükseltme türü çeşidi | 6 tip, hepsi düz artış | En az 12 tip, farklı davranışlar |
| İlk 10 dakikada görülen yeni kart | ~4 | 10-12 |
| Oyuncunun verdiği strateji kararı | **0** | Her kademede en az 1 |
| Otomasyon (elle yapmayı bırakma) | **Yok** | 3 kademeli |
| Prestij karşılığı | Sadece +%2 gelir/kredi | Yetenek ağacı |

**Kök sebep:** Yükseltmelerin hepsi aynı şeyi yapıyor — bir sayıyı büyütüyor. Hiçbiri
oyunun *nasıl oynandığını* değiştirmiyor. Oyuncu 3. kademede tüm içeriği görmüş oluyor
ve kalan 6 kademe boyunca aynı 6 kartı tekrar tekrar satın alıyor.

**Çözüm iki katmanlı:**
1. **Genişlik** — 36 yükseltme, her kademede 3-4 yenisi açılır (Bölüm 2-5).
2. **Derinlik** — kilometre taşı sistemi, mevcut yükseltmelerin ömrünü 10x uzatır (Bölüm 6).

---

## 1. Tasarım Kuralları

Bu ağacın uyduğu kurallar — yeni yükseltme eklerken bunlara sadık kal:

1. **Her kademe atlamada en az 2 yeni kart belirir.** Oyuncu bağlantıyı satın aldığında
   ödülü sadece "daha büyük sayı" değil, *yeni seçenekler* olmalı.
2. **Her sekmenin bir kimliği var.** Donanım = risk/ısı yönetimi. Yazılım = çarpan ve
   otomasyon. Altyapı = taban hız ve kapasite.
3. **Her kademede en az bir yükseltme oyunun kuralını değiştirir**, sadece sayıyı değil.
   (otomasyon, paralel slot, olay bağışıklığı gibi)
4. **Maliyet eğrisi `baseCost × 1.15^n`** olarak kalır — mevcut `UpgradeSO.CostAtLevel`
   değişmez. Yalnızca `baseCost` ve `maxLevel` yeni kartlara göre ayarlanır.
5. **Sınırlı seviyeli yükseltmeler bitmeli.** `maxLevel > 0` olanlar oyuncuya "bunu
   tamamladım" hissi verir; sınırsız olanlar para deposu görevi görür. Oran ~60/40.

---

## 2. DONANIM Sekmesi — Risk ve Isı Yönetimi

Fiziksel modem/PC parçaları. Tıklama gücü ve overheat ekonomisi burada.

| # | Yükseltme | Tip | Etki / seviye | Baz maliyet | Maks sv. | Kilit |
|---|---|---|---|---|---|---|
| H1 | **Modeme Vurmak** *(mevcut)* | ClickPower | +%15 tıklama gücü | $4 | ∞ | 0 |
| H2 | **Harici Fan** *(mevcut)* | OverheatTolerance | +0,5 sn redline süresi | $60 | 10 | 1 |
| H3 | **Sinyal Yükselteci** | ClickPower | +%25 tıklama gücü | $450 | 15 | 2 |
| H4 | **Isı Macunu** | OverheatPenaltyReduction | Overheat cezası −%12 | $900 | 6 | 2 |
| H5 | **Cat6 Kablo** | PassiveSpeed | +%20 taban hız | $2.5K | 8 | 3 |
| H6 | **Kesintisiz Güç (UPS)** | OverheatSoftFail | Overheat'te hız 0 yerine %25 | $18K | 3 | 4 |
| H7 | **Yönlü Anten** | RedlineBonus | Redline çarpanı +0,25 | $40K | 4 | 4 |
| H8 | **RAM Yükseltmesi** | ParallelSlots | +1 eşzamanlı dosya slotu | $850K | 2 | 5 |
| H9 | **NVMe SSD** | CompletionBonus | Dosya bitişinde +%8 ekstra ödül | $3M | 10 | 6 |
| H10 | **Mekanik Klavye** | AutoClick | Saniyede +0,5 otomatik tıklama | $25M | 8 | 6 |
| H11 | **Sıvı Soğutma** | OverheatTolerance | +1,5 sn redline süresi | $900M | 5 | 7 |
| H12 | **Kuantum İşlemci** | ClickPower | +%100 tıklama gücü | $50B | ∞ | 8 |

**Kritik kartlar:** H6 (UPS) ve H8 (RAM) oyunun kuralını değiştirir — biri overheat
cezasını yumuşatır, diğeri paralel indirme açar. H10 ilk otomasyon adımı.

---

## 3. YAZILIM Sekmesi — Çarpan ve Otomasyon

Ödül çarpanları, olay direnci, otomasyon. Oyunun "artık elle yapmıyorum" anları burada.

| # | Yükseltme | Tip | Etki / seviye | Baz maliyet | Maks sv. | Kilit |
|---|---|---|---|---|---|---|
| S1 | **DNS Ayarı** *(mevcut)* | CritChance | +%5 kritik şansı | $250 | 5 | 1 |
| S2 | **Premium Sunucu** *(mevcut)* | RewardMultiplier | +%20 dosya ödülü | $1.2K | ∞ | 2 |
| S3 | **Download Manager** *(mevcut)* | OfflineCapacity | Offline kazanç kademesi | $2K | 2 | 3 |
| S4 | **Sıkıştırma Algoritması** | FileSizeReduction | Dosya boyutu −%6 | $6K | 8 | 3 |
| S5 | **Reklam Engelleyici** | EventResistance | Olumsuz olay şansı −%15 | $30K | 4 | 4 |
| S6 | **Torrent İstemcisi** | SeedIncome | Biten her dosya %2 pasif gelir verir | $500K | 10 | 5 |
| S7 | **Antivirüs** | VirusImmunity | Virüs olayına karşı %30 koruma | $2M | 3 | 5 |
| S8 | **Otomatik Kuyruk** | QueueAutomation | Dosyaları otomatik seçer (sv.2: en kârlıyı) | $15M | 2 | 6 |
| S9 | **Makro Script** | AutoClick | Otomatik tıklama hızı +%40 | $200M | 6 | 6 |
| S10 | **VPN Tüneli** | QuotaBypass | Kota olayı hızı yarıya indirmez | $5B | 1 | 7 |
| S11 | **Overclock Aracı** | RedlineThreshold | Redline eşiği −0,04 (bölge genişler) | $80B | 5 | 7 |
| S12 | **Yapay Zekâ Optimizer** | RewardMultiplier | +%75 dosya ödülü | $2T | ∞ | 8 |

**Kritik kartlar:** S6 (Torrent) idle gelirin belkemiği olur — koleksiyon sistemiyle
birleşince "oyunu açık bırakmaya değer" hissi doğar. S8 ikinci otomasyon adımı.

---

## 4. ALTYAPI Sekmesi — Bağlantı ve Kapasite

Mevcut 8 bağlantı korunur; aralarına nefes aldıran kartlar eklenir. Şu anki sorun:
bu sekmede oyuncu yalnızca "sıradaki bağlantıyı biriktiriyor", arada yapacak bir şey yok.

| # | Yükseltme | Tip | Etki / seviye | Baz maliyet | Maks sv. | Kilit |
|---|---|---|---|---|---|---|
| I1 | **Hat Bakımı** *(mevcut)* | PassiveSpeed | +%10 taban hız | $80 | ∞ | 0 |
| I2 | **Bağlantı Kademeleri 1-8** *(mevcut)* | Connection | Kademe açar | türetilir | 1 | sırayla |
| I3 | **Yerel Değişim Noktası (IXP)** | PassiveSpeed | +%35 taban hız | $12K | 6 | 3 |
| I4 | **İkinci Hat** | ParallelSlots | +1 eşzamanlı dosya slotu | $2M | 1 | 5 |
| I5 | **Uydu Yedeklemesi** | OfflineCapacity | Offline tavanı +4 saat | $80M | 4 | 6 |
| I6 | **Omurga Peering** | PassiveSpeed | +%80 taban hız | $10B | ∞ | 7 |

---

## 5. PRESTİJ Sekmesi — Fiber Kredisi Yetenek Ağacı

**Bu sekme şu an sadece bir buton.** Prestijin tek karşılığı kredi başına +%2 gelir —
kimseyi ikinci tura sokmaz. Fiber Kredisi ile satın alınan **kalıcı** yetenekler:

| # | Yetenek | Etki | Maliyet (FK) | Maks sv. |
|---|---|---|---|---|
| P1 | **Hızlı Başlangıç** | Prestij sonrası kademe 2'den başla (sv.2: kademe 3) | 3 | 2 |
| P2 | **Kalıcı Arşiv** | Koleksiyon bonusu prestijde korunur | 5 | 1 |
| P3 | **Offline Ustası** | Offline verimi +%25 | 4 | 4 |
| P4 | **Kredi Faizi** | Kazanılan Fiber Kredisi +%20 | 8 | 5 |
| P5 | **Soğuk Başlangıç** | Overheat toleransı kalıcı +1 sn | 6 | 3 |
| P6 | **Kadran Temaları** | Kozmetik — 4 alternatif kadran rengi | 10 | 1 |

> **Prestij eşiğini düşür:** Şu an kademe 8 gerekiyor (`prestigeUnlockTier`). **Kademe 6'ya
> çek.** Oyuncu döngüyü bir kere görmeden bırakırsa prestij sistemi hiç var olmamış demektir.

---

## 6. Kilometre Taşı Sistemi — Bedava Derinlik

**Bu, listedeki en yüksek getirili madde ve tek bir yeni sprite gerektirmiyor.**

Her yükseltmenin belirli seviyelerinde etkisi ikiye katlanır:

```
Seviye 10  -> o yükseltmenin etkisi ×2
Seviye 25  -> ×4  (kümülatif)
Seviye 50  -> ×8
Seviye 100 -> ×16
```

Neden bu kadar önemli:

- **Sınırsız yükseltmeler bir anda anlamlı hale gelir.** Şu an "Modeme Vurmak" 40. seviyede
  39. seviyeden farksız hissettiriyor. Kilometre taşıyla oyuncunun gözü hep bir sonraki
  eşikte olur.
- **İçerik üretmeden içerik yaratır.** 12 sınırsız yükseltme × 4 eşik = 48 yeni "an".
- **Kart üzerinde ilerleme çubuğu** (sv. 7/10) oyuncuya sürekli bir hedef gösterir.

Uygulama: `UpgradeSO`'ya `milestoneLevels = [10,25,50,100]` ve `milestoneMultiplier = 2`
alanları; `EconomyManager.TotalEffect` hesabında seviyeye göre çarpan uygulanır.

### 6.1 Sabit eşiğin yarattığı kusur — 2026-08-30 düzeltmesi

Yukarıdaki `[10,25,50,100]` **sabit** eşik listesi, tavanı bu sayıların altında olan
kartlarda kırıldı. Tarama sonucu 5 kart bozuk çıktı:

| Kart | Tavan | Eski eşikler | Sonuç |
|---|---|---|---|
| `Upg_HatBakimi` | 20 | 10, ~~25~~, ~~50~~, ~~100~~ | üçü **asla varılamaz** hedef |
| `Upg_SinyalYukselteci` | 15 | 10, ~~25, 50, 100~~ | aynı |
| `Upg_HariciFan` | 10 | 10, ~~25, 50, 100~~ | tek eşik **son seviyede** — ödülün tadı çıkmıyor |
| `Upg_NvmeSSD` | 10 | 10, ~~25, 50, 100~~ | aynı |
| `Upg_TorrentIstemcisi` | 10 | 10, ~~25, 50, 100~~ | aynı |

Sebep `5. Migrate Upgrade Assets` aracıydı: eşikleri sabit veriyor, tavanı 10'un altında
olan karta sistemi **tamamen kapatıyordu**. Bu yüzden 41 kartın 31'inde kilometre taşı
kapalıydı — yani "bedava derinlik" olarak tasarlanan sistem çoğunlukla ölüydü.

Doğru çözüm sistemi kapatmak değil, **eşiği karta uydurmaktı**:

| Kart | Yeni eşikler | Çarpan | Tavanda toplam |
|---|---|---|---|
| `Upg_HatBakimi` | 7, 14 | ×1.6 | ×2.56 |
| `Upg_SinyalYukselteci` | 5, 10 | ×1.6 | ×2.56 |
| `Upg_HariciFan` / `NvmeSSD` / `TorrentIstemcisi` | 4, 8 | ×1.6 | ×2.56 |

Çarpan 2.0 yerine 1.6: **iki** çekim noktası oluşuyor (eskiden bir) ama güç bütçesi
patlamıyor. `5. Migrate` aracı da artık eşikleri tavanın içine kırpıyor — aynı hatayı
yeni kartlarda tekrar üretemez.

---

## 6.2 Eğri Karakterleri — Düzlüğün Asıl Kaynağı

Kilometre taşlarından daha derin bir sorun vardı: **seviyelenen 33 kartın `costGrowth`
değeri birebir aynıydı (1.15).**

Neden bu bir tasarım hatası:

```
etki(n)    = n × effectPerLevel × kilometreÇarpanı     (additif)
maliyet(n) = baseCost × costGrowth^n                    (üssel)
```

Bir kartın "para başına etki" verimi `r^n` ile söner. **Tüm kartlarda `r` aynı olduğunda
kartların birbirine göre sırası asla değişmez** — oyuncu `effectPerLevel / baseCost`
oranına göre sabit bir sırayı takip eder. Bu bir strateji değil, bir bölme işlemidir.
41 kart, davranış olarak 41 kez gösterilen 1 karta döner.

`r` farklılaştığında sıra **zaman içinde değişir**: yüksek `r`'li kart erken güçlü olup
sonra fiyattan düşer, düşük `r`'li kart başta zayıfken sonra en iyi alım hâline gelir.
Aranan doku budur.

### Arketipler

| Arketip | `r` | Karakter | Kime |
|---|---|---|---|
| **Sprint** | 1.30 | ucuz başlar, 3-4 seviyede fiyattan düşer | düşük tavanlı yardımcı kartlar |
| **Standart** | 1.20 | dengeli | orta tavanlı iş gücü kartları |
| **Dayanıklı** | 1.14 | dönüp dönüp alınır | yüksek tavanlı kartlar |
| **Omurga** | 1.12 | hiç bitmez | sınırsız kartlar — uzun kuyruğu bunlar taşır |
| **Sabit** | 1.15 | dokunulmaz | tavanı 1-2 olan kartlar (`r` matematiksel olarak etkisiz) |

Sonuç: **1 benzersiz `r` → 9.**

### Kalibrasyon — sayılar tahmin değil, ölçüm

`Tools/SpeedDownload/5b. Report Upgrade Curves` kuru raporu üç turda ayarlandı:

1. **Omurga 1.10 denendi** → K=1e6 göreceli bütçede toplam güç **+%148**. Sebep bileşik:
   düşük `r` daha çok seviye → daha çok kilometre eşiği aşılıyor → her eşik ayrıca ×2.
2. **Omurga 1.12'ye çekildi** → seviye çarpanı 1.47'den 1.24'e indi.
3. **Bütçe süpürümü** yapıldı ve kalan +%130'un enflasyon değil **faz farkı** olduğu
   görüldü — boşluk büyümüyor, salınıyor:

   | K | 1e5 | 1e6 | 3e6 | 1e7 | 1e8 | 1e9 |
   |---|---|---|---|---|---|---|
   | kayma | +%20 | +%130 | +%133 | +%21 | +%20 | +%21 |

   Yeni eğri seviye-100 uçurumunu daha erken geçiyor; eski eğri de geçince fark kapanıyor.
   **Kalıcı kayma ~+%20**, kart başı maliyet kayması **medyan +%0**.

> ⚠ Tek bir K'ya bakmak faz farkını enflasyon sanmana yol açar. Sabit değiştirmeden önce
> `5b`'yi çalıştır ve medyan ile K sütunlarının **tamamına** birlikte bak.

### Aynı tip + aynı tier çakışması

Açgözlü alım simülasyonu ikinci bir düzlük yakaladı: aynı `UpgradeType` ve aynı tier
bandındaki kartlara aynı arketip verilince o bantta düzlük geri geliyordu.

| Tip | Kartlar | Düzeltme |
|---|---|---|
| `ClickPower` | `ModemeVurmak` (∞) vs `SinyalYukselteci` (tavan 15) | 1.14 vs **1.18** |
| `PassiveSpeed` | `Cat6Kablo` (tavan 8) vs `IXP` (tavan 6) | 1.20 vs **1.26** |

`Verify Upgrade Curves` bu çakışmayı ve ulaşılamaz eşiği artık denetliyor.

### Araçlar

| Komut | İş |
|---|---|
| `5b. Report Upgrade Curves (dry run)` | sapma raporu — hiçbir şey yazmaz |
| `5c. Apply Upgrade Curve Archetypes` | arketipleri uygular (idempotent) |
| `Verify Upgrade Curves` | plandan sapma + ulaşılamaz eşik denetimi |

Politikanın sahibi `UpgradeBalanceTool.BuildPlan()`. **Yeni kart eklersen plana da ekle** —
`Verify` eksik kartı hata olarak bildirir.

---

## 7. Gereken Yeni `UpgradeType` Değerleri

Mevcut enum'a eklenecekler (`Assets/Scripts/Data/UpgradeSO.cs`):

```csharp
OverheatPenaltyReduction = 7,   // Isı Macunu
OverheatSoftFail         = 8,   // UPS — overheat'te tam kesinti yerine kısmi hız
RedlineBonus             = 9,   // Anten — redline çarpanını artırır
RedlineThreshold         = 10,  // Overclock — redline eşiğini düşürür
ParallelSlots            = 11,  // RAM / İkinci Hat — eşzamanlı dosya
CompletionBonus          = 12,  // SSD — dosya bitişinde ekstra ödül
AutoClick                = 13,  // Klavye / Makro — otomatik tıklama
FileSizeReduction        = 14,  // Sıkıştırma
SeedIncome               = 15,  // Torrent — pasif gelir
EventResistance          = 16,  // Reklam engelleyici
VirusImmunity            = 17,  // Antivirüs
QuotaBypass              = 18,  // VPN
QueueAutomation          = 19,  // Otomatik kuyruk
```

Ayrıca `UpgradeTab` enum'una `Prestige = 3` eklenir (sprite zaten var: `ui_tab_prestige`).

---

## 8. Sprite İhtiyaç Listesi

Format kuralı: **512×512, şeffaf PNG**, mevcut `UpgradeIcons/` klasöründeki 13 ikonun
stiliyle aynı (retro-modern, kalın kontur, sınırlı palet).

### 8.1 Öncelik 1 — Bunlar olmadan başlayamayız (12 ikon)

Kademe 2-5 arasını dolduran kartlar. Oyuncunun ilk 30 dakikada göreceği içerik.

| Dosya adı | Ne çizilecek |
|---|---|
| `icon_upgrade_amplifier.png` | Sinyal yükselteci — anten sembollü küçük kutu |
| `icon_upgrade_thermalpaste.png` | Isı macunu tüpü |
| `icon_upgrade_cat6.png` | Turuncu/mavi ethernet kablosu, RJ45 uçlu |
| `icon_upgrade_ups.png` | Kesintisiz güç kaynağı — pil sembollü kutu |
| `icon_upgrade_antenna.png` | Yönlü anten / çanak |
| `icon_upgrade_ram.png` | RAM çubuğu (yeşil PCB, altın pinler) |
| `icon_upgrade_compression.png` | ZIP klasörü / sıkıştırılmış dosya, mengene |
| `icon_upgrade_adblock.png` | Kalkan + engellenmiş reklam banner'ı |
| `icon_upgrade_torrent.png` | Yukarı/aşağı ok çifti (seed/leech) |
| `icon_upgrade_ixp.png` | Ağ düğümü — birbirine bağlı noktalar |
| `icon_upgrade_secondline.png` | İkiye ayrılan telefon hattı |
| `icon_upgrade_linemaintenance.png` | Hat bakımı — tornavida + kablo *(mevcut kartın kendi ikonu yok)* |

### 8.2 Öncelik 2 — Üst kademeler (9 ikon)

| Dosya adı | Ne çizilecek |
|---|---|
| `icon_upgrade_ssd.png` | NVMe SSD çubuğu |
| `icon_upgrade_keyboard.png` | Mekanik klavye (otomasyon hissi) |
| `icon_upgrade_watercooling.png` | Sıvı soğutma radyatörü + hortum |
| `icon_upgrade_cpu.png` | Kuantum işlemci — parlayan çip |
| `icon_upgrade_antivirus.png` | Kalkan + böcek/virüs sembolü |
| `icon_upgrade_autoqueue.png` | Dosya listesi + dönen ok |
| `icon_upgrade_macro.png` | Script/terminal penceresi |
| `icon_upgrade_vpn.png` | Tünel + kilit |
| `icon_upgrade_overclock.png` | Kadran ibresi kırmızıda + alev |

### 8.3 Öncelik 3 — Prestij ağacı (6 ikon + 1 çerçeve)

| Dosya adı | Ne çizilecek |
|---|---|
| `icon_prestige_quickstart.png` | Roket / hızlı ileri sembolü |
| `icon_prestige_archive.png` | Arşiv kutusu + kalıcılık mührü |
| `icon_prestige_offline.png` | Ay / uyku sembolü + indirme oku |
| `icon_prestige_interest.png` | Fiber kredisi sembolü + yukarı ok |
| `icon_prestige_coldstart.png` | Kar tanesi + modem |
| `icon_prestige_themes.png` | Palet / renk çemberi |
| `ui_node_frame.png` | **Yetenek ağacı düğüm çerçevesi** — 512×512, kilitli/açık için tek çerçeve (renk kodla ayrılır) |

### 8.4 UI — Yeni mekanikler için (4 varlık)

Bunlar ikon değil, arayüz parçası. **En kritik olan ilki.**

| Dosya adı | Boyut | Ne çizilecek |
|---|---|---|
| `ui_heat_gauge.png` | 512×512 şeffaf | **Isı göstergesi** — kadranın etrafını saran yarım halka, boş hali. Doldurma `Image Type = Filled/Radial` ile kodla yapılacak, o yüzden **tek parça boş halka** yeterli |
| `ui_heat_fill.png` | 512×512 şeffaf | Aynı halkanın dolu hali (yeşil→sarı→kırmızı tint kodla verilir, **beyaz/gri çiz**) |
| `ui_card_file_choice.png` | 512×420 | Dosya seçim kartı çerçevesi — 3 seçenek yan yana duracak, 9-slice uyumlu |
| `ui_badge_milestone.png` | 256×256 şeffaf | Kilometre taşı rozeti — yıldız/mühür, kart köşesine oturur |

> **Not:** `ui_icon_lock.png` zaten var, kilitli kartlarda tekrar kullanılacak.
> Yeni sprite gerekmiyor.

### 8.5 Sprite gerekmeyen işler

Şunlar için **hiçbir görsel üretme** — kod ve mevcut varlıklarla çözülüyor:

- Kilometre taşı sistemi (rozet dışında)
- Otomatik tıklama
- Paralel dosya slotları (mevcut progress bar çoğaltılır)
- Prestij eşiğinin düşürülmesi
- Torrent pasif geliri
- Overheat cezası azaltma

---

## 9. Uygulama Sırası

| Faz | İş | Sprite gerekir mi |
|---|---|---|
| **A** | Isı göstergesini HUD'a ekle | `ui_heat_gauge` + `ui_heat_fill` |
| **B** | Kilometre taşı sistemi + kart üzerinde ilerleme | `ui_badge_milestone` |
| **C** | Yeni `UpgradeType`'lar + Donanım/Yazılım kademe 2-5 kartları | Öncelik 1 (12 ikon) |
| **D** | Otomasyon (AutoClick, QueueAutomation) | Öncelik 2'den 3 ikon |
| **E** | Prestij yetenek ağacı + eşiği kademe 6'ya çek | Öncelik 3 |
| **F** | Paralel slotlar, torrent, üst kademe kartları | Öncelik 2 kalanı |

**A ve B sprite beklemeden başlayabilir** — A için geçici olarak beyaz bir halka
kullanılabilir, B zaten çoğunlukla kod.

---

## 10. Beklenen Sonuç

| Ölçüm | Önce | Sonra |
|---|---|---|
| Gerçek yükseltme | 6 | **30** (+8 bağlantı +6 prestij) |
| Yükseltme türü | 6 | **19** |
| İlk 10 dk'da yeni kart | ~4 | **11** |
| Oyunun kuralını değiştiren yükseltme | 0 | **7** |
| Sınırsız yükseltmede hedef eşiği | yok | **4 kilometre taşı** |
| Ulaşılamaz kilometre taşı gösteren kart | 5 | **0** |
| Benzersiz maliyet eğrisi (`costGrowth`) | 1 | **9** |
| Aynı tip + tier'da eğri çakışması | 4 kart | **0** |
| Prestij karşılığı | tek çarpan | **6 yetenek** |
