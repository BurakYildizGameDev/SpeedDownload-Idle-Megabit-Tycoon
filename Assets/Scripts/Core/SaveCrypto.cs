using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Kayit dosyasinin diskteki bicimi: sifreleme + butunluk imzasi.
    ///
    /// NEDEN GEREKLIYDI
    /// ----------------
    /// Onceki bicim duz JSON'du ve yaninda yalnizca YEDI alani kapsayan bir
    /// HMAC tasiyordu. Uc ayri kapi aciktı:
    ///
    ///   1. Imza kontrolu <c>saveVersion &gt;= 6 &amp;&amp; signature bos degil</c>
    ///      sartina baglıydı — yani "signature" satirini silmek dogrulamayi
    ///      tamamen atlatiyordu.
    ///   2. Imza yalnizca bakiye/kademe/kredi gibi birkac sayiyi kapsiyordu;
    ///      yukseltme seviyeleri, arsiv, slotlar ve aktif olay TAMAMEN
    ///      korumasizdi. Tum yukseltmeleri 9999 yapip dosyayi kaydetmek gecerli
    ///      bir imzayla sonuclaniyordu.
    ///   3. Dosya duz metindi: not defteriyle acilip degistirilebiliyordu.
    ///
    /// NE YAPIYOR
    /// ----------
    /// AES-256-CBC ile sifreler, ardindan sifreli metnin TAMAMINI HMAC-SHA256
    /// ile imzalar (encrypt-then-MAC). Anahtarlar PBKDF2 ile su ucluden
    /// turetilir: uygulama sirri + kuruluma ozel kimlik + tuz.
    ///
    /// DURUSTCE SINIRLARI
    /// ------------------
    /// Sunucu olmayan bir oyunda anahtar her zaman istemcide bulunur; yeterince
    /// kararli biri IL2CPP ikilisini cozup sirri cikarabilir. Amac mukemmel
    /// koruma degil, GERCEKCI olan: dosyayi bir metin editoruyle degistirmeyi
    /// imkansiz kilmak ve baska bir cihazdan kopyalanan kaydi reddetmek.
    /// Hile vakalarinin nerdeyse tamami bu iki yoldan geliyor.
    ///
    /// IL2CPP + KOD BUDAMA (STRIPPING) NOTU — ONEMLI
    /// ---------------------------------------------
    /// Proje Android'de IL2CPP ve "High" managed stripping ile derleniyor.
    /// Bu birlesim, YANSIMA (reflection) ile cozulen tipleri siler.
    ///
    /// .NET'in kripto FABRIKA metotlari tam olarak boyle calisiyor:
    ///   Aes.Create()                     -> "AesCryptoServiceProvider" (ADIYLA)
    ///   RandomNumberGenerator.Create()   -> "RNGCryptoServiceProvider" (ADIYLA)
    ///   Rfc2898DeriveBytes(SHA256)       -> ic HMAC'i adiyla kurabiliyor
    ///
    /// Editorde stripping yok, dolayisiyla hepsi sorunsuz calisir; APK'da tip
    /// silinmisse ilk kayit denemesi patlar. Yani "editorde calisti" bu hatayi
    /// HIC yakalayamaz — kayip yalnizca cihazda, oyuncunun ilerlemesi olarak
    /// ortaya cikar.
    ///
    /// Bu yuzden burada hicbir fabrika metodu kullanilmiyor:
    ///   - AES icin dogrudan <c>new AesManaged()</c>
    ///   - Rastgelelik icin dogrudan <c>new RNGCryptoServiceProvider()</c>
    ///   - PBKDF2 elle yazildi ve dogrudan <c>new HMACSHA256()</c> kullaniyor
    ///     (dogrulugu Rfc2898DeriveBytes'a karsi birim testiyle kanitlaniyor)
    ///
    /// Hepsi statik referans oldugu icin linker onlari GOREBILIYOR ve budamıyor.
    /// Assets/link.xml ikinci emniyet kemeri.
    /// </summary>
    public static class SaveCrypto
    {
        /// <summary>
        /// Sifreli dosyanin basindaki imza. Eski (duz JSON) kayitlari ayirt
        /// etmek icin: '{' ile baslayan her sey eski bicimdir.
        /// </summary>
        public const string Magic = "SDMT1:";

        // Uygulama sirri. Tek basina yeterli degil — kuruluma ozel kimlikle
        // birlestiriliyor, dolayisiyla bunu bilmek baska bir cihazin kaydini
        // acmaya yetmiyor.
        const string AppSecret = "SpeedDownload.MegabitTycoon/v1#7f3a9c2e";

        const string InstallIdPrefKey = "sd_install_id";
        const string SaltPrefKey = "sd_save_salt";

        const int SaltSize = 16;
        const int IvSize = 16;
        const int MacSize = 32;
        const int KdfIterations = 20000;

        // Anahtar turetme (PBKDF2, 20k tur) telefonda birkac on milisaniye
        // suruyor. Otomatik kayit 15 saniyede bir calistigi icin her yazimda
        // yeniden turetmek olculebilir bir israf olurdu — oturum boyunca
        // onbellege aliniyor. Tuz kuruluma ozel ve sabit oldugu icin bu guvenli:
        // her yazimda degisen sey IV, ki o zaten her seferinde yeniden uretiliyor.
        static byte[] _aesKey;
        static byte[] _macKey;
        static byte[] _salt;

        /// <summary>Son cozme denemesinin sonucu — teshis icin.</summary>
        public static string Diagnostics { get; private set; } = "kullanilmadi";

        // ------------------------------------------------------------------
        // Kurulum kimligi
        // ------------------------------------------------------------------

        /// <summary>
        /// Bu kuruluma ozel kimlik. Ilk calistirmada uretilip PlayerPrefs'e
        /// yaziliyor.
        ///
        /// Neden <c>SystemInfo.deviceUniqueIdentifier</c> degil: Android'de o
        /// deger isletim sistemi guncellemesiyle DEGISEBILIYOR ve degistigi anda
        /// oyuncunun kendi kaydi acilamaz hale gelirdi. PlayerPrefs ile kayit
        /// dosyasi ayni uygulama deposunda yasiyor: biri silinirse digeri de
        /// silinir, yani ikisi hep uyumlu kalir.
        ///
        /// Cihaz kimligi yine de karisima giriyor — kaydi baska bir telefona
        /// kopyalamak icin PlayerPrefs dosyasini da tasimak gerekiyor.
        /// </summary>
        static string InstallId
        {
            get
            {
                string id = PlayerPrefs.GetString(InstallIdPrefKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(InstallIdPrefKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        static byte[] Salt
        {
            get
            {
                if (_salt != null) return _salt;

                string stored = PlayerPrefs.GetString(SaltPrefKey, "");
                if (!string.IsNullOrEmpty(stored))
                {
                    try
                    {
                        byte[] decoded = Convert.FromBase64String(stored);
                        if (decoded.Length == SaltSize) { _salt = decoded; return _salt; }
                    }
                    catch (Exception) { /* bozuksa yeniden uret */ }
                }

                _salt = new byte[SaltSize];
                FillRandom(_salt);

                PlayerPrefs.SetString(SaltPrefKey, Convert.ToBase64String(_salt));
                PlayerPrefs.Save();
                return _salt;
            }
        }

        static void EnsureKeys()
        {
            if (_aesKey != null && _macKey != null) return;

            string password = AppSecret + "|" + InstallId + "|" + SystemInfo.deviceUniqueIdentifier;

            byte[] material = Pbkdf2(Encoding.UTF8.GetBytes(password), Salt, KdfIterations, 64);

            _aesKey = new byte[32];
            _macKey = new byte[32];
            Buffer.BlockCopy(material, 0, _aesKey, 0, 32);
            Buffer.BlockCopy(material, 32, _macKey, 0, 32);
        }

        /// <summary>
        /// Turetilmis anahtarlari unutur. Kayit silindiginde cagriliyor —
        /// yeni bir tur icin taze kimlik/tuz uretilebilsin.
        /// </summary>
        public static void ResetIdentity()
        {
            _aesKey = null;
            _macKey = null;
            _salt = null;

            PlayerPrefs.DeleteKey(InstallIdPrefKey);
            PlayerPrefs.DeleteKey(SaltPrefKey);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // Sifreleme
        // ------------------------------------------------------------------

        /// <summary>
        /// JSON metnini diske yazilabilir bicime cevirir.
        ///
        /// Bicim: <c>Magic + Base64( iv[16] | mac[32] | ciphertext )</c>
        /// MAC, iv ile ciphertext'in BIRLESIMI uzerinden hesaplanir
        /// (encrypt-then-MAC): boylece IV'yi degistirerek cozumu bozmaya
        /// calisan bir saldiri da imzada yakalanir.
        /// </summary>
        public static string Encrypt(string json)
        {
            EnsureKeys();

            byte[] plain = Encoding.UTF8.GetBytes(json);
            byte[] iv = new byte[IvSize];
            FillRandom(iv);

            byte[] cipher;
            using (var aes = NewAes(iv))
            using (var enc = aes.CreateEncryptor())
                cipher = enc.TransformFinalBlock(plain, 0, plain.Length);

            byte[] mac = ComputeMac(iv, cipher);

            var payload = new byte[IvSize + MacSize + cipher.Length];
            Buffer.BlockCopy(iv, 0, payload, 0, IvSize);
            Buffer.BlockCopy(mac, 0, payload, IvSize, MacSize);
            Buffer.BlockCopy(cipher, 0, payload, IvSize + MacSize, cipher.Length);

            return Magic + Convert.ToBase64String(payload);
        }

        /// <summary>
        /// Diskteki metni JSON'a cevirir. Basarisizsa null doner ve
        /// <see cref="Diagnostics"/> sebebi yazar.
        ///
        /// Imza gecersizse cozulmus veri DONDURULMEZ. Eski surumdeki en buyuk
        /// acik buydu: dogrulama basarisiz olsa bile veri kullanilabiliyordu.
        /// </summary>
        public static string Decrypt(string stored)
        {
            if (string.IsNullOrEmpty(stored)) { Diagnostics = "bos dosya"; return null; }
            if (!IsEncrypted(stored)) { Diagnostics = "sifreli bicimde degil"; return null; }

            try
            {
                EnsureKeys();

                byte[] payload = Convert.FromBase64String(stored.Substring(Magic.Length).Trim());
                if (payload.Length <= IvSize + MacSize)
                {
                    Diagnostics = "govde cok kisa";
                    return null;
                }

                var iv = new byte[IvSize];
                var mac = new byte[MacSize];
                var cipher = new byte[payload.Length - IvSize - MacSize];

                Buffer.BlockCopy(payload, 0, iv, 0, IvSize);
                Buffer.BlockCopy(payload, IvSize, mac, 0, MacSize);
                Buffer.BlockCopy(payload, IvSize + MacSize, cipher, 0, cipher.Length);

                byte[] expected = ComputeMac(iv, cipher);
                if (!FixedTimeEquals(expected, mac))
                {
                    Diagnostics = "imza gecersiz (tahrifat veya baska cihazin kaydi)";
                    return null;
                }

                using (var aes = NewAes(iv))
                using (var dec = aes.CreateDecryptor())
                {
                    byte[] plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                    Diagnostics = "cozuldu";
                    return Encoding.UTF8.GetString(plain);
                }
            }
            catch (Exception e)
            {
                Diagnostics = "cozme hatasi: " + e.Message;
                return null;
            }
        }

        public static bool IsEncrypted(string stored)
        {
            return !string.IsNullOrEmpty(stored) && stored.StartsWith(Magic, StringComparison.Ordinal);
        }

        static byte[] ComputeMac(byte[] iv, byte[] cipher)
        {
            var buffer = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, buffer, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, buffer, iv.Length, cipher.Length);

            using (var hmac = new HMACSHA256(_macKey))
                return hmac.ComputeHash(buffer);
        }

        // ------------------------------------------------------------------
        // Yansima kullanmayan kripto yardimcilari
        //
        // Hepsi SOMUT tipe dogrudan referans veriyor; boylece IL2CPP linker'i
        // onlari statik olarak goruyor ve "High" budama seviyesinde bile
        // silmiyor. Ayrintili gerekce sinif yorumunda.
        // ------------------------------------------------------------------

        /// <summary>Kriptografik rastgele bayt (fabrika metodu yok).</summary>
        static void FillRandom(byte[] buffer)
        {
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(buffer);
        }

        /// <summary>
        /// Yapilandirilmis AES-256-CBC ornegi.
        ///
        /// <c>Aes.Create()</c> yerine <c>new AesManaged()</c>: ilki tipi adiyla
        /// cozuyor ve budanabiliyor. AesManaged tamamen yonetilen bir uygulama,
        /// her platformda ayni sekilde calisiyor. Bizim yukumuz ~500 bayt
        /// oldugu icin hiz farki olculemez.
        /// </summary>
        static AesManaged NewAes(byte[] iv)
        {
            var aes = new AesManaged();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _aesKey;
            aes.IV = iv;
            return aes;
        }

        /// <summary>
        /// PBKDF2-HMAC-SHA256 (RFC 8018 Bolum 5.2).
        ///
        /// <c>Rfc2898DeriveBytes</c> yerine elle yazildi: o sinif SHA256
        /// istendiginde ic HMAC'ini adiyla kurabiliyor ve budanma riski
        /// tasiyor. Burasi dogrudan <c>new HMACSHA256()</c> kullaniyor.
        ///
        /// Dogrulugu tahmine birakilmadi: birim testi ciktisini
        /// Rfc2898DeriveBytes'in ciktisiyla BAYT BAYT karsilastiriyor
        /// (bkz. SaveCrypto_Pbkdf2_MatchesReferenceImplementation).
        /// </summary>
        static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations, int outputBytes)
        {
            using (var hmac = new HMACSHA256(password))
            {
                int hashLen = hmac.HashSize / 8;                 // 32
                int blocks = (outputBytes + hashLen - 1) / hashLen;

                var output = new byte[blocks * hashLen];
                var blockInput = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, blockInput, 0, salt.Length);

                for (int block = 1; block <= blocks; block++)
                {
                    // INT_32_BE(block) tuzun sonuna ekleniyor.
                    blockInput[salt.Length + 0] = (byte)(block >> 24);
                    blockInput[salt.Length + 1] = (byte)(block >> 16);
                    blockInput[salt.Length + 2] = (byte)(block >> 8);
                    blockInput[salt.Length + 3] = (byte)block;

                    byte[] u = hmac.ComputeHash(blockInput);
                    var accumulated = new byte[hashLen];
                    Buffer.BlockCopy(u, 0, accumulated, 0, hashLen);

                    for (int i = 1; i < iterations; i++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int j = 0; j < hashLen; j++) accumulated[j] ^= u[j];
                    }

                    Buffer.BlockCopy(accumulated, 0, output, (block - 1) * hashLen, hashLen);
                }

                if (output.Length == outputBytes) return output;

                var trimmed = new byte[outputBytes];
                Buffer.BlockCopy(output, 0, trimmed, 0, outputBytes);
                return trimmed;
            }
        }

        /// <summary>
        /// Kripto altyapisi bu cihazda gercekten calisiyor mu?
        ///
        /// Acilista bir kez cagriliyor. Budama yuzunden bir tip silinmisse
        /// sonuc SESSIZ bir kayit kaybi olurdu; bu kontrol sorunu ilk saniyede
        /// ve acikca gunluge yaziyor.
        /// </summary>
        public static bool SelfTest(out string detail)
        {
            try
            {
                const string probe = "{\"saveVersion\":8}";
                string round = Decrypt(Encrypt(probe));

                if (round == probe) { detail = "kripto calisiyor"; return true; }

                detail = "gidis-donus eslesmedi (" + Diagnostics + ")";
                return false;
            }
            catch (Exception e)
            {
                detail = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Sabit surede karsilastirma.
        ///
        /// Ilk farkli baytta donen bir karsilastirma, imzayi bayt bayt tahmin
        /// etmeye izin veren bir zamanlama kanali birakir. Tek oyunculuk bir
        /// oyunda somurulmesi zor ama dogrusunu yazmanin maliyeti de yok.
        /// </summary>
        static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
