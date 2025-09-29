using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Collections;
using System.Reflection;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[ExecuteInEditMode]
public class DynamicLightsLevelData : MonoBehaviour
{

    [System.Serializable]
    public class RendererInfo
    {
        public Renderer renderer;
        public int transformHash;
        public int meshHash;
        public string name;
        public int lightmapIndex;
        public Vector4 lightmapScaleOffset;
        public bool isStatic;
        public bool isActive;
    }

    public bool latestBuildHasReltimeLights;
    [Tooltip("Enable this if you want to allow the script to load a lighting scene additively. This is useful when the scene contains a light set to realtime or mixed mode or reflection probes. If you're managing the scenes loading yourself you should disable it.")]
    public bool allowLoadingLightingScenes = true;
    [Tooltip("Enable this if you want to use different lightmap resolutions in your different lighting scenarios. In that case you'll have to disable Static Batching in the Player Settings. When disabled, Static Batching can be used but all your lighting scenarios need to use the same lightmap resolution.")]
    public bool applyLightmapScaleAndOffset = true;
    [Tooltip("Enable this to apply reflection probes when switching lighting scenarios.")]
    public bool applyReflectionProbes = true;
    [Tooltip("Enable this to apply skybox and ambient lighting when switching lighting scenarios.")]
    public bool applySkyboxAndAmbient = true;

    [SerializeField]
    public List<DynamicLightsScenarioData> lightingScenariosData;

#if UNITY_EDITOR
    [SerializeField]
	public List<SceneAsset> lightingScenariosScenes;
#endif
    public string currentLightingScenario = "";
    public string previousLightingScenario = "";

    private Coroutine m_SwitchSceneCoroutine;

    [SerializeField]
    public int lightingScenariosCount;

    //TODO : enable logs only when verbose enabled
    public bool verbose = false;

    static string messagePrefix = "Dynamic Lights - ";

    public void LoadLightingScenario(int index)
    {
        if (lightingScenariosData == null || lightingScenariosData.Count == 0)
        {
            Debug.LogError(messagePrefix + "No lighting scenarios data available!");
            return;
        }

        if (index < 0 || index >= lightingScenariosData.Count)
        {
            Debug.LogError(messagePrefix + $"Lighting scenario index {index} is out of range! Available scenarios: 0-{lightingScenariosData.Count - 1}");
            return;
        }

        var dataToLoad = lightingScenariosData[index];
        if (dataToLoad == null)
        {
            Debug.LogError(messagePrefix + $"Lighting scenario data at index {index} is null!");
            return;
        }

        LoadLightingScenarioData(dataToLoad);
    }

    public void LoadLightingScenario(string name)
    {
        var data = lightingScenariosData.Find(x => x.lightingSceneName.Equals(name));
        if (data == null)
        {
            Debug.LogError(messagePrefix + "Can't find lighting scenario with name (case sensitive) " + name);
            return;
        }
        LoadLightingScenario(data);
    }

    public void LoadLightingScenario(DynamicLightsScenarioData data)
    {
        if (data == null)
        {
            Debug.LogError(messagePrefix + "Cannot load null lighting scenario data!");
            return;
        }

        if (data.lightingSceneName != currentLightingScenario)
        {
            previousLightingScenario = currentLightingScenario;

            currentLightingScenario = data.lightingSceneName;

            LightmapSettings.lightmapsMode = data.lightmapsMode;

            if (allowLoadingLightingScenes)
            {
                string previousSceneName = lightingScenariosData?.Find(x => x?.lightingSceneName == previousLightingScenario)?.lightingSceneName;
                string currentSceneName = lightingScenariosData?.Find(x => x?.lightingSceneName == currentLightingScenario)?.lightingSceneName;

                // Only start the coroutine if we have valid scene names or if we need to unload something
                if (!string.IsNullOrEmpty(previousSceneName) || !string.IsNullOrEmpty(currentSceneName))
                {
                    m_SwitchSceneCoroutine = StartCoroutine(SwitchSceneCoroutine(previousSceneName, currentSceneName));
                }
            }

            var newLightmaps = LoadLightmaps(data);

            ApplyDataRendererInfo(data.rendererInfos);

            LightmapSettings.lightmaps = newLightmaps;

            LoadLightProbes(data);

            // Apply reflection probes and skybox
            if (applyReflectionProbes)
                ApplyReflectionProbes(data);

            if (applySkyboxAndAmbient)
                ApplySkyboxAndAmbient(data);
        }
    }

