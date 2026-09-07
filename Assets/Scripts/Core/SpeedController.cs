using System;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Speed Limiter fizigi (GDD Bolum 4).
    ///
    /// Hiz her zaman bit/saniye cinsinden double olarak tutulur. Kadranin
    /// gorsel tarafi DialView'in isi; burada yalnizca sayisal durum var.
    /// </summary>
    public class SpeedController : MonoBehaviour
    {
        // --- disaridan (EconomyManager) beslenen degerler ---

        /// <summary>Tiklama olmadan devam eden taban hiz (bit/sn).</summary>
        public double BaseSpeed { get; set; }

        /// <summary>
        /// Hat Bakimi yukseltmesinden gelen taban hiz carpani (GDD 6.3).
        /// EconomyManager yazar. Kademe payi burada degil, tier.baseSpeed'te —
        /// ikisi ayri tutuldugu icin TierChanged dinleyicilerinin sirasi onemsiz.
        /// </summary>
        public double PassiveSpeedMultiplier
        {
            get { return _passiveMultiplier; }
            set
            {
                _passiveMultiplier = (value <= 0.0 || double.IsNaN(value)) ? 1.0 : value;
                RecalculateBaseSpeed();
            }
        }

        double _passiveMultiplier = 1.0;

        /// <summary>
        /// Tiklama basina eklenen hiz — TABAN HIZIN kati olarak.
        ///
        /// Mutlak bir bit/sn degeri degil: kademe araliklari ustel buyudugu icin
        /// sabit bir sayi ust kademelerde ibreyi kimildatmiyordu (bkz.
        /// <see cref="GameConfigSO.clickPowerRatio"/>). Oran olarak tutulunca
        /// tiklama her kademede ayni oranda etki ediyor.
        /// </summary>
        public double ClickPowerRatio { get; set; } = 0.3;

        /// <summary>
        /// Tiklama basina eklenen ham hiz (bit/sn). Turetilmis deger —
        /// <see cref="ClickPowerRatio"/> ile <see cref="BaseSpeed"/> carpimidir.
        /// </summary>
        public double ClickPower
        {
            get { return BaseSpeed * ClickPowerRatio; }
        }

        /// <summary>Kritik patlama ihtimali (0-1).</summary>
        public float CritChance { get; set; }

        /// <summary>Harici Fan'dan gelen ek overheat toleransi (sn).</summary>
        public float ExtraOverheatTolerance { get; set; }

        // --- Faz 13 yukseltmeleri ---

        /// <summary>Yonlu Anten: redline tepe carpanina eklenen miktar.</summary>
        public float ExtraRedlineBonus { get; set; }

        /// <summary>
        /// Overclock Araci: redline esigini asagi ceker (bolgeyi genisletir).
        /// Esik 0,10'un altina inemez — aksi halde ibre neredeyse hep redline'da
        /// sayilir ve mekanik anlamini yitirir.
        /// </summary>
        public float RedlineThresholdReduction { get; set; }

        /// <summary>Isi Macunu: overheat cezasinin kisalma orani (0-1).</summary>
        public float OverheatPenaltyReduction { get; set; }

        /// <summary>
        /// UPS: overheat sirasinda korunan taban hiz orani (0-1).
        /// 0 = tam kesinti (varsayilan davranis), 0,25 = taban hizin dortte biri.
        /// </summary>
        public float OverheatSoftFailRatio { get; set; }

        /// <summary>
        /// Saniyede kac otomatik tiklama yapilacagi (Mekanik Klavye / Makro).
        /// EconomyManager yazar. 0 = otomasyon yok.
        /// </summary>
        public float AutoClicksPerSecond { get; set; }

        /// <summary>Yukseltmelerle duzeltilmis redline esigi.</summary>
        public float EffectiveRedlineThreshold
        {
            get
            {
                if (_config == null) return 0.5f;
                return Mathf.Max(0.10f, _config.redlineThreshold - RedlineThresholdReduction);
            }
        }

        /// <summary>
        /// Hizin gecici ust siniri (bit/sn). Kota Asimi olayi bunu taban hiza
        /// cekerek ibreyi dipte tutar (GDD Bolum 8). Sinirsiz = PositiveInfinity.
        /// </summary>
        public double SpeedCeiling { get; set; } = double.PositiveInfinity;

        /// <summary>Kademe tavani ile olay tavaninin kucugu.</summary>
        public double EffectiveCeiling
        {
            get
            {
                double tierMax = _tier != null ? _tier.maxSpeed : double.PositiveInfinity;
                return Math.Min(tierMax, SpeedCeiling);
            }
        }

        // --- okunur durum ---

        public double CurrentSpeed { get; private set; }

        /// <summary>Kadran uzerindeki normalize konum (0 = minAngle, 1 = maxAngle).</summary>
        public float NormalizedT { get; private set; }

        public bool IsOverheated { get; private set; }

        /// <summary>Indirme hizina uygulanan redline carpani (1.0 - 2.0).</summary>
        public float RedlineMultiplier { get; private set; }

        public bool InRedline { get; private set; }

        /// <summary>Overheat sayacinin doluluk orani (0-1). UI uyarisi icin.</summary>
        public float OverheatProgress { get; private set; }

        /// <summary>Overheat cezasinin kalan suresi (sn).</summary>
        public float OverheatRemaining { get { return _overheatRemaining; } }

        // --- Ritmik Kombo Sistemi ---
        /// <summary>Ritmik tiklama kombo sayisi.</summary>
        public int CurrentCombo { get; private set; }

        /// <summary>Kombodan gelen tiklama gucu carpani (1.0 - 2.0).</summary>
        public float ComboMultiplier { get; private set; } = 1.0f;

        // --- olaylar ---

        public event Action Clicked;
        public event Action<Vector2, double, bool> ClickedWithData;
        public event Action CriticalHit;
        public event Action OverheatStarted;
        public event Action OverheatEnded;
        public event Action EmergencyVented;
        public event Action<int, float> ComboUpdated;

        /// <summary>Isı %60'ın üzerindeyken acil soğutma yapılabilir mi?</summary>
        public bool CanEmergencyVent { get { return !IsOverheated && OverheatProgress >= 0.60f; } }

        /// <summary>
        /// Acil Isı Tahliyesi: Hararet tehlike sınırına ulaştığında ısı sayacını
        /// %65 boşaltarak sistemi çöküşten kurtarır.
        /// </summary>
        public bool TriggerEmergencyVent()
        {
            if (IsOverheated || OverheatProgress < 0.60f || _config == null) return false;

            float limit = Mathf.Max(0.1f, _config.overheatLimit + ExtraOverheatTolerance);
            _overheatTimer = Mathf.Max(0f, _overheatTimer - limit * 0.65f);
            OverheatProgress = Mathf.Clamp01(_overheatTimer / limit);

            CountStat(StatType.Vents);

            if (EmergencyVented != null) EmergencyVented();
            return true;
        }

        float _overheatTimer;
        float _overheatRemaining;
        ConnectionTierSO _tier;
        GameConfigSO _config;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[SpeedController] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            _config = gm.Config;
            gm.TierChanged += ApplyTier;
            ApplyTier(gm.CurrentTier);
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TierChanged -= ApplyTier;
        }

        public void ApplyTier(ConnectionTierSO tier)
        {
            _tier = tier;
            if (_tier == null) return;

            // Ekonomi henuz yokken taban tiklama orani config'ten gelir.
            if (ClickPowerRatio <= 0.0 && _config != null)
                ClickPowerRatio = _config.clickPowerRatio;

            RecalculateBaseSpeed();
            if (CurrentSpeed > _tier.maxSpeed) CurrentSpeed = _tier.maxSpeed;

            CancelOverheat();
        }

        /// <summary>Taban hiz = kademe baz hizi * pasif yukseltme carpani.</summary>
        void RecalculateBaseSpeed()
        {
            if (_tier == null) return;

            BaseSpeed = _tier.baseSpeed * _passiveMultiplier;
            if (CurrentSpeed < BaseSpeed) CurrentSpeed = BaseSpeed;
        }

        void Update()
        {
            if (_tier == null || _config == null) return;

            float dt = Time.deltaTime;

            if (IsOverheated)
            {
                TickOverheatPenalty(dt);
                return;
            }

            // Ustel sonumleme: hiz taban seviyeye yumusakca doner.
            double decayFactor = Math.Exp(-_tier.speedDecayRate * dt);
            CurrentSpeed = BaseSpeed + (CurrentSpeed - BaseSpeed) * decayFactor;

            if (CurrentSpeed < BaseSpeed) CurrentSpeed = BaseSpeed;

            double ceiling = EffectiveCeiling;
            if (CurrentSpeed > ceiling) CurrentSpeed = ceiling;

            NormalizedT = _tier.SpeedToNormalized(CurrentSpeed);
            UpdateRedline();
            UpdateOverheatCounter(dt);
            TickAutoClick(dt);
            TickComboDecay(dt);
        }

        float _lastClickTime;
        float _comboDecayTimer;
        const float ComboMaxInterval = 0.85f;
        const float ComboMinInterval = 0.03f;
        const float ComboLingerTime = 1.25f;

        void TickComboDecay(float dt)
        {
            if (_comboDecayTimer > 0f)
            {
                _comboDecayTimer -= dt;
                if (_comboDecayTimer <= 0f && CurrentCombo > 0)
                {
                    CurrentCombo = 0;
                    ComboMultiplier = 1.0f;
                    if (ComboUpdated != null) ComboUpdated(0, 1.0f);
                }
            }
        }

        // Kesirli tiklamalar birikir: 2,5 tik/sn'de bir karede 0,04 tik olusur,
        // yuvarlamak otomasyonu tamamen yok ederdi.
        float _autoClickAccumulator;

        void TickAutoClick(float dt)
        {
            if (AutoClicksPerSecond <= 0f || IsOverheated) return;

            _autoClickAccumulator += AutoClicksPerSecond * dt;

            // Kare atlamalarinda birikmis onlarca tikin tek karede patlamasini
            // engelle; fazlasi bir sonraki kareye devreder.
            int budget = 8;

            while (_autoClickAccumulator >= 1f && budget-- > 0)
            {
                _autoClickAccumulator -= 1f;
                RegisterClick(NoPosition, true);
            }

            if (_autoClickAccumulator > 4f) _autoClickAccumulator = 4f;
        }

        /// <summary>
        /// "Bu tiklamanin ekran konumu yok" isareti.
        ///
        /// Eskiden bu anlami <see cref="Vector2.zero"/> tasiyordu ve (0,0) ayni
        /// zamanda ekranin sol alt kosesinin GECERLI koordinatiydi: tam oraya
        /// dokunan oyuncuda dalga efekti kosede degil kadranin ortasinda
        /// patliyordu. NaN gercek bir dokunus konumu olamaz, dolayisiyla
        /// belirsizlik kalmiyor.
        /// </summary>
        public static readonly Vector2 NoPosition = new Vector2(float.NaN, float.NaN);

        /// <summary>Konum bilgisi olmayan tiklama (otomasyon, test araclari).</summary>
        public void RegisterClick()
        {
            RegisterClick(NoPosition, false);
        }

        /// <summary>Ekrana her dokunusta cagrilir.</summary>
        public void RegisterClick(Vector2 screenPosition)
        {
            RegisterClick(screenPosition, false);
        }

        /// <summary>
        /// Tiklamayi isler.
        ///
        /// <paramref name="silent"/> otomatik tiklamalar icindir: hiz artisi ve
        /// kritik sansi aynen isler ama HICBIR olay yayilmaz. Aksi halde saniyede
        /// birkac kez klik sesi calar ve ekranda dalga efekti patlardi — otomasyon
        /// oyuncuyu rahatlatmak yerine bogardi. Geri bildirim zaten ibrenin
        /// kendiliginden yukselmesi.
        /// </summary>
        public void RegisterClick(Vector2 screenPosition, bool silent)
        {
            if (IsOverheated || _tier == null || _config == null) return;

            if (!silent)
            {
                float now = Time.unscaledTime;
                float delta = now - _lastClickTime;
                _lastClickTime = now;

                if (delta >= ComboMinInterval && delta <= ComboMaxInterval)
                {
                    CurrentCombo++;
                    _comboDecayTimer = ComboLingerTime;
                }
                else if (delta > ComboMaxInterval)
                {
                    CurrentCombo = 1;
                    _comboDecayTimer = ComboLingerTime;
                }

                if (CurrentCombo >= 50) ComboMultiplier = 2.00f;
                else if (CurrentCombo >= 35) ComboMultiplier = 1.75f;
                else if (CurrentCombo >= 20) ComboMultiplier = 1.50f;
                else if (CurrentCombo >= 10) ComboMultiplier = 1.30f;
                else if (CurrentCombo >= 5) ComboMultiplier = 1.15f;
                else ComboMultiplier = 1.0f;

                if (CurrentCombo > 1)
                {
                    if (ComboUpdated != null) ComboUpdated(CurrentCombo, ComboMultiplier);
                    GameManager gm = GameManager.Instance;
                    if (gm != null && gm.Stats != null)
                    {
                        gm.Stats.SetMax(StatType.MaxCombo, CurrentCombo);
                    }
                }
            }

            double gain = ClickPower * _config.clickCoefficient * (double)ComboMultiplier;
            bool isCrit = false;

            if (CritChance > 0f && UnityEngine.Random.value < CritChance)
            {
                gain *= _config.critMultiplier;
                isCrit = true;
                if (!silent && CriticalHit != null) CriticalHit();
            }

            CurrentSpeed = Math.Min(CurrentSpeed + gain, EffectiveCeiling);
            NormalizedT = _tier.SpeedToNormalized(CurrentSpeed);
            UpdateRedline();

            if (silent) return;

            // Sayac YALNIZCA elle tiklamada artiyor: otomasyonu saymak
            // "50.000 tiklama" basarimini bir yukseltmeyle bekleyerek
            // acilan bir seye cevirirdi.
            CountStat(StatType.Clicks);

            if (Clicked != null) Clicked();
            if (ClickedWithData != null) ClickedWithData(screenPosition, gain, isCrit);
        }

        void UpdateRedline()
        {
            float threshold = EffectiveRedlineThreshold;
            InRedline = NormalizedT >= threshold;

            if (!InRedline)
            {
                RedlineMultiplier = 1f;
                return;
            }

            // Yonlu Anten tepe carpanini yukseltir (2.0 -> 2.25 -> ...).
            float peak = _config.redlineMultiplier + ExtraRedlineBonus;

            if (_config.redlineBonusMode == RedlineBonusMode.Flat)
            {
                RedlineMultiplier = peak;
                return;
            }

            // Ramp: esikte 1.0x, tepede tam carpan. Redline bolgesi cizilen yay
            // yuzunden genis oldugu icin tam carpan overheat sinirina saklanir.
            float span = 1f - threshold;
            float k = span <= 0.0001f ? 1f : (NormalizedT - threshold) / span;
            RedlineMultiplier = Mathf.Lerp(1f, peak, Mathf.Clamp01(k));
        }

        void UpdateOverheatCounter(float dt)
        {
            float limit = Mathf.Max(0.1f, _config.overheatLimit + ExtraOverheatTolerance);

            if (NormalizedT >= _config.overheatThreshold)
            {
                _overheatTimer += dt;
                if (_overheatTimer >= limit)
                {
                    TriggerOverheat();
                    return;
                }
            }
            else
            {
                // Tepeden inince sayac iki kat hizli sogur.
                _overheatTimer = Mathf.Max(0f, _overheatTimer - dt * 2f);
            }

            OverheatProgress = Mathf.Clamp01(_overheatTimer / limit);
        }

        void TriggerOverheat()
        {
            IsOverheated = true;

            // Isi Macunu cezayi kisaltir; sifira inemez ki mekanik kaybolmasin.
            float penalty = _config.overheatPenalty *
                            (1f - Mathf.Clamp01(OverheatPenaltyReduction));
            _overheatRemaining = Mathf.Max(0.25f, penalty);

            _overheatTimer = 0f;
            OverheatProgress = 1f;

            CurrentSpeed = OverheatSpeed;
            NormalizedT = _tier != null ? _tier.SpeedToNormalized(CurrentSpeed) : 0f;
            InRedline = false;
            RedlineMultiplier = 1f;

            CountStat(StatType.Overheats);

            if (OverheatStarted != null) OverheatStarted();
        }

        /// <summary>Basarim/gorev sayacini artirir.</summary>
        static void CountStat(StatType type)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Stats != null) gm.Stats.Add(type);
        }

        /// <summary>
        /// Overheat sirasindaki hiz. UPS alinmadan 0 (tam kesinti); alindiktan
        /// sonra taban hizin bir kismi korunur — indirme tamamen durmaz.
        /// </summary>
        double OverheatSpeed
        {
            get
            {
                float ratio = Mathf.Clamp01(OverheatSoftFailRatio);
                return ratio <= 0f ? 0.0 : BaseSpeed * ratio;
            }
        }

        void TickOverheatPenalty(float dt)
        {
            _overheatRemaining -= dt;
            CurrentSpeed = OverheatSpeed;
            NormalizedT = _tier != null ? _tier.SpeedToNormalized(CurrentSpeed) : 0f;
            RedlineMultiplier = 1f;
            InRedline = false;

            if (_overheatRemaining <= 0f)
            {
                IsOverheated = false;
                OverheatProgress = 0f;
                CurrentSpeed = BaseSpeed;
                if (OverheatEnded != null) OverheatEnded();
            }
        }

        /// <summary>
        /// Overheat durumunu sartsiz temizler (kademe degisimi gibi durum
        /// sifirlayan anlarda).
        ///
        /// Onemli: ceza gercekten aktifken cagrilirsa <see cref="OverheatEnded"/>
        /// YAYILIR. Eskiden yayilmiyordu; <see cref="IsOverheated"/> sessizce
        /// false'a duserken "basladi" haberini alan dinleyiciler "bitti"
        /// haberini hic almiyordu. Overheat perdesini/uyarisini bu olayla acip
        /// kapatan bir arayuz, kademe atlandiginda ekranda kilitli kalirdi.
        /// </summary>
        void CancelOverheat()
        {
            bool wasOverheated = IsOverheated;

            IsOverheated = false;
            _overheatTimer = 0f;
            _overheatRemaining = 0f;
            OverheatProgress = 0f;

            if (wasOverheated && OverheatEnded != null) OverheatEnded();
        }

        /// <summary>Indirme hizina uygulanacak nihai hiz (redline dahil).</summary>
        public double EffectiveSpeed
        {
            get { return CurrentSpeed * RedlineMultiplier; }
        }
    }
}
