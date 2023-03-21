using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class StableDiffusionGenerator : MonoBehaviour
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

    /// <summary>
    /// Find all the game objects that contains a certain component type.
    /// </summary>
    /// <typeparam name="T">Type of component to search for</typeparam>
    /// <param name="g">Game object for which to search it's children</param>
    /// <param name="active">The game object must be active (true) or can also be not active (false)</param>
    /// <returns>Array of game object found, all of which containing a component of the specified type</returns>
    public static T[] FindInChildrenAll<T>(GameObject g, bool active = true) where T : class
    {
        List<T> list = new List<T>();

        // Search in all the children of the specified game object
        foreach (Transform t in g.transform)
        {
            // GameObject has no children, skip it
            if (t == null)
                continue;

            if (active && !t.gameObject.activeSelf)
                continue;

            // Found one, check component
            T comp = t.GetComponent<T>();
            if (comp != null && comp.ToString() != "null")
                list.Add(comp);

            // Recursively search into the children of this game object
            T[] compo = FindInChildrenAll<T>(t.gameObject);
            if (compo != null && compo.Length > 0)
            {
                foreach (T tt in compo)
                    list.Add(tt);
            }
        }

        // Not found, return null
        return list.ToArray();
    }
}