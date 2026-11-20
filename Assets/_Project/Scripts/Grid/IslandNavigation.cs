using Unity.AI.Navigation;
using UnityEngine;

// IslandSpawner creates terrain in Awake; SpiritSpawner creates agents in Start.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(NavMeshSurface))]
[DisallowMultipleComponent]
public class IslandNavigation : MonoBehaviour
{
    private NavMeshSurface _surface;

    private void Awake()
    {
        _surface = GetComponent<NavMeshSurface>();
    }

    private void Start()
    {
        // Editor-only baking cannot include the blocks instantiated at runtime.
        _surface.BuildNavMesh();
        if (_surface.navMeshData == null)
            Debug.LogError("IslandNavigation could not build a NavMesh. Check the surface's terrain layer and collection settings.", this);
    }
}
