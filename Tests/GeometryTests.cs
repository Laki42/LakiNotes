using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Laki;

internal static class GeometryTests
{
    public static void Run(Action<bool, string> check, string previewPath)
    {
        var shapes = LakiGeometry.Create();
        check(shapes.Length == 7, "Seven shared meshes");
        int triangles = 0;
        foreach (var shape in shapes)
        {
            check(shape.Vertices.Length == shape.Colors.Length, "Per-vertex colors");
            check(shape.Triangles.Length % 3 == 0, "Complete triangles");
            foreach (var v in shape.Vertices)
                check(!float.IsNaN(v.x) && !float.IsNaN(v.y) && Math.Abs(v.x) <= 1.01 && Math.Abs(v.y) <= 1.01 && v.z == 0,
                    "Finite, bounded, planar motif geometry");
            foreach (int index in shape.Triangles) check(index >= 0 && index < shape.Vertices.Length, "Valid mesh index");
            triangles += shape.Triangles.Length / 3;
        }
        check(triangles == 252, "Fixed shared geometry budget");
        Draw(shapes, previewPath);
        Console.WriteLine("Geometry: 7 shared meshes / " + triangles + " triangles total; preview rendered from production vertices.");
    }
    private static void Draw(LakiGeometry.Shape[] shapes, string path)
    {
        using (var bitmap = new Bitmap(1440, 660))
        using (var g = Graphics.FromImage(bitmap))
        using (var title = new Font("Segoe UI", 28, FontStyle.Bold))
        using (var subtitle = new Font("Segoe UI", 12))
        using (var cardTitle = new Font("Segoe UI", 18, FontStyle.Bold))
        using (var white = new SolidBrush(Color.FromArgb(242, 243, 249)))
        using (var muted = new SolidBrush(Color.FromArgb(155, 163, 186)))
        using (var card = new SolidBrush(Color.FromArgb(23, 29, 44)))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(12, 16, 27));
            g.DrawString("Laki Notes", title, white, 52, 28);
            g.DrawString("A little luck, just for you.", subtitle, muted, 55, 82);
            string[] labels = { "Laki Note", "Super Laki Note", "Secret Laki Note" };
            string[] captions = { "Four-leaf clovers + mint sparkles", "Gold stars + a tiny pastel rainbow", "Crescent moon + quiet violet stars" };
            string[] effects = { "Soft outward drift / 0.24 s", "Star burst + rainbow pulse / 0.22 s", "Moonlit drift / 0.28 s" };
            int[][] indices = { new[] {0,1,1,0,1}, new[] {2,3,4,2,3}, new[] {5,6,6,6,6} };
            for (int kind = 0; kind < 3; kind++)
            {
                float left = 42 + kind * 465, cx = left + 212, cy = 370, scale = 430;
                g.FillRectangle(card, left, 140, 438, 438);
                g.DrawString(labels[kind], cardTitle, white, left + 26, 163);
                g.DrawString(captions[kind], subtitle, muted, left + 26, 202);
                using (var note = new SolidBrush(kind == 1 ? Color.FromArgb(40, 104, 190) : Color.FromArgb(184, 52, 79)))
                    g.FillRectangle(note, cx - 86, cy - 86, 172, 172);
                // A directional arrow remains completely unobstructed.
                using (var arrow = new SolidBrush(Color.FromArgb(239, 246, 255)))
                    g.FillPolygon(arrow, new[] { new PointF(cx-35,cy-9), new PointF(cx+35,cy-9), new PointF(cx,cy+25) });
                float[] x = { -.272f, -.075f, .10f, .272f, -.07f };
                float[] y = { .09f, .272f, .284f, -.09f, -.272f };
                for (int i = 0; i < 5; i++)
                {
                    float size = i == 0 || i == 3 ? .038f : .021f;
                    if (kind == 1 && i == 2) size = .048f;
                    if (kind == 2 && i == 0) size = .052f;
                    Shape(g, shapes[indices[kind][i]], cx + x[i] * scale, cy - y[i] * scale, size * scale);
                }
                g.DrawString(effects[kind], subtitle, white, left + 26, 519);
                g.DrawString("5 small decorations - clear center", subtitle, muted, left + 26, 546);
            }
            g.DrawString("Production mesh geometry preview. Not an in-game capture; VR scale, shader and visibility still need headset testing.",
                subtitle, muted, 48, 609);
            bitmap.Save(path, ImageFormat.Png);
        }
    }
    private static void Shape(Graphics graphics, LakiGeometry.Shape shape, float x, float y, float scale)
    {
        // Group equal-color triangles into one path to avoid antialiasing seams.
        var groups = new System.Collections.Generic.Dictionary<int, GraphicsPath>();
        try
        {
            for (int i = 0; i < shape.Triangles.Length; i += 3)
            {
                var color = shape.Colors[shape.Triangles[i]];
                int argb = Color.FromArgb(230, (int)(color.r * 255), (int)(color.g * 255), (int)(color.b * 255)).ToArgb();
                if (!groups.TryGetValue(argb, out var geometry)) { geometry = new GraphicsPath(FillMode.Winding); groups.Add(argb, geometry); }
                var points = new PointF[3];
                for (int v = 0; v < 3; v++)
                {
                    var p = shape.Vertices[shape.Triangles[i+v]];
                    points[v] = new PointF(x + p.x * scale, y - p.y * scale);
                }
                geometry.AddPolygon(points);
            }
            foreach (var entry in groups)
                using (var brush = new SolidBrush(Color.FromArgb(entry.Key))) graphics.FillPath(brush, entry.Value);
        }
        finally { foreach (var geometry in groups.Values) geometry.Dispose(); }
    }
}
