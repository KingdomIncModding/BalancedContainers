using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class ContainerHoverRowView
    {
        static readonly Color ProducerColor = new Color(26f / 255f, 18f / 255f, 26f / 255f, 0.58f);
        static readonly Color InputSummaryColor = new Color(48f / 255f, 28f / 255f, 34f / 255f, 0.78f);
        static readonly Color OutputSummaryColor = new Color(36f / 255f, 44f / 255f, 32f / 255f, 0.80f);
        static readonly Color HoverRowColor = new Color(110f / 255f, 80f / 255f, 44f / 255f, 0.94f);

        public GameObject Root { get; }
        public RectTransform RowRect { get; }
        public bool IsInteractive { get; private set; }

        readonly Image rowBackground;
        readonly Button rowButton;
        readonly Image iconImage;
        readonly TextMeshProUGUI nameText;
        readonly TextMeshProUGUI dividerText;
        readonly TextMeshProUGUI rateText;

        BuildingProductionEntry boundEntry;
        Color idleColor;
        Action<BuildingProductionEntry> clickHandler;

        public ContainerHoverRowView(Transform parent)
        {
            RowRect = ContainerUiTheme.CreateRect("HoverRow", parent);
            Root = RowRect.gameObject;

            rowBackground = RowRect.gameObject.AddComponent<Image>();
            rowBackground.raycastTarget = true;

            rowButton = RowRect.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowBackground;
            rowButton.transition = Selectable.Transition.None;
            rowButton.navigation = new Navigation { mode = Navigation.Mode.None };
            rowButton.onClick.AddListener(HandleClick);

            var layout = RowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rowElem = RowRect.gameObject.AddComponent<LayoutElement>();
            rowElem.preferredHeight = 24f;

            var iconRect = ContainerUiTheme.CreateRect("Icon", RowRect);
            iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            var iconElem = iconRect.gameObject.AddComponent<LayoutElement>();
            iconElem.preferredWidth = 19f;
            iconElem.preferredHeight = 19f;

            nameText = ContainerUiTheme.CreateText(
                RowRect,
                "Label",
                string.Empty,
                13.5f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            var nameElem = nameText.gameObject.AddComponent<LayoutElement>();
            nameElem.preferredWidth = 210f;
            nameElem.flexibleWidth = 1f;
            nameElem.preferredHeight = 20f;

            dividerText = ContainerUiTheme.CreateText(
                RowRect,
                "Pipe",
                "|",
                13f,
                ContainerUiTheme.MutedColor,
                TextAlignmentOptions.Center,
                raycastTarget: false,
                cleanFont: true);
            var divElem = dividerText.gameObject.AddComponent<LayoutElement>();
            divElem.preferredWidth = 10f;
            divElem.preferredHeight = 20f;

            rateText = ContainerUiTheme.CreateText(
                RowRect,
                "Rates",
                string.Empty,
                13.5f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Right,
                raycastTarget: false,
                cleanFont: true);
            rateText.textWrappingMode = TextWrappingModes.NoWrap;
            var rateElem = rateText.gameObject.AddComponent<LayoutElement>();
            rateElem.preferredWidth = 148f;
            rateElem.preferredHeight = 20f;
        }

        public void BindBuilding(
            BuildingProductionEntry entry,
            bool perMinute,
            bool highlighted,
            bool interactive,
            Action<BuildingProductionEntry> onClicked)
        {
            boundEntry = entry;
            clickHandler = onClicked;
            IsInteractive = interactive;

            string stack = entry.StackSize > 1 ? $" x{entry.StackSize}" : string.Empty;
            Color bgColor = highlighted ? ContainerUiTheme.TargetPillColor : ProducerColor;

            BindVisuals(
                entry.ResourceKind,
                entry.ResourceId,
                entry.BuildingIcon,
                $"{entry.BuildingName}{stack}",
                entry.CurrentPerSecond,
                entry.PotentialPerSecond,
                perMinute,
                highlighted,
                isInput: entry.IsConsumer,
                interactive,
                bgColor);
        }

        public void BindSummary(
            ContainerTargetKind kind,
            string id,
            Sprite icon,
            string label,
            double currentPerSecond,
            double potentialPerSecond,
            bool perMinute,
            bool isInput)
        {
            boundEntry = null;
            clickHandler = null;
            IsInteractive = false;

            Color bgColor = isInput ? InputSummaryColor : OutputSummaryColor;
            BindVisuals(
                kind,
                id,
                icon,
                label,
                currentPerSecond,
                potentialPerSecond,
                perMinute,
                highlighted: !isInput,
                isInput,
                interactive: false,
                bgColor);
        }

        public void SetHovered(bool hovered)
        {
            if (!IsInteractive || !Root.activeSelf)
            {
                return;
            }

            rowBackground.color = hovered ? HoverRowColor : idleColor;
        }

        public void Hide()
        {
            boundEntry = null;
            clickHandler = null;
            IsInteractive = false;
            Root.SetActive(false);
        }

        void BindVisuals(
            ContainerTargetKind resourceKind,
            string resourceId,
            Sprite icon,
            string label,
            double currentPerSecond,
            double potentialPerSecond,
            bool perMinute,
            bool highlighted,
            bool isInput,
            bool interactive,
            Color backgroundColor)
        {
            Root.SetActive(true);
            idleColor = backgroundColor;
            rowBackground.color = idleColor;
            rowBackground.raycastTarget = interactive;
            rowButton.interactable = interactive;

            iconImage.enabled = icon != null;
            iconImage.sprite = icon;
            nameText.text = label ?? string.Empty;

            string curText = ContainerUiTheme.FormatResourceRate(resourceKind, resourceId, currentPerSecond, perMinute);
            string potText = ContainerUiTheme.FormatResourceRate(resourceKind, resourceId, potentialPerSecond, perMinute);
            string unit = perMinute ? "/m" : "/s";

            Color curColor = isInput
                ? new Color(217f / 255f, 165f / 255f, 138f / 255f, 1f)
                : ContainerUiTheme.ResolveCurrentColor(currentPerSecond, potentialPerSecond);
            string curHex = ContainerUiTheme.ColorToHex(curColor);
            string potHex = ContainerUiTheme.ColorToHex(
                highlighted ? ContainerUiTheme.InkColor : ContainerUiTheme.SecondaryInkColor);

            rateText.text = $"<color={curHex}>{curText}</color> <color=#a59988>/</color> <color={potHex}>{potText}{unit}</color>";
        }

        void HandleClick()
        {
            if (boundEntry != null && IsInteractive)
            {
                clickHandler?.Invoke(boundEntry);
            }
        }
    }
}
