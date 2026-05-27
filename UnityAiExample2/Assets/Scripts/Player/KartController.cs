using UnityEngine;
using UnityEngine.InputSystem;

public class KartController : MonoBehaviour
{
    [Header("Suspension Settings")]
    [SerializeField] private float suspensionRestLength = 0.6f;
    [SerializeField] private float springStrength = 35000f;
    [SerializeField] private float springDamper = 1500f;
    [SerializeField] private float wheelRadius = 0.3f;
    [SerializeField] private int suspensionSubsteps = 4;
    [SerializeField] private int suspensionSamples = 3;
    [SerializeField] private float sampleRadius = 0.15f;
    [SerializeField] private float forceSmoothing = 0.5f;
[SerializeField] private Transform[] wheelAnchors; // FL, FR, RL, RR

    private float[] prevSuspensionForces = new float[4];

    [Header("Physics Settings")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float acceleration = 8000f;
    [SerializeField] private float maxSpeed = 50f;
    [SerializeField] private float steeringStrength = 5000f;
    [SerializeField] private float steeringDamping = 10f;
    [SerializeField] private float lateralFriction = 0.95f; // 0 to 1 for VelocityChange
    [SerializeField] private float gravityMultiplier = 2f;
    [SerializeField] private float downforce = 15000f;
    [SerializeField] private float steeringDownforce = 5000f;
    [SerializeField] private float antiRollForce = 5000f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0);

    [Header("Wheel Visuals")]
[SerializeField] private Transform[] wheelVisuals; // FL, FR, RL, RR
    [SerializeField] private float maxSteerVisualAngle = 30f;

    [Header("Body Visuals")]
    [SerializeField] private Transform visuals;
    [SerializeField] private float visualTiltAngle = 5f;
    [SerializeField] private float visualTiltSpeed = 5f;

    private float moveInput;
    private float steerInput;
    private InputAction moveAction;
    private InputAction steerAction;
    private float[] suspensionOffsets = new float[4];
    private float currentSpinAngle;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        moveAction = InputSystem.actions.FindAction("Accelerate");
        steerAction = InputSystem.actions.FindAction("Steer");

        rb.centerOfMass = centerOfMassOffset;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<float>();
        steerInput = steerAction.ReadValue<float>();

