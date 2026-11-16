using System;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class DecoOptionsTargetPanel : MonoBehaviour
    {
        ContainerTargetStore store;
        Action<WorkspaceObject, ContainerTarget> onTargetChanged;
        Action<WorkspaceObject> onPromptDismissed;

        RectTransform panelRoot;
        Image targetIcon;
        TextMeshProUGUI targetNameLabel;
        Button clearButton;
        Component popupComponent;
        bool built;

        public void Initialize(
            Component popup,
            ContainerTargetStore targetStore,
            Action<WorkspaceObject, ContainerTarget> targetChangedCallback,
            Action<WorkspaceObject> dismissedCallback)
        {
            popupComponent = popup;
            store = targetStore;
            onTargetChanged = targetChangedCallback;
            onPromptDismissed = dismissedCallback;
            EnsureBuilt();
        }

        public void Refresh()
        {
            if (!built || !panelRoot)
            {
                return;
            }

            var current = FoundationApi.Decorations?.Current(popupComponent);
            bool isContainer = ContainerTargetStore.IsSupportedContainer(current);
            panelRoot.gameObject.SetActive(isContainer);
            if (!isContainer)
            {
                return;
            }

            var target = store != null ? store.GetTarget(current) : ContainerTarget.None;
            if (target.IsAssigned)
            {
                targetIcon.sprite = target.GetIcon();
                targetIcon.enabled = targetIcon.sprite != null;
                targetNameLabel.text = target.GetDisplayName();
                targetNameLabel.color = ContainerUiTheme.InkColor;
                clearButton.interactable = true;
            }
            else
            {
                targetIcon.sprite = FoundationApi.Decorations?.ContainerIcon;
                targetIcon.enabled = targetIcon.sprite != null;
                targetNameLabel.text = "None (Regular Container)";
                targetNameLabel.color = ContainerUiTheme.SecondaryInkColor;
                clearButton.interactable = false;
            }
        }

        void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            var window = FoundationApi.Decorations?.Window(popupComponent);
            if (!window)
            {
                return;
            }

            built = true;

            panelRoot = ContainerUiTheme.CreateRect("ProductionTargetSidePanel", window);
            panelRoot.anchorMin = new Vector2(1f, 1f);
            panelRoot.anchorMax = new Vector2(1f, 1f);
            panelRoot.pivot = new Vector2(0f, 1f);
            panelRoot.anchoredPosition = new Vector2(10f, 0f);
            panelRoot.sizeDelta = new Vector2(258f, 168f);

            var bg = panelRoot.gameObject.AddComponent<Image>();
            bg.color = ContainerUiTheme.PanelColor;
            bg.raycastTarget = true;
            ContainerUiTheme.AddFramedBorder(panelRoot, ContainerUiTheme.BrassColor, raycastTarget: false);

            var title = ContainerUiTheme.CreateText(
                panelRoot,
                "Title",
                "Production Target",
                17f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(14f, -12f);
            titleRect.sizeDelta = new Vector2(-28f, 24f);

            var wellRect = ContainerUiTheme.CreateRect("CurrentTargetWell", panelRoot);
            wellRect.anchorMin = new Vector2(0f, 1f);
            wellRect.anchorMax = new Vector2(1f, 1f);
            wellRect.pivot = new Vector2(0f, 1f);
            wellRect.anchoredPosition = new Vector2(12f, -44f);
            wellRect.sizeDelta = new Vector2(-24f, 44f);

            var wellBg = wellRect.gameObject.AddComponent<Image>();
            wellBg.color = ContainerUiTheme.WellColor;
            wellBg.raycastTarget = false;

            var wellLayout = wellRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            wellLayout.padding = new RectOffset(8, 8, 6, 6);
            wellLayout.spacing = 8f;
            wellLayout.childAlignment = TextAnchor.MiddleLeft;
            wellLayout.childControlWidth = true;
            wellLayout.childControlHeight = true;
            wellLayout.childForceExpandWidth = false;
            wellLayout.childForceExpandHeight = false;

            var iconRect = ContainerUiTheme.CreateRect("TargetIcon", wellRect);
            targetIcon = iconRect.gameObject.AddComponent<Image>();
            targetIcon.preserveAspect = true;
            targetIcon.raycastTarget = false;
            var iconElement = iconRect.gameObject.AddComponent<LayoutElement>();
            iconElement.preferredWidth = 28f;
            iconElement.preferredHeight = 28f;

            targetNameLabel = ContainerUiTheme.CreateText(
                wellRect,
                "TargetName",
                "None",
                15f,
                ContainerUiTheme.SecondaryInkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false);
            targetNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            targetNameLabel.overflowMode = TextOverflowModes.Ellipsis;
            var nameElement = targetNameLabel.gameObject.AddComponent<LayoutElement>();
            nameElement.flexibleWidth = 1f;
            nameElement.preferredHeight = 28f;

            var selectBtn = ContainerUiTheme.CreateButton(
                panelRoot,
                "SelectTargetButton",
                "Select / Change...",
                15f,
                OnSelectClicked);
            var selectRect = selectBtn.GetComponent<RectTransform>();
            selectRect.anchorMin = new Vector2(0f, 0f);
            selectRect.anchorMax = new Vector2(1f, 0f);
            selectRect.pivot = new Vector2(0f, 0f);
            selectRect.anchoredPosition = new Vector2(12f, 48f);
            selectRect.sizeDelta = new Vector2(-24f, 32f);

            clearButton = ContainerUiTheme.CreateButton(
                panelRoot,
                "ClearTargetButton",
                "Clear Target",
                14f,
                OnClearClicked);
            var clearRect = clearButton.GetComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(0f, 0f);
            clearRect.anchorMax = new Vector2(1f, 0f);
            clearRect.pivot = new Vector2(0f, 0f);
            clearRect.anchoredPosition = new Vector2(12f, 12f);
            clearRect.sizeDelta = new Vector2(-24f, 30f);
        }

        void OnSelectClicked()
        {
            var current = FoundationApi.Decorations?.Current(popupComponent);
            if (!ContainerTargetStore.IsSupportedContainer(current))
            {
                return;
            }

            var selector = TargetSelectorPopup.GetOrCreate();
            if (!selector)
            {
                return;
            }

            var existing = store != null ? store.GetTarget(current) : ContainerTarget.None;
            selector.OpenFor(current, existing, onTargetChanged, onPromptDismissed);
        }

        void OnClearClicked()
        {
            var current = FoundationApi.Decorations?.Current(popupComponent);
            if (!ContainerTargetStore.IsSupportedContainer(current))
            {
                return;
            }

            onTargetChanged?.Invoke(current, ContainerTarget.None);
            Refresh();
        }
    }
}
