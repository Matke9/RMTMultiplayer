using UnityEngine;

public class SpawnObjectOnDestroy : MonoBehaviour
{
    [SerializeField] private GameObject spawnedObject;
    
    private void OnDestroy()
    {
        Instantiate(spawnedObject, transform.position, Quaternion.identity);
    }
}
