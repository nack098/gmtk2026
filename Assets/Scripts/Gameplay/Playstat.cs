using UnityEngine;
using StarterAssets;
using TrashCount.Data;
using TrashCount.Gameplay;

public class Playstat : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private PlayerData playerData;

    [Header("References")]
    [SerializeField] private ThirdPersonController controller;
    [SerializeField] private HungerSystem hungerSystem;
    [SerializeField] private StarterAssetsInputs inputs;

    [Header("Stamina Settings")]
    [SerializeField] private float staminaDrainRate = 15f; // Stamina drained per second while sprinting
    [SerializeField] private float walkingStaminaRegenMultiplier = 0.5f; // Stamina regen multiplier while walking (0.5 = 50% rate)

    [Header("Runtime Stats (Read-Only)")]
    [SerializeField] private float currentHealthy;
    [SerializeField] private float currentHungry;
    [SerializeField] private float currentStamina;

    public float CurrentHealthy => currentHealthy;
    public float CurrentHungry => currentHungry;
    public float CurrentStamina => currentStamina;

    public float MaxHealthy => playerData.Healthy;
    public float MaxHungry => playerData.Hungry;
    public float MaxStamina => playerData.Stamina;

    private float _baseMoveSpeed = 2.0f;
    private float _baseSprintSpeed = 5.335f;

    private void Start()
    {
        if (controller == null) controller = GetComponent<ThirdPersonController>();
        if (hungerSystem == null) hungerSystem = GetComponent<HungerSystem>();
        if (inputs == null) inputs = GetComponent<StarterAssetsInputs>();

        // Store base speeds from controller if available, otherwise use playerData
        if (controller != null)
        {
            _baseMoveSpeed = controller.MoveSpeed;
            _baseSprintSpeed = controller.SprintSpeed;
        }
        else
        {
            _baseMoveSpeed = playerData.Speed;
            _baseSprintSpeed = playerData.Speed * 2.0f;
        }

        // Initialize runtime stats
        currentHealthy = MaxHealthy;
        currentHungry = MaxHungry;
        currentStamina = MaxStamina;

        // Initialize HungerSystem state
        hungerSystem.ChangeState(HungerState.Normal);
    }

    private void Update()
    {
        bool isSprinting = inputs != null && inputs.sprint;
        bool isMoving = inputs != null && inputs.move.sqrMagnitude > 0.01f;

        // 1. Handle Sprinting & Stamina Consumption
        if (isSprinting && currentStamina > 0f)
        {
            currentStamina = Mathf.Max(0f, currentStamina - staminaDrainRate * Time.deltaTime);

            if (currentStamina <= 0f && inputs != null)
            {
                inputs.sprint = false; // Cancel sprint when stamina runs out
            }
        }
        else if (isSprinting && currentStamina <= 0f)
        {
            if (inputs != null) inputs.sprint = false; // Block sprinting when out of stamina
        }

        // 2. Handle Stamina Regeneration when not sprinting
        bool isRegeneratingStamina = (!isSprinting || currentStamina <= 0f) && currentStamina < MaxStamina;

        if (isRegeneratingStamina)
        {
            float hungerMultiplier = hungerSystem.Data[hungerSystem.CurrentStateKey].DrainValue;

            // While walking (moving without sprinting), apply walkingStaminaRegenMultiplier (e.g. 50% rate)
            float moveStateMultiplier = isMoving ? walkingStaminaRegenMultiplier : 1.0f;

            float baseRegenRate = playerData.StaminaRegen;
            float staminaAdded = baseRegenRate * hungerMultiplier * moveStateMultiplier * Time.deltaTime;

            // Cap to remaining stamina space
            staminaAdded = Mathf.Min(staminaAdded, MaxStamina - currentStamina);

            if (staminaAdded > 0f)
            {
                currentStamina += staminaAdded;

                // 1:1 ratio exchange with Hungry / Healthy
                if (currentHungry > 0f)
                {
                    currentHungry = Mathf.Max(0f, currentHungry - staminaAdded);
                }
                else
                {
                    // Hungry is 0: deduct from Healthy instead
                    currentHealthy = Mathf.Max(0f, currentHealthy - staminaAdded);
                }
            }
        }

        // 3. Update HungerState based on remaining Hungry percentage
        UpdateHungerState();

        // 4. Apply calculated movement speeds to ThirdPersonController
        ApplySpeedToController();
    }

    private void UpdateHungerState()
    {
        if (hungerSystem == null) return;

        float hungryPercent = (currentHungry / MaxHungry) * 100f;

        if (hungryPercent >= 50f)
        {
            hungerSystem.ChangeState(HungerState.Normal);
        }
        else if (hungryPercent > 0f)
        {
            hungerSystem.ChangeState(HungerState.Hungry);
        }
        else
        {
            hungerSystem.ChangeState(HungerState.Starving);
        }
    }

    private void ApplySpeedToController()
    {
        if (controller == null) return;

        float stateSpeedMultiplier = 1.0f;
        if (hungerSystem != null)
        {
            switch (hungerSystem.CurrentStateKey)
            {
                case HungerState.Normal:
                    stateSpeedMultiplier = hungerSystem.Data[HungerState.Normal].MoveSpeedMultiplier;
                    break;
                case HungerState.Hungry:
                    stateSpeedMultiplier = hungerSystem.Data[HungerState.Hungry].MoveSpeedMultiplier;
                    break;
                case HungerState.Starving:
                    stateSpeedMultiplier = hungerSystem.Data[HungerState.Starving].MoveSpeedMultiplier;
                    break;
                default:
                    stateSpeedMultiplier = 1.0f;
                    break;
            }
        }

        controller.MoveSpeed = _baseMoveSpeed * stateSpeedMultiplier;
        controller.SprintSpeed = _baseSprintSpeed * stateSpeedMultiplier;
    }

    public void EatFood(float amount)
    {
        currentHungry = Mathf.Min(MaxHungry, currentHungry + amount);
    }

    public void Heal(float amount)
    {
        currentHealthy = Mathf.Min(MaxHealthy, currentHealthy + amount);
    }
}
