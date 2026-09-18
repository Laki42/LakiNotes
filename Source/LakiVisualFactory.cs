using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Laki
{
    internal sealed class LakiVisualFactory : IDisposable
    {
        private Material material;
        private Mesh[] meshes;
        private bool failed;

        public bool EnsureReady()
        {
            if (material != null) return true;
            if (failed) return false;
            try
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null || !shader.isSupported)
                {
                    failed = true;
                    Plugin.Log.Warn("Laki Notes visuals unavailable: Sprites/Default shader is missing or unsupported.");
                    return false;
                }
                // One material for every motif and every session. Never touch a game's material.
                material = new Material(shader) { name = "Laki.SharedSpriteMaterial" };
                material.SetColor("_Color", Color.white);
                var shapes = LakiGeometry.Create();
                meshes = new Mesh[shapes.Length];
                for (int i = 0; i < shapes.Length; i++)
                {
                    var shape = shapes[i];
                    var mesh = new Mesh { name = shape.Name };
                    meshes[i] = mesh;
                    mesh.vertices = shape.Vertices;
                    mesh.colors = shape.Colors;
                    mesh.triangles = shape.Triangles;
                    mesh.uv = new Vector2[shape.Vertices.Length];
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(true);
                }
                return true;
            }
            catch (Exception e)
            {
                Dispose(); failed = true;
                Plugin.Log.Warn("Laki Notes visual assets unavailable: " + e.Message);
                return false;
            }
        }

        public LakiVisualController Create(LakiKind kind, IGamePause pause)
        {
            var root = new GameObject("Laki.Visual");
            root.SetActive(false);
            try
            {
                var parts = new Transform[5];
                var renderers = new MeshRenderer[5];
                int[] shape = kind == LakiKind.Laki ? new[] { 0, 1, 1, 0, 1 } :
                    kind == LakiKind.Super ? new[] { 2, 3, 4, 2, 3 } : new[] { 5, 6, 6, 6, 6 };
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = new GameObject("Laki.Motif");
                    parts[i] = part.transform;
                    parts[i].SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = meshes[shape[i]];
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderers[i] = renderer;
                }
                var controller = root.AddComponent<LakiVisualController>();
                controller.Setup(kind, parts, renderers, pause);
                return controller;
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }

        public void Dispose()
        {
            if (meshes != null)
                foreach (var mesh in meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
            meshes = null;
            material = null;
        }

    }
}

