using UnityEngine;

// Base class for any enemy that can "see" the player with a vision cone.
// CameraEnemy and SearcherEnemy both inherit from this — this class only
// handles: detecting the player, drawing the yellow cone, and broadcasting
// a shared "alert" event whenever ANY enemy spots the player. Movement (or
// not moving) is added by the subclasses.
public class EnemyBase : MonoBehaviour
{
    // Fired the moment ANY enemy first spots the player, carrying the
    // player's position at that instant. SearcherEnemy listens to this to
    // know when to break off patrol and go investigate. CameraEnemy simply
    // never subscribes to it, so it has no effect on cameras.
    public static event System.Action<Vector2> PlayerSpotted;

    [Header("Vision Settings")]
    [SerializeField] protected float viewRadius = 3f;
    [SerializeField] protected float viewAngle = 65f;

    [Header("Layers")]
    [SerializeField] protected LayerMask obstacleMask;  // set to "Walls"
    [SerializeField] protected LayerMask playerMask;    // set to "Player"
    [SerializeField] protected LayerMask safeZoneMask;  // set to "SafeZones"

    [Header("Cone Colors")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 0f, 0.35f);   // yellow, semi-transparent
    [SerializeField] private Color detectedColor = new Color(1f, 0f, 0f, 0.45f); // red, semi-transparent

    protected bool playerDetected;
    protected Transform detectedPlayer;
    private bool wasDetectedLastFrame; // used to only fire the alert once per detection, not every frame

    private const int rayCount = 30; // how many slices make up the cone mesh (higher = smoother)
    private Mesh viewMesh;
    private MeshRenderer meshRenderer;

    protected virtual void Awake()
    {
        // Create a child object to hold the cone's MeshFilter/MeshRenderer.
        GameObject visionCone = new GameObject("Vision Cone");
        visionCone.transform.SetParent(transform, false); // sits exactly on top of the enemy

        viewMesh = new Mesh { name = "Vision Cone Mesh" };
        visionCone.AddComponent<MeshFilter>().mesh = viewMesh;

        meshRenderer = visionCone.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material.color = normalColor;
    }

    protected virtual void Update()
    {
        DetectPlayer();
        DrawVisionCone();
        meshRenderer.material.color = playerDetected ? detectedColor : normalColor;
    }

    // Checks whether the player is within range, within the cone angle,
    // NOT hidden behind a wall, and NOT standing in a safe zone. Fires
    // PlayerSpotted the moment detection starts (not every frame it stays true).
    protected virtual void DetectPlayer()
    {
        playerDetected = false;
        detectedPlayer = null;

        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, viewRadius, playerMask);
        if (playerCollider != null && !IsInSafeZone(playerCollider.transform.position))
        {
            Vector2 dirToPlayer = (playerCollider.transform.position - transform.position).normalized;

            // transform.up is treated as "forward" — the direction the enemy is facing.
            if (Vector2.Angle(transform.up, dirToPlayer) < viewAngle / 2f)
            {
                float distToPlayer = Vector2.Distance(transform.position, playerCollider.transform.position);

                // If a wall is hit before reaching the player, they're hidden — not detected.
                if (!Physics2D.Raycast(transform.position, dirToPlayer, distToPlayer, obstacleMask))
                {
                    playerDetected = true;
                    detectedPlayer = playerCollider.transform;
                }
            }
        }

        // Rising edge: only fire the moment detection STARTS, not every frame.
        if (playerDetected && !wasDetectedLastFrame)
            PlayerSpotted?.Invoke(detectedPlayer.position);

        wasDetectedLastFrame = playerDetected;
    }

    // The player is invisible while standing in a safe zone. This is asked
    // fresh every frame rather than stored, so stepping in or out takes effect
    // immediately — even if the enemy was already looking right at them.
    // Uses the player's center point: they're hidden as soon as it's on a
    // safe-zone tile, without needing to fit the whole sprite inside.
    protected bool IsInSafeZone(Vector2 position)
    {
        return Physics2D.OverlapPoint(position, safeZoneMask) != null;
    }

    // Builds a fan-shaped mesh representing the vision cone. Each "slice" is
    // cast as a ray so the cone visually stops at walls too.
    private void DrawVisionCone()
    {
        float angleStep = viewAngle / rayCount;
        Vector3[] vertices = new Vector3[rayCount + 2];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = Vector3.zero; // cone origin, in local space

        for (int i = 0; i <= rayCount; i++)
        {
            float angleOffset = -viewAngle / 2f + angleStep * i;
            Vector2 dir = DirectionFromAngle(angleOffset);

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, viewRadius, obstacleMask);
            Vector3 point = hit.collider != null
                ? transform.InverseTransformPoint(hit.point)
                : transform.InverseTransformPoint((Vector2)transform.position + dir * viewRadius);

            vertices[i + 1] = point;

            if (i < rayCount)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
    }

    // Rotates transform.up by angleOffset degrees to get a ray direction.
    private Vector2 DirectionFromAngle(float angleOffset)
    {
        float baseAngle = Mathf.Atan2(transform.up.y, transform.up.x) * Mathf.Rad2Deg;
        float finalAngle = (baseAngle + angleOffset) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
    }
}
