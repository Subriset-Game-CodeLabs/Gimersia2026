using System;
using System.Collections.Generic;
using UnityEngine;

public class SpiritSelection : MonoBehaviour
{
    private readonly List<SpiritView> _selected = new List<SpiritView>();

    public IReadOnlyList<SpiritView> Selected => _selected;

    // Step 3 (movement) and step 5 (keys) will subscribe or call in here.
    public event Action OnSelectionChanged;

    // Needed to look spirits up by number. The spawner fills its list in Start.
    [SerializeField] private SpiritSpawner _spawner;


    public void SelectOnly(SpiritView spirit)
    {
        ClearRings();
        _selected.Clear();

        _selected.Add(spirit);
        spirit.SetSelected(true);

        OnSelectionChanged?.Invoke();
    }

    private void ClearRings()
    {
        foreach (SpiritView s in _selected) s.SetSelected(false);
    }

    public SpiritView SelectByIndex(int index)
    {
        if (index < 0 || index >= _spawner.Spirits.Count) return null;

        SpiritView spirit = _spawner.Spirits[index];
        SelectOnly(spirit);
        return spirit;
    }

    public void SelectAll()
    {
        ClearRings();
        _selected.Clear();

        foreach (SpiritView spirit in _spawner.Spirits)
        {
            _selected.Add(spirit);
            spirit.SetSelected(true);
        }

        OnSelectionChanged?.Invoke();
    }
}
