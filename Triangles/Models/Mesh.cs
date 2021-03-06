﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Triangles.Models
{
    public class Mesh
    {
        public readonly (int a, int b, int c)[] TriangleIdx;
        public readonly Vertex[] Vertices;
        public Matrix4x4 M = Matrix4x4.Identity;
        public List<Mesh> Children = new List<Mesh>();
        public Action<Mesh, float> UpdateAction;
        public readonly IMaterial Material;

        public Mesh((int a, int b, int c)[] trianlgeIdx, Vertex[] vertices, IMaterial material)
        {
            TriangleIdx = trianlgeIdx;
            Vertices = vertices;
            Material = material;
        }

        public Mesh(string objFile, IMaterial material)
        {
            var points = new List<Vector3>();
            var norms = new List<Vector3>();
            var faces = new List<Face>();
            var uvs = new List<Vector2>();

            foreach (var line in File.ReadLines(objFile))
            {
                var components = line.Split(' ');
                if (components[0] == "v")
                {
                    points.Add(ParseVector(components.Skip(1).ToArray()));
                }
                else if (components[0] == "vn")
                {
                    norms.Add(ParseVector(components.Skip(1).ToArray()));
                }
                else if (components[0] == "f")
                {
                    faces.Add(ParseFace(components.Skip(1).ToArray()));
                }
                else if (components[0] == "vt ")
                {
                    uvs.Add(ParseVector2(components.Skip(1).ToArray()));
                }
            }

            if (uvs.Count == 0)
            {
                uvs.AddRange(Enumerable.Repeat(Vector2.Zero, faces.Count));
            }

            var verts = new List<Vertex>();
            var idx = new List<(int a, int b, int c)>();

            var k = -1;
            for (var i = 0; i < faces.Count; i++)
            {
                verts.Add(new Vertex(points[faces[i].VertsId[2] - 1], Vector3.One, norms[faces[i].NormsId[2] - 1], uvs[faces[i].VertsId[2] - 1]));
                verts.Add(new Vertex(points[faces[i].VertsId[1] - 1], Vector3.One, norms[faces[i].NormsId[1] - 1], uvs[faces[i].VertsId[1] - 1]));
                verts.Add(new Vertex(points[faces[i].VertsId[0] - 1], Vector3.One, norms[faces[i].NormsId[0] - 1], uvs[faces[i].VertsId[0] - 1]));

                idx.Add((++k, ++k, ++k));
            }

            Vertices = verts.ToArray();
            TriangleIdx = idx.ToArray();
            Material = material;
        }

        public Vertex[] Transform(Matrix4x4 mParten, Matrix4x4 viewModel)
        {
            var m = M * mParten * viewModel;
            return Vertices.Select(v => v.Transform(m)).ToArray();
        }

        public IEnumerable<Triangle> Triangles(float zPlane, Matrix4x4 m, Matrix4x4 viewModel, float deltaTime, bool doNotUpdate = false)
        {
            if(!doNotUpdate) UpdateAction?.Invoke(this, deltaTime);
            var verts = Transform(m, viewModel);

            foreach (var idx in TriangleIdx)
            {
                foreach (var triangle in ZClipping(idx, zPlane, verts))
                {
                    yield return triangle;
                }
            }

            foreach (var chid in Children)
            {
                foreach (var triangle in chid.Triangles(zPlane, M, viewModel, deltaTime))
                {
                    yield return triangle;
                }
            }
        }

        public int TriangleCount()
        {
            return TriangleIdx.Length + Children.Sum(chid => chid.TriangleCount());
        }

        public IEnumerable<int> FillArray(Matrix4x4 m, Matrix4x4 viewModel, float deltaTime, int start, float[] verts, float[] colors, float[] normals, float[] uvs)
        {
            UpdateAction?.Invoke(this, deltaTime);
            var vs = Transform(m, viewModel);

            var k = start;
            var j = start;
            var l = start;
            for (var i = start; i < TriangleIdx.Length; i++, k += 9, j += 12, l += 6)
            {
                var idx = TriangleIdx[i];

                verts[j + 0] = vs[idx.a].Pos.X;
                verts[j + 1] = vs[idx.a].Pos.Y;
                verts[j + 0] = vs[idx.a].Pos.Z;
                verts[j + 3] = 0;

                verts[j + 4] = vs[idx.b].Pos.X;
                verts[j + 5] = vs[idx.b].Pos.Y;
                verts[j + 6] = vs[idx.b].Pos.Z;
                verts[j + 7] = 0;

                verts[j +  8] = vs[idx.c].Pos.X;
                verts[j +  9] = vs[idx.c].Pos.Y;
                verts[j + 10] = vs[idx.c].Pos.Z;
                verts[j + 11] = 0;

                colors[k + 0] = vs[idx.a].Color.X;
                colors[k + 1] = vs[idx.a].Color.Y;
                colors[k + 2] = vs[idx.a].Color.Z;

                colors[k + 3] = vs[idx.b].Color.X;
                colors[k + 4] = vs[idx.b].Color.Y;
                colors[k + 5] = vs[idx.b].Color.Z;

                colors[k + 6] = vs[idx.c].Color.X;
                colors[k + 7] = vs[idx.c].Color.Y;
                colors[k + 8] = vs[idx.c].Color.Z;

                normals[k + 0] = vs[idx.a].Normal.X;
                normals[k + 1] = vs[idx.a].Normal.Y;
                normals[k + 2] = vs[idx.a].Normal.Z;

                normals[k + 3] = vs[idx.b].Normal.X;
                normals[k + 4] = vs[idx.b].Normal.Y;
                normals[k + 5] = vs[idx.b].Normal.Z;

                normals[k + 6] = vs[idx.c].Normal.X;
                normals[k + 7] = vs[idx.c].Normal.Y;
                normals[k + 8] = vs[idx.c].Normal.Z;

                uvs[l + 0] = vs[idx.a].Uv.X;
                uvs[l + 1] = vs[idx.a].Uv.Y;

                uvs[l + 2] = vs[idx.b].Uv.X;
                uvs[l + 3] = vs[idx.b].Uv.Y;

                uvs[l + 4] = vs[idx.c].Uv.X;
                uvs[l + 5] = vs[idx.c].Uv.Y;

                yield return start + i;
            }

            int index = start + TriangleIdx.Length;
            foreach (var chid in Children)
            {
                foreach (var i in chid.FillArray(m, viewModel, deltaTime, index, verts, colors, normals, uvs))
                {
                    index = i;
                    yield return i;
                }
            }
        }

        private IEnumerable<Triangle> ZClipping((int a, int b, int c) idx, float zPlane, Vertex[] verts)
        {
            var points = new[] { verts[idx.a], verts[idx.b], verts[idx.c] };
            var newVerts = new List<Vertex>();

            for (var i = 0; i < points.Length; i++)
            {
                var ib = (i + 1) % points.Length;

                var a = points[i];
                var b = points[ib];

                if (a.Pos.Z > zPlane)
                {
                    newVerts.Add(a);
                    if (b.Pos.Z > zPlane) continue;
                }

                if (IsIntersectingZ(zPlane, a.Pos, b.Pos, out var p, out var t))
                    newVerts.Add(
                        new Vertex(p, Vector3.Lerp(a.Color, b.Color, t), Vector3.Lerp(a.Normal, b.Normal, t), Vector2.Lerp(a.Uv, b.Uv, t)));
            }

            for (var i = 1; i < newVerts.Count - 1; i++)
            {
                yield return new Triangle(
                    newVerts[0].Pos, newVerts[i].Pos, newVerts[i + 1].Pos,
                    new[] { newVerts[0].Color, newVerts[i].Color, newVerts[i + 1].Color },
                    new[] { newVerts[0].Normal, newVerts[i].Normal, newVerts[i + 1].Normal },
                    new[] { newVerts[0].Uv, newVerts[i].Uv, newVerts[i + 1].Uv },
                    Material
                );
            }
        }

        private static bool IsIntersectingZ(float zPlane, Vector3 a, Vector3 b, out Vector3 p, out float t)
        {
            p = Vector3.Zero;
            t = (a.Z - zPlane) / (a.Z - b.Z);
            if (t > 1 || t < 0) return false;
            p = Vector3.Lerp(a, b, (a.Z - zPlane) / (a.Z - b.Z));
            return p.Z <= zPlane;
        }

        private Vector3 ParseVector(string[] comps)
        {
            return new Vector3(float.Parse(comps[0]), float.Parse(comps[1]), float.Parse(comps[2]));
        }

        private Vector2 ParseVector2(string[] comps)
        {
            return new Vector2(float.Parse(comps[0]), float.Parse(comps[1]));
        }

        private Face ParseFace(string[] comps)
        {
            var vIds = new int[3];
            var nIds = new int[3];

            for (var i = 0; i < comps.Length; i++)
            {
                var comp = comps[i];
                var c = comp.Split(new[] { "//" }, StringSplitOptions.None);
                vIds[i] = int.Parse(c[0]);
                nIds[i] = int.Parse(c[1]);
            }

            return new Face(vIds, nIds);
        }
    }
}