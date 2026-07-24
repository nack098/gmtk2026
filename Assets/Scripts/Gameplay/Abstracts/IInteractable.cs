using UnityEngine;

namespace TrashCount.Gameplay.Abstracts
{
    public interface IInteractable
    {
        string GetInteractPrompt();
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
