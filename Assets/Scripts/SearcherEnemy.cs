using UnityEngine;

// A patrolling enemy: walks between waypoints, ALWAYS facing the direction it
// is walking. At each waypoint it stops, looks forward for a moment, then
// smoothly rotates until it's aiming at the next waypoint, and walks on.
// Detection + vision cone come from EnemyBase, same as CameraEnemy.
//
// It also listens for EnemyBase.PlayerSpotted: when ANY enemy spots the
// player, it drops its patrol and reacts. CameraEnemy never subscribes, so
// cameras are unaffected.
//
// Facing is never stored — it's always derived from where the enemy is
// heading. That's what keeps it from ending up crooked after a chase.
public class SearcherEnemy : EnemyBase
{
    public enum TurnDirection { None, Left, Right }

    [System.Serializable]
    public class PatrolPoint
    {
        public Transform point;

        [Tooltip("Which way to spin at this point. The ANGLE comes from the path itself — " +
                 "this only picks whether it rotates clockwise or counter-clockwise. " +
                 "None = take the shortest way around.")]
        public TurnDirection turnAfterWait = TurnDirection.None;
    }

    [Header("Patrol")]
    [SerializeField] private PatrolPoint[] patrolPoints;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitDuration = 1.5f;
    [SerializeField] private float turnSpeed = 180f; // degrees per second while turning

    private enum State { Moving, Waiting, Turning, Chasing, Returning }
    private State state;
    private int targetIndex;
    private float waitTimer;

    private float turnRemaining; // degrees still to rotate
    private float turnSign;      // +1 = counter-clockwise, -1 = clockwise

    private Vector2 investigateTarget;
    private Vector2 preInvestigatePosition; // spot to walk back to once it gives up chasing
    private bool hasSeenPlayerThisChase;    // true once THIS enemy's own cone has actually caught the player

    protected override void Awake()
    {
        base.Awake(); // sets up the vision cone (from EnemyBase)

        if (patrolPoints.Length > 0)
            transform.position = patrolPoints[0].point.position;

        // Start walking toward the second point (index 1), if there is one.
        targetIndex = patrolPoints.Length > 1 ? 1 : 0;
        state = State.Moving;
    }

    private void OnEnable()
    {
        PlayerSpotted += OnPlayerSpotted;
    }

    private void OnDisable()
    {
        PlayerSpotted -= OnPlayerSpotted;
    }

    // Called for every searcher whenever ANY enemy spots the player.
    // Interrupts the patrol (no matter what it was doing) and starts chasing.
    private void OnPlayerSpotted(Vector2 lastKnownPosition)
    {
        // Only remember the spot to come back to the first time — if it's already
        // chasing or heading home, don't overwrite it with where it is mid-chase.
        if (state != State.Chasing && state != State.Returning)
        {
            preInvestigatePosition = transform.position;
            hasSeenPlayerThisChase = false;
        }

        investigateTarget = lastKnownPosition;
        state = State.Chasing;
    }

    protected override void Update()
    {
        base.Update(); // still runs detection + draws the cone every frame

        switch (state)
        {
            case State.Moving:
                if (patrolPoints.Length < 2) break; // needs at least 2 points to patrol

                Vector2 target = patrolPoints[targetIndex].point.position;
                FaceDirection(target - (Vector2)transform.position); // always looks where it walks
                transform.position = Vector2.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

                if (Vector2.Distance(transform.position, target) < 0.05f)
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
                float step = Mathf.Min(turnSpeed * Time.deltaTime, turnRemaining);
                transform.Rotate(0f, 0f, turnSign * step);
                turnRemaining -= step;

                if (turnRemaining <= 0f)
                    AdvanceToNextPoint();
                break;

            case State.Chasing:
                // Still seeing the player with its OWN cone? Keep tracking their live position.
                if (playerDetected && detectedPlayer != null)
                {
                    investigateTarget = detectedPlayer.position;
                    hasSeenPlayerThisChase = true;
                }

                FaceDirection(investigateTarget - (Vector2)transform.position);
                transform.position = Vector2.MoveTowards(transform.position, investigateTarget, moveSpeed * Time.deltaTime);

                // Two ways this leg ends:
                // - It was actually watching the player and just lost sight -> turn back right away.
                // - It never saw the player itself (just reacting to another enemy's alert) ->
                //   walk all the way to the reported spot first, then give up.
                bool lostSight = hasSeenPlayerThisChase && !playerDetected;
                bool reachedReportedSpot = !hasSeenPlayerThisChase &&
                    Vector2.Distance(transform.position, investigateTarget) < 0.05f;

                if (lostSight || reachedReportedSpot)
                    state = State.Returning;
                break;

            case State.Returning:
                FaceDirection(preInvestigatePosition - (Vector2)transform.position); // looks the way it's walking back
                transform.position = Vector2.MoveTowards(transform.position, preInvestigatePosition, moveSpeed * Time.deltaTime);

                if (Vector2.Distance(transform.position, preInvestigatePosition) < 0.05f)
                    ResumePatrol();
                break;
        }
    }

    // Points the enemy along dir. Rotates ONLY on the Z axis — FromToRotation
    // would pick an arbitrary axis when dir points straight down and flip the
    // sprite out of the 2D plane.
    private void FaceDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return; // nothing meaningful to aim at

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // -90 because "forward" is up, not right
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // Starts rotating until the enemy is aiming at the NEXT waypoint. The amount
    // comes from the path; `turn` only decides which way around it spins.
    private void BeginTurn(TurnDirection turn)
    {
        int nextIndex = (targetIndex + 1) % patrolPoints.Length;
        Vector2 nextLeg = (Vector2)patrolPoints[nextIndex].point.position - (Vector2)transform.position;

        if (nextLeg.sqrMagnitude < 0.0001f)
        {
            AdvanceToNextPoint();
            return;
        }

        float targetAngle = Mathf.Atan2(nextLeg.y, nextLeg.x) * Mathf.Rad2Deg - 90f;
        float delta = Mathf.DeltaAngle(transform.eulerAngles.z, targetAngle); // shortest way, -180..180

        // Force the long way around when a specific direction was asked for.
        if (turn == TurnDirection.Left && delta < 0f) delta += 360f;
        else if (turn == TurnDirection.Right && delta > 0f) delta -= 360f;

        if (Mathf.Abs(delta) < 0.5f) // already aiming there (straight-through waypoint)
        {
            AdvanceToNextPoint();
            return;
        }

        turnRemaining = Mathf.Abs(delta);
        turnSign = Mathf.Sign(delta);
        state = State.Turning;
    }

    // Back on the path after a chase. No rotation to restore — State.Moving
    // aims it at the waypoint on the very next frame.
    private void ResumePatrol()
    {
        // Came back standing on the waypoint it was headed to? Take the next one
        // instead of re-doing that waypoint's wait and turn.
        if (patrolPoints.Length >= 2 &&
            Vector2.Distance(transform.position, patrolPoints[targetIndex].point.position) < 0.05f)
            targetIndex = (targetIndex + 1) % patrolPoints.Length;

        state = State.Moving;
    }

    private void AdvanceToNextPoint()
    {
        targetIndex = (targetIndex + 1) % patrolPoints.Length; // loop back to point 0 after the last one
        state = State.Moving;
    }
}
