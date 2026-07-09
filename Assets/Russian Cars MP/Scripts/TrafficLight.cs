using UnityEngine;

public class TrafficLight : MonoBehaviour
{
    [Header("Lámpa Fények (Húzz be többet is!)")]
    public GameObject[] redLights;
    public GameObject[] yellowLights;
    public GameObject[] greenLights;

    [Header("Időzítések (Másodperc)")]
    public float greenTime = 8f;
    public float yellowTime = 2f;
    public float redTime = 8f;

    [Header("Kezdő Állapot")]
    [Tooltip("0 = Zöld, 1 = Sárga, 2 = Piros (A keresztutcákat állítsd 2-re!)")]
    public int startingState = 0;

    private float timer;
    private int currentState;

    private void Start()
    {
        currentState = startingState;
        SetState(currentState);
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            currentState++;
            if (currentState > 2) currentState = 0; // Piros után vissza zöldre

            SetState(currentState);
        }
    }

    private void SetState(int state)
    {
        // 1. Minden lámpát kikapcsolunk
        ToggleLights(redLights, false);
        ToggleLights(yellowLights, false);
        ToggleLights(greenLights, false);

        // 2. Bekapcsoljuk az aktuálist és beállítjuk az időzítőt
        if (state == 0) 
        {
            ToggleLights(greenLights, true);
            timer = greenTime;
        }
        else if (state == 1)
        {
            ToggleLights(yellowLights, true);
            timer = yellowTime;
        }
        else if (state == 2)
        {
            ToggleLights(redLights, true);
            timer = redTime;
        }
    }

    // Segédfüggvény, ami az összes listában lévő lámpát egyszerre kapcsolja
    private void ToggleLights(GameObject[] lightsArray, bool state)
    {
        if (lightsArray == null) return;
        foreach (GameObject lightObj in lightsArray)
        {
            if (lightObj != null) lightObj.SetActive(state);
        }
    }
}