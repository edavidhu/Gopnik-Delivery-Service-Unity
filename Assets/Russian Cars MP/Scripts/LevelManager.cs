using UnityEngine;
using TMPro; 
using UnityEngine.SceneManagement; 
using UnityEngine.InputSystem;     
using System.Collections;
using System.Collections.Generic; 

public class LevelManager : MonoBehaviour
{
    [Header("AI Forgalom (BeamNG Stílus)")]
    public GameObject[] aiCarPrefabs; 
    public int maxAICars = 15;        
    public float minSpawnDistance = 60f; 
    public float maxSpawnDistance = 120f; 

    [Header("Kocsik és Spawnolás")]
    public GameObject[] carPrefabs; 
    public int[] carPrices; // <-- ÚJ: EBBŐL TUDJA A SZERELŐ, HOGY MILYEN DRÁGA A KOCSI!
    public Transform startPoint;    

    [Header("Kamera és UI")]
    public CarCameraFollow cameraScript; 
    public TextMeshProUGUI speedometerText; 

    private GameObject spawnedPlayerCar;
    private GameObject[] allRoads; 

    private void Start()
    {
        Application.targetFrameRate = 60;
        // 1. Lekérdezzük a monitor aktuális, valós felbontását (pl. 1920x1080 vagy 2560x1440)
        Resolution currentRes = Screen.currentResolution;
        
        // 2. Megnézzük a mentést, hogy teljes képernyőt akar-e a játékos
        bool isFullscreen = PlayerPrefs.GetInt("IsFullscreen", 1) == 1;

        // 3. RÁERŐLTETJÜK a monitor natív méretét a játékra fekete sávok nélkül!
        // A FullScreenWindow a "Keret nélküli ablak", ami megengedi, hogy átvidd másik monitorra!
        Screen.SetResolution(currentRes.width, currentRes.height, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        Cursor.lockState = CursorLockMode.None;

        int selectedCarIndex = PlayerPrefs.GetInt("SelectedCar", 0);
        if (selectedCarIndex < 0 || selectedCarIndex >= carPrefabs.Length) selectedCarIndex = 0;

        spawnedPlayerCar = Instantiate(carPrefabs[selectedCarIndex], startPoint.position, startPoint.rotation);
        spawnedPlayerCar.name = "JatekosAuto";
        spawnedPlayerCar.tag = "Player";

        SimpleCarController carController = spawnedPlayerCar.GetComponent<SimpleCarController>();
        if (carController != null)
        {
            if (speedometerText != null) carController.speedometerText = speedometerText;

            int carPrice = (carPrices != null && carPrices.Length > selectedCarIndex) ? carPrices[selectedCarIndex] : 1000;
            carController.SetupCarData(selectedCarIndex, carPrice);
            
            if (GameUIManager.Instance != null) GameUIManager.Instance.RegisterPlayerCar(carController);

            LoadAndApplyCarData(carController, selectedCarIndex);
        }

        if (cameraScript != null)
        {
            Transform camTarget = spawnedPlayerCar.transform.Find("CameraTarget");
            if (camTarget != null) cameraScript.target = camTarget;
            else cameraScript.target = spawnedPlayerCar.transform; 
        }

        // --- 2. AI FORGALOM BEKAPCSOLÁSA (CSAK HA A MENÜBEN ENGEDÉLYEZTÉK!) ---
        int aiEnabled = PlayerPrefs.GetInt("AITrafficOn", 1); // Alapból 1 (Bekapcsolva)
        allRoads = GameObject.FindGameObjectsWithTag("Road");
        
        if (aiEnabled == 1 && allRoads.Length > 0 && aiCarPrefabs.Length > 0)
        {
            StartCoroutine(MaintainTrafficLoop());
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ReturnToGarage();
        }
    }

    IEnumerator MaintainTrafficLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); 

            if (spawnedPlayerCar == null) continue;

            GameObject[] currentAICars = GameObject.FindGameObjectsWithTag("AICar");