    public void LoadLightingScenarioData(DynamicLightsScenarioData data)
    {
        LoadLightingScenario(data);
    }

    public void LoadAssetBundleByName(string name)
    {
        AssetBundle assetBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/" + name);
        Debug.Log(assetBundle == null ? messagePrefix + "Failed to load Asset Bundle" : "Dynamic Lights - Asset bundle loaded successfully");
        assetBundle.LoadAllAssets();
    }

    public void RefreshLightingScenarios()
    {
        /* lightingScenariosData = Resources.FindObjectsOfTypeAll<DynamicLightsScenarioData>().Where(x => x.geometrySceneName == gameObject.scene.name).ToList();
        Debug.Log(messagePrefix + "Loaded " + lightingScenariosData.Count + " suitable lighting scenarios.");
        foreach (var scene in lightingScenariosData)
        {
            Debug.Log(scene.lightingSceneName);
        } */
    }


#if UNITY_EDITOR

    // In editor only we cache the baked probe data when entering playmode, and reset it on exit
    // This negates runtime changes that the Dynamic Lights library creates in the lighting asset loaded into the starting scene 

    UnityEngine.Rendering.SphericalHarmonicsL2[] cachedBakedProbeData = null;

    public void OnEnteredPlayMode_EditorOnly()
    {
        if(LightmapSettings.lightProbes != null)
        {
            cachedBakedProbeData = LightmapSettings.lightProbes.bakedProbes;
            Debug.Log(messagePrefix+"Caching editor lightProbes");
        }
    }

    public void OnExitingPlayMode_EditorOnly()
    {
        // Only do this cache restore if we have probe data of matching length
        if (cachedBakedProbeData != null && LightmapSettings.lightProbes.bakedProbes.Length == cachedBakedProbeData.Length)
        {
            LightmapSettings.lightProbes.bakedProbes = cachedBakedProbeData;
            Debug.Log(messagePrefix+"Restoring editor lightProbes");
        }
    }

#endif

    IEnumerator SwitchSceneCoroutine(string sceneToUnload, string sceneToLoad)
    {
        AsyncOperation unloadop = null;
        AsyncOperation loadop = null;

        // Only attempt to unload if the scene name is valid and the scene actually exists
        if (!string.IsNullOrEmpty(sceneToUnload) && sceneToUnload != sceneToLoad)
        {
            // Check if the scene is actually loaded before trying to unload it
            UnityEngine.SceneManagement.Scene sceneToUnloadObj = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneToUnload);
            if (sceneToUnloadObj.isLoaded)
            {
                if (verbose)
                    Debug.Log(messagePrefix + "Unloading scene: " + sceneToUnload);

                unloadop = SceneManager.UnloadSceneAsync(sceneToUnload);
                while (!unloadop.isDone)
                {
                    yield return new WaitForEndOfFrame();
                }
            }
            else if (verbose)
            {
                Debug.Log(messagePrefix + "Scene to unload '" + sceneToUnload + "' is not loaded, skipping unload.");
            }
        }

