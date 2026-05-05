using UnityEngine;

/// <summary>
/// Self-configuring galaxy background effect.
/// Builds four layered particle systems entirely through code — no prefab setup needed.
///
/// Layers:
///   1. Distant Stars   — tiny, slow-drifting, gently twinkling points
///   2. Near Stars      — slightly larger, brighter, subtle pulse
///   3. Nebula Dust     — large semi-transparent soft blobs drifting downward
///   4. Comets          — rare, fast, stretched streaks across the screen
///
/// Setup:
///   - Create an empty GameObject, attach this script.
///   - Set Z position to something behind your gameplay layer (e.g. 10).
///   - The effect loops forever and never destroys itself.
/// </summary>
public class GalaxyBackground : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("Assign your scene camera. Screen dimensions are derived from it automatically.")]
    public Camera sceneCamera;

    // Derived at runtime from the camera
    private float screenWidth;
    private float screenHeight;

    [Header("Colors")]
    public Color coldStarColor   = new Color(0.75f, 0.90f, 1.00f, 1f);   // ice blue
    public Color warmStarColor   = new Color(1.00f, 0.95f, 0.85f, 1f);   // warm white
    public Color nebulaColorA    = new Color(0.45f, 0.20f, 0.70f, 1f);   // deep purple
    public Color nebulaColorB    = new Color(0.20f, 0.10f, 0.45f, 1f);   // dark violet
    public Color cometColor      = new Color(0.80f, 0.90f, 1.00f, 1f);   // blue-white

    [Header("Density")]
    [Tooltip("Approximate number of distant stars visible at once.")]
    [Range(40, 300)]
    public int distantStarCount = 160;

    [Tooltip("Approximate number of near stars visible at once.")]
    [Range(10, 100)]
    public int nearStarCount = 40;

    [Tooltip("Approximate number of nebula dust blobs visible at once.")]
    [Range(4, 30)]
    public int nebulaCount = 12;

    [Tooltip("Average seconds between comet appearances.")]
    [Range(2f, 20f)]
    public float cometInterval = 6f;

    [Header("Scroll Speed")]
    [Tooltip("How fast the background scrolls downward, simulating upward player movement.\n" +
             "Each layer moves at a different fraction of this for parallax depth.")]
    [Range(0.1f, 5f)]
    public float scrollSpeed = 1.2f;
    [Tooltip("Sorting layer name for the background particles.\n" +
             "Create a 'Background' sorting layer below 'Default' in Project Settings.")]
    public string sortingLayerName = "Background";

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (sceneCamera == null)
        {
            Debug.LogError("[GalaxyBackground] No camera assigned and Camera.main is null.", this);
            enabled = false;
            return;
        }

        screenHeight = sceneCamera.orthographicSize * 2f;
        screenWidth  = screenHeight * sceneCamera.aspect;

        BuildDistantStars();
        BuildNearStars();
        BuildNebulaDust();
        BuildComets();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  1. DISTANT STARS
    //     Tiny, slow drift downward, gentle twinkle via alpha pulse.
    // ─────────────────────────────────────────────────────────────────────────

    void BuildDistantStars()
    {
        var go = new GameObject("DistantStars");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetSortingLayer(go, sortingLayerName, -3);

        var r            = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode     = ParticleSystemRenderMode.Billboard;
        r.material       = CreateAdditiveMaterial(coldStarColor);

        var main             = ps.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.prewarm         = true;

        // Lifetime must cover full screen travel: spawn at +57% height, exit at -57% height
        // total distance = screenHeight * 1.15, slowest speed = scrollSpeed * 0.3
        float distantLifetime = (screenHeight * 1.15f) / (scrollSpeed * 0.3f);
        main.duration        = distantLifetime;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(distantLifetime * 0.9f, distantLifetime * 1.1f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            WithAlpha(coldStarColor, 0.5f), WithAlpha(warmStarColor, 0.9f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = distantStarCount + 50;

        var emission             = ps.emission;
        emission.rateOverTime    = distantStarCount / distantLifetime;

        // Spawn from a thin strip just above the top of the screen
        var shape            = ps.shape;
        shape.enabled        = true;
        shape.shapeType      = ParticleSystemShapeType.Rectangle;
        shape.scale          = new Vector3(screenWidth * 1.1f, screenHeight * 0.01f, 0f);
        shape.position       = new Vector3(0f, screenHeight * 0.57f, 0f);

        // Drift slowly downward
        var vel              = ps.velocityOverLifetime;
        vel.enabled          = true;
        vel.space            = ParticleSystemSimulationSpace.World;
        vel.x                = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y                = new ParticleSystem.MinMaxCurve(-scrollSpeed * 0.4f, -scrollSpeed * 0.3f);
        vel.z                = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Twinkle — pulse alpha over lifetime
        var col              = ps.colorOverLifetime;
        col.enabled          = true;
        var grad             = new Gradient();
        grad.mode            = GradientMode.Blend;
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(coldStarColor, 0f),
                new GradientColorKey(warmStarColor, 0.5f),
                new GradientColorKey(coldStarColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(0.7f, 0.15f),
                new GradientAlphaKey(1f,   0.4f),
                new GradientAlphaKey(0.5f, 0.7f),
                new GradientAlphaKey(0.8f, 0.85f),
                new GradientAlphaKey(0f,   1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Play();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. NEAR STARS
    //     Slightly larger and brighter, occasional warm tint.
    // ─────────────────────────────────────────────────────────────────────────

    void BuildNearStars()
    {
        var go = new GameObject("NearStars");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetSortingLayer(go, sortingLayerName, -2);

        var r            = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode     = ParticleSystemRenderMode.Billboard;
        r.material       = CreateAdditiveMaterial(warmStarColor);

        var main             = ps.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.prewarm         = true;

        float nearLifetime   = (screenHeight * 1.15f) / (scrollSpeed * 0.6f);
        main.duration        = nearLifetime;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(nearLifetime * 0.9f, nearLifetime * 1.1f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            WithAlpha(coldStarColor, 0.6f), WithAlpha(warmStarColor, 1f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = nearStarCount + 20;

        var emission          = ps.emission;
        emission.rateOverTime = nearStarCount / nearLifetime;

        var shape            = ps.shape;
        shape.enabled        = true;
        shape.shapeType      = ParticleSystemShapeType.Rectangle;
        shape.scale          = new Vector3(screenWidth * 1.1f, screenHeight * 0.01f, 0f);
        shape.position       = new Vector3(0f, screenHeight * 0.57f, 0f);

        var vel              = ps.velocityOverLifetime;
        vel.enabled          = true;
        vel.space            = ParticleSystemSimulationSpace.World;
        vel.x                = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y                = new ParticleSystem.MinMaxCurve(-scrollSpeed * 0.7f, -scrollSpeed * 0.6f);
        vel.z                = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Sharper twinkle — brighter pulse
        var col              = ps.colorOverLifetime;
        col.enabled          = true;
        var grad             = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(coldStarColor, 0f),
                new GradientColorKey(Color.white,   0.5f),
                new GradientColorKey(warmStarColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(0.9f, 0.1f),
                new GradientAlphaKey(0.6f, 0.45f),
                new GradientAlphaKey(1.0f, 0.55f),
                new GradientAlphaKey(0.4f, 0.8f),
                new GradientAlphaKey(0f,   1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Play();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. NEBULA DUST
    //     Large, very soft, semi-transparent blobs drifting slowly downward.
    //     Purple → violet color gives the nebula feel.
    // ─────────────────────────────────────────────────────────────────────────

    void BuildNebulaDust()
    {
        var go = new GameObject("NebulaDust");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetSortingLayer(go, sortingLayerName, -4);

        var r                   = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode            = ParticleSystemRenderMode.Billboard;
        r.material              = CreateSoftMaterial(nebulaColorA);
        r.minParticleSize       = 0f;
        r.maxParticleSize       = 2f;

        var main                = ps.main;
        main.loop               = true;
        main.playOnAwake        = true;
        main.prewarm            = true;

        float nebulaLifetime    = (screenHeight * 1.15f) / (scrollSpeed * 0.15f);
        main.duration           = nebulaLifetime;
        main.startLifetime      = new ParticleSystem.MinMaxCurve(nebulaLifetime * 0.9f, nebulaLifetime * 1.1f);
        main.startSpeed         = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startSize          = new ParticleSystem.MinMaxCurve(1.5f, 4.0f);
        main.startColor         = new ParticleSystem.MinMaxGradient(
            WithAlpha(nebulaColorB, 0.04f), WithAlpha(nebulaColorA, 0.10f));
        main.gravityModifier    = 0f;
        main.simulationSpace    = ParticleSystemSimulationSpace.World;
        main.maxParticles       = nebulaCount + 8;

        var emission            = ps.emission;
        emission.rateOverTime   = nebulaCount / nebulaLifetime;

        var shape               = ps.shape;
        shape.enabled           = true;
        shape.shapeType         = ParticleSystemShapeType.Rectangle;
        shape.scale             = new Vector3(screenWidth * 1.1f, screenHeight * 0.01f, 0f);
        shape.position          = new Vector3(0f, screenHeight * 0.57f, 0f);

        var vel                 = ps.velocityOverLifetime;
        vel.enabled             = true;
        vel.space               = ParticleSystemSimulationSpace.World;
        vel.x                   = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        vel.y                   = new ParticleSystem.MinMaxCurve(-scrollSpeed * 0.2f, -scrollSpeed * 0.15f);
        vel.z                   = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Very gentle fade in and out
        var col                 = ps.colorOverLifetime;
        col.enabled             = true;
        var grad                = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(nebulaColorB, 0f),
                new GradientColorKey(nebulaColorA, 0.5f),
                new GradientColorKey(nebulaColorB, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.08f, 0.2f),
                new GradientAlphaKey(0.1f,  0.5f),
                new GradientAlphaKey(0.06f, 0.8f),
                new GradientAlphaKey(0f,    1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Slowly rotate
        var rot                 = ps.rotationOverLifetime;
        rot.enabled             = true;
        rot.z                   = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        ps.Play();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  4. COMETS
    //     Rare, fast, stretched streaks. Low emission rate means they appear
    //     occasionally like a shooting star rather than constantly.
    // ─────────────────────────────────────────────────────────────────────────

    void BuildComets()
    {
        var go = new GameObject("Comets");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetSortingLayer(go, sortingLayerName, -1);

        var r                   = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode            = ParticleSystemRenderMode.Stretch;
        r.velocityScale         = 0.12f;
        r.lengthScale           = 0f;
        r.material              = CreateAdditiveMaterial(cometColor);

        var main                = ps.main;
        main.loop               = true;
        main.playOnAwake        = true;
        main.duration           = cometInterval;
        main.startLifetime      = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed         = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSize          = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
        main.startColor         = new ParticleSystem.MinMaxGradient(
            WithAlpha(cometColor, 0.6f), WithAlpha(Color.white, 0.95f));
        main.gravityModifier    = 0f;
        main.simulationSpace    = ParticleSystemSimulationSpace.World;
        main.maxParticles       = 6;

        // Low emission — one comet every few seconds
        var emission            = ps.emission;
        emission.rateOverTime   = 1f / cometInterval;

        // Spawn from the top edge, aimed downward with slight random angle
        var shape               = ps.shape;
        shape.enabled           = true;
        shape.shapeType         = ParticleSystemShapeType.Rectangle;
        shape.scale             = new Vector3(screenWidth * 1.1f, 0.1f, 0f);
        shape.position          = new Vector3(0f, screenHeight * 0.57f, 0f);
        shape.rotation          = new Vector3(0f, 0f, 0f);

        // Aim downward with slight random spread
        var velocity            = ps.inheritVelocity;
        velocity.enabled        = false;

        var forceField          = ps.externalForces;
        forceField.enabled      = false;

        // Particles travel downward
        var vel                 = ps.velocityOverLifetime;
        vel.enabled             = true;
        vel.space               = ParticleSystemSimulationSpace.World;
        vel.x                   = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        vel.y                   = new ParticleSystem.MinMaxCurve(-(12f + scrollSpeed), -(8f + scrollSpeed));
        vel.z                   = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Fade in quickly, long tail fade out
        var col                 = ps.colorOverLifetime;
        col.enabled             = true;
        var grad                = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white,  0f),
                new GradientColorKey(cometColor,   0.3f),
                new GradientColorKey(cometColor,   1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.08f),
                new GradientAlphaKey(0.8f, 0.4f),
                new GradientAlphaKey(0f,   1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Shrink along their length as they fade
        var sol                 = ps.sizeOverLifetime;
        sol.enabled             = true;
        var sizeCurve           = new AnimationCurve();
        sizeCurve.AddKey(0f,   0.3f);
        sizeCurve.AddKey(0.1f, 1.0f);
        sizeCurve.AddKey(1f,   0.1f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ps.Play();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Material Helpers
    // ─────────────────────────────────────────────────────────────────────────

    Material CreateAdditiveMaterial(Color color)
    {
        var mat   = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite",   0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    // Soft blend for nebula — uses normal alpha blending rather than additive
    // so it doesn't over-brighten the background
    Material CreateSoftMaterial(Color color)
    {
        var mat   = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",   0);
        mat.renderQueue = 3000;
        return mat;
    }

    void SetSortingLayer(GameObject go, string layerName, int order)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.sortingLayerName = layerName;
        r.sortingOrder     = order;
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}