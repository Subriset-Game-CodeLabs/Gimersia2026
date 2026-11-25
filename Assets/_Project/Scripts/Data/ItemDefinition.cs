using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Game/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _title;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;
    [SerializeField] private ItemEffectType _effect;

    [Header("Effect Settings - only relevant fields are used")]
    // TODO(balance): designer placeholders, not final balance values.
    [SerializeField, Min(0.01f)] private float _radius = 3f;
    [SerializeField, Min(0.01f)] private float _damage = 10f;
    [SerializeField, Min(0.01f)] private float _duration = 5f;
    [SerializeField, Min(1f)] private float _moveSpeedMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float _attackSpeedMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float _damageMultiplier = 2f;

    public string Id => _id;
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
    public ItemEffectType Effect => _effect;
    public float Radius => _radius;
    public float Damage => _damage;
    public float Duration => _duration;
    public float MoveSpeedMultiplier => _moveSpeedMultiplier;
    public float AttackSpeedMultiplier => _attackSpeedMultiplier;
    public float DamageMultiplier => _damageMultiplier;
    public bool TargetsArea => _effect == ItemEffectType.AreaDamage || _effect == ItemEffectType.AreaFreeze
        || _effect == ItemEffectType.TreeInvulnerability;
    public bool TargetsTeam => _effect == ItemEffectType.TeamHaste || _effect == ItemEffectType.TeamEmpowerment;

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(_id) || string.IsNullOrWhiteSpace(_title) || string.IsNullOrWhiteSpace(_description))
            error = "Assign a non-empty ID, title and description.";
        else if (!Enum.IsDefined(typeof(ItemEffectType), _effect))
            error = "Choose a supported effect.";
        else if (TargetsArea && !IsPositiveFinite(_radius))
            error = "Area radius must be finite and positive.";
        else if (_effect == ItemEffectType.AreaDamage && !IsPositiveFinite(_damage))
            error = "Damage must be finite and positive.";
        else if (_effect != ItemEffectType.AreaDamage && _effect != ItemEffectType.InstantGrowth && !IsPositiveFinite(_duration))
            error = "Timed effects need a finite positive duration.";
        else if (_effect == ItemEffectType.TeamHaste && (!IsMultiplier(_moveSpeedMultiplier) || !IsMultiplier(_attackSpeedMultiplier)))
            error = "Haste multipliers must be finite and greater than one.";
        else if (_effect == ItemEffectType.TeamEmpowerment && !IsMultiplier(_damageMultiplier))
            error = "Damage multiplier must be finite and greater than one.";
        return error == null;
    }

    private static bool IsPositiveFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    private static bool IsMultiplier(float value) => IsPositiveFinite(value) && value > 1f;
}
