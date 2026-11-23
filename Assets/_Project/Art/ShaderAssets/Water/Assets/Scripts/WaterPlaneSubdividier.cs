using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaterPlaneSubdivider : MonoBehaviour
{
    [Range(10, 150)]
    public int resolution = 64;

    void Awake()
    {
        Mesh mesh = new Mesh { name = "SubdividedWaterPlane" };
        int vertsPerLine = resolution + 1;
        Vector3[] vertices = new Vector3[vertsPerLine * vertsPerLine];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[resolution * resolution * 6];

        // Menyamakan ukuran standar Plane Unity (-5 sampai +5)
        float size = 10f;
        float halfSize = size * 0.5f;

        for (int z = 0, i = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++, i++)
            {
                float u = (float)x / resolution;
                float v = (float)z / resolution;
                vertices[i] = new Vector3(u * size - halfSize, 0f, v * size - halfSize);
                uvs[i] = new Vector2(u, v);
            }
        }

        for (int z = 0, ti = 0, vi = 0; z < resolution; z++, vi++)
        {
            for (int x = 0; x < resolution; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + resolution + 1;
                triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + resolution + 1;
                triangles[ti + 5] = vi + resolution + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}