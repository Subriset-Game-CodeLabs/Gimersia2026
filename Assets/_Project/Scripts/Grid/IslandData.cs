using System.Collections.Generic;

public class IslandData
{
    private readonly List<GridBlockData> _blocks = new List<GridBlockData>();

    public IReadOnlyList<GridBlockData> Blocks => _blocks;

    public IslandData(IslandDefinition definition)
    {
        foreach (var blockDef in definition.Blocks)
            _blocks.Add(new GridBlockData(blockDef));
    }

    public GridBlockData GetBlock(string id)
    {
        for (int i = 0; i < _blocks.Count; i++)
            if (_blocks[i].Id == id) return _blocks[i];
        return null;
    }
}