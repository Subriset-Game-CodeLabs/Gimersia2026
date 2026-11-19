using UnityEngine;

[CreateAssetMenu(fileName = "SpiritCombatSettings", menuName = "Game/Spirit Combat Settings")]
public class SpiritCombatSettings : ScriptableObject
{
    // TODO(balance): these combat values are placeholders.
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _attackRange = 8f;
    [SerializeField] private float _attackInterval = 1f;
    [SerializeField] private TeamworkSettings _teamworkSettings;

    public float Damage => Mathf.Max(0f, _damage);
    public float AttackRange => Mathf.Max(0f, _attackRange);
    public float AttackInterval => Mathf.Max(0.01f, _attackInterval);
    public TeamworkSettings TeamworkSettings => _teamworkSettings;
}
