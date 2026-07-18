using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TexasHoldem
{
    public static class CasinoFontAssetCreator
    {
        public const string SourceFontPath       = "Assets/TextMesh Pro/Fonts/Casino3D.ttf";
        public const string SdfAssetPath         = "Assets/TextMesh Pro/Fonts/Casino3D SDF.asset";
        public const string SdfResourcesPath     = "Assets/Resources/Fonts/Casino3D SDF.asset";
        public const string FilledSourceFontPath = "Assets/TextMesh Pro/Fonts/Casino3DFilled.ttf";
        public const string FilledSdfAssetPath   = "Assets/TextMesh Pro/Fonts/Casino3DFilled SDF.asset";

        [InitializeOnLoadMethod]
        private static void EnsureOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (BuildPipeline.isBuildingPlayer)
                    return;

                EnsureExists(SdfAssetPath, SourceFontPath, "Casino3D SDF", force: false);
                EnsureExists(FilledSdfAssetPath, FilledSourceFontPath, "Casino3DFilled SDF", force: false);
            };
        }

        [MenuItem("Texas Hold'em/Assets/Generate Casino3D SDF Font")]
        public static void EnsureExistsMenu()
        {
            EnsureExists(SdfAssetPath, SourceFontPath, "Casino3D SDF", force: false);
            EnsureExists(FilledSdfAssetPath, FilledSourceFontPath, "Casino3DFilled SDF", force: false);
        }

        [MenuItem("Texas Hold'em/Assets/Regenerate Casino3D SDF Font (Force)")]
        public static void ForceRegenerateMenu()
        {
            EnsureExists(SdfAssetPath, SourceFontPath, "Casino3D SDF", force: true);
            EnsureExists(FilledSdfAssetPath, FilledSourceFontPath, "Casino3DFilled SDF", force: true);
        }

        /// <summary>Entry point for Unity batch mode: -executeMethod TexasHoldem.CasinoFontAssetCreator.BatchRegenerate</summary>
        public static void BatchRegenerate()
        {
            EnsureExists(SdfAssetPath, SourceFontPath, "Casino3D SDF", force: true);
            EnsureExists(FilledSdfAssetPath, FilledSourceFontPath, "Casino3DFilled SDF", force: true);
        }

        public static void EnsureExists(string sdfPath, string sourcePath, string assetName, bool force)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
            if (!force && HasBakedGlyphs(existing))
            {
                if (sdfPath == SdfAssetPath && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfResourcesPath) == null)
                    SyncResourcesCopy();
                return;
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"Casino font source not found at {sourcePath}");
                return;
            }

            if (existing != null)
                DeleteAssetPreserveMeta(sdfPath);

            TMP_FontAsset fontAsset = CreatePopulatedFontAsset(source);
            if (fontAsset == null)
            {
                Debug.LogError($"Failed to create TMP font asset for {assetName}.");
                return;
            }

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, sdfPath);

            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            if (fontAsset.atlasTexture != null)
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created TMP font asset at {sdfPath} ({fontAsset.characterTable.Count} characters)");

            if (sdfPath == SdfAssetPath)
                SyncResourcesCopy();
        }

        private static void SyncResourcesCopy()
        {
            const string resourcesDir = "Assets/Resources/Fonts";
            if (!AssetDatabase.IsValidFolder(resourcesDir))
                return;

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfResourcesPath) != null)
                AssetDatabase.DeleteAsset(SdfResourcesPath);

            if (!AssetDatabase.CopyAsset(SdfAssetPath, SdfResourcesPath))
                Debug.LogWarning($"Failed to copy {SdfAssetPath} to {SdfResourcesPath}");
            else
                Debug.Log($"Synced build font copy to {SdfResourcesPath}");
        }

        public static bool HasBakedGlyphs(TMP_FontAsset fontAsset)
        {
            return fontAsset != null
                && fontAsset.characterTable != null
                && fontAsset.characterTable.Count > 0;
        }

        private static TMP_FontAsset CreatePopulatedFontAsset(Font source)
        {
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                samplingPointSize: 72,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
                return null;

            string characters = GetButtonCharacterSet();
            if (!fontAsset.TryAddCharacters(characters, out string missing))
                Debug.LogWarning($"Casino3D font missing characters: {missing}");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            return fontAsset;
        }

        private static string GetButtonCharacterSet()
        {
            var sb = new StringBuilder(96);
            for (int i = 32; i <= 126; i++)
                sb.Append((char)i);
            return sb.ToString();
        }

        private static void DeleteAssetPreserveMeta(string assetPath)
        {
            if (!File.Exists(assetPath))
                return;

            AssetDatabase.StartAssetEditing();
            try
            {
                File.Delete(assetPath);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
        }
    }
}
