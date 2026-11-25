using System;
using System.Collections.Generic;

// Plain session service. Purchases/rewards commit durable state before notifying UI.
public sealed class ShopProgress
{
    private readonly IShopSaveStore _store;
    private bool _busy;
    private long _revision;
    private long _savedRevision;
    private SelectedStage _runStage;
    private bool _runFinished = true;
    private bool? _pendingOutcome;
    public ItemInventory Inventory { get; } = new ItemInventory();
    public int Coins { get; private set; }
    public bool IsDirty => _revision != _savedRevision;
    public event Action OnChanged;
    public event Action<string> OnSaveError;

    private ShopProgress(IShopSaveStore store, ShopSaveData data)
    {
        _store = store;
        Coins = data.coins;
        foreach (ShopSaveData.ItemCount item in data.items) Inventory.TryAdd(item.id, item.quantity);
        Inventory.OnQuantityChanged += HandleQuantity;
    }

    public static bool TryOpen(IShopSaveStore store, int initialCoins, out ShopProgress progress, out string error)
    {
        progress = null;
        if (store == null || initialCoins < 0) { error = "Invalid save store or starting coins."; return false; }
        if (!store.TryLoad(out ShopSaveData data, out bool exists, out error)) return false;
        string recoveryNotice = error;
        if (!exists)
        {
            data = new ShopSaveData { coins = initialCoins };
            if (!store.TrySave(data, out error)) return false;
        }
        if (data == null || !data.IsValid()) { error = "Invalid saved shop state."; return false; }
        progress = new ShopProgress(store, data);
        error = recoveryNotice;
        return true;
    }

    public bool CanBuy(string id, int price) => !string.IsNullOrWhiteSpace(id)
        && price > 0 && Coins >= price && Inventory.GetQuantity(id) < int.MaxValue;

    public bool TryBuy(string id, int price, out string error)
    {
        error = null;
        if (_busy || !CanBuy(id, price)) { error = "Purchase unavailable: insufficient coins, full stack, invalid item/price, or another transaction is active."; return false; }
        _busy = true;
        try
        {
            ShopSaveData proposed = Capture(Coins - price);
            bool found = false;
            foreach (ShopSaveData.ItemCount item in proposed.items)
                if (item.id == id) { item.quantity++; found = true; break; }
            if (!found) proposed.items.Add(new ShopSaveData.ItemCount { id = id, quantity = 1 });
            if (!_store.TrySave(proposed, out error)) { OnSaveError?.Invoke(error); return false; }
            Coins -= price;
            // Quantity observers see the new wallet too. Keep the guard through all callbacks.
            // This one increment is already durable; later callback mutations stay dirty.
            _savedRevision = _revision + 1;
            Inventory.TryAdd(id, 1);
            return true;
        }
        finally { _busy = false; }
    }

    public void BeginRun(SelectedStage stage)
    {
        _runStage = stage;
        _runFinished = stage == null;
        _pendingOutcome = null;
    }

    public bool TryFinishRun(bool won, out string error)
    {
        error = null;
        if (_busy || _runFinished || _runStage == null) return false;
        if (!_pendingOutcome.HasValue) _pendingOutcome = won;
        won = _pendingOutcome.Value;
        _busy = true;
        try
        {
            int reward = won ? _runStage.WinCoins : 0;
            if (reward < 0 || Coins > int.MaxValue - reward)
            { error = "Victory coins exceed wallet capacity; reward not committed."; OnSaveError?.Invoke(error); return false; }
            if (!_store.TrySave(Capture(Coins + reward), out error))
            { OnSaveError?.Invoke(error); return false; }
            Coins += reward;
            _savedRevision = _revision;
            _runFinished = true;
            _pendingOutcome = null;
            OnChanged?.Invoke();
            return true;
        }
        finally { _busy = false; }
    }

    public bool TryFlush(out string error)
    {
        error = null;
        if (_busy) { error = "A shop transaction is active."; return false; }
        if (_pendingOutcome.HasValue && !TryFinishRun(_pendingOutcome.Value, out error)) return false;
        if (!IsDirty) return true;
        _busy = true;
        try
        {
            long revision = _revision;
            if (!_store.TrySave(Capture(Coins), out error)) { OnSaveError?.Invoke(error); return false; }
            _savedRevision = revision;
            return true;
        }
        finally { _busy = false; }
    }

    private void HandleQuantity(string id, int before, int after)
    {
        _revision++;
        OnChanged?.Invoke();
    }

    private ShopSaveData Capture(int coins)
    {
        var data = new ShopSaveData { coins = coins };
        foreach (KeyValuePair<string, int> item in Inventory.CaptureQuantities())
            data.items.Add(new ShopSaveData.ItemCount { id = item.Key, quantity = item.Value });
        data.items.Sort((a, b) => StringComparer.Ordinal.Compare(a.id, b.id));
        return data;
    }
}
