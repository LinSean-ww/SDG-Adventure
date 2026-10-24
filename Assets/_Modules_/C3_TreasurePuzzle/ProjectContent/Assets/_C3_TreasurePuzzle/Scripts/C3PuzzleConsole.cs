using System.Collections.Generic;
using UnityEngine;
using SDGAdventure.UI;

namespace C3TreasurePuzzle
{
    [RequireComponent(typeof(Collider))]
    public sealed class C3PuzzleConsole : C3Interactable
    {
        public const string Clue = "工廠作業線索：先把混合廢棄物依材質分類；可再利用的材料接著送去資源回收；最後妥善處理剩餘污染物。請依作業先後輸入。這是本關的簡化教育謎題，並非所有工業廢棄物的唯一標準流程。";
        private static readonly string[] Names = { "廢棄物分類", "資源回收", "污染處理" };
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        [SerializeField] private C3DeviceState device;
        private readonly List<C3ProcessingStep> selection = new List<C3ProcessingStep>(3);
        private bool panelOpen;
        private bool activationPanel;
        private string feedback = "";
        private float noticeUntil;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private Vector2 scroll;
        private static C3PuzzleConsole activePanel;
        private static int closedFrame = -1;

        public override string DisplayName => "資源處理控制台";
        public bool PanelOpen => panelOpen;
        public string Feedback => feedback;
        public IReadOnlyList<C3ProcessingStep> Selection => selection;
        public static bool BlocksGameplayInputThisFrame => activePanel != null || closedFrame == Time.frameCount;

        public void Configure(C3TreasureProgress owner, C3PuzzleState state)
        {
            progress = owner;
            puzzle = state;
        }

        public void SetDevice(C3DeviceState value) => device = value;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<C3TreasureProgress>();
            if (puzzle == null) puzzle = FindObjectOfType<C3PuzzleState>();
            if (device == null) device = FindObjectOfType<C3DeviceState>();
        }

        public override bool CanInteract(GameObject actor) => isActiveAndEnabled && progress != null && puzzle != null &&
            device != null && !device.IsActivated && !panelOpen && !C3CompletionScreen.BlocksGameplayInputThisFrame;

