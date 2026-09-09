using UnityEngine;

namespace SpeedDownload.UI
{
    /// <summary>
    /// RectTransform'u Screen.safeArea'ya oturtur — centik ve alt cubuk olan
    /// telefonlarda ust HUD'un kesilmesini engeller.
    ///
    /// Ekran donmez (portre kilitli) ama editorde Game view cozunurlugu
    /// degistiginde safeArea da degisir, o yuzden her karede degisim kontrol
    /// edilir. Kontrol iki struct karsilastirmasi — olcum yapmaya degmeyecek
    /// kadar ucuz.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea == _lastSafeArea &&
                Screen.width == _lastScreen.x && Screen.height == _lastScreen.y) return;

            Apply();
        }

        void Apply()
        {
            if (_rect == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            // safeArea piksel cinsinden; anchor'lar 0-1 oldugu icin normalize et.
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            // Editorde safeArea ile Screen.width/height bazen uyumsuz raporlanir
            // (simulator ayari, odak degisimi). Kelepceleme olmazsa oran 1'i asip
            // tum arayuzu ekran disina tasirdi.
            min.x = Mathf.Clamp01(min.x);
            min.y = Mathf.Clamp01(min.y);
            max.x = Mathf.Clamp(max.x, min.x, 1f);
            max.y = Mathf.Clamp(max.y, min.y, 1f);

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
