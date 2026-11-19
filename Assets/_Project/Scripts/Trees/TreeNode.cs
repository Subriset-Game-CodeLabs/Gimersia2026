using System;
using UnityEngine;

public class TreeNode : MonoBehaviour
{
    [SerializeField] private Transform _visual; // pivot at the base of the tree

    // TODO(balance): placeholder scale per stage (Initial, Intermediate, Complete).
    [SerializeField] private float[] _stageScales = { 0.4f, 0.7f, 1f };

    public TreeData Data { get; private set; }

    public event Action<TreeNode> OnAvailabilityChanged;

    public bool IsRevealed { get; private set; }
    public bool IsTargetable => IsRevealed && Data.IsAlive;
    public Vector3 TargetPoint => transform.position;

    public void Initialize(TreeDefinition definition)
    {
        Data = new TreeData(definition);
        Data.OnStageChanged += Refresh;
        Data.OnDied += HandleDied;
        Refresh();
        SetRevealed(false);
    }

    public void SetRevealed(bool revealed)
    {
        IsRevealed = revealed;
        Refresh();
    }

    private void OnDestroy()
    {
        if (Data != null) Data.OnStageChanged -= Refresh;
    }

    private void Refresh()
    {
        int index = Mathf.Clamp((int)Data.Stage, 0, _stageScales.Length - 1);
        _visual.localScale = Vector3.one * _stageScales[index];

        // A Tumbang starts as a fallen tree. Placeholder: lie on its side.
        bool fallen = Data.Definition.Type == TreeType.Tumbang && Data.Stage == TreeStage.Initial;
        _visual.localRotation = fallen ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

        gameObject.SetActive(IsRevealed && Data.IsAlive);
        OnAvailabilityChanged?.Invoke(this);
    }

    // A dead tree stays hidden even if its block is cleaned again.
    private void HandleDied()
    {
        Refresh();
    }

    

}
