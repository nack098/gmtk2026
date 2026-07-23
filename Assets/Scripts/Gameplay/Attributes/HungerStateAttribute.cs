using System;
using UnityEngine;
using TrashCount.Data;

namespace TrashCount.Gameplay.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class HungerStateAttribute : Attribute
    {
        public HungerState StateKey { get; }

        public HungerStateAttribute(HungerState stateKey)
        {
            StateKey = stateKey;
        }
    }
}
