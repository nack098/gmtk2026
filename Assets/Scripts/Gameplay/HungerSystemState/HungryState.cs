using UnityEngine;
using TrashCount.Data;
using TrashCount.Gameplay.Attributes;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.HungerSystemState
{
    [HungerState(HungerState.Hungry)]
    public class HungryState : HungerStateBase
    {
        public override void Enter()
        {
            Debug.Log("[HungerSystem] Entered Hungry State.");
        }

        public override void Tick(float deltaTime)
        {
        }

        public override void Exit()
        {
        }
    }
}
