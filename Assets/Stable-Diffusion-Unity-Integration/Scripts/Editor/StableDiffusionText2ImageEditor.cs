using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// Custom Inspector to add a models list as a drop-down selection UI 
/// and add a "Generate" button for the StableDiffusionImage.
/// </summary>
[CustomEditor(typeof(StableDiffusionText2Image))]
public class StableDiffusionText2ImageEditor : Editor
{
    static readonly Type txt2imgType = typeof(SDParamsInTxt2Img);
    StableDiffusionText2Image myComponent;
    bool webUIview = true;

    void DrawWithTightLabel(string propertyName, bool inSDParams = true, float mulSize = 1f, params GUILayoutOption[] option)
    {
        var path = inSDParams ? "SD_Params." : "";
        var property = serializedObject.FindProperty($"{path}{propertyName}");
        if (property is null) return;
        var label = typeof(SDParamsInTxt2Img)?.GetProperty(propertyName)?.GetCustomAttribute<LabelAttribute>()?.Label;
        label ??= property.displayName;
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent($"{label}xxx")).x * mulSize;
        if (option is null) EditorGUILayout.PropertyField(property);
        else EditorGUILayout.PropertyField(property, option);
        EditorGUIUtility.labelWidth = 0;
    }

    void Draw(string sdParamPropertyName, GUIContent label = null, GUILayoutOption option = null)
    {
        var property = serializedObject.FindProperty($"SD_Params.{sdParamPropertyName}");
        if (property is null) return;
        if (option is null)
            if (label is null) EditorGUILayout.PropertyField(property);
            else EditorGUILayout.PropertyField(property, label);
        else if (label is null) EditorGUILayout.PropertyField(property);
        else EditorGUILayout.PropertyField(property, label, option);
    }

    void DrawProgress()
    {
        var property = serializedObject.FindProperty($"progress");
        if (property is null) return;
        EditorGUILayout.PropertyField(property);
    }

    public override void OnInspectorGUI()
    {
        myComponent = (StableDiffusionText2Image)target;

        if (GUILayout.Button(webUIview ? "Change to default layout (has additional params)" : "Change to a WebUI similar layout"))
            webUIview = !webUIview;

        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent("Model  ")).x;
        myComponent.selectedModel = EditorGUILayout.Popup("Model", myComponent.selectedModel, myComponent.modelsList);
        EditorGUIUtility.labelWidth = 0;


        if (webUIview)
        {
            Draw("prompt");
            Draw("negative_prompt");

            SamplerSection();
            AdditionalProccessesSection();
            HiResSection();
            BatchAndCfgSection();
            SeedSection();

            EditorGUILayout.Separator();
            DrawWithTightLabel("generatedSeed", false);
            serializedObject.ApplyModifiedProperties();
        }
        else
        {
            DrawDefaultInspector();
        }

        EditorGUILayout.Separator();
        EditorGUI.BeginDisabledGroup(myComponent.Generating);
        if (GUILayout.Button("Generate"))
            myComponent.Generate();
        EditorGUI.EndDisabledGroup();
        DrawProgress();
    }

    void AdditionalProccessesSection()
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = 0;
        DrawWithTightLabel("restore_faces");
        DrawWithTightLabel("tiling");
        DrawWithTightLabel("enable_hr");
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = 0;
    }

    private GUIStyle smallFont = new GUIStyle();

    void HiResSection()
    {
        if (myComponent.SD_Params.enable_hr is false) return;

        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.fieldWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxxx")).x * 4;
        DrawWithTightLabel("hr_upscaler", true, 0.7f, GUILayout.Width(EditorGUIUtility.currentViewWidth / 4f));
        EditorGUIUtility.fieldWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxxx")).x;
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxxx")).x;
        // smallFont.fontSize = 10;

        Draw("hr_second_pass_steps");
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxxxxxxx")).x;
        Draw("denoising_strength");
        EditorGUILayout.EndHorizontal();
        //EditorGUILayout.EndVertical();


        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.fieldWidth = 0;
        DrawWithTightLabel("hr_scale", option: GUILayout.Width(EditorGUIUtility.currentViewWidth / 4f));
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxxxxxx")).x;
        Draw("hr_resize_x");
        Draw("hr_resize_y");
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = 0;
    }

    void BatchAndCfgSection()
    {
        EditorGUILayout.Separator();
        //not needed for this implementation... but will be needed later
        //EditorGUILayout.BeginHorizontal();
        //EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxxx")).x;
        //EditorGUIUtility.fieldWidth = EditorStyles.label.CalcSize(new GUIContent($"xxx")).x;
        //Draw("n_iter");
        //Draw("batch_size");
        //EditorGUILayout.EndHorizontal();
        //EditorGUIUtility.fieldWidth = EditorStyles.label.CalcSize(new GUIContent($"xxx.xxx")).x;
        DrawWithTightLabel("cfg_scale");
    }

    void SamplerSection()
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxx")).x * 3.4f;
        Draw("sampler_name");
        EditorGUIUtility.fieldWidth = EditorStyles.label.CalcSize(new GUIContent($"xxxxx")).x;
        DrawWithTightLabel("steps");
        EditorGUILayout.EndHorizontal();
    }

    void SeedSection()
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        DrawWithTightLabel("seed");
        if (GUILayout.Button("Random")) myComponent.SD_Params.seed = -1;
        if (GUILayout.Button("Copy Generated")) myComponent.SD_Params.seed = myComponent.generatedSeed;
        EditorGUILayout.EndHorizontal();
    }

}
