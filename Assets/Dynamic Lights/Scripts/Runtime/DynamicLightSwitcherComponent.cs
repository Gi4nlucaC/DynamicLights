using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicLightSwitcherComponent : MonoBehaviour
{

    [Header("Lighting System")]
    [Tooltip("Riferimento al componente DynamicLightsLevelData per gestire gli scenari di illuminazione")]
    public DynamicLightsLevelData lightmapData;

    [Header("Settings")]
    [Tooltip("Se true, disattiva tutti gli altri GameObjects quando ne attiva uno nuovo")]
    public bool exclusiveActivation = true;

    [Tooltip("Se true, disabilita automaticamente il caricamento delle scene additive per evitare errori")]
    public bool disableSceneLoading = true;

    // Indice corrente attivo (-1 significa nessuno attivo)
    private int currentActiveIndex = -1;
    private bool _isSwitchingLights;
    private List<GameObject> _initialRootObjects = new();

    // Variables for smooth transitions
    private bool _isTransitioning = false;
    private float _transitionDuration = 2.0f;
    private int _fromScenarioIndex = -1;
    private int _toScenarioIndex = -1;
    private float _transitionProgress = 0f;
    
    // Seamless transition variables
    private Material[] _originalMaterials;
    private Renderer[] _sceneRenderers;
    private Dictionary<Renderer, Material[]> _rendererMaterialsCache = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> _blendMaterialsCache = new Dictionary<Renderer, Material[]>();
    
    // Shader references
    private Shader _lightmapBlendShader;
    private Shader _lightmapBlendAdvancedShader;


    public static event System.Action<int> OnSwitchingLights;

    // Commented out project-specific reference for generic plugin compatibility
    // public List<EndlessModeSceneComponent> _sceneComponentReferences;
    
    public int CurrentActiveIndex { get => currentActiveIndex; set => currentActiveIndex = value; }

    void Start()
    {
        // Trova automaticamente il componente DynamicLightsLevelData se non è stato assegnato
        if (lightmapData == null)
        {
            lightmapData = FindObjectOfType<DynamicLightsLevelData>();
        }

        // *** FIX: Inizializza i dati degli scenari di illuminazione se necessario ***
        if (lightmapData != null)
        {
            if (lightmapData.lightingScenariosCount == 0)
            {
                Debug.Log($"[LightSwitcherComponent] Refreshing lighting scenarios data...");
                lightmapData.RefreshLightingScenarios();
            }
        }

        // Commented out gameObjectsToSwitch functionality
        // Verifica che l'array sia della dimensione corretta
        // if (gameObjectsToSwitch.Length != 4)
        // {
        //     System.Array.Resize(ref gameObjectsToSwitch, 4);
        // }

        // Disabilita il caricamento delle scene additive per evitare errori
        if (lightmapData != null && disableSceneLoading)
        {
            if (lightmapData.allowLoadingLightingScenes)
            {
                lightmapData.allowLoadingLightingScenes = false;
            }
        }

        gameObject.scene.GetRootGameObjects(_initialRootObjects);

        // Carica gli shader personalizzati
        LoadCustomShaders();
    }

    /// <summary>
    /// Carica gli shader personalizzati per il blending delle lightmap
    /// </summary>
    private void LoadCustomShaders()
    {
        _lightmapBlendShader = Shader.Find("DynamicLights/LightmapBlend");
        _lightmapBlendAdvancedShader = Shader.Find("DynamicLights/LightmapBlendAdvanced");
        
        if (_lightmapBlendShader == null)
        {
            Debug.LogWarning("[DynamicLightSwitcher] LightmapBlend shader not found. Seamless transitions may not work optimally.");
        }
        
        if (_lightmapBlendAdvancedShader == null)
        {
            Debug.LogWarning("[DynamicLightSwitcher] LightmapBlendAdvanced shader not found. Advanced blending features disabled.");
        }
        else
        {
            Debug.Log("[DynamicLightSwitcher] Custom lightmap blending shaders loaded successfully.");
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0)) TriggerSmoothTransition();
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToScenario(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToScenario(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToScenario(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToScenario(3);
    }
#endif

    /// <summary>
    /// Attiva lo scenario specificato (indice 0-3)
    /// </summary>
    /// <param name="index">Indice dello scenario da attivare (0-3)</param>
    public void SwitchToScenario(int index)
    {
        // Commented out gameObjectsToSwitch functionality
        // Verifica che l'indice sia valido
        // if (index < 0 || index >= gameObjectsToSwitch.Length || _isSwitchingLights)
        // {
        //     return;
        // }
        
        if (_isSwitchingLights)
        {
            return;
        }

        _isSwitchingLights = true;

        Debug.Log($"[LightSwitcherComponent] Switching to scenario {index}");

        // Disattiva tutti i GameObjects se l'attivazione esclusiva è abilitata
        if (exclusiveActivation)
        {
            DeactivateAllGameObjects();
        }

        // Aggiorna l'indice corrente
        currentActiveIndex = index;

        // *** AGGIUNTO: Notifica l'EndlessModeManager del cambio scenario ***
        // Se l'istanza di EndlessModeManager esiste, aggiorna l'indice della scena selezionata
        // if (TacoStudios.Endless.EndlessModeManager.Instance != null)
        // {
        //     TacoStudios.Endless.EndlessModeManager.Instance.SelectedSceneIndex = index;
        // }

        // Commented out gameObjectsToSwitch functionality
        // Attiva il GameObject specificato
        // if (gameObjectsToSwitch[index] != null)
        // {
        //     gameObjectsToSwitch[index].SetActive(true);
        // }

        // Cambia lo scenario di illuminazione
        if (lightmapData != null)
        {
            try
            {
                // *** FIX: Verifica e inizializza i dati se necessario ***
                if (lightmapData.lightingScenariosCount == 0)
                {
                    lightmapData.RefreshLightingScenarios();
                }

                // *** FIX: Per scenario 0, forza la riapplicazione delle lightmap ***
                /* if (index == 0 || currentActiveIndex != index)
                { */
                // Pulisci lo scenario corrente per forzare il reload
                string previousScenario = lightmapData.currentLightingScenario;
                lightmapData.currentLightingScenario = "";
                lightmapData.LoadLightingScenario(index);
                Debug.Log($"[LightSwitcherComponent] Applied lighting scenario {index}");
                //}
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LightSwitcherComponent] Error loading lighting scenario {index}: {e.Message}");
            }
        }

        // Chiama l'evento personalizzato se necessario
        OnScenarioChanged(index);

        _isSwitchingLights = false;
    }

    /// <summary>
    /// Disattiva tutti i GameObjects nell'array
    /// </summary>
    public void DeactivateAllGameObjects()
    {
        // Commented out gameObjectsToSwitch functionality
        // for (int i = 0; i < gameObjectsToSwitch.Length; i++)
        // {
        //     if (gameObjectsToSwitch[i] != null && gameObjectsToSwitch[i].activeInHierarchy)
        //     {
        //         GameObject[] currentRoots = gameObject.scene.GetRootGameObjects();
        //
        //         for (int j = 0; j < currentRoots.Length; j++)
        //         {
        //             GameObject go = currentRoots[j];
        //             // Commented out gameObjectsToSwitch functionality
        //             // if (!_initialRootObjects.Contains(go) && go != gameObjectsToSwitch[i] && !go.name.Contains("Camera"))
        //             // {
        //             //     Debug.Log($"Rende '{go.name}' figlio di '{gameObjectsToSwitch[i].name}'");
        //             //     go.transform.SetParent(gameObjectsToSwitch[i].transform);
        //             // }
        //         }
        //
        //         // gameObjectsToSwitch[i].SetActive(false);
        //     }
        // }

        currentActiveIndex = -1;
    }

    /// <summary>
    /// Restituisce l'indice dello scenario attualmente attivo (-1 se nessuno)
    /// </summary>
    /// <returns>Indice corrente o -1</returns>
    public int GetCurrentActiveIndex()
    {
        return currentActiveIndex;
    }

    /// <summary>
    /// Esegue una transizione fluida tra due scenari di illuminazione
    /// </summary>
    /// <param name="fromIndex">Indice dello scenario di partenza (0-3)</param>
    /// <param name="toIndex">Indice dello scenario di destinazione (0-3)</param>
    /// <param name="duration">Durata della transizione in secondi (default: 2.0f)</param>
    public void TransitionToScenario(int fromIndex, int toIndex, float duration = 2.0f)
    {
        // Validazione degli indici
        if (lightmapData == null)
        {
            Debug.LogWarning("[DynamicLightSwitcher] No lightmap data reference found.");
            return;
        }

        if (fromIndex < 0 || fromIndex >= lightmapData.lightingScenariosCount ||
            toIndex < 0 || toIndex >= lightmapData.lightingScenariosCount)
        {
            Debug.LogWarning("[DynamicLightSwitcher] Invalid scenario indices for transition.");
            return;
        }

        if (_isTransitioning)
        {
            Debug.LogWarning("[DynamicLightSwitcher] Transition already in progress.");
            return;
        }

        _fromScenarioIndex = fromIndex;
        _toScenarioIndex = toIndex;
        _transitionDuration = Mathf.Max(0.1f, duration);
        _transitionProgress = 0f;
        _isTransitioning = true;

        Debug.Log($"[DynamicLightSwitcher] Starting smooth transition from scenario {fromIndex} to {toIndex} over {duration} seconds");

        StartCoroutine(SmoothTransitionCoroutine());
    }

    /// <summary>
    /// Avvia una transizione fluida quando viene premuto il tasto "0"
    /// Transiziona dal scenario corrente al prossimo disponibile
    /// </summary>
    public void TriggerSmoothTransition()
    {
        if (lightmapData == null || lightmapData.lightingScenariosCount == 0)
        {
            Debug.LogWarning("[DynamicLightSwitcher] No lighting scenarios available for transition.");
            return;
        }

        int nextScenario = (currentActiveIndex + 1) % lightmapData.lightingScenariosCount;
        TransitionToScenario(currentActiveIndex >= 0 ? currentActiveIndex : 0, nextScenario);
    }

    /// <summary>
    /// Coroutine che gestisce la transizione fluida tra scenari
    /// </summary>
    private IEnumerator SmoothTransitionCoroutine()
    {
        while (_transitionProgress < 1.0f)
        {
            _transitionProgress += Time.deltaTime / _transitionDuration;
            _transitionProgress = Mathf.Clamp01(_transitionProgress);

            // Interpola le lightmap utilizzando il sistema esistente
            // Nota: Unity non supporta nativamente il lerp delle lightmap in runtime
            // Questa è una implementazione concettuale che potrebbe richiedere personalizzazioni
            LerpLightmaps(_fromScenarioIndex, _toScenarioIndex, _transitionProgress);

            yield return null;
        }

        // Completa la transizione applicando completamente lo scenario finale
        RestoreOriginalMaterials();
        SwitchToScenario(_toScenarioIndex);
        
        _isTransitioning = false;
        currentActiveIndex = _toScenarioIndex;
        
        Debug.Log($"[DynamicLightSwitcher] Seamless transition completed to scenario {_toScenarioIndex}");
    }

    /// <summary>
    /// Implementa una transizione seamless reale tra lightmap usando material blending
    /// </summary>
    private void LerpLightmaps(int fromIndex, int toIndex, float t)
    {
        if (lightmapData == null) return;

        try
        {
            // Inizializza i renderer se necessario
            if (_sceneRenderers == null)
            {
                CacheSceneRenderers();
            }

            // Applica il blending seamless
            ApplySeamlessLightmapTransition(fromIndex, toIndex, t);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DynamicLightSwitcher] Error during seamless lightmap transition: {e.Message}");
        }
    }

    /// <summary>
    /// Crea una cache di tutti i renderer nella scena per le transizioni seamless
    /// </summary>
    private void CacheSceneRenderers()
    {
        // Trova tutti i renderer che utilizzano lightmap
        List<Renderer> renderers = new List<Renderer>();
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        
        foreach (GameObject root in rootObjects)
        {
            Renderer[] childRenderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                // Include solo renderer che hanno lightmap
                if (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length)
                {
                    renderers.Add(renderer);
                    
                    // Cache dei materiali originali
                    if (!_rendererMaterialsCache.ContainsKey(renderer))
                    {
                        _rendererMaterialsCache[renderer] = renderer.materials;
                    }
                }
            }
        }
        
        _sceneRenderers = renderers.ToArray();
        Debug.Log($"[DynamicLightSwitcher] Cached {_sceneRenderers.Length} lightmapped renderers for seamless transitions");
    }

    /// <summary>
    /// Applica la transizione seamless modificando i parametri dei materiali
    /// </summary>
    private void ApplySeamlessLightmapTransition(int fromIndex, int toIndex, float t)
    {
        if (lightmapData == null || _sceneRenderers == null) return;

        // Ottieni i dati dei due scenari
        var fromScenario = lightmapData.lightingScenariosData[fromIndex];
        var toScenario = lightmapData.lightingScenariosData[toIndex];

        // Applica la transizione per ogni renderer
        foreach (Renderer renderer in _sceneRenderers)
        {
            if (renderer == null) continue;

            // Trova le lightmap corrispondenti per questo renderer
            int lightmapIndex = renderer.lightmapIndex;
            
            if (lightmapIndex >= 0 && 
                lightmapIndex < fromScenario.lightmaps.Length && 
                lightmapIndex < toScenario.lightmaps.Length)
            {
                ApplyBlendedLightmap(renderer, fromScenario, toScenario, lightmapIndex, t);
            }
        }

        // Interpola anche i parametri globali di illuminazione
        InterpolateGlobalLighting(fromScenario, toScenario, t);
    }

    /// <summary>
    /// Applica il blending delle lightmap su un singolo renderer usando shader personalizzati
    /// </summary>
    private void ApplyBlendedLightmap(Renderer renderer, DynamicLightsScenarioData fromScenario, DynamicLightsScenarioData toScenario, int lightmapIndex, float t)
    {
        // Crea o recupera i materiali di blending per questo renderer
        if (!_blendMaterialsCache.ContainsKey(renderer))
        {
            CreateBlendMaterials(renderer);
        }
        
        Material[] blendMaterials = _blendMaterialsCache[renderer];
        
        for (int i = 0; i < blendMaterials.Length; i++)
        {
            Material blendMat = blendMaterials[i];
            
            if (blendMat != null && fromScenario.lightmaps[lightmapIndex] != null && toScenario.lightmaps[lightmapIndex] != null)
            {
                // Imposta le texture delle lightmap
                blendMat.SetTexture("_LightMap1", fromScenario.lightmaps[lightmapIndex]);
                blendMat.SetTexture("_LightMap2", toScenario.lightmaps[lightmapIndex]);
                
                // Imposta le directional lightmap se disponibili
                if (fromScenario.lightmapsDir != null && fromScenario.lightmapsDir.Length > lightmapIndex)
                {
                    blendMat.SetTexture("_LightMapDir1", fromScenario.lightmapsDir[lightmapIndex]);
                }
                if (toScenario.lightmapsDir != null && toScenario.lightmapsDir.Length > lightmapIndex)
                {
                    blendMat.SetTexture("_LightMapDir2", toScenario.lightmapsDir[lightmapIndex]);
                }
                
                // Imposta il fattore di blending
                blendMat.SetFloat("_BlendFactor", t);
                
                // Applica un blending smoothing per transizioni più naturali
                float smoothing = Mathf.Sin(t * Mathf.PI) * 0.2f; // Smooth curve
                if (blendMat.HasProperty("_BlendSmoothing"))
                {
                    blendMat.SetFloat("_BlendSmoothing", smoothing);
                }
            }
        }
        
        // Applica i materiali di blending al renderer
        renderer.materials = blendMaterials;
    }

    /// <summary>
    /// Crea materiali di blending personalizzati per un renderer
    /// </summary>
    private void CreateBlendMaterials(Renderer renderer)
    {
        Material[] originalMaterials = _rendererMaterialsCache[renderer];
        Material[] blendMaterials = new Material[originalMaterials.Length];
        
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMat = originalMaterials[i];
            Material blendMat = null;
            
            // Scegli lo shader appropriato
            Shader targetShader = _lightmapBlendAdvancedShader ?? _lightmapBlendShader;
            
            if (targetShader != null)
            {
                // Crea un nuovo materiale con lo shader di blending
                blendMat = new Material(targetShader);
                blendMat.name = $"{originalMat.name}_BlendTransition";
                
                // Copia le proprietà base dal materiale originale
                CopyMaterialProperties(originalMat, blendMat);
            }
            else
            {
                // Fallback: usa il materiale originale
                blendMat = new Material(originalMat);
                Debug.LogWarning($"[DynamicLightSwitcher] No custom shader available, using fallback for {originalMat.name}");
            }
            
            blendMaterials[i] = blendMat;
        }
        
        _blendMaterialsCache[renderer] = blendMaterials;
    }

    /// <summary>
    /// Copia le proprietà compatibili da un materiale ad un altro
    /// </summary>
    private void CopyMaterialProperties(Material source, Material destination)
    {
        // Copia texture principali
        if (source.HasProperty("_MainTex") && destination.HasProperty("_MainTex"))
        {
            destination.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            destination.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
            destination.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
        }
        
        // Copia colore principale
        if (source.HasProperty("_Color") && destination.HasProperty("_Color"))
        {
            destination.SetColor("_Color", source.GetColor("_Color"));
        }
        
        // Copia normal map
        if (source.HasProperty("_BumpMap") && destination.HasProperty("_BumpMap"))
        {
            destination.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
        }
        
        // Copia proprietà metallic/smoothness
        if (source.HasProperty("_Metallic") && destination.HasProperty("_Metallic"))
        {
            destination.SetFloat("_Metallic", source.GetFloat("_Metallic"));
        }
        
        if (source.HasProperty("_Glossiness") && destination.HasProperty("_Glossiness"))
        {
            destination.SetFloat("_Glossiness", source.GetFloat("_Glossiness"));
        }
        else if (source.HasProperty("_Smoothness") && destination.HasProperty("_Glossiness"))
        {
            destination.SetFloat("_Glossiness", source.GetFloat("_Smoothness"));
        }
        
        // Copia occlusion map
        if (source.HasProperty("_OcclusionMap") && destination.HasProperty("_OcclusionMap"))
        {
            destination.SetTexture("_OcclusionMap", source.GetTexture("_OcclusionMap"));
        }
        
        if (source.HasProperty("_OcclusionStrength") && destination.HasProperty("_OcclusionStrength"))
        {
            destination.SetFloat("_OcclusionStrength", source.GetFloat("_OcclusionStrength"));
        }
    }

    /// <summary>
    /// Interpola i parametri globali di illuminazione tra due scenari
    /// </summary>
    private void InterpolateGlobalLighting(DynamicLightsScenarioData fromScenario, DynamicLightsScenarioData toScenario, float t)
    {
        // Interpola i parametri ambientali
        if (fromScenario.reflectionProbes != null && toScenario.reflectionProbes != null)
        {
            // Interpolazione delle reflection probe (semplificata)
            // In un sistema completo, dovresti interpolare l'intensità e altre proprietà
            
            if (t < 0.5f)
            {
                // Applica le reflection probe del primo scenario con fade
                ApplyReflectionProbes(fromScenario, 1.0f - t * 2.0f);
            }
            else
            {
                // Applica le reflection probe del secondo scenario con fade
                ApplyReflectionProbes(toScenario, (t - 0.5f) * 2.0f);
            }
        }

        // Interpola skybox e ambient lighting se disponibili
        if (fromScenario.skyboxMaterial != null && toScenario.skyboxMaterial != null)
        {
            // Crossfade tra skybox
            if (t < 0.5f)
            {
                RenderSettings.skybox = fromScenario.skyboxMaterial;
                RenderSettings.ambientIntensity = Mathf.Lerp(fromScenario.ambientIntensity, toScenario.ambientIntensity, t);
            }
            else
            {
                RenderSettings.skybox = toScenario.skyboxMaterial;
                RenderSettings.ambientIntensity = Mathf.Lerp(fromScenario.ambientIntensity, toScenario.ambientIntensity, t);
            }
        }
    }

    /// <summary>
    /// Applica le reflection probe con intensità specificata
    /// </summary>
    private void ApplyReflectionProbes(DynamicLightsScenarioData scenario, float intensity)
    {
        // Implementazione semplificata per le reflection probe
        // In un sistema completo, modificheresti l'intensità di ogni probe
        ReflectionProbe[] probes = FindObjectsOfType<ReflectionProbe>();
        foreach (ReflectionProbe probe in probes)
        {
            probe.intensity = intensity;
        }
    }

    /// <summary>
    /// Ripristina i materiali originali dopo la transizione e pulisce la cache
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        if (_sceneRenderers == null) return;

        foreach (Renderer renderer in _sceneRenderers)
        {
            if (renderer != null && _rendererMaterialsCache.ContainsKey(renderer))
            {
                // Ripristina i materiali originali
                renderer.materials = _rendererMaterialsCache[renderer];
                
                // Distruggi i materiali di blending temporanei
                if (_blendMaterialsCache.ContainsKey(renderer))
                {
                    Material[] blendMaterials = _blendMaterialsCache[renderer];
                    foreach (Material mat in blendMaterials)
                    {
                        if (mat != null)
                        {
                            DestroyImmediate(mat);
                        }
                    }
                    _blendMaterialsCache.Remove(renderer);
                }
            }
        }
        
        Debug.Log("[DynamicLightSwitcher] Original materials restored and blend materials cleaned up.");
    }

    /// <summary>
    /// Verifica se un determinato scenario è attivo
    /// </summary>
    /// <param name="index">Indice da verificare</param>
    /// <returns>True se il scenario è attivo</returns>
    public bool IsScenarioActive(int index)
    {
        return currentActiveIndex == index;
    }

    /// <summary>
    /// Metodo chiamato quando cambia lo scenario - può essere sovrascritto nelle classi derivate
    /// </summary>
    /// <param name="newIndex">Nuovo indice attivo</param>
    protected virtual void OnScenarioChanged(int newIndex)
    {

    }

    /// <summary>
    /// Metodo pubblico per cambiare scenario tramite codice (utile per UI o altri sistemi)
    /// </summary>
    /// <param name="scenarioNumber">Numero dello scenario (1-4)</param>
    public void SwitchToScenarioByNumber(int scenarioNumber)
    {
        SwitchToScenario(scenarioNumber - 1);
    }

    // Metodi per Unity Events se necessario
    public void SwitchToScenario1() => SwitchToScenario(0);
    public void SwitchToScenario2() => SwitchToScenario(1);
    public void SwitchToScenario3() => SwitchToScenario(2);
    public void SwitchToScenario4() => SwitchToScenario(3);

    void OnValidate()
    {
        // Commented out gameObjectsToSwitch functionality
        // Assicurati che l'array sia sempre di 4 elementi nell'inspector
        // if (gameObjectsToSwitch.Length != 4)
        // {
        //     System.Array.Resize(ref gameObjectsToSwitch, 4);
        // }
    }
}
