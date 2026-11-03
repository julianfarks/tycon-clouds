using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>Procedural meshes with rounded shapes (no cubes with hard edges).</summary>
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static Mesh sphere, cylinder;

        // ------------------------------------------------------------------ primitives

        static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Mesh m = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
            return m;
        }

        /// <summary>Unit sphere (diameter 1).</summary>
        public static Mesh Sphere()
        {
            if (sphere == null) sphere = GetPrimitiveMesh(PrimitiveType.Sphere);
            return sphere;
        }

        /// <summary>Unit cylinder (radius 0.5, height 2). Scale y by height/2.</summary>
        public static Mesh Cylinder()
        {
            if (cylinder == null) cylinder = GetPrimitiveMesh(PrimitiveType.Cylinder);
            return cylinder;
        }

        // ------------------------------------------------------------------ helpers

        static void AddTriangle(List<int> tris, List<Vector3> verts, List<Vector3> norms, int a, int b, int c)
        {
            Vector3 fn = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            Vector3 avg = norms[a] + norms[b] + norms[c];
            if (Vector3.Dot(fn, avg) < 0f)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
        }

        static Mesh Build(string name, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            Mesh m = new Mesh();
            m.name = name;
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ rounded box

        static float[] Coords(float half, float r, int seg)
        {
            float[] c = new float[(seg + 1) * 2];
            for (int k = 0; k <= seg; k++)
            {
                c[k] = -half + r * k / seg;
                c[seg + 1 + k] = (half - r) + r * k / seg;
            }
            return c;
        }

        /// <summary>Box with rounded edges and corners, centered on the origin.</summary>
        public static Mesh RoundedBox(float w, float h, float d, float radius)
        {
            return RoundedBox(w, h, d, radius, 2);
        }

        public static Mesh RoundedBox(float w, float h, float d, float radius, int seg)
        {
            string key = "rb" + Mathf.RoundToInt(w * 100) + "_" + Mathf.RoundToInt(h * 100) + "_" + Mathf.RoundToInt(d * 100)
                + "_" + Mathf.RoundToInt(radius * 100) + "_" + seg;
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Vector3 half = new Vector3(w, h, d) * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.999f);
            if (r < 0.001f) r = 0.001f;
            Vector3 inner = half - new Vector3(r, r, r);

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int a1 = (axis + 1) % 3;
                    int a2 = (axis + 2) % 3;
                    float[] c1 = Coords(half[a1], r, seg);
                    float[] c2 = Coords(half[a2], r, seg);
                    int n1 = c1.Length;
                    int n2 = c2.Length;
                    int start = verts.Count;
                    Vector3 faceNormal = Vector3.zero;
                    faceNormal[axis] = sign;

                    for (int i = 0; i < n1; i++)
                    {
                        for (int j = 0; j < n2; j++)
                        {
                            Vector3 p = Vector3.zero;
                            p[axis] = sign * half[axis];
                            p[a1] = c1[i];
                            p[a2] = c2[j];
                            Vector3 q = new Vector3(
                                Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 dir = p - q;
                            Vector3 n = dir.sqrMagnitude > 1e-10f ? dir.normalized : faceNormal;
                            verts.Add(q + n * r);
                            norms.Add(n);
                            uvs.Add(new Vector2(p[a1] / (2f * half[a1]) + 0.5f, p[a2] / (2f * half[a2]) + 0.5f));
                        }
                    }

                    for (int i = 0; i < n1 - 1; i++)
                    {
                        for (int j = 0; j < n2 - 1; j++)
                        {
                            int i0 = start + i * n2 + j;
                            int i1 = i0 + 1;
                            int i2 = i0 + n2;
                            int i3 = i2 + 1;
                            AddTriangle(tris, verts, norms, i0, i1, i2);
                            AddTriangle(tris, verts, norms, i1, i3, i2);
                        }
                    }
                }
            }

            Mesh m = Build(key, verts, norms, uvs, tris);
            cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ cone

        /// <summary>Cone with the base on y=0 and the apex at y=height.</summary>
        public static Mesh Cone(float radius, float height, int segments)
        {
            string key = "cone" + Mathf.RoundToInt(radius * 100) + "_" + Mathf.RoundToInt(height * 100) + "_" + segments;
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float slope = radius / Mathf.Max(0.001f, height);
            for (int i = 0; i <= segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang);
                float sz = Mathf.Sin(ang);
                Vector3 n = new Vector3(cx, slope, sz).normalized;
                verts.Add(new Vector3(cx * radius, 0f, sz * radius));
                norms.Add(n);
                uvs.Add(new Vector2((float)i / segments, 0f));
                verts.Add(new Vector3(0f, height, 0f));
                norms.Add(n);
                uvs.Add(new Vector2((float)i / segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                AddTriangle(tris, verts, norms, a, b, c);
            }

            int center = verts.Count;
            verts.Add(Vector3.zero);
            norms.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));
            int ringStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(Mathf.Cos(ang) * 0.5f + 0.5f, Mathf.Sin(ang) * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
                AddTriangle(tris, verts, norms, center, ringStart + i, ringStart + i + 1);

            Mesh m = Build(key, verts, norms, uvs, tris);
            cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ torus

        /// <summary>Torus lying on the XZ plane. R = ring radius, r = tube radius.</summary>
        public static Mesh Torus(float ringRadius, float tubeRadius, int segments, int sides)
        {
            string key = "torus" + Mathf.RoundToInt(ringRadius * 100) + "_" + Mathf.RoundToInt(tubeRadius * 100) + "_" + segments + "_" + sides;
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u)) * ringRadius;
                for (int j = 0; j <= sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    verts.Add(center + n * tubeRadius);
                    norms.Add(n);
                    uvs.Add(new Vector2((float)i / segments, (float)j / sides));
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    AddTriangle(tris, verts, norms, a, b, c);
                    AddTriangle(tris, verts, norms, b, d, c);
                }
            }

            Mesh m = Build(key, verts, norms, uvs, tris);
            cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ islands

        /// <summary>Radius of a rounded square (superellipse) outline in direction angle.</summary>
        public static float OutlineRadius(float angle, float extent)
        {
            const float n = 5f;
            float c = Mathf.Abs(Mathf.Cos(angle));
            float s = Mathf.Abs(Mathf.Sin(angle));
            float denom = Mathf.Pow(Mathf.Pow(c, n) + Mathf.Pow(s, n), 1f / n);
            return extent / Mathf.Max(0.0001f, denom);
        }

        static float Smoothstep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static float Hash01(int x, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ seed * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>
        /// Floating island top surface: flat inside, softly rolled-off edge. UVs are world scale so the
        /// painted ground texture keeps a constant size when the island grows.
        /// </summary>
        public static Mesh IslandTop(float extent, int seed)
        {
            int rings = 10;
            int around = 72;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            verts.Add(Vector3.zero);
            norms.Add(Vector3.up);
            uvs.Add(Vector2.zero);
            for (int k = 1; k <= rings; k++)
            {
                float t = (float)k / rings;
                for (int i = 0; i < around; i++)
                {
                    float ang = (float)i / around * Mathf.PI * 2f;
                    float edgeNoise = 1f + (Hash01(i % 24, seed) - 0.5f) * 0.05f * Smoothstep(0.7f, 1f, t);
                    float radius = OutlineRadius(ang, extent) * t * edgeNoise;
                    float roll = Smoothstep(0.82f, 1f, t);
                    float y = -0.35f * roll * roll;
                    Vector3 p = new Vector3(Mathf.Cos(ang) * radius, y, Mathf.Sin(ang) * radius);
                    verts.Add(p);
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(p.x / 6f, p.z / 6f));
                }
            }
            for (int i = 0; i < around; i++)
                AddTriangle(tris, verts, norms, 0, 1 + i, 1 + (i + 1) % around);
            for (int k = 1; k < rings; k++)
            {
                int a0 = 1 + (k - 1) * around;
                int b0 = 1 + k * around;
                for (int i = 0; i < around; i++)
                {
                    int i1 = (i + 1) % around;
                    AddTriangle(tris, verts, norms, a0 + i, b0 + i, a0 + i1);
                    AddTriangle(tris, verts, norms, a0 + i1, b0 + i, b0 + i1);
                }
            }

            // Smooth normals for the rolled-off edge.
            Mesh m = Build("islandTop", verts, norms, uvs, tris);
            m.RecalculateNormals();
            return m;
        }

        /// <summary>The rocky underside that tapers to a point, with irregular layers.</summary>
        public static Mesh IslandUnder(float extent, float depth, int seed)
        {
            int layers = 9;
            int around = 48;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int l = 0; l <= layers; l++)
            {
                float t = (float)l / layers;
                float scale = Mathf.Lerp(1f, 0.04f, Mathf.Pow(t, 0.75f));
                float y = -0.35f - depth * Mathf.Pow(t, 1.25f);
                for (int i = 0; i < around; i++)
                {
                    float ang = (float)i / around * Mathf.PI * 2f;
                    float wobble = 1f + (Hash01(i * 7 + l * 13, seed) - 0.5f) * 0.28f * Mathf.Sin(t * Mathf.PI);
                    float radius = OutlineRadius(ang, extent) * scale * wobble;
                    Vector3 p = new Vector3(Mathf.Cos(ang) * radius, y + (Hash01(i + l * 31, seed + 5) - 0.5f) * 0.35f * (1f - t), Mathf.Sin(ang) * radius);
                    verts.Add(p);
                    norms.Add(new Vector3(p.x, 0.2f, p.z).normalized);
                    uvs.Add(new Vector2((float)i / around * 6f, t * 3f));
                }
            }
            for (int l = 0; l < layers; l++)
            {
                int a0 = l * around;
                int b0 = (l + 1) * around;
                for (int i = 0; i < around; i++)
                {
                    int i1 = (i + 1) % around;
                    AddTriangle(tris, verts, norms, a0 + i, a0 + i1, b0 + i);
                    AddTriangle(tris, verts, norms, a0 + i1, b0 + i1, b0 + i);
                }
            }
            Mesh m = Build("islandUnder", verts, norms, uvs, tris);
            m.RecalculateNormals();
            return m;
        }

        /// <summary>Flat quad lying on XZ (used for water strips, grid cells).</summary>
        public static Mesh Quad(float w, float d)
        {
            string key = "quad" + Mathf.RoundToInt(w * 100) + "_" + Mathf.RoundToInt(d * 100);
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;
            List<Vector3> verts = new List<Vector3>
            {
                new Vector3(-w / 2, 0, -d / 2), new Vector3(-w / 2, 0, d / 2),
                new Vector3(w / 2, 0, d / 2), new Vector3(w / 2, 0, -d / 2)
            };
            List<Vector3> norms = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            List<Vector2> uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            List<int> tris = new List<int>();
            AddTriangle(tris, verts, norms, 0, 1, 2);
            AddTriangle(tris, verts, norms, 0, 2, 3);
            Mesh m = Build(key, verts, norms, uvs, tris);
            cache[key] = m;
            return m;
        }

        /// <summary>Grid overlay of thin lines on XZ covering [min, max+1] tiles.</summary>
        public static Mesh GridLines(int min, int max, float thickness)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            float lo = min;
            float hi = max + 1;
            float ht = thickness * 0.5f;
            for (int i = min; i <= max + 1; i++)
            {
                AddLineQuad(verts, norms, uvs, tris, new Vector3(i - ht, 0f, lo), new Vector3(i + ht, 0f, hi));
                AddLineQuad(verts, norms, uvs, tris, new Vector3(lo, 0f, i - ht), new Vector3(hi, 0f, i + ht));
            }
            return Build("grid", verts, norms, uvs, tris);
        }

        static void AddLineQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b)
        {
            int s = verts.Count;
            verts.Add(new Vector3(a.x, 0, a.z));
            verts.Add(new Vector3(a.x, 0, b.z));
            verts.Add(new Vector3(b.x, 0, b.z));
            verts.Add(new Vector3(b.x, 0, a.z));
            for (int i = 0; i < 4; i++) { norms.Add(Vector3.up); uvs.Add(Vector2.zero); }
            AddTriangle(tris, verts, norms, s, s + 1, s + 2);
            AddTriangle(tris, verts, norms, s, s + 2, s + 3);
        }
    }
}
