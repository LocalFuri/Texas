using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Bold pre-colored green checkmark inside white checkbox (legacy debug menu).</summary>
    public static class OptionsMenuToggleStyle
    {
        private const string ProjectCheckboxPath   = "UI/OptionsCheckbox";
        private const string BoldCheckmarkPath     = "UI/OptionsCheckBold";

        // Reference green ~#468e49
        private static readonly Color BoldCheckGreen = new Color(0.275f, 0.557f, 0.286f, 1f);

        private static Sprite _whiteSprite;
        private static Sprite _checkmarkSprite;

        /// <summary>Editor builder can inject sprites so the scene saves references.</summary>
        public static void CacheSprites(Sprite uiSprite, Sprite checkmarkSprite)
        {
            if (uiSprite != null)
                _whiteSprite = uiSprite;
            if (checkmarkSprite != null)
                _checkmarkSprite = checkmarkSprite;
        }

        public static void EnsurePanelBackgroundSprite(Image bg)
        {
            if (bg == null)
                return;

            if (bg.sprite == null)
                bg.sprite = GetWhiteSprite();
        }

        /// <summary>Clears cached sprites so Play mode always reloads project assets.</summary>
        public static void ResetRuntimeSprites()
        {
            _whiteSprite     = null;
            _checkmarkSprite = null;
        }

        public static void Apply(Toggle toggle)
        {
            if (toggle == null)
                return;

            ApplyInternal(toggle);
        }

        /// <summary>Shared flat white sprite for checkbox backgrounds and slider parts.</summary>
        public static Sprite GetCheckboxSprite() => GetWhiteSprite();

        private static void ApplyInternal(Toggle toggle)
        {
            Transform background = toggle.transform.Find("Background");
            if (background != null)
            {
                var bgImg = background.GetComponent<Image>();
                if (bgImg != null)
                {
                    Sprite whiteSprite = GetWhiteSprite();
                    bgImg.sprite         = whiteSprite;
                    bgImg.color          = Color.white;
                    bgImg.raycastTarget  = true;
                    toggle.targetGraphic = bgImg;
                }
            }

            Transform check = toggle.transform.Find("Background/Checkmark");
            if (check != null)
            {
                if (check is RectTransform checkRt)
                {
                    checkRt.anchorMin        = Vector2.zero;
                    checkRt.anchorMax        = Vector2.one;
                    checkRt.sizeDelta        = Vector2.zero;
                    checkRt.anchoredPosition = Vector2.zero;
                }

                SetupCheckmarkGraphic(toggle, check.gameObject);
            }

            toggle.transition       = Selectable.Transition.None;
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.colors = new ColorBlock
            {
                normalColor      = Color.white,
                highlightedColor = Color.white,
                pressedColor     = Color.white,
                selectedColor    = Color.white,
                disabledColor    = new Color(0.55f, 0.55f, 0.55f, 0.5f),
                colorMultiplier  = 1f,
                fadeDuration     = 0f
            };

            RefreshToggleVisual(toggle);
        }

        private static void SetupCheckmarkGraphic(Toggle toggle, GameObject checkGo)
        {
            foreach (var tmp in checkGo.GetComponents<TMPro.TextMeshProUGUI>())
                tmp.enabled = false;

            var checkImg = checkGo.GetComponent<Image>();
            if (checkImg == null)
                checkImg = checkGo.AddComponent<Image>();

            Sprite checkmark = GetBoldCheckmarkSprite();
            checkImg.enabled        = true;
            checkImg.sprite         = checkmark;
            checkImg.color          = Color.white;
            checkImg.preserveAspect = false;
            checkImg.raycastTarget  = false;
            toggle.graphic          = checkImg;
        }

        private static void RefreshToggleVisual(Toggle toggle)
        {
            bool isOn = toggle.isOn;
            toggle.SetIsOnWithoutNotify(!isOn);
            toggle.SetIsOnWithoutNotify(isOn);
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;

            // Flat white square only — never UISprite (gray beveled box).
            _whiteSprite = Resources.Load<Sprite>(ProjectCheckboxPath);

            if (_whiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            }

            return _whiteSprite;
        }

        private static Sprite GetBoldCheckmarkSprite()
        {
            if (_checkmarkSprite != null)
                return _checkmarkSprite;

            _checkmarkSprite = Resources.Load<Sprite>(BoldCheckmarkPath);
            if (_checkmarkSprite != null)
                return _checkmarkSprite;

            _checkmarkSprite = CreateBoldCheckmarkSprite();
            return _checkmarkSprite;
        }

        private static Sprite CreateBoldCheckmarkSprite()
        {
            const int size = 64;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            DrawBoldStroke(pixels, size, 8, 36, 30, 56, 9);
            DrawBoldStroke(pixels, size, 30, 56, 56, 8, 9);

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DrawBoldStroke(
            Color[] pixels, int size,
            int x0, int y0, int x1, int y1, int radius)
        {
            int dx  = Mathf.Abs(x1 - x0);
            int dy  = Mathf.Abs(y1 - y0);
            int sx  = x0 < x1 ? 1 : -1;
            int sy  = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                FillDisk(pixels, size, x0, y0, radius, BoldCheckGreen);

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0  += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0  += sy;
                }
            }
        }

        private static void FillDisk(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > r2)
                        continue;

                    int px = cx + x;
                    int py = cy + y;
                    if (px < 0 || py < 0 || px >= size || py >= size)
                        continue;

                    pixels[py * size + px] = color;
                }
            }
        }
    }
}
