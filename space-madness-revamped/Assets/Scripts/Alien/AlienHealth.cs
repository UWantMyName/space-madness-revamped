using System.Collections;
using UnityEngine;

/// <summary>
/// Tracks alien HP, triggers hit reactions, and handles the death sequence.
///
/// Death sequence:
///   1. Disable AlienController (stops movement and shooting).
///   2. Trigger the death animation on the Animator.
///   3. Wait for the animation to finish.
///   4. Fire OnDeath (WaveManager listens to this).
///   5. Destroy the GameObject.
/// </summary>
[RequireComponent(typeof(AlienController))]
public class AlienHealth : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Death Animation")]
    [Tooltip("Animator trigger name that starts the death animation.")]
    public string deathTrigger = "Die";

    [Tooltip("How long to wait for the death animation before force-destroying.\n" +
             "Set this to match your animation clip length.")]
    [Min(0f)]
    public float deathAnimationDuration = 0.6f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired after the death animation completes, just before the GameObject is destroyed.
    /// WaveManager subscribes to this to track wave completion.
    /// </summary>
    public event System.Action OnDeath;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public int  CurrentHP  { get; private set; }
    public bool IsDead     { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private AlienController  _controller;
    private Animator         _animator;
    private AlienHitReaction _hitReaction;
    private AlienDeathEffect _deathEffect; // optional — code-driven death sequence

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _controller  = GetComponent<AlienController>();
        _animator    = GetComponent<Animator>();
        _hitReaction = GetComponent<AlienHitReaction>();
        _deathEffect = GetComponent<AlienDeathEffect>();
    }

    private void Start()
    {
        if (_controller.definition == null)
        {
            Debug.LogError("[AlienHealth] AlienController has no definition — cannot read maxHP.", this);
            enabled = false;
            return;
        }

        CurrentHP = _controller.definition.maxHP;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Collision
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Bullet>() != null)
        {
            Destroy(other.gameObject);
            TakeDamage(1);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Overrides the max HP from the AlienDefinition.
    /// Call immediately after spawning, before any damage is dealt.
    /// Used by WaveManager when a chapter file specifies a health override.
    /// </summary>
    public void SetMaxHP(int newMaxHP)
    {
        CurrentHP = Mathf.Max(1, newMaxHP);
    }

    /// <summary>
    /// Deals damage to the alien.
    /// Safe to call multiple times — ignores hits after death.
    /// </summary>
    public void TakeDamage(int amount = 1)
    {
        if (IsDead) return;

        CurrentHP -= amount;

        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            StartCoroutine(DieRoutine());
        }
        else
        {
            _hitReaction?.Play();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Death Sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator DieRoutine()
    {
        IsDead = true;
        _controller.enabled = false;

        if (_deathEffect != null)
        {
            // Code-driven sequence — AlienDeathEffect calls back when done
            bool done = false;
            _deathEffect.Play(() => done = true);
            yield return new WaitUntil(() => done);
        }
        else
        {
            // Fallback: Animator trigger + fixed wait (original behaviour)
            if (_animator != null)
                _animator.SetTrigger(deathTrigger);

            yield return new WaitForSeconds(deathAnimationDuration);
        }

        OnDeath?.Invoke();
        Destroy(gameObject);
    }
}