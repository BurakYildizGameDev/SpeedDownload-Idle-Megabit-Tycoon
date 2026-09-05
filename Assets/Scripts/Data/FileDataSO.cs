using UnityEngine;

namespace SpeedDownload.Data
{
    /// <summary>
    /// Indirilebilir bir dosya (GDD Bolum 5).
    ///
    /// Boyut bit cinsindendir; indirme suresi = sizeBits / anlikHiz (bit/sn).
    /// Bu yuzden boyutlar kademe hizina gore turetilir (bkz. DataAssetGenerator) —
    /// GDD v2.2'deki sabit boyut tablosu dusuk kademelerde 30+ dakikalik
    /// indirmelere yol aciyordu.
    /// </summary>
    [CreateAssetMenu(fileName = "FileData", menuName = "SpeedDownload/File Data")]
    public class FileDataSO : ScriptableObject
    {
        [Header("Kimlik")]
        public string displayName = "dosya.txt";
        public Sprite icon;

        [Header("Boyut ve odul")]
        [Tooltip("Dosya boyutu, BIT cinsinden. 1 KB = 8192 bit.")]
        public double sizeBits = 8192.0;

        [Tooltip("Tamamlaninca verilen temel para. Carpanlar (premium, prestij, " +
                 "happy hour) bunun uzerine uygulanir.")]
        public double baseReward = 1.0;

        [Header("Erisilebilirlik")]
        [Tooltip("Bu dosyanin cikmaya basladigi baglanti kademesi.")]
        public int minConnectionTier = 0;

        [Tooltip("Bu dosyanin artik cikmadigi kademe (dahil). -1 = sinir yok.")]
        public int maxConnectionTier = -1;

        [Tooltip("Ayni kademedeki dosyalar arasinda secilme agirligi.")]
        public float weight = 1f;

        [Header("Prosedurel")]
        [Tooltip("Kademe 9+ icin mega arsiv sablonu.")]
        public bool isProceduralTemplate = false;

        /// <summary>Bu dosya verilen kademede cikabilir mi?</summary>
        public bool IsAvailableAt(int connectionTier)
        {
            if (connectionTier < minConnectionTier) return false;
            if (maxConnectionTier >= 0 && connectionTier > maxConnectionTier) return false;
            return true;
        }
    }
}
