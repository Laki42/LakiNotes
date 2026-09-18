using System.Collections.Generic;
using UnityEngine;

namespace Laki
{
    // Pure geometry, shared by production and offline geometry/preview tests.
    internal static class LakiGeometry
    {
        internal sealed class Shape
        {
            public string Name;
            public Vector3[] Vertices;
            public Color[] Colors;
            public int[] Triangles;
        }
        private static readonly Color Mint = new Color(.45f, 1f, .65f, 1f);
        private static readonly Color Gold = new Color(1f, .86f, .32f, 1f);
        private static readonly Color Moon = new Color(.80f, .73f, 1f, 1f);
        public static Shape[] Create() => new[] { Clover(), Star(4, .23f, Mint, "Mint sparkle"),
            Star(5, .47f, Gold, "Gold star"), Star(4, .23f, Gold, "Gold sparkle"),
            Rainbow(), Crescent(), Star(4, .23f, Moon, "Moon sparkle") };
        private static Shape Star(int points, float inner, Color color, string name)
        {
            var b = new MeshBuilder();
            for (int i = 0; i < points * 2; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / points;
                float next = a + Mathf.PI / points;
                b.Triangle(Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? 1 : inner),
                    new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * (i % 2 == 0 ? inner : 1), color);
            }
            return b.Finish(name);
        }
        private static Shape Clover()
        {
            var b = new MeshBuilder();
            // Four heart-shaped leaves, 12 triangles each, and a small stem.
            for (int leaf = 0; leaf < 4; leaf++)
            {
                float angle = leaf * Mathf.PI / 2;
                for (int step = 0; step < 12; step++)
                    b.Triangle(Rotate(new Vector2(0, .47f), angle), Heart(step * Mathf.PI * 2 / 12, angle),
                        Heart((step + 1) * Mathf.PI * 2 / 12, angle), Mint);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, next = (i + 1) * Mathf.PI / 4;
                b.Triangle(Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .12f,
                    new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * .12f, Mint);
            }
            b.Quad(new Vector2(-.03f, -.10f), new Vector2(.045f, -.10f),
                new Vector2(.18f, -.96f), new Vector2(.10f, -.96f), Mint);
            return b.Finish("Four-leaf clover");
        }
        private static Vector2 Heart(float t, float rotation)
        {
            float sin = Mathf.Sin(t);
            return Rotate(new Vector2(.025f * 16 * sin * sin * sin,
                .52f + .025f * (13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t))), rotation);
        }
        private static Vector2 Rotate(Vector2 p, float a) => new Vector2(p.x * Mathf.Cos(a) - p.y * Mathf.Sin(a), p.x * Mathf.Sin(a) + p.y * Mathf.Cos(a));
        private static Shape Crescent()
        {
            var b = new MeshBuilder();
            // A crescent strip, not a disk with a black overlaid circle; transparent hole.
            for (int i = 0; i < 20; i++)
            {
                float a = Mathf.PI * i / 20, next = Mathf.PI * (i + 1) / 20;
                b.Quad(new Vector2(-Mathf.Sin(a), Mathf.Cos(a)), new Vector2(-Mathf.Sin(next), Mathf.Cos(next)),
                    new Vector2(-.34f * Mathf.Sin(next) + .12f * Mathf.Sin(next * 2), Mathf.Cos(next)),
                    new Vector2(-.34f * Mathf.Sin(a) + .12f * Mathf.Sin(a * 2), Mathf.Cos(a)), Moon);
            }
            return b.Finish("Crescent moon");
        }
        private static Shape Rainbow()
        {
            var b = new MeshBuilder();
            var colors = new[] { new Color(1,.45f,.59f), new Color(1,.69f,.35f), new Color(1,.9f,.43f),
                new Color(.43f,1,.72f), new Color(.4f,.78f,1), new Color(.79f,.57f,1) };
            for (int band = 0; band < 6; band++)
            {
                float outer = 1f - band * .1f, inner = outer - .075f;
                for (int s = 0; s < 10; s++)
                {
                    float a = s * Mathf.PI / 10, next = (s + 1) * Mathf.PI / 10;
                    b.Quad(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer,
                        new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * outer,
                        new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * inner,
                        new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner, colors[band]);
                }
            }
            return b.Finish("Tiny rainbow");
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(color); colors.Add(color); colors.Add(color);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }
            public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
            { Triangle(a, b, c, color); Triangle(a, c, d, color); }
            public Shape Finish(string name)
            {
                return new Shape { Name = "Laki." + name, Vertices = vertices.ToArray(),
                    Colors = colors.ToArray(), Triangles = triangles.ToArray() };
            }
        }
    }
}

