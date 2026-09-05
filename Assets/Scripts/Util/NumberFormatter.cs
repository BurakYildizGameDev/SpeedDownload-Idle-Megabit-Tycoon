using System;
using System.Globalization;
using SpeedDownload.Core;

namespace SpeedDownload.Util
{
    /// <summary>
    /// Oyundaki tum sayi gosterimi tek noktadan gecer.
    ///
    /// Birim kurallari:
    ///   - Hiz  : dahili birim bit/saniye, gosterim 1000 tabanli (ag standardi: 1 Kbps = 1000 bps)
    ///   - Boyut: dahili birim bit, gosterim 1024 tabanli byte (1 KB = 1024 B)
    ///   - Para : 1000 tabanli kisaltma (K, M, B, T, Qa, ...)
    ///
    /// Kultur: her zaman InvariantCulture. Cihaz diline gore ondalik ayiracin degismesi
    /// (1.5 / 1,5) HUD'da tutarsizlik yaratir; deterministik olmasi tercih edildi.
    /// </summary>
    public static class NumberFormatter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static readonly string[] SpeedUnits =
        {
            "bps", "Kbps", "Mbps", "Gbps", "Tbps", "Pbps", "Ebps", "Zbps", "Ybps"
        };

        static readonly string[] ByteUnits =
        {
            "B", "KB", "MB", "GB", "TB", "PB", "EB"
        };

