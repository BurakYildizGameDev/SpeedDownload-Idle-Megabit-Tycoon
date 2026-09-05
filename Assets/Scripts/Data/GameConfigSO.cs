using UnityEngine;

namespace SpeedDownload.Data
{
    /// <summary>Redline bonusunun uygulanma bicimi.</summary>
    public enum RedlineBonusMode
    {
        /// <summary>Esik gecilir gecilmez tam carpan (GDD v2.2 davranisi).</summary>
        Flat = 0,
        /// <summary>Esikten tepeye dogru 1.0 -> tam carpan rampasi (varsayilan).</summary>
        Ramp = 1
    }

    /// <summary>
    /// Tum denge sabitleri tek yerde (GDD Bolum 6.3 + PLAN Bolum 12).
    /// Play testinde buradan ayarlanir; kod degismez.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "SpeedDownload/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("Tiklama")]
        // Tiklama gucu artik MUTLAK bit/sn degil, kademenin taban hizina ORAN.
        //
        // Eskiden tiklama sabit bir sayi ekliyordu (kademe indeksine gore
        // logaritmik olcekleniyordu: 8, 16, 24 ... 96 bit/sn). Ama kademe hiz
        // araliklari ustel buyuyor — kademe 8'de kadran 1e12 ile 1e14 arasi.
        // Oraya saniyede 96 bit eklemek ibreyi hic kimildatmiyordu. Yani oyunun
        // MERKEZ mekanigi olan tiklama, kademe 3-4'ten sonra tamamen olu bir
        // dugmeye donusuyordu; oyun kendiliginden akan bir ekrana dusuyordu.
        //
        // Oran olarak tanimlanmasi bunu kokten cozuyor: her kademede tiklamanin
        // kadran uzerindeki etkisi ayni hissediliyor.
        [Tooltip("Tiklama basina, o kademenin TABAN hizinin kacta kaci eklenir. " +
                 "0.30 = her tiklama taban hizin %30'u kadar hiz ekler.")]
        public double clickPowerRatio = 0.30;

        [Tooltip("Nihai tiklama kazancina uygulanan genel katsayi.")]
        public double clickCoefficient = 1.0;

        [Tooltip("Kullanimdan kaldirildi — clickPowerRatio kullaniliyor. " +
                 "Eski kayitlar/asset'ler bozulmasin diye alan duruyor.")]
        public double baseClickPower = 8.0;

        [Header("Kadran acilari (olculdu — Rehber Bolum 8)")]
        [Tooltip("t=0 konumu. Olculen redline yayina gore -90 (saat 9).")]
        public float minAngle = -90f;

        [Tooltip("t=1 konumu. Olculen redline yayina gore +90 (saat 3).")]
        public float maxAngle = 90f;

        [Header("Redline")]
        [Tooltip("dial_redline_overlay yayi 0..+89 derece arasini kapliyor. " +
                 "180 derecelik supurmede bu t=0.50 demektir.")]
        [Range(0f, 1f)] public float redlineThreshold = 0.50f;

        public float redlineMultiplier = 2.0f;

        [Tooltip("Ramp: esikte 1.0x, tepede tam carpan. Redline bolgesi genis " +
                 "oldugu icin tam 2x'i overheat sinirina saklar.")]
        public RedlineBonusMode redlineBonusMode = RedlineBonusMode.Ramp;

        [Header("Overheat")]
        [Tooltip("Ibrenin tepede kesintisiz kalabilecegi sure (sn).")]
        public float overheatLimit = 2.0f;

        [Tooltip("Harici Fan seviyesi basina eklenen tolerans (sn).")]
        public float overheatLimitPerFanLevel = 0.5f;

        [Tooltip("Asiri isinma sonrasi baglantinin kopuk kaldigi sure (sn).")]
        public float overheatPenalty = 2.0f;

        [Tooltip("Bu normalize degerin uzerinde overheat sayaci isler.")]
        [Range(0.9f, 1f)] public float overheatThreshold = 0.995f;

        [Header("Kritik patlama (DNS)")]
        [Range(0f, 1f)] public float baseCritChance = 0.0f;
        public float critMultiplier = 5.0f;

        [Header("Ekonomi")]
        public float upgradeCostGrowth = 1.15f;

        [Tooltip("Pasif yukseltme seviyesi basina taban hiz artisi.")]
        public float passiveSpeedPerLevel = 0.10f;

        [Tooltip("Tiklama gucu yukseltmesi seviyesi basina artis.")]
        public float clickPowerPerLevel = 0.15f;

        [Header("Offline kazanc")]
        // Uc kademe var ve HEPSI sifirdan buyuk.
        //
        // Onceki surumde offline kazanc tamamen Download Manager yukseltmesine
        // bagliydi; o alinmadan oyuna donen oyuncu hicbir sey kazanmiyor ve
        // hicbir aciklama da gormuyordu. Bir idle oyunun temel vaadi "ben yokken
        // de calisir" oldugu icin bu, sistemin var olmadigi hissini veriyordu.
        // Artik taban her zaman calisiyor, yukseltme onu iyilestiriyor.
        [Tooltip("Download Manager alinmadan gecerli olan verim.")]
        [Range(0f, 1f)] public float offlineEfficiencyBase = 0.25f;

        [Tooltip("Download Manager alinmadan gecerli olan tavan (saat).")]
        public float offlineCapHoursBase = 2f;

        [Tooltip("Download Manager seviye 1 verimi.")]
        [Range(0f, 1f)] public float offlineEfficiency = 0.50f;
        public float offlineCapHours = 8f;

        [Tooltip("Download Manager seviye 2 (Pro) verimi.")]
        [Range(0f, 1f)] public float offlineEfficiencyPro = 1.0f;
        public float offlineCapHoursPro = 24f;

        [Header("Prestij")]
        public double prestigeThreshold = 1e9;
        public float prestigeCreditMultiplier = 0.02f;

        [Tooltip("Prestijin acildigi kademe. Faz 15'te 8'den 6'ya cekildi: oyuncu " +
                 "dongusu bir kere gormeden birakirsa prestij sistemi hic var " +
                 "olmamis demektir.")]
        public int prestigeUnlockTier = 6;

        [Header("Olaylar")]
        public float eventMinInterval = 90f;
        public float eventMaxInterval = 180f;
        public float happyHourDuration = 45f;
        public float happyHourMultiplier = 3f;
        [Range(0f, 1f)] public float wifiStealFactor = 0.6f;
        public int wifiPasswordTaps = 3;

        [Header("Dosya uretimi")]
        [Tooltip("Bir dosyanin kademe orta hizinda tamamlanmasi hedeflenen sure (sn). " +
                 "DataAssetGenerator dosya boyutlarini bundan turetir.")]
        public double fileTargetSeconds = 12.0;

        [Tooltip("Kademe araliginda hangi noktanin 'tipik oyun hizi' sayilacagi " +
                 "(0 = min, 1 = max, logaritmik).")]
        [Range(0f, 1f)] public float fileReferenceSpeedT = 0.6f;

        [Header("Kayit")]
        public float autosaveInterval = 15f;

        [Header("Sonsuz kademe (9+)")]
        [Tooltip("Her sonsuz kademede hizin carpani.")]
        public double infiniteTierSpeedFactor = 100.0;

        [Tooltip("Her sonsuz kademede kadran hue kaymasi (altin aci / 360).")]
        public float infiniteTierHueStep = 0.381966f;
    }
}
