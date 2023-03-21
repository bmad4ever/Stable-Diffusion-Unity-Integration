using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public partial class StableDiffusionGenerator : MonoBehaviour
{
    protected static StableDiffusionConfiguration sdc = null;
    private Coroutine _updateProgressRunning = null;

    public static readonly WaitForSeconds requestCompletionCheckDeltaTime = new WaitForSeconds(0.5f);

    protected IEnumerator ShowProgressAndWaitUntilDone(UnityWebRequest request)
    {
        while (!request.isDone)
        {
            UpdateGenerationProgress();
            yield return requestCompletionCheckDeltaTime;
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
        string url = sdc.settings.StableDiffusionServerURL + sdc.settings.ProgressAPI;
        using (UnityWebRequest modelInfoRequest = UnityWebRequest.Get(url))
        {
            modelInfoRequest.SetupSDRequest<DownloadHandlerBuffer>(sdc.settings);
            yield return modelInfoRequest.SendWebRequest();

            // Deserialize the response to a class
            SDProgress sdp = JsonUtility.FromJson<SDProgress>(modelInfoRequest.downloadHandler.text);
            float progress = sdp.progress;
            EditorUtility.DisplayProgressBar("Generation in progress", progress * 100 + "%", progress);
        }

        _updateProgressRunning = null;
    }
}