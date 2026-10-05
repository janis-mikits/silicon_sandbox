using SiliconSandbox.Authoring;
using UnityEngine;

namespace SiliconSandbox.Interaction
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CreativeCameraController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 2.4f;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float flySpeed = 8f;

        private CharacterController character;
        private float pitch;
        private float verticalVelocity;
        private float lastSpacePress = -10f;
        private bool interfaceOpen;
        private WorldBounds? worldBounds;

        public bool Flying { get; private set; }
        public Transform CameraPivot => cameraPivot;

        public void SetCameraPivot(Transform pivot) => cameraPivot = pivot;
        public void SetWorldBounds(WorldBounds bounds) => worldBounds = bounds;

        public void SetInterfaceOpen(bool open)
        {
            interfaceOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
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
                }
                else if (!Flying && character.isGrounded)
                    verticalVelocity = 6f;
                lastSpacePress = Time.unscaledTime;
            }

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
