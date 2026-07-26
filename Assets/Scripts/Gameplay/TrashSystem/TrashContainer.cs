using System;
using System.Collections;
using UnityEngine;
using TrashCount.Data;
using TrashCount.Data.Models;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.TrashSystem
{
    public class TrashContainer : MonoBehaviour, IInteractable
    {
        [Header("Drop & Item Settings")]
        [SerializeField] private RandomDropSystem dropSystem;
        [SerializeField] private ItemData itemData;
        [SerializeField] private GameObject defaultItemPrefab;
        [SerializeField] private Transform spawnPoint;

        [Header("Container Capacity")]
        [SerializeField] private int maxSearches = 3;
        [SerializeField] private float respawnCooldown = 60f; // Seconds until container refills
        [SerializeField] private float staminaCost ; // Stamina cost per rummage

        [Header("Minigame Settings (Future Expansion)")]
        [SerializeField] private bool enableMinigame = false;

        public int RemainingSearches { get; private set; }
        public bool IsEmpty => RemainingSearches <= 0;

        // Events for Future Expansion & UI
        public event Action<TrashContainer> OnMinigameRequested;
        public event Action<TrashContainer> OnRummageStarted;
        public event Action<TrashContainer, ItemState, ItemModel> OnItemRummaged;
        public event Action<TrashContainer> OnRummageEmpty;
        public event Action<TrashContainer> OnContainerEmptied;

        private bool _isCoolingDown = false;

        private void Awake()
        {
            RemainingSearches = maxSearches;
        }

        public string GetInteractPrompt()
        {
            if (_isCoolingDown || RemainingSearches <= 0)
            {
                return "ถังขยะว่างเปล่า (ไม่มีขยะให้คุ้ย)";
            }
            return $"กด E เพื่อคุ้ยถังขยะ (ค้นได้อีก {RemainingSearches} ครั้ง)";
        }

        public bool CanInteract(GameObject interactor)
        {
            return !_isCoolingDown && RemainingSearches > 0;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            if (enableMinigame)
            {
                // Hook for future Minigame UI trigger
                Debug.Log($"[TrashContainer] Minigame requested for {gameObject.name}");
                OnMinigameRequested?.Invoke(this);
            }
            else
            {
                // Direct Rummage
                PerformRummage(interactor, 1.0f);
            }
        }

        /// <summary>
        /// Rummages the trash container and spawns a random item.
        /// Can be called directly or by a Minigame upon completion.
        /// </summary>
        /// <param name="interactor">The player object</param>
        /// <param name="successQualityMultiplier">Optional quality multiplier from minigame</param>
        public void PerformRummage(GameObject interactor, float successQualityMultiplier = 1.0f)
        {
            if (RemainingSearches <= 0) return;

            // Consume Stamina from Player (using RummageStaminaCost from PlayerData)
            if (interactor != null && interactor.TryGetComponent<Playstat>(out var player))
            {
                float cost = player.RummageStaminaCost;
                if (!player.ConsumeStamina(cost))
                {
                    Debug.Log($"[TrashContainer] Not enough stamina to rummage {gameObject.name}. Required: {cost}");
                    return;
                }
            }

            RemainingSearches--;
            OnRummageStarted?.Invoke(this);

            bool hasDrop = false;
            ItemState droppedState = ItemState.None;
            ItemModel droppedItem = null;

            if (dropSystem != null)
            {
                hasDrop = dropSystem.TryGetRandomDrop(out droppedState, out droppedItem);
            }

            if (hasDrop && droppedItem != null)
            {
                SpawnDroppedItem(droppedState, droppedItem);
                OnItemRummaged?.Invoke(this, droppedState, droppedItem);
                Debug.Log($"[TrashContainer] Found item: {droppedState} from {gameObject.name}");
            }
            else
            {
                OnRummageEmpty?.Invoke(this);
                Debug.Log($"[TrashContainer] Searched {gameObject.name} but found nothing.");
            }

            if (RemainingSearches <= 0)
            {
                OnContainerEmptied?.Invoke(this);
                StartCoroutine(RespawnRoutine());
            }
        }

        private void SpawnDroppedItem(ItemState state, ItemModel model)
        {
            Vector3 spawnPos = spawnPoint.position + new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f),
                0.5f,
                UnityEngine.Random.Range(-0.5f, 0.5f)
            );

            GameObject prefabToSpawn = defaultItemPrefab;

            // Check if model has PickableCapability with custom 3D prefab
            if (model != null && model.TryGetCapability<PickableCapability>(out var pickable) && pickable.WorldPrefab != null)
            {
                prefabToSpawn = pickable.WorldPrefab;
            }

            if (prefabToSpawn != null)
            {
                GameObject spawnedObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

                // Attach or initialize WorldItem component if needed
                if (!spawnedObj.TryGetComponent<WorldItem>(out var worldItem))
                {
                    worldItem = spawnedObj.AddComponent<WorldItem>();
                }
                worldItem.Initialize(state, itemData);
            }
            else
            {
                Debug.LogWarning($"[TrashContainer] No prefab available to spawn item: {state}", this);
            }
        }

        private IEnumerator RespawnRoutine()
        {
            _isCoolingDown = true;
            yield return new WaitForSeconds(respawnCooldown);
            RemainingSearches = maxSearches;
            _isCoolingDown = false;
            Debug.Log($"[TrashContainer] {gameObject.name} refilled with trash.");
        }
    }
}
