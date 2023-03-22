using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;

#endif

/// <summary>
/// Component to help generate a Material Texture using Stable Diffusion.
/// </summary>
[ExecuteAlways]
public class StableDiffusionText2Material : StableDiffusionText2Image
{
    //TODO check if Update can be replaced with OnValidate

    private const string StreamingAssetsFolder = "SDMaterials";

    public SDGMaterialData material;

    // Keep track of material properties value, to detect if the user changes them on the fly, from the inspector
    private SDGMaterialData _material;


#if UNITY_EDITOR
    /// <summary>
    /// On Awake, fill the properties with default values from the selected settings.
    /// </summary>
    protected new void Awake() => base.Awake();

    protected new void Start() => base.Start();

    /// <summary>
    /// Loop update
    /// </summary>
    protected new void OnValidate()
    {
        base.OnValidate();

        _material ??= material;
        (bool differentNormalMapStrength, bool someOtherDifferentField) = material != _material;

        // Update normal map strength whenever the user modifies it in the inspector
        if (differentNormalMapStrength)
        {
            MeshRenderer mr = material.GetMeshRenderer(gameObject);
            if (mr != null)
                mr.sharedMaterial.SetFloat("_BumpScale", material.normalMapStrength);

            material.UpdateMaterialProperties(gameObject);
        }

        // Update tilling, metallic and smoothness properties whenever the user modifies them in the inspector
        if (someOtherDifferentField)
        {
            material.UpdateMaterialProperties(gameObject);
        }

        //update private material with the changes
        if(differentNormalMapStrength || someOtherDifferentField)
            _material.CopyFrom(material);
    }
#endif

    /// <summary>
    /// Request an image generation to the Stable Diffusion server, asynchronously.
    /// </summary>
    /// <returns></returns>
    protected override IEnumerator GenerateAsync()
    {
        generating = true;

        SetupFolders(StreamingAssetsFolder);

        // Set the model parameters
        yield return sdc.SetModelAsync(modelsList[selectedModel]);

        UnityWebRequest.Result requestResult = GenerateImage(out SDResponseTxt2Img sDResponseTxt2Img);
        if (requestResult is not UnityWebRequest.Result.Success)
        {
            Debug.Log(requestResult);
            yield break;
        }

        // If no image, there was probably an error so abort
        if (sDResponseTxt2Img.images == null || sDResponseTxt2Img.images.Length == 0)
        {
            Debug.LogError("No image was return by the server. This should not happen. Verify that the server is correctly setup.");

            generating = false;
#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
#endif
            yield break;
        }

        // Decode the image from Base64 string into an array of bytes
        byte[] imageData = Convert.FromBase64String(sDResponseTxt2Img.images[0]);

        // Write it in the specified project output folder
        using (FileStream imageFile = new FileStream(filename, FileMode.Create))
        {
#if UNITY_EDITOR
            AssetDatabase.StartAssetEditing();
#endif
            yield return imageFile.WriteAsync(imageData, 0, imageData.Length);
#if UNITY_EDITOR
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
#endif
        }

        try
        {
            // Read back the image into a texture
            if (File.Exists(filename))
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);
                texture.Apply();

                material.LoadIntoMaterial(texture, gameObject, sdc);
            }

            // Read the seed that was used by Stable Diffusion to generate this result
            generatedSeed = sDResponseTxt2Img.info.seed;
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n\n" + e.StackTrace);
        }
#if UNITY_EDITOR
        EditorUtility.ClearProgressBar();
#endif
        generating = false;
        yield return null;
    }
}