using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.UI;

namespace SDGAdventure.ModuleSystem
{
    public sealed class ModuleCoordinator : MonoBehaviour
    {
        public const string SelectionScenePath = "Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity";
        public static ModuleCoordinator Instance { get; private set; }
        public string CurrentModuleId { get; private set; }
        public ModuleResult? LastResult { get; private set; }
        public event Action<ModuleResult> Completed;

        private IModuleAdapter adapter;
        private bool completionReceived;
        private string errorText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject host = new GameObject("ModuleCoordinator");
            DontDestroyOnLoad(host);
            host.AddComponent<ModuleCoordinator>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ShutdownAdapter();
            Instance = null;
        }

        public bool TryLaunch(string moduleId, out string error)
        {
            if (!ModuleRegistry.TryGet(moduleId, out ModuleDefinition module))
            { error = "Unknown module ID: " + moduleId; return false; }
            if (!module.IsValid(out error)) return false;
            if (!Application.CanStreamedLevelBeLoaded(module.ScenePath))
            { error = "Scene is not enabled in Build Settings: " + module.ScenePath; return false; }
            PrepareTransition();
            LastResult = null;
            SceneManager.LoadScene(module.ScenePath, LoadSceneMode.Single);
            error = null;
            return true;
        }

        public bool RestartCurrent(out string error)
        {
            if (!ModuleRegistry.TryGet(CurrentModuleId, out ModuleDefinition module))
            { error = "No active registered module."; return false; }
            return TryLaunch(module.Id, out error);
        }

        public bool ReturnToSelection(out string error)
        {
            if (!Application.CanStreamedLevelBeLoaded(SelectionScenePath))
            { error = "Selection scene is not enabled in Build Settings."; return false; }
            PrepareTransition();
            SceneManager.LoadScene(SelectionScenePath, LoadSceneMode.Single);
            error = null;
            return true;
        }

        public bool ReportCompletion(IModuleAdapter sender, ModuleResult result)
        {
            if (sender == null || sender != adapter || completionReceived ||
                CurrentModuleId != sender.ModuleId || result.ModuleId != CurrentModuleId || !result.Completed)
                return false;
            completionReceived = true;
            LastResult = result;
            Completed?.Invoke(result);
            return true;
        }

        private void PrepareTransition()
        {
            ShutdownAdapter();
            CurrentModuleId = null;
            completionReceived = false;
            errorText = null;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ShutdownAdapter()
        {
            if (adapter == null) return;
            adapter.Shutdown();
            adapter = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != this || mode != LoadSceneMode.Single) return;
            if (!ModuleRegistry.TryGetByScene(scene.path, out ModuleDefinition module))
            {
                ShutdownAdapter();
                CurrentModuleId = null;
                completionReceived = false;
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            // Start can observe a scene already bound by sceneLoaded. A destroyed
            // adapter from a previous load of the same scene must not count.
            if (adapter is MonoBehaviour currentAdapter && currentAdapter != null &&
                currentAdapter.gameObject.scene == scene && CurrentModuleId == module.Id) return;
            ShutdownAdapter();
            CurrentModuleId = module.Id;
            completionReceived = false;
            GameObject host = new GameObject(module.Id + " Adapter");
            SceneManager.MoveGameObjectToScene(host, scene);
            adapter = (IModuleAdapter)host.AddComponent(module.AdapterType);
            if (!adapter.Initialize(this))
            {
                errorText = "Module adapter failed to initialize: " + module.Id;
                Debug.LogError(errorText);
                adapter.Shutdown();
                adapter = null;
                CurrentModuleId = null;
                Destroy(host);
            }
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().path != SelectionScenePath) return;
            CjkGuiFont.Apply();
            GUI.depth = -100;
            float width = Mathf.Min(600, Screen.width - 32);
            Rect panel = new Rect((Screen.width - width) / 2, 40, width, 290);
            GUI.Box(panel, "SDG Adventure｜M6 開發驗證選關入口");
            GUI.Label(new Rect(panel.x + 20, panel.y + 42, width - 40, 44), "僅供整合驗證；不是正式遊戲 UI。");
            if (GUI.Button(new Rect(panel.x + 20, panel.y + 94, width - 40, 50), "進入 C3_TreasurePuzzle"))
            {
                if (!TryLaunch("c3_treasure_puzzle", out string error)) errorText = error;
            }
            if (LastResult.HasValue)
                GUI.Label(new Rect(panel.x + 20, panel.y + 160, width - 40, 44),
                    "最近結果：" + LastResult.Value.ModuleId + (LastResult.Value.Completed ? " 通關" : " 未完成"));
            if (!string.IsNullOrEmpty(errorText))
                GUI.Label(new Rect(panel.x + 20, panel.y + 215, width - 40, 50), errorText);
        }
    }
}
