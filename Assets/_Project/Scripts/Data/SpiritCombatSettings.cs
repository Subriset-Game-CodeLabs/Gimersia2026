using UnityEngine;

[CreateAssetMenu(fileName = "SpiritCombatSettings", menuName = "Game/Spirit Combat Settings")]
public class SpiritCombatSettings : ScriptableObject
{
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _attackRange = 8f;
    [SerializeField] private float _attackInterval = 1f;
    [Tooltip("Pengali kecepatan serangan dasar Spirit (mempercepat interval serangan sekaligus animasi Basic & Special Attack).")]
    [SerializeField, Min(0.1f)] private float _baseAttackSpeedMultiplier = 1.5f;
    [SerializeField] private TeamworkSettings _teamworkSettings;

    [Header("Off-Island Attack Radius (Laut & Bat) & Posisi Tepi Pulau")]
    [Tooltip("Radius jarak serang dari posisi Spirit terhadap musuh di laut maupun Bat yang terbang.")]
    [SerializeField, Min(0.5f)] private float _seaEnemyTargetableRadiusFromIsland = 6f;
    [Tooltip("Jarak berdiri Spirit dari tepi/bibir pulau saat menunggu atau menyerang musuh di luar pulau.")]
    [SerializeField, Min(0f)] private float _shoreStandOffDistance = 0.35f;
    [Tooltip("Jeda waktu (detik) sebelum Spirit bergerak mengejar ke sisi tepi pulau lain ketika musuh laut/Bat yang sempat masuk Attack Range keluar lagi dari jangkauan. Tidak berlaku untuk Worm.")]
    [SerializeField, Min(0f)] private float _offIslandOutOfRangeChaseDelay = 2.5f;

    public float Damage => Mathf.Max(0f, _damage);
    public float AttackRange => Mathf.Max(0f, _attackRange);
    public float AttackInterval => Mathf.Max(0.01f, _attackInterval);
    public float BaseAttackSpeedMultiplier => Mathf.Max(0.1f, _baseAttackSpeedMultiplier);
    public TeamworkSettings TeamworkSettings => _teamworkSettings;
    public float SeaEnemyTargetableRadiusFromIsland => Mathf.Max(0.5f, _seaEnemyTargetableRadiusFromIsland);
    public float ShoreStandOffDistance => Mathf.Max(0f, _shoreStandOffDistance);
    public float OffIslandOutOfRangeChaseDelay => Mathf.Max(0f, _offIslandOutOfRangeChaseDelay);
}