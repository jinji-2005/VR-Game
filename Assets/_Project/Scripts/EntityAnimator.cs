using System.Collections.Generic;
using UnityEngine;

public class EntityAnimator : MonoBehaviour
{
    [Header("Breathing")]
    [SerializeField] private float breatheSpeed = 1.2f;
    [SerializeField] private float breatheAmplitude = 0.03f;

    [Header("Wobble")]
    [SerializeField] private float wobbleSpeed = 0.8f;
    [SerializeField] private float wobbleAmplitude = 0.015f;

    [Header("Float")]
    [SerializeField] private float floatSpeed = 0.5f;
    [SerializeField] private float floatAmplitude = 0.05f;

    [Header("Chase Multiplier")]
    [SerializeField] private float chaseMultiplier = 2.5f;

    [Header("Locomotion")]
    [SerializeField] private float moveResponse = 8f;
    [SerializeField] private float walkCycleSpeed = 9.5f;
    [SerializeField] private float moveBobAmplitude = 0.08f;
    [SerializeField] private float forwardLeanAngle = 11f;
    [SerializeField] private float movementActivationThreshold = 0.035f;
    [SerializeField] private float minimumVisibleMoveBlend = 0.42f;
    [SerializeField] private float bodyMotionBoost = 1.8f;
    [SerializeField] private float legMotionBoost = 1.85f;

    [Header("Leg Motion")]
    [SerializeField] private float legSwingAngle = 22f;
    [SerializeField] private float legTwistAngle = 10f;
    [SerializeField] private string[] leftLegNames = { "CableLeftLeg" };
    [SerializeField] private string[] rightLegNames = { "CableRightLeg1", "CableRightLeg2" };

    [Header("Body Targets")]
    [SerializeField] private string[] bodyTargetNames = { "CableTorso1", "CableTorso2", "CableHead", "CableShoulder", "CablesShoulder", "Legs" };

    private BacteriaController controller;
    [SerializeField] private Transform animationTarget;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private float animationBlend = 1f;
    private float locomotionBlend;
    private float walkCycle;
    private Vector3 lastRootPosition;
    private Transform[] bodyTargets = new Transform[0];
    private Vector3[] bodyBasePositions = new Vector3[0];
    private Quaternion[] bodyBaseRotations = new Quaternion[0];
    private Transform[] leftLegs = new Transform[0];
    private Transform[] rightLegs = new Transform[0];
    private Quaternion[] leftLegBaseRotations = new Quaternion[0];
    private Quaternion[] rightLegBaseRotations = new Quaternion[0];

    private void Awake()
    {
        controller = GetComponentInParent<BacteriaController>();
        if (animationTarget == null)
            animationTarget = ResolveAnimationTarget();

        if (animationTarget == null)
            animationTarget = transform;

        basePosition = animationTarget.localPosition;
        baseRotation = animationTarget.localRotation;
        baseScale = animationTarget.localScale;
        lastRootPosition = transform.position;

        CacheLegTargets();
    }

