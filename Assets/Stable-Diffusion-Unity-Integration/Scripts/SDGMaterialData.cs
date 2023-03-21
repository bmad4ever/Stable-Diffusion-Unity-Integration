using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;



public partial class StableDiffusionGenerator : MonoBehaviour
{
    [Serializable]
    public class SDGMaterialData
    {
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

        [HideInInspector] public Texture2D generatedTexture = null;
        [HideInInspector] public Texture2D generatedNormal = null;

        public bool applyRecursively = true;


        public static (bool, bool) operator ==(SDGMaterialData m1, SDGMaterialData m2) =>
                (m1.normalMapStrength == m2.normalMapStrength,
                m1.metallic == m2.metallic &&
                m1.tilingX == m2.tilingX &&
                m1.tilingY == m2.tilingY &&
                m1.smoothness == m2.smoothness);

        public static (bool, bool) operator !=(SDGMaterialData m1, SDGMaterialData m2)
        {
            (bool equalNormalMapStrength, bool equalOtherFields) = m1 == m2;
            return (!equalNormalMapStrength, !equalOtherFields);
        }

        public void CopyFrom(SDGMaterialData m2)
        {
            normalMapStrength = m2.normalMapStrength;
            tilingX = m2.tilingX;
            tilingY = m2.tilingY;
            metallic = m2.metallic;
            smoothness = m2.smoothness;
        }


        /// <summary>
        /// Get the mesh renderer in this object, or in childrens if allowed.
        /// </summary>
        /// <returns>The first mesh renderer found in the hierarchy at this level or in the children</returns>
        public MeshRenderer GetMeshRenderer(GameObject gameObject)
        {
            MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                if (!applyRecursively)
                    return null;

                mr = gameObject.FindComponentInDescendants<MeshRenderer>();
            }

            return mr;
        }


        /// <summary>
        /// Update the material properties.
        /// Also apply to children if set to apply recursively.
        /// </summary>
        public void UpdateMaterialProperties(GameObject gameObject)
        {
            MeshRenderer mr = GetMeshRenderer(gameObject);
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
                IEnumerable<MeshRenderer> mrs = gameObject.FindComponentsInDescendants<MeshRenderer>();
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


        /// <summary>
        /// Generate a normal map from the generated texture.
        /// </summary>
        public void GenerateNormalMap(GameObject gameObject)
        {
            if (generatedTexture == null)
                return;

            try
            {
                MeshRenderer mr = GetMeshRenderer(gameObject);
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

                UpdateMaterialProperties(gameObject);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + "\n\n" + e.StackTrace);
            }
        }


        /// <summary>
        /// Load the texture into a material.
        /// </summary>
        /// <param name="texture">Texture to add to the material</param>
        public void LoadIntoMaterial(Texture2D texture, GameObject gameObject, StableDiffusionConfiguration sdc)
        {
            try
            {
                MeshRenderer mr = GetMeshRenderer(gameObject);
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
                    List<MeshRenderer> mrs = gameObject.FindComponentsInDescendants<MeshRenderer>();
                    foreach (MeshRenderer m in mrs)
                        if (m != mr)
                        {
                            m.sharedMaterial = mr.sharedMaterial;
                        }
                }

                // Generate the normal map
                GenerateNormalMap(gameObject);

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
    }

}