using UnityEngine;
using Takayama.Math;

namespace TrashCount.Gameplay.Phases
{
    public class StandbyPhase : IGamePhaseState
    {
        public string PhaseName => "Standby Phase";

        private readonly GamePhaseManager _manager;

        public StandbyPhase(GamePhaseManager manager)
        {
            _manager = manager;
        }

        public void Enter()
        {
            Debug.Log("[StandbyPhase] Entered Standby Phase. Processing sickness calculation and advancing day.");
            ProcessSicknessAndNewDay();
        }

        public void Update()
        {
            // Standby phase processes logic instantly and loops back to GameplayPhase
        }

        public void Exit()
        {
            Debug.Log("[StandbyPhase] Exited Standby Phase.");
        }

        private void ProcessSicknessAndNewDay()
        {
            if (_manager.Data == null) return;

            // 1. Process Sickness for PlayerData
            if (_manager.Data.PlayerData != null)
            {
                CalculateCharacterSickness("Player", _manager.Data.PlayerData);
            }

            // 2. Process Sickness for FatherData
            if (_manager.Data.FatherData != null)
            {
                CalculateCharacterSickness("Father", _manager.Data.FatherData);
            }

            // 3. Advance Day counter (Day + 1)
            if (_manager.Data.DateTime != null)
            {
                _manager.Data.DateTime.Day += 1;
                Debug.Log($"[StandbyPhase] Advanced to Day {_manager.Data.DateTime.Day}!");
            }

            // 4. Check if Game Over occurred after sickness deduction
            if (_manager.CheckGameOverCondition())
            {
                _manager.ChangePhase(new GameOverPhase(_manager));
            }
            else
            {
                // Loop back to GameplayPhase for next day using configured duration!
                _manager.StartGameplayPhase();
            }
        }

        private void CalculateCharacterSickness(string characterName, Data.Models.CharacterModel character)
        {
            // Lower Healthy increases sickness chance (e.g. 100 Healthy = 0% chance, 50 Healthy = 50% chance)
            float healthPercent = Mathf.Clamp(character.Hunger, 0f, 100f);
            float sicknessChance = (100f - healthPercent) / 100f; // 0.0 to 1.0

            // Using Takayama.Math.Random.Value()
            if (Takayama.Math.Random.Value() < sicknessChance)
            {
                character.IsSick = true;
                character.Hunger = Mathf.Max(0f, character.Hunger - 15f); // Extra penalty when sick
                Debug.Log($"[StandbyPhase] {characterName} fell sick! Remaining Hunger: {character.Hunger}");
            }
            else
            {
                character.IsSick = false;
                Debug.Log($"[StandbyPhase] {characterName} stayed healthy.");
            }
        }
    }
}
