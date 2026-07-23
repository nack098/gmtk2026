using UnityEngine;
using TrashCount.Data;
using TrashCount.Gameplay.Attributes;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.HungerSystemState
{
    [HungerState(HungerState.Normal)]
    public class NormalState : HungerStateBase
    {
        public override void Enter()
        {
            Debug.Log("[HungerSystem] Entered NormalState.");
        }

        public override void Tick(float deltaTime)
        {
            
        }

        public override void Exit()
        {
        }
    }
}
