using NaughtyAttributes;
using System;
using System.ComponentModel;
using System.Text;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Data structure for specifying settings of Stable Diffusion server API or 
/// default settings to use when adding new StableDiffusionMaterial or StableDIffusionImage 
/// to a Unity GameObject.
/// </summary>
public class SDSettings : ScriptableObject
{
    #region SD CONSTRAINTS
    public const int MIN_SIDE_LENGHT_IN_PIXELS = 128,
                    MAX_SIDE_LENGHT_IN_PIXELS = 2048,
                    MIN_CFG_SCALE = 1,
                    MAX_CFG_SCALE = 30,
                    MIN_SAMPLING_STEPS = 1,
                    MAX_SAMPLING_STEPS = 150;

#if UNITY_EDITOR
    public static readonly EditorWaitForSeconds requestCompletionCheckDeltaTime = new EditorWaitForSeconds(.75f);
#else
    public static readonly WaitForSecondsRealtime requestCompletionCheckDeltaTime = new WaitForSecondsRealtime(.75f);
#endif
    #endregion

    // TODO try to use InspectorName instead, as it is already defined in Unity
    public enum DefaultSamplers
    {
        [Description("Euler a")] Euler_a,
        Euler,
        LMS,
        Heun,
        DPM2,
        [Description("DPM2 a")] DPM2_a,
        [Description("DPM++ 2S a")] DPM_plusplus_2S_a,
        [Description("DPM++ 2M")] DPM_plusplus_2M,
        [Description("DPM++ SDE")] DPM_plusplus_SDE,
        [Description("DPM fast")] DPM_fast,
        [Description("DPM adaptive")] DPM_adaptive,
        [Description("LMS Karras")] LMS_Karras,
        [Description("DPM2 Karras")] DPM2_Karras,
        [Description("DPM2 a Karras")] DPM2_a_Karras,
        [Description("DPM++ 2S a Karras")] DPM_plusplus_2S_a_Karras,
        [Description("DPM++ 2M Karras")] DPM_plusplus_2M_Karras,
        [Description("DPM++ SDE Karras")] DPM_plusplus_SDE_Karras,
        DDIM,
        PLMS
    }

    [Header("AUTOMATIC1111 Settings")]

    public API_Endpoints apiEndpoints;
    [Serializable]
    public class API_Endpoints
    {
        [SerializeField] private string stableDiffusionServerURL = "http://127.0.0.1:7860";
        [SerializeField] private string modelsAPI = "/sdapi/v1/sd-models";
        [SerializeField] private string textToImageAPI = "/sdapi/v1/txt2img";
        [SerializeField] private string imageToImageAPI = "/sdapi/v1/img2img";
        [SerializeField] private string optionAPI = "/sdapi/v1/options";
        [SerializeField] private string progressAPI = "/sdapi/v1/progress";

        public string Models => stableDiffusionServerURL + modelsAPI;
        public string TextToImage => stableDiffusionServerURL + textToImageAPI;
        public string ImageToImage => stableDiffusionServerURL + imageToImageAPI;
        public string Option => stableDiffusionServerURL + optionAPI;
        public string Progress => stableDiffusionServerURL + progressAPI;
    }

    public SD_DefaultParams sdDefaultParams;
    [Serializable]
    public class SD_DefaultParams
    {
        [StringInEnumDesc(typeof(DefaultSamplers))] public string sampler = "Euler a";

        [Range(SDSettings.MIN_SAMPLING_STEPS, SDSettings.MAX_SAMPLING_STEPS)]
        public int steps = 50;

        [Range(SDSettings.MIN_CFG_SCALE, SDSettings.MAX_CFG_SCALE)]
        public float cfgScale = 7;

