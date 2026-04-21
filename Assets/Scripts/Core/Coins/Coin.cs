using Unity.Netcode;
using UnityEngine;

public abstract class Coin : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;


    protected int coinValue;
    protected bool alreadyCollected;

    public abstract int Collect();

    public void setValue(int value)
    {
        coinValue = value;
    }

    protected void Show(bool show)
    {
        spriteRenderer.enabled = show;
    }
}
