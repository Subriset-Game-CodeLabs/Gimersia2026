using UnityEngine;

public enum BuffCategory
{
    Spirit,
    Tree,
    Enemy
}

[CreateAssetMenu(fileName = "BuffCategory", menuName = "Game/Buff Category")]
public sealed class BuffCategoryDefinition : ScriptableObject
{
    [SerializeField] private BuffCategory _category;
    [SerializeField] private string _label;

    public BuffCategory Category => _category;
    public string Label => string.IsNullOrWhiteSpace(_label) ? _category.ToString() : _label;
}