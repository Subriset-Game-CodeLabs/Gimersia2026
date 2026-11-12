using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IslandDefinition", menuName = "Game/Island Definition")]
public class IslandDefinition : ScriptableObject
{
    [SerializeField] private float _blockSize = 8f;
    [SerializeField] private List<GridBlockDefinition> _blocks = new List<GridBlockDefinition>();

    public float BlockSize => _blockSize;
    public IReadOnlyList<GridBlockDefinition> Blocks => _blocks;

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * _blockSize, 0f, cell.y * _blockSize);
    }

    [ContextMenu("Fill Placeholder Layout")]
    private void FillPlaceholderLayout()
    {
        Vector2[] SlotsPlaceholder() => new[]
        {
            new Vector2(-2f, -2f), new Vector2(2f, -2f),
            new Vector2(-2f, 2f),  new Vector2(2f, 2f)
        };

        _blocks = new List<GridBlockDefinition>
        {
            new GridBlockDefinition("Center", new Vector2Int(0, 0),  SlotsPlaceholder()),
            new GridBlockDefinition("North",  new Vector2Int(0, 1),  SlotsPlaceholder()),
            new GridBlockDefinition("East",   new Vector2Int(1, 0),  SlotsPlaceholder()),
            new GridBlockDefinition("South",  new Vector2Int(0, -1), SlotsPlaceholder()),
            new GridBlockDefinition("West",   new Vector2Int(-1, 0), SlotsPlaceholder())
        };
    }
}
