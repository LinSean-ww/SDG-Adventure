using System;
using UnityEngine;

namespace C3TreasurePuzzle
{
    public enum C3DevicePhase { Unrepaired, AwaitingPuzzle, ReadyToActivate, Activated }

    // The sole scene-level authority for activation and independent level completion.
    public sealed class C3DeviceState : MonoBehaviour
    {
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        private float startedAt;
        public bool IsActivated { get; private set; }
        public bool LevelCompleted { get; private set; }
        public float CompletionSeconds { get; private set; }
        public float ElapsedSeconds => LevelCompleted ? CompletionSeconds : Mathf.Max(0f, Time.unscaledTime - startedAt);
        public event Action Activated;
        public event Action Completed;

        public C3DevicePhase Phase => IsActivated ? C3DevicePhase.Activated :
            progress == null || !progress.AllPartsCollected ? C3DevicePhase.Unrepaired :
            puzzle == null || !puzzle.PuzzleSolved ? C3DevicePhase.AwaitingPuzzle : C3DevicePhase.ReadyToActivate;

        public void Configure(C3TreasureProgress owner, C3PuzzleState puzzleState)
        {
            progress = owner;
            puzzle = puzzleState;
        }

        private void Awake()
        {
            if (progress == null) progress = GetComponent<C3TreasureProgress>();
            if (puzzle == null) puzzle = GetComponent<C3PuzzleState>();
            startedAt = Time.unscaledTime;
        }

        public bool TryActivate()
        {
            if (Phase != C3DevicePhase.ReadyToActivate || IsActivated || LevelCompleted) return false;
            IsActivated = true;
            Activated?.Invoke();
            // Recheck all three conditions at the final transition.
            if (progress != null && progress.AllPartsCollected && puzzle != null && puzzle.PuzzleSolved && IsActivated)
            {
                LevelCompleted = true;
                CompletionSeconds = Mathf.Max(0f, Time.unscaledTime - startedAt);
                Completed?.Invoke();
            }
            return true;
        }
    }
}
