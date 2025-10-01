using UnityEngine;

[ExecuteInEditMode]
public class LightmapDataApplicator : MonoBehaviour
{
    private static MaterialPropertyBlock _propBlock;
    private MeshRenderer _renderer;

    void Start()
    {
        TryGetComponent<MeshRenderer>(out _renderer);
        ApplyBlock();
    }

    public void ApplyBlock()
    {
        if (_renderer)
        {
            _propBlock ??= new MaterialPropertyBlock();

            Vector4 lightmapScaleOffset = _renderer.lightmapScaleOffset;
            _propBlock.SetVector("_LightmapST", lightmapScaleOffset);
            _renderer.SetPropertyBlock(_propBlock);
        }
        else
            Debug.LogError("Nessun Renderer trovato su questo oggetto.");
    }


}