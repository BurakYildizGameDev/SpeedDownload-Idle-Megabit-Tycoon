using System;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Kademe neden degisti?
    ///
    /// <see cref="GameManager.TierChanged"/> uc bambaska durumda yayiliyordu ve
    /// dinleyiciler bunlari ayirt edemiyordu:
    ///
    ///   1. Oyuncu bir baglanti satin aldi  -> KUTLANMALI
    ///   2. Kayit yuklendi, kalinan kademe geri kondu -> kutlanmamali
    ///   3. Prestij sifirladi, kademe DUSTU -> kesinlikle kutlanmamali
    ///
    /// Ayrimin olmamasi yuzunden kayitli kademesi 0'dan buyuk olan her oyuncu,
    /// oyunu her acisinda konfeti + agir titresim + kademe atlama fanfari
    /// aliyordu; ustelik bunu aciklayan pencere (TierUpModalView) kendi ayri
    /// bayragiyla bastirildigi icin kutlama sebepsiz gorunuyordu. Prestijden
    /// sonra da kademe 8'den 0'a duserken ayni fanfar caliyordu.
    /// </summary>
    public enum TierChangeReason
    {
        /// <summary>Oyuncu gercekten yeni bir kademeye gecti.</summary>
        Advanced = 0,

        /// <summary>Kayittan geri yuklendi — oyuncu bir sey basarmadi.</summary>
        Restored = 1,

        /// <summary>Prestij sifirlamasi — kademe genellikle DUSER.</summary>
        Reset = 2
    }

    /// <summary>
    /// Sahnedeki tek giris noktasi. Veri tabanini tutar ve hangi kademede
    /// oldugumuzu bilir. Faz 2'de yalnizca kademe secimi var; ekonomi/kayit
    /// sonraki fazlarda buraya baglanacak.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] GameDatabaseSO database;
        [SerializeField] int startingTierIndex = 0;

        public GameDatabaseSO Database { get { return database; } }

        public GameConfigSO Config
        {
            get { return database != null ? database.config : null; }
        }

        public int CurrentTierIndex { get; private set; }

        /// <summary>
        /// Sahnedeki SpeedController. Gorsel bilesenler prefab icinde oldugu ve
        /// prefab sahne referansi saklayamadigi icin buradan cozulur.
        /// </summary>
        public SpeedController Speed { get; private set; }

        /// <summary>Sahnedeki DownloadController.</summary>
        public DownloadController Download { get; private set; }

        /// <summary>Sahnedeki Wallet.</summary>
        public Wallet Money { get; private set; }

        /// <summary>Sahnedeki EconomyManager.</summary>
        public EconomyManager Economy { get; private set; }

        /// <summary>Sahnedeki TierManager.</summary>
        public TierManager Tiers { get; private set; }

        /// <summary>Sahnedeki SaveManager.</summary>
        public SaveManager Save { get; private set; }

        /// <summary>Sahnedeki PrestigeManager.</summary>
        public PrestigeManager Prestige { get; private set; }

        /// <summary>Sahnedeki GameEventManager.</summary>
        public GameEventManager Events { get; private set; }

        /// <summary>
        /// Sahnedeki AudioManager. Sesin tek sahibi burasi; UI bilesenleri
        /// kendi basina klip calmaz, buradan ister.
        /// </summary>
        public AudioManager Audio { get; private set; }

        /// <summary>Sahnedeki CollectionManager (Indirilenler Arsivi).</summary>
        public CollectionManager Collection { get; private set; }

        /// <summary>
        /// Basarim ve gorevlerin dayandigi sayaclar.
        /// Prestijde sifirlanmaz — "bu hesabin tarihi".
        /// </summary>
        public PlayerStats Stats { get; private set; }

        /// <summary>Sahnedeki AchievementManager (kupa paneli).</summary>
        public AchievementManager Achievements { get; private set; }

        /// <summary>Sahnedeki DailyQuestManager (gunluk 3 gorev).</summary>
        public DailyQuestManager Quests { get; private set; }

        /// <summary>
        /// Odullu reklam saglayicisi. Faz 10'da StubAdService; gercek SDK
        /// geldiginde yalnizca sahnedeki bilesen degisir.
        /// </summary>
        public IAdService Ads { get; private set; }

        public ConnectionTierSO CurrentTier
        {
            get { return database != null ? database.GetTier(CurrentTierIndex) : null; }
        }

        /// <summary>Kademe degistiginde tetiklenir (kadran, oda, birim swap'i icin).</summary>
        public event Action<ConnectionTierSO> TierChanged;

        /// <summary>
        /// Su an islenmekte olan <see cref="TierChanged"/> yayininin sebebi.
        ///
        /// Olayin imzasina eklemek yerine burada tutuluyor: kademeyi dinleyen
        /// dokuz bilesenin cogu sebebi umursamiyor (kadran, oda, ekonomi hepsi
        /// ayni sekilde davranmali). Yalnizca KUTLAYAN uc dinleyici
        /// (FXManager, AudioManager, TierUpModalView) buna bakiyor.
        /// </summary>
        public TierChangeReason LastTierChangeReason { get; private set; }

        /// <summary>
        /// Kutlama yapan dinleyiciler icin tek satirlik kontrol.
        /// </summary>
        public bool IsCelebratedTierChange
        {
            get { return LastTierChangeReason == TierChangeReason.Advanced; }
        }

        void Awake()
        {
            Bootstrap();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Yalnizca edit-mode testleri icin.
        ///
        /// Unity edit modunda Awake'i CALISTIRMAZ (Start'i da). Bu olmadan
        /// <see cref="Instance"/> null kalir ve GameManager'a bagli her bilesen
        /// (Wallet, EconomyManager...) cozumlenemez — testler sessizce bos
        /// nesnelerle calisip yanlis "gecti" verir.
        /// </summary>
        public void EditorBootstrap()
        {
            Bootstrap();

            // Bootstrap bagimliliklari FindAnyObjectByType ile ariyor. Edit
            // modunda ACIK SAHNE de bellekte durdugu icin bu arama test
            // nesnesinin degil sahnenin bilesenini dondurebiliyor: test kendi
            // cuzdanini doldururken olcum baskasinin (bakiyesi 0 olan) cuzdani
            // uzerinden yapiliyor ve her sey sessizce 0 cikiyordu.
            //
            // Ayni nesne uzerindeki bilesenler her zaman oncelikli.
            var localWallet = GetComponent<Wallet>();
            if (localWallet != null) Money = localWallet;

            var localEconomy = GetComponent<EconomyManager>();
            if (localEconomy != null) Economy = localEconomy;

            var localSpeed = GetComponent<SpeedController>();
            if (localSpeed != null) Speed = localSpeed;

            var localDownload = GetComponent<DownloadController>();
            if (localDownload != null) Download = localDownload;

            var localPrestige = GetComponent<PrestigeManager>();
            if (localPrestige != null) Prestige = localPrestige;

            var localStats = GetComponent<PlayerStats>();
            if (localStats != null) Stats = localStats;

            var localAchievements = GetComponent<AchievementManager>();
            if (localAchievements != null) Achievements = localAchievements;

            var localQuests = GetComponent<DailyQuestManager>();
            if (localQuests != null) Quests = localQuests;

            // Reklam saglayicisi ayri bir sorun: Bootstrap onu FindObjectsByType
            // ile ariyor ve Unity o aramadan HideAndDontSave bayrakli nesneleri
            // GIZLIYOR. Testler rig'i tam da o bayrakla kuruyor (rig'in
            // GameManager'i gercek [Game] nesnesini yok etmesin diye), dolayisiyla
            // sahnede saglayici olsa bile Ads null kaliyordu.
            //
            // Yukaridakiler gibi tek bir GetComponent yetmez: Bootstrap "en
            // yuksek oncelikli" olani seciyor, "bulunan ilkini" degil. Ayni
            // nesnede hem sahte panel hem gercek SDK durdugu icin o kural
            // burada da korunmali — yoksa test yesile doner ama yanlis
            // saglayiciyi dogrulamis olur.
            var localBehaviours = GetComponents<MonoBehaviour>();
            int localBest = int.MinValue;

            for (int i = 0; i < localBehaviours.Length; i++)
            {
                var service = localBehaviours[i] as IAdService;
                if (service == null) continue;

                if (service.Priority > localBest)
                {
                    localBest = service.Priority;
                    Ads = service;
                }
            }
        }
#endif

        void Bootstrap()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (database == null)
            {
                Debug.LogError("[GameManager] GameDatabase atanmamis. " +
                               "Tools/SpeedDownload/3. Build Game Scene ile sahneyi yeniden kur.");
                enabled = false;
                return;
            }

            CurrentTierIndex = Mathf.Clamp(startingTierIndex, 0, MaxTierIndex);
            Speed = FindAnyObjectByType<SpeedController>();
            Download = FindAnyObjectByType<DownloadController>();
            Money = FindAnyObjectByType<Wallet>();
            Economy = FindAnyObjectByType<EconomyManager>();
            Tiers = FindAnyObjectByType<TierManager>();
            Save = FindAnyObjectByType<SaveManager>();
            Prestige = FindAnyObjectByType<PrestigeManager>();
            Events = FindAnyObjectByType<GameEventManager>();
            Audio = FindAnyObjectByType<AudioManager>();
            Collection = FindAnyObjectByType<CollectionManager>();
            Stats = FindAnyObjectByType<PlayerStats>();
            Achievements = FindAnyObjectByType<AchievementManager>();
            Quests = FindAnyObjectByType<DailyQuestManager>();

            // Arayuz uzerinden ara: somut sinifa bagimlilik olusmasin.
            //
            // EN YUKSEK ONCELIKLI olan seciliyor, "bulunan ilki" degil. Sahnede
            // hem sahte panel hem gercek SDK saglayicisi duruyor; ilkini almak,
            // hangisinin kullanildigini nesnelerin sirasina birakirdi.
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            int bestPriority = int.MinValue;

            for (int i = 0; i < behaviours.Length; i++)
            {
                var service = behaviours[i] as IAdService;
                if (service == null) continue;

                if (service.Priority > bestPriority)
                {
                    bestPriority = service.Priority;
                    Ads = service;
                }
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Yalnizca SceneBuilder icin. Alanlar private [SerializeField] oldugu
        /// icin sahne kurulurken disaridan baglanacak tek yol bu.
        /// </summary>
        public void EditorBind(GameDatabaseSO db, int startTier)
        {
            database = db;
            startingTierIndex = startTier;
        }
#endif

        /// <summary>
        /// Secilebilecek en yuksek kademe. Sonsuz kademe sablonu varsa ustu acik;
        /// yoksa sabit dizinin sonu.
        /// </summary>
        public int MaxTierIndex
        {
            get
            {
                if (database == null) return 0;
                return database.HasInfiniteTiers ? int.MaxValue - 1 : database.HighestFixedTierIndex;
            }
        }

        /// <summary>
        /// Kademeyi degistirir. Sebep verilmezse oyuncunun ilerledigi varsayilir —
        /// baglanti satin alma yolu bu.
        /// </summary>
        public void SetTier(int index)
        {
            SetTier(index, TierChangeReason.Advanced);
        }

        public void SetTier(int index, TierChangeReason reason)
        {
            if (database == null) return;

            int clamped = Mathf.Clamp(index, 0, MaxTierIndex);
            if (clamped == CurrentTierIndex) return;

            CurrentTierIndex = clamped;

            // Sebep, olay yayilmadan ONCE yazilmali: dinleyiciler cagri yiginin
            // icinde bunu okuyor.
            LastTierChangeReason = reason;

            if (TierChanged != null) TierChanged(CurrentTier);

            // Yayin bitti; bayragi varsayilana dondur ki olay disinda okunan bir
            // deger yanlislikla "kayittan geldi" gibi gorunmesin.
            LastTierChangeReason = TierChangeReason.Advanced;
        }
    }
}
