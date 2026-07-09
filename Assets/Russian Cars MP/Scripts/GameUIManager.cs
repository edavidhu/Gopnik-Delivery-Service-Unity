using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance; 

    [Header("Alap UI")]
    public TextMeshProUGUI speedometerText;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI moneyPopupText; 
    public Slider carHealthSlider; // <-- ÚJ VÁLTOZÓ: Az autód HP csíkjának referenciája


    [Header("Szerelő UI")]
    public GameObject repairPanel;
    public TextMeshProUGUI repairCostText;
    public Button yesButton;

    [Header("Hangok")]
    public AudioSource uiAudioSource;
    public AudioClip moneyGainClip;  // Pénz kapása (Csilingelés)
    public AudioClip moneySpendClip; // Pénz költése (Pénztárgép hang vagy halk hiba hang)

    private int playerMoney;
    private SimpleCarController activePlayerCar;
    private int currentRepairCost = 0;
    private Coroutine popupCoroutine; // ÚJ: Animáció tároló

    private void Awake() { Instance = this; }

    private void Start()
    {
        // KÖTELEZŐ: Kiolvassuk a TÉNYLEGES pénzt a memóriából (ha nincs, csak akkor ad 500-at)
        playerMoney = PlayerPrefs.GetInt("PlayerMoney", 500);
        UpdateMoneyUI();
        
        if (repairPanel != null) repairPanel.SetActive(false);
    }

    private void Update()
    {
        if (activePlayerCar != null)
        {
            // 1. Kilométeróra frissítése
            if (speedometerText != null)
            {
                float speedKMH = activePlayerCar.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f;
                speedometerText.text = Mathf.RoundToInt(speedKMH) + " KM/H";
            }

            // 2. Autó életerő csíkjának frissítése (JAVÍTVA)
            if (carHealthSlider != null)
            {
                carHealthSlider.maxValue = 100f; // <-- BIZTOSÍTÉK: 100 a maximum!
                carHealthSlider.minValue = 0f;   // <-- BIZTOSÍTÉK: 0 a minimum!
                carHealthSlider.value = activePlayerCar.carHealth; 
            }
        }
    }

    public void RegisterPlayerCar(SimpleCarController car) { activePlayerCar = car; }

    // --- PÉNZ RENDSZER ---
    public void AddMoney(int amount)
    {
        playerMoney += amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        StartCoroutine(FlashMoneyText(Color.green));
        
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(AnimateMoneyPopup($"+ {amount} $", Color.green));

        // HANG LEJÁTSZÁSA
        if (uiAudioSource != null && moneyGainClip != null) uiAudioSource.PlayOneShot(moneyGainClip);
    }

    public void SpendMoney(int amount)
    {
        playerMoney -= amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        StartCoroutine(FlashMoneyText(Color.red));
        
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(AnimateMoneyPopup($"- {amount} $", Color.red));

        // HANG LEJÁTSZÁSA
        if (uiAudioSource != null && moneySpendClip != null) uiAudioSource.PlayOneShot(moneySpendClip);
    }

    private void UpdateMoneyUI() { if (moneyText != null) moneyText.text = playerMoney.ToString() + " $"; }

    private IEnumerator FlashMoneyText(Color flashColor)
    {
        UpdateMoneyUI();
        moneyText.color = flashColor;
        yield return new WaitForSeconds(0.3f);
        float t = 0;
        while (t < 1f) { t += Time.deltaTime * 2f; moneyText.color = Color.Lerp(flashColor, Color.white, t); yield return null; }
        moneyText.color = Color.white;
    }

    // --- ÚJ: A LEBEGŐ SZÖVEG ANIMÁCIÓJA ---
    private IEnumerator AnimateMoneyPopup(string text, Color color)
    {
        if (moneyPopupText == null) yield break;

        moneyPopupText.text = text;
        moneyPopupText.color = color;
        moneyPopupText.gameObject.SetActive(true);

        // A pénz szöveg ALÁ tesszük induláskor
        Vector3 startPos = moneyText.transform.position - new Vector3(0, 40f, 0); 
        Vector3 endPos = startPos + new Vector3(0, 30f, 0); // Felfelé fog úszni 30 pixelt

        float duration = 1.5f; // 1.5 másodpercig tart
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Felfelé úszik
            moneyPopupText.transform.position = Vector3.Lerp(startPos, endPos, t);

            // Elhalványul (Alpha 1-ből 0 lesz)
            Color c = moneyPopupText.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            moneyPopupText.color = c;

            yield return null;
        }

        moneyPopupText.gameObject.SetActive(false);
    }

    // --- SZERELŐ ABLAK RENDSZER ---
    public void ShowRepairPopup(int cost, SimpleCarController car)
    {
        activePlayerCar = car; currentRepairCost = cost;
        bool canAfford = playerMoney >= cost;
        string colorHex = canAfford ? "#FFFFFF" : "#FF0000";
        repairCostText.text = $"Úgy néz ki, a kocsi kapott egy-két pofont...\n\nMegjavítod?\nÁr: <color={colorHex}>- {cost} $</color>";
        yesButton.interactable = canAfford;
        repairPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    public void ConfirmRepair()
    {
        if (playerMoney >= currentRepairCost && activePlayerCar != null)
        {
            SpendMoney(currentRepairCost);
            activePlayerCar.RepairCarFull();
            repairPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    public void CancelRepair() { if (repairPanel != null) repairPanel.SetActive(false); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

    public void ToggleHelpPanel(GameObject helpPanel)
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(!helpPanel.activeSelf);
        }
    }
}