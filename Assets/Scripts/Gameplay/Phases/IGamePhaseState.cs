namespace TrashCount.Gameplay.Phases
{
    public interface IGamePhaseState
    {
        string PhaseName { get; }
        void Enter();
        void Update();
        void Exit();
    }
}
