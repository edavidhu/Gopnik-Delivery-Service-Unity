using UnityEngine;

public class RepairStation : MonoBehaviour
{
    public float repairCostMultiplier = 0.05f; 
    private float cooldownTimer = 0f; // Időzítő, hogy ne spammelje az ablakot

    private void Update()
    {
        // Visszaszámláló, ha bezártad az ablakot, de a körben maradtál
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    // AMIKOR BELEHAJTASZ A KÖRBE
    private void OnTriggerEnter(Collider other)
    {
        CheckAndRepair(other);
    }

    // AMIKOR BENNE ÁLLSZ A KÖRBE (És a cooldown lejárt)
    private void OnTriggerStay(Collider other)
    {
        if (cooldownTimer <= 0f)
        {
            CheckAndRepair(other);
        }
    }

    private void CheckAndRepair(Collider other)
    {
        // 1. RÖNTGEN: Látjuk, hogy valami hozzáért!
        Debug.Log("<color=yellow>MŰHELY: Valami belém ért -> " + other.gameObject.name + "</color>");

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
        {
            SimpleCarController car = other.attachedRigidbody.GetComponent<SimpleCarController>();
            
            if (car != null)
            {
                // 2. RÖNTGEN: Felismerte, hogy te vagy az!
                Debug.Log("<color=green>MŰHELY: Játékos autó felismerve! Életerő: " + car.carHealth + "</color>");

                // Ha a kocsi makkegészséges, nincs dolgunk
                if (car.carHealth >= 99f) return;

                // Ha már nyitva van a szerelő ablak, nem nyitjuk meg újra!
                if (GameUIManager.Instance != null && GameUIManager.Instance.repairPanel.activeSelf) return;

                // Dinamikus árképzés
                float missingHealthPercentage = (100f - car.carHealth) / 100f; 
                int finalCost = Mathf.RoundToInt(car.basePrice * missingHealthPercentage * repairCostMultiplier);

                if (finalCost < 10) finalCost = 10;

                // Ablak megjelenítése
                GameUIManager.Instance.ShowRepairPopup(finalCost, car);

                // Adunk 3 másodperc "nyugalmat", miután felugrott az ablak, hogy ne villogjon
                cooldownTimer = 3f; 
            }
        }
    }
}