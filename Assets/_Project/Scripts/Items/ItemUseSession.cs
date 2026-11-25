using System;
using UnityEngine;

// Runtime selection and one-use transaction, independent of UI and input devices.
public sealed class ItemUseSession
{
    private readonly ItemInventory _inventory;
    private readonly Func<bool> _canUse;
    private readonly ItemEffectExecutor _effects = new ItemEffectExecutor();
    private int _selectionFrame;
    private bool _committing;
    private int _cancellationVersion;

    public ItemDefinition SelectedItem { get; private set; }
    public bool IsTargeting => SelectedItem != null;
    public bool HasStock => IsTargeting && _inventory.GetQuantity(SelectedItem.Id) > 0;

    public ItemUseSession(ItemInventory inventory, Func<bool> canUse)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _canUse = canUse ?? throw new ArgumentNullException(nameof(canUse));
    }

    public bool TryBegin(ItemDefinition item, int frame)
    {
        if (_committing || !_canUse() || !ItemEffectExecutor.Supports(item) || item.TargetsTeam) return false;
        if (!item.TryValidate(out _) || _inventory.GetQuantity(item.Id) == 0) return false;

        SelectedItem = item;
        _selectionFrame = frame;
        return true;
    }

    public bool HasTargets(Vector3 center) => HasStock && _effects.HasTargets(SelectedItem, center);
    public bool TryUseTeam(ItemDefinition item)
    {
        if (_committing || !_canUse()) return false;
        _committing = true;
        try
        {
            int version = _cancellationVersion;
            return _effects.TryApplyTeam(item, _inventory, () => version == _cancellationVersion && _canUse());
        }
        finally
        {
            _committing = false;
        }
    }

    public bool HasTreeTarget(TreeInteractable tree) => HasStock && _effects.HasTreeTarget(SelectedItem, tree);

    public bool TryUse(Vector3 center, int frame)
    {
        return TryCommit(frame, item => _effects.TryApply(item, _inventory, center, CanCommit));
    }

    public bool TryUseTree(TreeInteractable tree, int frame)
    {
        return TryCommit(frame, item => _effects.TryApplyTree(item, _inventory, tree, CanCommit));
    }

    private bool TryCommit(int frame, Func<ItemDefinition, bool> apply)
    {
        if (_committing || !HasStock || frame <= _selectionFrame || !_canUse()) return false;

        _committing = true;
        try
        {
            if (!apply(SelectedItem)) return false;
            Cancel();
            return true;
        }
        finally
        {
            _committing = false;
        }
    }

    public void Cancel()
    {
        _cancellationVersion++;
        SelectedItem = null;
    }

    private bool CanCommit() => IsTargeting && _canUse();
}
