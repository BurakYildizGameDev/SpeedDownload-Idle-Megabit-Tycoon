using System;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Odullu reklamin nerede gosterildigi.
    ///
    /// Yerleştirmenin kendisi ODULU belirlemez — odulu cagiran taraf verir.
    /// Buradaki ayrimin iki isi var:
    ///   1. Sahte reklam panelinde izleme suresini belirlemek (kota ucuz bir
    ///      cozum, offline/boost daha buyuk odul).
    ///   2. Gercek SDK'da her yerlestirmeyi ayri raporlayabilmek — hangisinin
    ///      izlendigini bilmeden hangisinin oyunu bozdugu da anlasilmaz.
    /// </summary>
    public enum AdPlacement
    {
        /// <summary>Kota olayini temizler (GDD Bolum 8).</summary>
        QuotaClear = 0,

        /// <summary>Offline kazanci ikiye katlar.</summary>
        OfflineDouble = 1,

        /// <summary>Gecici hiz boost'u verir.</summary>
        SpeedBoost = 2
    }

    /// <summary>
    /// Odullu reklam saglayicisi (PLAN Bolum 2.7).
    ///
    /// Oyun bu arayuze konusuyor; gercek SDK geldiginde yalnizca uygulayan
    /// sinif degisiyor, cagri yerleri ayni kaliyor.
    /// </summary>
    public interface IAdService
    {
        /// <summary>
        /// Sahnede birden fazla saglayici varsa hangisinin secilecegi.
        ///
        /// Sahte panel ve gercek SDK ayni anda sahnede duruyor: GameManager
        /// "buldugu ilkini" alsaydi hangisinin secildigi nesne sirasina
        /// kalirdi — sessiz ve teshis edilmesi zor bir hata kaynagi.
        /// Yuksek olan kazanir.
        /// </summary>
        int Priority { get; }

        /// <summary>Gosterilebilecek odullu reklam var mi?</summary>
        bool IsRewardedReady { get; }

        /// <summary>Su an bir reklam gosteriliyor mu?</summary>
        bool IsShowing { get; }

        /// <summary>
        /// Odullu reklami gosterir. Reklam sonuna kadar izlenirse onRewarded,
        /// atlanirsa/kapatilirsa onSkipped cagrilir. Ikisi de opsiyonel.
        ///
        /// ODUL YALNIZCA onRewarded'da verilmeli: reklam yarida kapatildiginda
        /// da odul veren bir akis, reklamu tamamen anlamsizlastirir.
        /// </summary>
        void ShowRewarded(AdPlacement placement, Action onRewarded, Action onSkipped);
    }
}
