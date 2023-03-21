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
    private const string StreamingAssetsFolder = "SDMaterials";

    [Range(1, 100)]
    public int tilingX = 1;

    [Range(1, 100)]
    public int tilingY = 1;

    [Range(0, 1)]
    public float metallic = 0.1f;

    [Range(0, 1)]
    public float smoothness = 0.5f;

    public bool generateNormalMap = true;

    [Range(0, 10)]
    public float normalMapStrength = 0.5f;

    private Texture2D generatedTexture = null;
    private Texture2D generatedNormal = null;

    public bool applyRecursively = true;

    // Keep track of material properties value, to detect if the user changes them on the fly, from the inspector
    private float _normalMapStrength = -1;
    private int _tilingX = -1;
    private int _tilingY = -1;
    private float _metallic = -1;
    private float _smoothness = -1;


    /// <summary>
    /// Get the mesh renderer in this object, or in childrens if allowed.
    /// </summary>
    /// <returns>The first mesh renderer found in the hierarchy at this level or in the children</returns>
    private MeshRenderer GetMeshRenderer()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null)
        {
            if (!applyRecursively)
                return null;

            MeshRenderer[] mrs = FindInChildrenAll<MeshRenderer>(this.gameObject);
            if (mrs == null || mrs.Length == 0)
                return null;

            mr = mrs[0];
        }

        return mr;
    }


