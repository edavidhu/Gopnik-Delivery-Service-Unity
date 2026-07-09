using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class DayNightCycle : MonoBehaviour
{
    [Header("Napszak Beállítások")]
    public float dayLengthInSeconds = 120f;
    
    [Header("Éjszakai Sötétség")]
    public float nightAmbientIntensity = 0.4f;

    [Header("Központi Utcalámpa Vezérlő")]
    public Color streetlightColor = new Color(1f, 0.9f, 0.7f); 
    public float streetlightIntensity = 1000f; 
    public float streetlightRange = 80f;     
    
    private Light sun;
    private float maxSunIntensity;
    private float defaultAmbientIntensity;

    private List<Light> allStreetLights = new List<Light>();
    private bool areStreetLightsOn = false;
    private bool isCurrentlyNight = false; 

    private void Start()
    {
        //PlayerPrefs.DeleteAll();
        sun = GetComponent<Light>();
        if (sun != null) maxSunIntensity = sun.intensity;
        
        defaultAmbientIntensity = RenderSettings.ambientIntensity;

        FindAndSetupStreetLights();

        // --- A JAVÍTÁS: KIOLVASSA A MENÜBŐL A NAPSZAKOT! ---
        // Ha 1 van elmentve, éjszaka van. Ha 0 (alap), akkor nappal.
        int savedNightMode = PlayerPrefs.GetInt("IsNightMode", 0);
        isCurrentlyNight = (savedNightMode == 1);
        
        SetTimeOfDay(isCurrentlyNight);
    }

    private void Update()
    {
        // Ha megnyomod az 'N' betűt a billentyűzeten, azonnal váltja a napszakot!
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            ToggleDayNightButton();
        }
    }

    private void FindAndSetupStreetLights()
    {
        GameObject[] lightObjects = GameObject.FindGameObjectsWithTag("StreetLight");

        foreach (GameObject obj in lightObjects)
        {
            Light lightComponent = obj.GetComponent<Light>();
            if (lightComponent != null)
            {
                lightComponent.color = streetlightColor;
                lightComponent.intensity = streetlightIntensity;
                lightComponent.range = streetlightRange;
                
                allStreetLights.Add(lightComponent);
                lightComponent.enabled = false;
            }
        }
        Debug.Log($"[Utcalámpa Vezérlő] Sikeresen megtalálva és beállítva {allStreetLights.Count} darab utcalámpa!");
    }

    public void ToggleDayNightButton()
    {
        isCurrentlyNight = !isCurrentlyNight;
        SetTimeOfDay(isCurrentlyNight);
    }

    private void SetTimeOfDay(bool makeNight)
    {
        if (makeNight) // ÉJSZAKA CSINÁLÁSA
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); 
                sun.intensity = 0f;
            }
            RenderSettings.ambientIntensity = nightAmbientIntensity;
            ToggleStreetLights(true);
        }
        else // NAPPAL CSINÁLÁSA
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f); 
                sun.intensity = maxSunIntensity;
            }
            RenderSettings.ambientIntensity = defaultAmbientIntensity;
            ToggleStreetLights(false);
        }
    }

    private void ToggleStreetLights(bool state)
    {
        areStreetLightsOn = state;
        foreach (Light l in allStreetLights)
        {
            if (l != null) l.enabled = state;
        }
    }
}