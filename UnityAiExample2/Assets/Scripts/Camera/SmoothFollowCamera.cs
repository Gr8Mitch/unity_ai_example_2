using UnityEngine;

public class SmoothFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -10);
    [SerializeField] private float positionSmoothTime = 0.2f;
    [SerializeField] private float rotationSmoothTime = 0.1f;

    private Vector3 currentVelocity;
    private float currentRotationVelocity;

    void LateUpdate()
    {
        if (target == null) return;

        // Position
        Vector3 targetPosition = target.TransformPoint(offset);
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, positionSmoothTime);

        // Rotation
        Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
