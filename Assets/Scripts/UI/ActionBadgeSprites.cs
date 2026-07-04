using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Resolves PNG badge sprites for seat action display.</summary>
    public static class ActionBadgeSprites
    {
        public const string CheckPath  = "Assets/Graphic/Badges/Check_Image_trans.png";
        public const string FoldPath   = "Assets/Graphic/Badges/Fold_Image_trans.png";
        public const string RaisePath  = "Assets/Graphic/Badges/Raise_Image_trans.png";
        public const string AllInPath  = "Assets/Graphic/Badges/All-in_image_trans.png";
        public const string WinnerPath = "Assets/Graphic/Badges/Winner_image_trans.png";

        public const string ResourcesAssetPath = "Assets/Resources/ActionBadgeSpriteSet.asset";
        public const string ResourcesLoadName  = "ActionBadgeSpriteSet";

        public const float DefaultBadgeHeight = 40f;

        private static Sprite _check;
        private static Sprite _fold;
        private static Sprite _raise;
        private static Sprite _allIn;
        private static Sprite _winner;

        public static bool IsLoaded =>
            _check != null && _fold != null && _raise != null && _allIn != null && _winner != null;

        public static Sprite For(BettingAction action)
        {
            EnsureLoaded();
            return action switch
            {
                BettingAction.Check => _check,
                BettingAction.Call  => _check,
                BettingAction.Fold  => _fold,
                BettingAction.Raise => _raise,
                BettingAction.AllIn => _allIn,
                _                   => _raise,
            };
        }

        public static Sprite Winner
        {
            get
            {
                EnsureLoaded();
                return _winner;
            }
        }

        public static void EnsureLoaded()
        {
            if (IsLoaded)
                return;

            ActionBadgeSpriteSet set = Resources.Load<ActionBadgeSpriteSet>(ResourcesLoadName);
            if (set != null)
            {
                _check  ??= set.Check;
                _fold   ??= set.Fold;
                _raise  ??= set.Raise;
                _allIn  ??= set.AllIn;
                _winner ??= set.Winner;
            }

#if UNITY_EDITOR
            _check  ??= LoadEditorSprite(CheckPath);
            _fold   ??= LoadEditorSprite(FoldPath);
            _raise  ??= LoadEditorSprite(RaisePath);
            _allIn  ??= LoadEditorSprite(AllInPath);
            _winner ??= LoadEditorSprite(WinnerPath);
#endif

            if (_check == null)
                Debug.LogWarning("[ActionBadgeSprites] Check/Call badge sprite missing — run Texas Holdem → Create Action Badge Sprite Set.");
        }

        public static Vector2 SizeForSprite(Sprite sprite, float height = DefaultBadgeHeight)
        {
            if (sprite == null || sprite.rect.height <= 0f)
                return new Vector2(120f, height);

            float width = height * (sprite.rect.width / sprite.rect.height);
            return new Vector2(width, height);
        }

#if UNITY_EDITOR
        public static ActionBadgeSpriteSet LoadOrCreateResourcesAsset()
        {
            ActionBadgeSpriteSet set =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ActionBadgeSpriteSet>(ResourcesAssetPath);

            if (set == null)
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                    UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

                set = ScriptableObject.CreateInstance<ActionBadgeSpriteSet>();
                UnityEditor.AssetDatabase.CreateAsset(set, ResourcesAssetPath);
            }

            set.Check  = LoadEditorSprite(CheckPath);
            set.Fold   = LoadEditorSprite(FoldPath);
            set.Raise  = LoadEditorSprite(RaisePath);
            set.AllIn  = LoadEditorSprite(AllInPath);
            set.Winner = LoadEditorSprite(WinnerPath);

            UnityEditor.EditorUtility.SetDirty(set);
            UnityEditor.AssetDatabase.SaveAssets();

            _check = _fold = _raise = _allIn = _winner = null;
            EnsureLoaded();

            return set;
        }

        private static Sprite LoadEditorSprite(string assetPath)
        {
            Sprite direct = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (direct != null)
                return direct;

            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            return null;
        }
#endif
    }
}