#if UNITY_EDITOR
    /// <summary>
    /// On Awake, fill the properties with default values from the selected settings.
    /// </summary>
    protected new void Awake() => base.Awake();

    protected new void Start() => base.Start();

    /// <summary>
    /// Loop update
    /// </summary>
    protected new void Update()
    {
        base.Update();

        // Update normal map strength whenever the user modifies it in the inspector
        if (_normalMapStrength != normalMapStrength)
        {
            MeshRenderer mr = GetMeshRenderer();
            if (mr != null)
                mr.sharedMaterial.SetFloat("_BumpScale", normalMapStrength);

            UpdateMaterialProperties();

            _normalMapStrength = normalMapStrength;
        }

        // Update tilling, metallic and smoothness properties whenever the user modifies them in the inspector
        if (_tilingX != tilingX || _tilingY != tilingY || _metallic != metallic || _smoothness != smoothness)
        {
            UpdateMaterialProperties();

            _tilingX = tilingX;
            _tilingY = tilingY;
            _metallic = metallic;
            _smoothness = smoothness;
        }
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

                LoadIntoMaterial(texture);
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
    /// Load the texture into a material.
    /// </summary>
    /// <param name="texture">Texture to add to the material</param>
    private void LoadIntoMaterial(Texture2D texture)
    {
        try
        {
            MeshRenderer mr = GetMeshRenderer();
            if (mr == null)
                return;

            Shader standardShader = sdc.settings.useUniversalRenderPipeline ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");

            if (!standardShader)
                Debug.LogError("Shader setup wrong: Please check if you're project uses 'Standard' or 'Universal Render Pipeline'");

            mr.sharedMaterial = new Material(standardShader);
            mr.sharedMaterial.mainTexture = texture;
            generatedTexture = texture;

            // Apply the material to childrens if required
            if (applyRecursively)
            {
                MeshRenderer[] mrs = FindInChildrenAll<MeshRenderer>(this.gameObject);
                foreach (MeshRenderer m in mrs)
                    if (m != mr)
                    {
                        m.sharedMaterial = mr.sharedMaterial;
                    }
            }

            // Generate the normal map
            GenerateNormalMap();

            // Force the assets and scene to refresh with new material
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(generatedTexture);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                SceneView.RepaintAll();
                SceneView.FrameLastActiveSceneView();
                //SceneView.FocusWindowIfItsOpen(typeof(SceneView));
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

    /// <summary>
    /// Generate a normal map from the generated texture.
    /// </summary>
    public void GenerateNormalMap()
    {
        if (generatedTexture == null)
            return;

        try
        {
            MeshRenderer mr = GetMeshRenderer();
            if (mr == null)
                return;

            if (generateNormalMap)
            {
                generatedNormal = CreateNormalmap(generatedTexture, 0.5f);
#if UNITY_EDITOR
                EditorUtility.SetDirty(generatedNormal);
#endif
            }
            else
                generatedNormal = null;

            UpdateMaterialProperties();
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n\n" + e.StackTrace);
        }
    }

    /// <summary>
    /// Update the material properties.
    /// Also apply to children if set to apply recursively.
    /// </summary>
    private void UpdateMaterialProperties()
    {
        MeshRenderer mr = GetMeshRenderer();
        if (mr == null)
            return;

        // Apply tilling, metallic and smoothness
        mr.sharedMaterial.mainTextureScale = new Vector2(-tilingX, -tilingY);
        mr.sharedMaterial.SetFloat("_Metallic", metallic);
        mr.sharedMaterial.SetFloat("_Glossiness", smoothness);

        // Apply normal map if required
        if (generateNormalMap && generatedNormal != null)
        {
            mr.sharedMaterial.SetTexture("_BumpMap", generatedNormal);
            mr.sharedMaterial.SetFloat("_BumpScale", normalMapStrength);
            mr.sharedMaterial.EnableKeyword("_NORMALMAP");
        }
        // Disable normal map
        else
        {
            mr.sharedMaterial.SetTexture("_BumpMap", null);
            mr.sharedMaterial.DisableKeyword("_NORMALMAP");
        }

        // Apply recursively if required
        if (applyRecursively)
        {
            MeshRenderer[] mrs = FindInChildrenAll<MeshRenderer>(this.gameObject);
            foreach (MeshRenderer m in mrs)
                if (m != mr)
                {
                    m.sharedMaterial = mr.sharedMaterial;
                }
        }
    }

    /// <summary>
    /// Create a Normal map based on the gradient in 3x3 surrounding neighborhood.
    /// Based on UnityCoder code: https://github.com/unitycoder/NormalMapFromTexture
    /// </summary>
    /// <returns>Normal map texture</returns>
    /// <param name="t">Source texture</param>
    /// <param name="normalStrength">Normal map strength float (example: 1-20)</param>
    public static Texture2D CreateNormalmap(Texture2D t, float normalStrength)
    {
        Color[] pixels = new Color[t.width * t.height];
        Texture2D texNormal = new Texture2D(t.width, t.height, TextureFormat.RGB24, false, false);
        Vector3 vScale = new Vector3(0.3333f, 0.3333f, 0.3333f);

        Color tc;
        for (int y = 0; y < t.height; y++)
        {
            for (int x = 0; x < t.width; x++)
            {
                tc = t.GetPixel(x - 1, y - 1); Vector3 cSampleNegXNegY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x - 0, y - 1); Vector3 cSampleZerXNegY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x + 1, y - 1); Vector3 cSamplePosXNegY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x - 1, y - 0); Vector3 cSampleNegXZerY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x + 1, y - 0); Vector3 cSamplePosXZerY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x - 1, y + 1); Vector3 cSampleNegXPosY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x - 0, y + 1); Vector3 cSampleZerXPosY = new Vector3(tc.r, tc.g, tc.g);
                tc = t.GetPixel(x + 1, y + 1); Vector3 cSamplePosXPosY = new Vector3(tc.r, tc.g, tc.g);

                float fSampleNegXNegY = Vector3.Dot(cSampleNegXNegY, vScale);
                float fSampleZerXNegY = Vector3.Dot(cSampleZerXNegY, vScale);
                float fSamplePosXNegY = Vector3.Dot(cSamplePosXNegY, vScale);
                float fSampleNegXZerY = Vector3.Dot(cSampleNegXZerY, vScale);
                float fSamplePosXZerY = Vector3.Dot(cSamplePosXZerY, vScale);
                float fSampleNegXPosY = Vector3.Dot(cSampleNegXPosY, vScale);
                float fSampleZerXPosY = Vector3.Dot(cSampleZerXPosY, vScale);
                float fSamplePosXPosY = Vector3.Dot(cSamplePosXPosY, vScale);

                float edgeX = (fSampleNegXNegY - fSamplePosXNegY) * 0.25f + (fSampleNegXZerY - fSamplePosXZerY) * 0.5f + (fSampleNegXPosY - fSamplePosXPosY) * 0.25f;
                float edgeY = (fSampleNegXNegY - fSampleNegXPosY) * 0.25f + (fSampleZerXNegY - fSampleZerXPosY) * 0.5f + (fSamplePosXNegY - fSamplePosXPosY) * 0.25f;

                Vector2 vEdge = new Vector2(edgeX, edgeY) * normalStrength;
                Vector3 norm = new Vector3(vEdge.x, vEdge.y, 1.0f).normalized;
                Color c = new Color(norm.x * 0.5f + 0.5f, norm.y * 0.5f + 0.5f, norm.z * 0.5f + 0.5f, 1);

                pixels[x + y * t.width] = c;
            }
        }

        texNormal.SetPixels(pixels);
        texNormal.Apply();

        return texNormal;
    }
}