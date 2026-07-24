using UnityEngine;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.TrashSystem
{
    public class PushCartHandle : MonoBehaviour, IInteractable
    {
        [SerializeField] private PushCart pushCart;

        private void Awake()
        {
            if (pushCart == null) pushCart = GetComponentInParent<PushCart>();
        }

        public string GetInteractPrompt()
        {
            if (pushCart != null && pushCart.IsBeingPushed)
            {
                return "กด E เพื่อปล่อยมือจากรถเข็น";
            }
            return "กด E เพื่อเข็น/ดันรถขยะ";
        }

        public bool CanInteract(GameObject interactor)
        {
            if (pushCart == null) return false;

            if (interactor.TryGetComponent<PlayerInteraction>(out var player))
            {
                // Can only interact if not holding an item
                return !player.IsCarrying;
            }
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (pushCart == null) return;

            if (pushCart.IsBeingPushed)
            {
                pushCart.StopPushing();
            }
            else
            {
                if (interactor.TryGetComponent<PlayerInteraction>(out var player) &&
                    interactor.TryGetComponent<Playstat>(out var stat))
                {
                    pushCart.StartPushing(player, stat);
                }
            }
        }
    }
}
