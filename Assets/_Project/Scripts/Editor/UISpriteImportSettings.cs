using UnityEditor;
using UnityEngine;

// Auto-configures every PNG under Assets/_Project/UI as a UI sprite.
// - Texture Type: Sprite (2D and UI), single, no mipmaps, clamp, uncompressed alpha
// - Board_*: 9-slice border so the panel can be resized without stretching the frame
// - ClockHand: pivot at the hub so it can rotate around the clock centre
// UI_v2 (page "4 · Revisi & Tambahan", exported at 2x): same settings, but
// Pixels Per Unit = 200 so "Set Native Size" gives the exact Figma (in-game) size.
public class UISpriteImportSettings : AssetPostprocessor
{
    const string Root = "Assets/_Project/UI/";
    const string RootV2 = "Assets/_Project/UI_v2/";

    void OnPreprocessTexture()
    {
        bool v2 = assetPath.StartsWith(RootV2);
        if ((!assetPath.StartsWith(Root) && !v2) || !assetPath.EndsWith(".png")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.spritePixelsPerUnit = v2 ? 200 : 100;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = true;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;

        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        if (file.StartsWith("Board_"))
        {
            importer.spriteBorder = new Vector4(90, 90, 90, 90); // left, bottom, right, top
        }
        if (file == "ClockHand")
        {
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0.15f);
        }
        importer.SetTextureSettings(settings);
    }
}