        [Range(SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
        public int width = 512;

        [Range(SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
        public int height = 512;

        public long seed = -1;
    }

    //[Header("API Settings")]
    public RequestSettings requestSettings;

    [Serializable]
    public class RequestSettings
    {
        public bool useAuth = false;
        public string username = "";
        public string password = "";
        public string requestContentType = "application/json";
        public string requestAccept = "application/json";
    }
    [SerializeField, HideInInspector] private string previousUsername = "";
    [SerializeField, HideInInspector] private string previousPassword = "";
    [SerializeField, HideInInspector] private string auth;


    [Header("Unity Settings")]
    public bool useUniversalRenderPipeline = false;
    public string outputFolder = "/streamingAssets";


    public string Authorization => auth ??= BuildAuth();

    private string BuildAuth()
    {
        auth = $"{requestSettings.username}:{requestSettings.password}";
        auth = System.Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(auth));
        auth = $"Basic {auth}";
        return auth;
    }

    private void OnValidate()
    {
        //Build a new auth string if auth data has changed
        if (previousUsername.Equals(requestSettings.username) && previousPassword.Equals(requestSettings.password))
            return;
        BuildAuth();
    }
}

/// <summary>
/// Data structure to easily serialize the parameters to send
/// to the Stable Diffusion server when generating an image via Txt2Img.
/// </summary>
[Serializable]
public class SDParamsInTxt2Img
{
    [Label("Highres fix"), AllowNesting, ToggleLeft] public bool enable_hr = false;

    [Label("Upscale by"), MinValue(1), ShowIf("enable_hr"), EnableIf("enable_hr"), AllowNesting]
    public float hr_scale = 2;

    [Label("Resize W"), ShowIf("enable_hr"), EnableIf("enable_hr"), AllowNesting, Tooltip("Resize width to\n(will take priority over upscale value)"), Range(0, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)] 
    public int hr_resize_x = 0;

    [Label("Resize H"), ShowIf("enable_hr"), EnableIf("enable_hr"), AllowNesting, Tooltip("Resize height to\n(will take priority over upscale value)"), Range(0, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
    public int hr_resize_y = 0;

    public int firstphase_width = 0; //???
    public int firstphase_height = 0;

    [Label("Upscaler"), ShowIf("enable_hr"), EnableIf("enable_hr"), AllowNesting] 
    public string hr_upscaler = "";

    [Label("Steps"), ShowIf("enable_hr"), EnableIf("enable_hr"), AllowNesting, Range(SDSettings.MIN_SAMPLING_STEPS, SDSettings.MAX_SAMPLING_STEPS), Tooltip("Highres Steps")] 
    public int hr_second_pass_steps = 0;

    [Label("Denoising"), ShowIf("enable_hr"), EnableIf("enable_hr"), AllowNesting, Range(0f, 1.0f), Tooltip("Denoising Strength")]
    public float denoising_strength = 0.75f;


    [TextArea(1, 5)] public string prompt = "";

    public string[] styles = { "" };
    public long seed = -1;
    public long subseed = -1;
    public float subseed_strength = 0;
    public int seed_resize_from_h = -1;
    public int seed_resize_from_w = -1;

    [Label("Sampling method"), AllowNesting, StringInEnumDesc(typeof(SDSettings.DefaultSamplers))]
    public string sampler_name = "Euler a";

    [Label("Batch Size"), AllowNesting, Range(1, 8)] public int batch_size = 1;
    [Label("Batch Count"), AllowNesting, Range(1, 10)] public int n_iter = 1;

    [Range(SDSettings.MIN_SAMPLING_STEPS, SDSettings.MAX_SAMPLING_STEPS)]
    public int steps = 50;

    [Label("CFG scale"), AllowNesting, Range(SDSettings.MIN_CFG_SCALE, SDSettings.MAX_CFG_SCALE)]
    public float cfg_scale = 7;

    [Range(SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
    public int width = 512;

    [Range(SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
    public int height = 512;

    [Label("Restore faces"), AllowNesting, ToggleLeft] public bool restore_faces = false;
    [ToggleLeft] public bool tiling = false;

    [TextArea(1, 5)] public string negative_prompt = "";

    public float eta = 0;
    public float s_churn = 0;
    public float s_tmax = 0;
    public float s_tmin = 0;
    public float s_noise = 1;
    public bool override_settings_restore_afterwards = true;
    public string sampler_index = "Euler";

    public void EnforceSD_Constraints()
    {
        //Even w/ Range attribute may still be needed, if potentially set via some script
        //Additional contraints may be added later here too.
        width = Math.Clamp(width, SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS);
        height = Math.Clamp(height, SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS);
        steps = Math.Clamp(steps, SDSettings.MIN_SAMPLING_STEPS, SDSettings.MAX_SAMPLING_STEPS);
        cfg_scale = Math.Clamp(cfg_scale, SDSettings.MIN_CFG_SCALE, SDSettings.MAX_CFG_SCALE);
    }
}

/// <summary>
/// Data structure to easily deserialize the data returned
/// by the Stable Diffusion server after generating an image via Txt2Img.
/// </summary>
[Serializable]
public class SDParamsOutTxt2Img
{
    public bool enable_hr = false;
    public float denoising_strength = 0;
    public int firstphase_width = 0;
    public int firstphase_height = 0;
    public float hr_scale = 2;
    public string hr_upscaler = "";
    public int hr_second_pass_steps = 0;
    public int hr_resize_x = 0;
    public int hr_resize_y = 0;
    public string prompt = "";
    public string[] styles = { "" };
    public long seed = -1;
    public long subseed = -1;
    public float subseed_strength = 0;
    public int seed_resize_from_h = -1;
    public int seed_resize_from_w = -1;
    public string sampler_name = "Euler a";
    public int batch_size = 1;
    public int n_iter = 1;
    public int steps = 50;
    public float cfg_scale = 7;
    public int width = 512;
    public int height = 512;
    public bool restore_faces = false;
    public bool tiling = false;
    public string negative_prompt = "";
    public float eta = 0;
    public float s_churn = 0;
    public float s_tmax = 0;
    public float s_tmin = 0;
    public float s_noise = 1;
    public SettingsOveride override_settings;
    public bool override_settings_restore_afterwards = true;
    public string[] script_args = { };
    public string sampler_index = "Euler";
    public string script_name = "";

    public class SettingsOveride
    {

    }
}

/// <summary>
/// Data structure to easily serialize the parameters to send
/// to the Stable Diffusion server when generating an image via Img2Img.
/// </summary>
public class SDParamsInImg2Img
{
    public string[] init_images = { "" };
    public int resize_mode = 0;

    [Range(0, 1.0f)] public float denoising_strength = 0.75f;

    //    public string mask = ""; // including this throws a 500 Internal Server error
    public int mask_blur = 4;
    public int inpainting_fill = 0;
    public bool inpaint_full_res = true;
    public int inpaint_full_res_padding = 0;
    public int inpainting_mask_invert = 0;
    public int initial_noise_multiplier = 1; // if 0, output image looks more blurry

    [TextArea(1, 5)] public string prompt = "";

    public string[] styles = { "" };
    public long seed = -1;
    public long subseed = -1;
    public int subseed_strength = 0;
    public int seed_resize_from_h = -1;
    public int seed_resize_from_w = -1;

    [StringInEnumDesc(typeof(SDSettings.DefaultSamplers))]
    public string sampler_name = "Euler a";

    [Label("Batch size")] public int batch_size = 1;
    [Label("Batch count")] public int n_iter = 1;

    [Range(SDSettings.MIN_SAMPLING_STEPS, SDSettings.MAX_SAMPLING_STEPS)]
    public int steps = 50;

    [Range(SDSettings.MIN_CFG_SCALE, SDSettings.MAX_CFG_SCALE)]
    public float cfg_scale = 7;

    [Range(SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
    public int width = 512;

    [Range(SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS)]
    public int height = 512;

    [Label("Restore faces")] public bool restore_faces = false;
    public bool tiling = false;

    [TextArea(1, 5)] public string negative_prompt = "";

    public float eta = 0;
    public float s_churn = 0;
    public float s_tmax = 0;
    public float s_tmin = 0;
    public float s_noise = 1;
    public SettingsOveride override_settings;
    public bool override_settings_restore_afterwards = true;
    public string[] script_args = { };
    public string sampler_index = "Euler";
    public bool include_init_images = false;
    //    public string script_name = ""; // including this throws a 422 Unprocessable Entity error

    public void EnforceSD_Constraints()
    {
        //Even w/ Range attribute may still be needed, if potentially set via some script
        //Additional contraints may be added later here too.
        width = Math.Clamp(width, SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS);
        height = Math.Clamp(height, SDSettings.MIN_SIDE_LENGHT_IN_PIXELS, SDSettings.MAX_SIDE_LENGHT_IN_PIXELS);
        steps = Math.Clamp(steps, SDSettings.MIN_SAMPLING_STEPS, SDSettings.MAX_SAMPLING_STEPS);
        cfg_scale = Math.Clamp(cfg_scale, SDSettings.MIN_CFG_SCALE, SDSettings.MAX_CFG_SCALE);
    }

    public class SettingsOveride
    {

    }
}

/// <summary>
/// Data structure to easily deserialize the data returned
/// by the Stable Diffusion server after generating an image via Img2Img.
/// </summary>
public class SDParamsOutImg2Img
{
    public string[] init_images = { "" };
    public float resize_mode = 0;
    public float denoising_strength = 0.75f;
    public string mask = "";
    public float mask_blur = 4;
    public float inpainting_fill = 0;
    public bool inpaint_full_res = true;
    public float inpaint_full_res_padding = 0;
    public float inpainting_mask_invert = 0;
    public float initial_noise_multiplier = 0;
    public string prompt = "";
    public string[] styles = { "" };
    public long seed = -1;
    public long subseed = -1;
    public float subseed_strength = 0;
    public float seed_resize_from_h = -1;
    public float seed_resize_from_w = -1;
    public string sampler_name = "";
    public float batch_size = 1;
    public float n_iter = 1;
    public int steps = 50;
    public float cfg_scale = 7;
    public int width = 512;
    public int height = 512;
    public bool restore_faces = false;
    public bool tiling = false;
    public string negative_prompt = "";
    public float eta = 0;
    public float s_churn = 0;
    public float s_tmax = 0;
    public float s_tmin = 0;
    public float s_noise = 1;
    public SettingsOveride override_settings;
    public bool override_settings_restore_afterwards = true;
    public string[] script_args = { };
    public string sampler_index = "Euler";
    public bool include_init_images = false;
    public string script_name = "";

    public class SettingsOveride
    {

    }
}

/// <summary>
/// Data structure to easily deserialize the JSON response returned
/// by the Stable Diffusion server after generating an image via Txt2Img.
///
/// It will contain the generated images (in Ascii Byte64 format) and
/// the parameters used by Stable Diffusion.
/// 
/// Note that the out parameters returned should be almost identical to the in
/// parameters that you have submitted to the server for image generation, 
/// to the exception of the seed which will contain the value of the seed used 
/// for the generation if you have used -1 for value (random).
/// </summary>
public class SDResponseTxt2Img
{
    public string[] images;
    public SDParamsOutTxt2Img parameters;
    public SDParamsOutTxt2Img info;
}

/// <summary>
/// Data structure to easily deserialize the JSON response returned
/// by the Stable Diffusion server after generating an image via Img2Img.
///
/// It will contain the generated images (in Ascii Byte64 format) and
/// the parameters used by Stable Diffusion.
/// 
/// Note that the out parameters returned should be almost identical to the in
/// parameters that you have submitted to the server for image generation, 
/// to the exception of the seed which will contain the value of the seed used 
/// for the generation if you have used -1 for value (random).
/// </summary>
public class SDResponseImg2Img
{
    public string[] images;
    public SDParamsOutImg2Img parameters;
    public SDParamsOutTxt2Img info;
}


/// <summary>
/// Data structure to help serialize into a JSON the model to be used by Stable Diffusion.
/// This is to send along a Set Option API request to the server.
/// </summary>
public record SDOption
{
    public string sd_model_checkpoint;
}

/// <summary>
/// Data structure to help deserialize from a JSON the state of the progress of an image generation.
/// </summary>
class SDProgressState
{
    public bool skipped;
    public bool interrupted;
    public string job;
    public int job_count;
    public string job_timestamp;
    public int job_no;
    public int sampling_step;
    public int sampling_steps;
}

/// <summary>
/// Data structure to help deserialize from a JSON the progress status of an image generation.
/// </summary>
class SDProgress
{
    public float progress;
    public float eta_relative;
    public SDProgressState state;
    public string current_image;
    public string textinfo;
}

