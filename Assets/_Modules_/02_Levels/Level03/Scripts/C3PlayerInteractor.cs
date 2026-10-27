using UnityEngine;
using SDGAdventure.UI;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    public sealed class C3PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask raycastLayers = ~0;

        private C3Interactable currentTarget;
        private GUIStyle crosshairStyle;
        private GUIStyle promptStyle;
        private PlayerInputService sharedInput;
        private bool interactQueued;

        public void SetCamera(Camera value) => playerCamera = value;
        public void UseSharedInput(PlayerInputService service)
        {
            if (sharedInput != null) sharedInput.InteractPressed -= QueueInteract;
            sharedInput = service;
            if (isActiveAndEnabled && sharedInput != null) sharedInput.InteractPressed += QueueInteract;
        }

        private void OnEnable()
        {
            if (sharedInput != null) sharedInput.InteractPressed += QueueInteract;
        }

        private void OnDisable()
        {
            if (sharedInput != null) sharedInput.InteractPressed -= QueueInteract;
            interactQueued = false;
        }

        private void QueueInteract() => interactQueued = true;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            bool interact = sharedInput != null ? interactQueued : Input.GetKeyDown(KeyCode.E);
            interactQueued = false;
            currentTarget = null;
            if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked ||
                (sharedInput != null && sharedInput.Context != PlayerInputContext.Gameplay) ||
                C3PuzzleConsole.BlocksGameplayInputThisFrame || C3CompletionScreen.BlocksGameplayInputThisFrame) return;

            // 只看第一個射線命中；牆或非互動物件會阻斷後方目標。
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, raycastLayers, QueryTriggerInteraction.Ignore))
            {
                C3Interactable candidate = hit.collider.GetComponentInParent<C3Interactable>();
                if (candidate != null && candidate.isActiveAndEnabled && candidate.CanInteract(gameObject))
                    currentTarget = candidate;
            }

            // KeyDown 每次按鍵只觸發一次，不因停留或持續接觸重複執行。
            if (currentTarget != null && interact)
            {
                currentTarget.Interact(gameObject);
                // A collected target can disable itself in Interact; hide its prompt this frame.
                if (currentTarget != null && !currentTarget.CanInteract(gameObject)) currentTarget = null;
            }
        }

        private void OnGUI()
        {
            if (Cursor.lockState != CursorLockMode.Locked ||
                (sharedInput != null && sharedInput.Context != PlayerInputContext.Gameplay) ||
                C3PuzzleConsole.BlocksGameplayInputThisFrame ||
                C3CompletionScreen.BlocksGameplayInputThisFrame) return;
            CjkGuiFont.Apply();
            if (crosshairStyle == null)
            {
                crosshairStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
                crosshairStyle.normal.textColor = Color.white;
                promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
                promptStyle.normal.textColor = Color.white;
            }

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            GUI.Label(new Rect(centerX - 15f, centerY - 15f, 30f, 30f), "+", crosshairStyle);
            if (currentTarget != null)
                GUI.Label(new Rect(centerX - 220f, centerY + 28f, 440f, 32f), currentTarget.DisplayName +
                    "  [" + (sharedInput == null ? "E" : sharedInput.Keybindings.BindingDisplay("Gameplay", "Interact", 0)) + "] 互動", promptStyle);
        }
    }
}
