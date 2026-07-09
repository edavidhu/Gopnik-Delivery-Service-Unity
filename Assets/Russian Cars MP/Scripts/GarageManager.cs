using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections; 

public class GarageManager : MonoBehaviour
{
    [Header("Kocsik és Árak")]
    public GameObject[] carPrefabs; 
    public int[] carPrices;         
    public Transform spawnPoint;    

    [Header("Gazdaság (Pénz)")]
    public int startingMoney = 500; 
    private int playerMoney;

    [Header("UI Panelek")]
    public GameObject mainMenuPanel;      
    public GameObject tuningMenuPanel;    
    public GameObject warningPopupPanel;  
    public GameObject colorPickerPanel;   
    public GameObject helpPanel; 
    public GameObject settingsPanel; // <-- ÚJ: BEÁLLÍTÁSOK PANEL

    [Header("Beállítások UI Elemek (ÚJ)")]
    public Toggle fullscreenToggle;   // <-- ÚJ: Teljes képernyő kapcsoló
    public Toggle nightModeToggle;    // Éjszaka kapcsoló
    public Toggle aiTrafficToggle;    // Forgalom kapcsoló
    public Slider volumeSlider;       // Hangerő csúszka

    [Header("Megerősítő Ablak")]
    public GameObject upgradeConfirmPanel;   
    public TextMeshProUGUI upgradeConfirmText; 
    public Button upgradeYesButton;          

    [Header("UI Szövegek")]
    public TextMeshProUGUI statsText; 
    public TextMeshProUGUI moneyText;      
    public TextMeshProUGUI carNamePriceText; 
    public TextMeshProUGUI moneyPopupText; 
    
    [Header("Gombok")]
    public GameObject buyCarButton;        
    public GameObject openTuningButton; 
    public GameObject sellCarButton;       
    public Button startGameButton;         

    [Header("Hangok")]
    public AudioSource uiAudioSource;
    public AudioClip moneyGainClip;  
    public AudioClip moneyErrorClip; 

    private GameObject currentCarInstance;
    private SimpleCarController carController;
    private int currentCarIndex = 0;
    private bool isCurrentCarOwned = false;

    private bool hasUnsavedChanges = false;
    private Color defaultPrimaryColor;
    private Color defaultSecondaryColor;
    private bool isEditingPrimaryColor = true; 
    private int speedLevel, gripLevel, suspensionLevel, cargoLevel;

    private string pendingAction = ""; 
    private int pendingAmount = 0;
    private Coroutine popupCoroutine;      
    private bool allowColorPainting = false;

    private void Start()
    {
        Cursor.visible = true;
        allowColorPainting = false;
    
        // 1. Lekérdezzük a monitor aktuális, valós felbontását (pl. 1920x1080 vagy 2560x1440)
        Resolution currentRes = Screen.currentResolution;
        
        // 2. Megnézzük a mentést, hogy teljes képernyőt akar-e a játékos
        bool isFullscreen = PlayerPrefs.GetInt("IsFullscreen", 1) == 1;

        // 3. RÁERŐLTETJÜK a monitor natív méretét a játékra fekete sávok nélkül!
        // A FullScreenWindow a "Keret nélküli ablak", ami megengedi, hogy átvidd másik monitorra!
        Screen.SetResolution(currentRes.width, currentRes.height, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        Cursor.lockState = CursorLockMode.None;

        playerMoney = PlayerPrefs.GetInt("PlayerMoney", startingMoney);
        PlayerPrefs.SetInt("Car_0_Owned", 1); 
        currentCarIndex = PlayerPrefs.GetInt("SelectedCar", 0);
        
        if (warningPopupPanel != null) warningPopupPanel.SetActive(false);
        if (colorPickerPanel != null) colorPickerPanel.SetActive(false);
        if (upgradeConfirmPanel != null) upgradeConfirmPanel.SetActive(false);
        if (moneyPopupText != null) moneyPopupText.gameObject.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false); 
        if (settingsPanel != null) settingsPanel.SetActive(false); // Beállítások rejtése
        
        // Globális Hangerő beállítása induláskor!
        AudioListener.volume = PlayerPrefs.GetFloat("GameVolume", 1f);

        SpawnCar(currentCarIndex);
        OpenMainMenu(); 
    }

