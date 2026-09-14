using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Diske yazilan durum (PLAN Bolum 7.4).
    ///
    /// JsonUtility Dictionary serilestiremedigi icin yukseltmeler ad/seviye
    /// ciftlerinden olusan bir liste. Anahtar olarak asset adi kullaniliyor —
    /// dizideki sira degisse bile kayit bozulmaz.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Migration icin. Format degisirse artir.</summary>
        public int saveVersion = CurrentVersion;

        /// <summary>
        /// v2: fiberCredits (Faz 7). v3: aktif olay (Faz 8).
        /// v4: koleksiyon arsivi (Faz 16). v5: paralel indirme slotlari.
        /// v6: govde ici HMAC imzasi (artik kullanilmiyor, bkz. asagisi).
        /// v7: dosya butunuyle sifreli ve imzali (<see cref="SaveCrypto"/>);
        ///     govde ici <see cref="signature"/> alani terk edildi.
        /// v8: oyuncu sayaclari, basarimlar, gunluk gorevler, Holo dosyalar.
        /// Eski kayitlar sorunsuz yuklenir; eksik alanlar varsayilan gelir.
        /// </summary>
        public const int CurrentVersion = 8;

        /// <summary>
        /// Sifreleme oncesi son surum. Bu surume kadar olan kayitlar duz JSON
        /// olarak diskte durur ve bir kez daha okunup yeni bicime tasinir.
        /// </summary>
        public const int LastPlaintextVersion = 6;

        /// <summary>Son kayit ani (UTC tick). Offline kazanc bundan hesaplanir.</summary>
        public long lastSaveUtcTicks;

        /// <summary>
        /// ESKI govde ici imza (v6). Artik yazilmiyor ve dogrulanmiyor:
        /// dosyanin TAMAMI <see cref="SaveCrypto"/> tarafindan imzalaniyor.
        ///
        /// Alan yalnizca eski kayitlarin JsonUtility ile sorunsuz okunabilmesi
        /// icin duruyor — kaldirilmasi zararsiz olurdu ama tutmak da bedava.
        /// </summary>
        public string signature = "";

        // --- ekonomi ---
        public double balance;
        public double lifetimeEarnings;

        // --- ilerleme ---
        public int currentTierIndex;
        public int highestTierReached;

        // --- prestij (v2) ---
        public int fiberCredits;

        // --- aktif indirme ---
        public string currentFileName = "";
        public double fileProgressBits;
        public int completedCount;

        // --- aktif olay (v3) ---
        // Engeller kayda yazilmasaydi oyuncu uygulamayi kapatip acarak
        // cezadan kacabilirdi.
        public int activeEvent;
        public float eventRemaining;
        public int eventRemainingTaps;
        public double eventQuotaFee;

        // --- koleksiyon (v4) ---
        // Tamamlanmis benzersiz dosya adlari. Prestijde sifirlanmaz.
        public List<string> collectedFiles = new List<string>();

        // --- paralel indirme slotlari (v5) ---
        //
        // Ana dosyanin ilerlemesi bastan beri kaydediliyordu ama RAM / Ikinci
        // Hat yukseltmesiyle acilan EK slotlar kaydedilmiyordu: her acilista
        // hepsi sifirdan basliyordu. Ust kademelerde bir slot dakikalarca
        // dolduguna gore bu, oyuncunun her oturum kapatisinda sessizce kaybettigi
        // gercek bir kazancti — ustelik yukseltmenin tek isi buydu.
        public List<SlotEntry> slots = new List<SlotEntry>();

        // --- yukseltmeler ---
        public List<UpgradeEntry> upgrades = new List<UpgradeEntry>();

        // --- oyuncu sayaclari (v8) ---
        //
        // Basarimlarin ve gunluk gorevlerin dayandigi olculer. Prestijde
        // SIFIRLANMAZ: basarim "bu hesabin tarihi" demektir, tek bir turun degil.
        public List<StatEntry> stats = new List<StatEntry>();

        // --- basarimlar (v8) ---
        // Yalnizca ACILMIS olanlarin kimligi yaziliyor; ilerleme sayaclardan
        // yeniden hesaplaniyor. Ikisini birden yazmak, zamanla ayrisabilen iki
        // gercek yaratirdi.
        public List<string> achievements = new List<string>();

        // --- Holo (nadir) dosyalar (v8) ---
        // Arsivde yildizla isaretlenen dosyalarin adlari.
        public List<string> holoFiles = new List<string>();

        // --- gunluk gorevler (v8) ---
        //
        // Gorevler gunun kendisinden turetiliyor (bkz. DailyQuestManager), bu
        // yuzden gorev TANIMLARI kaydedilmiyor — yalnizca hangi gundeyiz,
        // gun basindaki sayac degerleri ve hangileri odullendirildi.
        //
        // Baslangic degerleri sart: gorev "5 dosya indir" ise, olcum gun
        // basindaki degere GORE yapilmali; toplam sayaca bakmak dunku ilerlemeyi
        // bugunku goreve sayardi ve gorevler acilir acilmaz tamamlanmis gorunurdu.
        public int questDayNumber;
        public List<StatEntry> questBaseline = new List<StatEntry>();
        public List<int> questsClaimed = new List<int>();

        // ------------------------------------------------------------------
        // Yukleme dogrulamasi
        //
        // Sifreleme dosyayi degistirmeyi zorlastiriyor ama TEK basina yetmez:
        // eski bir kayit, yarim yazilmis bir dosya veya ileride cikacak bir
        // hata da anlamsiz degerler uretebilir. NaN / Infinity / negatif sayilar
        // ekonomiye girdiginde sonuc sessiz ve teshisi imkansiz bozulmalar
        // oluyordu — ornegin eventRemaining sonsuz gelirse Gece Tarifesi'nin
        // 3x odul carpani hic bitmiyordu.
        //
        // Bu yuzden diskten gelen HER deger, kullanilmadan once burada makul bir
        // araliga cekiliyor.
        // ------------------------------------------------------------------

        /// <summary>Kayittan gelebilecek en yuksek yukseltme seviyesi.</summary>
        public const int MaxUpgradeLevel = 100000;

        /// <summary>Listelerin kabul edilen en buyuk uzunlugu (bellek sismesine karsi).</summary>
        public const int MaxListEntries = 512;

        /// <summary>
        /// Diskten okunan degerleri guvenli araliklara ceker. Yukleme yolunda
        /// HER ZAMAN cagrilmali.
        /// </summary>
        public void Sanitize()
        {
            if (saveVersion < 1) saveVersion = 1;

            lastSaveUtcTicks = ClampTicks(lastSaveUtcTicks);

            balance = SafePositive(balance);
            lifetimeEarnings = SafePositive(lifetimeEarnings);

            // Toplam kazanc bakiyeden kucuk olamaz: prestij kredisi toplam
            // kazanca bakiyor, tutarsiz bir cift onu ucretsiz krediye cevirirdi.
            if (lifetimeEarnings < balance) lifetimeEarnings = balance;

            currentTierIndex = Mathf.Clamp(currentTierIndex, 0, 9999);
            highestTierReached = Mathf.Clamp(highestTierReached, 0, 9999);
            if (highestTierReached < currentTierIndex) highestTierReached = currentTierIndex;

            fiberCredits = Mathf.Clamp(fiberCredits, 0, int.MaxValue - 1);

            fileProgressBits = SafePositive(fileProgressBits);
            completedCount = Mathf.Max(0, completedCount);

            if (currentFileName == null) currentFileName = "";

            // --- aktif olay ---
            if (activeEvent < 0 || activeEvent > (int)GameEventType.HappyHour) activeEvent = 0;

            eventRemaining = SafeSeconds(eventRemaining, 3600f);
            eventRemainingTaps = Mathf.Clamp(eventRemainingTaps, 0, 999);
            eventQuotaFee = SafePositive(eventQuotaFee);

            // --- listeler ---
            if (upgrades == null) upgrades = new List<UpgradeEntry>();
            if (slots == null) slots = new List<SlotEntry>();
            if (collectedFiles == null) collectedFiles = new List<string>();

            TrimTo(upgrades, MaxListEntries);
            TrimTo(slots, MaxListEntries);
            TrimTo(collectedFiles, MaxListEntries);

            for (int i = upgrades.Count - 1; i >= 0; i--)
            {
                UpgradeEntry e = upgrades[i];
                if (string.IsNullOrEmpty(e.name) || e.level <= 0) { upgrades.RemoveAt(i); continue; }

                e.level = Mathf.Clamp(e.level, 1, MaxUpgradeLevel);
                upgrades[i] = e;
            }

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                SlotEntry s = slots[i];
                if (string.IsNullOrEmpty(s.fileName)) { slots.RemoveAt(i); continue; }

                s.progressBits = SafePositive(s.progressBits);
                slots[i] = s;
            }

            for (int i = collectedFiles.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(collectedFiles[i])) collectedFiles.RemoveAt(i);

            // --- v8: sayaclar, basarimlar, Holo, gunluk gorevler ---
            if (stats == null) stats = new List<StatEntry>();
            if (achievements == null) achievements = new List<string>();
            if (holoFiles == null) holoFiles = new List<string>();
            if (questBaseline == null) questBaseline = new List<StatEntry>();
            if (questsClaimed == null) questsClaimed = new List<int>();

            TrimTo(stats, MaxListEntries);
            TrimTo(achievements, MaxListEntries);
            TrimTo(holoFiles, MaxListEntries);
            TrimTo(questBaseline, MaxListEntries);
            TrimTo(questsClaimed, MaxListEntries);

            SanitizeStats(stats);
            SanitizeStats(questBaseline);

            for (int i = achievements.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(achievements[i])) achievements.RemoveAt(i);

            for (int i = holoFiles.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(holoFiles[i])) holoFiles.RemoveAt(i);

            // Gun numarasi gelecege ayarlanirsa gorevler sonsuza dek "bugun
            // yapildi" sayilir ve bir daha hic yenilenmezdi.
            if (questDayNumber < 0 || questDayNumber > MaxDayNumber) questDayNumber = 0;

            for (int i = questsClaimed.Count - 1; i >= 0; i--)
                if (questsClaimed[i] < 0 || questsClaimed[i] > 64) questsClaimed.RemoveAt(i);
        }

        /// <summary>Bugunden makul olcude ileri bir gun numarasi ust siniri.</summary>
        static int MaxDayNumber
        {
            get { return (int)(DateTime.UtcNow.AddDays(2) - new DateTime(2020, 1, 1)).TotalDays; }
        }

        static void SanitizeStats(List<StatEntry> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                StatEntry e = list[i];
                if (string.IsNullOrEmpty(e.name) || e.value < 0L) { list.RemoveAt(i); continue; }
                list[i] = e;
            }
        }

        static void TrimTo<T>(List<T> list, int max)
        {
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
        }

        /// <summary>NaN / sonsuz / negatif degerleri 0'a cevirir.</summary>
        static double SafePositive(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0) return 0.0;
            return value;
        }

        static float SafeSeconds(float value, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return 0f;
            return Mathf.Min(value, max);
        }

        /// <summary>
        /// Tick degerini DateTime'in kabul ettigi araliga ceker.
        ///
        /// Aralik disinda bir deger <c>new DateTime(ticks)</c> cagrisinda
        /// ArgumentOutOfRangeException firlatiyor; offline hesabi bu istisnayla
        /// cokerse yukleme yarida kalirdi.
        /// </summary>
        static long ClampTicks(long ticks)
        {
            if (ticks < 0L) return 0L;
            return ticks > DateTime.MaxValue.Ticks ? 0L : ticks;
        }

        [Serializable]
        public struct UpgradeEntry
        {
            public string name;
            public int level;
        }

        /// <summary>
        /// Bir oyuncu sayaci. Dizi indeksi degil AD kaydediliyor: enum'a ortadan
        /// yeni bir deger eklendiginde indeksler kayar ve "500 tiklama" birden
        /// "500 prestij" olurdu.
        /// </summary>
        [Serializable]
        public struct StatEntry
        {
            public string name;
            public long value;
        }

        /// <summary>Bir ek indirme slotunun dosyasi ve birikmis ilerlemesi.</summary>
        [Serializable]
        public struct SlotEntry
        {
            public string fileName;
            public double progressBits;
        }
    }

    /// <summary>Offline kazanc hesabinin sonucu — rapor panelinin gosterecegi veri.</summary>
    public struct OfflineResult
    {
        /// <summary>Gercekte gecen sure (sn).</summary>
        public double elapsedSeconds;

        /// <summary>Tavana kirpildiktan sonra kazanca sayilan sure (sn).</summary>
        public double creditedSeconds;

        public double earnings;

        /// <summary>Download Manager seviyesi (0 = yukseltme yok, temel verim).</summary>
        public int managerLevel;

        /// <summary>Uygulanan verim orani (0-1). Rapor panelinde gosteriliyor.</summary>
        public double efficiency;

        /// <summary>Uygulanan tavan (saat).</summary>
        public double capHours;

        public bool IsPro { get { return managerLevel >= 2; } }

        public bool HasEarnings { get { return earnings > 0.0; } }

        /// <summary>
        /// Sure tavana takildi mi? (UI "tavan doldu" diyebilsin)
        ///
        /// Kazanc sarti onemli: Download Manager alinmamissa creditedSeconds 0
        /// kalir ve bu sadece sureye bakan bir kontrolde "tavan doldu" gibi
        /// gorunurdu — oysa sebep tavan degil, yukseltmenin hic olmamasi.
        /// </summary>
        public bool WasCapped
        {
            get { return HasEarnings && elapsedSeconds > creditedSeconds + 1.0; }
        }
    }
}
