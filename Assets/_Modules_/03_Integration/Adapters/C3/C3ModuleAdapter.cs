using C3TreasurePuzzle;
using SDGAdventure.ModuleSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.C3
{
    public sealed class C3ModuleAdapter : MonoBehaviour, IModuleAdapter
    {
        public const string Id = "c3_treasure_puzzle";
        public const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        public string ModuleId => Id;
        private ModuleCoordinator coordinator;
        private C3DeviceState device;
        private C3TreasureProgress progress;
        private C3PuzzleState puzzle;
        private bool reported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            if (!ModuleRegistry.TryRegister(new ModuleDefinition(Id, "C3 Treasure Puzzle", ScenePath,
                    typeof(C3ModuleAdapter)), out string error))
                Debug.LogError("C3 registration failed: " + error);
        }

        public bool Initialize(ModuleCoordinator owner)
        {
            coordinator = owner;
            Scene scene = gameObject.scene;
            int deviceCount = 0;
            int playerCount = 0;
            int cameraCount = 0;
            int listenerCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (C3DeviceState item in root.GetComponentsInChildren<C3DeviceState>(true))
                { device = item; deviceCount++; }
                foreach (C3TreasureProgress item in root.GetComponentsInChildren<C3TreasureProgress>(true)) progress = item;
                foreach (C3PuzzleState item in root.GetComponentsInChildren<C3PuzzleState>(true)) puzzle = item;
                playerCount += root.GetComponentsInChildren<C3FirstPersonController>(true).Length;
                cameraCount += root.GetComponentsInChildren<Camera>(true).Length;
                listenerCount += root.GetComponentsInChildren<AudioListener>(true).Length;
            }
            if (deviceCount != 1 || progress == null || puzzle == null || playerCount != 1 ||
                cameraCount != 1 || listenerCount != 1)
            {
                Debug.LogError($"C3 scene ownership invalid: devices={deviceCount}, players={playerCount}, cameras={cameraCount}, listeners={listenerCount}");
                return false;
            }
            // C_Main uses skybox ambient light and has no Lighting Data asset. A fresh
            // project import can start with a zero ambient probe, leaving unlit factory
            // walls black until Unity refreshes the environment. Refresh once per load.
            DynamicGI.UpdateEnvironment();
            device.Completed += OnCompleted;
            if (device.LevelCompleted) OnCompleted();
            return true;
        }

        private void OnCompleted()
        {
            if (reported || coordinator == null || device == null || progress == null || puzzle == null ||
                !device.LevelCompleted || !device.IsActivated || !progress.AllPartsCollected || !puzzle.PuzzleSolved)
                return;
            ModuleResult result = new ModuleResult
            {
                ModuleId = Id,
                Completed = true,
                CollectedParts = progress.CollectedCount,
                PuzzleAttempts = puzzle.SubmissionCount,
                PlaySeconds = device.CompletionSeconds
            };
            reported = coordinator.ReportCompletion(this, result);
        }

        public void Shutdown()
        {
            if (device != null) device.Completed -= OnCompleted;
            device = null;
            progress = null;
            puzzle = null;
            coordinator = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
