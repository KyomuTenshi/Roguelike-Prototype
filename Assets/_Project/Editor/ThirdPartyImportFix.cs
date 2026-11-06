using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RulesShift.EditorTools
{
    /// <summary>
    /// Third-party textures the game uses but the packs import "wrong" (blurred, compressed, cut as a sprite sheet):
    /// - Super Pixel Effects frames → one sprite each, 16 px per unit, point filter, no compression
    ///   (the scene references them as single sprites);
    /// - sheets cut at runtime (gamepad prompts, Modern tiles poses and friends) → point filter, no compression, no mipmaps;
    /// - the music the scene plays → compressed in memory, Vorbis at most 60 %, loaded in the background,
    ///   30 % in the web build (the music is most of the download and doesn't compress)
    ///   (a check: a re-imported pack comes back decompressed on load at 100 % — a lot of RAM and a heavy web build).
    /// Checked once per editor session (and from the menu); only textures that differ are reimported.
    /// </summary>
    [InitializeOnLoad]
    public static class ThirdPartyImportFix
    {
        const string Effects = "Assets/_ThirdParty/Super Pixel Effects Gigapack (Free Version)/PNG/";

        static readonly string[] EffectFolders =
        {
            "Symbols/symbol_alert_001/symbol_alert_001_small_red",
            "Symbols/symbol_warning_001/symbol_warning_001_small_yellow",
            "Symbols/symbol_complete_text_001/symbol_complete_text_001_small_blue",
            "Symbols/symbol_success_001/symbol_success_001_small_green",
            "Symbols/symbol_game_over_text_001/symbol_game_over_text_001_small_red",
            "Symbols/symbol_crown_001/symbol_crown_001_small_yellow",
            "Symbols/symbol_bonus_text_001/symbol_bonus_text_001_small_green",
            "Symbols/symbol_level_up_text_001/symbol_level_up_text_001_small_blue",
            "Symbols/symbol_question_001/symbol_question_001_small_red",
            "Fantasy Spells/spell_haste_001/spell_haste_001_small_green",
            "Fantasy Spells/spell_absorb_001/spell_absorb_001_small_violet",
            "Fantasy Spells/spell_death_001/spell_death_001_small_red",
            "Sci-fi/scifi_charge_up_001/scifi_charge_up_001_small_yellow",
            "Sci-fi/scifi_heartbeat_fast_001/scifi_heartbeat_fast_001_small_yellow",
            "Explosions/stylized_explosion_001/stylized_explosion_001_small_yellow",
            "Lightning/lightning_strike_001/lightning_strike_001_small_violet",
            "Magic Bursts/directional_music_burst_001/directional_music_burst_001_small_red",
            "Magic Bursts/round_light_burst_001/round_light_burst_001_small_yellow",
        };

        static readonly string[] Sheets =
        {
            "gdb-xbox-2", "gdb-playstation-2", "gdb-switch-2", "gdb-keyboard-2",
            "Adam_sit_16x16", "Amelia_sit_16x16", "Adam_phone_16x16", "Amelia_phone_16x16",
            "Alex_idle_anim_16x16", "Bob_idle_anim_16x16", "Alex_run_16x16", "Bob_run_16x16",
        };

        static ThirdPartyImportFix()
        {
            if (SessionState.GetBool("RulesShift.ThirdPartyImportFix.v3", false)) return;
            SessionState.SetBool("RulesShift.ThirdPartyImportFix.v3", true);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/RULECRAWL/Fix third-party imports")]
        public static void Run()
        {
            var changed = new List<string>();
            foreach (var folder in EffectFolders)
            {
                string path = Effects + folder;
                if (!AssetDatabase.IsValidFolder(path)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(p) is TextureImporter ti && Fix(ti, true)) changed.Add(p);
                }
            }
            foreach (var name in Sheets)
                foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { "Assets/_ThirdParty" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(p) != name) continue;
                    if (AssetImporter.GetAtPath(p) is TextureImporter ti && Fix(ti, false)) changed.Add(p);
                }
            FixMusic(changed);
            if (changed.Count == 0) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in changed)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(p);
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"[RULECRAWL] Reimported {changed.Count} third-party assets (pixel-art textures, music for phones and the web).");
        }

        const string Scene = "Assets/_Project/Scenes/Game.unity";

        const float WebMusicQuality = 0.3f;

        /// <summary>Every long clip the scene uses (music, not the short sound effects).</summary>
        static void FixMusic(List<string> changed)
        {
            foreach (var p in AssetDatabase.GetDependencies(Scene, true))
            {
                if (!p.StartsWith("Assets/_ThirdParty/")) continue;
                if (!(AssetImporter.GetAtPath(p) is AudioImporter ai)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip == null || clip.length < 20f) continue;   // jingles and sound effects stay as they are
                var s = ai.defaultSampleSettings;
                bool dirty = false;
                if (s.loadType != AudioClipLoadType.CompressedInMemory) { s.loadType = AudioClipLoadType.CompressedInMemory; dirty = true; }
                if (s.compressionFormat != AudioCompressionFormat.Vorbis) { s.compressionFormat = AudioCompressionFormat.Vorbis; dirty = true; }
                if (s.quality > 0.65f) { s.quality = 0.6f; dirty = true; }
                if (dirty || !ai.loadInBackground)
                {
                    ai.defaultSampleSettings = s;
                    ai.loadInBackground = true;
                }
                // the web build: music is most of the download and doesn't compress, so it gets a lighter encoding
                // (16-bit chiptune survives it well); desktop builds keep the 60 %
                var web = ai.ContainsSampleSettingsOverride("WebGL") ? ai.GetOverrideSampleSettings("WebGL") : s;
                bool webDirty = !ai.ContainsSampleSettingsOverride("WebGL") || web.quality > WebMusicQuality + 0.01f
                                || web.loadType != AudioClipLoadType.CompressedInMemory;
                if (webDirty)
                {
                    web.loadType = AudioClipLoadType.CompressedInMemory;
                    web.compressionFormat = AudioCompressionFormat.Vorbis;
                    web.quality = WebMusicQuality;
                    ai.SetOverrideSampleSettings("WebGL", web);
                }
                if (!dirty && !webDirty && ai.loadInBackground) continue;
                EditorUtility.SetDirty(ai);
                changed.Add(p);
            }
        }

        /// <summary>Sets the pixel-art import settings; true when something had to change.</summary>
        static bool Fix(TextureImporter ti, bool singleSprite)
        {
            bool dirty = false;
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (singleSprite)
            {
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!Mathf.Approximately(ti.spritePixelsPerUnit, 16f)) { ti.spritePixelsPerUnit = 16f; dirty = true; }
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                if (st.spriteMeshType != SpriteMeshType.FullRect)
                {
                    st.spriteMeshType = SpriteMeshType.FullRect;
                    ti.SetTextureSettings(st);
                    dirty = true;
                }
            }
            if (dirty) EditorUtility.SetDirty(ti);
            return dirty;
        }
    }
}
