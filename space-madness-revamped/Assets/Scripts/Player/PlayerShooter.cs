using UnityEngine;
using System.Collections.Generic;

public class PlayerShooter : MonoBehaviour
{
    [Header("Shooting")]
    public GameObject bulletPrefab;
    public List<Transform> firePoints = new List<Transform>();
    public float fireRate = 0.15f;

    private bool isFiring = false;
    private float nextFireTime = 0f;

    void Update()
    {
        bool fireDown = Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space);
        bool fireHeld = Input.GetButton("Fire1") || Input.GetKey(KeyCode.Space);

        // Tap: fires immediately on press
        if (fireDown)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
            isFiring = true;
        }

        // Hold: auto fire
        if (fireHeld && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }

        if (Input.GetButtonUp("Fire1") || Input.GetKeyUp(KeyCode.Space))
            isFiring = false;
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoints.Count == 0) return;

        foreach (Transform firePoint in firePoints)
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }
    }

    public void AddFirePoint(Transform newFirePoint)
    {
        firePoints.Add(newFirePoint);
    }
}