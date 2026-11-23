using System;
using System.Collections.Generic;
using UnityEngine;

public class SpiritSelection : MonoBehaviour
{
    private readonly List<SpiritView> _selected = new List<SpiritView>();

    [SerializeField] private SpiritSpawner _spawner;
    [SerializeField, Min(1)] private int _maxSelectableSpirits = 3;

    public IReadOnlyList<SpiritView> Selected => _selected;
    public event Action OnSelectionChanged;

    public void SelectOnly(SpiritView spirit)
    {
        if (spirit == null) return;

        ClearRings();
        _selected.Clear();

        _selected.Add(spirit);
        spirit.SetSelected(true);

        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Mode tunggal: jika Spirit sudah terpilih sendirian -> batalkan seleksi.
    /// Jika Spirit lain sedang terpilih -> batalkan Spirit lain dan pilih Spirit ini.
    /// </summary>
    public void ToggleSingle(SpiritView spirit)
    {
        if (spirit == null) return;

        if (_selected.Count == 1 && _selected[0] == spirit)
        {
            ClearSelection();
            return;
        }

        SelectOnly(spirit);
    }

    /// <summary>
    /// Mode multi-select (Left Shift): menambah/mengurangi Spirit dari seleksi (maksimal 3 Spirit).
    /// </summary>
    public void ToggleMulti(SpiritView spirit)
    {
        if (spirit == null) return;

        int existingIndex = _selected.IndexOf(spirit);
        if (existingIndex >= 0)
        {
            _selected.RemoveAt(existingIndex);
            spirit.SetSelected(false);
            OnSelectionChanged?.Invoke();
            return;
        }

        if (_selected.Count >= _maxSelectableSpirits) return;

        _selected.Add(spirit);
        spirit.SetSelected(true);
        OnSelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;

        ClearRings();
        _selected.Clear();
        OnSelectionChanged?.Invoke();
    }

    private void ClearRings()
    {
        foreach (SpiritView s in _selected)
        {
            if (s != null) s.SetSelected(false);
        }
    }

    public SpiritView SelectByIndex(int index)
    {
        SpiritView spirit = GetSpiritAt(index);
        if (spirit == null) return null;

        SelectOnly(spirit);
        return spirit;
    }

    public SpiritView ToggleSingleByIndex(int index)
    {
        SpiritView spirit = GetSpiritAt(index);
        if (spirit == null) return null;

        ToggleSingle(spirit);
        return _selected.Contains(spirit) ? spirit : null;
    }

    public SpiritView ToggleMultiByIndex(int index)
    {
        SpiritView spirit = GetSpiritAt(index);
        if (spirit == null) return null;

        ToggleMulti(spirit);
        return _selected.Contains(spirit) ? spirit : null;
    }

    public void SelectAll()
    {
        if (_spawner == null) return;

        ClearRings();
        _selected.Clear();

        foreach (SpiritView spirit in _spawner.Spirits)
        {
            if (spirit == null) continue;
            if (_selected.Count >= _maxSelectableSpirits) break;

            _selected.Add(spirit);
            spirit.SetSelected(true);
        }

        OnSelectionChanged?.Invoke();
    }

    public void ToggleSelectAll()
    {
        if (_spawner == null) return;

        int availableCount = 0;
        foreach (SpiritView spirit in _spawner.Spirits)
        {
            if (spirit != null) availableCount++;
            if (availableCount >= _maxSelectableSpirits) break;
        }

        if (availableCount > 0 && _selected.Count == availableCount)
        {
            ClearSelection();
            return;
        }

        SelectAll();
    }

    private SpiritView GetSpiritAt(int index)
    {
        if (_spawner == null || index < 0 || index >= _spawner.Spirits.Count) return null;
        return _spawner.Spirits[index];
    }
}