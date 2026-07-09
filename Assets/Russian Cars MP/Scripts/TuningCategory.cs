using UnityEngine;
using UnityEngine.UI;

public class TuningCategory : MonoBehaviour
{
    [Header("Beállítások")]
    public LayoutElement subPanel;   
    public float openHeight = 50f;   
    public float animationSpeed = 15f; 

    private bool isOpen = false;
    private static TuningCategory currentOpenCategory;

    private void Start()
    {
        // Induláskor azonnal 0-ra állítjuk a KÍVÁNT magasságot!
        subPanel.preferredHeight = 0f;
    }

    private void Update()
    {
        float targetHeight = isOpen ? openHeight : 0f;
        // A preferredHeight-et animáljuk, ez "erőszakolja" rá a méretet a gombokra!
        subPanel.preferredHeight = Mathf.Lerp(subPanel.preferredHeight, targetHeight, Time.deltaTime * animationSpeed);
    }

    public void ToggleCategory()
    {
        if (isOpen)
        {
            isOpen = false;
            if (currentOpenCategory == this) currentOpenCategory = null;
        }
        else
        {
            if (currentOpenCategory != null) 
            {
                currentOpenCategory.isOpen = false;
            }
            isOpen = true;
            currentOpenCategory = this;
        }
    }
}