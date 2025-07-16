using UnityEngine;

[ExecuteAlways]
public class DrawFaceNormals : MonoBehaviour
{
    public float normalLength = 0.3f;
    public Color normalColor = Color.cyan;
    public bool showNormals = true;
    public bool showStats = true;

    private void OnDrawGizmos()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Mesh mesh = mf.sharedMesh;

        if (showNormals)
        {
            Gizmos.color = normalColor;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

                Vector3 center = (v0 + v1 + v2) / 3f;
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                Gizmos.DrawRay(center, normal * normalLength);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showStats)
            return;

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Mesh mesh = mf.sharedMesh;
        int vertexCount = mesh.vertexCount;
        int faceCount = mesh.triangles.Length / 3;

        // Draw label above the GameObject
        Vector3 labelPos = transform.position + Vector3.up * 1.5f;
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(labelPos, $"Vertices: {vertexCount}, Faces: {faceCount}");
    }
#endif
}
