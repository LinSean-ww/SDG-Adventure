using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.ModuleSystem;
using SDGAdventure.UI;

namespace C3TreasurePuzzle
{
    public sealed class C3CompletionScreen : MonoBehaviour
    {
        public const string Knowledge = "SDG 12 責任消費與生產：正確分類有助於後續資源回收。可再利用的材料應優先回收或再利用；剩餘污染物需要適當處理。減少浪費、改善資源利用效率，有助於更負責任的生產與消費。本關流程是簡化教育謎題，並非所有工業廢棄物的通用處理順序。";
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        [SerializeField] private C3DeviceState device;
        private Vector2 scroll;
        private bool visible;
        private bool restarting;
        private static C3CompletionScreen active;
        public bool Visible => visible;
        public static bool BlocksGameplayInputThisFrame => active != null && active.visible;
        public string PartsText => progress == null ? "0/3" : progress.CollectedCount + "/" + C3TreasureProgress.RequiredCount;
        public int PuzzleAttempts => puzzle == null ? 0 : puzzle.SubmissionCount;
        public float PlaySeconds => device == null ? 0f : device.ElapsedSeconds;
        public string CompletionText => device != null && device.LevelCompleted ? "已完成" : "未完成";

        public void Configure(C3TreasureProgress owner, C3PuzzleState puzzleState, C3DeviceState deviceState)
        {
            progress = owner;
            puzzle = puzzleState;
            device = deviceState;
        }

        private void Awake()
        {
            if (progress == null) progress = GetComponent<C3TreasureProgress>();
            if (puzzle == null) puzzle = GetComponent<C3PuzzleState>();
            if (device == null) device = GetComponent<C3DeviceState>();
        }

        private void OnEnable()
        {
            if (device != null) device.Completed += Show;
        }

        private void OnDisable()
        {
            if (device != null) device.Completed -= Show;
            if (active == this) active = null;
        }

        public void Show()
        {
            if (visible || device == null || !device.LevelCompleted) return;
            visible = true;
            active = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void RestartLevel()
        {
            if (!visible || restarting) return;
            restarting = true;
            active = null;
            string error = "Module coordinator is unavailable.";
            if (ModuleCoordinator.Instance == null ||
                !ModuleCoordinator.Instance.RestartCurrent(out error))
            {
                restarting = false;
                Debug.LogError("C3 restart failed: " + error);
            }
        }

        public void ReturnToSelection()
        {
            if (!visible || ModuleCoordinator.Instance == null) return;
            if (!ModuleCoordinator.Instance.ReturnToSelection(out string error))
                Debug.LogError("C3 return failed: " + error);
        }

        public static Rect CalculatePanelRect(int width, int height)
        {
            float margin = Mathf.Min(20f, Mathf.Min(width, height) * 0.04f);
            return new Rect(margin, margin, width - 2f * margin, height - 2f * margin);
        }

        private void OnGUI()
        {
            if (!visible) return;
            CjkGuiFont.Apply();
            GUI.depth = -1100;
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            Rect panel = CalculatePanelRect(Screen.width, Screen.height);
            GUI.Box(panel, "");
            Rect viewport = new Rect(panel.x + 12f, panel.y + 12f, panel.width - 24f, panel.height - 24f);
            float contentWidth = Mathf.Max(180f, viewport.width - 22f);
            float textWidth = contentWidth - 16f;
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = true };
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            string[] lines = {
                "淨化設備啟動成功",
                "零件收集結果：" + PartsText,
                "解謎嘗試次數：" + PuzzleAttempts,
                "本次關卡遊玩時間：" + FormatTime(PlaySeconds),
                "完成狀態：" + CompletionText,
                Knowledge
            };
            float[] heights = new float[lines.Length];
            float contentHeight = 12f;
            for (int i = 0; i < lines.Length; i++)
            {
                heights[i] = Mathf.Ceil((i == 0 ? title : body).CalcHeight(new GUIContent(lines[i]), textWidth)) + 12f;
                contentHeight += heights[i] + 12f;
            }
            contentHeight += 126f;
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0f, 0f, contentWidth, contentHeight));
            float y = 12f;
            for (int i = 0; i < lines.Length; i++)
            {
                GUI.Label(new Rect(8f, y, textWidth, heights[i]), lines[i], i == 0 ? title : body);
                y += heights[i] + 12f;
            }
            if (GUI.Button(new Rect(8f, y, textWidth, 48f), "重新開始本關卡")) RestartLevel();
            if (GUI.Button(new Rect(8f, y + 58f, textWidth, 48f), "返回 M6 開發驗證選關入口")) ReturnToSelection();
            GUI.EndScrollView();
        }

        public static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
    }
}
