using UnityEngine;

public class AdvancedCityGenerator : MonoBehaviour
{
    [Header("Alap Elemek (Utak és Víz)")]
    public GameObject straightRoadPrefab; 
    public GameObject intersection4WayPrefab; 
    public GameObject intersection3WayPrefab; 
    public GameObject waterPrefab;    

    [Header("Híd Rendszer")]
    public GameObject bridgePrefab;       
    public GameObject bridgeRampPrefab;   
    [Tooltip("Az EGÉSZ híd emelése/süllyesztése")]
    public float bridgeGlobalYOffset = 0.0f;
    [Tooltip("CSAK a híd közepének emelése")]
    public float bridgeCenterHeight = 4.0f;  
    [Tooltip("A híd közepének nyújtása CSAK 1 irányba (X, Y vagy Z)")]
    public Vector3 bridgeCenterStretch = new Vector3(1f, 1f, 1f);   

    [Header("Talaj (Fű)")]
    public GameObject grassPrefab;        
    public float grassPadding = 40f;      

    [Header("Városi Zóna (Folyótól balra)")]
    public GameObject[] cityBuildingPrefabs; 

    [Header("Falusi Zóna (Folyótól jobbra)")]
    public GameObject[] villagePrefabs; 
    [Range(0f, 1f)]
    public float villageDensity = 0.85f;  

    [Header("Finomhangolás (Méretek)")]
    public int mapWidth = 30;  
    public int mapLength = 20; 
    public int blockSize = 4; 
    public float cityScale = 1.3f;        
    public float villageScale = 0.8f;     
    public float villageHeightOffset = -0.5f; 
    public float intersectionScale = 1.0f;
    [Tooltip("Arányos nagyítás (Hagyd 1-en, ha nyújtod a hidat!)")]
    public float bridgeScale = 1.0f; 
    public float offsetTowardsRoad = 8f;  

    [Header("FOLYÓ ÉS HÍD TÁVOLSÁGOK")]
    public float riverWidthInBlocks = 1.67f;
    public float bridgePartSpacing = 40f; 

    [Header("Modell Javítások (Forgatás)")]
    public Vector3 roadRotationFix = new Vector3(-90f, 0f, 0f);
    public Vector3 waterRotationFix = new Vector3(0f, 0f, 0f);
    public Vector3 bridgeCenterRotationFix = new Vector3(-90f, 90f, 0f); 
    public Vector3 bridgeRampRotationFix = new Vector3(-90f, 180f, 0f);    
    public Vector3 intersection3WayFix = new Vector3(-90f, -90f, 0f);     
    public Vector3 buildingRotationFix = new Vector3(0f, 0f, 0f);

    [Header("Pozíció Javítások (Eltolás / Nudge)")]
    public Vector3 intersection3WayOffset = Vector3.zero;
    public Vector3 bridgeRampOffset = Vector3.zero;
    public Vector3 bridgeCenterOffset = Vector3.zero;

