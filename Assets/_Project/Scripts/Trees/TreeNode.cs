using UnityEngine;

public class TreeNode : MonoBehaviour
{
    [SerializeField] private Transform _visual; // pivot at the base of the tree

    // TODO(balance): placeholder scale per stage (Initial, Intermediate, Complete).
    [SerializeField] private float[] _stageScales = { 0.4f, 0.7f, 1f };

    public TreeData Data { get; private set; }

    public void Initialize(TreeDefinition definition)
    {
        Data = new TreeData(definition);
        Data.OnStageChanged += Refresh;
        Refresh();
        SetRevealed(false);
    }

    public void SetRevealed(bool revealed)
    {
        gameObject.SetActive(revealed);
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
    }

}
