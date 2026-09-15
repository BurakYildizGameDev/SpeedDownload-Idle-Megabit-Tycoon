using SpeedDownload.Core;
using TMPro;
using UnityEngine;

namespace SpeedDownload.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] string locKey;
        [SerializeField] string fallbackText;

        TMP_Text _text;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            LocalizationManager.LanguageChanged += UpdateText;
            UpdateText();
        }

        void OnDisable()
        {
            LocalizationManager.LanguageChanged -= UpdateText;
        }

        public void SetKey(string key, string fallback = "")
        {
            locKey = key;
            if (!string.IsNullOrEmpty(fallback)) fallbackText = fallback;
            UpdateText();
        }

        public void UpdateText()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null || string.IsNullOrEmpty(locKey)) return;

            if (LocalizationManager.Instance != null)
            {
                _text.text = LocalizationManager.Instance.Get(locKey, fallbackText);
            }
            else if (!string.IsNullOrEmpty(fallbackText))
            {
                _text.text = fallbackText;
            }
        }
    }
}
