using UnityEngine;

public class CarDeformation : MonoBehaviour
{
    [Header("Melyik részt akarjuk törni?")]
    public MeshFilter carBodyMeshFilter;

    [Header("Törésfizika Beállítások")]
    public float damageRadius = 1.5f;
    public float maxDeformation = 0.2f;
    public float impactMultiplier = 1.0f; 
    public float minImpactForce = 2.0f;

    private Mesh deformedMesh;
    private Vector3[] modifiedVertices; 
    private Vector3[] originalVertices; // ÚJ: Itt tároljuk a gyári állapotot!

    private SimpleCarController carController; // ÚJ: Hivatkozás az autóra

    void Start()
    {
        if (carBodyMeshFilter == null) return;

        deformedMesh = carBodyMeshFilter.mesh;
        
        if (!deformedMesh.isReadable)
        {
            Debug.LogError("HIBA: Read/Write nincs bekapcsolva!");
            return;
        }

        modifiedVertices = deformedMesh.vertices; 
        originalVertices = deformedMesh.vertices; // Lementjük a sértetlen formát

        carController = GetComponent<SimpleCarController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;

        if (impactForce >= minImpactForce)
        {
            Vector3 contactPoint = collision.contacts[0].point;
            Vector3 hitNormal = collision.contacts[0].normal; 
            
            DeformMesh(contactPoint, hitNormal, impactForce);

            // ÚJ: Szólunk a kocsinak, hogy sérültünk! (Az erő alapján vonunk le HP-t)
            if (carController != null)
            {
                carController.TakeDamage(impactForce * 2f); 
            }
        }
    }

    void DeformMesh(Vector3 worldContactPoint, Vector3 hitNormal, float force)
    {
        Vector3 localContactPoint = carBodyMeshFilter.transform.InverseTransformPoint(worldContactPoint);
        Vector3 localDeformDirection = carBodyMeshFilter.transform.InverseTransformDirection(hitNormal).normalized;

        int deformedVerticesCount = 0;
        float localDamageRadius = damageRadius / carBodyMeshFilter.transform.lossyScale.x;
        float localMaxDeformation = maxDeformation / carBodyMeshFilter.transform.lossyScale.x;

        for (int i = 0; i < modifiedVertices.Length; i++)
        {
            float distance = Vector3.Distance(modifiedVertices[i], localContactPoint);
            if (distance < localDamageRadius)
            {
                float falloff = 1.0f - (distance / localDamageRadius);
                float damageScale = falloff * falloff * force * impactMultiplier;
                damageScale = Mathf.Clamp(damageScale, 0f, localMaxDeformation);

                modifiedVertices[i] += localDeformDirection * damageScale;
                deformedVerticesCount++;
            }
        }

        if (deformedVerticesCount > 0)
        {
            deformedMesh.vertices = modifiedVertices;
            deformedMesh.RecalculateNormals(); 
            deformedMesh.RecalculateBounds();
        }
    }

    // ÚJ: Ez a funkció "kivasalja" a kocsit!
    public void RepairMesh()
    {
        if (carBodyMeshFilter == null || originalVertices == null) return;
        
        modifiedVertices = (Vector3[])originalVertices.Clone(); // Visszaállítjuk az eredetit
        deformedMesh.vertices = modifiedVertices;
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();
    }
}