using UnityEngine;

namespace SpeedDownload.Data
{
    /// <summary>
    /// Bir baglanti kademesi (GDD Bolum 3).
    ///
    /// Hiz degerleri her zaman bit/saniye cinsindendir; gosterim birimini
    /// (bps/Kbps/Mbps...) NumberFormatter otomatik secer.
    /// </summary>
    [CreateAssetMenu(fileName = "ConnectionTier", menuName = "SpeedDownload/Connection Tier")]
    public class ConnectionTierSO : ScriptableObject
    {
        [Header("Kimlik")]
        public int tierIndex;
        public string tierName = "Yeni Kademe";
        [TextArea(2, 3)] public string flavorText;

        [Header("Hiz araligi (bit/saniye)")]
        public double minSpeed = 1.0;
        public double maxSpeed = 100.0;

        [Tooltip("Bu kademede tiklama olmadan devam eden taban hiz (bit/sn).")]
        public double baseSpeed = 1.0;

        [Header("Kadran davranisi")]
        [Tooltip("Kapaliysa ibre lineer haritalanir. Aralik 2 dekattan genisse " +
                 "lineer haritalama ibreyi dibe yapistirir — acik birakmak onerilir.")]
        public bool useLogarithmicScale = true;

        [Tooltip("Ibrenin hedefe oturma suresi (SmoothDamp). Ust kademelerde " +
                 "buyur -> ibre 'agirlasir'.")]
        public float needleInertia = 0.05f;

        [Tooltip("Tiklama birakildiginda hizin taban seviyeye donme hizi (1/sn).")]
        public float speedDecayRate = 1.8f;

        [Header("Gorsel")]
        public Sprite dialBackground;
        public Sprite connectionIcon;
        public Sprite roomBackground;

        [Tooltip("Oda arka planina uygulanan renk. Kademe 3 ve 5'in kendi odasi " +
                 "olmadigi icin komsu oda bu tint ile ayristirilir.")]
        public Color roomTint = Color.white;

        [Tooltip("Ibre rengi. dial_needle ici bos bir konturdur; SpriteSolidTint " +
                 "shader'i bu rengi kullanir. Koyu kadranlarda neon ton sec.")]
        public Color needleColor = new Color(0.78f, 0.13f, 0.13f, 1f);

        [Tooltip("Sonsuz kademeler (9+) icin kadran hue kaymasi (0-1). " +
                 "Tier 0-8'de 0 kalir.")]
        [Range(0f, 1f)] public float dialHueShift = 0f;

        [Header("Prosedurel")]
        [Tooltip("Kademe 9+ prosedurel olarak uretildiginde bu sablon kullanilir.")]
        public bool isProceduralTemplate = false;

        /// <summary>Kademe araliginin logaritmik genisligi (dekat sayisi).</summary>
        public double DecadeSpan
        {
            get { return System.Math.Log10(maxSpeed) - System.Math.Log10(minSpeed); }
        }

        /// <summary>
        /// Verilen hizin kadran uzerindeki normalize konumu (0 = minAngle, 1 = maxAngle).
        /// </summary>
        public float SpeedToNormalized(double speed)
        {
            if (speed <= minSpeed) return 0f;
            if (speed >= maxSpeed) return 1f;

            if (!useLogarithmicScale)
                return (float)((speed - minSpeed) / (maxSpeed - minSpeed));

            double logMin = System.Math.Log10(minSpeed);
            double logMax = System.Math.Log10(maxSpeed);
            return (float)((System.Math.Log10(speed) - logMin) / (logMax - logMin));
        }

        /// <summary>Normalize konumdan hiza (yukaridakinin tersi).</summary>
        public double NormalizedToSpeed(float t)
        {
            t = Mathf.Clamp01(t);

            if (!useLogarithmicScale)
                return minSpeed + (maxSpeed - minSpeed) * t;

            double logMin = System.Math.Log10(minSpeed);
            double logMax = System.Math.Log10(maxSpeed);
            return System.Math.Pow(10.0, logMin + (logMax - logMin) * t);
        }
    }
}
