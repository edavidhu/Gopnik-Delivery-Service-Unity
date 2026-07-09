using UnityEngine;
using UnityEngine.InputSystem;

public class CarCameraFollow : MonoBehaviour
{
    [Header("Mit kövessen a kamera?")]
    public Transform target; 

    [Header("Kamera Nézetek (V gomb)")]
    public Vector3[] cameraOffsets = new Vector3[] {
        new Vector3(0, 2.5f, -6.5f), 
        new Vector3(0, 4.0f, -9.0f), 
        new Vector3(0, 1.0f, 1.5f),  
        new Vector3(0, 0.4f, 3.0f)   
    };
    
    private int currentViewIndex = 0;

    [Header("Kamera Simítás")]
    public float positionDamping = 10f;
    public float rotationDamping = 10f;

    [Header("Szabad Nézet (Egér - C gomb)")]
    public float mouseSensitivity = 0.5f;
    private bool isFreeLooking = false;
    private float currentMouseX;
    private float currentMouseY;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current == null || target == null) return;

        // SZABAD NÉZET BE/KI
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            isFreeLooking = !isFreeLooking;
            
            if (!isFreeLooking)
            {
                currentMouseX = transform.eulerAngles.y;
                currentMouseY = transform.eulerAngles.x;
            }
        }

        // KAMERA NÉZET VÁLTÁS
        if (Keyboard.current.vKey.wasPressedThisFrame && !isFreeLooking)
        {
            currentViewIndex++;
            if (currentViewIndex >= cameraOffsets.Length) currentViewIndex = 0; 
        }

        // --- ÚJ: EGÉR KISZABADÍTÁSA / ELREJTÉSE JOBB KLIKKEL! ---
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (Cursor.visible) // Ha látszott, elrejtjük
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else // Ha rejtve volt, kiszabadítjuk
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (isFreeLooking)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            currentMouseX += mouseDelta.x * mouseSensitivity;
            currentMouseY -= mouseDelta.y * mouseSensitivity;
            currentMouseY = Mathf.Clamp(currentMouseY, -20f, 60f);

            Quaternion desiredRotation = Quaternion.Euler(currentMouseY, currentMouseX, 0);
            Vector3 desiredPosition = target.position - (desiredRotation * Vector3.forward * 6f);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * 15f);
        }
        else
        {
            Vector3 currentOffset = cameraOffsets[currentViewIndex];

            Vector3 desiredPosition = target.position + (target.right * currentOffset.x) + (target.up * currentOffset.y) + (target.forward * currentOffset.z);
            Quaternion desiredRotation;

            if (currentOffset.z > 0)
            {
                desiredRotation = target.rotation; 
            }
            else 
            {
                Vector3 lookAtPoint = target.position + Vector3.up * 1.5f;
                Vector3 direction = lookAtPoint - desiredPosition;
                desiredRotation = Quaternion.LookRotation(direction);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * positionDamping);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationDamping);
        }
    }

    // MOBIL KAMERA GOMBHOZ
    public void MobileSwitchCamera()
    {
        currentViewIndex++;
        if (currentViewIndex >= cameraOffsets.Length) currentViewIndex = 0;
    }

}