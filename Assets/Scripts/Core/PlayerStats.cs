using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Basarimlarin ve gunluk gorevlerin uzerinde durdugu sayaclar.
    ///
    /// NEDEN AYRI BIR SINIF
    /// --------------------
    /// Basarimlar ve gorevler ayni olculere bakiyor ("kac dosya indirdin",
    /// "kac kez tikladin"...). Her biri kendi sayacini tutsaydi ayni olay iki
    /// yerde dinlenir, ikisi zamanla ayrisir ve "gorevde 9/10 ama basarimda
    /// 10/10" gibi aciklanamaz durumlar cikardi. Tek kaynak, tek dogru.
    ///
    /// NEDEN long
    /// ----------
    /// Sayaclar prestijde SIFIRLANMIYOR (basarim "bu hesabin tarihi" demektir).
    /// Uzun oynanislarda tiklama sayisi milyonlari gecebiliyor; int 2,1 milyarda
    /// tasar ve tasan bir sayac basarimlari geri alirdi.
    ///
    /// PARA neden burada degil
    /// -----------------------
    /// Toplam kazanc <see cref="Wallet.LifetimeEarnings"/>'te zaten var ve
    /// double. Onu buraya kopyalamak ikinci bir gercek yaratirdi; para olculeri
    /// dogrudan cuzdandan okunuyor.
    /// </summary>
    public enum StatType
    {
        /// <summary>Tamamlanan dosya sayisi (prestijde sifirlanmaz).</summary>
        FilesDownloaded = 0,

        /// <summary>Oyuncunun elle yaptigi tiklama (otomasyon SAYILMAZ).</summary>
        Clicks = 1,

        /// <summary>Asiri isinma sayisi.</summary>
        Overheats = 2,

        /// <summary>Acil isi tahliyesi kullanimi.</summary>
        Vents = 3,

        /// <summary>Satin alinan yukseltme adedi (toplu alimda adet kadar).</summary>
        UpgradesBought = 4,

        /// <summary>Sonuna kadar izlenen odullu reklam.</summary>
        AdsWatched = 5,

        /// <summary>Yapilan prestij (Hat Degisimi) sayisi.</summary>
        Prestiges = 6,

        /// <summary>Yakalanan yuzen "Sinyal" balonu.</summary>
        SignalsCaught = 7,

        /// <summary>Duselen Holo (nadir) dosya.</summary>
        HoloDrops = 8,

        /// <summary>Cozulen olumsuz olay (Wi-Fi sifresi / kota).</summary>
        EventsResolved = 9,

        /// <summary>Tamamlanan gunluk gorev.</summary>
        QuestsCompleted = 10,

        /// <summary>Ulasilan en yuksek ritmik kombo zinciri.</summary>
        MaxCombo = 11
    }

    /// <summary>
    /// <see cref="StatType"/> sayaclarinin sahibi.
    ///
    /// Olaylara KENDI abone olmuyor: kim neyi sayacagini bilen taraf ilgili
    /// sistemin kendisi (ornegin reklam odulunu yalnizca reklam kodu bilir).
    /// Burasi yalnizca degeri tutar, yayar ve kaydeder — boylece yeni bir olcu
    /// eklemek bu sinifi degistirmiyor.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class PlayerStats : MonoBehaviour
    {
        /// <summary>Sozlukteki en buyuk anahtar + 1. Yeni deger eklenince buyur.</summary>
        public const int Count = 12;

        readonly long[] _values = new long[Count];

        /// <summary>(hangi olcu, yeni deger) — basarim ve gorevler buna abone.</summary>
        public event Action<StatType, long> Changed;

        public long Get(StatType type)
        {
            int i = (int)type;
            return i >= 0 && i < Count ? _values[i] : 0L;
        }

        /// <summary>Sayaci artirir. Negatif veya sifir artis yok sayilir.</summary>
        public void Add(StatType type, long amount = 1L)
        {
            if (amount <= 0L) return;

            int i = (int)type;
            if (i < 0 || i >= Count) return;

            // Tasmaya karsi: uzun oynanislarda deger buyuyebilir ama long'un
            // tavanina yaklasan bir sayac, artik anlamli bir olcu degildir.
            if (_values[i] > long.MaxValue - amount) _values[i] = long.MaxValue;
            else _values[i] += amount;

            if (Changed != null) Changed(type, _values[i]);
        }

        /// <summary>Mevcut degerden daha buyukse sayaci bu degere gunceller (kombo rekoru vb.).</summary>
        public void SetMax(StatType type, long value)
        {
            if (value <= 0L) return;

            int i = (int)type;
            if (i < 0 || i >= Count) return;

            if (value > _values[i])
            {
                _values[i] = value;
                if (Changed != null) Changed(type, _values[i]);
            }
        }

        // ------------------------------------------------------------------
        // Kayit
        //
        // Ad/deger ciftleri olarak yaziliyor, dizi indeksi olarak DEGIL: enum'a
        // ortadan yeni bir deger eklendiginde indeksler kayar ve eski kayitlar
        // yanlis sayaca yuklenirdi ("500 tiklama" birden "500 prestij" olurdu).
        // ------------------------------------------------------------------

        public List<SaveData.StatEntry> ToList()
        {
            var list = new List<SaveData.StatEntry>(Count);

            for (int i = 0; i < Count; i++)
            {
                if (_values[i] <= 0L) continue;
                list.Add(new SaveData.StatEntry { name = ((StatType)i).ToString(), value = _values[i] });
            }

            return list;
        }

        public void Restore(List<SaveData.StatEntry> entries)
        {
            Array.Clear(_values, 0, _values.Length);
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                SaveData.StatEntry e = entries[i];
                if (string.IsNullOrEmpty(e.name) || e.value <= 0L) continue;

                StatType type;
                // Tanimadigimiz ad: eski bir surumden kalmis olabilir, atla.
                if (!TryParse(e.name, out type)) continue;

                _values[(int)type] = e.value;
            }

            // Yukleme sonrasi tek bir toplu haber: dinleyiciler ilerlemeyi
            // bastan hesaplasin. Sayac basina yaymak, acilista onlarca gereksiz
            // basarim kontrolu tetiklerdi.
            if (Changed != null) Changed(StatType.FilesDownloaded, _values[0]);
        }

        static bool TryParse(string name, out StatType type)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!string.Equals(((StatType)i).ToString(), name, StringComparison.Ordinal)) continue;
                type = (StatType)i;
                return true;
            }

            type = StatType.FilesDownloaded;
            return false;
        }
    }
}
