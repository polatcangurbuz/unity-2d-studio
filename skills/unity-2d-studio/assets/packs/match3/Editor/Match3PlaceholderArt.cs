using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace __NS__.Puzzle.EditorTools
{
    // Procedural placeholder art that already reads well: one shape per colour (colour-blind safe), shading, rim, highlight.
    public static class Match3PlaceholderArt
    {
        public const int Size = 128;
        public static readonly Color[] Colors =
        {
            Hex(0xE8453C), Hex(0x4CBF4B), Hex(0x3D8BE8), Hex(0xF5C928), Hex(0x9B59D0), Hex(0xF28C28),
        };

        // n-gon sides (0 = circle) and rotation in degrees per colour index.
        private static readonly (int sides, float rotation)[] Shapes = { (0, 0), (4, 45), (4, 0), (3, 90), (5, 90), (6, 0) };

        public static Sprite Piece(string folder, int color) =>
            Save(folder, $"Piece_{color}", (x, y) => Gem(x, y, Colors[color], Shapes[color].sides, Shapes[color].rotation, 0.42f));

        public static Sprite Rocket(string folder, bool horizontal) => Save(folder, horizontal ? "RocketH" : "RocketV", (x, y) =>
        {
            var (u, v) = horizontal ? (x, y) : (y, x);
            var body = Box(u, v, 0.36f, 0.16f, 0.14f);
            var stripe = Mathf.Repeat(u * 6f, 1f) < 0.5f ? 0.85f : 1f;
            return Shade(body, new Color(stripe, stripe, stripe), u, v);
        });

        public static Sprite Bomb(string folder) => Save(folder, "Bomb", (x, y) =>
        {
            var ball = Shade(Poly(x, y + 0.04f, 0, 0, 0.34f), new Color(0.35f, 0.35f, 0.4f), x, y);
            var fuse = Box(x - 0.16f, y - 0.32f, 0.04f, 0.1f, 0.02f);
            return Over(ball, new Color(1f, 0.85f, 0.3f, Coverage(fuse)));
        });

        public static Sprite ColorBomb(string folder) => Save(folder, "ColorBomb", (x, y) =>
        {
            var hue = Mathf.Repeat(Mathf.Atan2(y, x) / (2f * Mathf.PI) + 0.5f, 1f);
            return Shade(Poly(x, y, 0, 0, 0.4f), Color.HSVToRGB(hue, 0.65f, 1f), x, y);
        });

        public static Sprite Cell(string folder) => Save(folder, "Cell", (x, y) => new Color(1, 1, 1, Coverage(Box(x, y, 0.47f, 0.47f, 0.08f))));

        public static Sprite Crate(string folder, int hp) => Save(folder, $"Crate_{hp}", (x, y) =>
        {
            var wood = Color.Lerp(Hex(0xB07A43), Hex(0x6E4423), (hp - 1) / 2f);
            var plank = Mathf.Abs(Mathf.Repeat(y * 4f, 1f) - 0.5f) > 0.45f ? 0.75f : 1f;
            var strap = hp > 1 && Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(y)) < 0.05f ? Hex(0x9AA0A8) : wood * plank;
            strap.a = 1f;
            return Shade(Box(x, y, 0.44f, 0.44f, 0.06f), strap, x, y);
        });

        public static Sprite Ice(string folder, int hp) => Save(folder, $"Ice_{hp}", (x, y) =>
        {
            var c = new Color(0.75f, 0.92f, 1f, 0.35f + 0.2f * hp);
            var crack = hp == 1 && Mathf.Abs(x * 0.7f - y + 0.05f) < 0.015f ? 0.4f : 0f;
            c.a *= Coverage(Box(x, y, 0.47f, 0.47f, 0.08f));
            return Color.Lerp(c, new Color(1, 1, 1, c.a), crack + Mathf.Clamp01(0.3f - (x + y)) * 0.6f);
        });

        public static Sprite Chain(string folder, int hp) => Save(folder, $"Chain_{hp}", (x, y) =>
        {
            var link = Mathf.Min(Mathf.Abs(Mathf.Abs(x - y) * 0.7071f), Mathf.Abs(Mathf.Abs(x + y) * 0.7071f));
            var a = Mathf.Clamp01((0.05f + 0.02f * hp - link) * Size) * Coverage(Box(x, y, 0.45f, 0.45f, 0.1f));
            return new Color(0.62f, 0.64f, 0.7f, a);
        });

        // Hard-edged square for the board's SpriteMask: rounded corners would clip the corner pieces once scaled to the board.
        public static Sprite Square(string folder) => Save(folder, "Square", (x, y) => Color.white);

        public static Sprite Spark(string folder) => Save(folder, "Spark", (x, y) =>
            new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 0.5f)));

        private static Color Gem(float x, float y, Color baseColor, int sides, float rotation, float radius) =>
            Shade(Poly(x, y, sides, rotation, radius), baseColor, x, y);

        // Lighter top-left, darker rim, soft white highlight: cheap "material" without a shader.
        private static Color Shade(float distance, Color c, float x, float y)
        {
            var light = Mathf.Clamp01(0.5f - (y - x) * 0.6f);
            var col = Color.Lerp(c * 0.8f, Color.Lerp(c, Color.white, 0.25f), light);
            var rim = Mathf.Clamp01((distance + 0.04f) / 0.04f);
            col = Color.Lerp(col, c * 0.55f, rim);
            var hx = x + 0.12f;
            var hy = y - 0.16f;
            var highlight = Mathf.Clamp01(1f - (hx * hx * 9f + hy * hy * 30f)) * 0.55f;
            col = Color.Lerp(col, Color.white, highlight);
            col.a = Coverage(distance) * c.a;
            return col;
        }

        // Signed distance (negative inside) to a regular polygon; sides 0 = circle.
        private static float Poly(float x, float y, int sides, float rotation, float radius)
        {
            var r = Mathf.Sqrt(x * x + y * y);
            if (sides == 0) return r - radius;
            var sector = 2f * Mathf.PI / sides;
            var a = Mathf.Repeat(Mathf.Atan2(y, x) - rotation * Mathf.Deg2Rad, sector) - sector / 2f;
            return r * Mathf.Cos(a) - radius * Mathf.Cos(Mathf.PI / sides);
        }

        private static float Box(float x, float y, float halfW, float halfH, float round)
        {
            var qx = Mathf.Abs(x) - halfW + round;
            var qy = Mathf.Abs(y) - halfH + round;
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - round;
        }

        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance * Size);

        private static Color Over(Color under, Color over) => Color.Lerp(under, new Color(over.r, over.g, over.b, Mathf.Max(under.a, over.a)), over.a);

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        // Pixel centres in -0.5..0.5 (y up); writes a PNG once and imports it as a single sprite, PPU = Size (1 unit wide).
        private static Sprite Save(string folder, string name, Func<float, float, Color> pixel)
        {
            var path = $"{folder}/{name}.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(folder);
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var px = new Color[Size * Size];
                for (var j = 0; j < Size; j++)
                for (var i = 0; i < Size; i++)
                    px[j * Size + i] = pixel((i + 0.5f) / Size - 0.5f, (j + 0.5f) / Size - 0.5f);
                tex.SetPixels(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != Size || importer.filterMode != FilterMode.Bilinear)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Size;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
