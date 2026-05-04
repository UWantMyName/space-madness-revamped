using UnityEngine;

/// <summary>
/// Self-configuring hit effect. Spawns three layered particle systems:
///
///   1. Impact Flash   — bright billboard that scales up and fades in ~0.1s
///   2. Energy Sparks  — fast stretched streaks shooting outward
///   3. Shockwave Ring — thin ring that expands and vanishes
///
/// Usage:
///   - Create an empty GameObject, attach this script.
///   - Set hitColor in the Inspector (or call SetColor() at spawn time).
///   - Call Play() to trigger the effect.
///   - The GameObject destroys itself after all particles have finished.
///
/// WeaponController / ProjectileBase can instantiate this at the hit point
/// and call Play() immediately.
/// </summary>
public class HitEffect : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Color")]
    [Tooltip("Primary color of the effect. Override per weapon type.")]
    public Color hitColor = new Color(0.3f, 0.8f, 1f, 1f); // default: cyan energy

    [Header("Scale")]
    [Tooltip("Overall scale multiplier. 1 = normal, 2 = twice as large.")]
    [Min(0.1f)]
    public float scale = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem _flash;
    private ParticleSystem _sparks;
    private ParticleSystem _ring;
    private bool           _built = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Override color before playing, useful when spawning from code.</summary>
    public void SetColor(Color color)
    {
        hitColor = color;
        ApplyColor();
    }

    /// <summary>Triggers all three effects. Call immediately after instantiating.</summary>
    public void Play()
    {
        if (!_built) Build();

        ApplyColor();

        _flash.Play();
        _sparks.Play();
        _ring.Play();

        // Self-destruct after the longest effect finishes
        float lifetime = Mathf.Max(
            _flash.main.duration  + _flash.main.startLifetime.constantMax,
            _sparks.main.duration + _sparks.main.startLifetime.constantMax,
            _ring.main.duration   + _ring.main.startLifetime.constantMax
        );
        Destroy(gameObject, lifetime + 0.1f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Build
    // ─────────────────────────────────────────────────────────────────────────

    private void Build()
    {
        if (_built) return;
        _built = true;

        _flash  = BuildFlash();
        _sparks = BuildSparks();
        _ring   = BuildRing();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  1. IMPACT FLASH
    //     Single burst — large, bright, fades out very fast.
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem BuildFlash()
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r    = go.GetComponent<ParticleSystemRenderer>();

        // Renderer — additive blend, no mesh, just a billboard quad
        r.renderMode         = ParticleSystemRenderMode.Billboard;
        r.material           = CreateAdditiveMaterial(hitColor);
        r.sortingOrder       = 2;

        var main             = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.duration        = 0.05f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.12f, 0.18f);
        main.startSpeed      = 0f;
        main.startSize       = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.0f * scale);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            WithAlpha(hitColor, 0.9f), WithAlpha(Color.white, 0.95f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission         = ps.emission;
        emission.rateOverTime = 0f;
        var burst            = new ParticleSystem.Burst(0f, 1);
        emission.SetBursts(new[] { burst });

        // Size over lifetime — scales UP then fades (handled by color alpha)
        var sol              = ps.sizeOverLifetime;
        sol.enabled          = true;
        var sizeCurve        = new AnimationCurve();
        sizeCurve.AddKey(0f,   0.2f);
        sizeCurve.AddKey(0.3f, 1.0f);
        sizeCurve.AddKey(1f,   1.4f);
        sol.size             = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over lifetime — quick fade out
        var col              = ps.colorOverLifetime;
        col.enabled          = true;
        var grad             = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(hitColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color            = new ParticleSystem.MinMaxGradient(grad);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. ENERGY SPARKS
    //     Fast particles stretched along velocity — look like light streaks.
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem BuildSparks()
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r    = go.GetComponent<ParticleSystemRenderer>();

        // Stretch along velocity direction
        r.renderMode             = ParticleSystemRenderMode.Stretch;
        r.velocityScale          = 0.08f;
        r.lengthScale            = 0f;
        r.material               = CreateAdditiveMaterial(hitColor);
        r.sortingOrder           = 1;

        var main                 = ps.main;
        main.loop                = false;
        main.playOnAwake         = false;
        main.duration            = 0.05f;
        main.startLifetime       = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed          = new ParticleSystem.MinMaxCurve(3f * scale, 9f * scale);
        main.startSize           = new ParticleSystem.MinMaxCurve(0.04f * scale, 0.1f * scale);
        main.startColor          = new ParticleSystem.MinMaxGradient(
            hitColor, WithAlpha(Color.white, 0.9f));
        main.gravityModifier     = 0f;
        main.simulationSpace     = ParticleSystemSimulationSpace.World;

        var emission             = ps.emission;
        emission.rateOverTime    = 0f;
        var burst                = new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(8, 14));
        emission.SetBursts(new[] { burst });

        // Emit in all directions
        var shape                = ps.shape;
        shape.enabled            = true;
        shape.shapeType          = ParticleSystemShapeType.Sphere;
        shape.radius             = 0.05f * scale;

        // Speed decreases as they travel
        var vel                  = ps.velocityOverLifetime;
        vel.enabled              = false;

        var lim                  = ps.limitVelocityOverLifetime;
        lim.enabled              = true;
        lim.limit                = new ParticleSystem.MinMaxCurve(2f * scale);
        lim.dampen               = 0.15f;

        // Fade out toward end of life
        var col                  = ps.colorOverLifetime;
        col.enabled              = true;
        var grad                 = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(hitColor, 0.4f),
                    new GradientColorKey(hitColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f) });
        col.color                = new ParticleSystem.MinMaxGradient(grad);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. SHOCKWAVE RING
    //     Thin ring that expands outward and fades quickly.
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem BuildRing()
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r    = go.GetComponent<ParticleSystemRenderer>();

        r.renderMode         = ParticleSystemRenderMode.Billboard;
        r.material           = CreateAdditiveMaterial(hitColor);
        r.sortingOrder       = 0;

        var main             = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.duration        = 0.05f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f * scale, 4f * scale);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f * scale, 0.14f * scale);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            WithAlpha(hitColor, 0.7f), WithAlpha(Color.white, 0.6f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission         = ps.emission;
        emission.rateOverTime = 0f;
        var burst            = new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(24, 32));
        emission.SetBursts(new[] { burst });

        // Emit in a thin ring on the XY plane (edge of a circle)
        var shape            = ps.shape;
        shape.enabled        = true;
        shape.shapeType      = ParticleSystemShapeType.Circle;
        shape.radius         = 0.05f * scale;
        shape.radiusThickness= 0f;    // emit only from the edge, not the fill
        shape.rotation       = new Vector3(0f, 0f, 0f);

        // Size shrinks as it expands (thinner ring as it travels outward)
        var sol              = ps.sizeOverLifetime;
        sol.enabled          = true;
        var sizeCurve        = new AnimationCurve();
        sizeCurve.AddKey(0f,  1.0f);
        sizeCurve.AddKey(1f,  0.2f);
        sol.size             = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Fade out
        var col              = ps.colorOverLifetime;
        col.enabled          = true;
        var grad             = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(hitColor, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color            = new ParticleSystem.MinMaxGradient(grad);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Color Application
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyColor()
    {
        if (_flash  != null) ApplyColorToSystem(_flash,  hitColor);
        if (_sparks != null) ApplyColorToSystem(_sparks, hitColor);
        if (_ring   != null) ApplyColorToSystem(_ring,   hitColor);
    }

    private void ApplyColorToSystem(ParticleSystem ps, Color color)
    {
        var r    = ps.GetComponent<ParticleSystemRenderer>();
        if (r    != null && r.material != null)
            r.material.color = color;

        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlpha(color, main.startColor.colorMin.a),
            WithAlpha(Color.white, main.startColor.colorMax.a));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Material Helper
    // ─────────────────────────────────────────────────────────────────────────

    private Material CreateAdditiveMaterial(Color color)
    {
        // Uses the built-in sprite shader in additive mode — no custom shader needed
        var mat         = new Material(Shader.Find("Sprites/Default"));
        mat.color       = color;

        // Enable additive blending so particles brighten whatever is behind them
        mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite",    0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        return mat;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}