using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector to add a models list as a drop-down selection UI 
/// and add a "Generate" button for the StableDiffusionImage.
/// </summary>
[CustomEditor(typeof(StableDiffusionText2Image))]
public class StableDiffusionText2ImageEditor : Editor
{
    static readonly Type txt2imgType = typeof(SDParamsInTxt2Img);
    //SerializedObject serializedObject;

    public override void OnInspectorGUI()
    {
        StableDiffusionText2Image myComponent = (StableDiffusionText2Image)target;
        //base.OnInspectorGUI();
        // Draw the drop-down list for the Models list

        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent("Model  ")).x;
        myComponent.selectedModel = EditorGUILayout.Popup("Model", myComponent.selectedModel, myComponent.modelsList);
        EditorGUIUtility.labelWidth = 0;


        AdditionalProccessesSection();
        HiResSection();
        BatchAndCfgSection();


        //EditorGUILayout.LongField("Generated Seed", myComponent.generatedSeed);
        EditorGUILayout.Separator();
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent("Generated Seed  ")).x;
        var generatedSeed = serializedObject.FindProperty("generatedSeed");
        EditorGUILayout.PropertyField(generatedSeed);
        EditorGUIUtility.labelWidth = 0;

        // Apply the changes to the serialized object
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Separator();
        if (GUILayout.Button("Generate"))
            myComponent.Generate();
    }




    static SerializedProperty enable_hr;
    static SerializedProperty restore_faces;
    static SerializedProperty tiling;

   // (SerializedProperty property, string ui_name) getProperty(SerializedObject sObj, string name) =>
     //   (sObj.FindProperty($"SD_Params.{name}"), GetNewNameFor(name) ?? name);

    void AdditionalProccessesSection()
    {
        enable_hr = serializedObject.FindProperty($"SD_Params.{nameof(enable_hr)}");
        restore_faces = serializedObject.FindProperty($"SD_Params.{nameof(restore_faces)}");
        tiling = serializedObject.FindProperty($"SD_Params.{nameof(tiling)}");

        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = 0;
        float toggleWidth = EditorGUIUtility.currentViewWidth / 3f;
        if(restore_faces is not null)  EditorGUILayout.PropertyField(restore_faces, GUILayout.Width(toggleWidth));
        if(tiling is not null)  EditorGUILayout.PropertyField(tiling, GUILayout.Width(toggleWidth));
        if(enable_hr is not null)  EditorGUILayout.PropertyField(enable_hr, GUILayout.Width(toggleWidth));
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = 0;
    }

    SerializedProperty hr_upscaler;
    SerializedProperty hr_second_pass_steps;
    SerializedProperty denoising_strength;

    SerializedProperty hr_scale;
    SerializedProperty hr_resize_x;
    SerializedProperty hr_resize_y;
    void HiResSection()
    {
        //if (enable_hr.boolValue is false) return;

        hr_upscaler = serializedObject.FindProperty($"SD_Params.{nameof(hr_upscaler)}");
        hr_second_pass_steps = serializedObject.FindProperty($"SD_Params.{nameof(hr_second_pass_steps)}");
        denoising_strength = serializedObject.FindProperty($"SD_Params.{nameof(denoising_strength)}");
        hr_scale = serializedObject.FindProperty($"SD_Params.{nameof(hr_scale)}");
        hr_resize_x = serializedObject.FindProperty($"SD_Params.{nameof(hr_resize_x)}");
        hr_resize_y = serializedObject.FindProperty($"SD_Params.{nameof(hr_resize_y)}");
        if (hr_resize_y is null) Debug.Log("is null");

        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth / 4f / 2f;
        if (hr_upscaler is not null) EditorGUILayout.PropertyField(hr_upscaler);
        if (hr_second_pass_steps is not null) EditorGUILayout.PropertyField(hr_second_pass_steps);
        EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth / 2f / 3f;
        if (denoising_strength is not null) EditorGUILayout.PropertyField(denoising_strength);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth / 3f /2f;
        //var toggleWidth = GUILayout.Width(EditorGUIUtility.currentViewWidth / 2f);
        if (hr_scale is not null) EditorGUILayout.PropertyField(hr_scale);
        if (hr_resize_x is not null) EditorGUILayout.PropertyField(hr_resize_x);
        if (hr_resize_y is not null) EditorGUILayout.PropertyField(hr_resize_y);
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = 0;
    }


    SerializedProperty batch_size;
    SerializedProperty n_iter;
    SerializedProperty cfg_scale;

    void BatchAndCfgSection()
    {
        cfg_scale = serializedObject.FindProperty($"SD_Params.{nameof(cfg_scale)}");
        batch_size = serializedObject.FindProperty($"SD_Params.{nameof(batch_size)}");
        n_iter = serializedObject.FindProperty($"SD_Params.{nameof(n_iter)}");

        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth / 2f / 3f /1.1f;
        //GUILayout.Label(n_iter_display_name);
        //n_iter.intValue = EditorGUILayout.IntSlider(n_iter.intValue, 1, n_iter_ceil);
        if (n_iter is not null) EditorGUILayout.PropertyField(n_iter);
        //GUILayout.Label(batch_size_display_name);
        if (batch_size is not null) EditorGUILayout.PropertyField(batch_size);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(cfg_scale, GUILayout.ExpandWidth(true));

        EditorGUIUtility.labelWidth = 0;
    }

}
