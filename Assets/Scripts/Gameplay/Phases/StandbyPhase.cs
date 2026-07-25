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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[StandbyPhase] Entered Standby Phase. Unlocked cursor for Summary UI interaction.");
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

            // 1. Process Overnight Hunger Decay (-15 for Player, -20 for Father)
            if (_manager.Data.PlayerData != null)
            {
                _manager.Data.PlayerData.Hunger = Mathf.Max(0f, _manager.Data.PlayerData.Hunger - 15f);
                CalculateCharacterSickness("Player", _manager.Data.PlayerData);
            }

            if (_manager.Data.FatherData != null)
            {
                _manager.Data.FatherData.Hunger = Mathf.Max(0f, _manager.Data.FatherData.Hunger - 20f);
                CalculateCharacterSickness("Father", _manager.Data.FatherData);
            }

            // 2. Advance Day counter (Day + 1) and reset Cart Items for new day
            if (_manager.Data.DateTime != null)
            {
                _manager.Data.DateTime.Day += 1;
                Debug.Log($"[StandbyPhase] Advanced to Day {_manager.Data.DateTime.Day}!");
            }

            if (_manager.Data.CartItemsData != null)
            {
                _manager.Data.CartItemsData.Clear();
                Debug.Log("[StandbyPhase] CartItemsData reset for new day!");
            }
        }

        public void ConfirmNextDay()
        {
            // Check if Game Over occurred after sickness deduction
            if (_manager.CheckGameOverCondition())
            {
                _manager.ChangePhase(new GameOverPhase(_manager));
            }
            else
            {
                // Transition back to ZenGameScene for the new day
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "ZenGameScene")
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("ZenGameScene");
                }
                else
                {
                    _manager.StartGameplayPhase();
                }
            }
        }

        private void CalculateCharacterSickness(string characterName, Data.Models.CharacterModel character)
        {
            // If Hunger is low (< 40%), calculate sickness chance
            float hungerPercent = Mathf.Clamp(character.Hunger, 0f, 100f);
            if (hungerPercent < 40f)
            {
                float sicknessChance = (40f - hungerPercent) / 40f; // 0.0 to 1.0
                if (Takayama.Math.Random.Value() < sicknessChance)
                {
                    character.IsSick = true;
                    Debug.Log($"[StandbyPhase] {characterName} fell sick!");
                }
            }

            // If character is sick, deduct Healthy (-15 per night)
            if (character.IsSick)
            {
                character.Healthy = Mathf.Max(0f, character.Healthy - 15f);
                Debug.Log($"[StandbyPhase] {characterName} is sick! Healthy reduced to {character.Healthy}");
            }
            else
            {
                Debug.Log($"[StandbyPhase] {characterName} is healthy.");
            }
        }
    }
}
