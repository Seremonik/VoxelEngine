using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

namespace VoxelEngine.Example
{
    public class CharacterControllerExample : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float gravity = -9.81f;
        public float jumpHeight = 1.5f;

        public float mouseSensitivity = 100f;
        public Transform cameraTransform;

        private CharacterController controller;
        private Vector3 velocity;
        private bool isGrounded;
        private float xRotation = 0f;
        private bool isLocked;

        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpPressed;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
        public void OnLook(InputValue value) => lookInput = value.Get<Vector2>();
        public void OnJump(InputValue value) => jumpPressed = value.isPressed;

        public void OnToggleCursorLock(InputValue value)
        {
            if (value.isPressed)
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
        }

        public void OnToggleMovementLock(InputValue value)
        {
            if (value.isPressed)
                isLocked = !isLocked;
        }

        void Update()
        {
            if (isLocked)
                return;

            // Mouse look
            float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
            float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);

            // Grounded check
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
                velocity.y = -2f;

            // Movement
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Jump
            if (jumpPressed && isGrounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Gravity
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
