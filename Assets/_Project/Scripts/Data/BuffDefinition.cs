using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Buff", menuName = "Game/Buff Definition")]
public sealed class BuffDefinition : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _title;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;
    [Tooltip("Optional static card background, separate from the icon. Unassigned keeps the card's original sprite.")]
    [SerializeField] private Sprite _cardTemplate;
    [Tooltip("Optional selected version of the card background, not pointer-hover artwork. Unassigned uses the normal card background.")]
    [SerializeField] private Sprite _cardHoverTemplate;
    [SerializeField] private BuffType _type;
    [Tooltip("Damage: flat points (use 1). Speed: fractional bonus, e.g. 0.1 = 10%.")]
    [SerializeField] private float _amount = 1f; // TODO(balance): author percentage amounts per speed asset.

    public string Id => _id;
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
    public Sprite CardTemplate => _cardTemplate;
    public Sprite CardHoverTemplate => _cardHoverTemplate;
    [SerializeField] private BuffCategoryDefinition _category;
    [Tooltip("Use {amount} for the first effect. Include the percent sign in the text for percentage effects.")]
    [SerializeField] private string _effectStripTemplate;
    [Tooltip("When empty, use the original Type and Amount above. Otherwise the first entry drives the effect strip.")]
    [SerializeField] private BuffEffect[] _effects = Array.Empty<BuffEffect>();
    [Tooltip("Zero means unlimited. TODO(balance): set per card.")]
    [SerializeField, Min(0)] private int _maxStacks;

    public int MaxStacks => _maxStacks;
    [Tooltip("Optional fraction healed once on selection, after persistent bonuses apply. Zero disables it.")]
    [SerializeField, Min(0)] private float _selectionHealFraction; // TODO(balance): author on Deep Roots.
    [Tooltip("Heal a fraction of missing HP instead of resulting maximum HP.")]
    [SerializeField] private bool _healMissingHp;
    public float SelectionHealFraction => _selectionHealFraction;
    public bool HealMissingHp => _healMissingHp;
    public int EffectCount => _effects == null || _effects.Length == 0 ? 1 : _effects.Length;
    public BuffEffect EffectAt(int index) => _effects == null || _effects.Length == 0
        ? index == 0 ? new BuffEffect(_type, _amount) : throw new ArgumentOutOfRangeException(nameof(index))
        : _effects[index];
    public BuffType Type => EffectAt(0).Type;
    public float Amount => EffectAt(0).Amount;
    public string CategoryLabel => _category != null ? _category.Label : string.Empty;
    public string PrimaryDisplayAmount => (Type == BuffType.SpiritDamage ? Amount : Amount * 100d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    public string EffectStrip => (_effectStripTemplate ?? string.Empty).Replace("{amount}", PrimaryDisplayAmount);

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(_id) || string.IsNullOrWhiteSpace(_title) || string.IsNullOrWhiteSpace(_description))
            error = "Assign a non-empty ID, title and description.";
        else if (_maxStacks < 0) error = "Max stacks cannot be negative.";
        else if (!(_selectionHealFraction >= 0f && _selectionHealFraction <= 1f)) error = "Selection heal fraction must be between zero and one.";
        else for (int i = 0; i < EffectCount; i++)
        {
            if (!EffectAt(i).IsValid) { error = $"Invalid effect at index {i}."; break; }
            for (int j = 0; j < i; j++)
                if (EffectAt(j).Type == EffectAt(i).Type) { error = "Combine duplicate stats into one entry."; break; }
            if (error != null) break;
        }
        return error == null;
    }
}
