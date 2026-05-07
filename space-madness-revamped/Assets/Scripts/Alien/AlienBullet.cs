using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AlienBullet : MonoBehaviour
{
    [Header("Stats")]
    public float speed = 6f;
    public int damage = 1;
    public float lifetime = 5f;

    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Start()
    {
        _rb.linearVelocity = Vector2.down * speed;
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
            health.TakeDamage(damage);

        Destroy(gameObject);
    }
}