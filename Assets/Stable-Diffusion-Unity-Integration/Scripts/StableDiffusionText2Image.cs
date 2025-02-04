using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using NaughtyAttributes;

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;

#endif

/// <summary>
/// Component to help generate a UI Image or RawImage using Stable Diffusion.
/// </summary>
[ExecuteAlways]
public class StableDiffusionText2Image : StableDiffusionGenerator
{
    private const string StreamingAssetsFolder = "SDImages";

    [ReadOnly]
    public string guid = "";

    public SDParamsInTxt2Img SD_Params;

    [ReadOnly, AllowNesting] 
    public long generatedSeed = -1;

    protected string filename = "";

    /// <summary>
    /// List of models to display as Drop-Down in the inspector
    /// </summary>
    [SerializeField]
    public string[] modelsList
    {
        get
        {
            if (sdc == null)
                sdc = GameObject.FindObjectOfType<StableDiffusionConfiguration>();
            return sdc.modelNames;
        }
    }

    /// <summary>
    /// Actual model selected in the drop-down list
    /// </summary>
    [HideInInspector]
    public int selectedModel = 0;

    // Internally keep tracking if we are currently generating (prevent re-entry)
    protected bool generating = false;
    public bool Generating => generating;


#if UNITY_EDITOR
    /// <summary>
    /// On Awake, fill the properties with default values from the selected settings.
    /// </summary>
    protected void Awake()
    {
        if (SD_Params.width < 0 || SD_Params.height < 0)
        {
            StableDiffusionConfiguration sdc = FindObjectOfType<StableDiffusionConfiguration>();
            if (sdc != null)
            {
                SDSettings settings = sdc.settings;
                if (settings != null)
                {
                    SD_Params.width = settings.sdDefaultParams.width;
                    SD_Params.height = settings.sdDefaultParams.height;
                    SD_Params.steps = settings.sdDefaultParams.steps;
                    SD_Params.cfg_scale = settings.sdDefaultParams.cfgScale;
                    SD_Params.seed = settings.sdDefaultParams.seed;
                    return;
                }
            }

            SD_Params.width = 512;
            SD_Params.height = 512;
            SD_Params.steps = 50;
            SD_Params.cfg_scale = 7;
            SD_Params.seed = -1;
        }
    }

    protected void Start() => EditorUtility.ClearProgressBar();

    protected void OnValidate()
    {
        SD_Params.EnforceSD_Constraints();

        // If not setup already, generate a GUID (Global Unique Identifier)
        if (string.IsNullOrEmpty(guid))
            guid = Guid.NewGuid().ToString();
    }
#endif

    /// <summary>
    /// Callback function for the inspector Generate button.
    /// </summary>
    public virtual void Generate()
    {
        // Start generation asynchronously
        if (!generating && !string.IsNullOrEmpty(SD_Params.prompt))
        {
            StartCoroutine(GenerateAsync());
        }
    }

    /// <summary>
    /// Setup the output path and filename for image generation
    /// </summary>
    /// <param name="targetFolder">The folder name in streaming assets where the files are stored.</param>
    protected void SetupFolders(string targetFolder = StreamingAssetsFolder)
    {
        // Get the configuration settings
        if (sdc == null)
            sdc = FindObjectOfType<StableDiffusionConfiguration>();

        try
        {
            // Determine output path
            string root = Application.dataPath + sdc.settings.outputFolder;
            if (root == "" || !Directory.Exists(root))
                root = Application.streamingAssetsPath;
            string mat = Path.Combine(root, targetFolder);
            filename = Path.Combine(mat, guid + ".png");

            // If folders not already exists, create them
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            if (!Directory.Exists(mat))
                Directory.CreateDirectory(mat);

            // If the file already exists, delete it
            if (File.Exists(filename))
                File.Delete(filename);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n\n" + e.StackTrace);
        }
    }

    /// <summary>
    /// Send request to generate an image and awaits an answer from sd service.
    /// </summary>
    /// <param name="sDResponseTxt2Img"></param>
    /// <returns>True if image generation was successful, false otherwise.</returns>
    protected IEnumerator GenerateImage(bool logResult = true)
    {
        UnityWebRequest request = new UnityWebRequest(sdc.settings.apiEndpoints.TextToImage, "POST");

        request.SetupSDRequest<DownloadHandlerBuffer>(sdc.settings, JsonUtility.ToJson(SD_Params));
        request.SendWebRequest();

        yield return ShowProgressAndWaitUntilDone(request);

        if(logResult) Debug.Log(request.result);
        requestResult = request.result;
        sDResponseTxt2Img = JsonUtility.FromJson<SDResponseTxt2Img>(request.downloadHandler.text);
        yield break;
    }

    protected SDResponseTxt2Img sDResponseTxt2Img;
    protected UnityWebRequest.Result requestResult;

    protected virtual IEnumerator GenerateAsync()
    {
        generating = true;

        SetupFolders();

        // Set the model parameters
        yield return sdc.SetModelAsync(modelsList[selectedModel]);
        yield return GenerateImage();
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

                LoadIntoImage(texture);
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

    /// <summary>
    /// Load the texture into an Image or RawImage.
    /// </summary>
    /// <param name="texture">Texture to setup</param>
    protected virtual void LoadIntoImage(Texture2D texture)
    {
        try
        {
            // Find the image component
            Image im = GetComponent<Image>();
            if (im != null)
            {
                // Create a new Sprite from the loaded image
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

                // Set the sprite as the source for the UI Image
                im.sprite = sprite;
            }
            // If no image found, try to find a RawImage component
            else
            {
                RawImage rim = GetComponent<RawImage>();
                if (rim != null)
                {
                    rim.texture = texture;
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Force Unity inspector to refresh with new asset
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                EditorApplication.QueuePlayerLoopUpdate();
                EditorSceneManager.MarkAllScenesDirty();
                EditorUtility.RequestScriptReload();
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n\n" + e.StackTrace);
        }
    }
}