using System;
using UnityEngine;

public class Lifetime : MonoBehaviour
{
    [SerializeField] float lifetime = 10f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