        UpdateWheelVisuals();
        ApplyVisualTilt();
    }

    void FixedUpdate()
    {
        bool isGrounded = false;
        for (int i = 0; i < wheelAnchors.Length; i++)
        {
            if (ApplySuspension(i))
            {
                isGrounded = true;
                ApplyDrivingForces(wheelAnchors[i]);
            }
        }

        // Apply Anti-Roll Bar
        ApplyAntiRollBar(0, 1); // Front
        ApplyAntiRollBar(2, 3); // Rear

        float speed = rb.linearVelocity.magnitude;

        // 2. Extra Gravity and Downforce
        if (!isGrounded)
        {
            rb.AddForce(Vector3.down * Physics.gravity.magnitude * gravityMultiplier, ForceMode.Acceleration);
        }
        else
        {
            // Base downforce
            float finalDownforce = downforce;
            
            // Extra downforce when steering to keep wheels planted
            finalDownforce += Mathf.Abs(steerInput) * steeringDownforce;
            
            rb.AddForce(-transform.up * finalDownforce * (speed / maxSpeed), ForceMode.Force);
        }

        ApplySteeringAndFriction();
    }

    private void ApplyAntiRollBar(int indexL, int indexR)
    {
        // Calculate the difference in compression between left and right wheels
        float travelL = suspensionOffsets[indexL] / suspensionRestLength;
        float travelR = suspensionOffsets[indexR] / suspensionRestLength;

        float antiRollForceAmount = (travelL - travelR) * antiRollForce;

        if (suspensionOffsets[indexL] > 0)
            rb.AddForceAtPosition(transform.up * -antiRollForceAmount, wheelAnchors[indexL].position);
        if (suspensionOffsets[indexR] > 0)
            rb.AddForceAtPosition(transform.up * antiRollForceAmount, wheelAnchors[indexR].position);
    }

    private bool ApplySuspension(int index)
    {
        Transform anchor = wheelAnchors[index];
        float radius = Mathf.Max(0.01f, wheelRadius);
        float rayLength = suspensionRestLength + radius;
        Vector3 worldUp = transform.up;
        
        RaycastHit hit;
        if (Physics.Raycast(anchor.position, -worldUp, out hit, rayLength))
        {
            float compression = rayLength - hit.distance;
            suspensionOffsets[index] = compression; // Save for visual update
            
            // Substepping logic: Iteratively integrate the suspension force
            // to prevent high spring/damper values from causing instability.
            float totalSubstepForce = 0;
            
            // Corner mass approximation (assuming even distribution for suspension stability)
            float cornerMass = rb.mass / wheelAnchors.Length;
            float substepDt = Time.fixedDeltaTime / suspensionSubsteps;
            
            // Initial state for integration
            float currentCompression = compression;
            float currentVelocity = Vector3.Dot(rb.GetPointVelocity(anchor.position), worldUp);

            for (int i = 0; i < suspensionSubsteps; i++)
            {
                // Calculate force for this substep
                float springForce = currentCompression * springStrength;
                float dampingForce = currentVelocity * springDamper;
                float force = springForce - dampingForce;

                // Accumulate the fraction of force
                totalSubstepForce += force;

                // Update the state for the next substep (Semi-implicit Euler)
                float acceleration = force / cornerMass;
                currentVelocity += acceleration * substepDt;
                currentCompression -= currentVelocity * substepDt;
            }

            // Average the accumulated forces
            float integratedForce = totalSubstepForce / suspensionSubsteps;

            // Apply Low-Pass Filter (Force Smoothing)
            float finalForce = Mathf.Lerp(prevSuspensionForces[index], integratedForce, 1f - forceSmoothing);
            prevSuspensionForces[index] = finalForce;

            // Clamp to zero to ensure suspension only pushes up
            finalForce = Mathf.Max(0, finalForce);
            
            rb.AddForceAtPosition(worldUp * finalForce, anchor.position, ForceMode.Force);
            return true;
        }
        
        suspensionOffsets[index] = 0;
        prevSuspensionForces[index] = 0;
        return false;
    }

    private void ApplyDrivingForces(Transform anchor)
    {
        if (rb.linearVelocity.magnitude > maxSpeed && moveInput > 0) return;
        rb.AddForceAtPosition(transform.forward * moveInput * acceleration, anchor.position);
    }

    private void ApplySteeringAndFriction()
    {
        float speed = rb.linearVelocity.magnitude;
        
        // 1. Steering
        if (speed > 1f)
        {
            float steerDir = Vector3.Dot(rb.linearVelocity, transform.forward) > 0 ? 1f : -1f;
            float speedRatio = maxSpeed > 0 ? Mathf.Clamp01(speed / (maxSpeed * 0.5f)) : 0; 
            
            // Apply steering torque
            if (Mathf.Abs(steerInput) > 0.01f)
            {
                rb.AddTorque(transform.up * steerInput * steeringStrength * steerDir * speedRatio, ForceMode.Force);
            }
            
            // Damping logic for snappier response
            Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
            
            // If no input, or if steering in the opposite direction of current spin, apply heavy damping
            bool isOpposing = steerInput != 0 && Mathf.Sign(steerInput * steerDir) != Mathf.Sign(localAngularVel.y);
            if (Mathf.Abs(steerInput) <= 0.01f || isOpposing)
            {
                float multiplier = isOpposing ? 2f : 1f; // Damp even faster if steering against the turn
                localAngularVel.y *= Mathf.Clamp01(1f - steeringDamping * multiplier * Time.fixedDeltaTime);
                rb.angularVelocity = transform.TransformDirection(localAngularVel);
            }
}
        else
        {
            // Kill angular velocity when stopped
            rb.angularVelocity = Vector3.zero;
        }

        // 2. Lateral Friction (Anti-drift)
        Vector3 lateralVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        float stableFriction = Mathf.Clamp01(lateralFriction);
        rb.AddForce(-lateralVel * stableFriction, ForceMode.VelocityChange);
    }

    private void UpdateWheelVisuals()
    {
        if (wheelVisuals == null || wheelVisuals.Length != 4) return;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        
        // Safety check for radius to avoid division by zero (NaN)
        float radius = Mathf.Max(0.01f, wheelRadius);
        
        // Accumulate spin (negated so positive speed = forward roll for the cylinder)
        float deltaSpin = (forwardSpeed / radius) * Time.deltaTime * Mathf.Rad2Deg;
        if (!float.IsNaN(deltaSpin))
        {
            currentSpinAngle += deltaSpin;
            // Keep currentSpinAngle in reasonable range to avoid precision issues over time
            currentSpinAngle %= 360f;
        }

        for (int i = 0; i < 4; i++)
        {
            if (wheelVisuals[i] == null) continue;

            // 1. Suspension position (Local to Kart)
            // compression = (restLength + radius) - hitDistance
            // We want wheel center at: hitDistance - radius
            float hitDistance = (suspensionRestLength + radius) - suspensionOffsets[i];
            float currentLength = hitDistance - radius;
            
            wheelVisuals[i].localPosition = wheelAnchors[i].localPosition - Vector3.up * currentLength;

            // 2. Base rotation
            float steerAngle = (i < 2) ? steerInput * maxSteerVisualAngle : 0;
            
            // Spin: around the cylinder's height axis (local Y)
            Quaternion spinRot = Quaternion.Euler(0, -currentSpinAngle, 0);
            
            // Tilt: Make the cylinder lie on its side along the X axis
            Quaternion tiltRot = Quaternion.Euler(0, 0, 90f);
            
            // Steer: Rotate around the chassis up axis
            Quaternion steerRot = Quaternion.Euler(0, steerAngle, 0);
            
            wheelVisuals[i].localRotation = steerRot * tiltRot * spinRot;
        }
    }

    private void ApplyVisualTilt()
    {
        if (visuals == null) return;
        float targetTilt = -steerInput * visualTiltAngle;
        Quaternion targetRot = Quaternion.Euler(0, 0, targetTilt);
        visuals.localRotation = Quaternion.Slerp(visuals.localRotation, targetRot, Time.deltaTime * visualTiltSpeed);
    }
}
