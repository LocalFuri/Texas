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

        public const string ResourcesAssetPath = "ActionBadgeSpriteSet";

        public const float DefaultBadgeHeight = 40f;

        private static ActionBadgeSpriteSet _set;
        private static Sprite _check;
        private static Sprite _fold;
        private static Sprite _raise;
        private static Sprite _allIn;
        private static Sprite _winner;

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
            if (_check != null)
                return;

            _set = Resources.Load<ActionBadgeSpriteSet>(ResourcesAssetPath);
            if (_set != null)
            {
                _check  = _set.Check;
                _fold   = _set.Fold;
                _raise  = _set.Raise;
                _allIn  = _set.AllIn;
                _winner = _set.Winner;
            }

#if UNITY_EDITOR
            if (_check == null)  _check  = LoadEditorSprite(CheckPath);
            if (_fold == null)   _fold   = LoadEditorSprite(FoldPath);
            if (_raise == null)  _raise  = LoadEditorSprite(RaisePath);
            if (_allIn == null)  _allIn  = LoadEditorSprite(AllInPath);
            if (_winner == null) _winner = LoadEditorSprite(WinnerPath);
#endif
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
            ActionBadgeSpriteSet existing =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ActionBadgeSpriteSet>(
                    "Assets/Resources/ActionBadgeSpriteSet.asset");
            if (existing != null)
                return existing;

            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            var set = ScriptableObject.CreateInstance<ActionBadgeSpriteSet>();
            set.Check  = LoadEditorSprite(CheckPath);
            set.Fold   = LoadEditorSprite(FoldPath);
            set.Raise  = LoadEditorSprite(RaisePath);
            set.AllIn  = LoadEditorSprite(AllInPath);
            set.Winner = LoadEditorSprite(WinnerPath);

            UnityEditor.AssetDatabase.CreateAsset(set, "Assets/Resources/ActionBadgeSpriteSet.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            return set;
        }

        private static Sprite LoadEditorSprite(string assetPath) =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#endif
    }
}
