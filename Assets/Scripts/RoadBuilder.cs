using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds a flat road mesh along a spline and can flatten a Terrain underneath it.
/// Usage: add to the GameObject that has the SplineContainer, assign a material and the Terrain,
/// then use the component's ⋮ menu: "Flatten Terrain Under Road" and "Build Road".
/// </summary>
[RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadBuilder : MonoBehaviour
{
    [Header("Road shape")]
    [Tooltip("Road width in meters.")]
    public float width = 8f;
    [Tooltip("Distance between road cross-sections in meters. Smaller = smoother curves.")]
    public float resolution = 0.5f;
    [Tooltip("How many meters of road one texture repeat covers.")]
    public float metersPerTextureTile = 8f;
    [Tooltip("Lifts the road slightly above the terrain to avoid flickering.")]
    public float heightOffset = 0.05f;

    [Header("Terrain")]
    public Terrain terrain;
    [Tooltip("Extra width on each side where the terrain blends smoothly back to its original height.")]
    public float blendMargin = 4f;

    SplineContainer Container => GetComponent<SplineContainer>();

    // ---------------------------------------------------------------- road mesh

    [ContextMenu("Build Road")]
    public void BuildRoad()
    {
        var samples = SampleSpline();
        if (samples.Count < 2) { Debug.LogWarning("Spline is too short."); return; }

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float distance = 0f;

        for (int i = 0; i < samples.Count; i++)
        {
            var (pos, right) = samples[i];
            if (i > 0) distance += Vector3.Distance(samples[i - 1].pos, pos);

            Vector3 lift = Vector3.up * heightOffset;
            Vector3 left = pos - right * (width * 0.5f) + lift;
            Vector3 rightEdge = pos + right * (width * 0.5f) + lift;

            verts.Add(transform.InverseTransformPoint(left));
            verts.Add(transform.InverseTransformPoint(rightEdge));

            float v = distance / metersPerTextureTile;
            uvs.Add(new Vector2(0, v));
            uvs.Add(new Vector2(1, v));

            if (i < samples.Count - 1)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        var mesh = new Mesh { name = "Road Mesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        var col = GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;

        Debug.Log($"Road built: {distance:F0} m long, {verts.Count} vertices.");
    }

    // ---------------------------------------------------------- terrain flatten

    [ContextMenu("Flatten Terrain Under Road")]
    public void FlattenTerrain()
    {
        if (terrain == null) { Debug.LogWarning("Assign a Terrain first."); return; }

        TerrainData td = terrain.terrainData;
#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(td, "Flatten Terrain Under Road");
#endif
        int res = td.heightmapResolution;
        Vector3 size = td.size;
        Vector3 tPos = terrain.transform.position;

        float[,] heights = td.GetHeights(0, 0, res, res);
        var bestDist = new float[res, res];
        var targetH = new float[res, res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                bestDist[z, x] = float.MaxValue;

        float halfW = width * 0.5f;
        float reach = halfW + blendMargin;

        // For every point along the road, find nearby heightmap pixels
        foreach (var (pos, _) in SampleSpline())
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((pos.x - reach - tPos.x) / size.x * (res - 1)), 0, res - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((pos.x + reach - tPos.x) / size.x * (res - 1)), 0, res - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((pos.z - reach - tPos.z) / size.z * (res - 1)), 0, res - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((pos.z + reach - tPos.z) / size.z * (res - 1)), 0, res - 1);

            float h = Mathf.Clamp01((pos.y - tPos.y) / size.y);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float wx = tPos.x + (float)x / (res - 1) * size.x;
                    float wz = tPos.z + (float)z / (res - 1) * size.z;
                    float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(pos.x, pos.z));
                    if (d < bestDist[z, x]) { bestDist[z, x] = d; targetH[z, x] = h; }
                }
            }
        }

        // Apply: flat under the road, smooth blend at the edges
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float d = bestDist[z, x];
                if (d > reach) continue;
                float t = d <= halfW ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - halfW) / blendMargin);
                heights[z, x] = Mathf.Lerp(heights[z, x], targetH[z, x], t);
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log("Terrain flattened under road.");
    }

    // ------------------------------------------------------------- helpers

    [ContextMenu("Snap Spline Knots To Terrain")]
    public void SnapKnotsToTerrain()
    {
        if (terrain == null) { Debug.LogWarning("Assign a Terrain first."); return; }
#if UNITY_EDITOR
        Undo.RecordObject(Container, "Snap Knots To Terrain");
#endif
        var spline = Container.Spline;
        for (int i = 0; i < spline.Count; i++)
        {
            var knot = spline[i];
            Vector3 world = transform.TransformPoint((Vector3)knot.Position);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            knot.Position = (float3)transform.InverseTransformPoint(world);
            spline[i] = knot;
        }
        Debug.Log("Knots snapped to terrain.");
    }

    List<(Vector3 pos, Vector3 right)> SampleSpline()
    {
        var list = new List<(Vector3, Vector3)>();
        var c = Container;
        if (c == null || c.Spline == null || c.Spline.Count < 2) return list;

        float length = c.CalculateLength();
        int count = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.05f, resolution)) + 1);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            c.Evaluate(t, out float3 p, out float3 tangent, out float3 _);
            Vector3 fwd = ((Vector3)tangent).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized; // keeps the road level side-to-side
            list.Add(((Vector3)p, right));
        }
        return list;
    }
}