using SiliconSandbox.Authoring;
using UnityEngine;

namespace SiliconSandbox.Interaction
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CreativeCameraController : MonoBehaviour
    {
        private const float HeldJumpLandingDelaySeconds = 0.15f;

        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 2.4f;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float flySpeed = 8f;

        private CharacterController character;
        private float pitch;
        private float verticalVelocity;
        private float lastSpacePress = -10f;
        private float heldJumpReadyAt;
        private bool jumpAwaitingLanding;
        private bool airborneSinceGroundContact;
        private bool heldJumpLandingObserved;
        private bool suppressHeldJumpUntilRelease;
        private bool interfaceOpen;
        private WorldBounds? worldBounds;

        public bool Flying { get; private set; }
        public bool InterfaceOpen => interfaceOpen;
        public Transform CameraPivot => cameraPivot;

        public void SetCameraPivot(Transform pivot) => cameraPivot = pivot;
        public void SetWorldBounds(WorldBounds bounds) => worldBounds = bounds;

        public void Teleport(Vector3 position)
        {
            // CharacterController.Move uses native collision state. Reset it
            // when loading a saved pose so the next frame cannot restore the
            // pre-load location.
            if (character == null) character = GetComponent<CharacterController>();
            character.enabled = false;
            transform.position = position;
            character.enabled = true;
            verticalVelocity = 0f;
            jumpAwaitingLanding = false;
            airborneSinceGroundContact = false;
            heldJumpLandingObserved = false;
            heldJumpReadyAt = 0f;
        }

        public void SetViewDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.999f ||
                direction.sqrMagnitude > 1.001f)
                throw new System.ArgumentException("View direction must be normalized.");
            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(-Mathf.Asin(direction.y) * Mathf.Rad2Deg,
                -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            verticalVelocity = 0f;
        }

        public void SetInterfaceOpen(bool open)
        {
            interfaceOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        public bool TryJump()
        {
            if (interfaceOpen || Flying || jumpAwaitingLanding ||
                !HasGroundSupport()) return false;
            verticalVelocity = 6f;
            jumpAwaitingLanding = true;
            airborneSinceGroundContact = false;
            heldJumpLandingObserved = false;
            return true;
        }

        public bool TryHeldJump()
        {
            return heldJumpLandingObserved &&
                   Time.unscaledTime >= heldJumpReadyAt && TryJump();
        }

        private bool HasGroundSupport()
        {
            if (character.isGrounded) return true;
            // isGrounded only describes the last CharacterController.Move.
            // It can be false while resting after spawn or Teleport, before
            // another Move has refreshed the collision flags.
            var bounds = character.bounds;
            var origin = new Vector3(bounds.center.x,
                bounds.min.y + 0.12f, bounds.center.z);
            return Physics.Raycast(origin, Vector3.down, out var hit,
                0.22f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && hit.collider != character;
        }

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null)
                cameraPivot = Camera.main.transform;
        }

        private void Start() => SetInterfaceOpen(false);

        private void Update()
        {
            if (interfaceOpen || cameraPivot == null) return;
            transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity,
                -89f, 89f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (Time.unscaledTime - lastSpacePress <= 0.3f)
                {
                    Flying = !Flying;
                    verticalVelocity = 0f;
                    jumpAwaitingLanding = false;
                    airborneSinceGroundContact = false;
                    heldJumpLandingObserved = false;
                    suppressHeldJumpUntilRelease = true;
                }
                else if (!Flying)
                    TryJump();
                lastSpacePress = Time.unscaledTime;
            }
            if (!Input.GetKey(KeyCode.Space))
                suppressHeldJumpUntilRelease = false;
            else if (!Input.GetKeyDown(KeyCode.Space) && !Flying &&
                     !suppressHeldJumpUntilRelease)
                TryHeldJump();

            var horizontal = Input.GetAxisRaw("Horizontal");
            var forward = Input.GetAxisRaw("Vertical");
            var planar = (transform.right * horizontal + transform.forward * forward);
            if (planar.sqrMagnitude > 1f) planar.Normalize();
            var sprint = Input.GetKey(KeyCode.LeftControl) ? 1.5f : 1f;
            var sneak = Input.GetKey(KeyCode.LeftShift) && !Flying ? 0.3f : 1f;
            var velocity = planar * (Flying ? flySpeed : walkSpeed) * sprint * sneak;
            if (Flying)
            {
                if (Input.GetKey(KeyCode.Space)) velocity.y += flySpeed;
                if (Input.GetKey(KeyCode.LeftShift)) velocity.y -= flySpeed;
            }
            else
            {
                if (character.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = -1f;
                verticalVelocity -= 16f * Time.deltaTime;
                velocity.y = verticalVelocity;
            }
            character.Move(velocity * Time.deltaTime);
            if (!Flying)
            {
                if (!HasGroundSupport())
                    airborneSinceGroundContact = true;
                else if (character.isGrounded &&
                         airborneSinceGroundContact && verticalVelocity <= 0f)
                {
                    jumpAwaitingLanding = false;
                    airborneSinceGroundContact = false;
                    heldJumpLandingObserved = true;
                    heldJumpReadyAt = Time.unscaledTime +
                        HeldJumpLandingDelaySeconds;
                }
            }
            if (worldBounds.HasValue)
            {
                var bounds = worldBounds.Value;
                var foot = transform.position;
                var inside = new Vector3(
                    Mathf.Clamp(foot.x, character.radius,
                        bounds.WidthCells - character.radius),
                    Mathf.Clamp(foot.y, 1f,
                        Mathf.Max(1f, bounds.HeightCells - character.height)),
                    Mathf.Clamp(foot.z, character.radius,
                        bounds.LengthCells - character.radius));
                if (inside != foot)
                {
                    transform.position = inside;
                    if (inside.y != foot.y) verticalVelocity = 0f;
                }
            }
        }
    }
}
