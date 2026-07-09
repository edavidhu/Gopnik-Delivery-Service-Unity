using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PhysicalAICar : MonoBehaviour
{
    [Header("Kerék Colliderek (Fizika)")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Kerék Modellek")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;
    public Transform bodyMesh; 

    [Header("AI Teljesítmény")]
    public float motorForce = 1200f;   
    public float brakeForce = 8000f;   
    public float maxSpeedKMH = 50f;    
    public float maxSteerAngle = 40f;  
    public Vector3 centerOfMassOffset = new Vector3(0, 0.2f, 0); 

    [Header("Lámpa Rendszer (Vizuális)")]
    public GameObject[] headlights;     
    public GameObject[] brakeLights;    
    public GameObject[] leftIndicators; 
    public GameObject[] rightIndicators;

    [Header("Szenzorok és Waypointok")]
    public float sensorLength = 15f;
    public float despawnDistance = 150f;
    public float searchRadius = 40f; 
    
    private Rigidbody rb;
    private Transform playerTransform;

    private float currentSteerAngle = 0f;
    private bool isBraking = false;
    public bool isCrashed = false; 
    private float stuckTimer = 0f;
    private bool isWaitingAtLight = false;
    
    private bool isUTurning = false; 
    private bool isPushingAI = false;

    private Transform currentWaypoint;
    private Transform lockedPathParent = null; 
    private Vector3 lookAheadPoint;

    // Indexelés változói
    private float indicatorTimer = 0f;
    private bool indicatorBlinkState = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMassOffset; 
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        SetAIGrip(frontLeftCollider);
        SetAIGrip(frontRightCollider);
        SetAIGrip(rearLeftCollider);
        SetAIGrip(rearRightCollider);

        // Lámpák lekapcsolása induláskor
        ToggleLights(headlights, false);
        ToggleLights(brakeLights, false);
        ToggleLights(leftIndicators, false);
        ToggleLights(rightIndicators, false);

        FindNextWaypoint(); 
    }

    void SetAIGrip(WheelCollider wc)
    {
        WheelFrictionCurve f = wc.sidewaysFriction;
        f.extremumValue = 3f;  
        f.asymptoteValue = 3f;
        f.stiffness = 3f;
        wc.sidewaysFriction = f;
    }

    void FixedUpdate()
    {
        // 70 fokos dőlésnél törlés
        if (Vector3.Angle(transform.up, Vector3.up) > 70f)
        {
            Destroy(gameObject);
            return;
        }

        if (transform.position.y < -5f) { Destroy(gameObject); return; }
        if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) > despawnDistance) { Destroy(gameObject); return; }

        if (isCrashed) return; 

        if (!isUTurning)
        {
            ManageWaypoint(); 
            HandleSteering();
            CheckSensors();
            HandleAntiJam(); 
            HandleMotor(); 
        }
        else
        {
            frontLeftCollider.motorTorque = 0f; frontRightCollider.motorTorque = 0f;
            rearLeftCollider.motorTorque = 0f; rearRightCollider.motorTorque = 0f;
            frontLeftCollider.brakeTorque = 0f; frontRightCollider.brakeTorque = 0f;
            rearLeftCollider.brakeTorque = 0f; rearRightCollider.brakeTorque = 0f;
        }

        UpdateWheels();
        HandleLights(); // ÚJ: Lámpák frissítése minden képkockán!
    }

    // --- LÁMPAVEZÉRLŐ RENDSZER ---
    void HandleLights()
    {
        // 1. Éjszakai Fényszórók (Ha a Unity környezeti fénye 0.8 alá csökken, felkapcsol)
        bool isNight = RenderSettings.ambientIntensity < 0.8f;
        ToggleLights(headlights, isNight);

        // 2. Féklámpa (Ha fékez a szenzor miatt, VAGY ha a kanyar miatt lassít)
        bool isActuallyBraking = isBraking || (Mathf.Abs(currentSteerAngle) > 15f && rb.linearVelocity.magnitude > 1f);
        ToggleLights(brakeLights, isActuallyBraking);

        // 3. Indexelés (Szögek alapján)
        bool indicateLeft = false;
        bool indicateRight = false;

        if (currentWaypoint != null && !isUTurning)
        {
            // Megnézi, milyen szögben áll a gömb kék nyila a kocsihoz képest
            float wpAngle = Vector3.SignedAngle(transform.forward, currentWaypoint.forward, Vector3.up);
            
            if (wpAngle > 25f) indicateRight = true;
            else if (wpAngle < -25f) indicateLeft = true;
        }
        else if (isUTurning)
        {
            // U-Fordulónál automatikusan balra indexel!
            indicateLeft = true;
        }

        // Villogás logikája (0.4 másodpercenként)
        if (indicateLeft || indicateRight)
        {
            indicatorTimer += Time.fixedDeltaTime;
            if (indicatorTimer >= 0.4f)
            {
                indicatorTimer = 0f;
                indicatorBlinkState = !indicatorBlinkState;
            }
        }
        else
        {
            indicatorBlinkState = false; // Ha egyenesen megy, kikapcsol
        }

        ToggleLights(leftIndicators, indicateLeft && indicatorBlinkState);
        ToggleLights(rightIndicators, indicateRight && indicatorBlinkState);
    }

    void ToggleLights(GameObject[] lightsArray, bool state)
    {
        if (lightsArray == null) return;
        foreach (GameObject lightObj in lightsArray)
        {
            // Az optimalizálás miatt csak akkor szólunk a Unitynek, ha tényleg változtatni kell!
            if (lightObj != null && lightObj.activeSelf != state) 
            {
                lightObj.SetActive(state);
            }
        }
    }

    void ManageWaypoint()
    {
        if (currentWaypoint == null) 
        {
            FindNextWaypoint();
            return;
        }

        Vector3 dirToWp = (currentWaypoint.position - transform.position).normalized;
        float distanceToWp = Vector3.Distance(transform.position, currentWaypoint.position);

        if (distanceToWp < 4f || Vector3.Dot(transform.forward, dirToWp) < 0f)
        {
            FindNextWaypoint();
        }
    }

    void FindNextWaypoint()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, searchRadius, Physics.AllLayers, QueryTriggerInteraction.Collide);
        List<Transform> validWaypoints = new List<Transform>();

        if (lockedPathParent != null)
        {
            foreach (var hit in hitColliders)
            {
                if (hit.CompareTag("Waypoint") && hit.transform != currentWaypoint && hit.transform.parent == lockedPathParent)
                {
                    Vector3 dirToWp = (hit.transform.position - transform.position).normalized;
                    if (Vector3.Dot(transform.forward, dirToWp) > 0.1f) validWaypoints.Add(hit.transform);
                }
            }
        }

        if (validWaypoints.Count == 0)
        {
            lockedPathParent = null; 
            foreach (var hit in hitColliders)
            {
                if (hit.CompareTag("Waypoint") && hit.transform != currentWaypoint)
                {
                    Vector3 dirToWp = (hit.transform.position - transform.position).normalized;
                    if (Vector3.Dot(transform.forward, dirToWp) > 0.1f) 
                    {
                        if (Vector3.Dot(transform.forward, hit.transform.forward) > -0.2f)
                        {
                            validWaypoints.Add(hit.transform);
                        }
                    }
                }
            }
        }

        if (validWaypoints.Count > 0)
        {
            float minDistance = 999f;
            foreach (var wp in validWaypoints)
            {
                float d = Vector3.Distance(transform.position, wp.position);
                if (d < minDistance) minDistance = d;
            }

            List<Transform> closestWaypoints = new List<Transform>();
            foreach (var wp in validWaypoints)
            {
                if (Vector3.Distance(transform.position, wp.position) <= minDistance + 4f)
                {
                    closestWaypoints.Add(wp);
                }
            }

            currentWaypoint = closestWaypoints[Random.Range(0, closestWaypoints.Count)];
            lockedPathParent = currentWaypoint.parent;
        }
    }

    void HandleSteering()
    {
        if (currentWaypoint != null)
        {
            lookAheadPoint = currentWaypoint.position + (currentWaypoint.forward * 10f);

            Vector3 relativeVector = transform.InverseTransformPoint(lookAheadPoint);
            float steerTarget = (relativeVector.x / relativeVector.magnitude) * maxSteerAngle;
            
            if (Mathf.Abs(steerTarget) < 1.5f) steerTarget = 0f;

            currentSteerAngle = Mathf.Lerp(currentSteerAngle, steerTarget, Time.fixedDeltaTime * 4f);
        }
        else
        {
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, 0f, Time.fixedDeltaTime * 4f);
        }

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    void HandleMotor()
    {
        float currentSpeedKMH = rb.linearVelocity.magnitude * 3.6f;
        float appliedMotorForce = 0f;
        float appliedBrakeForce = 0f;

        if (isBraking)
        {
            appliedBrakeForce = brakeForce;
        }
        else
        {
            if (rb.IsSleeping()) rb.WakeUp();

            float allowedSpeed = maxSpeedKMH; 
            if (currentWaypoint != null)
            {
                Vector3 dirToWp = (currentWaypoint.position - transform.position).normalized;
                float angleToWp = Vector3.Angle(transform.forward, dirToWp);
                
                if (angleToWp > 20f || Mathf.Abs(currentSteerAngle) > 15f)
                {
                    allowedSpeed = 15f; 
                }
            }

            if (currentSpeedKMH < allowedSpeed) appliedMotorForce = motorForce;
            else appliedBrakeForce = (allowedSpeed <= 15f) ? brakeForce * 0.5f : 100f; 
        }

        frontLeftCollider.motorTorque = appliedMotorForce;
        frontRightCollider.motorTorque = appliedMotorForce;
        rearLeftCollider.motorTorque = appliedMotorForce;
        rearRightCollider.motorTorque = appliedMotorForce;

        frontLeftCollider.brakeTorque = appliedBrakeForce;
        frontRightCollider.brakeTorque = appliedBrakeForce;
        rearLeftCollider.brakeTorque = appliedBrakeForce;
        rearRightCollider.brakeTorque = appliedBrakeForce;
    }

    void CheckSensors()
    {
        isBraking = false; 
        isWaitingAtLight = false;

        Vector3 origin = transform.position + transform.up * 0.8f + transform.forward * 2.5f; 
        Vector3 boxHalfExtents = new Vector3(1.2f, 0.5f, 0.8f); 

        Quaternion sensorRot = transform.rotation * Quaternion.Euler(0, currentSteerAngle * 0.8f, 0);
        Vector3 sensorDir = sensorRot * Vector3.forward;

        RaycastHit[] hits = Physics.BoxCastAll(origin, boxHalfExtents, sensorDir, sensorRot, sensorLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        foreach (RaycastHit hit in hits)
        {
            Transform rootObj = hit.collider.transform.root; 
            if (rootObj == this.transform.root) continue; 

            if (rootObj.CompareTag("RedLight") || hit.collider.CompareTag("RedLight"))
            {
                if (hit.collider.gameObject.activeInHierarchy)
                {
                    isBraking = true; 
                    isWaitingAtLight = true;
                }
            }
            else if (rootObj.CompareTag("Player") || hit.collider.transform.root.CompareTag("Player"))
            {
                isBraking = true; 
            }
            else if (rootObj.CompareTag("AICar") || hit.collider.transform.root.CompareTag("AICar"))
            {
                if (!isPushingAI)
                {
                    isBraking = true; 
                }
            }
        }
    }

    void HandleAntiJam()
    {
        if (rb.linearVelocity.magnitude < 1f && isBraking && !isWaitingAtLight)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 6f) Destroy(gameObject); 
        }
        else stuckTimer = 0f; 
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Grass") && !isUTurning)
        {
            Destroy(gameObject);
        }

        if (other.CompareTag("DeadEnd") && !isUTurning)
        {
            StartCoroutine(PerformUTurn());
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.transform.root.CompareTag("Player") && col.relativeVelocity.magnitude > 5f) Crash();
        if (col.gameObject.transform.root.CompareTag("AICar")) isPushingAI = true;
    }

    void OnCollisionStay(Collision col)
    {
        if (col.gameObject.transform.root.CompareTag("AICar")) isPushingAI = true;
    }

    void OnCollisionExit(Collision col)
    {
        if (col.gameObject.transform.root.CompareTag("AICar")) isPushingAI = false;
    }

    IEnumerator PerformUTurn()
    {
        isUTurning = true;
        currentWaypoint = null; 
        lockedPathParent = null; 

        float targetYAngle = Mathf.Round(transform.eulerAngles.y / 90f) * 90f - 180f;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float turnedAmount = 0f;
        float turnRate = 90f; 

        while (turnedAmount < 180f)
        {
            if (isCrashed) yield break;

            currentSteerAngle = -40f;
            float rotStep = turnRate * Time.fixedDeltaTime;
            turnedAmount += rotStep;

            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, -rotStep, 0));
            
            Vector3 newPos = rb.position + transform.forward * (15f / 3.6f) * Time.fixedDeltaTime;
            rb.MovePosition(newPos);

            UpdateWheels();
            yield return new WaitForFixedUpdate();
        }

        transform.rotation = Quaternion.Euler(0, targetYAngle, 0);
        currentSteerAngle = 0f;
        isUTurning = false;
        
        FindNextWaypoint(); 
    }

    public void Crash()
    {
        if (isCrashed) return;
        isCrashed = true;
        
        rb.constraints = RigidbodyConstraints.None; 
        rb.AddForce(Vector3.up * 2000f, ForceMode.Impulse); 
        rb.AddTorque(new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f)), ForceMode.Impulse);

        if (bodyMesh != null) bodyMesh.localRotation = Quaternion.identity;
    }

    void UpdateWheels()
    {
        float wheelRollAngle = (rb.linearVelocity.magnitude / 0.35f) * Time.fixedDeltaTime * Mathf.Rad2Deg;

        UpdateSingleWheel(frontLeftCollider, frontLeftMesh, true);
        UpdateSingleWheel(frontRightCollider, frontRightMesh, true);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh, false);
        UpdateSingleWheel(rearRightCollider, rearRightMesh, false);
    }

    void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform, bool isFront)
    {
        if (wheelTransform == null) return;
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }
}