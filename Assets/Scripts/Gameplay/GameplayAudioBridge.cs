using UnityEngine;
using TrashCount.Data;
using TrashCount.Data.Models;
using TrashCount.Gameplay.Phases;
using TrashCount.Gameplay.TrashSystem;

namespace TrashCount.Gameplay
{
    public class GameplayAudioBridge : MonoBehaviour
    {
        [Header("SFX Configuration")]
        [SerializeField] private GameplaySFXConfig sfxConfig;

        private PlayerInteraction _playerInteraction;
        private PushCart _pushCart;
        private ShopSystem _shopSystem;
        private HungerSystem _hungerSystem;
        private Playstat _playstat;

        private void OnEnable()
        {
            SubscribeToManagers();
            SubscribeToSceneComponents();
        }

        private void OnDisable()
        {
            UnsubscribeFromManagers();
            UnsubscribeFromSceneComponents();
        }

        private void Start()
        {
            // Re-bind in Start to ensure components instantiated at runtime are captured
            SubscribeToSceneComponents();
        }

        private void SubscribeToManagers()
        {
            if (GamePhaseManager.Instance != null)
            {
                GamePhaseManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                GamePhaseManager.Instance.OnGameOver += HandleGameOver;
            }
        }

        private void UnsubscribeFromManagers()
        {
            if (GamePhaseManager.Instance != null)
            {
                GamePhaseManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                GamePhaseManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        public void SubscribeToSceneComponents()
        {
            // Unsubscribe first to prevent double subscriptions
            UnsubscribeFromSceneComponents();

            // 1. Player Interaction
            _playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            if (_playerInteraction != null)
            {
                _playerInteraction.OnItemPickedUp += HandleItemPickedUp;
                _playerInteraction.OnItemDropped += HandleItemDropped;
                _playerInteraction.OnItemConsumed += HandleItemConsumed;
            }

            // 2. Push Cart
            _pushCart = PushCart.Instance != null ? PushCart.Instance : FindAnyObjectByType<PushCart>();
            if (_pushCart != null)
            {
                _pushCart.OnPushStarted += HandleCartPushStarted;
                _pushCart.OnPushStopped += HandleCartPushStopped;
                _pushCart.OnItemAddedToCart += HandleItemAddedToCart;
            }

            // 3. Shop System
            _shopSystem = FindAnyObjectByType<ShopSystem>();
            if (_shopSystem != null)
            {
                _shopSystem.OnItemPurchased += HandleItemPurchased;
                _shopSystem.OnItemSold += HandleItemSold;
                _shopSystem.OnTransactionFailed += HandleTransactionFailed;
            }

            // 4. Hunger System
            _hungerSystem = FindAnyObjectByType<HungerSystem>();
            if (_hungerSystem != null)
            {
                _hungerSystem.OnHungerStateChanged += HandleHungerStateChanged;
            }

            // 5. Playstat
            _playstat = FindAnyObjectByType<Playstat>();
            if (_playstat != null)
            {
                _playstat.OnStaminaExhausted += HandleStaminaExhausted;
            }

            // 6. Trash Containers in scene
            var trashContainers = FindObjectsByType<TrashContainer>(FindObjectsSortMode.None);
            foreach (var container in trashContainers)
            {
                if (container == null) continue;
                container.OnRummageStarted += HandleTrashRummageStarted;
                container.OnItemRummaged += HandleTrashItemRummaged;
                container.OnRummageEmpty += HandleTrashRummageEmpty;
            }
        }

        private void UnsubscribeFromSceneComponents()
        {
            if (_playerInteraction != null)
            {
                _playerInteraction.OnItemPickedUp -= HandleItemPickedUp;
                _playerInteraction.OnItemDropped -= HandleItemDropped;
                _playerInteraction.OnItemConsumed -= HandleItemConsumed;
            }

            if (_pushCart != null)
            {
                _pushCart.OnPushStarted -= HandleCartPushStarted;
                _pushCart.OnPushStopped -= HandleCartPushStopped;
                _pushCart.OnItemAddedToCart -= HandleItemAddedToCart;
            }

            if (_shopSystem != null)
            {
                _shopSystem.OnItemPurchased -= HandleItemPurchased;
                _shopSystem.OnItemSold -= HandleItemSold;
                _shopSystem.OnTransactionFailed -= HandleTransactionFailed;
            }

            if (_hungerSystem != null)
            {
                _hungerSystem.OnHungerStateChanged -= HandleHungerStateChanged;
            }

            if (_playstat != null)
            {
                _playstat.OnStaminaExhausted -= HandleStaminaExhausted;
            }

            var trashContainers = FindObjectsByType<TrashContainer>(FindObjectsSortMode.None);
            foreach (var container in trashContainers)
            {
                if (container == null) continue;
                container.OnRummageStarted -= HandleTrashRummageStarted;
                container.OnItemRummaged -= HandleTrashItemRummaged;
                container.OnRummageEmpty -= HandleTrashRummageEmpty;
            }
        }

        // ── Handlers ─────────────────────────────────────────────────────────────

        private void HandleItemPickedUp(WorldItem item)
        {
            if (sfxConfig == null || item == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.itemPickUp, item.transform.position);
        }

        private void HandleItemDropped(WorldItem item)
        {
            if (sfxConfig == null || item == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.itemDrop, item.transform.position);
        }

        private void HandleItemConsumed(WorldItem item)
        {
            if (sfxConfig == null) return;
            Vector3 pos = item != null ? item.transform.position : transform.position;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.itemConsume, pos);
        }

        private void HandleCartPushStarted(PushCart cart)
        {
            if (sfxConfig == null || cart == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.cartGrab, cart.transform.position);
        }

        private void HandleCartPushStopped(PushCart cart)
        {
            if (sfxConfig == null || cart == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.cartRelease, cart.transform.position);
        }

        private void HandleItemAddedToCart(WorldItem item)
        {
            if (sfxConfig == null || item == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.itemAddedToCart, item.transform.position);
        }

        private void HandleTrashRummageStarted(TrashContainer container)
        {
            if (sfxConfig == null || container == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.trashRummage, container.transform.position);
        }

        private void HandleTrashItemRummaged(TrashContainer container, ItemState state, ItemModel model)
        {
            if (sfxConfig == null || container == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.trashFoundItem, container.transform.position);
        }

        private void HandleTrashRummageEmpty(TrashContainer container)
        {
            if (sfxConfig == null || container == null) return;
            AudioManager.Instance.PlayRandomSfx(sfxConfig.trashEmpty, container.transform.position);
        }

        private void HandleItemPurchased(ItemState state, int price)
        {
            if (sfxConfig == null) return;
            AudioManager.Instance.PlayRandomSfx2D(sfxConfig.shopBuy);
        }

        private void HandleItemSold(ItemState state, int quantity, long totalRevenue)
        {
            if (sfxConfig == null) return;
            AudioManager.Instance.PlayRandomSfx2D(sfxConfig.shopSell);
        }

        private void HandleTransactionFailed()
        {
            if (sfxConfig == null) return;
            AudioManager.Instance.PlayRandomSfx2D(sfxConfig.shopTransactionFailed);
        }

        private void HandleHungerStateChanged(HungerState previousState, HungerState newState)
        {
            if (sfxConfig == null) return;
            if (newState == HungerState.Hungry || newState == HungerState.Starving)
            {
                AudioManager.Instance.PlayRandomSfx2D(sfxConfig.stomachGrowl);
            }
        }

        private void HandleStaminaExhausted()
        {
            if (sfxConfig == null) return;
            AudioManager.Instance.PlayRandomSfx2D(sfxConfig.staminaExhausted);
        }

        private void HandlePhaseChanged(IGamePhaseState phase)
        {
            if (sfxConfig == null || phase == null) return;
            if (phase is ShoppingPhase)
            {
                AudioManager.Instance.PlayRandomSfx2D(sfxConfig.dayEndBell);
            }
        }

        private void HandleGameOver()
        {
            if (sfxConfig == null) return;
            AudioManager.Instance.PlayRandomSfx2D(sfxConfig.gameOver);
        }
    }
}
