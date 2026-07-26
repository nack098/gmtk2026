using UnityEngine;

namespace TrashCount.Gameplay
{
    [CreateAssetMenu(fileName = "GameplaySFXConfig", menuName = "TrashCount/Audio/Gameplay SFX Config")]
    public class GameplaySFXConfig : ScriptableObject
    {
        [Header("Player Interaction SFX")]
        public AudioClip[] itemPickUp;
        public AudioClip[] itemDrop;
        public AudioClip[] itemConsume;

        [Header("Push Cart SFX")]
        public AudioClip[] cartGrab;
        public AudioClip[] cartRelease;
        public AudioClip[] itemAddedToCart;

        [Header("Trash Container SFX")]
        public AudioClip[] trashRummage;
        public AudioClip[] trashFoundItem;
        public AudioClip[] trashEmpty;

        [Header("Shop SFX")]
        public AudioClip[] shopBuy;
        public AudioClip[] shopSell;
        public AudioClip[] shopTransactionFailed;

        [Header("Player Stats & State SFX")]
        public AudioClip[] stomachGrowl;
        public AudioClip[] staminaExhausted;

        [Header("Phase & Game Event SFX")]
        public AudioClip[] dayEndBell;
        public AudioClip[] gameOver;
        public AudioClip[] uiClick;
    }
}