    [ContextMenu("1. OKOS Pálya Generálása (Nyújtott Híd V12)")]
    public void GenerateCity()
    {
        ClearCity();

        if (straightRoadPrefab == null) return;

        float tileSize = CalculateModelSize(straightRoadPrefab, roadRotationFix);

        // --- 0. FŰ (TALAJ) ---
        float centerX = ((mapWidth - 1) * tileSize) / 2f;
        float centerZ = ((mapLength - 1) * tileSize) / 2f;
        
        if (grassPrefab != null)
        {
            Vector3 grassPos = new Vector3(centerX, -0.5f, centerZ); 
            GameObject grass = Instantiate(grassPrefab, grassPos, Quaternion.identity, transform);
            grass.name = "Alap_Fuu_Talaj";
            grass.transform.localScale = new Vector3((mapWidth * tileSize) + grassPadding, 0.4f, (mapLength * tileSize) + grassPadding);
        }

        // --- OKOS RÁCS MATEMATIKA ---
        int leftRoadX = (mapWidth / 2 / blockSize) * blockSize;
        int riverTiles = Mathf.Max(2, Mathf.RoundToInt(riverWidthInBlocks * blockSize));
        
        int rightRoadX = leftRoadX + riverTiles;
        if (rightRoadX >= mapWidth - 1) leftRoadX = mapWidth - riverTiles - 2; 
        rightRoadX = leftRoadX + riverTiles;

        // --- 1. VÍZ ---
        if (waterPrefab != null)
        {
            float riverCenterX = (leftRoadX + rightRoadX) / 2f * tileSize;
            Vector3 waterPos = new Vector3(riverCenterX, -0.2f, centerZ); 
            GameObject water = Instantiate(waterPrefab, waterPos, Quaternion.Euler(waterRotationFix), transform);
            water.name = "Dinamikus_Mega_Folyo";

            float waterBaseSize = CalculateModelSize(waterPrefab, waterRotationFix);
            if (waterBaseSize > 0)
            {
                float riverWidthTilesVisual = (rightRoadX - leftRoadX) - 1; 
                float scaleX = (riverWidthTilesVisual * tileSize) / waterBaseSize;
                float scaleZ = ((mapLength * tileSize) + grassPadding) / waterBaseSize;
                water.transform.localScale = new Vector3(scaleX * 1.05f, 1f, scaleZ);
            }
        }

        // --- 2. VÁROS GENERÁLÁSA ---
        for (int x = 0; x < mapWidth; x++)
        {
            int effectiveX = x;
            if (x >= rightRoadX) effectiveX = x - rightRoadX;

            for (int z = 0; z < mapLength; z++)
            {
                Vector3 position = new Vector3(x * tileSize, 0, z * tileSize);
                bool isVerticalRoad = (effectiveX % blockSize == 0);
                bool isHorizontalRoad = (z % blockSize == 0);

                // --- FOLYÓ ZÓNA ÉS HÍD ---
                if (x > leftRoadX && x < rightRoadX)
                {
                    if (isHorizontalRoad && x == leftRoadX + 1) 
                    {
                        float riverCenterX = (leftRoadX + rightRoadX) / 2f * tileSize;
                        Vector3 baseBridgePos = new Vector3(riverCenterX, bridgeGlobalYOffset, z * tileSize);

                        // 1. Híd Közepe (Itt történik a nyújtás!)
                        Quaternion bridgeRot = Quaternion.Euler(bridgeCenterRotationFix);
                        Vector3 centerLocalOffset = bridgeRot * bridgeCenterOffset;
                        Vector3 bridgePos = baseBridgePos + new Vector3(0, bridgeCenterHeight, 0) + centerLocalOffset;
                        GameObject centerBridge = SpawnSmartPrefab(bridgePrefab, bridgePos, $"Hid_Kozepe_{z}", tileSize, true, bridgeRot, bridgeScale, 0f);
                        
                        if (centerBridge != null)
                        {
                            // Csak a híd közepét szorozzuk be a nyújtó Vector3-mal!
                            centerBridge.transform.localScale = new Vector3(
                                centerBridge.transform.localScale.x * bridgeCenterStretch.x,
                                centerBridge.transform.localScale.y * bridgeCenterStretch.y,
                                centerBridge.transform.localScale.z * bridgeCenterStretch.z
                            );
                        }

                        // 2. Bal Rámpa
                        Vector3 leftRampEuler = bridgeRampRotationFix;
                        leftRampEuler.y += 90f; 
                        Quaternion leftRampRot = Quaternion.Euler(leftRampEuler);
                        Vector3 leftLocalOffset = leftRampRot * bridgeRampOffset;
                        SpawnSmartPrefab(bridgeRampPrefab, baseBridgePos + new Vector3(-bridgePartSpacing, 0, 0) + leftLocalOffset, $"Rampa_Fel_{z}", tileSize, true, leftRampRot, bridgeScale, 0f);

                        // 3. Jobb Rámpa
                        Vector3 rightRampEuler = bridgeRampRotationFix;
                        rightRampEuler.y -= 90f; 
                        Quaternion rightRampRot = Quaternion.Euler(rightRampEuler);
                        Vector3 rightLocalOffset = rightRampRot * bridgeRampOffset;
                        SpawnSmartPrefab(bridgeRampPrefab, baseBridgePos + new Vector3(bridgePartSpacing, 0, 0) + rightLocalOffset, $"Rampa_Le_{z}", tileSize, true, rightRampRot, bridgeScale, 0f);
                    }
                    continue; 
                }

                // --- KERESZTEZŐDÉSEK ---
                if (isVerticalRoad && isHorizontalRoad)
                {
                    bool isTopEdge = (z == mapLength - 1);
                    bool isBottomEdge = (z == 0);
                    bool isLeftEdge = (effectiveX == 0 && x <= leftRoadX);
                    bool isRightEdge = (x >= mapWidth - 1);
                    
                    int edges = (isTopEdge ? 1 : 0) + (isBottomEdge ? 1 : 0) + (isLeftEdge ? 1 : 0) + (isRightEdge ? 1 : 0);

                    if (edges >= 1 && intersection3WayPrefab != null)
                    {
                        Vector3 tEuler = intersection3WayFix;
                        if (isTopEdge) tEuler.y += 180f;
                        if (isBottomEdge) tEuler.y += 0f;
                        if (isLeftEdge) tEuler.y += 90f;
                        if (isRightEdge) tEuler.y -= 90f;
                        
                        Quaternion tRot = Quaternion.Euler(tEuler);
                        Vector3 localOffset = tRot * intersection3WayOffset;

                        SpawnSmartPrefab(intersection3WayPrefab, position + localOffset, $"T_Elagazas_{x}_{z}", tileSize, true, tRot, intersectionScale, 0f);
                    }
                    else if (intersection4WayPrefab != null)
                    {
                        SpawnSmartPrefab(intersection4WayPrefab, position, $"Keresztezodes_{x}_{z}", tileSize, true, Quaternion.Euler(roadRotationFix), intersectionScale, 0f);
                    }
                    continue;
                }
                
                // --- SIMA UTAK ---
                else if (isVerticalRoad)
                {
                    SpawnSmartPrefab(straightRoadPrefab, position, $"Utca_V_{x}_{z}", tileSize, true, Quaternion.Euler(roadRotationFix), 1f, 0f);
                    continue;
                }
                else if (isHorizontalRoad)
                {
                    Vector3 horizEuler = roadRotationFix;
                    horizEuler.y += 90f; 
                    SpawnSmartPrefab(straightRoadPrefab, position, $"Utca_H_{x}_{z}", tileSize, true, Quaternion.Euler(horizEuler), 1f, 0f);
                    continue;
                }

                // --- ÉPÜLETEK ---
                bool roadLeft = ((effectiveX - 1) % blockSize == 0) || (x == leftRoadX + 1);
                bool roadRight = ((effectiveX + 1) % blockSize == 0);
                bool roadDown = ((z - 1) % blockSize == 0);
                bool roadUp = ((z + 1) % blockSize == 0);

                Vector3 offset = Vector3.zero;
                float faceAngleY = Random.Range(0, 4) * 90f;

                if (roadLeft) { offset.x -= offsetTowardsRoad; faceAngleY = -90f; }
                else if (roadRight) { offset.x += offsetTowardsRoad; faceAngleY = 90f; }
                else if (roadDown) { offset.z -= offsetTowardsRoad; faceAngleY = 180f; }
                else if (roadUp) { offset.z += offsetTowardsRoad; faceAngleY = 0f; }

                Vector3 finalPosition = position + offset;
                Vector3 buildingEuler = buildingRotationFix;
                buildingEuler.y += faceAngleY;

                if (x < leftRoadX && cityBuildingPrefabs.Length > 0)
                {
                    GameObject prefab = cityBuildingPrefabs[Random.Range(0, cityBuildingPrefabs.Length)];
                    SpawnSmartPrefab(prefab, finalPosition, $"Panel_{x}_{z}", tileSize, false, Quaternion.Euler(buildingEuler), cityScale, 0f);
                }
                else if (x >= rightRoadX && villagePrefabs.Length > 0 && Random.value < villageDensity)
                {
                    GameObject prefab = villagePrefabs[Random.Range(0, villagePrefabs.Length)];
                    SpawnSmartPrefab(prefab, finalPosition, $"Falu_{x}_{z}", tileSize, false, Quaternion.Euler(buildingEuler), villageScale, villageHeightOffset);
                }
            }
        }
    }

