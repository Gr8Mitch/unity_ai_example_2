using UnityEngine;
using UnityEngine.InputSystem;

public class KartController : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float steeringSpeed = 100f;
    [SerializeField] private float gravityForce = 35f;
    [SerializeField] private float dragOnGround = 2.0f;
    [SerializeField] private float dragInAir = 0.5f;

    [Header("Visual Alignment")]
    [SerializeField] private Transform visuals;
    [SerializeField] private float alignmentSpeed = 15f;
    [SerializeField] private float groundCheckDistance = 0.85f;
    [SerializeField] private LayerMask groundLayer;

    private float moveInput;
    private float steerInput;
    private bool isGrounded;
    private float currentRotation;
    private Vector3 currentGroundNormal = Vector3.up;
    private Vector3 rawGroundNormal = Vector3.up;
    private float groundedBuffer = 0f;

    private InputAction moveAction;
    private InputAction steerAction;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        moveAction = InputSystem.actions.FindAction("Accelerate");
        steerAction = InputSystem.actions.FindAction("Steer");

        rb.linearDamping = dragOnGround;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<float>();
        steerInput = steerInput = steerAction.ReadValue<float>();

        currentRotation += steerInput * steeringSpeed * Time.deltaTime;
        
        AlignVisuals();
    }

    void FixedUpdate()
    {
        CheckGround();

        if (isGrounded || groundedBuffer > 0)
        {
            rb.linearDamping = dragOnGround;

            Quaternion targetRotation = Quaternion.Euler(0, currentRotation, 0);
            Vector3 projectedForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, rawGroundNormal);
            
            if (projectedForward != Vector3.zero)
            {
                float powerMult = isGrounded ? 1f : 0.6f;
                Vector3 forwardForce = projectedForward.normalized * moveInput * acceleration * powerMult;
                rb.AddForce(forwardForce, ForceMode.Acceleration);
            }
        }
        else
        {
            rb.linearDamping = dragInAir;
        }

        float currentGravity = isGrounded ? gravityForce : gravityForce * 1.5f;
        rb.AddForce(Vector3.down * currentGravity, ForceMode.Acceleration);

        if (groundedBuffer > 0) groundedBuffer -= Time.fixedDeltaTime;
    }

    private void CheckGround()
    {
        RaycastHit hit;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance, groundLayer);
        
        if (isGrounded)
        {
            rawGroundNormal = hit.normal;
            groundedBuffer = 0.15f;
        }
        else if (groundedBuffer <= 0)
        {
            rawGroundNormal = Vector3.up;
        }
    }

    private void AlignVisuals()
    {
        currentGroundNormal = Vector3.Slerp(currentGroundNormal, rawGroundNormal, 10f * Time.deltaTime);

        Quaternion targetRotation = Quaternion.Euler(0, currentRotation, 0);
        Vector3 projectedForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, currentGroundNormal);
        
        if (projectedForward != Vector3.zero)
        {
            Quaternion finalRotation = Quaternion.LookRotation(projectedForward, currentGroundNormal);
            visuals.rotation = Quaternion.Slerp(visuals.rotation, finalRotation, alignmentSpeed * Time.deltaTime);
        }
    }
}
