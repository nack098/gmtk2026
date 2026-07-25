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

    public float RummageStaminaCost => playerData.RummageStaminaCost;

    public bool IsCarryingItem { get; set; }
    public bool IsPushingCart { get; set; }
    public float CartWeight { get; set; }

    private float _baseMoveSpeed = 2.0f;
    private float _baseSprintSpeed = 5.335f;

    [Header("GameData Integration")]
    [SerializeField] private GameData gameData;

    private void Start()
    {
        controller = GetComponent<ThirdPersonController>();
        hungerSystem = GetComponent<HungerSystem>();
        GetComponent<StarterAssetsInputs>();

        // Sync from GameData if available
        SyncFromGameData();

        if (currentHealthy <= 0f) currentHealthy = MaxHealthy;
        if (currentHungry <= 0f) currentHungry = MaxHungry;
        if (currentStamina <= 0f) currentStamina = MaxStamina;

        // Initialize HungerSystem state
        hungerSystem.ChangeState(HungerState.Normal);
    }

    public void SyncFromGameData()
    {
        if (gameData == null && TrashCount.Gameplay.Phases.GamePhaseManager.Instance != null)
        {
            gameData = TrashCount.Gameplay.Phases.GamePhaseManager.Instance.Data;
        }

        if (gameData != null && gameData.PlayerData != null)
        {
            currentHealthy = gameData.PlayerData.Healthy;
            currentHungry = gameData.PlayerData.Hunger;
        }
    }

    public void SyncToGameData()
    {
        if (gameData == null && TrashCount.Gameplay.Phases.GamePhaseManager.Instance != null)
        {
            gameData = TrashCount.Gameplay.Phases.GamePhaseManager.Instance.Data;
        }

        if (gameData != null && gameData.PlayerData != null)
        {
            gameData.PlayerData.Healthy = currentHealthy;
            gameData.PlayerData.Hunger = currentHungry;
        }
    }

    private void Update()
    {
        bool isSprinting = inputs != null && inputs.sprint;
        bool isMoving = inputs != null && inputs.move.sqrMagnitude > 0.01f;

        // 1. Handle Cart Pushing Stamina Drain
        if (IsPushingCart)
        {
            float cartDrainRate = playerData.CartBaseStaminaDrainRate + (CartWeight * playerData.CartWeightStaminaMultiplier);
            if (isSprinting) cartDrainRate *= 2.0f; // Double drain when sprinting with cart

            currentStamina = Mathf.Max(0f, currentStamina - cartDrainRate * Time.deltaTime);
        }
        // 2. Handle Carrying Item Stamina Drain
        else if (IsCarryingItem)
        {
            float carryDrainRate = playerData.CarryStaminaDrainRate;
            if (isSprinting) carryDrainRate *= 2.0f; // Double drain when sprinting with item

            currentStamina = Mathf.Max(0f, currentStamina - carryDrainRate * Time.deltaTime);
        }
        // 3. Handle Sprinting & Stamina Consumption
        else if (isSprinting && currentStamina > 0f)
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

        // 4. Handle Stamina Regeneration when not sprinting, NOT carrying item, and NOT pushing cart
        bool isRegeneratingStamina = !IsCarryingItem && !IsPushingCart && (!isSprinting || currentStamina <= 0f) && currentStamina < MaxStamina;

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

                // Ratio exchange with Hungry / Healthy using configurable ratios from PlayerData
                float hungryRatio = playerData.StaminaToHungryRatio;
                float healthyRatio = playerData.StaminaToHealthyRatio;

                if (currentHungry > 0f)
                {
                    currentHungry = Mathf.Max(0f, currentHungry - (staminaAdded * hungryRatio));
                }
                else
                {
                    // Hungry is 0: deduct from Healthy instead
                    currentHealthy = Mathf.Max(0f, currentHealthy - (staminaAdded * healthyRatio));
                }
            }
        }

        // 5. Update HungerState based on remaining Hungry percentage
        UpdateHungerState();

        // 6. Apply calculated movement speeds to ThirdPersonController
        ApplySpeedToController();

        // 7. Sync current runtime stats back to GameData
        SyncToGameData();
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

        // Apply CarrySpeedMultiplier if currently holding an item
        if (IsCarryingItem && playerData != null)
        {
            stateSpeedMultiplier *= playerData.CarrySpeedMultiplier;
        }

        // Apply CartBaseSpeedMultiplier if currently pushing a cart
        if (IsPushingCart && playerData != null)
        {
            stateSpeedMultiplier *= playerData.CartBaseSpeedMultiplier;
        }

        controller.MoveSpeed = _baseMoveSpeed * stateSpeedMultiplier;
        controller.SprintSpeed = _baseSprintSpeed * stateSpeedMultiplier;
    }

    public void EatFood(float amount)
    {
        currentHungry += amount;

        if (currentHungry > MaxHungry)
        {
            float overflowHungry = currentHungry - MaxHungry;
            currentHungry = MaxHungry;

            // Convert excess Hungry to Stamina based on StaminaToHungryRatio
            float hungryRatio =playerData.StaminaToHungryRatio;
            float staminaGain = overflowHungry * hungryRatio;

            currentStamina = Mathf.Min(MaxStamina, currentStamina + staminaGain);
        }
    }

    public void Heal(float amount)
    {
        currentHealthy = Mathf.Min(MaxHealthy, currentHealthy + amount);
    }

    public bool ConsumeStamina(float amount)
    {
        if (currentStamina < amount) return false;
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        return true;
    }
}
