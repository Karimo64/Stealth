using UnityEngine;

// A stationary "camera" enemy: doesn't move, just rotates between 4 fixed
// directions, pausing at each one. Detection + vision cone come from
// EnemyBase — this class only adds the rotation behavior.
public class CameraEnemy : EnemyBase
{
    [Header("Rotation Behavior")]
    [SerializeField] private float pauseDuration = 2f;   // seconds to hold each direction
    [SerializeField] private float rotationSpeed = 90f;  // degrees per second while turning

    // The 4 cardinal facing angles (90 degrees apart).
    private static readonly float[] directions = { 0f, 90f, 180f, 270f };

    private int currentIndex = 0;
    private Quaternion targetRotation;
    private bool rotating = false;
    private float pauseTimer;

    protected override void Awake()
    {
        base.Awake(); // sets up the vision cone mesh (from EnemyBase)

        transform.rotation = Quaternion.Euler(0f, 0f, directions[currentIndex]);
        targetRotation = transform.rotation;
        pauseTimer = pauseDuration;
    }

    protected override void Update()
    {
        base.Update(); // still runs detection + draws the cone every frame

        if (rotating)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                rotating = false;
                pauseTimer = pauseDuration;
            }
        }
        else
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                currentIndex = (currentIndex + 1) % directions.Length;
                targetRotation = Quaternion.Euler(0f, 0f, directions[currentIndex]);
                rotating = true;
            }
        }
    }
}
