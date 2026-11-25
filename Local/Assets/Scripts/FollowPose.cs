using UnityEngine;

public sealed class FollowPose : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform targetToFollow;

    [Header("Follow Options")]
    public bool followPosition = true;
    public bool followRotation = true;

    [Header("Smoothness (0 = Instant)")]
    [Range(0, 20)]
    public float positionSmoothness = 0f;
    [Range(0, 20)]
    public float rotationSmoothness = 0f;

    private void LateUpdate()
    {
        if (targetToFollow == null) return;

        // Handle Position
        if (followPosition)
        {
            if (positionSmoothness <= 0)
                transform.position = new Vector3(targetToFollow.position.x, targetToFollow.position.y, targetToFollow.position.z); 
            else
                transform.position = Vector3.Lerp(transform.position, targetToFollow.position, Time.deltaTime * positionSmoothness);
        }

        // Handle Rotation
        if (followRotation)
        {
            if (rotationSmoothness <= 0)
                transform.rotation = targetToFollow.rotation;
            else
                transform.rotation = Quaternion.Slerp(transform.rotation, targetToFollow.rotation, Time.deltaTime * rotationSmoothness);
        }

        // Transform firstChild = transform.GetChild(0);

        // firstChild.GetComponent<MeshRenderer>().enabled = false;
    }
}