using System;
using System.Collections.Generic;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Indirilenler Arsivi — oyuncunun tamamladigi BENZERSIZ dosyalarin kaydi.
    ///
    /// Panel (FileCollectionPanelView) Faz 9'da kurulmustu ama icini kimse
    /// doldurmuyordu: sayac hep 0, bonus hep "+%0" yaziyordu. Yarim gorunen bir
    /// ozellik hic olmayandan kotudur — bu sinif eksik yari.
    ///
    /// Koleksiyon prestijde SIFIRLANMAZ: "topladiklarin sende kalir" hissi,
    /// idle oyunlarda oturumu uzatan en ucuz mekaniktir.
    /// </summary>
    [DefaultExecutionOrder(-85)]
    public class CollectionManager : MonoBehaviour
    {
        [Tooltip("Kac benzersiz dosyada bir bonus kademesi kazanilir.")]
        [SerializeField] int filesPerStep = 3;

        [Tooltip("Her bonus kademesinin taban hiza etkisi (0.02 = %2).")]
        [SerializeField] float bonusPerStep = 0.02f;

        [Header("Holo (nadir) dosyalar")]
        [Tooltip("Her tamamlanan dosyanin Holo dusme ihtimali.")]
        [Range(0f, 0.5f)] [SerializeField] float holoChance = 0.05f;

        [Tooltip("Holo dustugunde dosyanin odulunun kac kati ikramiye verilir.")]
        [SerializeField] double holoBonusMultiplier = 5.0;

        readonly HashSet<string> _collected = new HashSet<string>();

        /// <summary>
        /// Holo (parlak/altin) olarak dusmus dosyalar.
        ///
        /// Ayri bir kume: arsivdeki her dosya toplanmis olabilir ama yalnizca
        /// bir kismi Holo. Panel bu kumeye bakip yildiz simgesi basiyor.
        /// </summary>
        readonly HashSet<string> _holo = new HashSet<string>();

        /// <summary>Holo dustu (dosya, verilen ikramiye).</summary>
        public event Action<FileDataSO, double> HoloDropped;

        /// <summary>Yeni bir dosya arsive girdiginde.</summary>
        public event Action<FileDataSO> Discovered;

        /// <summary>Arsiv degistiginde (yukleme dahil) — UI tazelemek icin.</summary>
        public event Action Changed;

        public int CollectedCount { get { return _collected.Count; } }

        /// <summary>Taban hiza uygulanan koleksiyon bonusu (0.06 = %6).</summary>
        public double SpeedBonus
        {
            get
            {
                if (filesPerStep <= 0) return 0.0;
                return (_collected.Count / filesPerStep) * (double)bonusPerStep;
            }
        }

        /// <summary>Bir sonraki bonus kademesine kalan dosya sayisi.</summary>
        public int FilesToNextStep
        {
            get
            {
                if (filesPerStep <= 0) return 0;
                return filesPerStep - (_collected.Count % filesPerStep);
            }
        }

        public bool Has(FileDataSO file)
        {
            return file != null && _collected.Contains(file.name);
        }

        /// <summary>Bu dosya Holo olarak dustu mu? (arsivde yildizli gorunur)</summary>
        public bool IsHolo(FileDataSO file)
        {
            return file != null && _holo.Contains(file.name);
        }

        public int HoloCount { get { return _holo.Count; } }

        GameManager _gm;
        DownloadController _download;

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            _download = _gm.Download;
            if (_download != null) _download.FileCompleted += OnFileCompleted;
        }

        void OnDestroy()
        {
            if (_download != null) _download.FileCompleted -= OnFileCompleted;
        }

        void OnFileCompleted(FileDataSO file, double reward)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) return;
            if (!IsArchivable(file)) return;

            // Holo zari HER tamamlamada atiliyor, yalnizca ilkinde degil.
            //
            // Tek seferlik olsaydi mekanik oyunun ilk yarim saatinde biter,
            // sonrasinda hicbir sey hissettirmezdi. Her indirmede kucuk bir
            // surpriz ihtimali olmasi, arsivi bitirmis oyuncu icin bile
            // indirmeyi izlemeye deger kiliyor.
            bool holo = RollHolo(file, reward);

            bool isNew = _collected.Add(file.name);

            if (isNew)
            {
                // Yeni bir dosya bonus esigini gecirmis olabilir; ekonomi tazelensin.
                if (_gm != null && _gm.Economy != null) _gm.Economy.ApplyStats();
                if (Discovered != null) Discovered(file);
            }

            if (isNew || holo) { if (Changed != null) Changed(); }
        }

        /// <summary>
        /// Holo zari. Dustuyse dosyayi arsivde isaretler ve ikramiye oder.
        /// </summary>
        bool RollHolo(FileDataSO file, double reward)
        {
            if (holoChance <= 0f || UnityEngine.Random.value >= holoChance) return false;

            _holo.Add(file.name);

            // Ikramiye dosyanin KENDI odulune oranli: kademe fark etmeksizin
            // "bu indirme ozeldi" hissi ayni kaliyor. Sabit bir para odulu ust
            // kademelerde gorunmez, alt kademelerde ekonomiyi bozardi.
            double bonus = reward * Math.Max(0.0, holoBonusMultiplier);

            if (bonus > 0.0 && _gm != null && _gm.Money != null) _gm.Money.Add(bonus);
            if (_gm != null && _gm.Stats != null) _gm.Stats.Add(StatType.HoloDrops);

            if (HoloDropped != null) HoloDropped(file, bonus);
            return true;
        }

        /// <summary>
        /// Bu dosya arsive girebilir mi?
        ///
        /// Kademe 9+ mega arsivleri calisma aninda uretiliyor ve her sonsuz
        /// kademe YENI bir ad aliyor (File_Infinite9, File_Infinite10 ...).
        /// Onlari da saymak iki soruna yol aciyordu:
        ///   1. Arsiv bonusu (her 3 dosyada +%2 taban hiz) sinirsiz buyuyordu.
        ///   2. Panel yalnizca sabit dosyalari cizdigi icin sayac "26/23" gibi
        ///      imkansiz degerler gosteriyordu.
        /// Arsiv, elle tasarlanmis dosya koleksiyonudur; uretilen dosyalar
        /// oraya ait degil.
        /// </summary>
        bool IsArchivable(FileDataSO file)
        {
            if (file.isProceduralTemplate) return false;

            GameDatabaseSO db = _gm != null ? _gm.Database : null;
            if (db == null || db.files == null) return true;

            for (int i = 0; i < db.files.Length; i++)
                if (db.files[i] == file) return true;

            return false;
        }

        /// <summary>Holo dosyalarin kayittan geri yuklenmesi.</summary>
        public void RestoreHolo(List<string> names)
        {
            _holo.Clear();
            if (names == null) return;

            for (int i = 0; i < names.Count; i++)
                if (!string.IsNullOrEmpty(names[i])) _holo.Add(names[i]);

            if (Changed != null) Changed();
        }

        /// <summary>Holo listesinin kayit yazimi icin.</summary>
        public List<string> HoloToList()
        {
            return new List<string>(_holo);
        }

        /// <summary>Kayittan yukleme icin.</summary>
        public void Restore(List<string> names)
        {
            _collected.Clear();

            if (names != null)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    string n = names[i];
                    if (string.IsNullOrEmpty(n)) continue;

                    // Eski kayitlarda uretilmis dosyalar birikmis olabilir;
                    // yuklerken de ayikla ki bonus dogru hesaplansin.
                    FileDataSO f = _gm != null && _gm.Database != null ? _gm.Database.FindFile(n) : null;
                    if (f != null && !IsArchivable(f)) continue;

                    _collected.Add(n);
                }
            }

            if (Changed != null) Changed();
        }

        /// <summary>Kayit yazimi icin.</summary>
        public List<string> ToList()
        {
            return new List<string>(_collected);
        }
    }
}