            if (currentAICars.Length < maxAICars)
            {
                SpawnSingleAICar();
            }
        }
    }

    private void SpawnSingleAICar()
    {
        List<Transform> validSpawnPoints = new List<Transform>();
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("AISpawnPoint");

        foreach (GameObject sp in spawnPoints)
        {
            if (spawnedPlayerCar != null)
            {
                float dist = Vector3.Distance(spawnedPlayerCar.transform.position, sp.transform.position);
                if (dist > minSpawnDistance && dist < maxSpawnDistance)
                {
                    validSpawnPoints.Add(sp.transform);
                }
            }
        }

        if (validSpawnPoints.Count > 0)
        {
            Transform selectedPoint = validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
            
            bool isRoadClear = true;
            Collider[] collidersInArea = Physics.OverlapSphere(selectedPoint.position, 8f); 
            
            foreach (Collider col in collidersInArea)
            {
                if (col.transform.root.CompareTag("AICar") || col.transform.root.CompareTag("Player"))
                {
                    isRoadClear = false;
                    break; 
                }
            }

            if (isRoadClear)
            {
                Quaternion flatRotation = Quaternion.Euler(0, selectedPoint.eulerAngles.y, 0);
                Instantiate(aiCarPrefabs[Random.Range(0, aiCarPrefabs.Length)], selectedPoint.position, flatRotation);
            }
        }
    }

    // Ezt hívja meg az ESC gomb és a UI Gomb!
    public void ReturnToGarage() 
    { 
        StartCoroutine(ExitSequence());
    }
    
    private IEnumerator ExitSequence()
    {
        // Ha van büntetés, levonjuk, animáljuk, és VÁRUNK!
        if (TrafficRulesManager.Instance != null && TrafficRulesManager.Instance.GetCurrentFines() > 0)
        {
            TrafficRulesManager.Instance.PayFinesOnExit();
            yield return new WaitForSeconds(2f); // 2 másodperc szünet az animációnak!
        }

        // 2 másodperc múlva (vagy azonnal, ha nincs bünti) betölti a Garázst
        SceneManager.LoadScene("GarageScene"); 
    }

    private void LoadAndApplyCarData(SimpleCarController carController, int index)
    {
        int speedLevel = PlayerPrefs.GetInt($"Car_{index}_Speed", 1);
        int gripLevel = PlayerPrefs.GetInt($"Car_{index}_Grip", 1);
        int suspensionLevel = PlayerPrefs.GetInt($"Car_{index}_Suspension", 1);

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

        if (PlayerPrefs.GetInt($"Car_{index}_Color1_Custom", 0) == 1 && carController.primaryRenderer != null)
        {
            float r = PlayerPrefs.GetFloat($"Car_{index}_Color1_R");
            float g = PlayerPrefs.GetFloat($"Car_{index}_Color1_G");
            float b = PlayerPrefs.GetFloat($"Car_{index}_Color1_B");
            Material[] mats = carController.primaryRenderer.materials;
            if (mats.Length > carController.primaryMaterialIndex) { mats[carController.primaryMaterialIndex].color = new Color(r, g, b); carController.primaryRenderer.materials = mats; }
        }

        if (PlayerPrefs.GetInt($"Car_{index}_Color2_Custom", 0) == 1 && carController.secondaryRenderer != null)
        {
            float r = PlayerPrefs.GetFloat($"Car_{index}_Color2_R");
            float g = PlayerPrefs.GetFloat($"Car_{index}_Color2_G");
            float b = PlayerPrefs.GetFloat($"Car_{index}_Color2_B");
            Material[] mats = carController.secondaryRenderer.materials;
            if (mats.Length > carController.secondaryMaterialIndex) { mats[carController.secondaryMaterialIndex].color = new Color(r, g, b); carController.secondaryRenderer.materials = mats; }
        }
    }

    private WheelFrictionCurve SetGrip(WheelCollider wc, float grip) { var f = wc.sidewaysFriction; f.asymptoteValue = grip; return f; }
    private JointSpring SetSpring(WheelCollider wc, float spring) { var s = wc.suspensionSpring; s.spring = spring; return s; }
}