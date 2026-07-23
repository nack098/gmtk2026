using UnityEngine;
using TrashCount.Data;
using TrashCount.Gameplay.Attributes;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.HungerSystemState
{
    [HungerState(HungerState.Test)]
    public class TestState : HungerStateBase
    {
        public override void Enter()
        {
            
        }
        
        public override void Tick(float deltaTime)
        {
            Context.ChangeState(HungerState.Test);
        }
        
        public override void Exit()
        {
            
        }
    }
}