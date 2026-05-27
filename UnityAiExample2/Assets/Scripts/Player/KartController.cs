using UnityEngine;
using UnityEngine.InputSystem;

public class KartController : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float steeringSpeed = 100f;
    [SerializeField] private float gravityForce = 40f;
    [SerializeField] private float dragOnGround = 3f;
    [SerializeField] private float dragInAir = 0.5f;

    [Header("Visual Alignment")]
    [SerializeField] private Transform visuals;
    [SerializeField] private float alignmentSpeed = 10f;
    [SerializeField] private float groundCheckDistance = 0.6f;
    [SerializeField] private LayerMask groundLayer;

    private float moveInput;
    private float steerInput;
    private bool isGrounded;
    private float currentRotation;

    private InputAction moveAction;
    private InputAction steerAction;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        // Setup input from project-wide actions
        moveAction = InputSystem.actions.FindAction("Accelerate");
        steerAction = InputSystem.actions.FindAction("Steer");

        rb.linearDamping = dragOnGround; // Set initial drag
    }

    void Update()
    {
        // Read input
        moveInput = moveAction.ReadValue<float>();
        steerInput = steerAction.ReadValue<float>();

        // Handle steering (only rotate if moving or if you want arcade zero-speed turn)
        currentRotation += steerInput * steeringSpeed * Time.deltaTime;
        
        // Update visual position to match sphere (visuals should be child but we can force it)
        visuals.position = rb.position;

        AlignVisuals();
    }

    void FixedUpdate()
    {
        CheckGround();

        if (isGrounded)
        {
            rb.linearDamping = dragOnGround;

            // Apply forward force based on current rotation
            Vector3 forwardForce = visuals.forward * moveInput * acceleration;
            rb.AddForce(forwardForce, ForceMode.Acceleration);
        }
        else
        {
            rb.linearDamping = dragInAir;
            // Apply extra gravity when in air for snappier landing
            rb.AddForce(Vector3.down * gravityForce, ForceMode.Acceleration);
        }
    }

    private void CheckGround()
    {
        RaycastHit hit;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance, groundLayer);
    }

    private void AlignVisuals()
    {
        RaycastHit hit;
        Vector3 groundNormal = Vector3.up;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance + 1f, groundLayer))
        {
            groundNormal = hit.normal;
        }

        // Target rotation based on steer and ground normal
        Quaternion targetRotation = Quaternion.Euler(0, currentRotation, 0);
        
        // Align forward with ground normal
        Vector3 projectedForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, groundNormal);
        Quaternion finalRotation = Quaternion.LookRotation(projectedForward, groundNormal);

        visuals.rotation = Quaternion.Slerp(visuals.rotation, finalRotation, alignmentSpeed * Time.deltaTime);
    }
}
