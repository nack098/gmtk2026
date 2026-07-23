using UnityEngine;
using TrashCount.Gameplay;
using TrashCount.Data.Models;

namespace TrashCount.Gameplay.Abstracts
{
    public abstract class HungerStateBase
    {
        protected HungerSystem Context { get; private set; }
        
        public void Initialize(HungerSystem context)
        {
            Context = context;
        }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
    }
}