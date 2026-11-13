using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IslandDefinition", menuName = "Game/Island Definition")]
public class IslandDefinition : ScriptableObject
{
    [SerializeField] private List<GridBlockDefinition> _blocks = new List<GridBlockDefinition>();

    public IReadOnlyList<GridBlockDefinition> Blocks => _blocks;

    
}
