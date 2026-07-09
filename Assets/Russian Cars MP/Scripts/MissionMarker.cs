using UnityEngine;

public class MissionMarker : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Ha a Játékos (Te) belemész a körbe
        if (other.transform.root.CompareTag("Player"))
        {
            DeliveryManager.Instance.OnMarkerReached();
        }
    }
}