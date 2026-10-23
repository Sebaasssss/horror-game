using UnityEngine;
using UnityEngine.UI;

// Renders the game at 384x216 into a point-filtered texture, then shows it full screen through the CRT shader.
public class RetroDisplay
{
    public readonly Camera worldCamera;
    public readonly Transform uiRoot;
    public readonly RenderTexture target;
    private readonly RawImage screenImage;
    private readonly Material crtMaterial;
    private readonly Material plainMaterial;
    private readonly GameObject root;
    private Vector2 cameraCentre = new Vector2(0f, GameConfig.CameraY);
    private Vector2 shakeOffset;
    private bool crtEnabled = true;

    public bool CrtEnabled
    {
        get => crtEnabled;
        set
        {
            crtEnabled = value;
            if (screenImage != null)
                screenImage.material = crtEnabled && crtMaterial != null ? crtMaterial : plainMaterial;
        }
    }

    public Vector2 CameraCentre => cameraCentre;

    public RetroDisplay(Transform parent, bool createScreen = true)
    {
        root = new GameObject("Retro Display");
        root.transform.SetParent(parent, false);

        target = new RenderTexture(GameConfig.ScreenWidth, GameConfig.ScreenHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "Game 384x216",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            antiAliasing = 1
        };
        target.Create();

        GameObject cameraObject = new GameObject("World Camera");
        cameraObject.transform.SetParent(root.transform, false);
        worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = GameConfig.HalfHeight;
        worldCamera.clearFlags = CameraClearFlags.SolidColor;
        worldCamera.backgroundColor = new Color(0.02f, 0.015f, 0.03f);
        worldCamera.targetTexture = target;
        worldCamera.nearClipPlane = 0.1f;
        worldCamera.farClipPlane = 100f;
        worldCamera.allowMSAA = false;
        worldCamera.allowHDR = false;

        uiRoot = new GameObject("UI Root").transform;
        uiRoot.SetParent(cameraObject.transform, false);
        uiRoot.localPosition = new Vector3(0f, 0f, 10f);
        SetCamera(cameraCentre, Vector2.zero);

        Shader crtShader = Resources.Load<Shader>("Shaders/CRT");
        if (crtShader != null)
            crtMaterial = new Material(crtShader) { hideFlags = HideFlags.DontSave };
        plainMaterial = null;

        if (!createScreen)
            return;

        // The real screen only clears to black; the canvas draws the game texture on top of it.
        GameObject screenCameraObject = new GameObject("Screen Camera");
        screenCameraObject.transform.SetParent(root.transform, false);
        Camera screenCamera = screenCameraObject.AddComponent<Camera>();
        screenCamera.cullingMask = 0;
        screenCamera.clearFlags = CameraClearFlags.SolidColor;
        screenCamera.backgroundColor = Color.black;
        screenCamera.depth = 10;

        GameObject canvasObject = new GameObject("Screen Canvas", typeof(Canvas));
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        GameObject imageObject = new GameObject("Game Image", typeof(RectTransform), typeof(RawImage),
            typeof(AspectRatioFitter));
        imageObject.transform.SetParent(canvasObject.transform, false);
        screenImage = imageObject.GetComponent<RawImage>();
        screenImage.texture = target;
        screenImage.raycastTarget = false;
        AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)GameConfig.ScreenWidth / GameConfig.ScreenHeight;
        CrtEnabled = true;
    }

    public void SetCamera(Vector2 centre, Vector2 shake)
    {
        cameraCentre = centre;
        shakeOffset = new Vector2(Mathf.Round(shake.x), Mathf.Round(shake.y));
        Vector2 snapped = Nodes.Snap(centre) + shakeOffset;
        worldCamera.transform.position = new Vector3(snapped.x, snapped.y, -10f);
        // UI stays still while the world shakes.
        uiRoot.localPosition = new Vector3(-shakeOffset.x, -shakeOffset.y, 10f);
    }

    public void Tick(float time)
    {
        if (crtMaterial != null)
            crtMaterial.SetFloat("_TimeValue", time);
    }

    public Material CrtMaterial => crtMaterial;

    public void Dispose()
    {
        if (target != null)
            target.Release();
        Nodes.Destroy(root);
    }
}
