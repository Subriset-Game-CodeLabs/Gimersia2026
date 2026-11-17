using UnityEngine;

[CreateAssetMenu(fileName = "TreeDefinition", menuName = "Game/TreeDefinition")]
public class TreeDefinition : ScriptableObject
{
    [SerializeField] private TreeType _type;

    // TODO(balance): placeholder values, none of these are final.
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _phase1Duration = 4f; // Initial -> Intermediate
    [SerializeField] private float _phase2Duration = 4f; // Intermediate -> Complete
    [SerializeField] private int _elementPower = 10;     // power given once Complete

    public TreeType Type => _type;
    public float MaxHp => _maxHp;
    public int ElementPower => _elementPower;

    // Work needed to leave the given stage. Complete has nothing left to grow.
    public float GetPhaseDuration(TreeStage from)
    {
        switch (from)
        {
            case TreeStage.Initial: return _phase1Duration;
            case TreeStage.Intermediate: return _phase2Duration;
            default: return 0f;
        }
    }
}
