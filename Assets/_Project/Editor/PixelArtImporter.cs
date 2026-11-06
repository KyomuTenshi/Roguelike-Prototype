using UnityEditor;
using UnityEngine;

namespace RulesShift.EditorTools
{
    /// <summary>
    /// New textures dropped into Assets/_ThirdParty import as pixel art: point filter, no compression, no mipmaps.
    /// Only the first import (no .meta yet) is touched, settings changed by hand are left alone.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_ThirdParty/") || !assetImporter.importSettingsMissing) return;
            var ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
        }
    }
}
