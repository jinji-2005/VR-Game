using UnityEngine;

public class BacteriaController : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Lost, Investigate }

    [Header("Detection")]
    [SerializeField] private float detectRange = 15f;
    [SerializeField] private float loseRange = 25f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private float eyeHeight = 1.25f;
    [SerializeField] private float hearingRange = 8f;
    [SerializeField] private float maxHearingVerticalDifference = 2.2f;
    [SerializeField] private float frontDetectionRange = 9.5f;
    [SerializeField, Range(0f, 180f)] private float frontDetectionAngle = 145f;
    [SerializeField, Range(0.1f, 1f)] private float rearDetectionMultiplier = 0.55f;
    [SerializeField] private float directChaseDistance = 2.2f;
    [SerializeField] private float frontalHearingBonus = 2f;
    [SerializeField, Range(0f, 1f)] private float hearingChaseThreshold = 0.72f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5.5f;
    [SerializeField] private float investigateSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float stoppingDistance = 1.4f;
    [SerializeField] private float patrolWaitTime = 3f;
    [SerializeField] private float[] patrolPositions = new float[4];

    [Header("Room Bounds")]
    [SerializeField] private float patrolRadius = 4.5f;
    [SerializeField] private float chaseRadius = 5.5f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float collisionProbeRadius = 0.3f;
    [SerializeField] private float collisionProbeDistance = 0.8f;
    [SerializeField] private bool dimNearbyLights = true;
    [SerializeField] private float nearbyLightDimRadius = 7f;
    [SerializeField] private float nearbyLightIntensityMultiplier = 0.35f;

    [Header("Chase Memory")]
    [SerializeField] private float chaseMemoryDuration = 4.5f;
    [SerializeField] private float lostSearchDuration = 3f;

    [Header("Patrol Recovery")]
    [SerializeField] private float avoidanceCommitTime = 0.45f;
    [SerializeField] private float stuckTimeout = 0.6f;
    [SerializeField] private float minimumPatrolTargetDistance = 1f;

    [Header("State")]
    public State currentState = State.Idle;
    [SerializeField] private GameObject playerTarget;
    [SerializeField] private bool disableImportedPlaneMeshes = true;

    private int currentPatrolIndex;
    private float patrolWaitTimer;
    private Vector3 patrolOrigin;
    private float lostTimer;
    private float currentMoveSpeed;
    private float nextPlayerSearchTime;
    private float investigateTimer;
    private Vector3 investigateTarget;
    private PlayerController playerController;
    private Light[] sceneLights;
    private float chaseMemoryTimer;
    private Vector3 lastKnownPlayerPosition;
    private bool hasLastKnownPlayerPosition;
    private Vector3 currentPatrolTarget;
    private bool hasPatrolTarget;
    private Vector3 avoidanceLockDirection;
    private float avoidanceLockTimer;
    private float stuckTimer;
    private float patrolAngleOffset;

    public float CurrentMoveSpeed => currentMoveSpeed;
    public float MaxConfiguredSpeed => Mathf.Max(patrolSpeed, chaseSpeed, investigateSpeed, 0.01f);

    private void Awake()
    {
        patrolOrigin = transform.position;
        patrolAngleOffset = Random.Range(0f, Mathf.PI * 2f);
        if (obstacleMask.value == 0)
            obstacleMask = Physics.DefaultRaycastLayers;

        DisableImportedHelpers();
        DimNearbySceneLights();
        CachePlayerReference(forceSearch: true);
    }

    private void Start()
    {
        EnterState(State.Idle);
    }

    private void Update()
    {
        CachePlayerReference();
        UpdateSoundInvestigation();

        switch (currentState)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Lost:
                UpdateLost();
                break;
            case State.Investigate:
                UpdateInvestigate();
                break;
        }
    }

    private void CachePlayerReference(bool forceSearch = false)
    {
        if (playerTarget != null)
            return;

        if (!forceSearch && Time.time < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.time + 0.5f;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (taggedPlayer != null)
        {
            playerTarget = taggedPlayer;
            return;
        }

        if (playerLayer.value == 0)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, loseRange, playerLayer);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag(playerTag))
            {
                playerTarget = hit.gameObject;
                return;
            }
        }
    }

    private void EnterState(State newState)
    {
        currentState = newState;
        ResetMovementRecovery();

        switch (newState)
        {
            case State.Idle:
                patrolWaitTimer = patrolWaitTime;
                currentMoveSpeed = 0f;
                hasPatrolTarget = false;
                break;
            case State.Patrol:
                if (patrolPositions != null && patrolPositions.Length > 0)
                    currentPatrolIndex = Random.Range(0, patrolPositions.Length);
                SelectNextPatrolTarget();
                break;
            case State.Chase:
                RememberPlayerPosition(GetPlayerPositionOrFallback(transform.position));
                break;
            case State.Lost:
                lostTimer = lostSearchDuration;
                break;
            case State.Investigate:
                investigateTimer = 4f;
                break;
        }
    }

    private void UpdateIdle()
    {
        if (CanImmediatelyChasePlayer() || CanDetectPlayer(detectRange, requireSight: true))
        {
            EnterState(State.Chase);
            return;
        }

        patrolWaitTimer -= Time.deltaTime;
        if (patrolWaitTimer <= 0f)
            EnterState(State.Patrol);
    }

    private void UpdatePatrol()
    {
        if (CanImmediatelyChasePlayer() || CanDetectPlayer(detectRange, requireSight: true))
        {
            EnterState(State.Chase);
            return;
        }

        if (!hasPatrolTarget)
            SelectNextPatrolTarget();

        if (MoveTowards(currentPatrolTarget, patrolSpeed, 0.5f, patrolRadius))
            return;

        SelectNextPatrolTarget();
    }

    private void UpdateChase()
    {
        if (!HasPlayerReference() && !hasLastKnownPlayerPosition)
        {
            EnterState(State.Lost);
            return;
        }

        bool canTrackPlayer = CanDetectPlayer(loseRange, requireSight: false);
        if (!canTrackPlayer)
        {
            chaseMemoryTimer -= Time.deltaTime;
            if (chaseMemoryTimer <= 0f || !hasLastKnownPlayerPosition)
            {
                EnterState(State.Lost);
                return;
            }
        }

        Vector3 chaseTarget = canTrackPlayer ? playerTarget.transform.position : lastKnownPlayerPosition;
        float chaseStopDistance = Mathf.Max(0.35f, stoppingDistance * 0.7f);
        if (MoveTowards(chaseTarget, chaseSpeed, chaseStopDistance, GetAlertLeashRadius()))
            return;

        FaceTowards(chaseTarget);

        if (!canTrackPlayer && IsWithinPlanarDistance(chaseTarget, chaseStopDistance + 0.15f))
            EnterState(State.Lost);
    }

    private void UpdateLost()
    {
        lostTimer -= Time.deltaTime;

        if (CanImmediatelyChasePlayer() || CanDetectPlayer(detectRange, requireSight: true))
        {
            EnterState(State.Chase);
            return;
        }

        if (lostTimer <= 0f)
        {
            EnterState(State.Patrol);
            return;
        }

        Vector3 searchTarget = hasLastKnownPlayerPosition ? lastKnownPlayerPosition : patrolOrigin;
        bool moved = MoveTowards(searchTarget, patrolSpeed * 0.8f, 0.25f, GetAlertLeashRadius());

        if (!moved && IsWithinPlanarDistance(searchTarget, 0.35f))
            EnterState(State.Patrol);
    }

    private void UpdateInvestigate()
    {
        if (CanImmediatelyChasePlayer() || CanDetectPlayer(detectRange, requireSight: true))
        {
            EnterState(State.Chase);
            return;
        }

        investigateTimer -= Time.deltaTime;
        bool moved = MoveTowards(investigateTarget, investigateSpeed, 0.45f, GetAlertLeashRadius());

        if (!moved || investigateTimer <= 0f)
            EnterState(State.Patrol);
    }

    private bool HasPlayerReference()
    {
        if (playerTarget == null)
            return false;

        if (!playerTarget.activeInHierarchy)
        {
            playerTarget = null;
            return false;
        }

        return true;
    }

    private void UpdateSoundInvestigation()
    {
        if (currentState == State.Chase)
            return;

        if (!CanHearPlayerMovement(out Vector3 soundTarget, out float heardStrength))
            return;

        if (CanImmediatelyChasePlayer() || heardStrength >= hearingChaseThreshold)
        {
            RememberPlayerPosition(soundTarget);
            EnterState(State.Chase);
            return;
        }

        bool shouldRefreshTarget =
            currentState != State.Investigate ||
            Vector3.Distance(investigateTarget, soundTarget) > 0.75f;

        if (!shouldRefreshTarget)
            return;

        investigateTarget = ClampToPatrolBounds(soundTarget, GetAlertLeashRadius());
        EnterState(State.Investigate);
    }

    private bool CanDetectPlayer(float range, bool requireSight)
    {
        if (!HasPlayerReference())
            return false;

        Vector3 toPlayer = playerTarget.transform.position - transform.position;
        toPlayer.y = 0f;
        float planarDistance = toPlayer.magnitude;
        if (planarDistance <= 0.001f)
        {
            RememberPlayerPosition(playerTarget.transform.position);
            return true;
        }

        if (!IsWithinRadiusFromOrigin(playerTarget.transform.position, GetAlertLeashRadius()))
            return false;

        if (requireSight && requireLineOfSight && !HasLineOfSightToPlayer())
            return false;

        float effectiveRange = requireSight
            ? GetEffectiveSightRange(range, toPlayer / planarDistance)
            : Mathf.Max(range, frontDetectionRange);

        if (planarDistance > effectiveRange)
            return false;

        RememberPlayerPosition(playerTarget.transform.position);
        return true;
    }

    private bool MoveTowards(Vector3 targetPosition, float targetSpeed, float stopDistance, float leashRadius)
    {
        Vector3 planarTarget = targetPosition;
        planarTarget.y = transform.position.y;
        planarTarget = ClampToPatrolBounds(planarTarget, leashRadius);

        Vector3 dir = planarTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= stopDistance * stopDistance)
        {
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, acceleration * Time.deltaTime);
            ResetMovementRecovery();
            return false;
        }

        currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, acceleration * Time.deltaTime);

        Vector3 moveDir = ResolveMoveDirection(dir.normalized, leashRadius);
        if (moveDir.sqrMagnitude < 0.0001f)
        {
            currentMoveSpeed = 0f;
            return false;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + moveDir * currentMoveSpeed * Time.deltaTime;
        nextPosition = ClampToPatrolBounds(nextPosition, leashRadius);
        nextPosition.y = patrolOrigin.y;
        transform.position = nextPosition;

        float expectedStep = currentMoveSpeed * Time.deltaTime;
        float movedStep = Vector3.Distance(previousPosition, transform.position);
        if (expectedStep > 0.01f && movedStep < expectedStep * 0.35f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeout)
            {
                currentMoveSpeed = 0f;
                ResetMovementRecovery();
                return false;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        return true;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (!HasPlayerReference())
            return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 target = playerTarget.transform.position + Vector3.up * 0.9f;
        Vector3 dir = target - origin;
        float distance = dir.magnitude;

        if (distance <= 0.001f)
            return true;

        if (Physics.SphereCast(origin, collisionProbeRadius * 0.6f, dir.normalized, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.collider.CompareTag(playerTag);

        return true;
    }

    private bool CanImmediatelyChasePlayer()
    {
        if (!HasPlayerReference())
            return false;

        Vector3 toPlayer = playerTarget.transform.position - transform.position;
        toPlayer.y = 0f;
        float planarDistance = toPlayer.magnitude;
        if (planarDistance <= 0.001f)
            return true;

        if (!IsWithinRadiusFromOrigin(playerTarget.transform.position, GetAlertLeashRadius()))
            return false;

        if (requireLineOfSight && !HasLineOfSightToPlayer())
            return false;

        if (planarDistance <= directChaseDistance)
        {
            RememberPlayerPosition(playerTarget.transform.position);
            return true;
        }

        if (planarDistance > frontDetectionRange)
            return false;

        if (!IsInFront(toPlayer / planarDistance, frontDetectionAngle))
            return false;

        RememberPlayerPosition(playerTarget.transform.position);
        return true;
    }

    private bool CanHearPlayerMovement(out Vector3 soundTarget, out float heardStrength)
    {
        soundTarget = transform.position;
        heardStrength = 0f;

        if (!HasPlayerReference())
            return false;

        if (playerController == null && playerTarget != null)
            playerController = playerTarget.GetComponent<PlayerController>();

        if (playerController == null || !playerController.IsProducingFootstepNoise)
            return false;

        Vector3 playerPosition = playerTarget.transform.position;
        if (Mathf.Abs(playerPosition.y - patrolOrigin.y) > maxHearingVerticalDifference)
            return false;

        Vector3 planarOffset = playerPosition - transform.position;
        planarOffset.y = 0f;
        float planarDistance = planarOffset.magnitude;
        if (planarDistance <= 0.001f)
        {
            soundTarget = playerPosition;
            heardStrength = 1f;
            return true;
        }

        Vector3 directionToPlayer = planarOffset / planarDistance;
        float baseNoiseStrength = Mathf.Clamp01(playerController.MovementNoiseStrength);
        float effectiveHearingRange = hearingRange * Mathf.Lerp(0.7f, 1.35f, baseNoiseStrength);
        if (IsInFront(directionToPlayer, frontDetectionAngle + 15f))
            effectiveHearingRange += frontalHearingBonus;

        if (planarDistance > effectiveHearingRange)
            return false;

        soundTarget = playerPosition;
        float distanceFactor = 1f - Mathf.Clamp01(planarDistance / Mathf.Max(effectiveHearingRange, 0.01f));
        heardStrength = Mathf.Clamp01(baseNoiseStrength * 0.7f + distanceFactor * 0.6f);
        return true;
    }

    private void DisableImportedHelpers()
    {
        foreach (Light lightComponent in GetComponentsInChildren<Light>(true))
            lightComponent.enabled = false;

        foreach (Camera cameraComponent in GetComponentsInChildren<Camera>(true))
            cameraComponent.enabled = false;

        foreach (AudioListener audioListener in GetComponentsInChildren<AudioListener>(true))
            audioListener.enabled = false;

        if (!disableImportedPlaneMeshes)
            return;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform)
                continue;

            bool suspiciousByName =
                child.name.StartsWith("Plane") ||
                child.name.Contains("Quad");

            bool suspiciousByMeshName = false;
            if (child.TryGetComponent(out MeshFilter meshFilter) &&
                meshFilter.sharedMesh != null &&
                !string.IsNullOrEmpty(meshFilter.sharedMesh.name))
            {
                suspiciousByMeshName =
                    meshFilter.sharedMesh.name.StartsWith("Plane") ||
                    meshFilter.sharedMesh.name.Contains("Quad");
            }

            if (!suspiciousByName && !suspiciousByMeshName)
                continue;

            child.gameObject.SetActive(false);
        }
    }

    private void DimNearbySceneLights()
    {
        if (!dimNearbyLights)
            return;

        sceneLights ??= FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float radiusSqr = nearbyLightDimRadius * nearbyLightDimRadius;

        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight == null || !sceneLight.enabled)
                continue;

            if (sceneLight.transform.IsChildOf(transform))
                continue;

            Vector3 offset = sceneLight.transform.position - patrolOrigin;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            sceneLight.intensity *= Mathf.Clamp01(nearbyLightIntensityMultiplier);
        }
    }

    private Vector3 ResolveMoveDirection(Vector3 desiredDirection, float leashRadius)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        if (avoidanceLockTimer > 0f)
        {
            avoidanceLockTimer -= Time.deltaTime;
            if (!IsPathBlocked(avoidanceLockDirection, leashRadius))
                return avoidanceLockDirection;
        }

        if (!IsPathBlocked(desiredDirection, leashRadius))
        {
            stuckTimer = 0f;
            return desiredDirection;
        }

        Vector3 bestDirection = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        float[] candidateAngles = { 25f, -25f, 50f, -50f, 75f, -75f, 110f, -110f, 180f };

        for (int i = 0; i < candidateAngles.Length; i++)
        {
            Vector3 candidate = RotatePlanar(desiredDirection, candidateAngles[i]);
            if (IsPathBlocked(candidate, leashRadius))
                continue;

            float score = Vector3.Dot(desiredDirection, candidate);
            if (avoidanceLockDirection.sqrMagnitude > 0.0001f)
                score += Vector3.Dot(avoidanceLockDirection, candidate) * 0.2f;

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        if (bestDirection.sqrMagnitude > 0.0001f)
        {
            avoidanceLockDirection = bestDirection;
            avoidanceLockTimer = avoidanceCommitTime;
            return bestDirection;
        }

        stuckTimer += Time.deltaTime;
        return Vector3.zero;
    }

    private bool IsPathBlocked(Vector3 direction, float leashRadius)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return true;

        float probeDistance = Mathf.Max(collisionProbeDistance, currentMoveSpeed * Time.deltaTime + collisionProbeRadius);
        Vector3 origin = transform.position + Vector3.up * eyeHeight;

        if (Physics.SphereCast(origin, collisionProbeRadius, direction, out RaycastHit hit, probeDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag(playerTag))
                return true;
        }

        Vector3 projectedNext = transform.position + direction.normalized * probeDistance;
        return !IsWithinRadiusFromOrigin(projectedNext, leashRadius);
    }

    private bool IsWithinRadiusFromOrigin(Vector3 worldPosition, float radius)
    {
        Vector3 offset = worldPosition - patrolOrigin;
        offset.y = 0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    private float GetEffectiveSightRange(float baseRange, Vector3 directionToPlayer)
    {
        if (IsInFront(directionToPlayer, frontDetectionAngle))
            return Mathf.Max(baseRange, frontDetectionRange);

        return baseRange * rearDetectionMultiplier;
    }

    private float GetAlertLeashRadius()
    {
        return Mathf.Max(chaseRadius, frontDetectionRange, hearingRange + frontalHearingBonus, detectRange + 1f);
    }

    private bool IsInFront(Vector3 directionToPlayer, float viewAngleDegrees)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Angle(forward.normalized, directionToPlayer.normalized) <= viewAngleDegrees * 0.5f;
    }

    private Vector3 ClampToPatrolBounds(Vector3 worldPosition, float radius)
    {
        Vector3 offset = worldPosition - patrolOrigin;
        offset.y = 0f;

        if (offset.sqrMagnitude <= radius * radius)
            return new Vector3(worldPosition.x, patrolOrigin.y, worldPosition.z);

        Vector3 clamped = patrolOrigin + offset.normalized * radius;
        clamped.y = patrolOrigin.y;
        return clamped;
    }

    private void SelectNextPatrolTarget()
    {
        if (patrolPositions == null || patrolPositions.Length == 0)
        {
            currentPatrolTarget = patrolOrigin;
            hasPatrolTarget = true;
            return;
        }

        int attempts = Mathf.Max(patrolPositions.Length * 2, 6);
        float angleStep = Mathf.PI * 2f / Mathf.Max(6, patrolPositions.Length * 2);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            int patrolIndex = (currentPatrolIndex + attempt) % patrolPositions.Length;
            float range = Mathf.Max(0.75f, patrolPositions[patrolIndex]);
            float angle = patrolAngleOffset + attempt * angleStep;
            Vector3 candidate = patrolOrigin + new Vector3(Mathf.Cos(angle) * range, 0f, Mathf.Sin(angle) * range);
            candidate = ClampToPatrolBounds(candidate, patrolRadius);

            if (!IsPatrolTargetUsable(candidate))
                continue;

            currentPatrolTarget = candidate;
            currentPatrolIndex = (patrolIndex + 1) % patrolPositions.Length;
            patrolAngleOffset = Mathf.Repeat(angle + angleStep * 1.5f, Mathf.PI * 2f);
            hasPatrolTarget = true;
            return;
        }

        currentPatrolTarget = ClampToPatrolBounds(transform.position - transform.right * 0.75f, patrolRadius);
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPositions.Length;
        patrolAngleOffset = Mathf.Repeat(patrolAngleOffset + angleStep, Mathf.PI * 2f);
        hasPatrolTarget = true;
    }

    private bool IsPatrolTargetUsable(Vector3 candidate)
    {
        Vector3 planarOffset = candidate - transform.position;
        planarOffset.y = 0f;
        if (planarOffset.sqrMagnitude < minimumPatrolTargetDistance * minimumPatrolTargetDistance)
            return false;

        return HasClearPathTo(candidate, collisionProbeRadius * 0.9f);
    }

    private bool HasClearPathTo(Vector3 targetPosition, float probeRadius)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 target = new Vector3(targetPosition.x, patrolOrigin.y + eyeHeight, targetPosition.z);
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return true;

        if (Physics.SphereCast(origin, probeRadius, direction.normalized, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.collider.CompareTag(playerTag);

        return true;
    }

    private void RememberPlayerPosition(Vector3 playerPosition)
    {
        lastKnownPlayerPosition = ClampToPatrolBounds(playerPosition, GetAlertLeashRadius());
        chaseMemoryTimer = chaseMemoryDuration;
        hasLastKnownPlayerPosition = true;
    }

    private Vector3 GetPlayerPositionOrFallback(Vector3 fallbackPosition)
    {
        return playerTarget != null ? playerTarget.transform.position : fallbackPosition;
    }

    private void ResetMovementRecovery()
    {
        avoidanceLockDirection = Vector3.zero;
        avoidanceLockTimer = 0f;
        stuckTimer = 0f;
    }

    private void FaceTowards(Vector3 targetPosition)
    {
        Vector3 flatDirection = targetPosition - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private bool IsWithinPlanarDistance(Vector3 targetPosition, float distance)
    {
        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= distance * distance;
    }

    private static Vector3 RotatePlanar(Vector3 direction, float angleDegrees)
    {
        return (Quaternion.Euler(0f, angleDegrees, 0f) * direction).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? patrolOrigin : transform.position, patrolRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
        Gizmos.DrawWireSphere(Application.isPlaying ? patrolOrigin : transform.position, chaseRadius);
    }
}
