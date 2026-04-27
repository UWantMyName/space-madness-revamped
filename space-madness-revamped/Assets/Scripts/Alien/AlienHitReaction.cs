using System.Collections;
using UnityEngine;

/// <summary>
/// Self-contained hit reaction: flashes the sprite white and shakes the transform.
/// Add this alongside AlienHealth on the alien prefab.
/// Reusable on anything with a SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AlienHitReaction : MonoBehaviour
{
    [Header("Flash")]
    [Tooltip("Color the sprite flashes to on hit.")]
    public Color flashColor = Color.white;

    [Tooltip("How long the flash lasts in seconds.")]
    [Min(0.01f)]
    public float flashDuration = 0.08f;

    [Header("Shake")]
    [Tooltip("How far the sprite shakes from its current position (world units).")]
    public float shakeMagnitude = 0.08f;

    [Tooltip("How long the shake lasts in seconds.")]
    [Min(0.01f)]
    public float shakeDuration = 0.12f;

    // ─────────────────────────────────────────────────────────────────────────

    private SpriteRenderer _renderer;
    private Color          _originalColor;
    private Vector3        _originalLocalPosition;
    private Coroutine      _flashCoroutine;
    private Coroutine      _shakeCoroutine;

    private void Awake()
    {
        _renderer      = GetComponent<SpriteRenderer>();
        _originalColor = _renderer.color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Triggers the flash and shake. Safe to call while a reaction is already playing.</summary>
    public void Play()
    {
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);

        // Always restore before restarting so we don't compound offsets
        _renderer.color      = _originalColor;
        transform.localPosition = _originalLocalPosition;

        _flashCoroutine = StartCoroutine(FlashRoutine());
        _shakeCoroutine = StartCoroutine(ShakeRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Coroutines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator FlashRoutine()
    {
        _renderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        _renderer.color = _originalColor;
        _flashCoroutine = null;
    }

    private IEnumerator ShakeRoutine()
    {
        _originalLocalPosition = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float progress = elapsed / shakeDuration;
            // Magnitude fades out toward the end of the shake
            float magnitude = shakeMagnitude * (1f - progress);

            transform.localPosition = _originalLocalPosition + (Vector3)Random.insideUnitCircle * magnitude;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = _originalLocalPosition;
        _shakeCoroutine = null;
    }
}