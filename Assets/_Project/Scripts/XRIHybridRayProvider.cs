using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public sealed class XRIHybridRayProvider : MonoBehaviour, IAdvancedLineRenderable
{
    public Transform Origin { get; set; }
    public Transform IgnoreRoot { get; set; }
    public float Length { get; set; } = 10f;
    public int RaycastMask { get; set; } = ~0;
    public int InteractionMask { get; set; } = ~0;

    private bool hasHit;
    private bool hasValidTarget;
    private Vector3 hitPoint;
    private Vector3 hitNormal;

    public bool HasHit => hasHit;
    public bool HasValidTarget => hasValidTarget;
    public Vector3 HitPoint => hitPoint;
    public Vector3 HitNormal => hitNormal;

    public bool GetLinePoints(ref NativeArray<Vector3> linePoints, out int numPoints, Ray? rayOriginOverride = null)
    {
        GetLineOriginAndDirection(rayOriginOverride, out Vector3 origin, out Vector3 direction);
        float rayLength = Mathf.Max(0.1f, Length);
        Vector3 end = origin + direction * rayLength;

        if (TryGetFirstHit(origin, direction, rayLength, out RaycastHit hit))
        {
            hasHit = true;
            hasValidTarget = TryResolveInteractable(hit.collider, InteractionMask);
            hitPoint = hit.point;
            hitNormal = hit.normal;
            end = hit.point;
        }
        else
        {
            hasHit = false;
            hasValidTarget = false;
            hitPoint = end;
            hitNormal = -direction;
        }

        numPoints = 2;
        EnsureCapacity(ref linePoints, numPoints);
        linePoints[0] = origin;
        linePoints[1] = end;
        return true;
    }

    public bool GetLinePoints(ref Vector3[] linePoints, out int numPoints)
    {
        if (linePoints == null || linePoints.Length < 2)
            linePoints = new Vector3[2];

        GetLineOriginAndDirection(out Vector3 origin, out Vector3 direction);
        float rayLength = Mathf.Max(0.1f, Length);
        Vector3 end = origin + direction * rayLength;
        if (TryGetFirstHit(origin, direction, rayLength, out RaycastHit hit))
        {
            hasHit = true;
            hasValidTarget = TryResolveInteractable(hit.collider, InteractionMask);
            hitPoint = hit.point;
            hitNormal = hit.normal;
            end = hit.point;
        }
        else
        {
            hasHit = false;
            hasValidTarget = false;
            hitPoint = end;
            hitNormal = -direction;
        }

        linePoints[0] = origin;
        linePoints[1] = end;
        numPoints = 2;
        return true;
    }

    public bool TryGetHitInfo(out Vector3 position, out Vector3 normal, out int positionInLine, out bool isValidTarget)
    {
        position = hitPoint;
        normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal : Vector3.up;
        positionInLine = 1;
        isValidTarget = hasValidTarget;
        return hasHit;
    }

    public void GetLineOriginAndDirection(out Vector3 origin, out Vector3 direction)
    {
        GetLineOriginAndDirection(null, out origin, out direction);
    }

    private void GetLineOriginAndDirection(Ray? rayOriginOverride, out Vector3 origin, out Vector3 direction)
    {
        if (rayOriginOverride.HasValue)
        {
            Ray ray = rayOriginOverride.Value;
            origin = ray.origin;
            direction = ray.direction.sqrMagnitude > 0.0001f ? ray.direction.normalized : Vector3.forward;
            return;
        }

        Transform originTransform = Origin != null ? Origin : transform;
        origin = originTransform.position;
        direction = originTransform.forward.sqrMagnitude > 0.0001f ? originTransform.forward.normalized : Vector3.forward;
    }

    private bool TryGetFirstHit(Vector3 origin, Vector3 direction, float rayLength, out RaycastHit nearestHit)
    {
        int mask = RaycastMask == 0 ? ~0 : RaycastMask;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, rayLength, mask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (IgnoreRoot != null && hit.collider.transform.IsChildOf(IgnoreRoot))
                continue;

            nearestHit = hit;
            return true;
        }

        nearestHit = default;
        return false;
    }

    private static bool TryResolveInteractable(Collider hitCollider, int interactionMask)
    {
        if (hitCollider == null)
            return false;

        int mask = interactionMask == 0 ? ~0 : interactionMask;
        if (hitCollider.TryGetComponent(out IInteractable _) &&
            IsLayerInMask(hitCollider.gameObject.layer, mask))
        {
            return true;
        }

        foreach (MonoBehaviour behaviour in hitCollider.GetComponentsInParent<MonoBehaviour>())
        {
            if (behaviour is IInteractable &&
                IsLayerInMask(behaviour.gameObject.layer, mask))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLayerInMask(int layer, int mask)
    {
        return (mask & (1 << layer)) != 0;
    }

    private static void EnsureCapacity(ref NativeArray<Vector3> linePoints, int numPoints)
    {
        if (linePoints.IsCreated && linePoints.Length >= numPoints)
            return;

        if (linePoints.IsCreated)
            linePoints.Dispose();

        linePoints = new NativeArray<Vector3>(numPoints, Allocator.Persistent);
    }
}
