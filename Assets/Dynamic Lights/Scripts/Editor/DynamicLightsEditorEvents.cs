using UnityEngine;
using UnityEditor;

// ensure class initializer is called whenever scripts recompile
[InitializeOnLoadAttribute]
public static class DynamicLightsEditorEvents
{
    // register an event handler when the class is initialized
    static DynamicLightsEditorEvents()
    {
        EditorApplication.playModeStateChanged += PlayModeChange;
        SetScriptIcon();
    }
    
    private static void SetScriptIcon()
    {
        // Set custom icon for DynamicLightsLevelData script
        var iconPath = "Assets/Dynamic Lights/Scripts/Editor/LightmapSwitcher.png";
        var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        
        if (iconTexture != null)
        {
            // Find the script asset
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript DynamicLightsLevelData");
            if (scriptGuids.Length > 0)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                
                if (scriptAsset != null)
                {
                    EditorGUIUtility.SetIconForObject(scriptAsset, iconTexture);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }

    private static void PlayModeChange(PlayModeStateChange state)
    {
        var lightmapData = GameObject.FindObjectOfType<DynamicLightsLevelData>();
        if (lightmapData != null)
        {
                switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    lightmapData.OnEnteredPlayMode_EditorOnly();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    lightmapData.OnExitingPlayMode_EditorOnly();
                    break;
            }
        }
    }
}