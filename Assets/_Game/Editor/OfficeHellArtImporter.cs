using UnityEditor;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>Importer policy for derived art. Source PSD files stay outside Assets.</summary>
    public sealed class OfficeHellArtImporter : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Game/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
            importer.textureCompression = TextureImporterCompression.Compressed;

            if (assetPath.Contains("/Branding/LogoMain") || assetPath.Contains("/Environment/OfficeMap"))
            {
                importer.maxTextureSize = 2048;
            }
            else if (assetPath.Contains("/Effects/Pie"))
            {
                importer.maxTextureSize = 1024;
            }
            else
            {
                importer.maxTextureSize = 512;
            }
        }
    }
}
