using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Faz 0: iki ozel shader icin materyalleri olusturur ve derlenip derlenmediklerini
    /// dogrular. ShaderUtil.ShaderHasError, shader'in gercekten derlendigini kanitlar —
    /// import basarili olsa bile sozdizimi hatasi burada yakalanir.
    /// </summary>
    public static class ShaderMaterialTool
    {
        const string MaterialsFolder = "Assets/Materials";

        struct Spec
        {
            public string shaderName;
            public string materialPath;
            public Color color;
        }

        static readonly Spec[] Specs =
        {
            new Spec
            {
                shaderName = "SpeedDownload/SpriteSolidTint",
                materialPath = MaterialsFolder + "/NeedleSolidTint.mat",
                // Kademe 0-1 acik krem kadranlar icin koyu kirmizi baslangic
                color = new Color(0.78f, 0.13f, 0.13f, 1f)
            },
            new Spec
            {
                shaderName = "SpeedDownload/HueShift",
                materialPath = MaterialsFolder + "/DialHueShift.mat",
                color = Color.white
            }
        };

        [MenuItem("Tools/SpeedDownload/0. Create Shader Materials", false, 99)]
        public static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            int created = 0;
            int reused = 0;
            int failed = 0;

            foreach (Spec spec in Specs)
            {
                Shader shader = Shader.Find(spec.shaderName);

                if (shader == null)
                {
                    Debug.LogError("[SpeedDownload] Shader bulunamadi: " + spec.shaderName);
                    failed++;
                    continue;
                }

                if (ShaderUtil.ShaderHasError(shader))
                {
                    Debug.LogError("[SpeedDownload] Shader DERLENEMEDI: " + spec.shaderName +
                                   " — Inspector'da hata detayina bak.");
                    failed++;
                    continue;
                }

                var existing = AssetDatabase.LoadAssetAtPath<Material>(spec.materialPath);
                if (existing != null)
                {
                    if (existing.shader != shader) existing.shader = shader;
                    EditorUtility.SetDirty(existing);
                    reused++;
                }
                else
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", spec.color);
                    AssetDatabase.CreateAsset(mat, spec.materialPath);
                    created++;
                }

                Debug.Log("[SpeedDownload] OK  " + spec.shaderName +
                          "  (derleme hatasi yok, " +
                          shader.GetPropertyCount() + " property)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failed == 0)
            {
                Debug.Log("[SpeedDownload] Shader materyalleri hazir. Olusturulan: " + created +
                          ", zaten vardi: " + reused + ".");
            }
            else
            {
                Debug.LogError("[SpeedDownload] " + failed + " shader basarisiz.");
            }
        }
    }
}
