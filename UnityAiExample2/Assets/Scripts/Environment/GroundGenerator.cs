using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class GroundGenerator : MonoBehaviour
{
    [SerializeField] private int width = 50;
    [SerializeField] private int depth = 50;
    [SerializeField] private float scale = 5f;
    [SerializeField] private float heightMultiplier = 2f;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        GenerateGround();
    }

    [ContextMenu("Generate Ground")]
    public void GenerateGround()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        Mesh mesh = new Mesh();
mesh.name = "BumpyGround";

        Vector3[] vertices = new Vector3[(width + 1) * (depth + 1)];
        int[] triangles = new int[width * depth * 6];

        for (int z = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                float y = Mathf.PerlinNoise(x * 0.1f * scale, z * 0.1f * scale) * heightMultiplier;
                vertices[z * (width + 1) + x] = new Vector3(x, y, z);
            }
        }

        int tri = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int current = z * (width + 1) + x;
                int next = current + width + 1;

                triangles[tri++] = current;
                triangles[tri++] = next;
                triangles[tri++] = current + 1;

                triangles[tri++] = next;
                triangles[tri++] = next + 1;
                triangles[tri++] = current + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
}
