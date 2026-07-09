using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; 

public class SimpleCarController : MonoBehaviour
{
    [Header("Kerék Colliderek (Fizika)")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Kerék Modellek (Látvány)")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Autó Beállítások")]
    public float motorForce = 1500f; 
    public float brakeForce = 3000f;  

    [Header("Árkád Kormányzás és Drift")]
    public float lowSpeedSteerAngle = 35f;  
    public float highSpeedSteerAngle = 10f; 
    public float steerSmoothSpeed = 5f;     
    [Range(0.8f, 1f)] public float driftAssist = 0.95f;       

    [Header("Sebességhatárok és Légellenállás")]
    public float maxSpeedKMH = 90f;        
    public float maxReverseSpeedKMH = 30f; 
    public float accelerationFalloff = 2f; 

    [Header("SÉRÜLÉS ÉS JAVÍTÁS")]
    public float carHealth = 100f;         
    public ParticleSystem engineSmoke;     
    private CarDeformation deformationScript;

    [HideInInspector] public int carIndex; 
    [HideInInspector] public int basePrice; 

    [Header("Garázs Beállítások (Festés)")]
    public MeshRenderer primaryRenderer;     
    public int primaryMaterialIndex = 0;     
    public MeshRenderer secondaryRenderer;   
    public int secondaryMaterialIndex = 1; 

    [Header("GUI / UI")]
    public TextMeshProUGUI speedometerText; 

    [Header("Lámpa Rendszer")]
    public GameObject[] headlights;     
    public GameObject[] brakeLights;    
    public GameObject[] reverseLights;  
    public GameObject[] leftIndicators; 
    public GameObject[] rightIndicators;

    [Header("Hangok - Motor")]
    public AudioSource engineIdleAudio; 
    public AudioSource engineMaxAudio;  
    public float minEnginePitch = 0.8f; 
    public float maxEnginePitch = 1.8f; 
    public int gears = 3;               
    public float engineMaxVolumeMultiplier = 1f; 

    [Header("Hangok - Ütközés")]
    public AudioSource crashAudioSource; 
    public AudioClip crashSmall;         
    public AudioClip crashMedium;        
    public AudioClip crashLarge;         
    public float crashVolumeMultiplier = 2.5f; 
    
    [Header("Hangok - Füst és Csikorgás")]
    public AudioSource skidAudio;       
    public TrailRenderer[] rearSkidmarks; 
    public ParticleSystem[] exhaustParticles; 
    public float idleSmokeRate = 15f; 
    public float gasSmokeRate = 150f; 

    [Header("Hangok - Extrák (Duda és Index)")]
    public AudioSource hornAudioSource;      
    public AudioSource indicatorAudioSource; 
    public AudioClip indicatorTickClip;      

    // --- ÁLLAPOT VÁLTOZÓK ---
    [HideInInspector] public bool isInGarage = false; 

    private float horizontalInput, verticalInput, currentSteerAngle = 0f;
    private bool isHandbraking, needsReset = false;
    private Rigidbody rb;
    private bool isHeadlightsOn, isLeftIndicatorOn, isRightIndicatorOn, indicatorBlinkState = false;
    private float indicatorTimer = 0f;
    private bool isMobileHornPressed = false;

    // --- KÖZLEKEDÉSI FIGYELŐ VÁLTOZÓK ---
    private bool insideIntersection = false;
    private Vector3 intersectionEntryDir;
    private bool usedLeftBlinkerInIntersection = false;
    private bool usedRightBlinkerInIntersection = false;

    public void SetupCarData(int index, int price)
    {
        carIndex = index;
        basePrice = price;
        carHealth = PlayerPrefs.GetFloat($"Car_{carIndex}_Health", 100f);
        if (carHealth <= 40f && engineSmoke != null && !engineSmoke.isPlaying) engineSmoke.Play();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        deformationScript = GetComponent<CarDeformation>(); 
        
        ToggleLights(headlights, false);
        ToggleLights(brakeLights, false);
        ToggleLights(reverseLights, false);
        ToggleLights(leftIndicators, false);
        ToggleLights(rightIndicators, false);
        SetSkidmarks(false);
        
        if (engineSmoke != null && carHealth > 40f) engineSmoke.Stop(); 

        // Biztosíték: a letöltött index hang betöltése induláskor
        if (indicatorAudioSource != null && indicatorTickClip != null && indicatorAudioSource.clip == null)
        {
            indicatorAudioSource.clip = indicatorTickClip;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) needsReset = true;
        
        GetInput();
        HandleLightInputs();
        UpdateUI();

        if (insideIntersection)
        {
            if (isLeftIndicatorOn) usedLeftBlinkerInIntersection = true;
            if (isRightIndicatorOn) usedRightBlinkerInIntersection = true;
        }
    }

    private void FixedUpdate()
    {
        if (needsReset)
        {
            PerformReset();
            needsReset = false;
        }

        HandleMotor();
        HandleSteering(); 
        ApplyDriftAssist(); 
        UpdateWheels();
        HandleEffects(); 
    }

    public void TakeDamage(float damageAmount)
    {
        carHealth -= damageAmount;
        if (carHealth < 0) carHealth = 0;

        PlayerPrefs.SetFloat($"Car_{carIndex}_Health", carHealth);
        PlayerPrefs.Save();

        if (carHealth <= 40f && engineSmoke != null && !engineSmoke.isPlaying)
        {
            engineSmoke.Play();
        }

        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.DamageCargoFromCrash(damageAmount);
        }
    }

    public void RepairCarFull()
    {
        carHealth = 100f;
        PlayerPrefs.SetFloat($"Car_{carIndex}_Health", carHealth);
        PlayerPrefs.Save();

        if (engineSmoke != null) engineSmoke.Stop();
        if (deformationScript != null) deformationScript.RepairMesh();
    }

    private void GetInput()
    {
        if (isInGarage)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            isHandbraking = true; 
            return;
        }

        // --- ÚJ MOBILOS + PC IRÁNYÍTÁS ---
        bool mobileGas = MobileInputManager.Instance != null && MobileInputManager.Instance.isGasPressed;
        bool mobileBrake = MobileInputManager.Instance != null && MobileInputManager.Instance.isBrakePressed;
        bool mobileLeft = MobileInputManager.Instance != null && MobileInputManager.Instance.isLeftPressed;
        bool mobileRight = MobileInputManager.Instance != null && MobileInputManager.Instance.isRightPressed;
        bool mobileHandbrake = MobileInputManager.Instance != null && MobileInputManager.Instance.isHandbrakePressed;

        float w = (Keyboard.current.wKey.isPressed || mobileGas) ? 1f : 0f;
        float s = (Keyboard.current.sKey.isPressed || mobileBrake) ? -1f : 0f;
        verticalInput = w + s; 

        float a = (Keyboard.current.aKey.isPressed || mobileLeft) ? -1f : 0f;
        float d = (Keyboard.current.dKey.isPressed || mobileRight) ? 1f : 0f;
        horizontalInput = a + d;

        isHandbraking = Keyboard.current.spaceKey.isPressed || mobileHandbrake;
    }

    private void HandleMotor()
    {
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        float currentSpeedKMH = rb.linearVelocity.magnitude * 3.6f; 

        float currentMotorForce = 0f;
        float currentBrakeForce = 0f;

        bool isActuallyBraking = false;
        bool isReversing = false;

        float healthMultiplier = Mathf.Clamp(carHealth / 100f, 0.3f, 1.0f);
        float actualMaxSpeed = maxSpeedKMH * healthMultiplier;
        float actualMotorForce = motorForce * healthMultiplier;

        if (isHandbraking) 
        {
            currentBrakeForce = brakeForce * 2f; 
            isActuallyBraking = true;
        }
        else if (Mathf.Abs(verticalInput) < 0.05f) 
        {
            currentMotorForce = 0.01f; 
            currentBrakeForce = 0f;
        }
        else
        {
            if (verticalInput > 0.05f) 
            {
                if (forwardSpeed < -1f) 
                {
                    currentBrakeForce = brakeForce;
                    isActuallyBraking = true;
                }
                else 
                {
                    float speedRatio = Mathf.Clamp01(currentSpeedKMH / actualMaxSpeed);
                    float dragFactor = 1f - Mathf.Pow(speedRatio, accelerationFalloff);

                    if (currentSpeedKMH < actualMaxSpeed) currentMotorForce = verticalInput * actualMotorForce * dragFactor;
                    else currentMotorForce = 0f; 
                }
            }
            else if (verticalInput < -0.05f) 
            {
                if (forwardSpeed > 1f) 
                {
                    currentBrakeForce = brakeForce;
                    isActuallyBraking = true;
                }
                else 
                {
                    float reverseRatio = Mathf.Clamp01(currentSpeedKMH / maxReverseSpeedKMH);
                    float reverseDragFactor = 1f - Mathf.Pow(reverseRatio, accelerationFalloff);

                    if (currentSpeedKMH < maxReverseSpeedKMH) currentMotorForce = verticalInput * actualMotorForce * reverseDragFactor;
                    else currentMotorForce = 0f;
                    
                    isReversing = true;
                }
            }
        }

        ToggleLights(brakeLights, isActuallyBraking);
        ToggleLights(reverseLights, isReversing && forwardSpeed < 0.5f);

        frontLeftCollider.motorTorque = currentMotorForce;
        frontRightCollider.motorTorque = currentMotorForce;
        rearLeftCollider.motorTorque = currentMotorForce;
        rearRightCollider.motorTorque = currentMotorForce;

        ApplyBraking(currentBrakeForce);
    }

    private void ApplyBraking(float force)
    {
        frontLeftCollider.brakeTorque = force;
        frontRightCollider.brakeTorque = force;
        rearLeftCollider.brakeTorque = force;
        rearRightCollider.brakeTorque = force;
    }

    private void HandleSteering()
    {
        float speedRatio = Mathf.Clamp01((rb.linearVelocity.magnitude * 3.6f) / maxSpeedKMH);
        float dynamicMaxSteer = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedRatio);
        float targetSteerAngle = horizontalInput * dynamicMaxSteer;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.deltaTime * steerSmoothSpeed);

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void ApplyDriftAssist()
    {
        if (rb.linearVelocity.magnitude > 3f)
        {
            if (Mathf.Abs(horizontalInput) < 0.1f) rb.angularVelocity = new Vector3(rb.angularVelocity.x, rb.angularVelocity.y * 0.85f, rb.angularVelocity.z);
            else rb.angularVelocity = new Vector3(rb.angularVelocity.x, rb.angularVelocity.y * driftAssist, rb.angularVelocity.z);
        }
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        if (wheelTransform == null) return;
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    private void HandleEffects()
    {
        if (isInGarage)
        {
            if (engineIdleAudio != null) engineIdleAudio.volume = 0f;
            if (engineMaxAudio != null) engineMaxAudio.volume = 0f;
            if (skidAudio != null) skidAudio.volume = 0f;
            
            foreach (ParticleSystem exhaust in exhaustParticles)
            {
                if (exhaust != null && exhaust.isPlaying) exhaust.Stop();
            }
            if (engineSmoke != null && engineSmoke.isPlaying) engineSmoke.Stop();
            return; 
        }

        float currentSpeedKMH = rb.linearVelocity.magnitude * 3.6f;
        float throttle = Mathf.Abs(verticalInput);

        // MOTORHANG
        if (engineIdleAudio != null && engineMaxAudio != null)
        {
            float targetPitch = minEnginePitch;
            float maxVolTarget = 0f;
            float idleVolTarget = 1f;

            if (throttle > 0.05f) 
            {
                float gearSize = maxSpeedKMH / gears; 
                float currentGearFloat = currentSpeedKMH / gearSize;
                
                if (currentGearFloat >= gears) currentGearFloat = gears - 0.01f;

                int currentGear = Mathf.FloorToInt(currentGearFloat);
                float rpmRatio = currentGearFloat - currentGear; 

                float gearBasePitch = minEnginePitch + (currentGear * 0.1f);
                targetPitch = Mathf.Lerp(gearBasePitch, maxEnginePitch, rpmRatio);

                maxVolTarget = 1f * engineMaxVolumeMultiplier; 
                idleVolTarget = 0.2f;
            }
            else 
            {
                targetPitch = minEnginePitch;
                maxVolTarget = 0.1f; 
                idleVolTarget = 1f;  
            }

            engineIdleAudio.pitch = Mathf.Lerp(engineIdleAudio.pitch, targetPitch, Time.deltaTime * 5f);
            engineIdleAudio.volume = Mathf.Lerp(engineIdleAudio.volume, idleVolTarget, Time.deltaTime * 5f);
            
            engineMaxAudio.pitch = Mathf.Lerp(engineMaxAudio.pitch, targetPitch, Time.deltaTime * 5f);
            engineMaxAudio.volume = Mathf.Lerp(engineMaxAudio.volume, maxVolTarget, Time.deltaTime * 5f);
        }

        // CSIKORGÁS
        bool isSkidding = (isHandbraking && currentSpeedKMH > 10f) || 
                          (Mathf.Abs(horizontalInput) > 0.6f && currentSpeedKMH > 35f) || 
                          (verticalInput < 0 && currentSpeedKMH > 15f && Vector3.Dot(transform.forward, rb.linearVelocity) > 0);

        if (isSkidding)
        {
            if (skidAudio != null && !skidAudio.isPlaying) skidAudio.Play();
            if (skidAudio != null) skidAudio.volume = Mathf.Lerp(skidAudio.volume, 1f, Time.deltaTime * 10f);
            SetSkidmarks(true);
        }
        else
        {
            if (skidAudio != null) skidAudio.volume = Mathf.Lerp(skidAudio.volume, 0f, Time.deltaTime * 10f);
            SetSkidmarks(false);
        }

        // FÜST
        foreach (ParticleSystem exhaust in exhaustParticles)
        {
            if (exhaust == null) continue;
            if (!exhaust.isPlaying) exhaust.Play();
            var emission = exhaust.emission;
            float targetRate = (throttle > 0.1f) ? gasSmokeRate : idleSmokeRate;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(targetRate);
        }
    }

    private void SetSkidmarks(bool emitting)
    {
        foreach (TrailRenderer trail in rearSkidmarks)
        {
            if (trail != null) trail.emitting = emitting;
        }
    }

    private void UpdateUI()
    {
        if (speedometerText != null)
        {
            float speedKMH = rb.linearVelocity.magnitude * 3.6f;
            speedometerText.text = Mathf.RoundToInt(speedKMH) + " KM/H";
        }
    }

    private void HandleLightInputs()
    {
        if (isInGarage) return; 

        bool wasIndicating = isLeftIndicatorOn || isRightIndicatorOn;

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            isHeadlightsOn = !isHeadlightsOn;
            ToggleLights(headlights, isHeadlightsOn);
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            isLeftIndicatorOn = !isLeftIndicatorOn;
            if (isLeftIndicatorOn) isRightIndicatorOn = false; 
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            isRightIndicatorOn = !isRightIndicatorOn;
            if (isRightIndicatorOn) isLeftIndicatorOn = false;
        }

        bool isIndicatingNow = isLeftIndicatorOn || isRightIndicatorOn;

        // 1. BEKAPCSOLÁS
        if (!wasIndicating && isIndicatingNow)
        {
            if (indicatorAudioSource != null)
            {
                indicatorAudioSource.Play();
            }
        }
        // 2. KIKAPCSOLÁS
        else if (wasIndicating && !isIndicatingNow)
        {
            if (indicatorAudioSource != null) indicatorAudioSource.Stop();
            
            indicatorBlinkState = false;
            indicatorTimer = 0f;
            ToggleLights(leftIndicators, false);
            ToggleLights(rightIndicators, false);
        }

        // 3. VILLOGÁS (Látvány)
        if (isIndicatingNow)
        {
            indicatorTimer += Time.deltaTime;
            if (indicatorTimer >= 0.4f)
            {
                indicatorTimer = 0f;
                indicatorBlinkState = !indicatorBlinkState;
                
                if (isLeftIndicatorOn) ToggleLights(leftIndicators, indicatorBlinkState);
                if (isRightIndicatorOn) ToggleLights(rightIndicators, indicatorBlinkState);
            }
        }

        // --- KÖZÖSÍTETT DUDA (Billentyűzet VAGY Mobil Gomb) ---
        bool isHornCurrentlyPressed = Keyboard.current.hKey.isPressed || isMobileHornPressed;

        if (isHornCurrentlyPressed)
        {
            if (hornAudioSource != null && !hornAudioSource.isPlaying) hornAudioSource.Play();
        }
        else
        {
            if (hornAudioSource != null && hornAudioSource.isPlaying) hornAudioSource.Stop();
        }
    }

    private void StartIndicator()
    {
        indicatorTimer = 0.4f; 
    }

    private void PerformReset()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true; 
        
        GameObject[] waypoints = GameObject.FindGameObjectsWithTag("Waypoint");
        Transform closestWp = null;
        float minDistance = Mathf.Infinity; 

        foreach (GameObject wp in waypoints)
        {
            float dist = Vector3.Distance(transform.position, wp.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist; 
                closestWp = wp.transform;
            }
        }

        if (closestWp != null)
        {
            transform.position = closestWp.position + new Vector3(0, 2f, 0);
            Vector3 forwardDir = closestWp.forward;
            forwardDir.y = 0; 
            if (forwardDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(forwardDir);
        }
        else
        {
            transform.position = new Vector3(0, 3f, 0); 
            transform.rotation = Quaternion.identity;
        }

        rb.isKinematic = false; 
    }

    private void ToggleLights(GameObject[] lightsArray, bool state)
    {
        if (lightsArray == null) return;
        foreach (GameObject lightObj in lightsArray)
        {
            if (lightObj != null) lightObj.SetActive(state);
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        float impactForce = col.relativeVelocity.magnitude;

        if (impactForce > 2f && crashAudioSource != null)
        {
            AudioClip clipToPlay = crashSmall;

            if (impactForce > 15f) clipToPlay = crashLarge;
            else if (impactForce > 7f) clipToPlay = crashMedium;

            if (clipToPlay != null)
            {
                crashAudioSource.pitch = Random.Range(0.8f, 1.2f); 
                float volume = Mathf.Clamp01(impactForce / 15f) * crashVolumeMultiplier;
                crashAudioSource.PlayOneShot(clipToPlay, volume);
            }
        }

        if (col.gameObject.transform.root.CompareTag("RedLight"))
        {
            if (!insideIntersection)
            {
                float angle = Vector3.Angle(transform.forward, col.gameObject.transform.root.forward);
                if (angle < 60f)
                {
                    if (TrafficRulesManager.Instance != null) TrafficRulesManager.Instance.ReportRedLight();
                }
            }
        }

        if (col.gameObject.transform.root.CompareTag("Intersection"))
        {
            insideIntersection = true;
            intersectionEntryDir = transform.forward; 
            usedLeftBlinkerInIntersection = false;
            usedRightBlinkerInIntersection = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("RedLight"))
        {
            if (!insideIntersection)
            {
                float angle = Vector3.Angle(transform.forward, other.transform.forward);
                if (angle < 60f)
                {
                    if (TrafficRulesManager.Instance != null) TrafficRulesManager.Instance.ReportRedLight();
                }
            }
        }

        if (other.CompareTag("Intersection"))
        {
            insideIntersection = true;
            intersectionEntryDir = transform.forward; 
            usedLeftBlinkerInIntersection = false;
            usedRightBlinkerInIntersection = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Intersection"))
        {
            insideIntersection = false;

            float turnAngle = Vector3.SignedAngle(intersectionEntryDir, transform.forward, Vector3.up);

            if (turnAngle > 40f && !usedRightBlinkerInIntersection)
            {
                if (TrafficRulesManager.Instance != null) TrafficRulesManager.Instance.ReportNoIndicator();
            }
            else if (turnAngle < -40f && !usedLeftBlinkerInIntersection)
            {
                if (TrafficRulesManager.Instance != null) TrafficRulesManager.Instance.ReportNoIndicator();
            }
        }
    }

    // --- MOBIL GOMBOK FUNKCIÓI ---

    public void MobileToggleHeadlights()
    {
        isHeadlightsOn = !isHeadlightsOn;
        ToggleLights(headlights, isHeadlightsOn);
    }

    public void MobileToggleLeftIndicator()
    {
        isLeftIndicatorOn = !isLeftIndicatorOn;
        if (isLeftIndicatorOn) isRightIndicatorOn = false; 
        
        if (isLeftIndicatorOn) { if (indicatorAudioSource != null) indicatorAudioSource.Play(); }
        else { 
            if (indicatorAudioSource != null) indicatorAudioSource.Stop(); 
            indicatorBlinkState = false; ToggleLights(leftIndicators, false); 
        }
    }

    public void MobileToggleRightIndicator()
    {
        isRightIndicatorOn = !isRightIndicatorOn;
        if (isRightIndicatorOn) isLeftIndicatorOn = false; 
        
        if (isRightIndicatorOn) { if (indicatorAudioSource != null) indicatorAudioSource.Play(); }
        else { 
            if (indicatorAudioSource != null) indicatorAudioSource.Stop(); 
            indicatorBlinkState = false; ToggleLights(rightIndicators, false); 
        }
    }

    public void MobileResetCar()
    {
        needsReset = true;
    }

    public void MobileHorn(bool isPressed)
    {
        isMobileHornPressed = isPressed; 
    }

}