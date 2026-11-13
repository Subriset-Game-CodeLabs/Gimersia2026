using System.Collections.Generic;
using UnityEngine;

public class SpiritSpawner : MonoBehaviour
{
   // One prefab per spirit, so each can have its own model later.
    [SerializeField] private SpiritView[] _spiritPrefabs = new SpiritView[3];

    // TODO(balance): placeholder start positions, relative to this object. The final spots depend on the island layout.
    [SerializeField] private Vector3[] _spawnOffsets =
    {
        new Vector3(-2f, 0f, 0f),
        new Vector3(0f, 0f, 0f),
        new Vector3(2f, 0f, 0f)
    };

    private readonly List<SpiritView> _spirits = new List<SpiritView>();

    public IReadOnlyList<SpiritView> Spirits => _spirits;

    private void Start()
    {
        for (int i = 0; i < _spiritPrefabs.Length; i++)
        {
            if (_spiritPrefabs[i] == null)
            {
                Debug.LogError($"SpiritSpawner: prefab {i} is not assigned.", this);
                continue;
            }

            Vector3 offset = _spawnOffsets[i % _spawnOffsets.Length];
            SpiritView view = Instantiate(_spiritPrefabs[i], transform.position + offset, Quaternion.identity, transform);
            view.name = $"Spirit {i + 1}";
            view.Initialize(i);
            _spirits.Add(view);
        }
    }

    // --- TEMPORARY TEST: remove before finishing the task ---
    [ContextMenu("Test: Rings On")]
    private void TestRingsOn()
    {
        foreach (SpiritView s in _spirits) s.SetSelected(true);
    }

    [ContextMenu("Test: Rings Off")]
    private void TestRingsOff()
    {
        foreach (SpiritView s in _spirits) s.SetSelected(false);
    }
}
