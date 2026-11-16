using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class TargetOptionRow
    {
        public GameObject Root { get; }

        readonly Image background;
        readonly Image highlightBorder;
        readonly Image iconImage;
        readonly TextMeshProUGUI nameText;
        readonly TextMeshProUGUI typeBadge;
        readonly Button button;

        TargetCatalogEntry boundEntry;

        public TargetOptionRow(Transform parent, Action<TargetCatalogEntry> onSelected)
        {
            var rect = ContainerUiTheme.CreateRect("OptionRow", parent);
            Root = rect.gameObject;

            background = rect.gameObject.AddComponent<Image>();
            background.color = ContainerUiTheme.RowColor;

            button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.16f, 1.05f, 1f);
            colors.pressedColor = new Color(0.90f, 0.85f, 0.78f, 1f);
            button.colors = colors;

            button.onClick.AddListener(() =>
            {
                if (EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                if (boundEntry != null)
                {
                    onSelected?.Invoke(boundEntry);
                }
            });

            var borderImg = ContainerUiTheme.CreateImage("SelectedBorder", rect, ContainerUiTheme.BrassColor, raycastTarget: false);
            var borderSprite = ContainerUiTheme.FindFrameSprite();
            if (borderSprite)
            {
                borderImg.sprite = borderSprite;
                borderImg.type = Image.Type.Sliced;
            }

            var borderRect = borderImg.rectTransform;
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            highlightBorder = borderImg;

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconRect = ContainerUiTheme.CreateRect("Icon", rect);
            iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            var iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 28f;
            iconLayout.preferredHeight = 28f;

            nameText = ContainerUiTheme.CreateText(
                rect,
                "Name",
                string.Empty,
                16f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            var nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;
            nameLayout.preferredHeight = 28f;

            typeBadge = ContainerUiTheme.CreateText(
                rect,
                "Kind",
                string.Empty,
                12f,
                ContainerUiTheme.MutedColor,
                TextAlignmentOptions.Right,
                raycastTarget: false);
            typeBadge.textWrappingMode = TextWrappingModes.NoWrap;
            var badgeLayout = typeBadge.gameObject.AddComponent<LayoutElement>();
            badgeLayout.preferredWidth = 62f;
            badgeLayout.preferredHeight = 24f;
        }

        public void Bind(TargetCatalogEntry entry, bool isSelected)
        {
            boundEntry = entry;
            Root.SetActive(true);

            iconImage.enabled = entry.Icon != null;
            iconImage.sprite = entry.Icon;
            nameText.text = entry.DisplayName;
            typeBadge.text = entry.IsCurrency ? "Currency" : "Product";
            typeBadge.color = entry.IsCurrency ? ContainerUiTheme.WarnRateColor : ContainerUiTheme.MutedColor;

            background.color = isSelected ? ContainerUiTheme.TargetPillColor : ContainerUiTheme.RowColor;
            highlightBorder.enabled = isSelected;
        }

        public void Hide()
        {
            boundEntry = null;
            Root.SetActive(false);
        }
    }
}
