using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private TreeDefinition[] _definitions;

    [ContextMenu("Test: Tree Data")]
    private void Run()
    {
        foreach (var def in _definitions)
        {
            var tree = new TreeData(def);
            tree.OnStageChanged += () => Debug.Log($"{def.Type} stage -> {tree.Stage}");
            tree.OnElementChanged += () => Debug.Log($"{def.Type} element -> {tree.Element}");

            Debug.Log($"{def.Type}: hp {tree.Hp}, alive {tree.IsAlive}, stage {tree.Stage}, element {tree.Element}, " +
                      $"phase1 {def.GetPhaseDuration(TreeStage.Initial)}s, phase2 {def.GetPhaseDuration(TreeStage.Intermediate)}s");

            tree.SetElement(ElementType.Fire);
            tree.SetStage(TreeStage.Intermediate);
            tree.SetStage(TreeStage.Complete);
            Debug.Log($"{def.Type}: fully grown {tree.IsFullyGrown}");
        }
    }
}
