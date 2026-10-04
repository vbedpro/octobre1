using UnityEngine;

// Generates a sphere mesh with GUARANTEED standard equirectangular UV
// mapping — u = (lon+180)/360, v = (lat+90)/180 — using the EXACT same
// formula GlobeDotSpawner uses for dot positions. This replaces Unity's
// built-in primitive sphere, whose own UV generation does NOT reliably
// match standard geographic lat/lon spacing.
//
// Confirmed via calibration testing: both poles align correctly (pure
// geometry, unaffected by any UV distortion), but the equator visibly
// stretches vertically even at Tiling 1,1 with a correctly-proportioned
// 2:1 source image — meaning the MESH's own UV distribution, not the
// texture or the math, was the actual mismatch. Using this custom mesh
// makes texture-vs-dot misalignment mathematically impossible, since both
// now use the identical underlying formula.
[RequireComponent(typeof(MeshFilter))]
public class EquirectangularSphereGenerator : MonoBehaviour
{
    [Tooltip("Number of vertical rings (latitude divisions). Higher = smoother, more expensive.")]
    public int latSegments = 48;
    [Tooltip("Number of segments around each ring (longitude divisions).")]
    public int lonSegments = 96;
    public float radius = 0.5f;

    [ContextMenu("Generate Sphere Mesh")]
    public void GenerateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "EquirectangularSphere";

        int vertCount = (latSegments + 1) * (lonSegments + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];

        int index = 0;
        for (int y = 0; y <= latSegments; y++)
        {
            // v goes 0 (south pole) to 1 (north pole) — matches
            // GlobeDotSpawner's v_content = (lat+90)/180 exactly.
            float v = (float)y / latSegments;
            float lat = v * 180f - 90f;

            for (int x = 0; x <= lonSegments; x++)
            {
                // u goes 0 to 1 across the full 360° — matches
                // u_content = (lon+180)/360 exactly.
                float u = (float)x / lonSegments;
                float lon = u * 360f - 180f;

                Vector3 pos = GlobeDotSpawner.LatLongToLocalPosition(lat, lon, radius);
                vertices[index] = pos;
                normals[index] = pos.normalized;
                uvs[index] = new Vector2(u, v);
                index++;
            }
        }

        int[] triangles = new int[latSegments * lonSegments * 6];
        int triIndex = 0;
        int rowStride = lonSegments + 1;

        for (int y = 0; y < latSegments; y++)
        {
            for (int x = 0; x < lonSegments; x++)
            {
                int i0 = y * rowStride + x;
                int i1 = i0 + 1;
                int i2 = i0 + rowStride;
                int i3 = i2 + 1;

                triangles[triIndex++] = i0;
                triangles[triIndex++] = i2;
                triangles[triIndex++] = i1;

                triangles[triIndex++] = i1;
                triangles[triIndex++] = i2;
                triangles[triIndex++] = i3;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = mesh;

        Debug.Log("EquirectangularSphereGenerator: mesh generated with " + vertCount + " vertices, matching dot-placement formula exactly.");
    }

    void Awake()
    {
        // Auto-generate at runtime too, in case it wasn't generated/saved
        // ahead of time via the context menu.
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh == null || mf.sharedMesh.name != "EquirectangularSphere")
            GenerateMesh();
    }
}