        static readonly string[] MoneySuffix =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
        };

        /// <summary>Bu kademenin ustunde bilimsel gosterime gecilir (1e102).</summary>
        const int ScientificTier = 34;

        // ------------------------------------------------------------------ hiz

        /// <summary>Bit/saniye degerini kademe birimiyle bicimler. Ornek: 47 bps, 1.5 Mbps</summary>
        public static string FormatSpeed(double bitsPerSecond)
        {
            if (double.IsNaN(bitsPerSecond) || double.IsInfinity(bitsPerSecond)) return "-- bps";

            double v = bitsPerSecond < 0.0 ? 0.0 : bitsPerSecond;
            if (v < 1.0) return v.ToString("0.##", Inv) + " bps";

            int tier = (int)Math.Floor(Math.Log10(v) / 3.0);
            if (tier >= SpeedUnits.Length)
                return v.ToString("0.###e+0", Inv) + " bps";

            double scaled = v / Math.Pow(1000.0, tier);
            if (scaled >= 999.995 && tier + 1 < SpeedUnits.Length)
            {
                tier++;
                scaled /= 1000.0;
            }

            return scaled.ToString(PrecisionFor(scaled), Inv) + " " + SpeedUnits[tier];
        }

        // ----------------------------------------------------------------- para

        /// <summary>Para degerini $ isaretiyle kisaltir. Ornek: $4.2K, $1.08B</summary>
        public static string FormatMoney(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "$--";

            bool negative = value < 0.0;
            string body = FormatCompact(Math.Abs(value));
            return (negative ? "-$" : "$") + body;
        }

        /// <summary>Isaretsiz, birimsiz kisaltma. Ornek: 4.2K, 1.08B</summary>
        public static string FormatCompact(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "--";
            if (v < 0.0) v = 0.0;
            if (v < 1000.0) return v.ToString(PrecisionFor(v), Inv);

            int tier = (int)Math.Floor(Math.Log10(v) / 3.0);
            if (tier >= ScientificTier)
                return v.ToString("0.###e+0", Inv);

            double scaled = v / Math.Pow(1000.0, tier);
            if (scaled >= 999.995)
            {
                tier++;
                scaled /= 1000.0;
                if (tier >= ScientificTier) return v.ToString("0.###e+0", Inv);
            }

            return scaled.ToString(PrecisionFor(scaled), Inv) + SuffixFor(tier);
        }

        /// <summary>
        /// 0-11 arasi kisa ad (K..Dc), sonrasi iki harfli alfabetik (aa, ab, ...).
        /// double'in ust siniri ~1e308 oldugu icin alfabetik aralik fazlasiyla yeterli.
        /// </summary>
        static string SuffixFor(int tier)
        {
            if (tier < 0) return "";
            if (tier < MoneySuffix.Length) return MoneySuffix[tier];

            int n = tier - MoneySuffix.Length;
            char first = (char)('a' + (n / 26) % 26);
            char second = (char)('a' + n % 26);
            return string.Concat(first, second);
        }

        // ---------------------------------------------------------------- boyut

        /// <summary>Bit cinsinden dosya boyutunu byte tabanli bicimler. Ornek: 250 KB, 1.4 GB</summary>
        public static string FormatSize(double bits)
        {
            if (double.IsNaN(bits) || double.IsInfinity(bits)) return "-- B";

            double bytes = (bits < 0.0 ? 0.0 : bits) / 8.0;
            if (bytes < 1024.0) return bytes.ToString("0", Inv) + " B";

            int tier = (int)Math.Floor(Math.Log(bytes, 1024.0));
            if (tier >= ByteUnits.Length)
                return bytes.ToString("0.###e+0", Inv) + " B";

            double scaled = bytes / Math.Pow(1024.0, tier);
            if (scaled >= 1023.995 && tier + 1 < ByteUnits.Length)
            {
                tier++;
                scaled /= 1024.0;
            }

            return scaled.ToString(PrecisionFor(scaled), Inv) + " " + ByteUnits[tier];
        }

        // ------------------------------------------------------------------ sure

        /// <summary>Saniyeyi kisa sureye cevirir. Ornek: 42s, 5m 12s / 42 sn, 5 dk 12 sn</summary>
        public static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0) return "∞";

            bool isTr = LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.Turkish;
            string secUnit = isTr ? "sn" : "s";
            string minUnit = isTr ? "dk" : "m";
            string hrUnit = isTr ? "sa" : "h";
            string dayUnit = isTr ? "g" : "d";

            if (seconds < 1.0) return "<1 " + secUnit;

            long total = (long)seconds;

            if (total < 60) return total + " " + secUnit;

            if (total < 3600)
            {
                long m = total / 60;
                long s = total % 60;
                return s > 0 ? (m + " " + minUnit + " " + s + " " + secUnit) : (m + " " + minUnit);
            }

            if (total < 86400)
            {
                long h = total / 3600;
                long m = (total % 3600) / 60;
                return m > 0 ? (h + " " + hrUnit + " " + m + " " + minUnit) : (h + " " + hrUnit);
            }

            long d = total / 86400;
            long hh = (total % 86400) / 3600;
            return hh > 0 ? (d + " " + dayUnit + " " + hh + " " + hrUnit) : (d + " " + dayUnit);
        }

        // ----------------------------------------------------------------- oran

        /// <summary>0-1 arasi orani yuzde olarak bicimler. TR: %42, EN: 42%</summary>
        public static string FormatPercent(double ratio01)
        {
            if (double.IsNaN(ratio01) || double.IsInfinity(ratio01)) return "--%";

            double p = ratio01 * 100.0;
            if (p < 0.0) p = 0.0;
            if (p > 100.0) p = 100.0;

            string valStr = p.ToString(p < 10.0 ? "0.#" : "0", Inv);
            bool isTr = LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.Turkish;
            return isTr ? ("%" + valStr) : (valStr + "%");
        }

        /// <summary>
        /// Bonus oranini yuzde olarak bicimler — KELEPCESIZ.
        ///
        /// <see cref="FormatPercent"/> 0-100 arasina kirpiyor cunku o ilerleme
        /// cubugu icin yazildi. Yukseltme kartlarindaki "su ana kadarki toplam
        /// katki" ise 100'u rahatlikla asiyor: 25 seviye Modeme Vurmak zaten
        /// %1500 ediyordu ama kart "+%100" yaziyordu. Oyuncu satin aldigi
        /// seylerin biriktigini goremiyordu.
        /// </summary>
        public static string FormatBonusPercent(double ratio)
        {
            if (double.IsNaN(ratio) || double.IsInfinity(ratio)) return "--%";

            double p = ratio * 100.0;
            if (p < 0.0) p = 0.0;

            // Buyuk degerlerde kisaltmaya gec: "+%1.5M" "+%152K"
            string valStr = p >= 100000.0
                ? FormatCompact(p)
                : p.ToString(p < 10.0 ? "0.#" : "0", Inv);

            bool isTr = LocalizationManager.Instance != null &&
                        LocalizationManager.Instance.CurrentLanguage == Language.Turkish;
            return isTr ? ("%" + valStr) : (valStr + "%");
        }

        /// <summary>Carpani bicimler. Ornek: 2x, 1.5x</summary>
        public static string FormatMultiplier(double mult)
        {
            if (double.IsNaN(mult) || double.IsInfinity(mult)) return "--x";
            return mult.ToString("0.##", Inv) + "x";
        }

        // ---------------------------------------------------------------- yardim

        /// <summary>
        /// Toplam 3 anlamli haneyi korur: 4.28 / 42.8 / 428.
        /// HUD'da sayi genisligi sabit kalir, rakamlar zipmaz.
        ///
        /// 1'IN ALTINDA OZEL KURAL — NEDEN GEREKLI
        /// ---------------------------------------
        /// Eski hal 1'in altindaki her seye "0.##" veriyordu. Gelir gostergesi
        /// oyunun basinda 0.0026 $/sn uretiyor; bu kalipla "0" olarak yuvarlanip
        /// ekranda "$0/sn" yaziyordu. Yani GERCEK bir gelir varken oyuncuya
        /// "hicbir sey kazanmiyorsun" deniyordu — olculdu ve oyunun ilk
        /// dakikalarindaki en yaniltici geri bildirim buydu.
        ///
        /// Simdi 1'in altinda EN AZ IKI ANLAMLI HANE garanti ediliyor:
        ///     0.5     -> "0.5"
        ///     0.06    -> "0.06"
        ///     0.0026  -> "0.0026"
        ///     0.00004 -> "0.00004"
        /// Ust sinir 8 hane: daha oteye gitmek HUD'da okunmayan bir kuyruk
        /// uretir, kazanci da yoktur.
        ///
        /// 1'in USTUNDE eski davranis aynen korunuyor — orada sabit genislik
        /// hala dogru tercih, cunku rakamlar zipladiginda goz yoruluyor.
        /// </summary>
        static string PrecisionFor(double v)
        {
            if (v >= 100.0) return "0";
            if (v >= 10.0) return "0.#";
            if (v >= 1.0) return "0.##";
            if (v <= 0.0) return "0.##";

            // v, (0,1) araliginda: ilk anlamli hanenin kaci ondalik oldugunu bul.
            //   0.0026 -> log10 = -2.58 -> taban -3 -> 4 ondalik yeter
            int lead = (int)Math.Floor(Math.Log10(v));
            int dec = -lead + 1;
            if (dec < 2) dec = 2;
            if (dec > 8) dec = 8;

            return "0." + new string('#', dec);
        }
    }
}
