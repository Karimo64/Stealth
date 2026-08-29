using UnityEngine;

// Goes on the Main Camera. Makes it smoothly follow a target (the Player).
public class CameraFollow : MonoBehaviour
{
    // Drag the Player object here in the Inspector.
    [SerializeField] private Transform target;

    // Lower = snappier, higher = more lag/smoothing.
    [SerializeField] private float smoothTime = 0.15f;

    // Keep Z at -10 so the camera stays behind everything in 2D.
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private Vector3 velocity; // used internally by SmoothDamp, ignore this

    // LateUpdate runs after every Update() this frame, so the camera moves
    // AFTER the player has already moved — avoids jittery/lagging visuals.
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }
}