        public override void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            if (!progress.AllPartsCollected)
            {
                feedback = "零件尚未收齊：" + MissingParts();
                noticeUntil = Time.unscaledTime + 4f;
                return;
            }
            OpenPanel();
        }

        public bool OpenPanel()
        {
            if (panelOpen || activePanel != null || progress == null || puzzle == null || device == null ||
                device.IsActivated || C3CompletionScreen.BlocksGameplayInputThisFrame || !progress.AllPartsCollected) return false;
            selection.Clear();
            scroll = Vector2.zero;
            activationPanel = puzzle.PuzzleSolved;
            feedback = puzzle.PuzzleSolved ? "解謎已完成；設備尚未啟動。請確認啟動。" : "請依線索選擇三個處理步驟。";
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            activePanel = this;
            panelOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void ClosePanel()
        {
            if (!panelOpen) return;
            panelOpen = false;
            if (activePanel == this) activePanel = null;
            closedFrame = Time.frameCount;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        public bool SelectStep(C3ProcessingStep step)
        {
            if (!panelOpen || puzzle.PuzzleSolved || selection.Count >= 3 ||
                step < C3ProcessingStep.Sorting || step > C3ProcessingStep.PollutionTreatment ||
                selection.Contains(step)) return false;
            selection.Add(step);
            feedback = "已選擇 " + selection.Count + "/3 個步驟。";
            return true;
        }

        public void Retry()
        {
            if (!panelOpen || puzzle.PuzzleSolved) return;
            selection.Clear();
            feedback = "已清除順序，請重新選擇。";
        }

        public bool Confirm()
        {
            if (!panelOpen || puzzle.PuzzleSolved) return false;
            if (selection.Count != 3)
            {
                feedback = "請先選滿三個處理步驟。";
                return false;
            }
            bool correct = puzzle.SubmitAnswer(selection.ToArray());
            feedback = correct ? "解謎成功！已取得設備啟動資格；設備尚未啟動。" :
                "順序錯誤。請參考工廠作業線索，按「重試」重新挑戰。";
            return correct;
        }

        public bool ConfirmActivation()
        {
            if (!panelOpen || !activationPanel || device == null || device.Phase != C3DevicePhase.ReadyToActivate) return false;
            ClosePanel();
            return device.TryActivate();
        }

        public string MissingParts()
        {
            if (progress == null) return "進度資料未就緒";
            var names = new List<string>(3);
            if (!progress.HasPart(C3TreasureProgress.FuseId)) names.Add("保險絲");
            if (!progress.HasPart(C3TreasureProgress.ChipId)) names.Add("控制晶片");
            if (!progress.HasPart(C3TreasureProgress.FilterId)) names.Add("過濾模組");
            return string.Join("、", names.ToArray());
        }

        private void Update()
        {
            if (panelOpen && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
        }

        private void OnDisable()
        {
            ClosePanel();
        }

        private void OnGUI()
        {
            CjkGuiFont.Apply();
            GUI.depth = -1000;
            if (!panelOpen)
            {
                if (Time.unscaledTime < noticeUntil)
                {
                    float noticeWidth = Mathf.Min(520f, Screen.width - 24f);
                    Rect notice = new Rect((Screen.width - noticeWidth) * 0.5f,
                        Screen.height * 0.64f, noticeWidth, 62f);
                    GUI.Box(notice, feedback);
                }
                return;
            }

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            float width = Mathf.Min(720f, Screen.width - 24f);
            float height = Mathf.Min(640f, Screen.height - 24f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, "");
            Rect viewport = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f);
            float contentWidth = viewport.width - 22f;
            GUIStyle heading = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, wordWrap = true };
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            if (activationPanel)
            {
                scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0f, 0f, contentWidth, 300f));
                GUI.Label(new Rect(8f, 8f, contentWidth - 16f, 40f), "淨化設備待啟動", heading);
                GUI.Label(new Rect(8f, 62f, contentWidth - 16f, 86f),
                    "三項零件已收齊，處理順序已解開。按下確認後才會啟動淨化設備並完成本關。", body);
                if (GUI.Button(new Rect(8f, 174f, contentWidth - 16f, 48f), "確認啟動設備")) ConfirmActivation();
                if (GUI.Button(new Rect(8f, 232f, contentWidth - 16f, 48f), "暫不啟動／關閉")) ClosePanel();
                GUI.EndScrollView();
                return;
            }
            float textWidth = contentWidth - 16f;
            float clueHeight = Mathf.Ceil(body.CalcHeight(new GUIContent(Clue), textWidth)) + 8f;
            float promptHeight = Mathf.Ceil(body.CalcHeight(new GUIContent("選擇處理步驟（按作業先後）："), textWidth)) + 6f;
            string order = selection.Count == 0 ? "尚未輸入" : string.Join(" → ", selection.ConvertAll(step => Names[(int)step]).ToArray());
            float orderHeight = Mathf.Ceil(body.CalcHeight(new GUIContent("目前順序：" + order), textWidth)) + 8f;
            float feedbackHeight = Mathf.Ceil(body.CalcHeight(new GUIContent(feedback), textWidth)) + 12f;
            float y = 50f + clueHeight + 10f + promptHeight + 8f + 3f * 51f + 10f;
            float controlsY = y + orderHeight + feedbackHeight + 16f;
            float contentHeight = controlsY + 100f;
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, contentWidth, contentHeight));
            GUI.Label(new Rect(8, 4, contentWidth - 16, 40), DisplayName, heading);
            GUI.Label(new Rect(8, 50, textWidth, clueHeight), Clue, body);
            float promptY = 50f + clueHeight + 10f;
            GUI.Label(new Rect(8, promptY, textWidth, promptHeight), "選擇處理步驟（按作業先後）：", body);
            float choicesY = promptY + promptHeight + 8f;
            for (int i = 0; i < Names.Length; i++)
            {
                bool selected = selection.Contains((C3ProcessingStep)i);
                GUI.enabled = !puzzle.PuzzleSolved && !selected && selection.Count < 3;
                if (GUI.Button(new Rect(8, choicesY + 51f * i, textWidth, 43f), Names[i])) SelectStep((C3ProcessingStep)i);
            }
            GUI.enabled = true;
            GUI.Label(new Rect(8, y, textWidth, orderHeight), "目前順序：" + order, body);
            GUI.Label(new Rect(8, y + orderHeight + 8f, textWidth, feedbackHeight), feedback, body);
            float buttonWidth = (contentWidth - 36f) / 3f;
            GUI.enabled = !puzzle.PuzzleSolved;
            if (GUI.Button(new Rect(8, controlsY, buttonWidth, 48), "確認")) Confirm();
            if (GUI.Button(new Rect(18 + buttonWidth, controlsY, buttonWidth, 48), "重試")) Retry();
            GUI.enabled = true;
            if (GUI.Button(new Rect(28 + buttonWidth * 2f, controlsY, buttonWidth, 48), "關閉")) ClosePanel();
            GUI.Label(new Rect(8, controlsY + 60f, textWidth, 28), "ESC 可關閉面板", body);
            GUI.EndScrollView();
        }
    }
}