        // Only attempt to load if the scene name is valid and not already loaded
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            // Check if the scene is already loaded
            UnityEngine.SceneManagement.Scene sceneToLoadObj = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneToLoad);
            if (!sceneToLoadObj.isLoaded)
            {
                if (verbose)
                    Debug.Log(messagePrefix + "Loading scene: " + sceneToLoad);

                loadop = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
                while (loadop != null && !loadop.isDone)
                {
                    yield return new WaitForEndOfFrame();
                }

                // Set the loaded scene as active if it was successfully loaded
                if (loadop != null && loadop.isDone)
                {
                    UnityEngine.SceneManagement.Scene loadedScene = SceneManager.GetSceneByName(sceneToLoad);
                    if (loadedScene.isLoaded)
                    {
                        SceneManager.SetActiveScene(loadedScene);
                    }
                }
            }
            else if (verbose)
            {
                Debug.Log(messagePrefix + "Scene to load '" + sceneToLoad + "' is already loaded, skipping load.");
                // Still set it as active scene if requested
                SceneManager.SetActiveScene(sceneToLoadObj);
            }
        }
    }

    LightmapData[] LoadLightmaps(int index)
    {
        if (lightingScenariosData[index].lightmaps == null || lightingScenariosData[index].lightmaps.Length == 0)
        {
            Debug.LogWarning(messagePrefix + "No lightmaps stored in scenario " + index);
            return null;
        }

        var newLightmaps = new LightmapData[lightingScenariosData[index].lightmaps.Length];

        for (int i = 0; i < newLightmaps.Length; i++)
        {
            newLightmaps[i] = new LightmapData();
            newLightmaps[i].lightmapColor = lightingScenariosData[index].lightmaps[i];

            if (lightingScenariosData[index].lightmapsMode != LightmapsMode.NonDirectional)
            {
                if (lightingScenariosData[index].lightmapsDir != null && lightingScenariosData[index].lightmapsDir.Length > i)
                    newLightmaps[i].lightmapDir = lightingScenariosData[index].lightmapsDir[i];
            }
            if (lightingScenariosData[index].shadowMasks != null && lightingScenariosData[index].shadowMasks.Length > i)
            {
                newLightmaps[i].shadowMask = lightingScenariosData[index].shadowMasks[i];
            }
        }

        return newLightmaps;
    }

    LightmapData[] LoadLightmaps(DynamicLightsScenarioData data)
    {
        if (data.lightmaps == null || data.lightmaps.Length == 0)
        {
            Debug.LogWarning("No lightmaps stored in scenario " + data.lightingSceneName);
            return null;
        }

        var newLightmaps = new LightmapData[data.lightmaps.Length];

        for (int i = 0; i < newLightmaps.Length; i++)
        {
            newLightmaps[i] = new LightmapData();
            newLightmaps[i].lightmapColor = data.lightmaps[i];

            if (data.lightmapsMode != LightmapsMode.NonDirectional)
            {
                if (data.lightmapsDir != null && data.lightmapsDir.Length > i)
                    newLightmaps[i].lightmapDir = data.lightmapsDir[i];
            }
            if (data.shadowMasks != null && data.shadowMasks.Length > i)
            {
                newLightmaps[i].shadowMask = data.shadowMasks[i];
            }
        }

        return newLightmaps;
    }

    public void ApplyDataRendererInfo(RendererInfo[] infos)
    {
        if (infos == null || infos.Length == 0)
            return;

        foreach (var info in infos)
        {
            if (!info.isActive || !info.isStatic)
                continue;

            if (info.renderer != null)
            {
                info.renderer.lightmapIndex = info.lightmapIndex;
                if (applyLightmapScaleAndOffset)
                    info.renderer.lightmapScaleOffset = info.lightmapScaleOffset;
            }
            /* else if (info.terrain != null)
            {
                info.terrain.lightmapIndex = info.lightmapIndex;
                if (applyLightmapScaleAndOffset)
                    info.terrain.lightmapScaleOffset = info.lightmapScaleOffset;
            } */
        }

        /* try
        {
            var hashRendererPairs = new Dictionary<int, RendererInfo>();

            //Fill with lighting scenario to load renderer infos - only consider static and active ones
            foreach (var info in infos)
            {
                if (info.isStatic && info.isActive)
                {
                    var uniquehash = info.transformHash + info.name.GetHashCode() + info.meshHash;
                    if (hashRendererPairs.ContainsKey(uniquehash))
                        Debug.LogWarning(messagePrefix + "This renderer info could not be matched. Please check that you don't have 2 gameobjects with the same name, transform, and mesh.", info.renderer);
                    else
                        hashRendererPairs.Add(uniquehash, info);
                }
            }

            //Find all renderers - only get static and active ones
            var renderers = FindObjectsOfType<Renderer>().Where(r =>
                r.gameObject.activeInHierarchy &&
                IsStaticForLightmapping(r.gameObject)).ToArray();

            //Apply stored scale and offset if transform and mesh hashes match
            foreach (var render in renderers)
            {
                var infoToApply = new RendererInfo();
                var meshfilter = render.gameObject.GetComponent<MeshFilter>();
                int meshHash = 0;
                if (meshfilter != null && meshfilter.sharedMesh != null)
                    meshHash = meshfilter.sharedMesh.GetHashCode();

                if (hashRendererPairs.TryGetValue(GetStableHash(render.gameObject.transform) + render.gameObject.name.GetHashCode() + meshHash, out infoToApply))
                {
                    render.lightmapIndex = infoToApply.lightmapIndex;
                    if (applyLightmapScaleAndOffset)
                        render.lightmapScaleOffset = infoToApply.lightmapScaleOffset;
                }
                else if (verbose)
                    Debug.LogWarning(messagePrefix + "Couldn't find renderer info for " + render.gameObject.name + ". This can be ignored if it's not supposed to receive any lightmap.", render);
            }

            //Find all terrains - only get static and active ones
            var terrains = FindObjectsOfType<Terrain>().Where(t =>
                t.gameObject.activeInHierarchy &&
                IsStaticForLightmapping(t.gameObject)).ToArray();

            //Apply stored scale and offset if transform and mesh hashes match
            foreach (var terrain in terrains)
            {
                var infoToApply = new RendererInfo();

                if (hashRendererPairs.TryGetValue(GetStableHash(terrain.gameObject.transform) + terrain.name.GetHashCode() + terrain.terrainData.GetHashCode(), out infoToApply))
                {
                    if (terrain.gameObject.name == infoToApply.name)
                    {
                        terrain.lightmapIndex = infoToApply.lightmapIndex;
                        if (applyLightmapScaleAndOffset)
                            terrain.lightmapScaleOffset = infoToApply.lightmapScaleOffset;
                    }
                }
            } 

        }
        catch (Exception e)
        {
            if (Application.isEditor)
                Debug.LogError(messagePrefix + "Error in ApplyDataRendererInfo:" + e.GetType().ToString());
        }*/
    }

    private bool IsStaticForLightmapping(GameObject go)
    {
#if UNITY_EDITOR
        try
        {
            return GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.ContributeGI);
        }
        catch
        {
            // Fallback for older Unity versions or compatibility issues
            return go.isStatic;
        }