    [ContextMenu("2. Pálya Törlése")]
    public void ClearCity()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private float CalculateModelSize(GameObject prefab, Vector3 rotationFix)
    {
        if (prefab == null) return 10f;
        GameObject temp = Instantiate(prefab);
        temp.transform.rotation = Quaternion.Euler(rotationFix);
        Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
        float maxSize = 10f; 
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            maxSize = Mathf.Max(bounds.size.x, bounds.size.z);
        }
        DestroyImmediate(temp);
        return maxSize;
    }

    private GameObject SpawnSmartPrefab(GameObject prefab, Vector3 pos, string name, float targetSize, bool isRoadOrWater, Quaternion rot, float customScale, float yOffset)
    {
        if (prefab == null) return null;
        pos.y += yOffset; 
        GameObject obj = Instantiate(prefab, pos, rot, transform);
        obj.name = name;

        if (!isRoadOrWater)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                float currentMaxSize = Mathf.Max(bounds.size.x, bounds.size.z);

                if (currentMaxSize > 0)
                {
                    float scaleFactor = ((targetSize * 0.9f) / currentMaxSize) * customScale;
                    obj.transform.localScale = obj.transform.localScale * scaleFactor;
                }
            }
        }
        else
        {
            obj.transform.localScale *= customScale;
        }
        return obj;
    }
}