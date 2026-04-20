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
    
    [Header("Settings")]
    [SerializeField] float projectileSpeed;
    
    bool shouldFire = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }
        fireAction = InputSystem.actions.FindAction("Fire");
    }

    void Update()
    {
        if (!IsOwner) { return; }
        if (fireAction.WasPressedThisFrame())
        {
            PrimaryFireServerRpc();
            SpawnDummyProjectile();
        }
    }

    [ServerRpc]
    void PrimaryFireServerRpc()
    {
        GameObject projectileInstance = Instantiate(serverProjectilePrefab, 
            firePoint.position, firePoint.parent.parent.rotation, null);
        
        SpawnDummyProjectileClientRpc();
    }

    [ClientRpc]
    void SpawnDummyProjectileClientRpc()
    {
        if (IsOwner) { return; }
        SpawnDummyProjectile();
    }
    
    private void SpawnDummyProjectile()
    {
        GameObject projectileInstance = Instantiate(clientProjectilePrefab, 
            firePoint.position, firePoint.parent.parent.rotation, null);
    }
}
