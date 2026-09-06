using System;
using System.Collections.Generic;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Dosya indirme dongusu (GDD Bolum 5).
    ///
    ///     etkinHiz     = SpeedController.EffectiveSpeed * SpeedMultiplier
    ///     ilerlemeBit += etkinHiz * dt
    ///     kalanSure    = (boyut - ilerleme) / etkinHiz
    ///
    /// Dosya bitince odul cuzdana yazilir ve havuzdan yeni dosya secilir.
    /// Bir onceki dosya art arda tekrar gelmez (GameDatabaseSO.PickRandomFile).
    /// </summary>
    public class DownloadController : MonoBehaviour
    {
        // --- disaridan (olaylar, prestij, yukseltmeler) beslenen carpanlar ---

        /// <summary>Happy hour / kota / komsu Wi-Fi gibi hiz carpanlari (Faz 8).</summary>
        public double SpeedMultiplier { get; set; }

        /// <summary>Kalici odul carpani: premium sunucu + prestij. EconomyManager yazar.</summary>
        public double RewardMultiplier { get; set; }

        /// <summary>
        /// Gecici odul carpani: Gece Tarifesi (GDD Bolum 8). GameEventManager yazar.
        /// Kalici carpandan ayri tutuluyor ki iki sistem ayni alani ezmesin.
        /// </summary>
        public double EventRewardMultiplier { get; set; }

        /// <summary>Odule uygulanan nihai carpan.</summary>
        public double TotalRewardMultiplier
        {
            get { return RewardMultiplier * EventRewardMultiplier; }
        }

        // --- okunur durum ---

        /// <summary>
        /// Sikistirma Algoritmasi'ndan gelen boyut carpani (Faz 13).
        /// 1,0 = ham boyut · 0,7 = %30 kucuk. EconomyManager yazar.
        /// </summary>
        public double FileSizeMultiplier { get; set; }

        public FileDataSO CurrentFile { get; private set; }

        public double ProgressBits { get; private set; }

        /// <summary>Indirilecek fiili boyut — sikistirma uygulanmis hali.</summary>
        public double CurrentSize
        {
            get
            {
                if (CurrentFile == null) return 0.0;
                return CurrentFile.sizeBits * FileSizeMultiplier;
            }
        }

        public double RemainingBits
        {
            get { return Math.Max(0.0, CurrentSize - ProgressBits); }
        }

        public float Progress01
        {
            get
            {
                double size = CurrentSize;
                if (size <= 0.0) return 0f;
                return Mathf.Clamp01((float)(ProgressBits / size));
            }
        }

        /// <summary>Su anki hizla kalan sure (sn). Hiz sifirsa PositiveInfinity.</summary>
        public double EstimatedSeconds
        {
            get
            {
                double speed = EffectiveSpeed;
                if (speed <= 0.0) return double.PositiveInfinity;
                return RemainingBits / speed;
            }
        }

        /// <summary>
        /// Yuzen "Sinyal Yakalandi" balonundan gelen gecici hiz carpani.
        ///
        /// <see cref="SpeedMultiplier"/>'dan AYRI tutuluyor: onun sahibi
        /// GameEventManager ve olay bitince 1.0'a geri yaziyor. Ikisi ayni alani
        /// paylassaydi biten bir olay, devam eden bir boost'u sessizce silerdi.
        /// </summary>
        public double BoostMultiplier { get; set; }

        /// <summary>
        /// Odullu reklamdan gelen gecici hiz carpani (1 = boost yok).
        ///
        /// <see cref="BoostMultiplier"/>'dan AYRI: onun sahibi yuzen balon
        /// olayi ve suresi bitince 1.0'a geri yaziyor. Ayni alani paylassalardi
        /// once biten, digerinin devam eden boost'unu sessizce silerdi — bu
        /// projede ayni tuzaga SpeedMultiplier/BoostMultiplier ikilisinde de
        /// dusulmus ve ayni sekilde cozulmustu.
        /// </summary>
        public double AdBoostMultiplier { get; set; }

        /// <summary>Indirmeye fiilen uygulanan hiz (bit/sn).</summary>
        public double EffectiveSpeed
        {
            get
            {
                return _speed != null
                    ? _speed.EffectiveSpeed * SpeedMultiplier * BoostMultiplier * AdBoostMultiplier
                    : 0.0;
            }
        }

        /// <summary>Bu oturumda tamamlanan dosya sayisi.</summary>
        public int CompletedCount { get; private set; }

        // --- olaylar ---

        public event Action<FileDataSO> FileStarted;

        /// <summary>Tamamlanan dosya ve verilen odul.</summary>
        public event Action<FileDataSO, double> FileCompleted;

        SpeedController _speed;
        Wallet _wallet;
        GameDatabaseSO _database;
        int _tierIndex;

        // Cok yuksek hizlarda tek karede yuzlerce dosya bitebilir. Kare basina
        // tamamlama sayisini sinirlamak donmayi engeller; artan bit bir sonraki
        // kareye devreder, yani kazanc kaybolmaz.
        const int MaxCompletionsPerFrame = 32;

        void Awake()
        {
            SpeedMultiplier = 1.0;
            BoostMultiplier = 1.0;
            AdBoostMultiplier = 1.0;
            RewardMultiplier = 1.0;
            EventRewardMultiplier = 1.0;
            FileSizeMultiplier = 1.0;
        }

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[DownloadController] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            _speed = gm.Speed;
            _wallet = gm.Money;
            _database = gm.Database;
            _tierIndex = gm.CurrentTierIndex;

            gm.TierChanged += OnTierChanged;
            StartNextFile();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TierChanged -= OnTierChanged;
        }

        void OnTierChanged(ConnectionTierSO tier)
        {
            if (tier == null) return;
            _tierIndex = tier.tierIndex;

            // Yeni kademede artik cikmayan bir dosyayi indirmeye devam etmek
            // anlamsiz olurdu (kademe 8'den 0'a dusunce dosya asla bitmezdi).
            if (CurrentFile == null || !CurrentFile.IsAvailableAt(_tierIndex))
                StartNextFile();
        }

        // ------------------------------------------------------------------
        // Paralel indirme (Faz 16)
        //
        // Ikincil slotlar ekranda GORUNMEZ: ust HUD tek dosya gostermek uzere
        // kurulu ve oraya ikinci bir cubuk sikistirmak paneli bogardi. Oyuncu
        // kazanci yuzen "+$" yazisindan ve gelir hizindan zaten goruyor.
        //
        // Her ek slot tam hizda degil, parallelEfficiency katinda calisir —
        // aksi halde iki slot geliri birebir ikiye katlar ve diger tum
        // yukseltmeleri anlamsizlastirirdi.
        // ------------------------------------------------------------------

        /// <summary>Ek indirme slotu sayisi (RAM / Ikinci Hat). EconomyManager yazar.</summary>
        public int ExtraSlots { get; set; }

        [Tooltip("Ek slotlarin ana hiza gore verimi.")]
        [SerializeField] double parallelEfficiency = 0.6;

        class Slot
        {
            public FileDataSO file;
            public double progress;
        }

        readonly List<Slot> _slots = new List<Slot>();

        void UpdateParallelSlots(double baseSpeed, float dt)
        {
            // Slot sayisini yukseltmeye gore esitle.
            while (_slots.Count < ExtraSlots) _slots.Add(new Slot());
            while (_slots.Count > ExtraSlots) _slots.RemoveAt(_slots.Count - 1);

            if (_slots.Count == 0) return;

            double slotSpeed = baseSpeed * parallelEfficiency;
            if (slotSpeed <= 0.0) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];

                if (s.file == null || !s.file.IsAvailableAt(_tierIndex))
                {
                    s.file = PickForSlot(s.file);
                    s.progress = 0.0;
                    if (s.file == null) continue;
                }

                s.progress += slotSpeed * dt;

                int guard = 0;
                double size = s.file.sizeBits * FileSizeMultiplier;

                while (size > 0.0 && s.progress >= size && guard++ < MaxCompletionsPerFrame)
                {
                    s.progress -= size;

                    double reward = s.file.baseReward * TotalRewardMultiplier;
                    if (_wallet != null) _wallet.Add(reward);

                    CompletedCount++;
                    CountStat();
                    if (FileCompleted != null) FileCompleted(s.file, reward);

                    s.file = PickForSlot(s.file);
                    if (s.file == null) break;

                    size = s.file.sizeBits * FileSizeMultiplier;
                }
            }
        }

        // ------------------------------------------------------------------
        // Torrent geliri (Faz 16)
        //
        // Tamamlanan dosyalari paylasmak, bosta gelirin bir yuzdesi kadar ek
        // kazanc saglar — oyunu acik birakmayi anlamli kilan mekanik.
        //
        // Saniyede bir toplu ekleniyor: her karede Wallet.Add cagirmak
        // BalanceChanged olayini tetikler ve o da tum yukseltme kartlarini
        // yeniden cizerdi.
        // ------------------------------------------------------------------

        /// <summary>Torrent Istemcisi orani (0.20 = bosta gelirin %20'si). EconomyManager yazar.</summary>
        public double SeedIncomeRate { get; set; }

        float _seedTimer;

        void TickSeedIncome(float dt)
        {
            if (SeedIncomeRate <= 0.0 || _wallet == null) return;

            _seedTimer += dt;
            if (_seedTimer < 1f) return;

            double amount = EstimateIdleIncomePerSecond() * SeedIncomeRate * _seedTimer;
            _seedTimer = 0f;

            if (amount > 0.0) _wallet.Add(amount);
        }

        // ------------------------------------------------------------------
        // Slot durumunun kaydedilmesi
        //
        // SaveManager'in kayit bicimine bagimli kalmamak icin (Restore ile ayni
        // desen) duz indeksli erisim veriliyor.
        // ------------------------------------------------------------------

        /// <summary>Su an var olan ek slot sayisi (kayit yazimi icin).</summary>
        public int ActiveSlotCount { get { return _slots.Count; } }

        /// <summary>Bozuk/kotu niyetli bir kayitin bellek sismesine yol acmamasi icin.</summary>
        const int MaxRestorableSlots = 64;

        public FileDataSO GetSlotFile(int index)
        {
            return index >= 0 && index < _slots.Count ? _slots[index].file : null;
        }

        public double GetSlotProgress(int index)
        {
            return index >= 0 && index < _slots.Count ? _slots[index].progress : 0.0;
        }

        /// <summary>
        /// Kayittan yukleme: bir ek slotun dosyasini ve ilerlemesini geri koyar.
        ///
        /// Slot listesi gerekiyorsa buyutulur. Sayiyi ExtraSlots'a esitlemek
        /// <see cref="UpdateParallelSlots"/>'un isi: yukseltme sonradan
        /// dusuruldiyse fazla slotlar bir sonraki karede zaten kirpilir.
        /// </summary>
        public void RestoreSlot(int index, FileDataSO file, double progressBits)
        {
            if (index < 0 || index >= MaxRestorableSlots) return;

            while (_slots.Count <= index) _slots.Add(new Slot());

            Slot s = _slots[index];
            s.file = file;
            s.progress = progressBits > 0.0 && !double.IsNaN(progressBits) ? progressBits : 0.0;
        }

        FileDataSO PickForSlot(FileDataSO previous)
        {
            if (_database == null) return null;

            return QueueAutomationLevel >= 1
                ? _database.PickBestFile(_tierIndex, QueueAutomationLevel >= 2 ? null : previous)
                : _database.PickRandomFile(_tierIndex, previous);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Torrent geliri kadranin O ANKI hizindan BAGIMSIZ: tamamlanmis
            // dosyalari paylasmanin karsiligi bosta gelire baglaniyor
            // (EstimateIdleIncomePerSecond), indirmenin ne kadar hizli gittigine
            // degil. Eskiden asagidaki iki erken donusun ARDINDA duruyordu:
            //   - overheat sirasinda hiz 0'a dustugu icin (UPS alinmadiysa)
            //     torrent geliri de tamamen kesiliyordu,
            //   - kademede uygun dosya bulunamadigi anlarda da duruyordu.
            // Yukseltmenin vaadi "oyunu acik birakmak anlamli olsun"; en cok
            // ihtiyac duyuldugu anda susmasi tam tersi etki yapiyordu.
            TickSeedIncome(dt);

            if (CurrentFile == null)
            {
                StartNextFile();
                return;
            }

            double speed = EffectiveSpeed;
            if (speed <= 0.0) return;

            UpdateParallelSlots(speed, dt);

            ProgressBits += speed * dt;

            int guard = 0;
            while (CurrentFile != null && CurrentSize > 0.0 && ProgressBits >= CurrentSize)
            {
                if (++guard > MaxCompletionsPerFrame)
                {
                    // Kalani bir sonraki kareye birak.
                    ProgressBits = CurrentSize;
                    break;
                }
                CompleteFile();
            }
        }

        void CompleteFile()
        {
            FileDataSO finished = CurrentFile;

            // Sikistirma uygulanmis boyut — ham sizeBits degil, yoksa dosya
            // bittigi halde ilerleme dusulemez ve dongu kilitlenir.
            double size = CurrentSize;

            // Artan bitler bir sonraki dosyaya devreder — yuksek hizda kayip olmaz.
            ProgressBits -= size;
            if (ProgressBits < 0.0) ProgressBits = 0.0;

            double reward = finished.baseReward * TotalRewardMultiplier;
            if (_wallet != null) _wallet.Add(reward);

            CompletedCount++;
            CountStat();
            if (FileCompleted != null) FileCompleted(finished, reward);

            PickNext(finished);
        }

        /// <summary>
        /// Basarim/gorev sayacini artirir.
        ///
        /// <see cref="CompletedCount"/> PRESTIJDE SIFIRLANIYOR (o, bu turun
        /// sayaci). Basarimlar ise hesabin tum tarihine bakiyor, bu yuzden
        /// ayri ve kalici bir sayac tutuluyor.
        /// </summary>
        void CountStat()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Stats != null) gm.Stats.Add(StatType.FilesDownloaded);
        }

        void StartNextFile()
        {
            ProgressBits = 0.0;
            PickNext(CurrentFile);
        }

        /// <summary>
        /// Otomatik Kuyruk seviyesi (Faz 14). EconomyManager yazar.
        ///   0 = rastgele dosya (varsayilan)
        ///   1 = en karli dosya, ama art arda ayni gelmez
        ///   2 = her zaman en karli dosya
        /// </summary>
        public int QueueAutomationLevel { get; set; }

        void PickNext(FileDataSO previous)
        {
            if (_database == null) return;

            FileDataSO next = QueueAutomationLevel >= 1
                // Seviye 2'de tekrar sinirini kaldiriyoruz: "hep en iyisini indir"
                // yukseltmenin vaadi tam olarak bu.
                ? _database.PickBestFile(_tierIndex, QueueAutomationLevel >= 2 ? null : previous)
                : _database.PickRandomFile(_tierIndex, previous);
            if (next == null)
            {
                Debug.LogWarning("[DownloadController] Kademe " + _tierIndex +
                                 " icin dosya bulunamadi.");
                CurrentFile = null;
                return;
            }

            CurrentFile = next;
            if (FileStarted != null) FileStarted(next);
        }

        /// <summary>
        /// Oyuncu hic tiklamadan saniyede kazandigi para (offline hesabi icin).
        ///
        /// Bosta hiz = BaseSpeed, redline yok. Bir dosyanin gelir hizi
        /// odul/boyut * hiz; havuzun agirlikli ortalamasi bekleneni verir.
        /// </summary>
        public double EstimateIdleIncomePerSecond()
        {
            if (_speed == null) return 0.0;
            return AverageRewardPerBit() * _speed.BaseSpeed * RewardMultiplier;
        }

        /// <summary>
        /// Su anki hizla saniyede kazanilan para — HUD'daki "$/sn" gostergesi icin.
        ///
        /// Idle tahmininden farki: taban hiz yerine FIILI hizi (redline ve olay
        /// carpanlari dahil) ve gecici odul carpanlarini da hesaba katar. Yani
        /// oyuncu tikladikca bu deger yukselir — yukseltmenin ise yarayip
        /// yaramadigi ilk kez gorulebilir hale gelir.
        /// </summary>
        public double CurrentIncomePerSecond
        {
            get { return AverageRewardPerBit() * EffectiveSpeed * TotalRewardMultiplier; }
        }

        /// <summary>
        /// Havuzdaki dosyalarin agirlikli ortalama "odul / bit" orani.
        /// Bir dosyanin gelir hizi odul/boyut x hiz oldugu icin, havuz ortalamasi
        /// beklenen kazanci verir.
        /// </summary>
        double AverageRewardPerBit()
        {
            if (_database == null) return 0.0;

            var pool = _database.GetFilesForTier(_tierIndex);
            if (pool == null || pool.Count == 0) return 0.0;

            double totalWeight = 0.0;
            double weightedRate = 0.0;

            for (int i = 0; i < pool.Count; i++)
            {
                FileDataSO f = pool[i];
                if (f == null || f.sizeBits <= 0.0) continue;

                double w = Math.Max(0.0001, f.weight);
                totalWeight += w;

                // Sikistirma dosyayi kucultuyorsa gelir de artar; hesap bunu
                // gormezse yukseltme kapaliyken hesaplanmis olur.
                double size = f.sizeBits * FileSizeMultiplier;
                if (size <= 0.0) continue;

                weightedRate += w * (f.baseReward / size);
            }

            if (totalWeight <= 0.0) return 0.0;
            return weightedRate / totalWeight;
        }

        /// <summary>
        /// Kayittan yukleme icin (Faz 6). Prestij sifirlamasi da bunu kullanir.
        /// </summary>
        public void Restore(FileDataSO file, double progressBits, int completedCount)
        {
            CurrentFile = file;
            ProgressBits = Math.Max(0.0, progressBits);
            CompletedCount = completedCount;

            // Ek slotlar da bu durumun parcasi: prestijden sonra eski turun
            // yarim kalmis indirmeleri devam etmemeli. Kayittan yuklemede
            // SaveManager bunlari hemen ardindan RestoreSlot ile geri koyuyor.
            _slots.Clear();

            if (CurrentFile == null) StartNextFile();
            else if (FileStarted != null) FileStarted(CurrentFile);
        }
    }
}
