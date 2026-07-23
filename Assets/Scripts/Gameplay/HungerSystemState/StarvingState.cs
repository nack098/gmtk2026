using UnityEngine;
using TrashCount.Data;
using TrashCount.Gameplay.Attributes;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.HungerSystemState
{
    [HungerState(HungerState.Starving)]
    public class StarvingState : HungerStateBase
    {
        public override void Enter()
        {
            Debug.Log("[HungerSystem] Entered Starving State.");
        }

        public override void Tick(float deltaTime)
        {
        }

        public override void Exit()
        {
        }
    }
}
