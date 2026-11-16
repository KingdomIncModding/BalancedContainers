using System;
using System.Globalization;
using System.Linq;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalancedContainers
{
    public static class ContainerUiTheme
    {
        public static readonly Color PanelColor = new Color(38f / 255f, 26f / 255f, 35f / 255f, 0.97f);
        public static readonly Color HeaderBgColor = new Color(24f / 255f, 17f / 255f, 23f / 255f, 0.90f);
        public static readonly Color TargetPillColor = new Color(76f / 255f, 54f / 255f, 31f / 255f, 0.95f);
        public static readonly Color WellColor = new Color(26f / 255f, 18f / 255f, 26f / 255f, 1f);
        public static readonly Color StatusWellColor = new Color(45f / 255f, 24f / 255f, 11f / 255f, 1f);
        public static readonly Color InkColor = new Color(234f / 255f, 223f / 255f, 203f / 255f, 1f);
        public static readonly Color SecondaryInkColor = new Color(205f / 255f, 190f / 255f, 161f / 255f, 1f);
        public static readonly Color MutedColor = new Color(156f / 255f, 143f / 255f, 128f / 255f, 1f);
        public static readonly Color BrassColor = new Color(174f / 255f, 130f / 255f, 73f / 255f, 1f);
        public static readonly Color GoodRateColor = new Color(172f / 255f, 212f / 255f, 140f / 255f, 1f);
        public static readonly Color WarnRateColor = new Color(226f / 255f, 189f / 255f, 119f / 255f, 1f);
        public static readonly Color LowRateColor = new Color(228f / 255f, 154f / 255f, 136f / 255f, 1f);
        public static readonly Color OversaturatedColor = new Color(196f / 255f, 134f / 255f, 238f / 255f, 1f);
        public static readonly Color ButtonIdleColor = new Color(68f / 255f, 51f / 255f, 44f / 255f, 1f);
        public static readonly Color ButtonSelectedColor = new Color(118f / 255f, 86f / 255f, 46f / 255f, 1f);
        public static readonly Color RowColor = new Color(46f / 255f, 33f / 255f, 42f / 255f, 0.96f);
        public static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.55f);

        static TMP_FontAsset cachedFont;
        static TMP_FontAsset cachedCleanFont;
        static Sprite cachedFrameSprite;
        static Sprite cachedButtonSprite;
        static Image.Type cachedButtonType = Image.Type.Sliced;
        static float cachedButtonPpu = 1f;

        public static void InvalidateCache()
        {
            cachedFont = null;
            cachedCleanFont = null;
            cachedFrameSprite = null;
            cachedButtonSprite = null;
        }

        public static TMP_FontAsset ResolveFont()
        {
            if (cachedFont)
            {
                return cachedFont;
            }

            var decorations = FoundationApi.Decorations;
            if (decorations?.Popup)
            {
                cachedFont = decorations.Font(decorations.Popup);
                if (cachedFont)
                {
                    return cachedFont;
                }
            }

            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            cachedFont = fonts.FirstOrDefault(f => f && f.name.Contains("LiberationSans"))
                ?? fonts.FirstOrDefault(f => f)
                ?? TMP_Settings.defaultFontAsset;
            return cachedFont;
        }

        public static TMP_FontAsset ResolveCleanFont()
        {
            if (cachedCleanFont)
            {
                return cachedCleanFont;
            }

            cachedCleanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                ?? Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f && f.name.Contains("LiberationSans"))
                ?? TMP_Settings.defaultFontAsset
                ?? ResolveFont();
            return cachedCleanFont;
        }

        public static string TranslateName(string raw, string fallbackId = null)
        {
            var presentation = FoundationApi.Presentation;
            string candidate = raw;
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                if (presentation != null && candidate.StartsWith("@", StringComparison.Ordinal))
                {
                    string translated = presentation.Translate(candidate);
                    if (!string.IsNullOrWhiteSpace(translated) && !translated.StartsWith("@", StringComparison.Ordinal))
                    {
                        return translated;
                    }
                }
                else if (!candidate.StartsWith("@", StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            if (presentation != null && !string.IsNullOrWhiteSpace(fallbackId))
            {
                string key = fallbackId.StartsWith("@", StringComparison.Ordinal) ? fallbackId : "@" + fallbackId;
                string translated = presentation.Translate(key);
                if (!string.IsNullOrWhiteSpace(translated) && !translated.StartsWith("@", StringComparison.Ordinal))
                {
                    return translated;
                }
            }

            string rawFallback = !string.IsNullOrWhiteSpace(candidate) ? candidate : fallbackId;
            if (string.IsNullOrWhiteSpace(rawFallback))
            {
                return "Unknown";
            }

            string clean = rawFallback.Trim().TrimStart('@').Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean);
        }

        public static Sprite FindFrameSprite()
        {
            if (cachedFrameSprite)
            {
                return cachedFrameSprite;
            }

            cachedFrameSprite = FoundationApi.Presentation?.FindSprite("DefaultBorderDark")
                ?? Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s && s.name == "DefaultBorderDark");
            return cachedFrameSprite;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (parent)
            {
                rect.SetParent(parent, worldPositionStays: false);
            }

            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget = true)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static void AddFramedBorder(Transform parent, Color? tint = null, bool raycastTarget = false)
        {
            var border = FindFrameSprite();
            if (!border)
            {
                return;
            }

            var frameImage = CreateImage("Frame", parent, tint ?? BrassColor, raycastTarget);
            frameImage.sprite = border;
            frameImage.type = Image.Type.Sliced;
            var rect = frameImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool raycastTarget = false,
            bool cleanFont = false)
        {
            var rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = cleanFont ? ResolveCleanFont() : ResolveFont();
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.text = content ?? string.Empty;
            label.raycastTarget = raycastTarget;
            return label;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Action onClick)
        {
            EnsureButtonTemplate();

            var rect = CreateRect(name, parent);
            var bg = rect.gameObject.AddComponent<Image>();
            if (cachedButtonSprite)
            {
                bg.sprite = cachedButtonSprite;
                bg.type = cachedButtonType;
                bg.pixelsPerUnitMultiplier = cachedButtonPpu;
            }

            bg.color = ButtonIdleColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.12f, 1.05f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
            button.colors = colors;

            button.onClick.AddListener(() =>
            {
                if (EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                FoundationApi.UiAccess?.PlaySound(GameUiSound.Button);
                onClick?.Invoke();
            });

            var label = CreateText(rect, "Label", text, fontSize, InkColor, TextAlignmentOptions.Center, raycastTarget: false);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);

            return button;
        }

        public static bool IsGoldCurrency(ContainerTargetKind kind, string id)
        {
            return kind == ContainerTargetKind.Currency
                && string.Equals(id, nameof(GameCurrency.Gold), StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatResourceRate(ContainerTargetKind kind, string id, double perSecond, bool perMinute)
        {
            if (!IsGoldCurrency(kind, id))
            {
                return FormatRate(perSecond, perMinute);
            }

            double copper = perMinute ? perSecond * 60.0d : perSecond;
            if (copper < 0.005d)
            {
                return "0c";
            }

            if (copper >= 10_000d)
            {
                return FormatCompactNumber(copper / 10_000d) + "g";
            }

            if (copper >= 100d)
            {
                return FormatCompactNumber(copper / 100d) + "s";
            }

            return FormatCompactNumber(copper) + "c";
        }

        public static string FormatRate(double perSecond, bool perMinute)
        {
            double value = perMinute ? perSecond * 60.0d : perSecond;
            return FormatCompactNumber(value);
        }

        static string FormatCompactNumber(double value)
        {
            if (value < 0.005d)
            {
                return "0";
            }

            if (value >= 1_000_000_000d)
            {
                return (value / 1_000_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "B";
            }

            if (value >= 1_000_000d)
            {
                return (value / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            }

            if (value >= 1_000d)
            {
                return (value / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            }

            if (value >= 100d)
            {
                return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
            }

            if (value >= 10d)
            {
                return value.ToString("0.#", CultureInfo.InvariantCulture);
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string ColorToHex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        public static Color ResolveCurrentColor(double current, double potential)
        {
            if (potential <= 0.0001d)
            {
                return current > 0.005d ? GoodRateColor : SecondaryInkColor;
            }

            double ratio = current / potential;
            if (ratio >= 0.90d)
            {
                return GoodRateColor;
            }

            if (current > 0.005d)
            {
                return WarnRateColor;
            }

            return LowRateColor;
        }

        static void EnsureButtonTemplate()
        {
            if (cachedButtonSprite)
            {
                return;
            }

            var template = FoundationApi.UiAccess?.CloneButtonTemplate;
            if (!template)
            {
                return;
            }

            var img = template.GetComponent<Image>();
            if (img && img.sprite)
            {
                cachedButtonSprite = img.sprite;
                cachedButtonType = img.type;
                cachedButtonPpu = img.pixelsPerUnitMultiplier;
            }
        }
    }
}

