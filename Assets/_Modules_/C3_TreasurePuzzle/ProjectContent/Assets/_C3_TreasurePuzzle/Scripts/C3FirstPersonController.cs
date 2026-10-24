using UnityEngine;

namespace C3TreasurePuzzle
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class C3FirstPersonController : MonoBehaviour
    {
        [Header("視角")]
        [SerializeField] private Transform viewTransform;
        [SerializeField, Range(0.1f, 10f)] private float mouseSensitivity = 2f;
        [SerializeField, Range(30f, 89f)] private float maxLookAngle = 80f;

        [Header("移動")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float runSpeed = 7f;
        [SerializeField, Min(0.1f)] private float crouchSpeed = 2f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [SerializeField] private bool allowJump = true;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;

        [Header("蹲下")]
        [SerializeField, Min(0.6f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.6f)] private float crouchHeight = 1.1f;
        [SerializeField, Min(0.1f)] private float standingEyeHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float crouchEyeHeight = 0.85f;
        [SerializeField, Min(0.1f)] private float stanceSpeed = 3f;

        private CharacterController characterController;
        private float verticalSpeed;
        private float lookPitch;
        private float currentMoveSpeed;
        private bool isCrouching;
        private readonly Collider[] clearanceHits = new Collider[32];

        public void SetViewTransform(Transform value) => viewTransform = value;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (viewTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null) viewTransform = childCamera.transform;
            }
            standingHeight = Mathf.Max(standingHeight, characterController.radius * 2f);
            crouchHeight = Mathf.Clamp(crouchHeight, characterController.radius * 2f, standingHeight);
            characterController.height = standingHeight;
            characterController.center = Vector3.up * (standingHeight * 0.5f);
            if (viewTransform != null)
                viewTransform.localPosition = new Vector3(viewTransform.localPosition.x, standingEyeHeight, viewTransform.localPosition.z);
            if (viewTransform != null) lookPitch = Mathf.DeltaAngle(0f, viewTransform.localEulerAngles.x);
        }

        private void Start()
        {
            // 遊戲開始先鎖定滑鼠；ESC 放開，點擊遊戲畫面可重新鎖定。
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (C3PuzzleConsole.BlocksGameplayInputThisFrame || C3CompletionScreen.BlocksGameplayInputThisFrame)
            {
                TickMovement(Vector2.zero, false, false, false, Time.deltaTime);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 解鎖滑鼠時停用水平與視角輸入，但重力仍持續作用。
            bool acceptsInput = Cursor.lockState == CursorLockMode.Locked && viewTransform != null;
            if (acceptsInput)
            {
                transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
                lookPitch = Mathf.Clamp(lookPitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -maxLookAngle, maxLookAngle);
                viewTransform.localRotation = Quaternion.Euler(lookPitch, 0f, 0f);
            }

            Vector2 input = acceptsInput
                ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
                : Vector2.zero;
            TickMovement(input,
                acceptsInput && Input.GetKey(KeyCode.LeftShift),
                acceptsInput && Input.GetKey(KeyCode.LeftControl),
                acceptsInput && Input.GetKeyDown(KeyCode.Space),
                Time.deltaTime);
        }

        // Input is collected in Update; keeping the movement step separate also permits
        // PlayMode tests to exercise the real controller without synthetic OS key events.
        private void TickMovement(Vector2 input, bool sprintHeld, bool crouchHeld, bool jumpPressed, float deltaTime)
        {
            bool blockedFromStanding = !crouchHeld &&
                characterController.height < standingHeight - 0.01f && !CanStand();
            float targetHeight = crouchHeld || blockedFromStanding ? crouchHeight : standingHeight;
            float height = Mathf.MoveTowards(characterController.height, targetHeight, stanceSpeed * deltaTime);
            characterController.height = height;
            characterController.center = Vector3.up * (height * 0.5f);
            isCrouching = crouchHeld || height < standingHeight - 0.01f;
            if (viewTransform != null)
            {
                float eye = Mathf.Lerp(crouchEyeHeight, standingEyeHeight,
                    Mathf.InverseLerp(crouchHeight, standingHeight, height));
                Vector3 position = viewTransform.localPosition;
                position.y = Mathf.MoveTowards(position.y, eye, stanceSpeed * deltaTime);
                viewTransform.localPosition = position;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            currentMoveSpeed = isCrouching ? crouchSpeed : sprintHeld ? runSpeed : moveSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * currentMoveSpeed;

            // Keep gravity active even when the cursor is unlocked.
            if (characterController.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (jumpPressed && allowJump && !isCrouching && characterController.isGrounded)
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            verticalSpeed -= gravity * deltaTime;

            CollisionFlags collision = characterController.Move((horizontal + Vector3.up * verticalSpeed) * deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collision & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        }

        private bool CanStand()
        {
            float radius = characterController.radius;
            Vector3 up = transform.up;
            Vector3 bottom = transform.position + up * radius;
            Vector3 top = transform.position + up * (standingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, clearanceHits,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (clearanceHits[i] != characterController &&
                    !clearanceHits[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
