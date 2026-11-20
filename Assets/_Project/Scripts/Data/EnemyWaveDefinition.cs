using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyWave", menuName = "Game/Enemy Wave")]
public class EnemyWaveDefinition : ScriptableObject
{
    // Groups run sequentially. Scene marker IDs are resolved by the level's EnemySpawner.
    [SerializeField] private List<EnemySpawnGroup> _groups = new List<EnemySpawnGroup>();

    public IReadOnlyList<EnemySpawnGroup> Groups => _groups;
}
