using System.Collections.Generic;
using UnityEngine;

public class BlockTreeRevealer : MonoBehaviour
{
    private GridBlockData _block;
    private List<TreeNode> _nodes;

    public void Initialize(GridBlockData block, List<TreeNode> nodes)
    {
        _block = block;
        _nodes = nodes;
        _block.OnStateChanged += HandleStateChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_block != null) _block.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GridBlockData _)
    {
        Refresh();
    }


    private void Refresh()
    {
        bool revealed = _block.State == GridState.Clean;
        foreach (var node in _nodes) node.SetRevealed(revealed);
    }
}
