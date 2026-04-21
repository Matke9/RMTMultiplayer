using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProjectileLauncher : NetworkBehaviour
{
    InputAction fireAction;
    [Header("References")]
    [SerializeField] GameObject serverProjectilePrefab;
    [SerializeField] GameObject clientProjectilePrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] GameObject muzzleFlash;
    [SerializeField] Collider2D playerCollider;
    
    [Header("Settings")]
    [SerializeField] float projectileSpeed;
    [SerializeField] float fireRate;
    [SerializeField] float muzzleFlashDuration;
    
    bool shouldFire = false;
    float previousFireTime;
    float muzzleFlashTimer;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }
        fireAction = InputSystem.actions.FindAction("Fire");
    }

    void Update()
    {
        if(muzzleFlashTimer > 0f)
        {
            muzzleFlashTimer -= Time.deltaTime;
            if(muzzleFlashTimer <= 0f)
            {
                muzzleFlash.SetActive(false);
            }
        }

        if (!IsOwner) { return; }

        if (fireAction.WasPressedThisFrame())
        {
            if(Time.time < (1/fireRate) + previousFireTime) { return; }

            previousFireTime = Time.time;

            PrimaryFireServerRpc();
            SpawnDummyProjectile();
        }
    }

    [ServerRpc]
    void PrimaryFireServerRpc()
    {
        GameObject projectileInstance = Instantiate(serverProjectilePrefab, 
            firePoint.position, firePoint.parent.parent.rotation, null);

        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        if(projectileInstance.TryGetComponent<DealDamageOnContact>(out DealDamageOnContact dealDamage))
        {
            dealDamage.setOwner(OwnerClientId);
        }

        if(projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = rb.transform.up * projectileSpeed; 
        }
        
        SpawnDummyProjectileClientRpc();
    }

    [ClientRpc]
    void SpawnDummyProjectileClientRpc()
    {
        if (IsOwner) { return; }
        SpawnDummyProjectile();
    }
    
    void SpawnDummyProjectile()
    {
        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;

        GameObject projectileInstance = Instantiate(clientProjectilePrefab, 
            firePoint.position, firePoint.parent.parent.rotation, null);

        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());
    
        if(projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = rb.transform.up * projectileSpeed; 
        }
    }
}
