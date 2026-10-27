using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SDGAdventure.SharedPlayer
{
    public enum PlayerMenuState { Playing, Paused, Settings, Tasks }

    public sealed class PlayerPauseMenu : MonoBehaviour
    {
        [SerializeField] private SharedFirstPersonController player;
        [SerializeField] private Transform testReturnPoint;

        private PlayerInputService input;
        private PlayerMenuCanvas view;
        private PlayerTaskJournal journal;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;
        private int rebindFinishedFrame = -1;
        private float previousTimeScale = 1f;
        private Action restartLevel;
        private Action returnToSelection;
        private Func<bool> canOpen;
        private bool tasksOpenedFromPause;
        private Action openTaskView;
        private Action closeTaskView;

        public PlayerMenuState State { get; private set; } = PlayerMenuState.Playing;
        public bool HasTestReturnPoint => testReturnPoint != null;
        public bool HasLevelActions => restartLevel != null && returnToSelection != null;
        public bool IsRebinding => activeRebind != null;
        public string Feedback { get; private set; } = "";
        public PlayerTaskJournal Journal => journal;
        public PlayerMenuCanvas View => view;
        public bool UsesExternalTaskView => openTaskView != null && closeTaskView != null;

        public void SetPlayer(SharedFirstPersonController value) => player = value;
        public void SetTestReturnPoint(Transform value) => testReturnPoint = value;
        public void SetLevelActions(Action restart, Action returnAction, Func<bool> canOpenMenu)
        {
            restartLevel = restart;
            returnToSelection = returnAction;
            canOpen = canOpenMenu;
        }
        public void SetTaskView(Action open, Action close)
        {
            openTaskView = open;
            closeTaskView = close;
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<SharedFirstPersonController>();
            journal = GetComponent<PlayerTaskJournal>();
            if (journal == null) journal = gameObject.AddComponent<PlayerTaskJournal>();
            view = GetComponent<PlayerMenuCanvas>();
            if (view == null) view = gameObject.AddComponent<PlayerMenuCanvas>();
            view.Build();
        }

        private void OnEnable()
        {
            input = PlayerInputService.Instance;
            if (input != null)
            {
                input.PausePressed += Pause;
                input.TaskListPressed += OpenTasks;
                input.UiTaskListPressed += HandleUiTaskList;
                input.UiCancelPressed += HandleCancel;
            }
            if (journal != null) journal.Changed += RefreshTasks;
        }

        private void Start() => Resume();

        public void Pause()
        {
            if (State != PlayerMenuState.Playing || input == null || (canOpen != null && !canOpen())) return;
            EnterUi(PlayerMenuState.Paused);
            view.ShowPause(this);
        }

        public void OpenTasks()
        {
            if (State != PlayerMenuState.Playing || input == null || (canOpen != null && !canOpen())) return;
            tasksOpenedFromPause = false;
            EnterUi(PlayerMenuState.Tasks);
            ShowTasks();
        }

        public void OpenTasksFromPause()
        {
            if (State != PlayerMenuState.Paused) return;
            tasksOpenedFromPause = true;
            State = PlayerMenuState.Tasks;
            ShowTasks();
        }

        public void CloseTasks()
        {
            if (State != PlayerMenuState.Tasks) return;
            if (UsesExternalTaskView) closeTaskView();
            if (!tasksOpenedFromPause) { Resume(); return; }
            tasksOpenedFromPause = false;
            State = PlayerMenuState.Paused;
            view.ShowPause(this);
        }

        public void OpenSettings()
        {
            if (State != PlayerMenuState.Paused) return;
            State = PlayerMenuState.Settings;
            Feedback = "";
            view.ShowSettings(this, input);
        }

        public void BackToPause()
        {
            if (State != PlayerMenuState.Settings) return;
            CancelRebind();
            State = PlayerMenuState.Paused;
            view.ShowPause(this);
        }

        public void Resume()
        {
            CancelRebind();
            if (UsesExternalTaskView) closeTaskView();
            tasksOpenedFromPause = false;
            State = PlayerMenuState.Playing;
            view?.Hide();
            Time.timeScale = previousTimeScale;
            input?.SetContext(PlayerInputContext.Gameplay);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReturnToTestStart()
        {
            if (testReturnPoint == null || player == null) return;
            player.Teleport(testReturnPoint.position, testReturnPoint.rotation);
            Resume();
        }

        public void RestartLevel() => restartLevel?.Invoke();
        public void ReturnToSelection() => returnToSelection?.Invoke();

        public void BeginRebind(string map, string action, int bindingIndex)
        {
            if (State != PlayerMenuState.Settings || activeRebind != null) return;
            Feedback = "請按下新按鍵；Esc 取消重綁。";
            activeRebind = input.Keybindings.BeginInteractiveRebind(map, action, bindingIndex,
                (accepted, message) =>
                {
                    activeRebind = null;
                    rebindFinishedFrame = Time.frameCount;
                    Feedback = accepted ? "按鍵已更新並儲存。" : message;
                    if (State == PlayerMenuState.Settings) view.RefreshSettings(this, input);
                });
            view.RefreshSettings(this, input);
        }

        public void CancelRebind()
        {
            if (activeRebind == null) return;
            InputActionRebindingExtensions.RebindingOperation operation = activeRebind;
            activeRebind = null;
            operation.Cancel();
            if (State == PlayerMenuState.Settings) view.RefreshSettings(this, input);
        }

        public void RestoreDefaults()
        {
            CancelRebind();
            input.RestoreDefaults();
            Feedback = "已恢復預設按鍵與設定。";
            view.RefreshSettings(this, input);
        }

        public void ToggleSprintMode()
        {
            ButtonMode current = input.Keybindings.Settings.sprintMode;
            input.SetSprintMode(current == ButtonMode.Hold ? ButtonMode.Toggle : ButtonMode.Hold);
            view.RefreshSettings(this, input);
        }

        public void ToggleCrouchMode()
        {
            ButtonMode current = input.Keybindings.Settings.crouchMode;
            input.SetCrouchMode(current == ButtonMode.Hold ? ButtonMode.Toggle : ButtonMode.Hold);
            view.RefreshSettings(this, input);
        }

        public void SetMouseSensitivity(float value)
        {
            float rounded = Mathf.Round(value * 10f) * 0.1f;
            if (Mathf.Abs(rounded - input.Keybindings.Settings.mouseSensitivity) < 0.05f) return;
            input.SetMouseSensitivity(rounded);
            view.RefreshSettings(this, input);
        }

        private void EnterUi(PlayerMenuState state)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            State = state;
            Time.timeScale = 0f;
            input.SetContext(PlayerInputContext.UI);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleCancel()
        {
            if (activeRebind != null) { CancelRebind(); return; }
            if (rebindFinishedFrame == Time.frameCount) return;
            if (State == PlayerMenuState.Tasks) CloseTasks();
            else if (State == PlayerMenuState.Settings) BackToPause();
            else if (State == PlayerMenuState.Paused) Resume();
        }

        private void HandleUiTaskList()
        {
            if (State == PlayerMenuState.Tasks) CloseTasks();
            else if (State == PlayerMenuState.Paused && UsesExternalTaskView) OpenTasksFromPause();
        }

        private void ShowTasks()
        {
            if (UsesExternalTaskView)
            {
                view.Hide();
                openTaskView();
            }
            else view.ShowTasks(this, journal);
        }

        private void RefreshTasks()
        {
            if (State == PlayerMenuState.Tasks && !UsesExternalTaskView) view.ShowTasks(this, journal);
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PausePressed -= Pause;
                input.TaskListPressed -= OpenTasks;
                input.UiTaskListPressed -= HandleUiTaskList;
                input.UiCancelPressed -= HandleCancel;
                if (input.Context != PlayerInputContext.Inactive)
                    input.SetContext(PlayerInputContext.Inactive);
            }
            if (journal != null) journal.Changed -= RefreshTasks;
            CancelRebind();
            if (UsesExternalTaskView) closeTaskView();
            if (State != PlayerMenuState.Playing) Time.timeScale = previousTimeScale;
            view?.Hide();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
