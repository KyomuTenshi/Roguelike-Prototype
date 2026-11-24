using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace RulesShift.EditorTools
{
    /// <summary>
    /// Packs the loose 0x72 sprites (every frame is its own PNG) into sprite atlases, so heroes, enemies, props,
    /// chests and coins share one texture and draw in a few batches instead of one draw call per texture
    /// (it matters most in the web build and on phones).
    /// Floor and wall tiles stay out: the tilemaps would show thin seams between atlas tiles.
    /// Created once (on opening the project, if missing) and from the menu; the atlases live in Assets/_Project/Atlases.
    /// </summary>
    [InitializeOnLoad]
    public static class SpriteAtlasBuilder
    {
        const string Folder = "Assets/_Project/Atlases";

        static readonly (string name, string source)[] Sets =
        {
            ("Dungeon", "Assets/_ThirdParty/0x72_DungeonTilesetII_v1.7/frames"),
            ("ClassicItems", "Assets/_ThirdParty/0x72_16x16DungeonTileset.v5/items"),
        };

        // wall decorations that stand in the room as props, not tiles
        static readonly string[] WallProps = { "wall_banner", "wall_fountain", "wall_goo" };

        static SpriteAtlasBuilder()
        {
            if (SessionState.GetBool("RulesShift.SpriteAtlasBuilder.v1", false)) return;
            SessionState.SetBool("RulesShift.SpriteAtlasBuilder.v1", true);
            EditorApplication.delayCall += () => Build(false);
        }

        [MenuItem("Tools/RULECRAWL/Build sprite atlases")]
        static void Rebuild() => Build(true);

        static void Build(bool force)
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            int made = 0;
            foreach (var (name, source) in Sets)
            {
                if (!AssetDatabase.IsValidFolder(source)) continue;   // the pack is not in the project yet
                string path = $"{Folder}/{name}.spriteatlasv2";
                if (!force && File.Exists(path)) continue;

                var sprites = new List<Object>();
                foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { source }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                        if (o is Sprite s && !IsTile(s.name)) sprites.Add(s);
                }
                if (sprites.Count == 0) continue;

                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project", "Atlases");
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                var atlas = new SpriteAtlasAsset();
                atlas.Add(sprites.ToArray());
                SpriteAtlasAsset.Save(atlas, path);
                AssetDatabase.ImportAsset(path);

                if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter importer)
                {
                    importer.includeInBuild = true;
                    importer.packingSettings = new SpriteAtlasPackingSettings
                    {
                        blockOffset = 1,
                        padding = 4,
                        enableRotation = false,      // pixel art: no rotated or tight-packed sprites
                        enableTightPacking = false,
                    };
                    importer.textureSettings = new SpriteAtlasTextureSettings
                    {
                        filterMode = FilterMode.Point,
                        generateMipMaps = false,
                        readable = false,
                        sRGB = true,
                        anisoLevel = 0,
                    };
                    var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
                    platform.maxTextureSize = 2048;
                    platform.textureCompression = TextureImporterCompression.Uncompressed;   // crisp pixels
                    platform.crunchedCompression = false;
                    importer.SetPlatformSettings(platform);
                    importer.SaveAndReimport();
                }
                made++;
            }
            if (made > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[RULECRAWL] Built {made} sprite atlas(es) in {Folder}.");
            }
        }

        /// <summary>Floor and wall pieces the tilemaps use.</summary>
        static bool IsTile(string n)
        {
            string s = n.ToLowerInvariant();
            if (s == "hole" || s.StartsWith("edge") || s == "column_wall") return true;
            if (s.StartsWith("floor_")) return !s.StartsWith("floor_spikes");
            if (s.StartsWith("wall_"))
            {
                foreach (var prop in WallProps) if (s.StartsWith(prop)) return false;
                return true;
            }
            return false;
        }
    }
}
