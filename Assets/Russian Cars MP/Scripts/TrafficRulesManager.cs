using UnityEngine;
using TMPro;
using System.Collections;

public class TrafficRulesManager : MonoBehaviour
{
    public static TrafficRulesManager Instance;

    [Header("Büntetések és Jutalmak")]
    public int redLightFine = 50;
    public int noIndicatorFine = 30;
    public int goodCitizenReward = 200;
    public int GetCurrentFines() { return currentFines; }

    [Header("Időzítés és Csalásvédelem")]
    public float evaluationTimeSeconds = 300f; // 5 perc (Teszthez írd át az Inspectorban 30-ra!)
    public float requiredDistanceForReward = 500f; // Minimum ennyit kell menni a bónuszért

    [Header("UI (Opcionális)")]
    public TextMeshProUGUI policeMessageText; // Ide írjuk a büntetést/jutalmat

    private float timer;
    private int currentFines = 0;
    
    // Csalásvédelem változói
    private Transform playerCar;
    private Vector3 lastPlayerPosition;
    private float distanceDriven = 0f;

    private Coroutine messageCoroutine;

    private void Awake() { Instance = this; }

    private void Start()
    {
        timer = evaluationTimeSeconds;
        if (policeMessageText != null) policeMessageText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 1. Játékos megkeresése (ha még nincs meg)
        if (playerCar == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) 
            {
                playerCar = p.transform;
                lastPlayerPosition = playerCar.position;
            }
            return;
        }

        // 2. Megtett távolság mérése (A csalásvédelemhez)
        float distMoved = Vector3.Distance(playerCar.position, lastPlayerPosition);
        distanceDriven += distMoved;
        lastPlayerPosition = playerCar.position;

        // 3. Időzítő visszaszámlálása
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            EvaluateDriving();
            ResetCycle();
        }
    }

    // Ezt hívja meg a kocsid, ha átmész a piroson
    public void ReportRedLight()
    {
        currentFines += redLightFine;
        ShowPoliceMessage($"PIROSON ÁTHAJDÁS!\nBüntetés felírva: -{redLightFine}$", Color.red);
    }

    // Ezt hívja meg a kocsid, ha nem indexelsz
    public void ReportNoIndicator()
    {
        currentFines += noIndicatorFine;
        ShowPoliceMessage($"INDEXELÉS ELMARADT!\nBüntetés felírva: -{noIndicatorFine}$", new Color(1f, 0.5f, 0f)); // Narancssárga
    }

    private void EvaluateDriving()
    {
        if (currentFines > 0)
        {
            // Levonjuk a büntetéseket
            if (GameUIManager.Instance != null) GameUIManager.Instance.SpendMoney(currentFines);
            ShowPoliceMessage($"KÖZLEKEDÉSI BÍRSÁGOK LEVONVA:\n-{currentFines}$", Color.red);
        }
        else
        {
            // Ha szabályos volt, ellenőrizzük a csalásvédelmet!
            if (distanceDriven >= requiredDistanceForReward)
            {
                if (GameUIManager.Instance != null) GameUIManager.Instance.AddMoney(goodCitizenReward);
                ShowPoliceMessage($"SZABÁLYOS KÖZLEKEDÉS BÓNUSZ!\n+{goodCitizenReward}$", Color.green);
            }
            else
            {
                ShowPoliceMessage($"TÚL KEVESET VEZETTÉL A BÓNUSZHOZ!\nMegtett táv: {Mathf.RoundToInt(distanceDriven)}m", Color.yellow);
            }
        }
    }

    private void ResetCycle()
    {
        timer = evaluationTimeSeconds;
        currentFines = 0;
        distanceDriven = 0f;
    }

    private void ShowPoliceMessage(string message, Color c)
    {
        if (policeMessageText == null) return;
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageCoroutine = StartCoroutine(AnimateMessage(message, c));
    }

    private IEnumerator AnimateMessage(string msg, Color c)
    {
        policeMessageText.text = msg;
        policeMessageText.color = c;
        policeMessageText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3.5f); // 3.5 másodpercig látszik

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            Color temp = policeMessageText.color;
            temp.a = Mathf.Lerp(1f, 0f, t);
            policeMessageText.color = temp;
            yield return null;
        }

        policeMessageText.gameObject.SetActive(false);
    }

    // --- ÚJ: AZONNALI BÜNTETÉS-LEVONÁS KILÉPÉSKOR ---
    public void PayFinesOnExit()
    {
        if (currentFines > 0)
        {
            // Ha él a UI, ráküldjük az okos animációs levonásra!
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.SpendMoney(currentFines);
            }
            else
            {
                // Biztosíték ALT+F4 esetére, ha már nincs képernyő
                int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 500);
                savedMoney -= currentFines;
                PlayerPrefs.SetInt("PlayerMoney", savedMoney);
                PlayerPrefs.Save();
            }
            
            currentFines = 0; // Nullázzuk, hogy ne vonja le kétszer
        }
    }

    // EXTRA: Ha a játékos ALT+F4-et nyom, vagy "kilövi" a játékot az X-el, ez a funkció akkor is lefut az utolsó pillanatban!
    private void OnApplicationQuit()
    {
        PayFinesOnExit();
    }

}