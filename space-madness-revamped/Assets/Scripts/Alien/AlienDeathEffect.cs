using System.Collections;
using UnityEngine;

/// <summary>
/// Code-driven death effect for aliens. No Animator required.
///
/// Sequence:
///   Phase 1 — Flash white + intense shake (~0.15s)
///   Phase 2 — Sprite fades out while square particles explode outward in a ring (~0.5s)
///
/// Usage:
///   Add this component to the alien prefab alongside AlienHealth.
///   AlienHealth will call Play(onComplete) instead of waiting a fixed duration.
///
/// The square particles use Unity's default particle sprite — no custom asset needed.
/// Set inheritSpriteColor to true to tint fragments to match the alien automatically.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AlienDeathEffect : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Phase 1 — Flash & Shake")]
    [Min(0.05f)]
    public float flashDuration  = 0.15f;
    public float shakeMagnitude = 0.12f;

    [Header("Phase 2 — Fragment Explosion")]
    [Min(0.1f)]
    public float fragmentDuration = 0.5f;

    [Range(6, 24)]
    public int fragmentCount = 12;

    public float fragmentSpeed = 4f;
    public float fragmentSize  = 0.18f;

    [Tooltip("If true, fragments inherit the SpriteRenderer's color automatically.")]
    public bool inheritSpriteColor = true;

    [Tooltip("Used only when inheritSpriteColor is false.")]
    public Color fragmentColor = Color.white;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private SpriteRenderer _renderer;
    private Vector3        _originalLocalPos;

    private void Awake()
    {
        _renderer         = GetComponent<SpriteRenderer>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the full death sequence.
    /// Calls onComplete when finished so AlienHealth can fire OnDeath and destroy.
    /// </summary>
    public void Play(System.Action onComplete)
    {
        // Capture world position immediately before any yields or controller changes
        StartCoroutine(DeathSequence(onComplete, transform.position));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator DeathSequence(System.Action onComplete, Vector3 worldPosition)
    {
        Color originalColor = _renderer.color;

        // ── Phase 1: Flash white + shake ──────────────────────────────────
        // Phase 1: Flash white + shake
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            float t         = elapsed / flashDuration;
            _renderer.color = Color.Lerp(Color.white, originalColor, t);
            transform.position = worldPosition + (Vector3)Random.insideUnitCircle * shakeMagnitude;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = worldPosition; // restore, not localPosition

        // ── Phase 2: Fragments explode, sprite fades out ──────────────────
        Color baseColor = inheritSpriteColor ? originalColor : fragmentColor;
        SpawnFragments(baseColor, worldPosition);

        elapsed = 0f;
        while (elapsed < fragmentDuration)
        {
            float t     = elapsed / fragmentDuration;
            Color c     = originalColor;
            c.a         = Mathf.Lerp(1f, 0f, t * 2f); // fade in the first half
            _renderer.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        _renderer.color = WithAlpha(originalColor, 0f);
        onComplete?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Fragment Particle System
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnFragments(Color baseColor, Vector3 worldPosition)
    {
        var go           = new GameObject("DeathFragments");
        go.transform.position = worldPosition;

        var ps           = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var r            = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode     = ParticleSystemRenderMode.Billboard;
        r.material       = CreateAdditiveMaterial(baseColor);
        r.sortingLayerName = _renderer.sortingLayerName;
        r.sortingOrder   = _renderer.sortingOrder + 1;

        var main                 = ps.main;
        main.loop                = false;
        main.playOnAwake         = false;
        main.duration            = 0.05f;
        main.startLifetime       = new ParticleSystem.MinMaxCurve(
            fragmentDuration * 0.8f, fragmentDuration);
        main.startSpeed          = new ParticleSystem.MinMaxCurve(
            fragmentSpeed * 0.7f, fragmentSpeed * 1.3f);
        main.startSize           = new ParticleSystem.MinMaxCurve(
            fragmentSize * 0.7f, fragmentSize * 1.3f);
        main.startColor          = new ParticleSystem.MinMaxGradient(
            WithAlpha(baseColor, 1f), WithAlpha(Color.white, 0.9f));
        main.startRotation       = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier     = 0f;
        main.simulationSpace     = ParticleSystemSimulationSpace.Local;
        main.maxParticles        = fragmentCount + 4;

        // All fragments burst at once
        var emission             = ps.emission;
        emission.rateOverTime    = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, fragmentCount) });

        // Ring emitter — fragments fly outward evenly in all directions
        var shape                = ps.shape;
        shape.enabled            = true;
        shape.shapeType          = ParticleSystemShapeType.Circle;
        shape.radius             = 0.1f;
        shape.radiusThickness    = 0f;
        shape.rotation           = new Vector3(0f, 0f, Random.Range(0f, 360f));

        // Decelerate as they travel
        var lim                  = ps.limitVelocityOverLifetime;
        lim.enabled              = true;
        lim.limit                = new ParticleSystem.MinMaxCurve(fragmentSpeed * 0.3f);
        lim.dampen               = 0.2f;

        // Fade from white flash → base color → transparent
        var col                  = ps.colorOverLifetime;
        col.enabled              = true;
        var grad                 = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(baseColor,   0.3f),
                new GradientColorKey(baseColor,   1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Shrink as they fade
        var sol                  = ps.sizeOverLifetime;
        sol.enabled              = true;
        var sizeCurve            = new AnimationCurve();
        sizeCurve.AddKey(0f,   1f);
        sizeCurve.AddKey(0.6f, 0.8f);
        sizeCurve.AddKey(1f,   0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Spin the squares as they fly
        var rot                  = ps.rotationOverLifetime;
        rot.enabled              = true;
        rot.z                    = new ParticleSystem.MinMaxCurve(
            -180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

        ps.Play();
        Destroy(go, fragmentDuration + 0.2f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private Material CreateAdditiveMaterial(Color color)
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

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}