    public void SpawnCar(int index)
    {
        if (currentCarInstance != null) Destroy(currentCarInstance);

        currentCarInstance = Instantiate(carPrefabs[index], spawnPoint.position, spawnPoint.rotation, spawnPoint);
        currentCarInstance.GetComponent<Rigidbody>().isKinematic = true; 
        carController = currentCarInstance.GetComponent<SimpleCarController>();
        
        if (carController != null) carController.isInGarage = true; 

        isCurrentCarOwned = PlayerPrefs.GetInt($"Car_{index}_Owned", 0) == 1;

        if (carController.primaryRenderer != null && carController.primaryRenderer.materials.Length > carController.primaryMaterialIndex) 
            defaultPrimaryColor = carController.primaryRenderer.materials[carController.primaryMaterialIndex].color;
        if (carController.secondaryRenderer != null && carController.secondaryRenderer.materials.Length > carController.secondaryMaterialIndex) 
            defaultSecondaryColor = carController.secondaryRenderer.materials[carController.secondaryMaterialIndex].color;

        LoadCarStats(index);
        ApplyStatsToCar();
        UpdateUI();
    }

    // --- ÚJ: BEÁLLÍTÁSOK MENÜ KEZELÉSE ---
    public void OpenSettingsMenu()
    {
        if (settingsPanel == null) return;
        
        // Betöltjük a memóriából a gombok állapotát, mielőtt megjelenítjük
        if (nightModeToggle != null) nightModeToggle.isOn = PlayerPrefs.GetInt("IsNightMode", 0) == 1;
        if (aiTrafficToggle != null) aiTrafficToggle.isOn = PlayerPrefs.GetInt("AITrafficOn", 1) == 1;
        if (volumeSlider != null) volumeSlider.value = PlayerPrefs.GetFloat("GameVolume", 1f);

        // ÚJ: Képernyő gomb állapotának betöltése
        if (fullscreenToggle != null) fullscreenToggle.isOn = PlayerPrefs.GetInt("IsFullscreen", 1) == 1;

        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // Ezt kötjük be a Hangerő Slider "OnValueChanged" eseményére!
    public void OnVolumeSliderChanged()
    {
        if (volumeSlider != null)
        {
            AudioListener.volume = volumeSlider.value; // Azonnal halkítja a zenét/hangokat
        }
    }

    public void SaveAndCloseSettings()
    {
        if (nightModeToggle != null) PlayerPrefs.SetInt("IsNightMode", nightModeToggle.isOn ? 1 : 0);
        if (aiTrafficToggle != null) PlayerPrefs.SetInt("AITrafficOn", aiTrafficToggle.isOn ? 1 : 0);
        if (volumeSlider != null) PlayerPrefs.SetFloat("GameVolume", volumeSlider.value);
        
        // ÚJ: Képernyő mentése és ALKALMAZÁSA!
        if (fullscreenToggle != null) 
        {
            PlayerPrefs.SetInt("IsFullscreen", fullscreenToggle.isOn ? 1 : 0);
            Screen.fullScreen = fullscreenToggle.isOn; // Azonnal átváltja az ablakot!
        }
        
        PlayerPrefs.Save();
        
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // ... [A TÖBBI RÉGI FUNKCIÓ, PÉNZ, FESTÉS, TUNING STB PONTOSAN UGYANÚGY MARAD ITT!] ...

    private void AddMoney(int amount)
    {
        playerMoney += amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        StartCoroutine(FlashMoneyText(Color.green)); 
        UpdateUI();
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(AnimateMoneyPopup($"+ {amount} $", Color.green));
        if (uiAudioSource != null && moneyGainClip != null) uiAudioSource.PlayOneShot(moneyGainClip);
    }

    private void SpendMoney(int amount)
    {
        playerMoney -= amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        StartCoroutine(FlashMoneyText(Color.red)); 
        UpdateUI();
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(AnimateMoneyPopup($"- {amount} $", Color.red));
        if (uiAudioSource != null && moneyGainClip != null) uiAudioSource.PlayOneShot(moneyGainClip);
    }

    private void PlayErrorSound() { if (uiAudioSource != null && moneyErrorClip != null) uiAudioSource.PlayOneShot(moneyErrorClip); }

    private IEnumerator FlashMoneyText(Color flashColor)
    {
        UpdateUI();
        moneyText.color = flashColor;
        yield return new WaitForSeconds(0.3f); 
        float t = 0;
        while (t < 1f) { t += Time.deltaTime * 2f; moneyText.color = Color.Lerp(flashColor, Color.white, t); yield return null; }
        moneyText.color = Color.white;
    }

    private IEnumerator AnimateMoneyPopup(string text, Color color)
    {
        if (moneyPopupText == null) yield break;
        moneyPopupText.text = text;
        moneyPopupText.color = color;
        moneyPopupText.gameObject.SetActive(true);

        Vector3 startPos = moneyText.transform.position - new Vector3(0, 40f, 0); 
        Vector3 endPos = startPos + new Vector3(0, 30f, 0); 

        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            moneyPopupText.transform.position = Vector3.Lerp(startPos, endPos, t);
            Color c = moneyPopupText.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            moneyPopupText.color = c;
            yield return null;
        }
        moneyPopupText.gameObject.SetActive(false);
    }

    public void RequestUpgradeSpeed() { SetupPopup("U_Speed", "Sebesség", speedLevel, true); }
    public void RequestUpgradeGrip() { SetupPopup("U_Grip", "Tapadás", gripLevel, true); }
    public void RequestUpgradeSuspension() { SetupPopup("U_Susp", "Felfüggesztés", suspensionLevel, true); }
    public void RequestUpgradeCargo() { SetupPopup("U_Cargo", "Raktér", cargoLevel, true); }

    public void RequestDowngradeSpeed() { SetupPopup("D_Speed", "Sebesség", speedLevel, false); }
    public void RequestDowngradeGrip() { SetupPopup("D_Grip", "Tapadás", gripLevel, false); }
    public void RequestDowngradeSuspension() { SetupPopup("D_Susp", "Felfüggesztés", suspensionLevel, false); }
    public void RequestDowngradeCargo() { SetupPopup("D_Cargo", "Raktér", cargoLevel, false); }

    public void RequestSellCar()
    {
        if (currentCarIndex == 0) return; 
        int refund = carPrices[currentCarIndex]; 
        pendingAction = "Sell_Car";
        pendingAmount = refund;
        upgradeConfirmText.text = $"Biztosan eladod ezt az autót?\n(A tuningok is elvesznek!)\n\nVisszakapsz: <color=#00FF00>+ {refund} $</color>";
        upgradeYesButton.interactable = true;
        upgradeConfirmPanel.SetActive(true);
    }

    private void SetupPopup(string actionCode, string statName, int currentLevel, bool isUpgrade)
    {
        if (isUpgrade && currentLevel >= 5) return; 
        if (!isUpgrade && currentLevel <= 1) return; 

        pendingAction = actionCode;

        if (isUpgrade)
        {
            pendingAmount = currentLevel * 250; 
            bool canAfford = playerMoney >= pendingAmount;
            string costColor = canAfford ? "#FFFFFF" : "#FF0000";
            
            if (!canAfford) PlayErrorSound();

            upgradeConfirmText.text = $"Biztosan fejleszted:\n<b>{statName}</b> (Lvl {currentLevel} -> Lvl {currentLevel + 1})\n\nÁr: <color={costColor}>- {pendingAmount} $</color>";
            upgradeYesButton.interactable = canAfford;
        }
        else
        {
            pendingAmount = (currentLevel - 1) * 250; 
            upgradeConfirmText.text = $"Biztosan leszereled:\n<b>{statName}</b> (Lvl {currentLevel} -> Lvl {currentLevel - 1})\n\nVisszakapsz: <color=#00FF00>+ {pendingAmount} $</color>";
            upgradeYesButton.interactable = true; 
        }
        
        upgradeConfirmPanel.SetActive(true);
    }

    public void ConfirmAction()
    {
        if (pendingAction.StartsWith("U_")) 
        {
            SpendMoney(pendingAmount);
            if (pendingAction.Contains("Speed")) speedLevel++;
            else if (pendingAction.Contains("Grip")) gripLevel++;
            else if (pendingAction.Contains("Susp")) suspensionLevel++;
            else if (pendingAction.Contains("Cargo")) cargoLevel++;
            hasUnsavedChanges = true;
        }
        else if (pendingAction.StartsWith("D_")) 
        {
            AddMoney(pendingAmount);
            if (pendingAction.Contains("Speed")) speedLevel--;
            else if (pendingAction.Contains("Grip")) gripLevel--;
            else if (pendingAction.Contains("Susp")) suspensionLevel--;
            else if (pendingAction.Contains("Cargo")) cargoLevel--;
            hasUnsavedChanges = true;
        }
        else if (pendingAction == "Sell_Car") 
        {
            AddMoney(pendingAmount);
            FactoryResetCar(currentCarIndex); 
            
            isCurrentCarOwned = false;
            upgradeConfirmPanel.SetActive(false);
            OpenMainMenu();
            SpawnCar(currentCarIndex); 
            return;
        }

        ApplyStatsToCar();
        UpdateUI();
        upgradeConfirmPanel.SetActive(false);
    }

    public void CancelAction()
    {
        upgradeConfirmPanel.SetActive(false);
        pendingAction = "";
    }

    private void FactoryResetCar(int index)
    {
        PlayerPrefs.DeleteKey($"Car_{index}_Owned");
        PlayerPrefs.DeleteKey($"Car_{index}_Speed");
        PlayerPrefs.DeleteKey($"Car_{index}_Grip");
        PlayerPrefs.DeleteKey($"Car_{index}_Suspension");
        PlayerPrefs.DeleteKey($"Car_{index}_Cargo");
        PlayerPrefs.DeleteKey($"Car_{index}_Color1_Custom");
        PlayerPrefs.DeleteKey($"Car_{index}_Color2_Custom");
        PlayerPrefs.Save();
    }

    public void BuyCurrentCar()
    {
        int price = carPrices[currentCarIndex];
        if (playerMoney >= price && !isCurrentCarOwned)
        {
            SpendMoney(price);
            PlayerPrefs.SetInt($"Car_{currentCarIndex}_Owned", 1);
            PlayerPrefs.Save();
            isCurrentCarOwned = true;
            UpdateUI();
        }
        else
        {
            PlayErrorSound(); 
        }
    }

    public void ToggleHelpPanel() { if (helpPanel != null) helpPanel.SetActive(!helpPanel.activeSelf); }

    public void OpenMainMenu()
    {
        allowColorPainting = false; 
        tuningMenuPanel.SetActive(false);
        colorPickerPanel.SetActive(false);
        if (upgradeConfirmPanel != null) upgradeConfirmPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        hasUnsavedChanges = false;
        UpdateUI();
    }

    public void OpenTuningMenu()
    {
        allowColorPainting = false; 
        mainMenuPanel.SetActive(false);
        tuningMenuPanel.SetActive(true);
        hasUnsavedChanges = false; 
        UpdateUI();
    }

    public void BackButtonPushed()
    {
        if (hasUnsavedChanges) warningPopupPanel.SetActive(true);
        else OpenMainMenu(); 
    }

    public void ConfirmDiscardChanges()
    {
        warningPopupPanel.SetActive(false);
        hasUnsavedChanges = false;
        playerMoney = PlayerPrefs.GetInt("PlayerMoney", startingMoney);
        LoadCarStats(currentCarIndex);
        ApplyStatsToCar();
        OpenMainMenu();
    }

    public void CancelDiscardChanges() { warningPopupPanel.SetActive(false); }

    public void SaveChanges()
    {
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.SetInt($"Car_{currentCarIndex}_Speed", speedLevel);
        PlayerPrefs.SetInt($"Car_{currentCarIndex}_Grip", gripLevel);
        PlayerPrefs.SetInt($"Car_{currentCarIndex}_Suspension", suspensionLevel);
        PlayerPrefs.SetInt($"Car_{currentCarIndex}_Cargo", cargoLevel);

        if (carController.primaryRenderer != null)
        {
            Color c1 = carController.primaryRenderer.materials[carController.primaryMaterialIndex].color;
            PlayerPrefs.SetFloat($"Car_{currentCarIndex}_Color1_R", c1.r);
            PlayerPrefs.SetFloat($"Car_{currentCarIndex}_Color1_G", c1.g);
            PlayerPrefs.SetFloat($"Car_{currentCarIndex}_Color1_B", c1.b);
            PlayerPrefs.SetInt($"Car_{currentCarIndex}_Color1_Custom", 1);
        }
        if (carController.secondaryRenderer != null)
        {
            Color c2 = carController.secondaryRenderer.materials[carController.secondaryMaterialIndex].color;
            PlayerPrefs.SetFloat($"Car_{currentCarIndex}_Color2_R", c2.r);
            PlayerPrefs.SetFloat($"Car_{currentCarIndex}_Color2_G", c2.g);
            PlayerPrefs.SetFloat($"Car_{currentCarIndex}_Color2_B", c2.b);
            PlayerPrefs.SetInt($"Car_{currentCarIndex}_Color2_Custom", 1);
        }

        PlayerPrefs.Save();
        hasUnsavedChanges = false;
        UpdateUI();
    }

    private void UpdateUI()
    {
        moneyText.text = $"Pénz: {playerMoney} $";
        if (sellCarButton != null) sellCarButton.SetActive(isCurrentCarOwned && currentCarIndex != 0);

        if (isCurrentCarOwned)
        {
            buyCarButton.SetActive(false);
            openTuningButton.SetActive(true); 
            startGameButton.interactable = true;
            carNamePriceText.text = $"Autó {currentCarIndex + 1} (Saját)";
            
            statsText.text = $"Sebesség: Lvl {speedLevel}\n" +
                             $"Tapadás: Lvl {gripLevel}\n" +
                             $"Felfüggesztés: Lvl {suspensionLevel}\n" +
                             $"Raktér: Lvl {cargoLevel}";
        }
        else
        {
            buyCarButton.SetActive(true);
            openTuningButton.SetActive(false); 
            startGameButton.interactable = false; 
            carNamePriceText.text = $"Autó {currentCarIndex + 1} - Ára: {carPrices[currentCarIndex]} $";
            statsText.text = "Vedd meg az autót a fejlesztéshez!";
        }
    }

    public void ToggleColorPicker() 
    { 
        if (colorPickerPanel != null) 
        {
            // Panel ki/be kapcsolása
            colorPickerPanel.SetActive(!colorPickerPanel.activeSelf); 
            
            // ITT A LÉNYEG: Levesszük a lakatot, ha a panel nyitva van!
            allowColorPainting = colorPickerPanel.activeSelf;
        } 
    }
    public void SelectPrimaryColorEdit() { isEditingPrimaryColor = true; }
    public void SelectSecondaryColorEdit() { isEditingPrimaryColor = false; }

    public void OnFCPColorChanged(Color newColor)
    {
        Debug.Log("FCP küldi a színt! A Lakat állapota: " + allowColorPainting);

        if (!allowColorPainting) return;
        if (carController == null || !isCurrentCarOwned) return;
        hasUnsavedChanges = true; 

        if (isEditingPrimaryColor && carController.primaryRenderer != null)
        {
            Material[] mats = carController.primaryRenderer.materials;
            if (mats.Length > carController.primaryMaterialIndex)
            {
                mats[carController.primaryMaterialIndex].color = newColor;
                carController.primaryRenderer.materials = mats; 
            }
        }
        else if (!isEditingPrimaryColor && carController.secondaryRenderer != null)
        {
            Material[] mats = carController.secondaryRenderer.materials;
            if (mats.Length > carController.secondaryMaterialIndex)
            {
                mats[carController.secondaryMaterialIndex].color = newColor;
                carController.secondaryRenderer.materials = mats;
            }
        }
    }

    public void ResetColorsToDefault()
    {
        if (carController == null || !isCurrentCarOwned) return;
        hasUnsavedChanges = true;

        if (carController.primaryRenderer != null)
        {
            Material[] mats = carController.primaryRenderer.materials;
            if (mats.Length > carController.primaryMaterialIndex)
            {
                mats[carController.primaryMaterialIndex].color = defaultPrimaryColor;
                carController.primaryRenderer.materials = mats;
            }
        }

        if (carController.secondaryRenderer != null)
        {
            Material[] mats = carController.secondaryRenderer.materials;
            if (mats.Length > carController.secondaryMaterialIndex)
            {
                mats[carController.secondaryMaterialIndex].color = defaultSecondaryColor;
                carController.secondaryRenderer.materials = mats;
            }
        }
    }

    private void LoadCarStats(int index)
    {
        speedLevel = PlayerPrefs.GetInt($"Car_{index}_Speed", 1);
        gripLevel = PlayerPrefs.GetInt($"Car_{index}_Grip", 1);
        suspensionLevel = PlayerPrefs.GetInt($"Car_{index}_Suspension", 1);
        cargoLevel = PlayerPrefs.GetInt($"Car_{index}_Cargo", 1);

        if (PlayerPrefs.GetInt($"Car_{index}_Color1_Custom", 0) == 1 && carController.primaryRenderer != null)
        {
            float r = PlayerPrefs.GetFloat($"Car_{index}_Color1_R");
            float g = PlayerPrefs.GetFloat($"Car_{index}_Color1_G");
            float b = PlayerPrefs.GetFloat($"Car_{index}_Color1_B");
            Material[] mats = carController.primaryRenderer.materials;
            if (mats.Length > carController.primaryMaterialIndex)
            {
                mats[carController.primaryMaterialIndex].color = new Color(r, g, b);
                carController.primaryRenderer.materials = mats;
            }
        }

        if (PlayerPrefs.GetInt($"Car_{index}_Color2_Custom", 0) == 1 && carController.secondaryRenderer != null)
        {
            float r = PlayerPrefs.GetFloat($"Car_{index}_Color2_R");
            float g = PlayerPrefs.GetFloat($"Car_{index}_Color2_G");
            float b = PlayerPrefs.GetFloat($"Car_{index}_Color2_B");
            Material[] mats = carController.secondaryRenderer.materials;
            if (mats.Length > carController.secondaryMaterialIndex)
            {
                mats[carController.secondaryMaterialIndex].color = new Color(r, g, b);
                carController.secondaryRenderer.materials = mats;
            }
        }
    }

    private void ApplyStatsToCar()
    {
        if (carController == null) return;
        carController.maxSpeedKMH = 80f + (speedLevel * 10f); 
        carController.motorForce = 1300f + (speedLevel * 200f);

        float newGrip = 0.5f + (gripLevel * 0.1f);
        carController.rearLeftCollider.sidewaysFriction = SetGrip(carController.rearLeftCollider, newGrip);
        carController.rearRightCollider.sidewaysFriction = SetGrip(carController.rearRightCollider, newGrip);

        float newSpring = 30000f + (suspensionLevel * 5000f);
        carController.frontLeftCollider.suspensionSpring = SetSpring(carController.frontLeftCollider, newSpring);
        carController.frontRightCollider.suspensionSpring = SetSpring(carController.frontRightCollider, newSpring);
        carController.rearLeftCollider.suspensionSpring = SetSpring(carController.rearLeftCollider, newSpring);
        carController.rearRightCollider.suspensionSpring = SetSpring(carController.rearRightCollider, newSpring);
    }

    private WheelFrictionCurve SetGrip(WheelCollider wc, float grip) { var f = wc.sidewaysFriction; f.asymptoteValue = grip; return f; }
    private JointSpring SetSpring(WheelCollider wc, float spring) { var s = wc.suspensionSpring; s.spring = spring; return s; }

    public void NextCar()
    {
        if (hasUnsavedChanges) return; 
        currentCarIndex++;
        if (currentCarIndex >= carPrefabs.Length) currentCarIndex = 0;
        PlayerPrefs.SetInt("SelectedCar", currentCarIndex);
        SpawnCar(currentCarIndex);
    }

    public void PreviousCar()
    {
        if (hasUnsavedChanges) return;
        currentCarIndex--;
        if (currentCarIndex < 0) currentCarIndex = carPrefabs.Length - 1;
        PlayerPrefs.SetInt("SelectedCar", currentCarIndex);
        SpawnCar(currentCarIndex);
    }

    public void StartGame() { SceneManager.LoadScene("SampleScene"); }
    public void QuitGame()
    {
        Debug.Log("Kilépés a játékból...");
        Application.Quit(); 
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}