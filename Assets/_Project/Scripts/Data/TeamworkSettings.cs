using UnityEngine;

// Gotong Royong: total speed multiplier by number of spirits on one target.
[CreateAssetMenu(fileName = "TeamworkSettings", menuName = "Game/Teamwork Settings")]
public class TeamworkSettings : ScriptableObject
{
    // Index 0 = 1 spirit, 1 = 2 spirits, 2 = 3 spirits. Values from AGENTS.md.
    [SerializeField] private float[] _multipliers = { 1f, 2.25f, 4f };

    public float GetMultiplier(int workers)
    {
        if (_multipliers.Length == 0) return 1f;
        int index = Mathf.Clamp(workers, 1, _multipliers.Length) - 1;
        return _multipliers[index];
    }
}
