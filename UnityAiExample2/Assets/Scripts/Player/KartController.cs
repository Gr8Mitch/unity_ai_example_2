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
    [SerializeField] private AnimationCurve accelerationCurve = AnimationCurve.Linear(0, 1, 1, 0.5f);
    [SerializeField] private float maxSpeed = 50f;
    [SerializeField] private float steeringStrength = 1000f;
    [SerializeField] private AnimationCurve steeringRadiusCurve = AnimationCurve.Linear(0, 5, 50, 40);
    [SerializeField] private float steeringDamping = 10f;
    [SerializeField] private float lateralFriction = 0.95f; // Used for force calculation
    [SerializeField] private float alignmentStrength = 5000f;
    [SerializeField] private float alignmentDamping = 100f;
    [SerializeField] private float gravityMultiplier = 2f;
    [SerializeField] private float downforce = 5000f; // Reduced from 15000
    [SerializeField] private float steeringDownforce = 2000f; // Reduced from 5000
    [SerializeField] private float antiRollForce = 5000f;
    [SerializeField] private float stickyForce = 4000f; // Reduced from 8000
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0);

    private Vector3 averageNormal = Vector3.up;
    private int groundedWheelsCount = 0;

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
        float speed = rb.linearVelocity.magnitude;
        bool isGrounded = false;
        
        averageNormal = Vector3.zero;
        groundedWheelsCount = 0;

        for (int i = 0; i < wheelAnchors.Length; i++)
        {
            if (ApplySuspension(i))
            {
                isGrounded = true;
                ApplyDrivingForces(wheelAnchors[i], speed);
            }
        }

        // Apply Anti-Roll Bar
        ApplyAntiRollBar(0, 1); // Front
        ApplyAntiRollBar(2, 3); // Rear

        // 2. Extra Gravity and Sticky Force
        if (isGrounded && groundedWheelsCount > 0)
        {
            averageNormal /= groundedWheelsCount;
            averageNormal.Normalize();

            // Blend world gravity with surface-normal aligned gravity
            Vector3 worldGravity = Vector3.down * Physics.gravity.magnitude;
            Vector3 surfaceGravity = -averageNormal * Physics.gravity.magnitude;
            
            // The steeper the hill, the more we prefer surface gravity to prevent sliding
            float slopeAngle = Vector3.Angle(Vector3.up, averageNormal);
            float gravityBlend = Mathf.Clamp01(slopeAngle / 45f);
            Vector3 blendedGravity = Vector3.Lerp(worldGravity, surfaceGravity, gravityBlend);

            rb.AddForce(blendedGravity * (gravityMultiplier - 1f), ForceMode.Acceleration);

            // Apply Sticky Force (always pushing into the surface normal)
            float speedFactor = maxSpeed > 0 ? Mathf.Clamp01(speed / maxSpeed) : 0;
            float finalStickyForce = stickyForce + (downforce * speedFactor);
            finalStickyForce += Mathf.Abs(steerInput) * steeringDownforce;
            
            rb.AddForce(-averageNormal * finalStickyForce, ForceMode.Force);

            // 3. Surface Alignment Torque (User's request to stick more without downward forces)
            Vector3 currentUp = transform.up;
            Vector3 axis = Vector3.Cross(currentUp, averageNormal);
            float angle = Vector3.Angle(currentUp, averageNormal);
            
            // Apply alignment torque
            rb.AddTorque(axis * angle * alignmentStrength, ForceMode.Force);
            
            // Apply damping to the alignment (prevent oscillation)
            Vector3 angularVel = rb.angularVelocity;
            rb.AddTorque(-angularVel * alignmentDamping, ForceMode.Force);
        }
else
        {
            // Just apply extra world gravity when in air
            rb.AddForce(Vector3.down * Physics.gravity.magnitude * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }

        ApplySteeringAndFriction();
    }

    private void ApplyAntiRollBar(int indexL, int indexR)
    {
        // Calculate the difference in compression between left and right wheels
        float travelL = suspensionOffsets[indexL] / suspensionRestLength;
        float travelR = suspensionOffsets[indexR] / suspensionRestLength;

        float antiRollForceAmount = (travelL - travelR) * antiRollForce;

        // If Left is more compressed (travelL > travelR), antiRollForceAmount is positive.
        // We should push the Left side UP and the Right side DOWN to equalize.
        if (suspensionOffsets[indexL] > 0)
            rb.AddForceAtPosition(transform.up * antiRollForceAmount, wheelAnchors[indexL].position);
        if (suspensionOffsets[indexR] > 0)
            rb.AddForceAtPosition(transform.up * -antiRollForceAmount, wheelAnchors[indexR].position);
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

    private void ApplyDrivingForces(Transform anchor, float speed)
    {
        if (speed > maxSpeed && moveInput > 0) return;
        
        float speedRatio = maxSpeed > 0 ? Mathf.Clamp01(speed / maxSpeed) : 0;
        float curveAccel = accelerationCurve.Evaluate(speedRatio);
        
        rb.AddForceAtPosition(transform.forward * moveInput * acceleration * curveAccel, anchor.position, ForceMode.Force);
    }

    private void ApplySteeringAndFriction()
    {
        float speed = rb.linearVelocity.magnitude;
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
        
        // 1. Steering
        if (speed > 0.1f)
        {
            float steerDir = Vector3.Dot(rb.linearVelocity, transform.forward) > 0 ? 1f : -1f;
            
            // Calculate target angular velocity based on steering radius from velocity
            float radius = steeringRadiusCurve.Evaluate(speed);
            radius = Mathf.Max(radius, 0.1f); // Avoid division by zero
            
            float targetAngularVel = (speed / radius) * steerInput * steerDir;

            if (Mathf.Abs(steerInput) > 0.01f)
            {
                // Apply torque to reach target angular velocity
                float angularVelError = targetAngularVel - localAngularVel.y;
                rb.AddTorque(transform.up * angularVelError * steeringStrength, ForceMode.Force);
            }

            // If no input, or if steering in the opposite direction of current spin, apply heavy damping
            bool isOpposing = steerInput != 0 && Mathf.Sign(steerInput * steerDir) != Mathf.Sign(localAngularVel.y);
            if (Mathf.Abs(steerInput) <= 0.01f || isOpposing)
            {
                float multiplier = isOpposing ? 2f : 1f;
                float dampForce = localAngularVel.y * steeringDamping * multiplier * rb.mass;
                rb.AddTorque(-transform.up * dampForce, ForceMode.Force);
            }
        }
        else
        {
            // Damp angular velocity when stopped (instead of setting to zero)
            rb.AddTorque(-rb.angularVelocity * steeringDamping * rb.mass, ForceMode.Force);
        }

        // 2. Lateral Friction (Anti-drift)
        Vector3 lateralVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        float stableFriction = Mathf.Clamp01(lateralFriction);
        // Use Force with mass-based scaling to simulate the friction without direct velocity change
        rb.AddForce(-lateralVel * stableFriction * (rb.mass / Time.fixedDeltaTime), ForceMode.Force);
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
