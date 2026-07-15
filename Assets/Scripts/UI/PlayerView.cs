using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TexasHoldem
{
    [DefaultExecutionOrder(-100)]
    public class PlayerView : MonoBehaviour
    {
        private const string PrefabDefaultName = "Alex Hunter";

        private static readonly NumberFormatInfo GermanMoneyNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 1,
            NumberGroupSizes       = new[] { 3 }
        };

        /// <summary>Formats seat chips for the HUD, e.g. 4002 chips at BB 20 → "4.002,0 (200 BB)".</summary>
        public static string FormatChipsHud(int chips, int bigBlind)
        {
            string money = chips.ToString("N1", GermanMoneyNFI);
            if (bigBlind <= 0)
                return money;

            int bb = chips / bigBlind;
            return $"{money} ({bb} BB)";
        }

        [Header("HUD Text")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _chipsText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _equityText;
        private TMP_Text _equityAdviceText;
        [Tooltip("Character name for this seat — source of truth for game logic and HUD.")]
        [SerializeField] private string _displayName;

        [Header("Font Sizes")]
        [SerializeField] private float _nameFontSize   = 20f;
        [SerializeField] private float _nameFontSizeMin = 14f;
        [SerializeField] private float _statusFontSize = 10f;

        [Header("Cards")]
        [SerializeField] private List<CardView> _cardSlots;

        [Header("Seat Visuals")]
        [SerializeField] private CanvasGroup          _canvasGroup;
        [FormerlySerializedAs("_avatarRingSdf")]
        [SerializeField] private AvatarRingSdfGraphic _avatarRingChrome; // always-on silver chrome base ring
        [SerializeField] private AvatarRingSdfGraphic _avatarRingGold;   // gold countdown overlay on active turn
        [SerializeField] private Image                _avatarImage;   // AvatarCircleImage — shader-clipped avatar photo
        [SerializeField] private BetDisplay           _betDisplay;    // chip stack + amount on BetAnchor
        [SerializeField] private ActionBadge          _actionBadge;   // neon pill when player acts
        [SerializeField] private SeatActionMenu     _seatActionMenu; // human Check/Fold/Raise above name
        [SerializeField] private RectTransform        _avatarFrame;   // chip animation origin
        [SerializeField] private HudPanelGlowGraphic _hudGlow; // SDF outer bloom; GGPoker-style smooth bright↔invisible pulse on active turn

        [Header("HUD Glow Pulse")]
        [FormerlySerializedAs("_hudGlowMaxIntensity")]
        [SerializeField, HideInInspector] private float _legacyHudGlowMaxIntensity;
        [SerializeField, Range(0f, 1f)] private float _hudGlowMinIntensity = 0f;
        [Tooltip("Seconds to hold at min and at max each cycle (GGPoker-style plateaus).")]
        [SerializeField, Range(0.05f, 0.6f)]  private float _hudGlowHoldSecs   = 0.25f;
        [Tooltip("Seconds for each fade between min and max.")]
        [SerializeField, Range(0.05f, 0.6f)] private float _hudGlowFadeSecs  = 0.25f;
        [SerializeField] private Color        _hudGlowColor                    = Color.white;

        [Header("Gold Ring Urgency")]
        [SerializeField, Range(0.05f, 0.5f)] private float _ringUrgencyWindowFrac = 0.25f;
        [SerializeField] private Color _ringUrgentColorTop = new Color(1.00f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color _ringUrgentColorBot = new Color(0.55f, 0.08f, 0.05f, 1f);

        [Header("HUD Layout")]
        [Tooltip("Mirror HUD band: avatar on the right, name/chips on the left (left table seats).")]
        [SerializeField] private bool _hudMirrored;

        [Header("Equity Display")]
        [SerializeField] private float _equityFontSize = 20f;

        [Header("Testing")]
        [SerializeField] private bool _showAvatarImages = true; // disable to hide all avatar photos during testing
        [SerializeField] private bool _showChromeRing   = true; // disable to hide the chrome base ring during testing


        private bool      _isHuman;
        private int       _revealedHoleCount;
        private Coroutine _ringCountdown;
        private Coroutine _winnerAvatarScaleCoroutine;
        private bool      _winnerAvatarScaleActive;
        private bool      _winnerGlowHeld;
        private bool      _winnerGoldRingActive;
        private Sprite    _currentAvatarSprite; // cached so the toggle can restore it without re-calling SetAvatar

        public bool HudMirrored => _hudMirrored;
        public bool IsHuman     => _isHuman;
        public bool IsWinnerAvatarZoomActive => _winnerAvatarScaleActive;

        /// <summary>Resolved display name for this seat (serialized field, then name label).</summary>
        public string DisplayName => ResolveDisplayName();

        public string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_displayName))
                return _displayName.Trim();

            if (_nameText != null && !string.IsNullOrWhiteSpace(_nameText.text))
            {
                string label = _nameText.text.Trim();
                if (!IsPrefabDefaultName(label))
                    return label;
            }

            return string.Empty;
        }

        /// <summary>True when this seat already has a portrait from the scene or an explicit assignment.</summary>
        public bool HasSeatAvatar()
            => _currentAvatarSprite != null
               || (_avatarImage != null && _avatarImage.sprite != null);

        public void SetDisplayName(string name)
        {
            _displayName = name?.Trim() ?? string.Empty;
            ApplyDisplayNameToHud();
        }

        /// <summary>Writes <see cref="_displayName"/> to the HUD label without changing the serialized name.</summary>
        public void ApplyDisplayNameToHud()
        {
            if (_nameText != null && !string.IsNullOrWhiteSpace(_displayName))
                _nameText.text = _displayName;
        }

        public void SetHudMirrored(bool mirrored)
        {
            if (_hudMirrored == mirrored) return;
            _hudMirrored = mirrored;
            ApplyHudLayout();
        }

        /// <summary>Repositions avatar and name/chips for default or mirrored HUD band.</summary>
        public void ApplyHudLayout()
        {
            EnsureHudGlowRef();
            ApplyHudGlowSettings();
            PlayerHudLayout.Apply(transform, _hudMirrored);
            SyncRingGeometry();
        }

        private void Awake()
        {
            CaptureAvatarFromImage();
            EnsureDisplayNameFromLabel();
            ApplyDisplayNameToHud();
            ApplyNameFontSize();
            ApplyFontSize(_statusText, _statusFontSize);
            EnsureRingRefs();
            EnsureHudGlowRef();
            ApplyHudGlowSettings();
            SyncRingGeometry();
            ApplyChromeRingVisibility();
            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
        }

        private void Start()
        {
            SyncRingGeometry();
            // Fallback when no TableLayoutManager drives seat layout (TableLayoutManager.Start applies all seats).
            if (FindFirstObjectByType<TableLayoutManager>() == null)
                ApplyHudLayout();
        }

        /// <summary>Applies font size and visibility changes immediately when Inspector values are modified.</summary>
        private void OnValidate()
        {
            CaptureAvatarFromImage();
            EnsureDisplayNameFromLabel();
            ApplyDisplayNameToHud();
            ApplyNameFontSize();
            ApplyFontSize(_statusText, _statusFontSize);
            EnsureRingRefs();
            EnsureHudGlowRef();
            ApplyHudGlowSettings();
            SyncRingGeometry();
            ApplyAvatarVisibility();
            ApplyChromeRingVisibility();
            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
            ApplyEquityTextStyle();
            ApplyHudLayout();
        }

        private void EnsureEquityText()
        {
            if (_equityText != null)
                return;

            Transform existing = transform.Find("EquityText");
            if (existing != null)
            {
                _equityText = existing.GetComponent<TMP_Text>();
                ApplyEquityTextStyle();
                return;
            }

            var go = new GameObject("EquityText", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _equityText = go.AddComponent<TextMeshProUGUI>();
            _equityText.raycastTarget = false;
            _equityText.gameObject.SetActive(false);
            ApplyEquityTextStyle();
            ApplyHudLayout();
        }

        private void ApplyEquityTextStyle()
        {
            if (_equityText == null)
                return;

            PlayerHudLayout.ApplyStackAmountFontIfMissing(_equityText);
            if (_nameText != null && _nameText.font != null)
                _equityText.font = _nameText.font;

            _equityText.fontSize           = _equityFontSize;
            _equityText.color              = UiColors.PotGold;
            _equityText.alignment          = TextAlignmentOptions.Midline;
            _equityText.enableWordWrapping = false;
            _equityText.overflowMode       = TextOverflowModes.Overflow;
        }

        private void ApplyNameFontSize()
        {
            if (_nameText == null) return;
            float min = Mathf.Clamp(_nameFontSizeMin, 8f, _nameFontSize);
            _nameText.enableAutoSizing   = true;
            _nameText.fontSize           = _nameFontSize;
            _nameText.fontSizeMin        = min;
            _nameText.fontSizeMax        = _nameFontSize;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode       = TextOverflowModes.Ellipsis;
            _nameText.alignment          = PlayerHudLayout.HudPanelTextAlign;
        }

        /// <summary>Sets a fixed font size on the text element without altering its RectTransform dimensions.</summary>
        private void ApplyFontSize(TMP_Text text, float size)
        {
            if (text == null) return;
            text.enableAutoSizing = false;
            text.fontSize         = size;
            text.alignment        = PlayerHudLayout.HudPanelTextAlign;
        }

        private void EnsureDisplayNameFromLabel()
        {
            if (!string.IsNullOrWhiteSpace(_displayName))
                return;

            if (_nameText == null || string.IsNullOrWhiteSpace(_nameText.text))
                return;

            string label = _nameText.text.Trim();
            if (IsPrefabDefaultName(label))
                return;

            _displayName = label;
        }

        private static bool IsPrefabDefaultName(string label)
            => string.Equals(label, PrefabDefaultName, System.StringComparison.OrdinalIgnoreCase);

        private void CaptureAvatarFromImage()
        {
            if (_currentAvatarSprite != null)
                return;

            if (_avatarImage != null && _avatarImage.sprite != null)
                _currentAvatarSprite = _avatarImage.sprite;
        }

        /// <summary>Returns the RectTransform of the hole-card slot at the given index.</summary>
        public RectTransform GetCardRect(int index)
        {
            if (index < 0 || index >= _cardSlots.Count || _cardSlots[index] == null) return null;
            return (RectTransform)_cardSlots[index].transform;
        }

        /// <summary>Card slot for hole-card index (0 = first card dealt).</summary>
        public CardView GetHoleCardSlot(int index)
        {
            if (index < 0 || index >= _cardSlots.Count)
                return null;

            return _cardSlots[index];
        }

        /// <summary>Shows one hole card face-down in its seat slot.</summary>
        public void PlaceHoleCardFaceDown(int slotIndex)
        {
            GetHoleCardSlot(slotIndex)?.ShowFaceDown();
        }

        /// <summary>Flies one hole card from the table centre to this seat.</summary>
        public IEnumerator AnimateHoleCardDeal(
            int slotIndex,
            Card card,
            bool faceUp,
            RectTransform canvasRt,
            Vector2 dealOriginCanvasPos,
            float flyDuration,
            float smearLength,
            float smearAlpha,
            float smearSpawnInterval,
            float smearFadeDuration)
        {
            CardView slot = GetHoleCardSlot(slotIndex);
            if (slot == null)
                yield break;

            yield return slot.AnimateFlyInOnCanvas(
                canvasRt,
                dealOriginCanvasPos,
                card,
                faceUp,
                flyDuration,
                smearLength,
                smearAlpha,
                smearSpawnInterval,
                smearFadeDuration);

            if (_isHuman && faceUp)
                _revealedHoleCount = Mathf.Max(_revealedHoleCount, slotIndex + 1);
        }

        /// <summary>
        /// BetAnchor in seat local space — chip stack sits here (TableLayoutManager.ApplyBetAnchor).
        /// </summary>
        public RectTransform BetAnchorRect
        {
            get
            {
                // Prefer the anchor that owns BetDisplay (ignore empty scene overrides).
                foreach (Transform child in transform)
                {
                    if (child.name != "BetAnchor")
                        continue;
                    if (child.Find("BetDisplay") != null)
                        return (RectTransform)child;
                }

                Transform fallback = transform.Find("BetAnchor");
                return fallback != null ? (RectTransform)fallback : null;
            }
        }

        /// <summary>Alias for layout gizmos — same as <see cref="BetAnchorRect"/>.</summary>
        public RectTransform BetLabelRect => BetAnchorRect;

        /// <summary>
        /// DealerButtonAnchor in seat local space — dealer token is placed here (TableLayoutManager).
        /// </summary>
        public RectTransform DealerButtonAnchorRect
        {
            get
            {
                Transform t = transform.Find("DealerButtonAnchor");
                return t != null ? (RectTransform)t : null;
            }
        }

        /// <summary>RectTransform of the avatar frame — used as the chip animation origin.</summary>
        public RectTransform AvatarRect =>
            _avatarFrame != null ? _avatarFrame : (RectTransform)transform;

        /// <summary>Chips row in the seat HUD — target for pot chips after a win.</summary>
        public RectTransform ChipsHudRect =>
            _chipsText != null ? _chipsText.transform as RectTransform : null;

        public void SetIsHuman(bool isHuman)
        {
            _isHuman = isHuman;
            if (!isHuman)
                RemoveEquityDisplay();
        }

        /// <summary>Shows Monte Carlo equity and optional pot-odds advice to its right.</summary>
        public void SetEquityDisplay(int equityPercent, string advice = null)
        {
            if (!_isHuman)
                return;

            EnsureEquityText();
            if (_equityText == null)
                return;

            equityPercent = Mathf.Clamp(equityPercent, 0, 100);
            _equityText.text = $"{equityPercent}%";
            _equityText.gameObject.SetActive(true);

            EnsureEquityAdviceText();
            if (_equityAdviceText == null)
                return;

            if (string.IsNullOrEmpty(advice))
            {
                _equityAdviceText.gameObject.SetActive(false);
                return;
            }

            _equityAdviceText.text  = advice;
            _equityAdviceText.color = BettingAdvisor.ColorForLabel(advice);
            _equityAdviceText.gameObject.SetActive(true);
        }

        public void ClearEquityDisplay()
        {
            if (_equityText != null)
                _equityText.gameObject.SetActive(false);

            if (_equityAdviceText != null)
                _equityAdviceText.gameObject.SetActive(false);
        }

        private void EnsureEquityAdviceText()
        {
            if (_equityAdviceText != null)
                return;

            Transform existing = transform.Find("EquityAdviceText");
            if (existing != null)
            {
                _equityAdviceText = existing.GetComponent<TMP_Text>();
                ApplyEquityAdviceTextStyle();
                return;
            }

            var go = new GameObject("EquityAdviceText", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _equityAdviceText = go.AddComponent<TextMeshProUGUI>();
            _equityAdviceText.raycastTarget = false;
            _equityAdviceText.gameObject.SetActive(false);
            ApplyEquityAdviceTextStyle();
            ApplyHudLayout();
        }

        private void ApplyEquityAdviceTextStyle()
        {
            if (_equityAdviceText == null)
                return;

            PlayerHudLayout.ApplyStackAmountFontIfMissing(_equityAdviceText);
            if (_nameText != null && _nameText.font != null)
                _equityAdviceText.font = _nameText.font;

            _equityAdviceText.fontSize           = _equityFontSize;
            _equityAdviceText.alignment          = TextAlignmentOptions.MidlineLeft;
            _equityAdviceText.enableWordWrapping = false;
            _equityAdviceText.overflowMode       = TextOverflowModes.Overflow;
        }

        /// <summary>Removes equity UI from AI seats entirely.</summary>
        public void RemoveEquityDisplay()
        {
            DestroyEquityChild("EquityText", ref _equityText);
            DestroyEquityChild("EquityAdviceText", ref _equityAdviceText);
        }

        private void DestroyEquityChild(string childName, ref TMP_Text field)
        {
            if (field != null)
            {
                if (Application.isPlaying)
                    Destroy(field.gameObject);
                else
                    DestroyImmediate(field.gameObject);

                field = null;
                return;
            }

            Transform existing = transform.Find(childName);
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        /// <summary>
        /// Assigns an avatar sprite to the shader-clipped photo image.
        /// </summary>
        public void SetAvatar(Sprite sprite)
        {
            if (sprite == null)
                return;

            _currentAvatarSprite = sprite;
            ApplyAvatarVisibility();
        }

        /// <summary>Shows or hides the avatar photo based on the testing toggle.</summary>
        private void ApplyAvatarVisibility()
        {
            if (_avatarImage == null) return;
#if UNITY_EDITOR
            // _currentAvatarSprite is null when called from OnValidate (field is non-serialized),
            // but is valid when called via SetAvatar (e.g. from UIManager.ApplySceneModePreview).
            // Apply the sprite when available; keep color white so any Inspector-assigned sprite
            // on the Image component remains visible even when _currentAvatarSprite is null.
            if (!Application.isPlaying)
            {
                bool editorVisible = _showAvatarImages && _currentAvatarSprite != null;
                _avatarImage.sprite = editorVisible ? _currentAvatarSprite : null;
                _avatarImage.color  = _showAvatarImages ? Color.white : Color.clear;
                return;
            }
#endif
            bool visible = _showAvatarImages && _currentAvatarSprite != null;
            _avatarImage.sprite = visible ? _currentAvatarSprite : null;
            _avatarImage.color  = visible ? Color.white : Color.clear;
        }

        /// <summary>Resolves ring refs from AvatarFrame children when the prefab was only partially wired.</summary>
        private void EnsureRingRefs()
        {
            if (_avatarFrame == null) return;
            if (_avatarRingChrome == null)
                _avatarRingChrome = _avatarFrame.Find("AvatarRingChrome")?.GetComponent<AvatarRingSdfGraphic>();
            if (_avatarRingGold == null)
                _avatarRingGold = _avatarFrame.Find("AvatarRingGold")?.GetComponent<AvatarRingSdfGraphic>();
        }

        /// <summary>Keeps the gold overlay on the same annulus as the chrome base ring.</summary>
        private void SyncRingGeometry()
        {
            if (_avatarRingChrome == null || _avatarRingGold == null) return;
            _avatarRingGold.CopyGeometryFrom(_avatarRingChrome);
        }

        /// <summary>Shows or hides the chrome base ring based on the testing toggle.</summary>
        private void ApplyChromeRingVisibility()
        {
            if (_avatarRingChrome == null) return;
            _avatarRingChrome.Look       = AvatarRingSdfGraphic.RingLook.Chrome;
            _avatarRingChrome.FillAmount = 1f;
            _avatarRingChrome.color      = _showChromeRing ? Color.white : Color.clear;
        }

        /// <summary>Hides the gold overlay when this seat is not on an active countdown.</summary>
        private void ApplyGoldRingIdle()
        {
            if (_avatarRingGold == null || _ringCountdown != null || _winnerGoldRingActive)
                return;
            _avatarRingGold.RestoreDefaultGoldColors();
            _avatarRingGold.Look       = AvatarRingSdfGraphic.RingLook.Gold;
            _avatarRingGold.FillAmount = 1f;
            _avatarRingGold.color      = Color.clear;
        }

        /// <summary>Solid gold ring for winner scale — hides chrome, no pulse or countdown.</summary>
        private void ApplyWinnerGoldRingSolid()
        {
            EnsureRingRefs();
            SyncRingGeometry();
            _winnerGoldRingActive = true;

            if (_avatarRingChrome != null)
                _avatarRingChrome.color = Color.clear;

            if (_avatarRingGold != null)
            {
                _avatarRingGold.SetGoldColors(UiColors.PotGold, UiColors.PotGoldDark);
                _avatarRingGold.Look       = AvatarRingSdfGraphic.RingLook.Gold;
                _avatarRingGold.FillAmount = 1f;
                _avatarRingGold.color      = Color.white;
            }
        }

        private void RestoreRingsAfterWin()
        {
            _winnerGoldRingActive = false;
            ApplyChromeRingVisibility();
            ApplyGoldRingIdle();
        }

        private void ApplyGoldRingUrgency(float remaining, float duration)
        {
            if (_avatarRingGold == null || duration <= 0f) return;

            float window  = duration * _ringUrgencyWindowFrac;
            float urgency = window <= 0f ? 0f : 1f - Mathf.Clamp01(remaining / window);
            Color top = Color.Lerp(_avatarRingGold.DefaultGoldColorTop, _ringUrgentColorTop, urgency);
            Color bot = Color.Lerp(_avatarRingGold.DefaultGoldColorBot, _ringUrgentColorBot, urgency);
            _avatarRingGold.SetGoldColors(top, bot);
        }

        private void EnsureHudGlowRef()
        {
            if (_hudGlow != null) return;
            _hudGlow = transform.Find("HudGlow")?.GetComponent<HudPanelGlowGraphic>();
        }

        /// <summary>Pushes shared glow settings and pulse color into the HudGlow graphic.</summary>
        private void ApplyHudGlowSettings()
        {
            if (_hudGlow == null) return;
            PlayerHudLayout.ApplyStandardHudGlow(_hudGlow, _hudGlowColor);
            _legacyHudGlowMaxIntensity = 0f;
        }

        /// <summary>Hides the HUD glow when this seat is not on an active countdown.</summary>
        private void ApplyHudGlowIdle()
        {
            if (_hudGlow == null || _ringCountdown != null || _winnerGlowHeld) return;
#if UNITY_EDITOR
            // Edit mode: keep HudGlow intensity as set in the Inspector (layout/preview).
            if (!Application.isPlaying) return;
#endif
            _hudGlow.GlowIntensity = 0f;
        }

        private float ResolveHudGlowPeak()
        {
            float min = Mathf.Clamp(_hudGlowMinIntensity, 0f, 1.5f);
            float peak = _hudGlow != null ? _hudGlow.PeakGlowIntensity : 0f;
            if (peak < 0.01f) peak = 1.1f;
            return Mathf.Clamp(peak, min + 0.01f, 1.5f);
        }

        private void ApplyHudGlowWinnerHold()
        {
            if (_hudGlow == null)
                return;

            _winnerGlowHeld          = true;
            _hudGlow.GlowIntensity = ResolveHudGlowPeak();
        }

        /// <summary>Clears post-win HUD glow held at peak brightness (e.g. when pot is collected).</summary>
        public void ReleaseWinnerGlowHold()
        {
            if (_ringCountdown != null)
            {
                StopCoroutine(_ringCountdown);
                _ringCountdown = null;
                ApplyGoldRingIdle();
            }

            _winnerGlowHeld = false;

            if (_hudGlow == null)
                return;

            _hudGlow.GlowIntensity = 0f;
        }

        /// <summary>GGPoker-style pulse: hold dark, fade up, hold bright, fade down.</summary>
        private float SampleHudGlowBreath(float elapsed)
        {
            float hold   = _hudGlowHoldSecs;
            float fade   = _hudGlowFadeSecs;
            float period = hold * 2f + fade * 2f;
            if (period <= 0f) return _hudGlowMinIntensity;

            float t = elapsed % period;
            float min = Mathf.Clamp(_hudGlowMinIntensity, 0f, 1.5f);
            float peak = _hudGlow != null ? _hudGlow.PeakGlowIntensity : 0f;
            if (peak < 0.01f) peak = 1.1f;
            float max = Mathf.Clamp(peak, min + 0.01f, 1.5f);

            if (t < hold) return min;
            t -= hold;
            if (t < fade) return Mathf.Lerp(min, max, SmoothStep01(t / fade));
            t -= fade;
            if (t < hold) return max;
            t -= hold;
            return Mathf.Lerp(max, min, SmoothStep01(t / fade));
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>How many hole cards have finished the flip reveal.</summary>
        public int RevealedHoleCount => _revealedHoleCount;

        /// <summary>Clears hole-card reveal tracking (call at the start of a new round).</summary>
        public void ResetHoleCardReveal() => _revealedHoleCount = 0;

        /// <summary>Stops in-flight hole-card flip animations without hiding revealed cards.</summary>
        public void CancelHoleCardFlips()
        {
            foreach (CardView slot in _cardSlots)
                slot?.CancelFlip();
        }

        /// <summary>
        /// Keeps hole cards visible between bet updates — face-up when already revealed,
        /// face-down when the deal flip has not started yet.
        /// </summary>
        /// <param name="holeRevealInProgress">When true, unrevealed slots are left untouched.</param>
        public void SyncHumanHoleCardDisplay(PlayerState player, bool holeRevealInProgress = false)
        {
            if (!_isHuman || player == null)
                return;

            if (player.HasFolded)
            {
                HideAllCardSlots();
                return;
            }

            if (player.HoleCards.Count == 0)
            {
                _revealedHoleCount = 0;
                HideAllCardSlots();
                return;
            }

            for (int i = 0; i < _cardSlots.Count; i++)
            {
                if (i >= player.HoleCards.Count)
                {
                    _cardSlots[i]?.Hide();
                    continue;
                }

                if (i < _revealedHoleCount)
                    _cardSlots[i].Show(player.HoleCards[i]);
                else if (!holeRevealInProgress)
                    _cardSlots[i].ShowFaceDown();
            }
        }

        /// <summary>Hides hole cards and clears reveal state (title screen / full reset).</summary>
        public void ClearTableCards()
        {
            ResetHoleCardReveal();
            HideAllCardSlots();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            if (_statusText != null)
                _statusText.text = string.Empty;
        }

        /// <summary>Updates name, chips, status text, and canvas fade. Bet is handled separately via ShowBetDisplay.</summary>
        public void RefreshHud(PlayerState player, int bigBlind)
        {
            if (_nameText  != null) _nameText.text  = player.Name;
            if (_chipsText != null) _chipsText.text = FormatChipsHud(player.Chips, bigBlind);

            string status = player.Chips == 0 ? "Eliminated"
                                : player.HasFolded  ? "Folded"
                                : player.IsAllIn    ? "All In"
                                :                    "";
            if (_statusText != null) _statusText.text = status;

            if (_canvasGroup != null)
            {
                bool isEliminated = player.Chips == 0 && !player.IsAllIn;
                _canvasGroup.alpha = (player.HasFolded || isEliminated) ? 0.45f : 1f;
            }

            if (player.HasFolded)
                HideAllCardSlots();
        }

        /// <summary>
        /// Shows the bet display with the given amount.
        /// Triggers a chip animation from the avatar when <paramref name="fromRect"/> is provided.
        /// </summary>
        public void ShowBetDisplay(int amount, RectTransform fromRect = null)
        {
            if (_betDisplay != null)
                _betDisplay.ShowBet(amount, fromRect);
        }

        /// <summary>Hides the bet display.</summary>
        public void HideBetDisplay()
        {
            if (_betDisplay != null)
                _betDisplay.HideBet();
        }

        /// <summary>Shows a neon action badge (Check, Fold, Raise, Call, All-In).</summary>
        public void ShowAction(BettingAction action, int amount = 0, float durationSecs = ActionBadge.DisplayDurationSecs)
        {
            ActionBadge badge = ResolveActionBadge();
            if (badge == null)
            {
                Debug.LogWarning("[PlayerView] ActionBadge missing — run Texas Holdem → Apply Player Seat Layout.", this);
                return;
            }

            badge.Show(action, amount, durationSecs);
        }

        /// <summary>Hides the action badge.</summary>
        public void HideActionBadge()
        {
            ResolveActionBadge()?.Hide();
        }

        /// <summary>Keeps a visible action badge above bet chips after HUD refresh.</summary>
        public void BringActionBadgeToFrontIfVisible()
        {
            if (_winnerAvatarScaleActive)
                return;

            ActionBadge badge = ResolveActionBadge();
            if (badge != null && badge.gameObject.activeInHierarchy)
                badge.BringToFront();
        }

        private void ApplyWinnerAvatarDrawOrder()
        {
            PlayerHudLayout.ApplyHudDrawOrder(transform, this);
        }

        private ActionBadge ResolveActionBadge()
        {
            Transform badgeTransform = transform.Find("ActionBadge");
            if (badgeTransform != null)
                _actionBadge = badgeTransform.GetComponent<ActionBadge>();

            if (_actionBadge == null)
                _actionBadge = GetComponentInChildren<ActionBadge>(true);

            return _actionBadge;
        }

        /// <summary>Hides name/chips while the seat action menu replaces that HUD band.</summary>
        public void SetSeatMenuHudMode(bool menuOpen)
        {
            if (_nameText  != null) _nameText.gameObject.SetActive(!menuOpen);
            if (_chipsText != null) _chipsText.gameObject.SetActive(!menuOpen);
        }

        /// <summary>Seat menu for human betting choices above the player name.</summary>
        public SeatActionMenu SeatActionMenu
        {
            get
            {
                if (_seatActionMenu == null)
                    _seatActionMenu = GetComponentInChildren<SeatActionMenu>(true);
                return _seatActionMenu;
            }
        }

        /// <summary>
        /// Keeps the chrome ring visible and animates the gold overlay during an active turn countdown.
        /// Pass isActive=false (or zero duration) to hide the gold overlay.
        /// </summary>
        public void SetActiveTurn(bool isActive, float countdownDuration = 0f)
        {
            if (_ringCountdown != null)
            {
                StopCoroutine(_ringCountdown);
                _ringCountdown = null;
            }

            SyncRingGeometry();
            ApplyChromeRingVisibility();

            if (isActive && countdownDuration > 0f)
            {
                if (_avatarRingGold != null)
                {
                    _avatarRingGold.RestoreDefaultGoldColors();
                    _avatarRingGold.Look       = AvatarRingSdfGraphic.RingLook.Gold;
                    _avatarRingGold.FillAmount = 1f;
                    _avatarRingGold.color      = Color.white;
                }

                if (_hudGlow != null)
                    _hudGlow.GlowIntensity = _hudGlowMinIntensity;

                _ringCountdown = StartCoroutine(RunRingCountdown(countdownDuration));
            }
            else
            {
                ApplyGoldRingIdle();
                ApplyHudGlowIdle();
            }
        }

        /// <summary>Drains the gold ring and softly pulses the HUD glow over the given duration.</summary>
        private IEnumerator RunRingCountdown(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float remaining = duration - elapsed;

                if (_avatarRingGold != null)
                {
                    _avatarRingGold.FillAmount = Mathf.Clamp01(1f - elapsed / duration);
                    ApplyGoldRingUrgency(remaining, duration);
                }

                if (_hudGlow != null)
                    _hudGlow.GlowIntensity = SampleHudGlowBreath(elapsed);

                yield return null;
            }

            _ringCountdown = null;
            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
        }

        /// <summary>Shows face-down hole cards for AI opponents.</summary>
        public void RefreshOpponentCards(PlayerState player)
        {
            if (player.HasFolded)
            {
                HideAllCardSlots();
                return;
            }

            for (int i = 0; i < _cardSlots.Count; i++)
            {
                if (i < player.HoleCards.Count)
                    _cardSlots[i].ShowFaceDown();
                else
                    _cardSlots[i].Hide();
            }
        }

        /// <summary>Flips the human player's hole cards face-up after the preflop deal.</summary>
        public IEnumerator RevealHumanHoleCards(PlayerState player)
        {
            if (!_isHuman)
                yield break;

            if (player.HoleCards.Count == 0)
            {
                _revealedHoleCount = 0;
                HideAllCardSlots();
                yield break;
            }

            float flipGap = UIManager.Instance != null
                ? UIManager.Instance.HoleCardDealStagger
                : 0.06f;

            for (int i = _revealedHoleCount; i < player.HoleCards.Count; i++)
            {
                if (i >= _cardSlots.Count || _cardSlots[i] == null)
                    continue;

                yield return _cardSlots[i].AnimateFlipToFace(player.HoleCards[i]);
                _revealedHoleCount = i + 1;

                if (flipGap > 0f && i < player.HoleCards.Count - 1)
                    yield return new WaitForSecondsRealtime(flipGap);
            }

            for (int i = player.HoleCards.Count; i < _cardSlots.Count; i++)
                _cardSlots[i]?.Hide();
        }

        /// <summary>Flips all hole cards face-up during showdown.</summary>
        public void RevealCards(PlayerState player)
        {
            if (player.HasFolded)
            {
                HideAllCardSlots();
                return;
            }

            for (int i = 0; i < _cardSlots.Count && i < player.HoleCards.Count; i++)
                _cardSlots[i].Show(player.HoleCards[i]);

            _revealedHoleCount = player.HoleCards.Count;
        }

        private void HideAllCardSlots()
        {
            foreach (CardView slot in _cardSlots)
                slot?.Hide();
        }

        public void ApplyWinningCardHighlights(PlayerState player, IReadOnlyList<Card> winningCards)
        {
            if (_cardSlots == null || player?.HoleCards == null)
                return;

            for (int i = 0; i < _cardSlots.Count; i++)
            {
                CardView slot = _cardSlots[i];
                if (slot == null)
                    continue;

                bool highlight = i < player.HoleCards.Count
                                 && WinningHandEvaluation.ContainsCard(winningCards, player.HoleCards[i]);
                slot.SetWinnerHighlight(highlight);
            }
        }

        public void ClearWinningCardHighlights()
        {
            if (_cardSlots == null)
                return;

            foreach (CardView slot in _cardSlots)
                slot?.SetWinnerHighlight(false);
        }

        /// <summary>Shows persistent WIN badge and HUD glow for the winner pause.</summary>
        public void StartWinnerHighlight(int potAmount, float duration)
        {
            if (_ringCountdown != null)
            {
                StopCoroutine(_ringCountdown);
                _ringCountdown = null;
            }

            ResolveActionBadge()?.ShowWinPersistent(potAmount);
            ApplyHudGlowWinnerHold();
            if (_winnerAvatarScaleActive)
                ApplyWinnerAvatarDrawOrder();
        }

        /// <summary>Scales AvatarFrame (photo + chrome/gold rings) up, holds, then returns to normal size.</summary>
        public void StartWinnerAvatarScale(float peakScale, float holdSeconds, float transitionSeconds)
        {
            StopWinnerAvatarScale();
            _winnerAvatarScaleActive = true;
            ApplyWinnerAvatarDrawOrder();
            _winnerAvatarScaleCoroutine = StartCoroutine(
                AnimateWinnerAvatarScale(peakScale, holdSeconds, transitionSeconds));
        }

        public void StopWinnerAvatarScale()
        {
            if (_winnerAvatarScaleCoroutine != null)
            {
                StopCoroutine(_winnerAvatarScaleCoroutine);
                _winnerAvatarScaleCoroutine = null;
            }

            _winnerAvatarScaleActive = false;
            ResetAvatarFrameScale();
            RestoreRingsAfterWin();
        }

        private void ResetAvatarFrameScale()
        {
            RectTransform rt = ResolveAvatarPulseTarget();
            if (rt != null)
                rt.localScale = Vector3.one;
        }

        public IEnumerator AnimateWinnerAvatarScale(float peakScale, float holdSeconds, float transitionSeconds)
        {
            ApplyWinnerGoldRingSolid();
            ApplyWinnerAvatarDrawOrder();

            RectTransform rt = ResolveAvatarPulseTarget();
            if (rt == null)
            {
                _winnerAvatarScaleActive = false;
                RestoreRingsAfterWin();
                yield break;
            }

            Vector3 normal = Vector3.one;
            Vector3 peak   = normal * Mathf.Max(1f, peakScale);
            transitionSeconds = Mathf.Max(0.03f, transitionSeconds);
            holdSeconds       = Mathf.Max(0f, holdSeconds);

            try
            {
                yield return LerpAvatarImageScale(rt, normal, peak, transitionSeconds);
                ApplyWinnerAvatarDrawOrder();

                if (holdSeconds > 0f)
                {
                    float elapsed = 0f;
                    while (elapsed < holdSeconds)
                    {
                        ApplyWinnerAvatarDrawOrder();
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                yield return LerpAvatarImageScale(rt, peak, normal, transitionSeconds);
            }
            finally
            {
                rt.localScale = normal;
                _winnerAvatarScaleActive = false;
                _winnerAvatarScaleCoroutine = null;
                RestoreRingsAfterWin();
                PlayerHudLayout.ApplyHudDrawOrder(transform, this);
            }
        }

        private RectTransform ResolveAvatarPulseTarget()
        {
            if (_avatarFrame != null)
                return _avatarFrame;

            if (_avatarImage != null)
                return _avatarImage.rectTransform;

            return null;
        }

        private static IEnumerator LerpAvatarImageScale(
            RectTransform rt, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rt.localScale = Vector3.Lerp(from, to, t);
                yield return null;
            }

            rt.localScale = to;
        }
    }
}
