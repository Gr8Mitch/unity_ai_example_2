using UnityEngine;

public class SmoothFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -10);
    [SerializeField] private float positionSmoothTime = 0.12f;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float lookAtHeight = 1.5f;

    private Vector3 currentPositionVelocity;
    private float currentYawVelocity;
    private float currentYaw;

    void Start()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Smooth the target yaw specifically
        // This prevents the camera from "snapping" or stuttering when the kart turns or vibrates
        float targetYaw = target.eulerAngles.y;
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref currentYawVelocity, rotationSmoothTime);

        // 2. Calculate position based on the smoothed yaw
        Quaternion smoothedRotation = Quaternion.Euler(0, currentYaw, 0);
        Vector3 targetPosition = target.position + (smoothedRotation * offset);

        // 3. Smoothly move to the target position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentPositionVelocity, positionSmoothTime);

        // 4. Always look at the kart slightly above center
        Vector3 lookAtTarget = target.position + Vector3.up * lookAtHeight;
        transform.LookAt(lookAtTarget);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) currentYaw = target.eulerAngles.y;
    }
}
