using System.Text;
using UnityEngine;
using UnityEngine.Networking;

static class UnityWebRequestExtensions
{
    /// <summary>
    /// Setup a Stable Diffusion web request.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="sdSettings"></param>
    /// <param name="requestBody"></param>
    /// <param name="setupHandler"></param>
    /// <returns>The value of useAuth in the provided settings.</returns>
    public static bool SetupSDRequest(this UnityWebRequest request, SDSettings sdSettings, string requestBody = null, DownloadHandler setupHandler = null)
    {
        request.SetRequestHeader("Content-Type", sdSettings.requestSettings.requestContentType);
        request.downloadHandler = setupHandler;
        if (setupHandler is not null) request.SetRequestHeader("Accept", sdSettings.requestSettings.requestAccept);
        if (requestBody is not null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));

        if (!sdSettings.requestSettings.useAuth) return false;
        IsAuthFieldIsNullOrEmpty(sdSettings.requestSettings.username, nameof(sdSettings.requestSettings.username));
        IsAuthFieldIsNullOrEmpty(sdSettings.requestSettings.password, nameof(sdSettings.requestSettings.password));

        request.SetRequestHeader("AUTHORIZATION", sdSettings.Authorization);
        return true;

        static void IsAuthFieldIsNullOrEmpty(string field, string nameOfField)
        {
            if (string.IsNullOrEmpty(field))
                Debug.LogWarning($"useAuth is set to true, but {nameOfField} is null or empty.");
        }
    }

    public static bool SetupSDRequest<T>(this UnityWebRequest request, SDSettings sdSettings, string requestBody = null) where T : DownloadHandler, new() =>
        SetupSDRequest(request, sdSettings, requestBody, new T());
}

