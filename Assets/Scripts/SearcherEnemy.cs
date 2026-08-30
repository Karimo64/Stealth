using UnityEngine;

// A patrolling enemy: walks in a straight line between waypoints. At each
// waypoint it stops, looks forward for a bit, then smoothly turns left or
// right (as set per-waypoint in the Inspector) and walks to the next one,
// facing the direction it just turned to. Detection + vision cone come
// from EnemyBase, same as CameraEnemy.
public class SearcherEnemy : EnemyBase
{
    public enum TurnDirection { None, Left, Right }

    [System.Serializable]
    public class PatrolPoint
    {
        public Transform point;

        [Tooltip("Which way to turn here (after waiting) before walking to the NEXT point in the list")]
        public TurnDirection turnAfterWait = TurnDirection.None;
    }

    [Header("Patrol")]
    [SerializeField] private PatrolPoint[] patrolPoints;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitDuration = 1.5f;
    [SerializeField] private float turnSpeed = 180f; // degrees per second while turning

    private enum State { Moving, Waiting, Turning }
    private State state;
    private int targetIndex;
    private float waitTimer;
    private Quaternion turnTargetRotation;

    protected override void Awake()
    {
        base.Awake(); // sets up the vision cone (from EnemyBase)

        if (patrolPoints.Length > 0)
            transform.position = patrolPoints[0].point.position;

        // Start walking toward the second point (index 1), if there is one.
        targetIndex = patrolPoints.Length > 1 ? 1 : 0;
        state = State.Moving;
    }

    protected override void Update()
    {
        base.Update(); // still runs detection + draws the cone every frame

        if (patrolPoints.Length < 2) return; // needs at least 2 points to patrol

        switch (state)
        {
            case State.Moving:
                Transform target = patrolPoints[targetIndex].point;
                transform.position = Vector2.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);

                if (Vector2.Distance(transform.position, target.position) < 0.05f)
                {
                    state = State.Waiting;
                    waitTimer = waitDuration;
                }
                break;

            case State.Waiting: // stands still, keeps looking forward
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                    BeginTurn(patrolPoints[targetIndex].turnAfterWait);
                break;

            case State.Turning:
                transform.rotation = Quaternion.RotateTowards(transform.rotation, turnTargetRotation, turnSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, turnTargetRotation) < 0.5f)
                {
                    transform.rotation = turnTargetRotation;
                    AdvanceToNextPoint();
                }
                break;
        }
    }

    // Kicks off a smooth rotation, or skips straight to moving if no turn is needed.
    private void BeginTurn(TurnDirection turn)
    {
        if (turn == TurnDirection.None)
        {
            AdvanceToNextPoint();
            return;
        }

        float delta = turn == TurnDirection.Left ? 90f : -90f;
        turnTargetRotation = transform.rotation * Quaternion.Euler(0f, 0f, delta);
        state = State.Turning;
    }

    private void AdvanceToNextPoint()
    {
        targetIndex = (targetIndex + 1) % patrolPoints.Length; // loop back to point 0 after the last one
        state = State.Moving;
    }
}
