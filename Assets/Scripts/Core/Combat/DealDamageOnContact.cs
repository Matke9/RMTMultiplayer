using Unity.Netcode;
using UnityEngine;

public class DealDamageOnContact : MonoBehaviour
{
    [SerializeField] int damage = 1;


    private ulong ownerClientId;

    public void setOwner(ulong ownerClientId)
    {
        this.ownerClientId = ownerClientId;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.attachedRigidbody == null){return;}

        if(collision.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            if(ownerClientId == netObj.OwnerClientId) {return;}
        }

        if(collision.attachedRigidbody.TryGetComponent<Health>(out Health health))
        {
            health.TakeDamage(damage);
        }
    }
}