    private void Update()
    {
        if (animationTarget == null)
            return;

        bool isChasing = IsChasing();
        float speedMult = isChasing ? chaseMultiplier : 1f;
        float move01 = GetMovementBlend();
        locomotionBlend = Mathf.Lerp(locomotionBlend, move01, Time.deltaTime * moveResponse);
        float visibleMoveBlend = GetVisibleMoveBlend(locomotionBlend);
        animationBlend = Mathf.Lerp(animationBlend, isChasing ? 0.18f : 1f, Time.deltaTime * 6f);
        walkCycle += Time.deltaTime * walkCycleSpeed * Mathf.Lerp(0.8f, 1.45f, visibleMoveBlend) * speedMult;

        float t = Time.time;
        float breathe = Mathf.Sin(t * breatheSpeed * speedMult) * breatheAmplitude * animationBlend;
        float wobbleX = Mathf.Sin(t * wobbleSpeed * speedMult) * wobbleAmplitude * Mathf.Lerp(1f, 0.45f, visibleMoveBlend);
        float wobbleZ = Mathf.Cos(t * wobbleSpeed * 1.3f * speedMult) * wobbleAmplitude * Mathf.Lerp(1f, 0.5f, visibleMoveBlend);
        float floatY = Mathf.Abs(Mathf.Sin(t * floatSpeed * speedMult)) * floatAmplitude * animationBlend;
        float moveBob = Mathf.Abs(Mathf.Sin(walkCycle)) * moveBobAmplitude * visibleMoveBlend * bodyMotionBoost;

        bool canAnimateTransform = animationTarget != transform || transform.parent != null;
        bool canAnimatePosition = canAnimateTransform;
        if (canAnimatePosition)
            animationTarget.localPosition = basePosition + new Vector3(wobbleX, breathe + floatY + moveBob, wobbleZ);

        if (canAnimateTransform)
        {
            float tiltAngle = Mathf.Sin(t * wobbleSpeed * 0.7f * speedMult) * 3f;
            float chaseLean = isChasing ? 8f : 0f;
            float moveLean = forwardLeanAngle * visibleMoveBlend;
            float roll = tiltAngle + Mathf.Sin(walkCycle * 0.5f) * 4.5f * visibleMoveBlend;
            animationTarget.localRotation = baseRotation * Quaternion.Euler(chaseLean + moveLean + tiltAngle * 0.35f, 0f, roll);
        }

        float pulse = 1f + breathe * 0.12f;
        animationTarget.localScale = new Vector3(baseScale.x * pulse, baseScale.y * (1f + breathe * 0.08f), baseScale.z * pulse);

        ApplyBodyMotion(speedMult, breathe, moveBob, isChasing, visibleMoveBlend);
        ApplyLegMotion(leftLegs, leftLegBaseRotations, 0f, visibleMoveBlend);
        ApplyLegMotion(rightLegs, rightLegBaseRotations, Mathf.PI, visibleMoveBlend);
    }

    private bool IsChasing()
    {
        return controller != null && controller.currentState == BacteriaController.State.Chase;
    }

    private Transform ResolveAnimationTarget()
    {
        Transform preferredTarget = FindFirstNamedChild(transform, bodyTargetNames);
        if (preferredTarget != null)
            return preferredTarget;

        Transform bestChild = null;
        int bestScore = -1;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            int rendererCount = child.GetComponentsInChildren<Renderer>(true).Length;
            if (rendererCount > bestScore)
            {
                bestScore = rendererCount;
                bestChild = child;
            }
        }

        if (bestChild != null)
            return bestChild;

