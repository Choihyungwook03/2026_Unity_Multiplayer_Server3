using Fusion;
using UnityEngine;

public class SimplePlayer : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Bullet")]
    [SerializeField] private NetworkPrefabRef bulletPrefab;
    [SerializeField] private Transform firePoint;

    [SerializeField] private float fireDistance = 20f;
    [SerializeField] private LayerMask hitMask;

    [Networked] private TickTimer FireCooldown { get; set; }
    [SerializeField] private float fireInterval = 0.2f;

    [SerializeField] private Animator animator;
    [Networked] private float MoveSpeedNet { get; set; }

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Networked] private int JumpTick { get; set; }
    private int _lastRenderedJumpTick = -1;


    [Networked] private float VerticalVelocity { get; set; }
    [Networked] private NetworkBool IsGroundedNet { get; set; }
    [Networked] private NetworkBool JumpTriggeredNet { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private int _lastJumpVisualTick = -1;

    [SerializeField] private GameObject CameraRoot;
    private Transform cameraTransform;
    private Transform cameraRootTransform;
    private Camera localCamera;

    public static float LocalCameraYaw { get; private set; }

    [Header("Camera")]
    [SerializeField] private Vector3 cameraFollowOffset = new Vector3(0, 1.5f, 0f);
    [SerializeField] private float cameraSenesitivity = 3.0f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    private float cameraYaw;
    private float cameraPitch = 15f;

    [Header("Pickup")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float pickupDistance = 3.0f;
    [SerializeField] private float dropForce = 2.0f;
    [SerializeField] private LayerMask pickupMask;

    [Networked] private NetworkObject HeldBox {  get; set; }

    public Vector3 HoldPointPosition =>
        holdPoint != null ? holdPoint.position : transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;

    public override void Spawned()
    {
        if (CameraRoot == null) return;

        bool isMine = Object.HasInputAuthority;
        CameraRoot.SetActive(isMine);

        if (isMine)
        {
            cameraRootTransform = CameraRoot.transform;

            localCamera = CameraRoot.GetComponentInChildren<Camera>(true);
            if(localCamera != null)
            {
                cameraTransform = localCamera.transform;
            }

            cameraYaw = transform.eulerAngles.y;
            cameraPitch = 15;
            LocalCameraYaw = cameraYaw;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<FusionBootStrap.NetworkInputData>(out var inputData))
        {
            Quaternion camYawRotation = Quaternion.Euler(0.0f, inputData.cameraYaw, 0.0f);

            Vector3 forward = camYawRotation * Vector3.forward;
            Vector3 right = camYawRotation * Vector3.right;

            Vector3 move = forward * inputData.move.y + right * inputData.move.x;

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            MoveSpeedNet = move.magnitude;

            bool grounded = Physics.CheckSphere(
                groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.9f,
                groundCheckRadius,
                groundMask
            );

            IsGroundedNet = grounded;

            if(grounded && VerticalVelocity < 0.0f)
            {
                VerticalVelocity = 0f;
            }

            if (grounded && inputData.buttons.WasPressed(PreviousButtons, (int)FusionBootStrap.InputButton.Jump))
            {
                VerticalVelocity = jumpForce;
                IsGroundedNet = false;
                grounded = false;
                JumpTick = Runner.Tick;
            }

            VerticalVelocity += gravity * Runner.DeltaTime;

            Vector3 horizontalMove = new Vector3(move.x * moveSpeed, 0f, move.z * moveSpeed);
            transform.position += horizontalMove * Runner.DeltaTime;

            if(!(grounded && VerticalVelocity <= 0f))
            {
                Vector3 verticalMove = new Vector3(0f, VerticalVelocity, 0f);
                transform.position += verticalMove * Runner.DeltaTime;
            }

            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Runner.DeltaTime
                );
            }
        }

        if (inputData.buttons.WasPressed(PreviousButtons, (int)FusionBootStrap.InputButton.Pickup))
        {
            if (!TryDropHeldBox())
                TryPickupBox();
        }

        if (inputData.buttons.IsSet((int)FusionBootStrap.InputButton.Fire))
        {
            if (FireCooldown.ExpiredOrNotRunning(Runner))
            {
                FireLagCompensated();
                FireCooldown = TickTimer.CreateFromSeconds(Runner, fireInterval);
            }

        }

        PreviousButtons = inputData.buttons;
    }

    private void Fire()
    {
        if (!Object.HasStateAuthority)
            return;

        Vector3 spawnPos = firePoint != null
        ? firePoint.position
            : transform.position + transform.forward + Vector3.up * 0.5f;
        Quaternion spawnRot = transform.rotation;

        NetworkObject bulletObj = Runner.Spawn(
            bulletPrefab,
            spawnPos,
            spawnRot,
            Object.InputAuthority
        );

        SampleBullet bullet = bulletObj.GetComponent<SampleBullet>();
        if (bullet != null)
        {
            bullet.Init(Object.InputAuthority);
        }
    }
    private void FireLagCompensated()
    {
        if (!Object.HasStateAuthority)
            return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 0.5f;
        Vector3 direction = transform.forward;

        if (Runner.LagCompensation.Raycast(
            origin,
            direction,
            fireDistance,
            Object.InputAuthority,
            out LagCompensatedHit hit,
            hitMask
            ))
        {
            Debug.Log($"LagComp Hit : {hit.Hitbox.name}");
            RPC_PlayHitEffect(hit.Point, hit.Normal);

            Hitbox hitbox = hit.Hitbox;
            if (hitbox != null)
            {
                HealthTarget target = hitbox.GetComponentInParent<HealthTarget>();
                if (target != null)
                {
                    target.TakeDamage(1);
                }
            }
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHitEffect(Vector3 pos, Vector3 normal)
    {
        if (EffectManager.Instance == null)
            return;
        EffectManager.Instance.PlayerWorldEffect(EffectManager.Instance.HitEffect, pos, normal);
    }

    public override void Render()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", MoveSpeedNet);
        animator.SetBool("Grounded", !IsGroundedNet);
        animator.SetBool("Jump", !IsGroundedNet && VerticalVelocity > 0.1f);
        animator.SetBool("FreeFall", !IsGroundedNet && VerticalVelocity <= 0.1f);
        animator.SetFloat("MotionSpeed", 3f);
    }

    public void LateUpdate()
    {
        if (!Object || !Object.HasInputAuthority || CameraRoot == null || cameraTransform == null) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        cameraYaw += mouseX * cameraSenesitivity;
        cameraPitch -= mouseY * cameraSenesitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);

        CameraRoot.transform.localRotation = Quaternion.Euler(0.0f, cameraYaw - transform.eulerAngles.y, 0f);

        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0.0f, 0.0f);

        LocalCameraYaw = cameraTransform.eulerAngles.y;
    }

    void TryPickupBox()
    {
        if (!Object.HasStateAuthority) return;

        if (HeldBox != null) return;

        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        Debug.DrawRay(origin, direction * pickupDistance, Color.red, 3f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, pickupDistance, pickupMask))
        {
            PickableBox box = hit.collider.GetComponentInChildren<PickableBox>();
            if (box == null) return;

            box.Pickup(Object.InputAuthority);
            HeldBox = box.Object;
        }
    }

    private bool TryDropHeldBox()
    {
        if (!Object.HasStateAuthority)
            return false;

        if( HeldBox == null) 
            return false;

        PickableBox box = HeldBox.GetComponent<PickableBox>();
        if (box == null) return false;

        box.Drop(transform.forward * dropForce);
        HeldBox = null;
        return true;
    }
}