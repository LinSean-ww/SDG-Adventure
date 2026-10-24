using UnityEngine;
using SDGAdventure.UI;

namespace C3TreasurePuzzle
{
    public sealed class C3TreasureHud : MonoBehaviour
    {
        [SerializeField] private C3TreasureProgress progress;
        private C3DeviceState device;
        private bool taskSheetOpen;
        private Vector2 scrollPosition;
        private float feedbackUntil;
        private GUIStyle headingStyle;
        private GUIStyle missionStyle;
        private GUIStyle progressStyle;
        private GUIStyle acquiredStyle;
        private GUIStyle missingStyle;
        private GUIStyle hintStyle;
        private GUIStyle smallStyle;

        private static readonly Color PanelColor = new Color(0.035f, 0.065f, 0.09f, 0.96f);
        private static readonly Color CompactColor = new Color(0.035f, 0.065f, 0.09f, 0.72f);
        private static readonly Color CollectedCompactColor = new Color(0.10f, 0.32f, 0.20f, 0.86f);
        private static readonly Color RowColor = new Color(0.11f, 0.17f, 0.21f, 0.98f);
        private static readonly Color HintColor = new Color(0.08f, 0.23f, 0.27f, 0.98f);
        private static readonly Color AccentColor = new Color(0.31f, 0.91f, 0.94f, 1f);
        private static readonly Color AcquiredColor = new Color(0.67f, 1f, 0.77f, 1f);
        private static readonly Color MissingColor = new Color(1f, 0.84f, 0.52f, 1f);

        public const string CollapsedText = "任務";
        public bool TaskSheetOpen => taskSheetOpen;
        public bool CollectionFeedbackActive => Time.unscaledTime < feedbackUntil;
        public void SetProgress(C3TreasureProgress value) => progress = value;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<C3TreasureProgress>();
            device = FindObjectOfType<C3DeviceState>();
        }

        private void OnEnable()
        {
            if (progress != null) progress.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (progress != null) progress.Changed -= OnChanged;
        }

        private void OnChanged() => feedbackUntil = Time.unscaledTime + 3f;

        private void Update()
        {
            // Legacy Input key edge: holding P does not repeatedly toggle the sheet.
            if (!C3PuzzleConsole.BlocksGameplayInputThisFrame && !C3CompletionScreen.BlocksGameplayInputThisFrame &&
                Input.GetKeyDown(KeyCode.P)) ToggleTaskSheet();
        }

        public void ToggleTaskSheet()
        {
            taskSheetOpen = !taskSheetOpen;
            if (!taskSheetOpen) scrollPosition = Vector2.zero;
        }

        public string GuidanceText => progress == null ? "" : device != null && device.IsActivated
            ? "淨化設備已啟動，本關已完成"
            : device != null && device.Phase == C3DevicePhase.ReadyToActivate
                ? "解謎已完成，請關閉面板後瞄準控制台按 E\n確認啟動淨化設備"
                : progress.AllPartsCollected
                    ? "零件收集完成，請前往控制台\n東北角青色立柱前的資源處理控制台\n按 E 開啟解謎面板"
                    : progress.Objective;

        public string StatusText => progress == null ? "" :
            "尋找淨化設備零件\n" + progress.CollectedCount + "/3\n" +
            "保險絲：" + State(C3TreasureProgress.FuseId) + "\n" +
            "控制晶片：" + State(C3TreasureProgress.ChipId) + "\n" +
            "過濾模組：" + State(C3TreasureProgress.FilterId) + "\n" + GuidanceText;

        private string State(string id) => progress.HasPart(id) ? "已取得" : "未取得";

