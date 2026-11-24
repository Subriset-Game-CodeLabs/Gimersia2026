using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Buff", menuName = "Game/Buff Definition")]
public sealed class BuffDefinition : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _title;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;
    [SerializeField] private BuffType _type;
    [Tooltip("Damage: flat points (use 1). Speed: fractional bonus, e.g. 0.1 = 10%.")]
    [SerializeField] private float _amount = 1f; // TODO(balance): author percentage amounts per speed asset.

    public string Id => _id;
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
    public BuffType Type => _type;
    public float Amount => _amount;

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(_id) || string.IsNullOrWhiteSpace(_title) || string.IsNullOrWhiteSpace(_description))
            error = "Assign a non-empty ID, title and description.";
        else if (!Enum.IsDefined(typeof(BuffType), _type) || float.IsNaN(_amount) || float.IsInfinity(_amount) || _amount <= 0f)
            error = "Choose a supported type and a finite positive amount (speed amounts are fractions).";
        return error == null;
    }
}
