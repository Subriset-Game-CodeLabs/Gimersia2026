using System.Collections.Generic;
using UnityEngine;

public class SpiritSpawner : MonoBehaviour
{
    [Tooltip("Isi dengan 3 prefab Spirit yang berbeda (misal: Api, Air, Petir).")]
    [SerializeField] private SpiritView[] _spiritPrefabs = new SpiritView[3];

    [SerializeField] private Vector3[] _spawnOffsets =
    {
        new Vector3(-2f, 0f, 0f),
        new Vector3(0f, 0f, 0f),
        new Vector3(2f, 0f, 0f)
    };

    private readonly List<SpiritView> _spirits = new List<SpiritView>();

    public IReadOnlyList<SpiritView> Spirits => _spirits;

    private void Awake()
    {
        SpawnSpirits();
    }

    private void SpawnSpirits()
    {
        if (_spirits.Count > 0) return;

        for (int i = 0; i < _spiritPrefabs.Length; i++)
        {
            if (_spiritPrefabs[i] == null)
            {
                Debug.LogError($"SpiritSpawner: prefab pada index {i} belum di-assign.", this);
                continue;
            }

            Vector3 offset = _spawnOffsets[i % _spawnOffsets.Length];
            SpiritView view = Instantiate(_spiritPrefabs[i], transform.position + offset, Quaternion.identity, transform);
            view.name = $"Spirit {i + 1} ({view.Element})";
            view.Initialize(i);
            _spirits.Add(view);
        }
    }
}