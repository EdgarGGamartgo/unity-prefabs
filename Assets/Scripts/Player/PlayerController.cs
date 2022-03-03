using UnityEngine;
using static Models;

public class PlayerController : MonoBehaviour
{
    private CharacterController characterController;
    private DefaultInput defaultInput;
    private Vector2 inputMovement;
    private Vector2 inputView;
    private Vector3 newCameraRotation;
    private Vector3 newPlayerRotation;

    [Header("References")]
    public Transform cameraHolder;
    public Transform feetTransform;

    [Header("Settings")]
    public PlayerSettingsModel playerSettings;
    public float viewClampYMin = -50;
    public float viewClampYMax = 80;
    public LayerMask playerMask;

    [Header("Gravity")]
    public float gravityAmount;
    private float playerGravity;
    public float gravityMin;
    public Vector3 jumpingForce;
    private Vector3 jumpingForceVelocity;

    [Header("Stance")]
    public PlayerStance playerStance;
    public float playerStanceSmoothing;
    public CharacterStance playerStandStance;
    public CharacterStance playerCrouchStance;
    public CharacterStance playerProneStance;
    private float stanceCheckErrorMargin = 0.05f;
    private float cameraHeight;
    private float cameraHeightVelocity;
    private Vector3 stanceCapsuleCenterVelocity;
    private float stanceCapsuleHeightVelocity;
    private bool isSprinting;
    private Vector3 newMovementSpeed;
    private Vector3 newMovementSpeedVelocity;


    private void Awake()
    {
        defaultInput = new DefaultInput();
        defaultInput.Player.Movement.performed += e => inputMovement = e.ReadValue<Vector2>();
        defaultInput.Player.View.performed += e => inputView = e.ReadValue<Vector2>();
        defaultInput.Player.Jump.performed += e => Jump();
        defaultInput.Player.Prone.performed += e => Prone();
        defaultInput.Player.Crouch.performed += e => Crouch();
        defaultInput.Player.Sprint.performed += e => ToggleSprint();
        defaultInput.Player.SprintReleased.performed += e => StopSprint();
        defaultInput.Enable();
        newCameraRotation = cameraHolder.localRotation.eulerAngles;
        newPlayerRotation = transform.localRotation.eulerAngles;
        characterController = GetComponent<CharacterController>();
        cameraHeight = cameraHolder.localPosition.y;
    }

    private void Update()
    {
        CalculateView();
        CalculateMovement();
        CalculateJump();
        CalculateStance();
    }

    private void CalculateView()
    {
        newPlayerRotation.y += playerSettings.viewXSensitivity * (playerSettings.viewXInverted ? -inputView.x : inputView.x) * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(newPlayerRotation);
        newCameraRotation.x += playerSettings.viewYSensitivity * (playerSettings.viewYInverted ? inputView.y : -inputView.y) * Time.deltaTime;
        newCameraRotation.x = Mathf.Clamp(newCameraRotation.x, viewClampYMin, viewClampYMax);
        cameraHolder.localRotation = Quaternion.Euler(newCameraRotation);
    }

    private void CalculateMovement()
    {
        if (inputMovement.y <= 0.2f)
        {
            isSprinting = false;
        }
        float verticalSpeed = playerSettings.walkingForwardSpeed;
        float horizontalSpeed = playerSettings.walkingStrafeSpeed;

        if (isSprinting)
        {
            verticalSpeed = playerSettings.runningForwardSpeed;
            horizontalSpeed = playerSettings.runningStrafeSpeed;
        }

        // Effectors
        if (!characterController.isGrounded) {
            playerSettings.speedEffector = playerSettings.fallingSpeedEffector;
        }
        else if (playerStance == PlayerStance.Crouch) {
            playerSettings.speedEffector = playerSettings.crouchSpeedEffector;
        }
        else if (playerStance == PlayerStance.Prone) {
            playerSettings.speedEffector = playerSettings.proneSpeedEffector;
        }
        else {
            playerSettings.speedEffector = 1;
        }
        verticalSpeed *= playerSettings.speedEffector;
        horizontalSpeed *= playerSettings.speedEffector;

        newMovementSpeed = Vector3.SmoothDamp(newMovementSpeed, new Vector3(horizontalSpeed * inputMovement.x * Time.deltaTime, 0, verticalSpeed * inputMovement.y * Time.deltaTime), ref newMovementSpeedVelocity, characterController.isGrounded ? playerSettings.movementSmoothing : playerSettings.fallingSmoothing);
        var movementSpeed = transform.TransformDirection(newMovementSpeed);

        if (playerGravity > gravityMin)
        {
            playerGravity -= gravityAmount * Time.deltaTime;
        }

        if (playerGravity < -0.1f && characterController.isGrounded)
        {
            playerGravity = -0.1f;
        }
        movementSpeed.y += playerGravity;
        movementSpeed += jumpingForce * Time.deltaTime;
        characterController.Move(movementSpeed);
    }