        return transform;
    }

    private float GetMovementBlend()
    {
        float configuredMaxSpeed = controller != null ? controller.MaxConfiguredSpeed : 6f;
        float commandedSpeed = controller != null ? controller.CurrentMoveSpeed : 0f;

        Vector3 delta = transform.position - lastRootPosition;
        delta.y = 0f;
        float measuredSpeed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        lastRootPosition = transform.position;

        float blendedSpeed = Mathf.Max(measuredSpeed, commandedSpeed * 0.85f);
        return Mathf.Clamp01(blendedSpeed / Mathf.Max(configuredMaxSpeed, 0.01f));
    }

    private float GetVisibleMoveBlend(float rawMoveBlend)
    {
        if (rawMoveBlend <= movementActivationThreshold)
            return 0f;

        return Mathf.Lerp(minimumVisibleMoveBlend, 1f, rawMoveBlend);
    }

    private void CacheLegTargets()
    {
        Transform searchRoot = controller != null ? controller.transform : transform;
        bodyTargets = FindNamedChildren(searchRoot, bodyTargetNames);
        bodyBasePositions = CaptureLocalPositions(bodyTargets);
        bodyBaseRotations = CaptureLocalRotations(bodyTargets);
        leftLegs = FindNamedChildren(searchRoot, leftLegNames);
        rightLegs = FindNamedChildren(searchRoot, rightLegNames);
        leftLegBaseRotations = CaptureLocalRotations(leftLegs);
        rightLegBaseRotations = CaptureLocalRotations(rightLegs);

        Debug.Log(
            $"[EntityAnimator] Bound bodyTargets={bodyTargets.Length}, leftLegs={leftLegs.Length}, rightLegs={rightLegs.Length} on {name}",
            this);
    }

    private Transform FindFirstNamedChild(Transform searchRoot, string[] desiredNames)
    {
        if (searchRoot == null || desiredNames == null || desiredNames.Length == 0)
            return null;

        Transform[] allChildren = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int desiredIndex = 0; desiredIndex < desiredNames.Length; desiredIndex++)
        {
            string desiredName = desiredNames[desiredIndex];
            for (int childIndex = 0; childIndex < allChildren.Length; childIndex++)
            {
                Transform candidate = allChildren[childIndex];
                if (string.Equals(candidate.name, desiredName, System.StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        return null;
    }

    private Transform[] FindNamedChildren(Transform searchRoot, string[] desiredNames)
    {
        if (searchRoot == null || desiredNames == null || desiredNames.Length == 0)
            return new Transform[0];

        Transform[] allChildren = searchRoot.GetComponentsInChildren<Transform>(true);
        List<Transform> matches = new List<Transform>();

        for (int i = 0; i < desiredNames.Length; i++)
        {
            string desiredName = desiredNames[i];
            for (int childIndex = 0; childIndex < allChildren.Length; childIndex++)
            {
                Transform candidate = allChildren[childIndex];
                if (!string.Equals(candidate.name, desiredName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                matches.Add(candidate);
                break;
            }
        }

        return matches.ToArray();
    }

    private Quaternion[] CaptureLocalRotations(Transform[] targets)
    {
        Quaternion[] rotations = new Quaternion[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            rotations[i] = targets[i].localRotation;

        return rotations;
    }

    private Vector3[] CaptureLocalPositions(Transform[] targets)
    {
        Vector3[] positions = new Vector3[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            positions[i] = targets[i].localPosition;

        return positions;
    }

    private void ApplyBodyMotion(float speedMult, float breathe, float moveBob, bool isChasing, float visibleMoveBlend)
    {
        if (bodyTargets == null || bodyBasePositions == null || bodyBaseRotations == null)
            return;

        for (int i = 0; i < bodyTargets.Length; i++)
        {
            Transform target = bodyTargets[i];
            if (target == null)
                continue;

            float phase = walkCycle + i * 0.45f;
            float swayX = Mathf.Sin(phase) * 0.045f * visibleMoveBlend * bodyMotionBoost;
            float swayY = moveBob * (0.65f + i * 0.06f) + breathe * 0.25f;
            float swayZ = Mathf.Cos(phase * 0.5f) * 0.03f * visibleMoveBlend * bodyMotionBoost;
            float pitch = Mathf.Sin(phase) * 11f * visibleMoveBlend + (isChasing ? 5f : 0f);
            float roll = Mathf.Cos(phase) * 9f * visibleMoveBlend;

            target.localPosition = bodyBasePositions[i] + new Vector3(swayX, swayY, swayZ);
            target.localRotation = bodyBaseRotations[i] * Quaternion.Euler(pitch, 0f, roll);
        }
    }

    private void ApplyLegMotion(Transform[] legs, Quaternion[] baseRotations, float phaseOffset, float visibleMoveBlend)
    {
        if (legs == null || baseRotations == null)
            return;

        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == null)
                continue;

            float phase = walkCycle + phaseOffset + i * 0.35f;
            float swing = Mathf.Sin(phase) * legSwingAngle * visibleMoveBlend * legMotionBoost;
            float twist = Mathf.Cos(phase) * legTwistAngle * visibleMoveBlend * legMotionBoost;
            legs[i].localRotation = baseRotations[i] * Quaternion.Euler(swing, 0f, twist);
        }
    }
}
