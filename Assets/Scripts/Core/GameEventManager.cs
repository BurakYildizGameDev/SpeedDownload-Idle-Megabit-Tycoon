using System;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>Rastgele olay turu (GDD Bolum 8).</summary>
    public enum GameEventType
    {
        None = 0,
        /// <summary>Komsu Wi-Fi caldi: hiz duser, sifre 3 tikla degistirilir.</summary>
        NeighborWifi = 1,
        /// <summary>Kota asimi: hiz tabana cekilir, odeme ile sifirlanir.</summary>
        QuotaExceeded = 2,
        /// <summary>Gece tarifesi: 45 sn boyunca kazanclar 3x.</summary>
        HappyHour = 3
    }

    /// <summary>
    /// Rastgele olaylar (GDD Bolum 8, PLAN Bolum 7.6).
    ///
    /// Ayni anda tek olay calisir. Sureli olay (Gece Tarifesi) kendiliginden
    /// biter; engeller (Wi-Fi, Kota) oyuncu cozene kadar durur — bu yuzden
    /// ikisinin de bir "cozme" yolu var.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class GameEventManager : MonoBehaviour
    {
        [Header("Agirliklar (goreli secilme sansi)")]
        [SerializeField] float weightNeighborWifi = 1f;
        [SerializeField] float weightQuota = 1f;
        [SerializeField] float weightHappyHour = 1.4f;

        [Tooltip("Oyunun ilk saniyelerinde olay cikmasin — oyuncu daha ne oldugunu anlamadan cezalandirilmasin.")]
        [SerializeField] float initialGraceSeconds = 60f;

        [Header("Kota")]
        [Tooltip("Kotayi kapatmanin maliyeti, kac saniyelik bosta gelire denk olsun.")]
        [SerializeField] double quotaFeeSeconds = 30.0;

        // --- durum ---

        public GameEventType Active { get; private set; }

        /// <summary>Sureli olaylarda kalan sure (sn).</summary>
        public float Remaining { get; private set; }

        /// <summary>Wi-Fi olayinda kalan tiklama sayisi.</summary>
        public int RemainingTaps { get; private set; }

        public bool HasActiveEvent { get { return Active != GameEventType.None; } }

        /// <summary>
        /// Reklam Engelleyici'den gelen olumsuz olay direnci (0-1). EconomyManager
        /// yazar. Yalnizca ceza olaylarini (Wi-Fi, Kota) seyreltir; Gece Tarifesi
        /// bir odul oldugu icin ondan kacinmak oyuncunun aleyhine olurdu.
        /// </summary>
        public float NegativeEventResistance { get; set; }

        /// <summary>Kotayi kapatmanin su anki bedeli.</summary>
        public double QuotaFee { get; private set; }

        // --- olaylar ---

        public event Action<GameEventType> EventStarted;
        public event Action<GameEventType> EventEnded;

        /// <summary>Wi-Fi tiklamasi veya kota bedeli degisince — banner tazelensin.</summary>
        public event Action EventProgress;

        GameManager _gm;
        GameConfigSO _config;
        float _nextEventTimer;

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[GameEventManager] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            _config = _gm.Config;
            _nextEventTimer = initialGraceSeconds + RandomInterval();
        }

        float RandomInterval()
        {
            float min = _config != null ? _config.eventMinInterval : 90f;
            float max = _config != null ? _config.eventMaxInterval : 180f;
            if (max < min) max = min;
            return UnityEngine.Random.Range(min, max);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (HasActiveEvent)
            {
                TickActive(dt);
                return;
            }

            _nextEventTimer -= dt;
            if (_nextEventTimer <= 0f) StartRandomEvent();
        }

        void TickActive(float dt)
        {
            // Engeller sureye bagli degil; oyuncu cozene kadar dururlar.
            if (Active != GameEventType.HappyHour) return;

            Remaining -= dt;
            if (Remaining <= 0f) EndEvent();
        }

        // ------------------------------------------------------------------
        // Baslatma
        // ------------------------------------------------------------------

        public void StartRandomEvent()
        {
            StartEvent(PickWeighted());
        }

        GameEventType PickWeighted()
        {
            // Direnc ceza olaylarinin agirligini dusurur; Gece Tarifesi'ninkine
            // dokunulmaz, dolayisiyla yukseltme aldikca odul olaylarinin PAYI
            // kendiliginden artar.
            float keep = 1f - Mathf.Clamp01(NegativeEventResistance);
            float wWifi = weightNeighborWifi * keep;
            float wQuota = weightQuota * keep;

            float total = Mathf.Max(0.0001f, wWifi + wQuota + weightHappyHour);
            float roll = UnityEngine.Random.value * total;

            if (roll < wWifi) return GameEventType.NeighborWifi;
            roll -= wWifi;

            if (roll < wQuota) return GameEventType.QuotaExceeded;
            return GameEventType.HappyHour;
        }

        public void StartEvent(GameEventType type)
        {
            if (type == GameEventType.None || HasActiveEvent) return;

            Active = type;
            Remaining = 0f;
            RemainingTaps = 0;
            QuotaFee = 0.0;

            switch (type)
            {
                case GameEventType.NeighborWifi:
                    RemainingTaps = _config != null ? _config.wifiPasswordTaps : 3;
                    ApplySpeedMultiplier(_config != null ? _config.wifiStealFactor : 0.6f);
                    break;

                case GameEventType.QuotaExceeded:
                    QuotaFee = CalculateQuotaFee();
                    ApplySpeedCeilingToBase();
                    break;

                case GameEventType.HappyHour:
                    Remaining = _config != null ? _config.happyHourDuration : 45f;
                    ApplyRewardMultiplier(_config != null ? _config.happyHourMultiplier : 3f);
                    break;
            }

            if (EventStarted != null) EventStarted(type);
        }

        // ------------------------------------------------------------------
        // Cozme
        // ------------------------------------------------------------------

        /// <summary>"Sifreyi Degistir" — 3 tikta Wi-Fi olayini bitirir.</summary>
        public void TapPassword()
        {
            if (Active != GameEventType.NeighborWifi) return;

            RemainingTaps--;
            if (EventProgress != null) EventProgress();

            if (RemainingTaps <= 0) { CountResolved(); EndEvent(); }
        }

        /// <summary>Kota bedelini oder. Para yetmezse false doner.</summary>
        public bool PayQuotaFee()
        {
            if (Active != GameEventType.QuotaExceeded) return false;
            if (_gm.Money == null) return false;

            if (!_gm.Money.TrySpend(QuotaFee)) return false;

            CountResolved();
            EndEvent();
            return true;
        }

        /// <summary>Odullu reklam yolu (Faz 10'da StubAdService baglanacak).</summary>
        public void ClearQuotaByAd()
        {
            if (Active != GameEventType.QuotaExceeded) return;

            CountResolved();
            EndEvent();
        }

        /// <summary>
        /// "Olumsuz olay cozuldu" sayaci.
        ///
        /// Gece Tarifesi SAYILMIYOR: o kendiliginden biten bir ODUL, oyuncunun
        /// cozdugu bir engel degil. Onu da saymak, hicbir sey yapmadan dolan
        /// bir gorev/basarim uretirdi.
        /// </summary>
        void CountResolved()
        {
            if (_gm != null && _gm.Stats != null) _gm.Stats.Add(StatType.EventsResolved);
        }

        double CalculateQuotaFee()
        {
            DownloadController dl = _gm.Download;
            double perSecond = dl != null ? dl.EstimateIdleIncomePerSecond() : 0.0;

            double fee = perSecond * quotaFeeSeconds;

            // Bosta gelir cok dusukse (kademe 0) bedel anlamsiz kalir; bakiyenin
            // kucuk bir yuzdesi daha adil bir taban veriyor.
            double balanceFloor = _gm.Money != null ? _gm.Money.Balance * 0.05 : 0.0;

            return Math.Max(1.0, Math.Max(fee, balanceFloor));
        }

        public void EndEvent()
        {
            if (!HasActiveEvent) return;

            GameEventType finished = Active;

            Active = GameEventType.None;
            Remaining = 0f;
            RemainingTaps = 0;
            QuotaFee = 0.0;

            // Tum gecici etkiler geri alinir.
            ApplySpeedMultiplier(1f);
            ApplyRewardMultiplier(1f);
            ClearSpeedCeiling();

            _nextEventTimer = RandomInterval();

            if (EventEnded != null) EventEnded(finished);
        }

        // ------------------------------------------------------------------
        // Etkiler
        // ------------------------------------------------------------------

        void ApplySpeedMultiplier(double value)
        {
            if (_gm.Download != null) _gm.Download.SpeedMultiplier = value;
        }

        void ApplyRewardMultiplier(double value)
        {
            if (_gm.Download != null) _gm.Download.EventRewardMultiplier = value;
        }

        /// <summary>
        /// VPN Tuneli: kota olayi hiz tavanini uygulamaz. EconomyManager yazar.
        /// Olay yine cikar ve bedeli durur — ama hiz kesilmez.
        /// </summary>
        public bool QuotaBypassed { get; set; }

        void ApplySpeedCeilingToBase()
        {
            if (_gm.Speed == null || QuotaBypassed) return;

            // Tavan anlik BaseSpeed'e degil, canli okunan bir degere baglanmali:
            // kota aktifken bir baglanti satin alinirsa yeni taban hiz gecerli
            // olmali, yoksa oyuncu yukseltmeyi alir ve hicbir sey degismezdi.
            _gm.Speed.SpeedCeiling = _gm.Speed.BaseSpeed;
        }

        /// <summary>
        /// Kota aktifken taban hiz degisirse tavani da guncelle
        /// (baglanti satin alma, pasif yukseltme, koleksiyon bonusu...).
        /// </summary>
        public void RefreshQuotaCeiling()
        {
            if (Active != GameEventType.QuotaExceeded) return;

            if (QuotaBypassed) ClearSpeedCeiling();
            else ApplySpeedCeilingToBase();
        }

        void ClearSpeedCeiling()
        {
            if (_gm.Speed != null) _gm.Speed.SpeedCeiling = double.PositiveInfinity;
        }

        // ------------------------------------------------------------------
        // Kayit (Faz 6 semasi v3)
        // ------------------------------------------------------------------

        /// <summary>
        /// Aktif olayi geri yukler. Engeller kayda yazilmasaydi oyuncu
        /// uygulamayi kapatip acarak cezadan kacabilirdi.
        /// </summary>
        public void Restore(GameEventType type, float remaining, int remainingTaps, double quotaFee)
        {
            EndEvent();
            if (type == GameEventType.None) return;

            Active = type;
            Remaining = remaining;
            RemainingTaps = remainingTaps;
            QuotaFee = quotaFee;

            switch (type)
            {
                case GameEventType.NeighborWifi:
                    if (RemainingTaps <= 0) RemainingTaps = _config != null ? _config.wifiPasswordTaps : 3;
                    ApplySpeedMultiplier(_config != null ? _config.wifiStealFactor : 0.6f);
                    break;

                case GameEventType.QuotaExceeded:
                    if (QuotaFee <= 0.0) QuotaFee = CalculateQuotaFee();
                    ApplySpeedCeilingToBase();
                    break;

                case GameEventType.HappyHour:
                    if (Remaining <= 0f) { Active = GameEventType.None; return; }
                    ApplyRewardMultiplier(_config != null ? _config.happyHourMultiplier : 3f);
                    break;
            }

            if (EventStarted != null) EventStarted(type);
        }
    }
}
