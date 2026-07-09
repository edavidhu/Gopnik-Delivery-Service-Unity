using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    [Tooltip("Milyen magasan legyen a kamera a város felett?")]
    public float cameraHeight = 100f; 
    
    [Tooltip("Forogjon a térkép az autóval együtt?")]
    public bool rotateWithCar = false;

    private Transform playerTransform;

    private void LateUpdate()
    {
        // 1. Megkeressük a játékost, ha még nincs meg
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            return;
        }

        // 2. Pontosan a játékos fölé tesszük a kamerát a megadott magasságban
        Vector3 newPos = playerTransform.position;
        newPos.y = cameraHeight;
        transform.position = newPos;

        // 3. Opcionális: A térkép forgatása az autó orra felé
        if (rotateWithCar)
        {
            transform.rotation = Quaternion.Euler(90f, playerTransform.eulerAngles.y, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Mindig Észak felé néz
        }
    }
}