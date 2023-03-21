using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;

#endif

/// <summary>
/// Component to help generate a UI Image or RawImage using Stable Diffusion via Img2Img.
/// </summary>
[ExecuteAlways]
public class StableDiffusionImage2Image : StableDiffusionGenerator
{
    [ReadOnly]
    public string guid = "";

    public Texture2D inputTexture;
    public string prompt;
    public string negativePrompt;

    /// <summary>
    /// List of samplers to display as Drop-Down in the inspector
    /// </summary>
    [SerializeField]
    public string[] samplersList
    {
        get
        {
            if (sdc == null)
                sdc = GameObject.FindObjectOfType<StableDiffusionConfiguration>();
            return sdc.samplers;
        }
    }

    /// <summary>
    /// Actual sampler selected in the drop-down list
    /// </summary>
    [HideInInspector]
    public int selectedSampler = 0;

    public int width = 512;
    public int height = 512;
    public int steps = 50;
    public float cfgScale = 7;
    public long seed = -1;

    public long generatedSeed = -1;

    private string filename = "";

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

    /// <summary>
    /// On Awake, fill the properties with default values from the selected settings.
    /// </summary>
    private void Awake()
    {
#if UNITY_EDITOR
        if (width < 0 || height < 0)
        {
            StableDiffusionConfiguration sdc = FindObjectOfType<StableDiffusionConfiguration>();
            if (sdc != null)
            {
                SDSettings settings = sdc.settings;
                if (settings != null)
                {
                    width = settings.width;
                    height = settings.height;
                    steps = settings.steps;
                    cfgScale = settings.cfgScale;
                    seed = settings.seed;
                    return;
                }
            }

            width = 512;
            height = 512;
            steps = 50;
            cfgScale = 7;
            seed = -1;
        }
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Clamp image dimensions values between 128 and 2048 pixels
        if (width < 128) width = 128;
        if (height < 128) height = 128;
        if (width > 2048) width = 2048;
        if (height > 2048) height = 2048;

        // If not setup already, generate a GUID (Global Unique Identifier)
        if (guid == "")
            guid = Guid.NewGuid().ToString();
#endif
    }

    // Internally keep tracking if we are currently generating (prevent re-entry)
    private bool generating = false;

    /// <summary>
    /// Callback function for the inspector Generate button.
    /// </summary>
    public void Generate()
    {
        // Start generation asynchronously
        if (!generating && !string.IsNullOrEmpty(prompt) && inputTexture)
        {
            if (!inputTexture.isReadable)
            {
                Debug.LogError($"Input Image {inputTexture.name} isn't readable. Go to texture import settings and tick the Read/Write box", this);
                return;
            }
            StartCoroutine(GenerateAsync());
        }
    }

    /// <summary>
    /// Setup the output path and filename for image generation
    /// </summary>
    private void SetupFolders()
    {
        // Get the configuration settings
        if (sdc == null)
            sdc = GameObject.FindObjectOfType<StableDiffusionConfiguration>();

        try
        {
            // Determine output path
            string root = Application.dataPath + sdc.settings.OutputFolder;
            if (root == "" || !Directory.Exists(root))
                root = Application.streamingAssetsPath;
            string mat = Path.Combine(root, "SDImages");
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

    private IEnumerator GenerateAsync()
    {
        generating = true;

        SetupFolders();

        // Set the model parameters
        yield return sdc.SetModelAsync(modelsList[selectedModel]);

        // Generate Image
        SDResponseImg2Img sDResponseImg2Img;
        using (UnityWebRequest request = new UnityWebRequest(sdc.settings.StableDiffusionServerURL + sdc.settings.ImageToImageAPI, "POST"))
        {
            byte[] inputImgBytes = inputTexture.EncodeToPNG();
            string inputImgString = Convert.ToBase64String(inputImgBytes);

            SDParamsInImg2Img sd = new SDParamsInImg2Img()
            {
                init_images = new string[] { inputImgString },
                prompt = prompt,
                negative_prompt = negativePrompt,
                steps = steps,
                cfg_scale = cfgScale,
                width = width,
                height = height,
                seed = seed,
                tiling = false
            };

            request.SetupSDRequest<DownloadHandlerBuffer>(sdc.settings, JsonUtility.ToJson(sd));
            request.SendWebRequest();

            ShowProgressAndWaitUntilDone(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(request.error);
                yield break;
            }
            Debug.Log(request.result);

            sDResponseImg2Img = JsonUtility.FromJson<SDResponseImg2Img>(request.downloadHandler.text);
        }

        // If no image, there was probably an error so abort
        if (sDResponseImg2Img.images == null || sDResponseImg2Img.images.Length == 0)
        {
            Debug.LogError("No image was return by the server. This should not happen. Verify that the server is correctly setup.");

            generating = false;
#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
#endif
            yield break;
        }

        // Decode the image from Base64 string into an array of bytes
        byte[] imageData = Convert.FromBase64String(sDResponseImg2Img.images[0]);

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
            generatedSeed = sDResponseImg2Img.info.seed;
        }
        catch (Exception e)
        {
            Debug.LogError($"{e.Message}\n\n{e.StackTrace}");
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
    private void LoadIntoImage(Texture2D texture)
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