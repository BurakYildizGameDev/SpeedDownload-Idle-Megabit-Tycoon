using System;
using System.Collections.Generic;
using System.IO;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Kayit / yukleme ve offline kazanc (PLAN Bolum 7.4, GDD Bolum 6.3).
    ///
    /// Diger sistemlerin Start'i bittikten SONRA yuklemesi gerektigi icin
    /// calisma sirasi geride. Boylece DownloadController rastgele bir dosya
    /// secse bile kayittaki dosya onun uzerine yazar.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class SaveManager : MonoBehaviour
    {
        public const string FileName = "save.json";

        [Tooltip("Kapali: kayit dosyasi okunmaz/yazilmaz (denge testi icin).")]
        [SerializeField] bool saveEnabled = true;

        /// <summary>Yukleme bittiginde, offline sonucuyla birlikte.</summary>
        public event Action<OfflineResult> Loaded;

        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        /// <summary>
        /// Yazim sirasindaki gecici dosya. Yerine gecirme yarida kalirsa
        /// EN GUNCEL veri burada durur; yukleme bunu kurtarma kaynagi sayar.
        /// </summary>
        static string TempPath { get { return SavePath + ".tmp"; } }

        /// <summary>
        /// Bir onceki basarili kayit. Atomik yer degistirme sirasinda kendiliginden
        /// olusur; ana dosya cozulemezse buraya dusuluyor.
        /// </summary>
        static string BackupPath { get { return SavePath + ".bak"; } }

        /// <summary>
        /// Cozulemeyen kayit buraya tasinir. Eskiden bozuk dosya oldugu yerde
        /// birakiliyor, oyun sifirdan basliyor ve 15 saniye sonraki otomatik
        /// kayit onun UZERINE yaziyordu: oyuncunun ilerlemesini kurtarmanin
        /// hicbir yolu kalmiyordu. Artik dosya bir kenara aliniyor.
        /// </summary>
        static string CorruptPath { get { return SavePath + ".corrupt"; } }

        public bool HasSave { get { return File.Exists(SavePath) || File.Exists(TempPath); } }

        GameManager _gm;
        GameConfigSO _config;
        float _autosaveTimer;
        bool _ready;

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[SaveManager] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            _config = _gm.Config;
            _autosaveTimer = _config != null ? _config.autosaveInterval : 15f;

            OfflineResult offline = Load();
            _ready = true;

            // Eski duz JSON kayit okunduysa HEMEN yeni sifreli bicime tasi.
            // Otomatik kaydi (15 sn) beklemek, tam o aralikta kapatilan bir
            // uygulamada dosyayi korumasiz birakirdi.
            if (_migratedFromPlaintext)
            {
                Save();
                Debug.Log("[SaveManager] Kayit sifreli bicime tasindi.");
            }

            if (Loaded != null) Loaded(offline);
        }

        void Update()
        {
            if (!_ready || !saveEnabled) return;

            _autosaveTimer -= Time.unscaledDeltaTime;
            if (_autosaveTimer > 0f) return;

            _autosaveTimer = _config != null ? _config.autosaveInterval : 15f;
            Save();
        }

        // ------------------------------------------------------------------
        // Uygulama yasam dongusu
        //
        // Android arka plana gecerken OnApplicationPause(true) VE
        // OnApplicationFocus(false) mesajlarinin IKISINI birden yolluyor;
        // ustelik cikista bunlarin ardindan OnApplicationQuit da gelebiliyor.
        // Her biri dogrudan Save() cagirdigi icin tek bir arka plana alma
        // hareketinde ayni dosya iki-uc kez yaziliyordu: JSON uretimi, gecici
        // dosya, silme ve tasima islemleri de o kadar tekrarlaniyordu — hem
        // bosuna I/O hem de tam o anda oldurulen bir uygulamada kayit
        // bozulmasi icin fazladan pencere.
        //
        // Ucu de ayni "artik kaydet" niyetini tasidigi icin kisa bir pencerede
        // teke indiriliyor. Elle cagrilan Save() (ayarlardan cikis, prestij)
        // bundan etkilenmez: orada gecikme degil kesinlik isteniyor.
        // ------------------------------------------------------------------

        const float LifecycleSaveCooldown = 1f;
        float _lastLifecycleSaveTime = -999f;

        void SaveForLifecycle()
        {
            if (Time.unscaledTime - _lastLifecycleSaveTime < LifecycleSaveCooldown) return;
            _lastLifecycleSaveTime = Time.unscaledTime;
            Save();
        }

        void OnApplicationPause(bool paused)
        {
            // Mobilde "cikis" cogu zaman budur — OnApplicationQuit hic gelmeyebilir.
            if (paused) SaveForLifecycle();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveForLifecycle();
        }

        void OnApplicationQuit()
        {
            SaveForLifecycle();
        }

        // ------------------------------------------------------------------
        // Yazma
        // ------------------------------------------------------------------

        public void Save()
        {
            if (!_ready || !saveEnabled || _gm == null) return;

            try
            {
                SaveData data = Capture();

                // Govde ici imza terk edildi: artik dosyanin TAMAMI imzalaniyor
                // (bkz. SaveCrypto). Alan yalnizca eski kayitlarla uyum icin var.
                data.signature = "";

                // Bicimli (indented) JSON'a gerek yok: dosya zaten sifreleniyor,
                // kimse ona bakmayacak. Bosluklar sadece dosyayi buyutuyordu.
                string json = JsonUtility.ToJson(data, false);
                string payload = SaveCrypto.Encrypt(json);

                // Once gecici dosyaya yaz, sonra ATOMIK olarak yerine gecir.
                File.WriteAllText(TempPath, payload);
                ReplaceAtomically(TempPath, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Kayit yazilamadi: " + e.Message);
            }
        }

        /// <summary>
        /// <paramref name="temp"/> dosyasini <paramref name="target"/> yerine
        /// gecirir. Hedefin hicbir an ortadan kaybolmamasi esas.
        ///
        /// Once File.Replace denenir (tek islemde yerine gecirme; yan etki olarak
        /// eski surumu .bak'a alir). Desteklenmeyen bir dosya sisteminde
        /// basarisiz olursa eski "sil ve tasi" yoluna dusulur — o yol atomik
        /// degil, ama tam o pencerede olunse bile .tmp kurtarma zinciri ayakta
        /// (bkz. <see cref="ReadFromDisk"/>), yani veri yine kaybolmaz.
        ///
        /// Not: File.Move'un uzerine yazan (3 argumanli) asiri yuklemesi Unity'nin
        /// .NET profilinde YOK; bu yuzden File.Replace birincil yol.
        /// </summary>
        static void ReplaceAtomically(string temp, string target)
        {
            if (!File.Exists(target))
            {
                File.Move(temp, target);
                return;
            }

            try
            {
                File.Replace(temp, target, BackupPath, true);
                return;
            }
            catch (Exception) { /* asagidaki yola dus */ }

            File.Delete(target);
            File.Move(temp, target);
        }

        SaveData Capture()
        {
            var data = new SaveData();
            data.saveVersion = SaveData.CurrentVersion;
            data.lastSaveUtcTicks = DateTime.UtcNow.Ticks;

            Wallet wallet = _gm.Money;
            if (wallet != null)
            {
                data.balance = wallet.Balance;
                data.lifetimeEarnings = wallet.LifetimeEarnings;
            }

            data.currentTierIndex = _gm.CurrentTierIndex;
            data.highestTierReached = _gm.Tiers != null
                ? _gm.Tiers.HighestTierReached : _gm.CurrentTierIndex;

            data.fiberCredits = _gm.Prestige != null ? _gm.Prestige.FiberCredits : 0;

            GameEventManager events = _gm.Events;
            if (events != null)
            {
                data.activeEvent = (int)events.Active;
                data.eventRemaining = events.Remaining;
                data.eventRemainingTaps = events.RemainingTaps;
                data.eventQuotaFee = events.QuotaFee;
            }

            DownloadController dl = _gm.Download;
            if (dl != null)
            {
                data.currentFileName = dl.CurrentFile != null ? dl.CurrentFile.name : "";
                data.fileProgressBits = dl.ProgressBits;
                data.completedCount = dl.CompletedCount;

                for (int i = 0; i < dl.ActiveSlotCount; i++)
                {
                    FileDataSO slotFile = dl.GetSlotFile(i);
                    if (slotFile == null) continue;

                    data.slots.Add(new SaveData.SlotEntry
                    {
                        fileName = slotFile.name,
                        progressBits = dl.GetSlotProgress(i)
                    });
                }
            }

            EconomyManager eco = _gm.Economy;
            if (eco != null)
            {
                foreach (KeyValuePair<UpgradeSO, int> kv in eco.AllLevels)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    data.upgrades.Add(new SaveData.UpgradeEntry { name = kv.Key.name, level = kv.Value });
                }
            }

            if (_gm.Collection != null)
            {
                data.collectedFiles = _gm.Collection.ToList();
                data.holoFiles = _gm.Collection.HoloToList();
            }

            if (_gm.Stats != null) data.stats = _gm.Stats.ToList();
            if (_gm.Achievements != null) data.achievements = _gm.Achievements.ToList();
            if (_gm.Quests != null) _gm.Quests.Capture(data);

            return data;
        }

        // ------------------------------------------------------------------
        // Okuma
        // ------------------------------------------------------------------

        OfflineResult Load()
        {
            var empty = new OfflineResult();
            if (!saveEnabled) { StartFreshSystems(); return empty; }

            SaveData data = ReadFromDisk();
            if (data == null) { StartFreshSystems(); return empty; }

            GameDatabaseSO db = _gm.Database;

            // 0) Prestij carpani en basta — yukseltme etkileri onunla carpiliyor.
            if (_gm.Prestige != null) _gm.Prestige.Restore(data.fiberCredits);

            // 0b) Koleksiyon — taban hiz bonusu verdigi icin yukseltmelerden
            //     ONCE yuklenmeli; ApplyStats onu okuyacak.
            if (_gm.Collection != null)
            {
                _gm.Collection.Restore(data.collectedFiles);
                _gm.Collection.RestoreHolo(data.holoFiles);
            }

            // 0c) Sayaclar — basarimlar ve gunluk gorevler bunlarin uzerine
            //     kuruluyor, dolayisiyla ikisinden de ONCE gelmeli.
            if (_gm.Stats != null) _gm.Stats.Restore(data.stats);

            // 0d) Basarimlar: sayaclar yerine oturduktan sonra sessizce
            //     senkronlaniyor (odul verilmeden — bkz. AchievementManager).
            if (_gm.Achievements != null) _gm.Achievements.Restore(data.achievements);

            // 0e) Gunluk gorevler: gun degistiyse yenileniyor.
            if (_gm.Quests != null)
                _gm.Quests.Restore(data.questDayNumber, data.questBaseline, data.questsClaimed);

            // 1) Yukseltmeler — taban hiz ve odul carpani offline hesabini
            //    etkiliyor, dolayisiyla once bunlarin oturmasi gerek.
            EconomyManager eco = _gm.Economy;
            if (eco != null && db != null)
            {
                var levels = new Dictionary<UpgradeSO, int>();
                for (int i = 0; i < data.upgrades.Count; i++)
                {
                    UpgradeSO u = db.FindUpgrade(data.upgrades[i].name);
                    if (u != null) levels[u] = data.upgrades[i].level;
                }
                eco.Restore(levels);
            }

            // 2) Kademe
            //
            // Restored olarak isaretleniyor: oyuncu yeni bir kademeye GECMIYOR,
            // kaldigi yere donuyor. Bu isaret olmadan konfeti, agir titresim ve
            // kademe atlama fanfari her acilista tetikleniyordu.
            if (_gm.Tiers != null) _gm.Tiers.Restore(data.highestTierReached);
            _gm.SetTier(data.currentTierIndex, TierChangeReason.Restored);

            // 3) Para
            Wallet wallet = _gm.Money;
            if (wallet != null) wallet.Restore(data.balance, data.lifetimeEarnings);

            // 4) Aktif dosya + ek indirme slotlari
            //
            // Slotlar yukseltmelerden SONRA yukleniyor: kac slot oldugunu
            // ExtraSlots belirliyor ve onu (1) adimindaki ApplyStats yaziyor.
            DownloadController dl = _gm.Download;
            if (dl != null && db != null)
            {
                FileDataSO file = db.FindFile(data.currentFileName);
                dl.Restore(file, data.fileProgressBits, data.completedCount);

                if (data.slots != null)
                {
                    for (int i = 0; i < data.slots.Count; i++)
                    {
                        SaveData.SlotEntry entry = data.slots[i];
                        FileDataSO slotFile = db.FindFile(entry.fileName);
                        if (slotFile == null) continue;

                        dl.RestoreSlot(i, slotFile, entry.progressBits);
                    }
                }
            }

            // 5) Aktif olay — hiz carpani/tavani offline hesabini etkilemez
            //    (offline'da olay islemez) ama oyuna donunce ceza devam etmeli.
            if (_gm.Events != null)
            {
                _gm.Events.Restore((GameEventType)data.activeEvent, data.eventRemaining,
                                   data.eventRemainingTaps, data.eventQuotaFee);
            }

            // 6) Offline kazanc — yukaridakiler oturduktan sonra hesaplanir.
            OfflineResult offline = CalculateOffline(data.lastSaveUtcTicks);
            if (offline.HasEarnings && wallet != null) wallet.Add(offline.earnings);

            return offline;
        }

        /// <summary>
        /// Kayit YOKSA (ilk oyun) veya okunamadiysa cagrilir.
        ///
        /// Basarimlar ve gorevler kendiliginden baslamiyor: ikisi de "kayit
        /// yuklendi mi" bilgisini bekliyor, cunku yukleme sirasinda tetiklenen
        /// sayac degisimleri sahte acilis/odul uretirdi. Kayit hic yoksa o
        /// bekleyis sonsuza kadar surerdi — yani ilk oynayista basarim ve
        /// gunluk gorev sistemi TAMAMEN olu kalirdi.
        /// </summary>
        void StartFreshSystems()
        {
            if (_gm == null) return;

            if (_gm.Achievements != null) _gm.Achievements.MarkReady();
            if (_gm.Quests != null) _gm.Quests.StartFresh();
        }

        /// <summary>
        /// Kaydi okur. Ana dosya yoksa veya cozulemiyorsa sirayla .tmp (yarida
        /// kalmis bir yazimin en guncel verisi) ve .bak (bir onceki basarili
        /// kayit) denenir.
        ///
        /// Tek dosyaya bakip pes etmek, kotu zamanlanan tek bir cokmede
        /// oyuncunun her seyini kaybetmesi demekti.
        /// </summary>
        SaveData ReadFromDisk()
        {
            SaveData data = TryRead(SavePath, false);
            if (data != null) return data;

            data = TryRead(TempPath, true);
            if (data != null)
            {
                Debug.LogWarning("[SaveManager] Ana kayit okunamadi; yarim kalmis yazimdan kurtarildi.");
                return data;
            }

            data = TryRead(BackupPath, true);
            if (data != null)
            {
                Debug.LogWarning("[SaveManager] Ana kayit okunamadi; yedekten kurtarildi.");
                return data;
            }

            return null;
        }

        /// <summary>
        /// Tek bir dosyayi cozmeye calisir. <paramref name="isRecovery"/> yalnizca
        /// gunluk gurultusunu ayarlar: kurtarma denemelerinde dosyanin olmamasi
        /// beklenen bir durum, karantinaya alinacak bir sey de yok.
        /// </summary>
        /// <summary>
        /// Eski (duz JSON) bir kayit okundu mu? Okunduysa bir sonraki yazim
        /// dosyayi yeni sifreli bicime tasir.
        /// </summary>
        bool _migratedFromPlaintext;

        SaveData TryRead(string path, bool isRecovery)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string raw = File.ReadAllText(path);
                string json;

                if (SaveCrypto.IsEncrypted(raw))
                {
                    json = SaveCrypto.Decrypt(raw);

                    // Cozulemeyen sifreli kayit: ya tahrif edilmis ya da baska
                    // bir cihazdan/kurulumdan kopyalanmis. Ikisinde de veriye
                    // GUVENILEMEZ; yuklenmiyor.
                    if (json == null)
                    {
                        Debug.LogWarning("[SaveManager] Kayit dogrulanamadi (" +
                                         SaveCrypto.Diagnostics + "): " + path);
                        if (!isRecovery) Quarantine(path);
                        return null;
                    }
                }
                else
                {
                    // Sifrelemeden ONCEKI bicim. Bir kez daha okunuyor ki mevcut
                    // oyuncular ilerlemesini kaybetmesin; ilk kayitta dosya yeni
                    // bicime tasinacak.
                    //
                    // Bu yolun bir kez kullanildiktan sonra kapanmasi onemli: aksi
                    // halde "duz JSON yaz, imzayi bos birak" hilesi kalici bir
                    // arka kapi olurdu. Tasima kaydi yazildiginda dosya artik
                    // sifreli oldugu icin bu dal bir daha calismaz.
                    json = raw;
                    _migratedFromPlaintext = true;
                }

                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[SaveManager] Kayit cozulemedi: " + path);
                    if (!isRecovery) Quarantine(path);
                    return null;
                }

                if (data.saveVersion > SaveData.CurrentVersion)
                {
                    // Ileri surumlu kayit: daha yeni bir yapidan geliyor, okumaya
                    // calismak sessizce yanlis durum uretebilir.
                    //
                    // KARANTINAYA ALINMAZ: dosya bozuk degil, yalnizca bu surum
                    // icin fazla yeni. Oyuncu guncel surume donerse calismali.
                    Debug.LogWarning("[SaveManager] Kayit surumu " + data.saveVersion +
                                     " destekleniyor olandan (" + SaveData.CurrentVersion +
                                     ") yeni. Yoksayildi.");
                    return null;
                }

                // Sifresiz gelen bir dosya yalnizca ESKI surumlerden olabilir.
                // v7 ve sonrasi her zaman sifreli yazilir; duz JSON olarak
                // gorunuyorsa birileri elle yazmis demektir.
                if (!SaveCrypto.IsEncrypted(raw) && data.saveVersion > SaveData.LastPlaintextVersion)
                {
                    Debug.LogWarning("[SaveManager] Sifresiz ama guncel surumlu kayit — " +
                                     "elle yazilmis kabul edildi ve reddedildi: " + path);
                    if (!isRecovery) Quarantine(path);
                    return null;
                }

                // Diskten gelen her deger makul araliga cekilir: bozuk dosya,
                // yarim yazim veya tahrifat sonrasi NaN/sonsuz/negatif degerler
                // ekonomiye sizmasin.
                data.Sanitize();

                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Kayit okunamadi (" + path + "): " + e.Message);
                if (!isRecovery) Quarantine(path);
                return null;
            }
        }

        /// <summary>
        /// Cozulemeyen kaydi bir kenara alir.
        ///
        /// Yerinde birakilirsa oyun sifirdan basliyor ve ilk otomatik kayit
        /// (15 sn sonra) dosyanin uzerine yaziyordu — elle kurtarma sansi sifir.
        /// Tasindiktan sonra dosya duruyor: destek talebinde okunabilir, gerekirse
        /// elle onarilip geri konabilir.
        /// </summary>
        static void Quarantine(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                if (File.Exists(CorruptPath)) File.Delete(CorruptPath);
                File.Move(path, CorruptPath);

                Debug.LogWarning("[SaveManager] Bozuk kayit karantinaya alindi: " + CorruptPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Bozuk kayit tasinamadi: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Offline kazanc (GDD 6.3)
        //   kazanc = pasifGelir/sn * min(gecenSure, tavan) * verim
        // ------------------------------------------------------------------

        public OfflineResult CalculateOffline(long lastSaveUtcTicks)
        {
            var result = new OfflineResult();
            if (_config == null || lastSaveUtcTicks <= 0) return result;

            EconomyManager eco = _gm.Economy;
            result.managerLevel = eco != null
                ? eco.TotalLevel(UpgradeType.OfflineCapacity) : 0;

            double elapsed = (DateTime.UtcNow - new DateTime(lastSaveUtcTicks, DateTimeKind.Utc)).TotalSeconds;

            // Sistem saati geri alinmis: sömürüyü de sacma sureleri de engelle.
            if (elapsed < 0.0 || double.IsNaN(elapsed)) elapsed = 0.0;
            result.elapsedSeconds = elapsed;

            // Offline kazanc HER ZAMAN calisir; Download Manager onu iyilestirir.
            //
            // Eskiden yukseltme alinmadan kazanc sifirdi. Bir idle oyunda "ben
            // yokken de ilerler" temel vaattir; onu bir yukseltmenin arkasina
            // kilitlemek, sistemi hic yokmus gibi gosteriyordu. Yukseltme yine
            // anlamli: verimi ikiye, tavani dorde katliyor.
            double capHours;
            double efficiency;

            if (result.managerLevel >= 2)
            {
                capHours = _config.offlineCapHoursPro;
                efficiency = _config.offlineEfficiencyPro;
            }
            else if (result.managerLevel == 1)
            {
                capHours = _config.offlineCapHours;
                efficiency = _config.offlineEfficiency;
            }
            else
            {
                capHours = _config.offlineCapHoursBase;
                efficiency = _config.offlineEfficiencyBase;
            }

            // Offline Ustasi (prestij yetenegi) verimi kalici olarak yukseltir.
            // Tavan 1,0: offline kazanc aktif oynamayi gecemez, yoksa oyunu
            // kapatmak en iyi strateji olurdu.
            if (eco != null)
                efficiency = Math.Min(1.0, efficiency + eco.TotalEffect(UpgradeType.OfflineEfficiency));

            result.efficiency = efficiency;
            result.capHours = capHours;
            result.creditedSeconds = Math.Min(elapsed, capHours * 3600.0);

            DownloadController dl = _gm.Download;
            double perSecond = dl != null ? dl.EstimateIdleIncomePerSecond() : 0.0;

            result.earnings = perSecond * result.creditedSeconds * efficiency;
            if (double.IsNaN(result.earnings) || result.earnings < 0.0) result.earnings = 0.0;

            return result;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Prestij / "sifirla" icin (Faz 7).
        ///
        /// Kurtarma dosyalari da silinir: yalnizca ana dosyayi silmek, bir
        /// sonraki aciliste .tmp veya .bak uzerinden silinen ilerlemenin geri
        /// gelmesi demekti.
        /// </summary>
        public void DeleteSave()
        {
            DeleteIfExists(SavePath);
            DeleteIfExists(TempPath);
            DeleteIfExists(BackupPath);
        }

        static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Silinemedi (" + path + "): " + e.Message);
            }
        }
    }
}
