using System;
using SiliconSandbox.Authoring;
using System.Collections.Generic;
using UnityEngine;

namespace SiliconSandbox.Presentation
{
    // Pure presentation geometry. Never infers or changes electrical joins.
    public static class WireMeshGeometry
    {
        public const float Radius = 0.0625f;
        public static Vector3 NodePosition(GridCell cell, QuarterPoint point, int channel, int geometryVersion = 1)
        {
            var position = new Vector3(cell.X + point.X * 0.25f,
                cell.Y + point.Y * 0.25f, cell.Z + point.Z * 0.25f);
            if (geometryVersion == 2) return position;
            // 0.14-cell lanes leave 0.015 clear space between 0.125 wires.
            // Offset all transverse axes so vertical crossings separate too.
            var offset = Vector3.one * ((channel - 1.5f) * 0.14f);
            if (point.IsFacePoint)
            {
                if (point.X == 0 || point.X == 4) offset.x = 0f;
                if (point.Y == 0 || point.Y == 4) offset.y = 0f;
                if (point.Z == 0 || point.Z == 4) offset.z = 0f;
            }
            return position + offset;
        }

        public static Vector3 FaceNormal(QuarterPoint point)
        {
            if (point.X == 0) return Vector3.left;
            if (point.X == 4) return Vector3.right;
            if (point.Y == 0) return Vector3.down;
            if (point.Y == 4) return Vector3.up;
            if (point.Z == 0) return Vector3.back;
            return Vector3.forward;
        }

        // A transported 12-sided section, projected onto each miter plane.
        // Adjacent sections share one ring: no overlapping caps or corner gaps.
        public static Mesh Path(IReadOnlyList<Vector3> points,
            Vector3? firstIncoming = null, Vector3? lastOutgoing = null)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var previousDirection = firstIncoming ?? (points[1] - points[0]).normalized;
            var radial = Mathf.Abs(previousDirection.x) > 0.5f
                ? Vector3.forward : Vector3.right;
            for (var i = 0; i < points.Count; i++)
            {
                var incoming = i == 0 ? previousDirection :
                    (points[i] - points[i - 1]).normalized;
                var outgoing = i == points.Count - 1 ? (lastOutgoing ?? incoming) :
                    (points[i + 1] - points[i]).normalized;
                radial = Quaternion.FromToRotation(previousDirection, incoming) * radial;
                var side = Vector3.Cross(incoming, radial).normalized;
                var plane = (incoming + outgoing).normalized;
                var denominator = Vector3.Dot(incoming, plane);
                if (denominator < 0.0001f)
                    throw new ArgumentException("A visual path cannot reverse at one point.");
                for (var k = 0; k < 12; k++)
                {
                    var angle = k * Mathf.PI / 6f;
                    var offset = Radius * (radial * Mathf.Cos(angle) + side * Mathf.Sin(angle));
                    vertices.Add(points[i] + offset - incoming *
                        (Vector3.Dot(offset, plane) / denominator));
                }
                previousDirection = incoming;
            }
            for (var i = 0; i < points.Count - 1; i++)
                for (var k = 0; k < 12; k++)
                {
                    var a = i * 12 + k;
                    var b = i * 12 + (k + 1) % 12;
                    triangles.Add(a); triangles.Add(b); triangles.Add(b + 12);
                    triangles.Add(a); triangles.Add(b + 12); triangles.Add(a + 12);
                }
            for (var k = 1; k < 11; k++)
            {
                triangles.Add(0); triangles.Add(k + 1); triangles.Add(k);
                var end = (points.Count - 1) * 12;
                triangles.Add(end); triangles.Add(end + k); triangles.Add(end + k + 1);
            }
            var mesh = new Mesh { name = "Continuous wire path" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // Imported variants include half-cell arms. At authored nodes the
        // span meshes supply the arms, so retain only the open central hub.
        public static Mesh JunctionCore(Mesh source)
        {
            var sourceVertices = source.vertices;
            var sourceTriangles = source.triangles;
            var triangles = new List<int>();
            for (var i = 0; i < sourceTriangles.Length; i += 3)
            {
                var keep = true;
                for (var k = 0; k < 3; k++)
                {
                    var v = sourceVertices[sourceTriangles[i + k]];
                    if (Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)) > 0.094f)
                        keep = false;
                }
                if (keep) for (var k = 0; k < 3; k++) triangles.Add(sourceTriangles[i + k]);
            }
            var mesh = new Mesh { name = source.name + " core" };
            var map = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var sourceNormals = source.normals;
            for (var i = 0; i < triangles.Count; i++)
            {
                var oldIndex = triangles[i];
                if (!map.TryGetValue(oldIndex, out var newIndex))
                {
                    newIndex = vertices.Count; map.Add(oldIndex, newIndex);
                    vertices.Add(sourceVertices[oldIndex]); normals.Add(sourceNormals[oldIndex]);
                }
                triangles[i] = newIndex;
            }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(normals); mesh.RecalculateBounds();
            return mesh;
        }
    }

    public sealed class OwnedWireMesh : MonoBehaviour
    {
        public Mesh Mesh;
        private void OnDestroy() { if (Mesh != null) Destroy(Mesh); }
    }
}
