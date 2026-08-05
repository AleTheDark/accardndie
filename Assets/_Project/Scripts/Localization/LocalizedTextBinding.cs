using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Localization
{
    /// <summary>
    /// Collega un Text UGUI serializzato a una chiave del catalogo e lo aggiorna
    /// automaticamente quando cambia la lingua. Utile per scene e prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedTextBinding : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField, TextArea(1, 4)] private string sourceFallback;

        private Text target;
        private object[] runtimeArguments;

        public string Key => key;

        public void Configure(string localizationKey, string fallback = null, params object[] arguments)
        {
            key = localizationKey;
            if (fallback != null)
                sourceFallback = fallback;
            runtimeArguments = arguments;
            Refresh();
        }

        private void OnEnable()
        {
            target = GetComponent<Text>();
            GameText.LocaleChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameText.LocaleChanged -= Refresh;
        }

        private void Refresh()
        {
            if (target == null)
                target = GetComponent<Text>();
            if (target == null || string.IsNullOrWhiteSpace(key))
                return;

            target.text = GameText.GetOrFallback(key, sourceFallback, runtimeArguments);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(sourceFallback))
            {
                Text text = GetComponent<Text>();
                if (text != null)
                    sourceFallback = text.text;
            }
        }
#endif
    }
}
