using System;

public sealed class ItemBagState : IDisposable
{
    private readonly ItemInventory _inventory;
    private bool _disposed;
    public bool IsOpen { get; private set; }
    public string SelectedId { get; private set; }
    public event Action OnChanged;

    public ItemBagState(ItemInventory inventory)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _inventory.OnQuantityChanged += HandleQuantityChanged;
    }

    public void Open()
    {
        if (_disposed || IsOpen) return;
        IsOpen = true;
        OnChanged?.Invoke();
    }

    public bool TrySelect(string id)
    {
        if (_disposed || !IsOpen || _inventory.GetQuantity(id) <= 0) return false;
        SelectedId = id;
        OnChanged?.Invoke();
        return true;
    }

    public void Close()
    {
        if (!IsOpen && SelectedId == null) return;
        IsOpen = false;
        SelectedId = null;
        OnChanged?.Invoke();
    }

    private void HandleQuantityChanged(string id, int before, int after)
    {
        if (SelectedId == id && after == 0) SelectedId = null;
        OnChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inventory.OnQuantityChanged -= HandleQuantityChanged;
        Close();
        OnChanged = null;
    }
}
