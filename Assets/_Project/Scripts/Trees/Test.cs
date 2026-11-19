using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private TreeDefinition[] _definitions;
    [SerializeField] private ElementSettings _elementSettings;

    [ContextMenu("Test: Tree Data")]
    private void Run()
    {
        if (_elementSettings == null)
        {
            Debug.LogError("Assign ElementSettings to the tree data test.", this);
            return;
        }

        foreach (TreeDefinition definition in _definitions)
        {
            TreeData tree = new TreeData(definition);
            tree.OnStageChanged += () =>
                Debug.Log($"{definition.Type} stage -> {tree.Stage}");
            tree.OnElementChanged += () =>
                Debug.Log($"{definition.Type} element -> {tree.Element}");

            tree.TryAssignElement(ElementType.Fire, _elementSettings);
            tree.SetStage(TreeStage.Intermediate);
            tree.SetStage(TreeStage.Complete);

            Debug.Log(
                $"{definition.Type}: hp {tree.Hp}, alive {tree.IsAlive}, " +
                $"stage {tree.Stage}, element {tree.Element}");
        }
    }
}
