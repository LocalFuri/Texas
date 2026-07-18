using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Builds reusable chip prefabs from Assets/Graphic/Chips sprites.</summary>
    public static class ChipPrefabBuilder
    {
        public const string PrefabFolder = "Assets/Prefabs/Chips";

        private static readonly (int denom, string spritePath)[] ChipDefs =
        {
            (1,   "Assets/Graphic/Chips/chip1.png"),
            (5,   "Assets/Graphic/Chips/chip5.png"),
            (25,  "Assets/Graphic/Chips/chip25.png"),
            (100, "Assets/Graphic/Chips/chip100.png"),
            (500, "Assets/Graphic/Chips/chip500.png"),
        };

        [MenuItem("Texas Hold'em/Assets/Build Chip Prefabs")]
        public static void BuildChipPrefabsMenu()
        {
            BuildChipPrefabs();
            Debug.Log("[ChipPrefabBuilder] Chip prefabs built under " + PrefabFolder);
        }

        public static void BuildChipPrefabs()
        {
            EnsureFolder(PrefabFolder);

            foreach ((int denom, string spritePath) in ChipDefs)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[ChipPrefabBuilder] Missing sprite: {spritePath}");
                    continue;
                }

                string prefabPath = $"{PrefabFolder}/Chip{denom}.prefab";
                CreateOrUpdatePrefab(prefabPath, denom, sprite);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static ChipVisual LoadChipPrefab(int denomination)
            => AssetDatabase.LoadAssetAtPath<ChipVisual>($"{PrefabFolder}/Chip{denomination}.prefab");

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "Chips");
        }

        private static void CreateOrUpdatePrefab(string prefabPath, int denomination, Sprite sprite)
        {
            var go = new GameObject(
                $"Chip{denomination}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ChipVisual));

            try
            {
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(38f, 38f);

                var img = go.GetComponent<Image>();
                img.sprite         = sprite;
                img.type           = Image.Type.Simple;
                img.preserveAspect = true;
                img.raycastTarget  = false;
                img.color          = Color.white;

                var visual = go.GetComponent<ChipVisual>();
                var so     = new SerializedObject(visual);
                so.FindProperty("_denomination").intValue = denomination;
                so.FindProperty("_image").objectReferenceValue = img;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
