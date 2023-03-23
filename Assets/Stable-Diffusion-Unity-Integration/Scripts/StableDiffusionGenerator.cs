using NaughtyAttributes;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public partial class StableDiffusionGenerator : MonoBehaviour
{
    protected static StableDiffusionConfiguration sdc = null;
    private Coroutine _updateProgressRunning = null;

    [HideInInspector, ProgressBar("Generation Progress", 100f, EColor.Yellow)]
    public float progress = 0;

    protected IEnumerator ShowProgressAndWaitUntilDone(UnityWebRequest request)
    {
        UpdateGenerationProgress();
        while (!request.isDone)
        {
            yield return SDSettings.requestCompletionCheckDeltaTime;
        }
    }

    /// <summary>
    /// Update a generation progress bar
    /// </summary>
    protected void UpdateGenerationProgress()
    {
#if UNITY_EDITOR
        if (_updateProgressRunning != null) return;
        _updateProgressRunning = StartCoroutine(UpdateGenerationProgressCoroutine());
#endif
    }

    private IEnumerator UpdateGenerationProgressCoroutine()
    {
        // Stable diffusion API url for setting a model
        string url = sdc.settings.apiEndpoints.Progress;

        //EditorUtility.DisplayProgressBar("Generation in progress", "0%", 0);
        yield return SDSettings.requestCompletionCheckDeltaTime;

        while (progress < 100f)
            using (UnityWebRequest modelInfoRequest = UnityWebRequest.Get(url))
            {
                modelInfoRequest.SetupSDRequest<DownloadHandlerBuffer>(sdc.settings);
                yield return modelInfoRequest.SendWebRequest();

                // Deserialize the response to a class
                SDProgress sdp = JsonUtility.FromJson<SDProgress>(modelInfoRequest.downloadHandler.text);
                progress = sdp.progress * 100;

                if (progress == 0) progress = 100f; //The task has already ended!
                yield return SDSettings.requestCompletionCheckDeltaTime;
                //EditorUtility.DisplayProgressBar("Generation in progress", progress * 100 + "%", progress);
            }

        progress = 0f;
        _updateProgressRunning = null;
    }
}