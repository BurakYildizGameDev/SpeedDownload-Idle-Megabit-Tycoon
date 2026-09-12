using System;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// "Hat Degisimi" — prestij dongusu (GDD Bolum 7).
    ///
    ///     kazanilacakKredi = floor(sqrt(toplamKazanc / prestigeThreshold))
    ///     kaliciCarpan     = 1 + 0.02 * toplamFiberKredisi
    ///
    /// Sifirlanir: para, yukseltmeler, kademe, aktif dosya.
    /// Korunur   : Fiber Kredisi, toplam yasam boyu kazanc.
    ///
    /// Toplam kazancin korunmasi kasitli: bir sonraki prestijde kazanilacak
    /// kredi hep TOPLAM kazanca bakar, yani her tur bir oncekinin uzerine biner.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class PrestigeManager : MonoBehaviour
    {
        /// <summary>Kalici prestij para birimi.</summary>
        public int FiberCredits { get; private set; }

        /// <summary>
        /// Kredi Faizi yeteneginden gelen ek kazanc orani (0,20 = +%20).
        /// EconomyManager yazar.
        /// </summary>
        public double CreditBonus { get; set; }

        /// <summary>
        /// Hizli Baslangic yeteneginden gelen baslangic kademesi.
        /// Prestij sonrasi sifir yerine buradan baslanir.
        /// </summary>
        public int StartingTier { get; set; }

        /// <summary>Su an prestij yapilsa kazanilacak kredi.</summary>
        public int PendingCredits
        {
            get
            {
                if (_config == null || _gm == null || _gm.Money == null) return 0;

                double threshold = _config.prestigeThreshold;
                if (threshold <= 0.0) return 0;

                double ratio = _gm.Money.LifetimeEarnings / threshold;
                if (ratio <= 0.0 || double.IsNaN(ratio)) return 0;

                double baseCredits = Math.Floor(Math.Sqrt(ratio));
                return (int)Math.Floor(baseCredits * (1.0 + Math.Max(0.0, CreditBonus)));
            }
        }

        /// <summary>
        /// Harcanmis krediyi geri verir (yukseltme geri alma penceresi).
        ///
        /// <see cref="Restore"/> ile yapilmiyor: o metot "kayittan yukle"
        /// anlamini tasiyor ve carpani bastan hesapliyor. Iade ayri bir niyet;
        /// ikisini ayni metoda yiginmak, ileride birinin davranisi degistiginde
        /// digerini sessizce bozardi.
        /// </summary>
        public void RefundCredits(int amount)
        {
            if (amount <= 0) return;

            FiberCredits += amount;

            ApplyMultiplier();
            if (Changed != null) Changed();
        }

        /// <summary>
        /// Prestij yeteneklerinin harcamasi icin. Yetmezse false doner.
        /// </summary>
        public bool TrySpendCredits(int amount)
        {
            if (amount <= 0 || FiberCredits < amount) return false;

            FiberCredits -= amount;

            if (Changed != null) Changed();
            return true;
        }

        /// <summary>Kalici gelir carpani (1.0 = prestij yok).</summary>
        public double Multiplier
        {
            get
            {
                float perCredit = _config != null ? _config.prestigeCreditMultiplier : 0.02f;
                return 1.0 + perCredit * FiberCredits;
            }
        }

        /// <summary>Prestij ekrani acilabilir mi? (kademe sarti)</summary>
        public bool IsUnlocked
        {
            get
            {
                if (_config == null || _gm == null) return false;
                int required = _config.prestigeUnlockTier;

                // Bir kez prestij yapildiysa ekran kalici olarak acik kalir —
                // aksi halde sifirlamadan sonra kendi ekranini kaybederdi.
                return FiberCredits > 0 || _gm.CurrentTierIndex >= required ||
                       (_gm.Tiers != null && _gm.Tiers.HighestTierReached >= required);
            }
        }

        /// <summary>Simdi prestij yapilabilir mi?</summary>
        public bool CanPrestige
        {
            get { return IsUnlocked && PendingCredits > 0; }
        }

        /// <summary>(kazanilanKredi, yeniToplam)</summary>
        public event Action<int, int> Prestiged;

        /// <summary>Kredi veya carpan degisince — UI tazelemek icin.</summary>
        public event Action Changed;

        GameManager _gm;
        GameConfigSO _config;

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[PrestigeManager] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            _config = _gm.Config;
            ApplyMultiplier();
        }

        /// <summary>Hat Degisimi. Basarisizsa false doner.</summary>
        public bool DoPrestige()
        {
            if (!CanPrestige) return false;

            int earned = PendingCredits;
            FiberCredits += earned;

            // Yukseltmeler ve para sifirlanir, toplam kazanc KORUNUR.
            // ResetUpgrades prestij sekmesindeki yetenekleri ELLEMEZ — onlar
            // Fiber Kredisi ile alindi, her turda yeniden alinmalari sacma olurdu.
            if (_gm.Economy != null) _gm.Economy.ResetUpgrades();

            if (_gm.Money != null)
                _gm.Money.Restore(0.0, _gm.Money.LifetimeEarnings);

            // Hizli Baslangic yetenegi varsa sifirdan degil, o kademeden basla.
            //
            // Reset olarak isaretleniyor: kademe burada neredeyse her zaman
            // DUSER (8 -> 0). Isaretsiz haliyle AudioManager kademe atlama
            // fanfarini calmaya devam ediyordu — dusus kutlaniyordu.
            _gm.SetTier(Mathf.Max(0, StartingTier), TierChangeReason.Reset);

            if (_gm.Download != null) _gm.Download.Restore(null, 0.0, 0);

            ApplyMultiplier();

            if (_gm.Stats != null) _gm.Stats.Add(StatType.Prestiges);

            if (Prestiged != null) Prestiged(earned, FiberCredits);
            if (Changed != null) Changed();

            // Sifirlanan durumu hemen diske yaz: prestijden hemen sonraki bir
            // cokme, harcanmis krediyi geri almanin yolunu birakmazdi.
            if (_gm.Save != null) _gm.Save.Save();

            return true;
        }

        /// <summary>Carpani ekonomiye ve indirmeye uygular.</summary>
        public void ApplyMultiplier()
        {
            if (_gm == null || _gm.Economy == null) return;

            _gm.Economy.PrestigeMultiplier = Multiplier;
            _gm.Economy.ApplyStats();
        }

        /// <summary>Kayittan yukleme icin.</summary>
        public void Restore(int fiberCredits)
        {
            FiberCredits = Mathf.Max(0, fiberCredits);
            ApplyMultiplier();
            if (Changed != null) Changed();
        }
    }
}
