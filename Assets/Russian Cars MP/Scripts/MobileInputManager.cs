using UnityEngine;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance;

    // Pedálok állapotai
    [HideInInspector] public bool isGasPressed;
    [HideInInspector] public bool isBrakePressed;
    [HideInInspector] public bool isLeftPressed;
    [HideInInspector] public bool isRightPressed;
    [HideInInspector] public bool isHandbrakePressed;

    private SimpleCarController activeCar;
    private CarCameraFollow activeCamera;

    private void Awake() { Instance = this; }

    // Ez a funkció megkeresi a frissen lespawnolt autót és kamerát!
    private void FindReferences()
    {
        if (activeCar == null) activeCar = FindFirstObjectByType<SimpleCarController>();
        if (activeCamera == null) activeCamera = FindFirstObjectByType<CarCameraFollow>();
    }

    // --- 1. NYOMVA TARTÓS GOMBOK (Ezeket kötjük az Event Triggerre) ---
    public void PressGas(bool state) { isGasPressed = state; }
    public void PressBrake(bool state) { isBrakePressed = state; }
    public void PressLeft(bool state) { isLeftPressed = state; }
    public void PressRight(bool state) { isRightPressed = state; }
    public void PressHandbrake(bool state) { isHandbrakePressed = state; }
    
    public void PressHorn(bool state) { 
        FindReferences(); 
        if (activeCar != null) activeCar.MobileHorn(state); 
    }

    // --- 2. KATTINTÓS GOMBOK (Ezeket kötjük az On Click-re) ---
    public void ToggleLights() { 
        FindReferences(); 
        if (activeCar != null) activeCar.MobileToggleHeadlights(); 
    }
    public void ToggleLeftIndicator() { 
        FindReferences(); 
        if (activeCar != null) activeCar.MobileToggleLeftIndicator(); 
    }
    public void ToggleRightIndicator() { 
        FindReferences(); 
        if (activeCar != null) activeCar.MobileToggleRightIndicator(); 
    }
    public void ResetCar() { 
        FindReferences(); 
        if (activeCar != null) activeCar.MobileResetCar(); 
    }
    public void SwitchCamera() { 
        FindReferences(); 
        if (activeCamera != null) activeCamera.MobileSwitchCamera(); 
    }
}