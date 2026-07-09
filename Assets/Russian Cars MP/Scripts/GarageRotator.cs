using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class GarageRotator : MonoBehaviour
{
    public float autoRotateSpeed = 15f; 
    public float dragSpeed = 0.2f;
    private bool isDragging = false;

    private void Update()
    {
        // 1. Ha nincs semmilyen mutatóeszköz (se egér, se érintő), csak forogjon magától
        if (Pointer.current == null)
        {
            transform.Rotate(Vector3.up, autoRotateSpeed * Time.deltaTime, Space.World);
            return;
        }

        // 2. Érintés / Kattintás kezdete (Ujj letétele)
        if (Pointer.current.press.wasPressedThisFrame)
        {
            // Ha UI gombra kattintunk, NE kezdje el forgatni a kocsit
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = false;
            }
            else
            {
                isDragging = true;
            }
        }

        // 3. Érintés / Kattintás vége (Ujj felemelése)
        if (Pointer.current.press.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        // 4. Forgatás végrehajtása
        if (isDragging)
        {
            float dragDelta = Pointer.current.delta.x.ReadValue();
            transform.Rotate(Vector3.up, -dragDelta * dragSpeed, Space.World);
        }
        else
        {
            transform.Rotate(Vector3.up, autoRotateSpeed * Time.deltaTime, Space.World);
        }
    }
}