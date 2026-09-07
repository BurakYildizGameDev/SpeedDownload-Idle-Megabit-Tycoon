using System;
using SpeedDownload.Data;
using SpeedDownload.Util;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Kademe ilerlemesinin sahibi (GDD Bolum 3).
    ///
    /// GameManager hangi kademede oldugumuzu tutar; TierManager ise ilerlemenin
    /// KURALLARINI bilir: en yuksek ulasilan kademe, bir sonrakine gecis, gecis
    /// suresince "baglanti kuruluyor" durumu.
    ///
    /// Kademe araliklari ust uste biner (her kademenin minSpeed'i bir oncekinin
    /// maxSpeed'i). Yani kademe atlayinca ayni hiz kadranin sag ucundan sol
    /// ucuna tasinir — bu kasitli: yeni kademe daha genis bir dunya demek.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class TierManager : MonoBehaviour
    {
        [Tooltip("Kademe atlandiktan sonra 'baglanti kuruluyor' sayilan sure (sn). " +
                 "Kadran suzulmesiyle ayni pencere.")]
        [SerializeField] float transitionDuration = 1.2f;

        /// <summary>Bu oyunda ulasilan en yuksek kademe (prestij ve kayit icin).</summary>
        public int HighestTierReached { get; private set; }

        /// <summary>Kademe atlama animasyonu suruyor mu?</summary>
        public bool IsTransitioning { get { return _timer > 0f; } }

        /// <summary>
        /// Bir onceki kademe. Ilk degisimden once null.
        ///
        /// Eskiden bu ozellik adiyla TERS calisiyordu: OnTierChanged icinde
        /// <c>PreviousTier = tier</c> yaziliyordu, yani cagri bittikten sonra
        /// "onceki kademe" aslinda MEVCUT kademeyi tutuyordu. Disaridan okuyan
        /// herkes yanlis veri alirdi. Artik mevcut kademe ayri bir alanda.
        /// </summary>
        public ConnectionTierSO PreviousTier { get; private set; }

        /// <summary>
        /// (oncekiKademe, yeniKademe) — banner, ses ve efektler icin.
        ///
        /// YALNIZCA gercek ilerlemede yayilir: kayittan yukleme ve prestij
        /// sifirlamasi (kademe duser) bu olayi tetiklemez.
        /// </summary>
        public event Action<ConnectionTierSO, ConnectionTierSO> TierAdvanced;

        ConnectionTierSO _currentTier;

        /// <summary>Gecis penceresi kapandiginda.</summary>
        public event Action<ConnectionTierSO> TransitionFinished;

        GameManager _gm;
        float _timer;

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[TierManager] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            HighestTierReached = _gm.CurrentTierIndex;
            _gm.TierChanged += OnTierChanged;
        }

        void OnDestroy()
        {
            if (_gm != null) _gm.TierChanged -= OnTierChanged;
        }

        void OnTierChanged(ConnectionTierSO tier)
        {
            if (tier == null) return;

            ConnectionTierSO from = _currentTier;
            PreviousTier = from;
            _currentTier = tier;

            if (tier.tierIndex > HighestTierReached) HighestTierReached = tier.tierIndex;

            // Gecis penceresi her degisimde acilir — kadran suzulmesi kayittan
            // yuklemede de gerekiyor, orada da ibre yeni araliga tasiniyor.
            _timer = transitionDuration;

            // Ilerleme olayi ise yalnizca oyuncu YUKARI ciktiginda. Prestij
            // sifirlamasi kademeyi 8'den 0'a dusuruyor; onu "advanced" saymak
            // ileride bu olaya baglanacak her kutlamayi yanlis tetiklerdi.
            if (!IsAdvance(from, tier)) return;

            if (TierAdvanced != null) TierAdvanced(from, tier);
        }

        /// <summary>Gercek bir ilerleme mi? (yukari + oyuncunun kendi eylemi)</summary>
        bool IsAdvance(ConnectionTierSO from, ConnectionTierSO to)
        {
            if (_gm != null && !_gm.IsCelebratedTierChange) return false;

            // Ilk atama (from == null) bir ilerleme degil, oyunun acilisi.
            return from != null && to.tierIndex > from.tierIndex;
        }

        void Update()
        {
            if (_timer <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = 0f;
            if (TransitionFinished != null) TransitionFinished(_gm.CurrentTier);
        }

        // ------------------------------------------------------------------
        // Ilerleme sorgulari
        // ------------------------------------------------------------------

        /// <summary>Bir sonraki kademe var mi? (sonsuz kademeler Faz 7'de)</summary>
        public bool HasNextTier
        {
            get
            {
                return _gm != null && _gm.Database != null &&
                       _gm.CurrentTierIndex < _gm.Database.HighestFixedTierIndex;
            }
        }

        public ConnectionTierSO NextTier
        {
            get
            {
                if (!HasNextTier) return null;
                return _gm.Database.GetTier(_gm.CurrentTierIndex + 1);
            }
        }

        /// <summary>Bir sonraki kademeyi acan baglanti yukseltmesi.</summary>
        public UpgradeSO NextConnectionUpgrade
        {
            get
            {
                if (!HasNextTier) return null;
                return _gm.Database.GetConnectionUpgrade(_gm.CurrentTierIndex + 1);
            }
        }

        /// <summary>Kademenin hiz araligini okunur bicimde: "100 bps - 1 Kbps".</summary>
        public string DescribeRange(ConnectionTierSO tier)
        {
            if (tier == null) return "";
            return NumberFormatter.FormatSpeed(tier.minSpeed) + " - " +
                   NumberFormatter.FormatSpeed(tier.maxSpeed);
        }

        /// <summary>Kayittan yukleme icin (Faz 6).</summary>
        public void Restore(int highestTierReached)
        {
            HighestTierReached = Mathf.Max(0, highestTierReached);
        }
    }
}
