using UnityEngine;

public class BacteriaActivator : MonoBehaviour
{
    [SerializeField] private BacteriaController[] bacteriaToActivate;
    [SerializeField] private bool activateOnce = true;

    private bool hasActivated;

    private void OnTriggerEnter(Collider other)
    {
        if (activateOnce && hasActivated)
            return;

        if (!other.CompareTag("Player"))
            return;

        hasActivated = true;

        foreach (BacteriaController bacteria in bacteriaToActivate)
        {
            if (bacteria != null)
                bacteria.Activate();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box)
                Gizmos.DrawCube(box.center, box.size);
            else if (col is SphereCollider sphere)
                Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
    }
}
