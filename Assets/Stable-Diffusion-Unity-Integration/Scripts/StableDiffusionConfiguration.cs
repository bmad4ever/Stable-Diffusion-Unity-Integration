using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Global Stable Diffusion parameters configuration.
/// </summary>
[ExecuteInEditMode]
public class StableDiffusionConfiguration : MonoBehaviour
{
    [SerializeField]
    public SDSettings settings;

    [SerializeField]
    public string[] samplers = new string[]{
        "Euler a", "Euler", "LMS", "Heun", "DPM2", "DPM2 a", "DPM++ 2S a", "DPM++ 2M", "DPM++ SDE", "DPM fast", "DPM adaptive",
        "LMS Karras", "DPM2 Karras", "DPM2 a Karras", "DPM++ 2S a Karras", "DPM++ 2M Karras", "DPM++ SDE Karras", "DDIM", "PLMS"
    };

    [SerializeField]
    public string[] modelNames;

    /// <summary>
    /// Data structure that represents a Stable Diffusion model to help deserialize from JSON string.
    /// </summary>
    class Model
    {
        public string title;
        public string model_name;
        public string hash;
        public string sha256;
        public string filename;
        public string config;
    }

    /// <summary>
    /// Method called when the user click on List Model from the inspector.
    /// </summary>
    public void ListModels()
    {
        StartCoroutine(ListModelsAsync());
    }

    /// <summary>
    /// Get the list of available Stable Diffusion models.
    /// </summary>
    /// <returns></returns>
    IEnumerator ListModelsAsync()
    {
        // Stable diffusion API url for getting the models list
        string url = settings.apiEndpoints.Models;
        UnityWebRequest request = new UnityWebRequest(url, "GET");
        request.SetupSDRequest<DownloadHandlerBuffer>(settings);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(request.error);
            yield break;
        }

        //Get array of model names from retrieved data
        Model[] ms = JsonUtility.FromJson<Model[]>(request.downloadHandler.text);
        List<string> modelsNames = new List<string>();
        foreach (Model m in ms)
            modelsNames.Add(m.model_name);
        modelNames = modelsNames.ToArray();
    }

    /// <summary>
    /// Set a model to use by Stable Diffusion.
    /// </summary>
    /// <param name="modelName">Model to set</param>
    /// <returns></returns>
    public IEnumerator SetModelAsync(string modelName)
    {
        // Stable diffusion API url for setting a model
        string url = settings.apiEndpoints.Option;

        // Load the list of models if not filled already
        if (modelNames == null || modelNames.Length == 0)
            yield return ListModelsAsync();

        // Tell Stable Diffusion to use the specified model using an HTTP POST request
        var sd = new SDOption { sd_model_checkpoint = modelName };
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.SetupSDRequest(settings, JsonUtility.ToJson(sd));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(request.error);
                yield break;
            }
            Debug.Log(request.result);
        }
    }
}
