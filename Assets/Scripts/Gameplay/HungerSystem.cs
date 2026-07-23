using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrashCount.Data;
using TrashCount.Gameplay.Abstracts;
using TrashCount.Gameplay.Attributes;

namespace TrashCount.Gameplay
{    
    public class HungerSystem : MonoBehaviour
    {
        [SerializeField] private HungerData hungerData;
        
        public HungerData Data => hungerData;
        public HungerState CurrentStateKey { get; private set; } = HungerState.None;

        private Dictionary<HungerState, HungerStateBase> _hungerStateMapping;
        private HungerStateBase _currentState;

        void Awake()
        {
            _hungerStateMapping = new Dictionary<HungerState, HungerStateBase>();
            var assembly = typeof(HungerSystem).Assembly;
            var stateTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(HungerStateBase).IsAssignableFrom(t));

            foreach (var type in stateTypes)
            {
                var attr = type.GetCustomAttribute<HungerStateAttribute>(inherit: false);
                if (attr == null) continue;

                if (Activator.CreateInstance(type) is HungerStateBase stateInstance)
                {
                    stateInstance.Initialize(this);
                    _hungerStateMapping[attr.StateKey] = stateInstance;
                }
            }
        }

        void Update()
        {
            _currentState?.Tick(Time.deltaTime);
        }
        
        public void ChangeState(HungerState nextState)
        {
            if (!_hungerStateMapping.TryGetValue(nextState, out var nextHungerState))
            {
                throw new ArgumentException($"[HungerSystem] Unregistered state: {nextState}");
            }
            
            _currentState?.Exit();
            
            CurrentStateKey = nextState;
            _currentState = nextHungerState;
            _currentState.Enter();
        }
    }
}