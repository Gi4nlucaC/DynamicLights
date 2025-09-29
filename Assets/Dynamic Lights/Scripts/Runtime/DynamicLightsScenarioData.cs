using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

[System.Serializable]
public class DynamicLightsScenarioData
{
    [FormerlySerializedAs("sceneName")]
    public string lightingSceneName;
    public string geometrySceneName;
    public bool storeRendererInfos;
    public DynamicLightsLevelData.RendererInfo[] rendererInfos;
    public Texture2D[] lightmaps;
    public Texture2D[] lightmapsDir;
    public Texture2D[] shadowMasks;
    public LightmapsMode lightmapsMode;
    public DynamicLightsProbesAsset lightProbesAsset;
    public bool hasRealtimeLights;

    // New fields for reflection probes and skybox
    [System.Serializable]
    public class ReflectionProbeInfo
    {
        public int instanceID;
        public Vector3 position;
        public Cubemap bakedTexture;
        public float intensity;
        public bool boxProjection;
        public Vector3 size;
        public Vector3 center;
    }

    public ReflectionProbeInfo[] reflectionProbes;
    public Material skyboxMaterial;
    public AmbientMode ambientMode;
    public Color ambientSkyColor;
    public Color ambientEquatorColor;
    public Color ambientGroundColor;
    public float ambientIntensity;
    public SphericalHarmonicsL2 ambientProbe;
}