using System;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Para kesesi. Bakiyeyi tutan tek yer.
    ///
    /// Faz 4'teki EconomyManager yukseltme maliyetlerini buradan dusecek;
    /// Faz 6'daki SaveManager bakiyeyi buradan okuyup buraya yazacak. Bu yuzden
    /// ekonomi mantigi degil, sadece "para" burada durur.
    /// </summary>
    public class Wallet : MonoBehaviour
    {
        [SerializeField] double startingBalance = 0.0;

        public double Balance { get; private set; }

        /// <summary>Prestij hesabi icin toplam kazanc. Harcama bunu dusurmez.</summary>
        public double LifetimeEarnings { get; private set; }

        /// <summary>Bakiye her degistiginde yeni bakiye ile tetiklenir.</summary>
        public event Action<double> BalanceChanged;

        void Awake()
        {
            Balance = startingBalance;
        }

        void Start()
        {
            // UI Awake sirasinda henuz baglanmamis olabilir; ilk degeri burada yayinla.
            Raise();
        }

        public void Add(double amount)
        {
            // Sonsuz da elenmeli: bir kez bakiyeye girdiginde her hesap NaN'a
            // doner ve durum geri donusu olmayacak sekilde bozulur.
            if (amount <= 0.0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;

            Balance += amount;
            LifetimeEarnings += amount;
            Raise();
        }

        /// <summary>
        /// Harcanmis parayi geri verir — TOPLAM KAZANCA DOKUNMADAN.
        ///
        /// <see cref="Add"/> ile iade etmek bir somuru kapisiydi: prestij kredisi
        /// <c>floor(sqrt(LifetimeEarnings / esik))</c> ile hesaplaniyor, yani
        /// "MAKS al -> geri al -> tekrar al" dongusu hic para kazanmadan toplam
        /// kazanci sisirip bedava Fiber Kredisi uretiyordu. Iade kazanc degildir.
        /// </summary>
        public void Refund(double amount)
        {
            if (amount <= 0.0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;

            Balance += amount;
            Raise();
        }

        /// <summary>Yeterli para varsa duser ve true doner.</summary>
        public bool TrySpend(double amount)
        {
            if (amount < 0.0 || double.IsNaN(amount)) return false;
            if (Balance < amount) return false;

            Balance -= amount;
            Raise();
            return true;
        }

        public bool CanAfford(double amount)
        {
            return Balance >= amount;
        }

        /// <summary>Kayittan yukleme icin. Toplam kazanc ayrica verilir.</summary>
        public void Restore(double balance, double lifetimeEarnings)
        {
            Balance = Sane(balance);
            LifetimeEarnings = Sane(lifetimeEarnings);

            // Toplam kazanc bakiyeden kucuk olamaz — prestij kredisi toplam
            // kazanca bakiyor, tutarsiz cift onu ucuzlatirdi.
            if (LifetimeEarnings < Balance) LifetimeEarnings = Balance;

            Raise();
        }

        /// <summary>Bozuk kayittan gelen NaN/sonsuz/negatif degerlere karsi.</summary>
        static double Sane(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0) return 0.0;
            return value;
        }

        void Raise()
        {
            if (BalanceChanged != null) BalanceChanged(Balance);
        }
    }
}
