using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Studio.EditorTools
{
    // Import settings for Art/Sprites come from .studio/brief.md ("style: pixel 16 PPU"), so every sprite matches the art bible.
    public class SpriteImportRules : AssetPostprocessor
    {
        private const string SpriteFolder = "Assets/_Project/Art/Sprites/";
        private static readonly Regex SheetName = new Regex(@"_sheet_(\d+)x(\d+)$", RegexOptions.IgnoreCase);

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteFolder)) return;
            var importer = (TextureImporter)assetImporter;
            var (ppu, pixelArt) = ReadBrief();

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            if (pixelArt)
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }

            var sheet = SheetName.Match(Path.GetFileNameWithoutExtension(assetPath));
            importer.spriteImportMode = sheet.Success ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            if (sheet.Success) importer.spritePivot = new Vector2(0.5f, 0f);
        }

        // Grid slicing needs the final pixel size, which only exists after import.
        private void OnPostprocessTexture(Texture2D texture)
        {
            var sheet = SheetName.Match(Path.GetFileNameWithoutExtension(assetPath));
            if (!assetPath.StartsWith(SpriteFolder) || !sheet.Success) return;
            var importer = (TextureImporter)assetImporter;
#pragma warning disable 0618
            if (importer.spritesheet.Length > 0) return;
            int w = int.Parse(sheet.Groups[1].Value), h = int.Parse(sheet.Groups[2].Value);
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var cells = new System.Collections.Generic.List<SpriteMetaData>();
            for (var y = texture.height - h; y >= 0; y -= h)
            for (var x = 0; x + w <= texture.width; x += w)
                cells.Add(new SpriteMetaData { name = $"{name}_{cells.Count}", rect = new Rect(x, y, w, h), alignment = (int)SpriteAlignment.BottomCenter });
            importer.spritesheet = cells.ToArray();
#pragma warning restore 0618
            EditorApplication.delayCall += () => importer.SaveAndReimport();
        }

        private static (int ppu, bool pixelArt) ReadBrief()
        {
            var brief = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".studio", "brief.md");
            var text = File.Exists(brief) ? File.ReadAllText(brief) : string.Empty;
            var ppu = Regex.Match(text, @"(\d+)\s*PPU", RegexOptions.IgnoreCase);
            return (ppu.Success ? int.Parse(ppu.Groups[1].Value) : 100, Regex.IsMatch(text, @"style:\s*pixel", RegexOptions.IgnoreCase));
        }
    }
}