#else
        // In runtime, check if the object has a valid lightmap index (indicating it was baked)
        var renderer = go.GetComponent<Renderer>();
        var terrain = go.GetComponent<Terrain>();

        if (renderer != null)
            return renderer.lightmapIndex >= 0 && renderer.lightmapIndex != 65534;
        if (terrain != null)
            return terrain.lightmapIndex >= 0 && terrain.lightmapIndex != 65534;

        return false;
#endif
    }

    public void ApplyDataRendererInfo(int index)
    {
        if (lightingScenariosData[index] != null)
            ApplyDataRendererInfo(lightingScenariosData[index].rendererInfos);
        else
            Debug.LogWarning(messagePrefix + "Trying to load null lighting scenario data at index " + index);
    }

    public void LoadLightProbes(int index)
    {
        if (lightingScenariosData[index] != null)
            LoadLightProbes(lightingScenariosData[index]);
        else
            Debug.LogWarning(messagePrefix + "Trying to load null lighting scenario data at index " + index);
    }

    public void LoadLightProbes(DynamicLightsScenarioData data)
    {
        if (data.lightProbesAsset != null && data.lightProbesAsset.coefficients != null && data.lightProbesAsset.coefficients.Length > 0)
        {
            try
            {
                LightmapSettings.lightProbes = data.lightProbesAsset.lightprobes;
                if (data.lightProbesAsset.lightprobes != null)
                    LightmapSettings.lightProbes.bakedProbes = data.lightProbesAsset.lightprobes.bakedProbes;
            }
            catch { Debug.LogWarning("Warning, error when trying to load lightprobes for scenario " + data.lightingSceneName); }
        }
    }

    public static int GetStableHash(Transform transform)
    {
        Vector3 stablePos = new Vector3(LimitDecimals(transform.position.x, 2), LimitDecimals(transform.position.y, 2), LimitDecimals(transform.position.z, 2));
        Vector3 stableRot = new Vector3(LimitDecimals(transform.rotation.x, 1), LimitDecimals(transform.rotation.y, 1), LimitDecimals(transform.rotation.z, 1));
        return stablePos.GetHashCode() + stableRot.GetHashCode();
    }

    static float LimitDecimals(float input, int decimalcount)
    {
        var multiplier = Mathf.Pow(10, decimalcount);
        return Mathf.Floor(input * multiplier) / multiplier;
    }

    public void StoreLightmapInfos(int index)
    {
        DynamicLightsScenarioData newLightingScenarioData;
        while (lightingScenariosData.Count < index + 1)
            lightingScenariosData.Add(null);
        if (lightingScenariosData[index] != null)
            newLightingScenarioData = lightingScenariosData[index];
        else
            newLightingScenarioData = new();

        var newRendererInfos = new List<RendererInfo>();
        var newLightmapsTextures = new List<Texture2D>();
        var newLightmapsTexturesDir = new List<Texture2D>();
        var newLightmapsMode = LightmapSettings.lightmapsMode;
        var newLightmapsShadowMasks = new List<Texture2D>();

#if UNITY_EDITOR
        newLightingScenarioData.lightingSceneName = lightingScenariosScenes[index].name;
        newLightingScenarioData.lightingSceneName = newLightingScenarioData.lightingSceneName;
#endif
        newLightingScenarioData.geometrySceneName = gameObject.scene.name;
        newLightingScenarioData.storeRendererInfos = true;

        GenerateLightmapInfo(gameObject, newRendererInfos, newLightmapsTextures, newLightmapsTexturesDir, newLightmapsShadowMasks, newLightmapsMode);

        newLightingScenarioData.lightmapsMode = newLightmapsMode;

        newLightingScenarioData.lightmaps = newLightmapsTextures.ToArray();

        if (newLightmapsMode != LightmapsMode.NonDirectional)
        {
            newLightingScenarioData.lightmapsDir = newLightmapsTexturesDir.ToArray();
        }

        //Mixed or realtime support
        newLightingScenarioData.hasRealtimeLights = latestBuildHasReltimeLights;

        newLightingScenarioData.shadowMasks = newLightmapsShadowMasks.ToArray();

        newLightingScenarioData.rendererInfos = newRendererInfos.ToArray();

        // Store reflection probes data
        StoreReflectionProbes(newLightingScenarioData);

        // Store skybox and ambient data
        StoreSkyboxAndAmbient(newLightingScenarioData);

        if (newLightingScenarioData.lightProbesAsset == null)
        {
            newLightingScenarioData.lightProbesAsset = new();
        }

        if (LightmapSettings.lightProbes != null)
        {
            newLightingScenarioData.lightProbesAsset.coefficients = LightmapSettings.lightProbes.bakedProbes;
            newLightingScenarioData.lightProbesAsset.lightprobes = LightProbes.Instantiate(LightmapSettings.lightProbes);
        }

        if (lightingScenariosData.Count < index + 1)
        {
            lightingScenariosData.Insert(index, newLightingScenarioData);
        }
        else
        {
            lightingScenariosData[index] = newLightingScenarioData;
        }

        lightingScenariosCount = lightingScenariosData.Count;
    }

    private void StoreReflectionProbes(DynamicLightsScenarioData data)
    {
        var reflectionProbes = FindObjectsOfType<ReflectionProbe>();
        var reflectionProbeInfos = new List<DynamicLightsScenarioData.ReflectionProbeInfo>();

        foreach (var probe in reflectionProbes)
        {
            if (probe.bakedTexture != null)
            {
                var probeInfo = new DynamicLightsScenarioData.ReflectionProbeInfo
                {
                    instanceID = probe.GetInstanceID(),
                    position = probe.transform.position,
                    bakedTexture = probe.bakedTexture as Cubemap, // Fixed: explicit cast to Cubemap
                    intensity = probe.intensity,
                    boxProjection = probe.boxProjection,
                    size = probe.size,
                    center = probe.center
                };
                reflectionProbeInfos.Add(probeInfo);
            }
        }

        data.reflectionProbes = reflectionProbeInfos.ToArray();

        if (verbose)
            Debug.Log(messagePrefix + "Stored " + reflectionProbeInfos.Count + " reflection probes");
    }

    private void StoreSkyboxAndAmbient(DynamicLightsScenarioData data)
    {
        data.skyboxMaterial = RenderSettings.skybox;
        data.ambientMode = RenderSettings.ambientMode;
        data.ambientSkyColor = RenderSettings.ambientSkyColor;
        data.ambientEquatorColor = RenderSettings.ambientEquatorColor;
        data.ambientGroundColor = RenderSettings.ambientGroundColor;
        data.ambientIntensity = RenderSettings.ambientIntensity;
        data.ambientProbe = RenderSettings.ambientProbe;

        if (verbose)
            Debug.Log(messagePrefix + "Stored skybox and ambient lighting settings");
    }

    public void ApplyReflectionProbes(DynamicLightsScenarioData data)
    {
        if (data.reflectionProbes == null || data.reflectionProbes.Length == 0)
            return;

        var reflectionProbes = FindObjectsOfType<ReflectionProbe>();

        foreach (var probeInfo in data.reflectionProbes)
        {
            // Find matching reflection probe by position (most reliable method)
            var matchingProbe = reflectionProbes.FirstOrDefault(p =>
                Vector3.Distance(p.transform.position, probeInfo.position) < 0.1f);

            if (matchingProbe != null)
            {
                matchingProbe.bakedTexture = probeInfo.bakedTexture;
                matchingProbe.intensity = probeInfo.intensity;
                matchingProbe.boxProjection = probeInfo.boxProjection;
                matchingProbe.size = probeInfo.size;
                matchingProbe.center = probeInfo.center;

                if (verbose)
                    Debug.Log(messagePrefix + "Applied reflection probe data to " + matchingProbe.name);
            }
        }
    }

    public void ApplySkyboxAndAmbient(DynamicLightsScenarioData data)
    {
        if (data.skyboxMaterial != null)
        {
            RenderSettings.skybox = data.skyboxMaterial;
        }

        RenderSettings.ambientMode = data.ambientMode;

        switch (data.ambientMode)
        {
            case AmbientMode.Trilight:
                RenderSettings.ambientSkyColor = data.ambientSkyColor;
                RenderSettings.ambientEquatorColor = data.ambientEquatorColor;
                RenderSettings.ambientGroundColor = data.ambientGroundColor;
                break;
            case AmbientMode.Flat:
                RenderSettings.ambientSkyColor = data.ambientSkyColor;
                break;
            case AmbientMode.Skybox:
                RenderSettings.ambientIntensity = data.ambientIntensity;
                RenderSettings.ambientProbe = data.ambientProbe;
                break;
        }

        if (verbose)
            Debug.Log(messagePrefix + "Applied skybox and ambient lighting settings");
    }

    static void GenerateLightmapInfo(GameObject root, List<RendererInfo> newRendererInfos, List<Texture2D> newLightmapsLight, List<Texture2D> newLightmapsDir, List<Texture2D> newLightmapsShadow, LightmapsMode newLightmapsMode)
    {
        // Only get static and active GameObjects with renderers or terrains
        var gameObjects = FindObjectsOfType<GameObject>().Where(x =>
            x.activeInHierarchy &&
            (x.GetComponent<Renderer>() != null || x.GetComponent<Terrain>() != null));

        newLightmapsMode = LightmapSettings.lightmapsMode;

        foreach (var go in gameObjects)
        {
            // Skip if not static for lightmapping
            if (!IsStaticForLightmappingStatic(go))
                continue;

            Terrain t;
            Renderer r;
            MeshFilter m;
            go.TryGetComponent<Renderer>(out r);
            go.TryGetComponent<Terrain>(out t);
            go.TryGetComponent<MeshFilter>(out m);

            // Skip if lightmap index is invalid
            int lightmapIndex = r ? r.lightmapIndex : (t ? t.lightmapIndex : -1);
            if (lightmapIndex < 0 || lightmapIndex == 65534)
                continue;

            RendererInfo rendererInfo = new RendererInfo()
            {
                name = go.name,
                transformHash = GetStableHash(go.transform),
                lightmapScaleOffset = r ? r.lightmapScaleOffset : t.lightmapScaleOffset,
                lightmapIndex = lightmapIndex,
                meshHash = r ? (m != null && m.sharedMesh != null ? m.sharedMesh.GetHashCode() : 0) : (t != null && t.terrainData != null ? t.terrainData.GetHashCode() : 0),
                renderer = r ? r : null,
                isStatic = true,
                isActive = go.activeInHierarchy
            };
            newRendererInfos.Add(rendererInfo);
        }

        LightmapData[] datas = LightmapSettings.lightmaps;
        foreach (var data in datas)
        {
            if (data.lightmapColor != null)
                newLightmapsLight.Add(data.lightmapColor);
            if (data.lightmapDir != null)
                newLightmapsDir.Add(data.lightmapDir);
            if (data.shadowMask != null)
                newLightmapsShadow.Add(data.shadowMask);
        }

        if (Application.isEditor)
            Debug.Log(messagePrefix + "Stored info for " + newRendererInfos.Count + " static GameObjects.");
    }

    private static bool IsStaticForLightmappingStatic(GameObject go)
    {
#if UNITY_EDITOR
        try
        {
            return GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.ContributeGI);
        }
        catch
        {
            // Fallback for older Unity versions or compatibility issues
            return go.isStatic;
        }
#else
        // In runtime, check if the object has a valid lightmap index (indicating it was baked)
        var renderer = go.GetComponent<Renderer>();
        var terrain = go.GetComponent<Terrain>();

        if (renderer != null)
            return renderer.lightmapIndex >= 0 && renderer.lightmapIndex != 65534;
        if (terrain != null)
            return terrain.lightmapIndex >= 0 && terrain.lightmapIndex != 65534;

        return false;
#endif
    }

}
