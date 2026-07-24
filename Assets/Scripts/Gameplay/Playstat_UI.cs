using UnityEngine;
using UnityEngine.UI;

public class Playstat_UI : MonoBehaviour
{
    [Header("UI Image Fill References")]
    public Image healthyImage;
    public Image hungryImage;
    public Image staminaImage;

    [Header("GameObject References (Inspector)")]
    public GameObject Character;

    private Playstat _playStat;

    private void Start()
    {
        SetupComponents();
    }

    private void SetupComponents()
    {
        // 2. Resolve Playstat reference
        if (_playStat == null)
        {
            if (Character != null) _playStat = Character.GetComponent<Playstat>();
            if (_playStat == null) _playStat = FindAnyObjectByType<Playstat>();
        }
    }

    private void Update()
    {
        if (_playStat == null)
        {
            SetupComponents();
            if (_playStat == null) return;
        }

        // 1. Healthy UI Update
        if (healthyImage != null && _playStat.MaxHealthy > 0f)
        {
            healthyImage.fillAmount = Mathf.Clamp01(_playStat.CurrentHealthy / _playStat.MaxHealthy);
        }

        // 2. Hungry UI Update
        if (hungryImage != null && _playStat.MaxHungry > 0f)
        {
            hungryImage.fillAmount = Mathf.Clamp01(_playStat.CurrentHungry / _playStat.MaxHungry);
        }

        // 3. Stamina UI Update
        if (staminaImage != null && _playStat.MaxStamina > 0f)
        {
            staminaImage.fillAmount = Mathf.Clamp01(_playStat.CurrentStamina / _playStat.MaxStamina);
        }
    }
}