    private void CalculateJump()
    {
        jumpingForce = Vector3.SmoothDamp(jumpingForce, Vector3.zero, ref jumpingForceVelocity, playerSettings.jumpingFallof);
    }

    private void Jump()
    {
        if (!characterController.isGrounded)
        {
            return;
        }
        else if (playerStance == PlayerStance.Crouch)
        {
            if (StandCheck(playerStandStance.stanceCollider.height))
            {
                return;
            }
            playerStance = PlayerStance.Stand; // Make the player stand if the player tries to jump while crouching
            return;
        }
        else if (playerStance == PlayerStance.Prone)
        {
            playerStance = PlayerStance.Stand; // Make the player stand if the player tries to jump while proning
            return;
        }
        jumpingForce = Vector3.up * playerSettings.jumpingHeight;
        playerGravity = 0;
    }

    private void CalculateStance()
    {
        CharacterStance currentStance = playerStandStance;
        if (playerStance == PlayerStance.Crouch)
        {
            currentStance = playerCrouchStance;
        }
        else if (playerStance == PlayerStance.Prone)
        {
            currentStance = playerProneStance;
        }

        cameraHeight = Mathf.SmoothDamp(cameraHolder.localPosition.y, currentStance.cameraHeight, ref cameraHeightVelocity, playerStanceSmoothing);
        cameraHolder.localPosition = new Vector3(cameraHolder.localPosition.x, cameraHeight, cameraHolder.localPosition.z);
        characterController.height = Mathf.SmoothDamp(characterController.height, currentStance.stanceCollider.height, ref stanceCapsuleHeightVelocity, playerStanceSmoothing);
        characterController.center = Vector3.SmoothDamp(characterController.center, currentStance.stanceCollider.center, ref stanceCapsuleCenterVelocity, playerStanceSmoothing);
    }

    private void Crouch()
    {
        if (playerStance == PlayerStance.Crouch)
        {
            if (StandCheck(playerStandStance.stanceCollider.height))
            {
                return;
            }
            playerStance = PlayerStance.Stand;
            return;
        }
        if (StandCheck(playerCrouchStance.stanceCollider.height))
        {
            return;
        }
        playerStance = PlayerStance.Crouch;
        isSprinting = false;
    }

    private void Prone()
    {
        playerStance = PlayerStance.Prone;
        isSprinting = false;
    }

    private bool StandCheck(float standCheckHeight)
    {
        Vector3 start = new Vector3(feetTransform.position.x, feetTransform.position.y + characterController.radius + stanceCheckErrorMargin, feetTransform.position.z);
        Vector3 end = new Vector3(feetTransform.position.x, feetTransform.position.y - characterController.radius - stanceCheckErrorMargin + standCheckHeight, feetTransform.position.z);
        return Physics.CheckCapsule(start, end, characterController.radius, playerMask);
    }

    private void ToggleSprint()
    {
        if(playerStance == PlayerStance.Prone || playerStance == PlayerStance.Crouch) {
            playerStance = PlayerStance.Stand;
        }
        if (inputMovement.y <= 0.2f)
        {
            isSprinting = false;
            return;
        }
        isSprinting = !isSprinting;
    }

    private void StopSprint()
    {
        if(playerSettings.sprintingHold) {
            isSprinting = false;
        }
    }
}
