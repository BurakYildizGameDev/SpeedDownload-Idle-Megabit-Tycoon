using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Assets/Sprites altindaki 82 PNG'ye Asset_Integration_Guide.md Bolum 2'deki
    /// import ayarlarini toplu uygular.
    ///
    /// Idempotent: sadece gercekten degisen dosyalar reimport edilir, tekrar
    /// calistirmak zarar vermez.
    ///
    /// 9-slice border ve ibre pivot degerleri tahmin degil, sprite'larin alfa
    /// kanalindan olculmustur (bkz. Asset_Integration_Guide.md Bolum 8).
    /// </summary>
    public static class SpriteImportTool
    {
        const string Root = "Assets/Sprites";

        /// <summary>
        /// Kod tarafindan <c>Resources.Load</c> ile cagrilan sprite'lar.
        ///
        /// Bu klasor uzun sure taranmiyordu ve icindeki 12 PNG Unity'nin
        /// varsayilan ayarlariyla iceri girmisti:
        ///   - SpriteImportMode = Multiple (tek bir alt-sprite'a otomatik
        ///     kirpilmis), yani ikon cercevesi kayiyor ve 9-slice imkansiz,
        ///   - maxTextureSize 2048, Android/iOS icin ASTC override YOK
        ///     (mobilde gereksiz bellek ve paket boyutu),
        ///   - ui_speech_bubble 9-slice olarak kullaniliyor ama border'i 0.
        ///
        /// Bir aracin "tum sprite'lari duzenler" demesi ama bir klasoru
        /// atlamasi, atlanan klasorun sessizce bozuk kalmasi demek.
        /// </summary>
        const string ResourcesRoot = "Assets/Resources";

        static readonly string[] SearchRoots = { Root, ResourcesRoot };

        struct Rule
        {
            public TextureImporterType type;
            public int maxSize;
            public bool alphaIsTransparency;
            public Vector4 border;      // (L, B, R, T) kaynak pikseli cinsinden
            public bool customPivot;
            public Vector2 pivot;
            public bool mobileOverride;
            public TextureImporterFormat mobileFormat;
        }

        // ------------------------------------------------------------ menu

        [MenuItem("Tools/SpeedDownload/1. Apply Sprite Import Settings", false, 100)]
        public static void ApplyAll()
        {
            BuildDialNeedleAsset();

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", SearchRoots);
            if (guids.Length == 0)
            {
                Debug.LogError("[SpeedDownload] " + Root + " altinda hic doku bulunamadi.");
                return;
            }

            int changed = 0;
            int alreadyOk = 0;
            var log = new StringBuilder();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    EditorUtility.DisplayProgressBar(
                        "SpeedDownload — Import ayarlari",
                        Path.GetFileName(path),
                        (float)i / guids.Length);

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    if (ApplyTo(importer, Resolve(path)))
                    {
                        changed++;
                        log.AppendLine("  + " + path);
                    }
                    else
                    {
                        alreadyOk++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                "[SpeedDownload] Import ayarlari uygulandi.\n" +
                "  Toplam doku : " + guids.Length + "\n" +
                "  Guncellenen : " + changed + "\n" +
                "  Zaten dogru : " + alreadyOk + "\n" +
                (changed > 0 ? log.ToString() : ""));
        }

        [MenuItem("Tools/SpeedDownload/Verify Sprite Import Settings", false, 101)]
        public static void VerifyAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", SearchRoots);
            var report = new StringBuilder();
            int bad = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                Rule rule = Resolve(path);
                var problems = new StringBuilder();

                if (importer.textureType != rule.type) problems.Append(" type=" + importer.textureType);
                if (importer.maxTextureSize != rule.maxSize) problems.Append(" maxSize=" + importer.maxTextureSize);
                if (importer.mipmapEnabled) problems.Append(" mipmap=ON");
                if (importer.wrapMode != TextureWrapMode.Clamp) problems.Append(" wrap=" + importer.wrapMode);
                if (importer.isReadable) problems.Append(" readable=ON");
                if (importer.alphaIsTransparency != rule.alphaIsTransparency)
                    problems.Append(" alphaIsTransparency=" + importer.alphaIsTransparency);

                if (rule.type == TextureImporterType.Sprite)
                {
                    var s = new TextureImporterSettings();
                    importer.ReadTextureSettings(s);

                    if (s.spriteBorder != rule.border)
                        problems.Append(" border=" + s.spriteBorder + " (beklenen " + rule.border + ")");

                    int wantAlign = rule.customPivot ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.Center;
                    if (s.spriteAlignment != wantAlign)
                        problems.Append(" align=" + (SpriteAlignment)s.spriteAlignment);

                    if (rule.customPivot && s.spritePivot != rule.pivot)
                        problems.Append(" pivot=" + s.spritePivot + " (beklenen " + rule.pivot + ")");
                }

                if (problems.Length > 0)
                {
                    bad++;
                    report.AppendLine("  ! " + Path.GetFileName(path) + " ->" + problems);
                }
            }

            if (bad == 0)
                Debug.Log("[SpeedDownload] Dogrulama: " + guids.Length + " dokunun tamami beklenen ayarlarda.");
            else
                Debug.LogWarning("[SpeedDownload] Dogrulama: " + bad + " / " + guids.Length +
                                 " doku beklenenden farkli.\n" + report);
        }

        public static void BuildDialNeedleAsset()
        {
            int width = 512;
            int height = 128;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            float cx = width * 0.10f;  // 51.2 px (pivot center)
            float cy = height * 0.50f; // 64.0 px

            float tipX = 492f;
            float baseRadius = 22f;
            float baseHalfWidth = 12f;
            float tipHalfWidth = 1.8f;
            float tailX = 18f;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    float dist = GetDistanceToNeedle(px, py, cx, cy, tipX, baseRadius, baseHalfWidth, tipHalfWidth, tailX);

                    // Sub-pixel anti-aliasing via signed distance field
                    float alpha = Mathf.Clamp01(0.5f - dist);

                    if (alpha > 0.001f)
                    {
                        // Slight center ridge brightness for sleek 3D gauge needle feel
                        float dy = Mathf.Abs(py - cy);
                        float maxHalfW = px < cx ? baseRadius : Mathf.Lerp(baseHalfWidth, tipHalfWidth, Mathf.Clamp01((px - cx) / (tipX - cx)));
                        float ridge = 1.0f - Mathf.Clamp01(dy / Mathf.Max(1f, maxHalfW)) * 0.15f;

                        pixels[y * width + x] = new Color(ridge, ridge, ridge, alpha);
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string path = "Assets/Sprites/Dial/dial_needle.png";
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.Log("[SpriteImportTool] Kusursuz, dolgulu ve anti-alias ibre olusturuldu: " + path);
        }

        static float GetDistanceToNeedle(float px, float py, float cx, float cy, float tipX, float baseRadius, float baseHalfW, float tipHalfW, float tailX)
        {
            float dy = Mathf.Abs(py - cy);

            // 1. Pivot center circle cap
            float distCap = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) - baseRadius;

            // 2. Rounded tail (behind pivot)
            float distTail = float.MaxValue;
            if (px < cx)
            {
                float tailRadius = (cx - tailX) * 0.95f;
                distTail = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) - tailRadius;
            }

            // 3. Tapered Needle Blade (from cx to tipX)
            float distBlade = float.MaxValue;
            if (px >= cx && px <= tipX)
            {
                float t = (px - cx) / (tipX - cx);
                float bladeHalfW = Mathf.Lerp(baseHalfW, tipHalfW, t);

                // distance to top/bottom edges of blade
                float dEdge = dy - bladeHalfW;
                distBlade = dEdge;
            }
            else if (px > tipX)
            {
                // Tip cap / point
                distBlade = Vector2.Distance(new Vector2(px, py), new Vector2(tipX, cy)) - tipHalfW;
            }

            // Union (minimum distance)
            float d = Mathf.Min(distCap, distBlade);
            if (px < cx)
            {
                d = Mathf.Min(d, distTail);
            }

            return d;
        }

        // ------------------------------------------------------------ kurallar

        static Rule Resolve(string assetPath)
        {
            string file = Path.GetFileNameWithoutExtension(assetPath);
            string dir = Path.GetFileName(Path.GetDirectoryName(assetPath));

            var r = new Rule
            {
                type = TextureImporterType.Sprite,
                maxSize = 512,
                alphaIsTransparency = true,
                border = Vector4.zero,
                customPivot = false,
                pivot = new Vector2(0.5f, 0.5f),
                mobileOverride = true,
                mobileFormat = TextureImporterFormat.ASTC_6x6
            };

            switch (dir)
            {
                case "Resources":
                    // Kod tarafindan Resources.Load ile cagrilan sprite'lar.
                    // Bunlar hicbir zaman elle ayarlanmadi; Unity'nin varsayilani
                    // olan Multiple modda ve 2048 px olarak duruyorlardi.
                    //
                    // Maskot yuzleri kadranin yaninda buyuk ciziliyor (512);
                    // konusma balonu kendi oraninda (1035x512) ciziliyor, bu
                    // yuzden 512 gerekiyor. Geri kalan ui_icon_* ve rozetler
                    // HUD'da kucuk gorunuyor: 256 yeterli, bellekte 4 kat tasarruf.
                    r.maxSize = (file.StartsWith("mascot_") || file == "ui_speech_bubble")
                        ? 512 : 256;
                    break;

                case "Backgrounds":
                    // Opak 16:9 oda arka planlari — alfa yok, dosya boyutu icin daha agresif blok
                    r.maxSize = 1024;
                    r.alphaIsTransparency = false;
                    r.mobileFormat = TextureImporterFormat.ASTC_8x8;
                    break;

                case "Dial":
                    r.maxSize = 512;
                    if (file == "dial_needle")
                    {
                        // Olculdu: tabandaki dairenin merkezi x = 51,2 px = 0,1 x 512
                        r.customPivot = true;
                        r.pivot = new Vector2(0.1f, 0.5f);
                    }
                    break;

                case "FX":
                case "Mascot":
                    r.maxSize = 512;
                    break;

                case "FileIcons":
                case "UpgradeIcons":
                    // HUD'da kucuk gorunuyorlar; 256 yeterli, bellekte 4 kat tasarruf
                    r.maxSize = 256;
                    break;

                case "Events":
                    r.maxSize = (file == "event_happyhour_banner") ? 512 : 256;
                    break;

                case "Branding":
                    if (file == "logo_icon_appstore")
                    {
                        // Unity sprite'i degil — Player Settings > Icon alanina atanir
                        r.type = TextureImporterType.Default;
                        r.maxSize = 1024;
                        r.alphaIsTransparency = false;
                        r.mobileOverride = false;
                    }
                    else
                    {
                        r.maxSize = 512;
                    }
                    break;

                case "UI":
                    // 9-slice border'lar alfa kanalindan olculdu (Rehber Bolum 8)
                    if (file.StartsWith("ui_button_"))
                    {
                        r.maxSize = 512;
                        r.border = new Vector4(70f, 60f, 70f, 60f);
                    }
                    else if (file == "ui_panel_bg")
                    {
                        r.maxSize = 512;
                        r.border = new Vector4(30f, 45f, 30f, 30f); // alt kenarda 35 px golge var
                    }
                    else if (file == "ui_progressbar_frame")
                    {
                        r.maxSize = 512;
                        r.border = new Vector4(75f, 20f, 75f, 20f);
                    }
                    else if (file == "ui_progressbar_fill")
                    {
                        r.maxSize = 512;
                        r.border = new Vector4(55f, 10f, 55f, 10f);
                    }
                    else if (file == "ui_heat_gauge" || file == "ui_heat_fill")
                    {
                        // Isi gostergesi kadranla ayni olcekte ciziliyor (563 px'e
                        // kadar buyuyor); 256'ya dusurmek yayin kenarlarini bulaniklastirir.
                        r.maxSize = 512;
                    }
                    else if (file == "ui_node_frame")
                    {
                        // Prestij yetenek agaci dugumu — ikonu cerceveliyor, buyuk cizilir.
                        r.maxSize = 512;
                    }
                    else if (file == "ui_card_file_choice")
                    {
                        // TODO (Faz 13): 9-slice border'i alfa kanalindan olcup gir.
                        // Kart kullanilana kadar Single olarak durmasi sorun degil.
                        r.maxSize = 512;
                    }
                    else
                    {
                        // ui_icon_*, ui_tab_*, ui_badge_*
                        r.maxSize = 256;
                    }
                    break;
            }

            return r;
        }

        // ------------------------------------------------------------ uygulama

        static bool ApplyTo(TextureImporter imp, Rule rule)
        {
            bool changed = false;

            if (imp.textureType != rule.type) { imp.textureType = rule.type; changed = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }
            if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; changed = true; }
            if (imp.isReadable) { imp.isReadable = false; changed = true; }
            if (imp.alphaIsTransparency != rule.alphaIsTransparency)
            {
                imp.alphaIsTransparency = rule.alphaIsTransparency;
                changed = true;
            }
            if (imp.maxTextureSize != rule.maxSize) { imp.maxTextureSize = rule.maxSize; changed = true; }

            if (rule.type == TextureImporterType.Sprite)
            {
                if (imp.spriteImportMode != SpriteImportMode.Single)
                {
                    imp.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (!Mathf.Approximately(imp.spritePixelsPerUnit, 100f))
                {
                    imp.spritePixelsPerUnit = 100f;
                    changed = true;
                }

                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                bool settingsChanged = false;

                int wantAlign = rule.customPivot ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.Center;
                if (s.spriteAlignment != wantAlign) { s.spriteAlignment = wantAlign; settingsChanged = true; }

                if (rule.customPivot && s.spritePivot != rule.pivot)
                {
                    s.spritePivot = rule.pivot;
                    settingsChanged = true;
                }

                if (s.spriteBorder != rule.border) { s.spriteBorder = rule.border; settingsChanged = true; }

                if (settingsChanged)
                {
                    imp.SetTextureSettings(s);
                    changed = true;
                }
            }

            if (rule.mobileOverride)
            {
                if (ApplyPlatform(imp, "Android", rule)) changed = true;
                if (ApplyPlatform(imp, "iPhone", rule)) changed = true;
            }

            if (changed) imp.SaveAndReimport();
            return changed;
        }

        /// <summary>
        /// ASTC yalnizca mobil platformda anlamli; Editor/Standalone varsayilan
        /// "Compressed" olarak birakilir.
        /// </summary>
        static bool ApplyPlatform(TextureImporter imp, string platform, Rule rule)
        {
            TextureImporterPlatformSettings ps = imp.GetPlatformTextureSettings(platform);

            bool needsUpdate = !ps.overridden
                               || ps.maxTextureSize != rule.maxSize
                               || ps.format != rule.mobileFormat
                               || ps.textureCompression != TextureImporterCompression.Compressed;

            if (!needsUpdate) return false;

            ps.overridden = true;
            ps.maxTextureSize = rule.maxSize;
            ps.format = rule.mobileFormat;
            ps.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(ps);
            return true;
        }
    }
}