        private static float Scale(int width, int height) =>
            Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.8f, 1.2f);

        public static Rect CalculateCompactRect(int width, int height)
        {
            float scale = Scale(width, height);
            return new Rect(16f * scale, 16f * scale, 82f * scale, 40f * scale);
        }

        public static Rect CalculateSheetRect(int width, int height, float desiredHeight)
        {
            float scale = Scale(width, height);
            float margin = 16f * scale;
            return new Rect(margin, margin,
                Mathf.Min(540f * scale, width * 0.44f),
                Mathf.Min(desiredHeight, height - margin * 2f));
        }

        // Order: heading, mission, progress, three parts, hint heading, guidance, close hint.
        // OnGUI supplies the height measured from each actual string and its drawing width.
        public static Rect[] CalculateContentRects(float scale, float contentWidth, float[] heights)
        {
            if (heights == null || heights.Length != 9)
                throw new System.ArgumentException("Nine measured section heights are required.", nameof(heights));
            Rect[] rects = new Rect[9];
            float x = 8f * scale;
            float width = contentWidth - 16f * scale;
            float y = 10f * scale;
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = new Rect(x, y, width, heights[i]);
                y += heights[i] + (i == 5 || i == 7 ? 13f : 8f) * scale;
            }
            return rects;
        }

        private void OnGUI()
        {
            if (progress == null || C3PuzzleConsole.BlocksGameplayInputThisFrame ||
                C3CompletionScreen.BlocksGameplayInputThisFrame) return;
            CjkGuiFont.Apply();
            float scale = Scale(Screen.width, Screen.height);
            PrepareStyles(scale);
            if (taskSheetOpen) DrawTaskSheet(scale);
            else DrawCollapsed(scale);
        }

        private void DrawCollapsed(float scale)
        {
            Rect panel = CalculateCompactRect(Screen.width, Screen.height);
            Fill(panel, CollectionFeedbackActive ? CollectedCompactColor : CompactColor);
            GUI.Label(new Rect(panel.x + 10f * scale, panel.y + 3f * scale,
                panel.width - 20f * scale, panel.height - 6f * scale), CollapsedText, smallStyle);
        }

        private void DrawTaskSheet(float scale)
        {
            float panelWidth = Mathf.Min(540f * scale, Screen.width * 0.44f);
            float viewportWidth = panelWidth - 12f * scale;
            float contentWidth = viewportWidth - 18f * scale; // Reserve the scrollbar width.
            float rowWidth = contentWidth - 16f * scale;
            float textWidth = rowWidth - 20f * scale;
            string[] labels = {
                "任務", "尋找淨化設備零件", "收集進度  " + progress.CollectedCount + "/3",
                "保險絲：" + State(C3TreasureProgress.FuseId),
                "控制晶片：" + State(C3TreasureProgress.ChipId),
                "過濾模組：" + State(C3TreasureProgress.FilterId),
                progress.AllPartsCollected ? "下一步" : "目前線索",
                GuidanceText, "再按 P 關閉任務表"
            };
            GUIStyle[] styles = { headingStyle, missionStyle, progressStyle,
                progress.HasPart(C3TreasureProgress.FuseId) ? acquiredStyle : missingStyle,
                progress.HasPart(C3TreasureProgress.ChipId) ? acquiredStyle : missingStyle,
                progress.HasPart(C3TreasureProgress.FilterId) ? acquiredStyle : missingStyle,
                smallStyle, hintStyle, smallStyle };
            float[] heights = new float[9];
            for (int i = 0; i < heights.Length; i++)
            {
                float paddingY = i == 7 ? 10f : 6f;
                heights[i] = Mathf.Ceil(styles[i].CalcHeight(new GUIContent(labels[i]), textWidth))
                    + (paddingY * 2f + 4f) * scale;
            }
            Rect[] rects = CalculateContentRects(scale, contentWidth, heights);
            float contentHeight = rects[8].yMax + 12f * scale;
            Rect panel = CalculateSheetRect(Screen.width, Screen.height, contentHeight + 12f * scale);
            Fill(panel, PanelColor);
            Fill(new Rect(panel.x, panel.y, 4f * scale, panel.height), AccentColor);

            Rect viewport = new Rect(panel.x + 6f * scale, panel.y + 6f * scale,
                viewportWidth, panel.height - 12f * scale);
            Rect content = new Rect(0f, 0f, contentWidth, contentHeight);
            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, content, false, false);
            for (int i = 0; i < rects.Length; i++)
            {
                if (i >= 3 && i <= 5) Fill(rects[i], RowColor);
                if (i == 7) Fill(rects[i], HintColor);
                float paddingY = (i == 7 ? 10f : 6f) * scale;
                GUI.Label(new Rect(rects[i].x + 10f * scale, rects[i].y + paddingY,
                    textWidth, rects[i].height - 2f * paddingY), labels[i], styles[i]);
            }
            GUI.EndScrollView();
        }

        private void PrepareStyles(float scale)
        {
            if (headingStyle == null)
            {
                headingStyle = MakeStyle(FontStyle.Bold, Color.white);
                missionStyle = MakeStyle(FontStyle.Bold, Color.white);
                progressStyle = MakeStyle(FontStyle.Bold, AccentColor);
                acquiredStyle = MakeStyle(FontStyle.Bold, AcquiredColor);
                missingStyle = MakeStyle(FontStyle.Bold, MissingColor);
                hintStyle = MakeStyle(FontStyle.Normal, Color.white);
                smallStyle = MakeStyle(FontStyle.Normal, Color.white);
            }
            headingStyle.fontSize = Mathf.RoundToInt(26f * scale);
            missionStyle.fontSize = Mathf.RoundToInt(20f * scale);
            progressStyle.fontSize = Mathf.RoundToInt(22f * scale);
            acquiredStyle.fontSize = Mathf.RoundToInt(19f * scale);
            missingStyle.fontSize = acquiredStyle.fontSize;
            hintStyle.fontSize = Mathf.RoundToInt(18f * scale);
            smallStyle.fontSize = Mathf.RoundToInt(18f * scale);
        }

        private static GUIStyle MakeStyle(FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { fontStyle = fontStyle, wordWrap = true };
            style.normal.textColor = color;
            return style;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
