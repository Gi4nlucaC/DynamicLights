using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class DynamicLightsProbesAsset
{
    [SerializeField]
    [FormerlySerializedAs("lightProbes")]
    public UnityEngine.Rendering.SphericalHarmonicsL2[] coefficients;
    [SerializeField]
    public LightProbes lightprobes;
}
