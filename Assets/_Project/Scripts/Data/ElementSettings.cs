using UnityEngine;

[CreateAssetMenu(fileName = "ElementSettings", menuName = "Game/Element Settings")]
public class ElementSettings : ScriptableObject
{
    [SerializeField] private int _basePower = 10;
    [SerializeField] private int _powerPerBonusStep = 10;
    // TODO(balance): these bonus values and the cooldown are not final.
    [SerializeField] private float _attackSpeedBonusPerStep = 0.05f;
    [SerializeField] private float _growthTimeReductionPerStep = 0.05f;
    [SerializeField] private float _minimumGrowthTimeMultiplier = 0.1f;
    [SerializeField] private float _reassignmentDuration  = 3f;
    [SerializeField] private float _advantageDamageMultiplier = 2f;
    [SerializeField] private float _neutralDamageMultiplier = 1f;
    [SerializeField] private float _resistedDamageMultiplier = 0.5f;
    public int BasePower => _basePower;
    public float ReassignmentDuration => Mathf.Max(0f, _reassignmentDuration );
    public float GetAttackSpeedMultiplier(int totalPower)
    {
        return 1f + BonusSteps(totalPower) * _attackSpeedBonusPerStep;
    }
    public float GetGrowthTimeMultiplier(int totalPower)
    {
        return Mathf.Max(
            _minimumGrowthTimeMultiplier,
            1f - BonusSteps(totalPower) * _growthTimeReductionPerStep);
    }
    public float GetDamageMultiplier(ElementType attacker, ElementType defender)
    {
        if (attacker == ElementType.None || defender == ElementType.None ||
            attacker == defender)
            return _neutralDamageMultiplier;
        bool advantage =
            (attacker == ElementType.Water && defender == ElementType.Fire) ||
            (attacker == ElementType.Fire && defender == ElementType.Lightning) ||
            (attacker == ElementType.Lightning && defender == ElementType.Water);
        return advantage
            ? _advantageDamageMultiplier
            : _resistedDamageMultiplier;
    }
    private int BonusSteps(int totalPower)
    {
        if (_powerPerBonusStep <= 0) return 0;
        // The base 10 is a starting value, not a free bonus step.
        return Mathf.Max(0, totalPower - _basePower) / _powerPerBonusStep;
    }
}
