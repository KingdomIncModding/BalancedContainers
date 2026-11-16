using System;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class ContainerHeaderEntryView
    {
        static readonly Color HoverPillColor = new Color(110f / 255f, 80f / 255f, 44f / 255f, 0.96f);
        static readonly Color PinnedPillColor = new Color(148f / 255f, 108f / 255f, 44f / 255f, 0.98f);

        public GameObject Root { get; }
        public RectTransform PillRect => background.rectTransform;
        public ResourceProductionEntry BoundEntry { get; private set; }

        readonly Image background;
        readonly Button pillButton;
        readonly Image icon;
        readonly TextMeshProUGUI rateText;
        readonly GameObject separator;

        Action<ResourceProductionEntry> clickHandler;

        public ContainerHeaderEntryView(Transform parent, bool withSeparator)
        {
            Root = ContainerUiTheme.CreateRect("Entry", parent).gameObject;
            var rootLayout = Root.AddComponent<HorizontalLayoutGroup>();
            rootLayout.childAlignment = TextAnchor.MiddleLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = false;
            rootLayout.spacing = 6f;

            if (withSeparator)
            {
                var sepText = ContainerUiTheme.CreateText(
                    Root.transform,
                    "Separator",
                    "|",
                    16f,
                    ContainerUiTheme.MutedColor,
                    TextAlignmentOptions.Center,
                    raycastTarget: false);
                var sepElement = sepText.gameObject.AddComponent<LayoutElement>();
                sepElement.preferredWidth = 8f;
                sepElement.preferredHeight = 24f;
                separator = sepText.gameObject;
            }

            var pillRect = ContainerUiTheme.CreateRect("Pill", Root.transform);
            background = pillRect.gameObject.AddComponent<Image>();
            background.raycastTarget = true;

            pillButton = pillRect.gameObject.AddComponent<Button>();
            pillButton.targetGraphic = background;
            pillButton.transition = Selectable.Transition.None;
            pillButton.navigation = new Navigation { mode = Navigation.Mode.None };
            pillButton.onClick.AddListener(HandleClick);

            var pillLayout = pillRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            pillLayout.padding = new RectOffset(6, 7, 3, 3);
            pillLayout.spacing = 4f;
            pillLayout.childAlignment = TextAnchor.MiddleLeft;
            pillLayout.childControlWidth = true;
            pillLayout.childControlHeight = true;
            pillLayout.childForceExpandWidth = false;
            pillLayout.childForceExpandHeight = false;

            var iconRect = ContainerUiTheme.CreateRect("Icon", pillRect);
            icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconElement = iconRect.gameObject.AddComponent<LayoutElement>();
            iconElement.preferredWidth = 22f;
            iconElement.preferredHeight = 22f;

            rateText = ContainerUiTheme.CreateText(
                pillRect,
                "Rates",
                string.Empty,
                16f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false);
            rateText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        public void Bind(
            ResourceProductionEntry entry,
            bool perMinute,
            bool showSeparator,
            Action<ResourceProductionEntry> onClicked,
            bool hovered = false,
            bool pinned = false)
        {
            BoundEntry = entry;
            clickHandler = onClicked;
            Root.SetActive(true);
            if (separator)
            {
                separator.SetActive(showSeparator);
            }

            SetVisualState(hovered, pinned);

            icon.enabled = entry.Icon != null;
            icon.sprite = entry.Icon;

            string curText = ContainerUiTheme.FormatResourceRate(entry.Kind, entry.Id, entry.CurrentPerSecond, perMinute);
            string potText = ContainerUiTheme.FormatResourceRate(entry.Kind, entry.Id, entry.PotentialPerSecond, perMinute);
            string unit = perMinute ? "/m" : "/s";

            Color curColor = ResolveStatusRateColor(entry);
            string curHex = ContainerUiTheme.ColorToHex(curColor);
            string potHex = ContainerUiTheme.ColorToHex(
                entry.IsPrimaryTarget ? ContainerUiTheme.InkColor : ContainerUiTheme.SecondaryInkColor);

            rateText.text = $"<color={curHex}>{curText}</color> <color=#a59988>/</color> <color={potHex}>({potText}){unit}</color>";
        }

        public void SetVisualState(bool hovered, bool pinned)
        {
            if (BoundEntry == null)
            {
                return;
            }

            if (pinned)
            {
                background.color = PinnedPillColor;
            }
            else if (hovered)
            {
                background.color = HoverPillColor;
            }
            else
            {
                background.color = BoundEntry.IsPrimaryTarget
                    ? ContainerUiTheme.TargetPillColor
                    : new Color(0f, 0f, 0f, 0f);
            }
        }

        public void Hide()
        {
            BoundEntry = null;
            clickHandler = null;
            Root.SetActive(false);
        }

        void HandleClick()
        {
            if (BoundEntry == null)
            {
                return;
            }

            var ui = FoundationApi.UiAccess;
            if (ui != null && ui.PointerTraverseDistance > 25f)
            {
                return;
            }

            clickHandler?.Invoke(BoundEntry);
        }

        static Color ResolveStatusRateColor(ResourceProductionEntry entry)
        {
            switch (entry.Status)
            {
                case ResourceDiagnosticState.Operational:
                    return ContainerUiTheme.GoodRateColor;
                case ResourceDiagnosticState.Saturated:
                    return ContainerUiTheme.WarnRateColor;
                case ResourceDiagnosticState.Oversaturated:
                    return ContainerUiTheme.OversaturatedColor;
                case ResourceDiagnosticState.Starved:
                    return ContainerUiTheme.LowRateColor;
                default:
                    return ContainerUiTheme.MutedColor;
            }
        }
    }
}
