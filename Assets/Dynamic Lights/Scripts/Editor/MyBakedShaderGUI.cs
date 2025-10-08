// BakedShaderGUI.cs
// Place this script in an 'Editor' folder.

using UnityEditor;
using UnityEngine;
using System;

public class MyBakedShaderGUI : ShaderGUI
{
    // Material Properties
    MaterialProperty baseMap;
    MaterialProperty baseColor;
    MaterialProperty cutoff;
    MaterialProperty bumpMap;
    MaterialProperty lightmapDay;
    MaterialProperty lightmapNight;
    MaterialProperty lightmapDirDay;
    MaterialProperty lightmapDirNight;
    MaterialProperty blend;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Find all the properties defined in the shader
        FindProperties(properties);

        // Get the current material being edited
        Material material = materialEditor.target as Material;

        // --- Main Surface Inputs ---
        EditorGUILayout.LabelField("Main Maps", EditorStyles.boldLabel);

        // Albedo Texture and Color
        materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);

        // Alpha Cutoff Slider (only shown if cutout is enabled)
        if (material.IsKeywordEnabled("_ALPHATEST_ON"))
        {
            materialEditor.ShaderProperty(cutoff, "Alpha Cutoff");
        }

        EditorGUILayout.Space();

        // --- Normal Map ---
        EditorGUILayout.LabelField("Normal Map", EditorStyles.boldLabel);
        materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), bumpMap);

        // Set the _NORMALMAP keyword based on whether a texture is assigned
        SetKeyword(material, "_NORMALMAP", bumpMap.textureValue != null);

        EditorGUILayout.Space();

        // --- Custom Lightmap Blending ---
        EditorGUILayout.LabelField("Day/Night Lightmap Blending", EditorStyles.boldLabel);

        // Blend Slider
        materialEditor.ShaderProperty(blend, "Lightmap Blender");

        // Lightmap Textures
        materialEditor.TexturePropertySingleLine(new GUIContent("Lightmap Day"), lightmapDay);
        materialEditor.TexturePropertySingleLine(new GUIContent("Lightmap Night"), lightmapNight);
        materialEditor.TexturePropertySingleLine(new GUIContent("Directional Day"), lightmapDirDay);
        materialEditor.TexturePropertySingleLine(new GUIContent("Directional Night"), lightmapDirNight);

        EditorGUILayout.Space();

        // --- Advanced Options ---
        EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
        materialEditor.EnableInstancingField();
        materialEditor.RenderQueueField();
    }

    public void FindProperties(MaterialProperty[] props)
    {
        // Find properties by their reference name in the shader
        baseMap = FindProperty("_BaseMap", props);
        baseColor = FindProperty("_BaseColor", props);
        cutoff = FindProperty("_Cutoff", props);
        bumpMap = FindProperty("_BumpMap", props);
        lightmapDay = FindProperty("_LightmapDay", props);
        lightmapNight = FindProperty("_LightmapNight", props);
        lightmapDirDay = FindProperty("_LightmapDirDay", props);
        lightmapDirNight = FindProperty("_LightmapDirNight", props);
        blend = FindProperty("_Blend", props);
    }

    // Helper function to enable or disable a shader keyword
    private static void SetKeyword(Material mat, string keyword, bool enabled)
    {
        if (enabled)
        {
            mat.EnableKeyword(keyword);
        }
        else
        {
            mat.DisableKeyword(keyword);
        }
    }
}