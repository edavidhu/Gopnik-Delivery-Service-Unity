using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;

public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance;

    [System.Serializable]
    public class Mission
    {
        public string title;
        public int reward;
        public int requiredCargoLevel;
        public float addedWeight;
        public float timeLimit;
        public Transform pickupPoint;
        public Transform dropoffPoint;
    }

    // --- AZ A* NAVIGÁCIÓS AGY ---
    private class NavNode
    {
        public Transform waypoint;
        public List<NavNode> neighbors = new List<NavNode>();
        public float gCost, hCost;
        public NavNode parent;
        public float fCost { get { return gCost + hCost; } }
    }

    [Header("Görgethető Menü Rendszer")]
    public GameObject jobBoardPanel;
    public Transform missionContentContainer; 
    public GameObject missionCardPrefab;      
    public int missionsToGenerate = 10;       

    [Header("Küldetés Műszerfal")]
    public GameObject missionHUDPanel; 
    public TextMeshProUGUI timerText; 
    public Slider gForceSlider;       
    public Slider cargoHealthSlider;  
    public TextMeshProUGUI missionStatusText; 

    [Header("Dinamikus Színek")]
    public Image gForceFillImage;        
    public Gradient gForceColorGradient; 
    public Image cargoHealthFillImage;   
    public Gradient healthColorGradient; 

    [Header("Fizika és Erőhatás")]
    public float maxGForceDisplay = 25f; 
    public float damagePerSecond = 15f;  
    public float gForceSensitivity = 0.2f; 

    [Header("Küldetés Rendszer")]
    public GameObject missionMarkerPrefab; 
    public Transform fixedPizzeriaLocation; 
    
    [Header("Navigáció és GPS")]
    public GameObject navArrow; 
    public float arrowHoverHeight = 3.5f; 
    public LineRenderer gpsLine; 
    public float gpsLineHeight = 5f; 
    public float rerouteDistance = 100f; // Ha 100 méterre letérsz a sárga csíkról, újratervez!

    public float cargoHealth = 100f;

    private Rigidbody playerRb;
    private int playerCargoLevel = 1;
    private float originalCarMass = 0f;
    private float lastRerouteTime = 0f; // <-- Ezt másold be a többi változó közé!

    private List<Mission> generatedMissions = new List<Mission>();
    private Mission activeMission;
    
    private enum DeliveryState { Idle, GoingToPickup, Delivering }
    private DeliveryState currentState = DeliveryState.Idle;

    private GameObject currentMarker;
    private float missionTimer = 0f;
    private float currentGForce;
    private float lastForwardSpeed = 0f;
    
    private Coroutine statusCoroutine;

    private List<NavNode> allNavNodes = new List<NavNode>();
    private List<Vector3> currentGpsPath = new List<Vector3>();

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (jobBoardPanel != null) jobBoardPanel.SetActive(false);
        if (missionHUDPanel != null) missionHUDPanel.SetActive(false);
        if (navArrow != null) navArrow.SetActive(false);
        if (missionStatusText != null) missionStatusText.gameObject.SetActive(false); 
        if (gpsLine != null) gpsLine.enabled = false;

        int currentCarIndex = PlayerPrefs.GetInt("SelectedCar", 0);
        playerCargoLevel = PlayerPrefs.GetInt($"Car_{currentCarIndex}_Cargo", 1);

        // A VÁROS FELTÉRKÉPEZÉSE INDULÁSKOR!
        StartCoroutine(BuildNavigationGraph());
    }

    private IEnumerator BuildNavigationGraph()
    {
        GameObject[] waypoints = GameObject.FindGameObjectsWithTag("Waypoint");
        Dictionary<Transform, NavNode> nodeDict = new Dictionary<Transform, NavNode>();

        foreach (var wp in waypoints)
        {
            NavNode newNode = new NavNode { waypoint = wp.transform };
            allNavNodes.Add(newNode);
            nodeDict.Add(wp.transform, newNode);
        }

        foreach (var node in allNavNodes)
        {
            Collider[] hits = Physics.OverlapSphere(node.waypoint.position, 30f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Waypoint") && hit.transform != node.waypoint)
                {
                    Vector3 dirToNeighbor = (hit.transform.position - node.waypoint.position).normalized;
                    // Csak a jó irányba lévő gömböket köti össze!
                    if (Vector3.Dot(node.waypoint.forward, dirToNeighbor) > 0f)
                    {
                        if (nodeDict.ContainsKey(hit.transform)) node.neighbors.Add(nodeDict[hit.transform]);
                    }
                }
            }
        }
        yield return null;
        Debug.Log($"<color=cyan>IGAZI GPS Rendszer Kalibrálva! {allNavNodes.Count} pont.</color>");
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.mKey.wasPressedThisFrame && currentState == DeliveryState.Idle) ToggleJobBoard();

        if (currentState == DeliveryState.Delivering)
        {
            missionTimer -= Time.deltaTime;
            if (timerText != null) timerText.text = "Idő: " + Mathf.Ceil(missionTimer).ToString() + " mp";
            if (missionTimer <= 0) FailMission("KIFUTOTTÁL AZ IDŐBŐL!");
        }
    }

    private void FixedUpdate()
    {
        if (currentState != DeliveryState.Delivering || playerRb == null) return;

        float currentForwardSpeed = playerRb.transform.InverseTransformDirection(playerRb.linearVelocity).z;
        float forwardG = (currentForwardSpeed - lastForwardSpeed) / Time.fixedDeltaTime;
        lastForwardSpeed = currentForwardSpeed;

        if (forwardG < 0f && forwardG > -20f) forwardG *= 0.1f; 

        float lateralG = playerRb.linearVelocity.magnitude * playerRb.angularVelocity.y * 2.5f;
        float rawGForce = Mathf.Sqrt((forwardG * forwardG) + (lateralG * lateralG));

        float targetGForce = rawGForce * gForceSensitivity;
        currentGForce = Mathf.Lerp(currentGForce, targetGForce, Time.fixedDeltaTime * 5f);
        
        if (gForceSlider != null && gForceFillImage != null) 
        {
            gForceSlider.value = currentGForce;
            float gForceNormalized = Mathf.Clamp01(currentGForce / maxGForceDisplay);
            gForceFillImage.color = gForceColorGradient.Evaluate(gForceNormalized);
        }

        if (currentGForce >= maxGForceDisplay * 0.95f)
        {
            cargoHealth -= damagePerSecond * Time.fixedDeltaTime;
            if (cargoHealth < 0) cargoHealth = 0;
            if (cargoHealth <= 0) FailMission("A RAKOMÁNY TÖNKREMENT!");
        }

        if (cargoHealthSlider != null && cargoHealthFillImage != null)
        {
            cargoHealthSlider.value = cargoHealth;
            float hpNormalized = Mathf.Clamp01(cargoHealth / 100f);
            cargoHealthFillImage.color = healthColorGradient.Evaluate(hpNormalized);
        }
    }

    private void LateUpdate()
    {
        // --- 1. A LEBEGŐ NYÍL MOZGATÁSA ---
        if (navArrow != null && navArrow.activeSelf && playerRb != null && currentMarker != null)
        {
            navArrow.transform.position = playerRb.transform.position + (Vector3.up * arrowHoverHeight);
            Vector3 targetPos = currentMarker.transform.position;
            targetPos.y = navArrow.transform.position.y; 
            Vector3 direction = targetPos - navArrow.transform.position;

            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                navArrow.transform.rotation = Quaternion.Slerp(navArrow.transform.rotation, targetRot, Time.deltaTime * 15f);
            }
        }

        // --- 2. A JAVÍTOTT, "ÉHES" GPS VONAL ---
        if (gpsLine != null && currentGpsPath.Count > 0 && playerRb != null)
        {
            if (currentState == DeliveryState.GoingToPickup || currentState == DeliveryState.Delivering)
            {
                gpsLine.enabled = true;

                // 1. MEGESZÜNK MINDEN PONTOT, AMIT ELHAGYTUNK!
                while (currentGpsPath.Count > 0)
                {
                    Vector3 dirToPoint = (currentGpsPath[0] - playerRb.position).normalized;
                    float dist = Vector3.Distance(playerRb.position, currentGpsPath[0]);

                    // HA a pont a hátunk mögött van (Dot < -0.1) VAGY nagyon közel vagyunk hozzá (dist < 20) -> DOBJUK!
                    if ((Vector3.Dot(playerRb.transform.forward, dirToPoint) < -0.1f && dist < 40f) || dist < 15f)
                    {
                        currentGpsPath.RemoveAt(0); // Levágja a vonal elejét!
                    }
                    else
                    {
                        break; // Megtaláltuk a legelső ÉRVÉNYES pontot előttünk, kilépünk a ciklusból!
                    }
                }

                // 2. ÚJRATERVEZÉS (Ha letértél a sárga csíkról)
                if (currentGpsPath.Count > 0)
                {
                    Vector3 checkPoint = currentGpsPath[0];
                    checkPoint.y = playerRb.position.y;

                    // Ha letértél az útról...
                    if (Vector3.Distance(playerRb.position, checkPoint) > rerouteDistance)
                    {
                        // CSAK akkor tervezhet újra, ha eltelt 2 másodperc az utolsó tervezés óta!
                        if (Time.time - lastRerouteTime > 2f) 
                        {
                            Debug.Log("<color=orange>GPS: ÚJRATERVEZÉS...</color>");
                            lastRerouteTime = Time.time;
                            CalculateGPSPath();
                        }
                        return; // Ebben a frame-ben megállunk, amíg a GPS gondolkodik
                    }
                }

                // 3. VONAL KIRAJZOLÁSA
                if (currentGpsPath.Count > 0)
                {
                    gpsLine.positionCount = currentGpsPath.Count + 1;
                    
                    // A vonal a kocsi orrából indul
                    Vector3 carPos = playerRb.position + (playerRb.transform.forward * 2f);
                    carPos.y = gpsLineHeight;
                    gpsLine.SetPosition(0, carPos); 

                    // A többi pont az utcákon megy a célig
                    for (int i = 0; i < currentGpsPath.Count; i++)
                    {
                        Vector3 p = currentGpsPath[i];
                        p.y = gpsLineHeight;
                        gpsLine.SetPosition(i + 1, p);
                    }
                }
                else
                {
                    gpsLine.enabled = false;
                }
            }
            else
            {
                gpsLine.enabled = false;
            }
        }
        else if (gpsLine != null)
        {
            gpsLine.enabled = false;
        }
    }

    // --- AZ A* PATHFINDING ALGORITMUS (A Mátrix agya!) ---
    private void CalculateGPSPath()
    {
        if (playerRb == null || currentMarker == null || allNavNodes.Count == 0) return;

        NavNode startNode = GetClosestNode(playerRb.position);
        NavNode endNode = GetClosestNode(currentMarker.transform.position);

        if (startNode == null || endNode == null) return;

        List<NavNode> openSet = new List<NavNode>();
        HashSet<NavNode> closedSet = new HashSet<NavNode>();
        openSet.Add(startNode);

        foreach (var n in allNavNodes) { n.gCost = int.MaxValue; n.parent = null; }
        startNode.gCost = 0;
        startNode.hCost = Vector3.Distance(startNode.waypoint.position, endNode.waypoint.position);

        while (openSet.Count > 0)
        {
            NavNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                RetracePath(startNode, endNode);
                return;
            }

            foreach (NavNode neighbor in currentNode.neighbors)
            {
                if (closedSet.Contains(neighbor)) continue;

                float newMovementCostToNeighbor = currentNode.gCost + Vector3.Distance(currentNode.waypoint.position, neighbor.waypoint.position);
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = Vector3.Distance(neighbor.waypoint.position, endNode.waypoint.position);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                }
            }
        }
    }

    private void RetracePath(NavNode startNode, NavNode endNode)
    {
        currentGpsPath.Clear();
        NavNode currentNode = endNode;

        while (currentNode != startNode)
        {
            Vector3 pos = currentNode.waypoint.position;
            pos.y = gpsLineHeight; 
            currentGpsPath.Add(pos);
            currentNode = currentNode.parent;
        }

        Vector3 startPos = startNode.waypoint.position;
        startPos.y = gpsLineHeight;
        currentGpsPath.Add(startPos);
        
        currentGpsPath.Reverse(); 

        Vector3 finalDest = currentMarker.transform.position;
        finalDest.y = gpsLineHeight;
        currentGpsPath.Add(finalDest);
    }

    private NavNode GetClosestNode(Vector3 position)
    {
        NavNode closest = null;
        float minDist = 9999f;
        foreach (var node in allNavNodes)
        {
            float dist = Vector3.Distance(position, node.waypoint.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = node;
            }
        }
        return closest;
    }

    public void ToggleJobBoard()
    {
        if (currentState != DeliveryState.Idle) return; 
        if (jobBoardPanel == null) return; 

        bool isOpen = jobBoardPanel.activeSelf;
        if (!isOpen) 
        {
            GenerateMissions(); 
            PopulateUI();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else 
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        jobBoardPanel.SetActive(!isOpen);
    }

    private void GenerateMissions()
    {
        generatedMissions.Clear();
        GameObject[] roads = GameObject.FindGameObjectsWithTag("Road");
        if (roads.Length < 2) return; 

        for (int i = 0; i < missionsToGenerate; i++)
        {
            Mission newJob = new Mission();
            int randType = Random.Range(0, 3); 

            if (randType == 0)
            {
                newJob.title = "Pizzaszállítás";
                newJob.requiredCargoLevel = 1;
                newJob.reward = Random.Range(100, 250);
                newJob.addedWeight = 50f; 
                newJob.timeLimit = Random.Range(60f, 100f);
                newJob.pickupPoint = fixedPizzeriaLocation != null ? fixedPizzeriaLocation : roads[Random.Range(0, roads.Length)].transform;
                newJob.dropoffPoint = roads[Random.Range(0, roads.Length)].transform;
            }
            else if (randType == 1)
            {
                newJob.title = "Közepes Csomag";
                newJob.requiredCargoLevel = Random.Range(1, 3); 
                newJob.reward = Random.Range(300, 600);
                newJob.addedWeight = 300f; 
                newJob.timeLimit = Random.Range(100f, 150f);
                newJob.pickupPoint = roads[Random.Range(0, roads.Length)].transform;
                newJob.dropoffPoint = roads[Random.Range(0, roads.Length)].transform;
            }
            else
            {
                newJob.title = "Építőanyag Szállítás";
                newJob.requiredCargoLevel = Random.Range(3, 6); 
                newJob.reward = Random.Range(800, 2000);
                newJob.addedWeight = Random.Range(800f, 1500f); 
                newJob.timeLimit = Random.Range(150f, 240f);
                newJob.pickupPoint = roads[Random.Range(0, roads.Length)].transform;
                newJob.dropoffPoint = roads[Random.Range(0, roads.Length)].transform;
            }

            generatedMissions.Add(newJob);
        }
    }

    private void PopulateUI()
    {
        foreach (Transform child in missionContentContainer) Destroy(child.gameObject);

        for (int i = 0; i < generatedMissions.Count; i++)
        {
            Mission m = generatedMissions[i];
            GameObject newCard = Instantiate(missionCardPrefab, missionContentContainer);
            MissionCardUI cardUI = newCard.GetComponent<MissionCardUI>();

            if (cardUI != null)
            {
                cardUI.titleText.text = m.title;
                cardUI.detailsText.text = $"Súly: {m.addedWeight:F0} kg\nJutalom: <color=#00FF00>{m.reward} $</color>\nMin. Raktér: Lvl {m.requiredCargoLevel}";

                if (playerCargoLevel < m.requiredCargoLevel)
                {
                    cardUI.acceptButton.interactable = false;
                    if (cardUI.buttonText != null) { cardUI.buttonText.text = "Kicsi a raktér"; cardUI.buttonText.color = Color.red; }
                }
                else
                {
                    cardUI.acceptButton.interactable = true;
                    if (cardUI.buttonText != null) { cardUI.buttonText.text = "Elfogad"; cardUI.buttonText.color = Color.black; }

                    int index = i; 
                    cardUI.acceptButton.onClick.RemoveAllListeners();
                    cardUI.acceptButton.onClick.AddListener(() => AcceptMission(index));
                }
            }
        }
    }

    public void AcceptMission(int index)
    {
        activeMission = generatedMissions[index];
        currentState = DeliveryState.GoingToPickup;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerRb = playerObj.GetComponent<Rigidbody>();

        if (currentMarker != null) Destroy(currentMarker);
        
        float heightOffset = missionMarkerPrefab.transform.localScale.y;
        currentMarker = Instantiate(missionMarkerPrefab, activeMission.pickupPoint.position + new Vector3(0, heightOffset, 0), Quaternion.identity);

        if (navArrow != null) navArrow.SetActive(true);

        CalculateGPSPath();

        if (jobBoardPanel != null) jobBoardPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        
        ShowStatusMessage("MENJ A FELVÉTELI PONTHOZ!", Color.yellow);
    }

    public void OnMarkerReached()
    {
        if (currentState == DeliveryState.GoingToPickup)
        {
            ShowStatusMessage("CSOMAG FELVÉVE! VIGYÁZZ RÁ!", Color.cyan);
            currentState = DeliveryState.Delivering;

            float heightOffset = missionMarkerPrefab.transform.localScale.y;
            currentMarker.transform.position = activeMission.dropoffPoint.position + new Vector3(0, heightOffset, 0);

            CalculateGPSPath();

            if (playerRb != null)
            {
                originalCarMass = playerRb.mass;
                playerRb.mass += activeMission.addedWeight;
            }

            cargoHealth = 100f; 
            missionTimer = activeMission.timeLimit;
            
            if (missionHUDPanel != null) missionHUDPanel.SetActive(true);
            if (gForceSlider != null) { gForceSlider.maxValue = maxGForceDisplay; gForceSlider.value = 0; }
            if (cargoHealthSlider != null) { cargoHealthSlider.maxValue = 100f; cargoHealthSlider.value = 100f; }
            
            if (playerRb != null) lastForwardSpeed = playerRb.transform.InverseTransformDirection(playerRb.linearVelocity).z;
        }
        else if (currentState == DeliveryState.Delivering)
        {
            CompleteMission();
        }
    }

    private void CompleteMission()
    {
        ShowStatusMessage($"SIKERES FUVAR! +{activeMission.reward} $", Color.green);
        if (GameUIManager.Instance != null) GameUIManager.Instance.AddMoney(activeMission.reward);
        EndMissionCleanup();
    }

    public void FailMission(string reason)
    {
        ShowStatusMessage($"KÜLDETÉS ELBUKVA!\n{reason}", Color.red);
        EndMissionCleanup();
    }

    public void DamageCargoFromCrash(float crashForce)
    {
        if (currentState != DeliveryState.Delivering) return;

        float cargoDamage = crashForce * 1.5f; 
        cargoHealth -= cargoDamage;
        if (cargoHealth < 0) cargoHealth = 0;

        currentGForce = maxGForceDisplay; 

        if (cargoHealthSlider != null) cargoHealthSlider.value = cargoHealth;

        if (cargoHealth <= 0)
        {
            FailMission("A RAKOMÁNY TÖNKREMENT AZ ÜTKÖZÉSBEN!");
        }
    }

    private void EndMissionCleanup()
    {
        currentState = DeliveryState.Idle;
        if (currentMarker != null) Destroy(currentMarker);
        if (navArrow != null) navArrow.SetActive(false);
        if (gpsLine != null) gpsLine.enabled = false;
        currentGpsPath.Clear(); 
        if (playerRb != null && originalCarMass > 0) playerRb.mass = originalCarMass;
        if (missionHUDPanel != null) missionHUDPanel.SetActive(false);
    }

    private void ShowStatusMessage(string message, Color color)
    {
        if (missionStatusText == null) return;
        
        if (statusCoroutine != null) StopCoroutine(statusCoroutine);
        statusCoroutine = StartCoroutine(AnimateStatusText(message, color));
    }

    private IEnumerator AnimateStatusText(string message, Color color)
    {
        missionStatusText.text = message;
        missionStatusText.color = color;
        missionStatusText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3f);

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            Color c = missionStatusText.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            missionStatusText.color = c;
            yield return null;
        }

        missionStatusText.gameObject.SetActive(false);
    }
}