using UnityEngine;

public class SpiritAnimationEvents : MonoBehaviour
{
    private SpiritController _controller;

    private void Awake()
    {
        _controller = GetComponentInParent<SpiritController>();
    }

    /// <summary>
    /// Tambahkan Animation Event bernama "OnFireProjectile" pada frame pelepasan serangan
    /// di masing-masing klip animasi Attack (Attack_1, Attack_2, Attack_3, SpecialAttack).
    /// </summary>
    public void OnFireProjectile()
    {
        if (_controller != null)
            _controller.TriggerAnimationProjectileRelease();
    }
}