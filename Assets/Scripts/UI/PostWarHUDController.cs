using UnityEngine;
using UnityEngine.UIElements;
using TrashCount.Data;
using PostWarUI;

namespace TrashCount.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class PostWarHUDController : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private PlayerData playerData;

        [Header("Maximum Capacities")]
        public float maxHealthy = 100f;
        public float maxHungry = 100f;
        public float maxStamina = 100f;

        [Header("Colors")]
        public Color healthColor = new Color(0.29f, 0.42f, 0.31f, 1f); // Canvas Green
        public Color hungerColor = new Color(0.72f, 0.52f, 0.04f, 1f); // Ochre Amber
        public Color staminaColor = new Color(0.31f, 0.40f, 0.45f, 1f); // Slate Blue
        public Color warningColor = new Color(0.64f, 0.23f, 0.16f, 1f); // Rust Red

        private CircularGauge _healthGauge;
        private CircularGauge _hungerGauge;
        private CircularGauge _staminaGauge;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _healthGauge = root.Q<CircularGauge>("health-gauge");
            _hungerGauge = root.Q<CircularGauge>("hunger-gauge");
            _staminaGauge = root.Q<CircularGauge>("stamina-gauge");

            // Assign individual inspector colors directly to elements on start!
            ApplyInitialColors();
        }

        private void ApplyInitialColors()
        {
            if (_healthGauge != null) _healthGauge.fillColor = healthColor;
            if (_hungerGauge != null) _hungerGauge.fillColor = hungerColor;
            if (_staminaGauge != null) _staminaGauge.fillColor = staminaColor;
        }

        private void Update()
        {
            if (playerData == null) return;

            // Compute percentage ratios (0.0 to 1.0)
            float healthPct = Mathf.Clamp01(playerData.Healthy / maxHealthy);
            float hungerPct = Mathf.Clamp01(playerData.Hungry / maxHungry);
            float staminaPct = Mathf.Clamp01(playerData.Stamina / maxStamina);

            // Health Gauge Update
            if (_healthGauge != null)
            {
                _healthGauge.progress = healthPct;
                _healthGauge.fillColor = healthPct <= 0.25f ? GetPulseColor(warningColor) : healthColor;
            }

            // Hunger Gauge Update
            if (_hungerGauge != null)
            {
                _hungerGauge.progress = hungerPct;
                _hungerGauge.fillColor = hungerPct <= 0.25f ? GetPulseColor(warningColor) : hungerColor;
            }

            // Stamina Gauge Update
            if (_staminaGauge != null)
            {
                _staminaGauge.progress = staminaPct;
                _staminaGauge.fillColor = staminaPct <= 0.25f ? GetPulseColor(warningColor) : staminaColor;
            }
        }

        private Color GetPulseColor(Color baseColor)
        {
            float pulse = (Mathf.Sin(Time.time * 6f) * 0.4f) + 0.6f;
            return new Color(baseColor.r * pulse, baseColor.g * pulse, baseColor.b * pulse, baseColor.a);
        }
    }
}
