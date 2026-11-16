using System;
using System.Collections.Generic;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class TargetSelectorPopup : FoundationPopup, IPointerEnterHandler, IPointerExitHandler
    {
        static TargetSelectorPopup instance;

        readonly List<TargetCatalogEntry> catalog = new List<TargetCatalogEntry>();
        readonly List<TargetOptionRow> rowPool = new List<TargetOptionRow>();

        WorkspaceObject activeContainer;
        ContainerTarget currentTarget = ContainerTarget.None;
        Action<WorkspaceObject, ContainerTarget> onSelected;
        Action<WorkspaceObject> onDismissed;

        TargetFilterTab activeTab = TargetFilterTab.All;
        TMP_InputField searchInput;
        Button allTabBtn;
        Button productTabBtn;
        Button currencyTabBtn;
        RectTransform gridContent;
        ScrollRect scrollRect;
        TextMeshProUGUI emptyLabel;
        TextMeshProUGUI subtitleLabel;

        bool uiBuilt;
        bool inputDisabledByPopup;

        public override bool CanCancel => true;
        public override bool CanEscape => true;

        public static TargetSelectorPopup GetOrCreate()
        {
            if (instance)
            {
                return instance;
            }

            var uiRoot = FoundationApi.UiAccess?.Root;
            if (!uiRoot)
            {
                return null;
            }

            var go = new GameObject("BalancedContainers_TargetSelectorPopup", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(uiRoot.transform, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            instance = go.AddComponent<TargetSelectorPopup>();
            go.SetActive(false);
            return instance;
        }

        public static void DestroyIfCreated()
        {
            if (instance)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        public void OpenFor(
            WorkspaceObject container,
            ContainerTarget existingTarget,
            Action<WorkspaceObject, ContainerTarget> selectedCallback,
            Action<WorkspaceObject> dismissedCallback,
            bool playSound = true)
        {
            if (container == null)
            {
                return;
            }

            EnsureBuilt();

            activeContainer = container;
            currentTarget = existingTarget ?? ContainerTarget.None;
            onSelected = selectedCallback;
            onDismissed = dismissedCallback;

            activeTab = TargetFilterTab.All;
            if (searchInput)
            {
                searchInput.SetTextWithoutNotify(string.Empty);
            }

            RebuildCatalog();
            UpdateSubtitle();
            UpdateTabVisuals();
            RebuildRows();

            if (scrollRect)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }

            transform.SetAsLastSibling();
            Show(true, playSound: playSound);
        }

        public override void PopupClosed()
        {
            ReleaseInputLock();

            var container = activeContainer;
            var dismissCallback = onDismissed;
            activeContainer = null;
            onSelected = null;
            onDismissed = null;

            if (container != null)
            {
                dismissCallback?.Invoke(container);
            }

            base.PopupClosed();
        }

        protected override void OnDestroy()
        {
            ReleaseInputLock();
            if (ReferenceEquals(instance, this))
            {
                instance = null;
            }

            base.OnDestroy();
        }

        void EnsureBuilt()
        {
            if (uiBuilt)
            {
                return;
            }

            uiBuilt = true;
            FoundationApi.NativeUiFactory?.BlockWorkspaceZoom(gameObject);

            var scrim = gameObject.AddComponent<Image>();
            scrim.color = ContainerUiTheme.ScrimColor;
            var scrimButton = gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.onClick.AddListener(CancelSelection);

            var window = ContainerUiTheme.CreateRect("Window", transform);
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(640f, 560f);

            var windowBg = window.gameObject.AddComponent<Image>();
            windowBg.color = ContainerUiTheme.PanelColor;
            windowBg.raycastTarget = true;
            ContainerUiTheme.AddFramedBorder(window, ContainerUiTheme.BrassColor, raycastTarget: false);

            BuildHeader(window);
            BuildFilterBar(window);
            BuildScrollList(window);
            BuildFooter(window);
        }

        void BuildHeader(RectTransform window)
        {
            var title = ContainerUiTheme.CreateText(
                window,
                "Title",
                "Select End Product / Currency",
                22f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(18f, -14f);
            titleRect.sizeDelta = new Vector2(-80f, 30f);

            subtitleLabel = ContainerUiTheme.CreateText(
                window,
                "Subtitle",
                string.Empty,
                14f,
                ContainerUiTheme.SecondaryInkColor,
                TextAlignmentOptions.Left);
            var subRect = subtitleLabel.rectTransform;
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0f, 1f);
            subRect.anchoredPosition = new Vector2(18f, -42f);
            subRect.sizeDelta = new Vector2(-80f, 22f);

            var closeBtn = ContainerUiTheme.CreateButton(window, "CloseButton", "X", 16f, CancelSelection);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-14f, -14f);
            closeRect.sizeDelta = new Vector2(34f, 32f);
        }

        void BuildFilterBar(RectTransform window)
        {
            allTabBtn = CreateTabButton(window, "AllTab", "All", new Vector2(18f, -72f), 84f, TargetFilterTab.All);
            productTabBtn = CreateTabButton(window, "ProductTab", "Products", new Vector2(108f, -72f), 108f, TargetFilterTab.Products);
            currencyTabBtn = CreateTabButton(window, "CurrencyTab", "Currencies", new Vector2(222f, -72f), 118f, TargetFilterTab.Currencies);

            var inputRect = ContainerUiTheme.CreateRect("SearchInput", window);
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(0f, 1f);
            inputRect.anchoredPosition = new Vector2(350f, -72f);
            inputRect.sizeDelta = new Vector2(-368f, 32f);

            var inputBg = inputRect.gameObject.AddComponent<Image>();
            inputBg.color = ContainerUiTheme.WellColor;
            ContainerUiTheme.AddFramedBorder(inputRect, ContainerUiTheme.MutedColor, raycastTarget: false);

            var viewport = ContainerUiTheme.CreateRect("Viewport", inputRect);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 3f);
            viewport.offsetMax = new Vector2(-8f, -3f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var placeholder = ContainerUiTheme.CreateText(
                viewport,
                "Placeholder",
                "Search item or currency...",
                14f,
                ContainerUiTheme.MutedColor,
                TextAlignmentOptions.Left,
                raycastTarget: false);
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            StretchFull(placeholder.rectTransform);

            var inputText = ContainerUiTheme.CreateText(
                viewport,
                "Text",
                string.Empty,
                15f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false);
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            StretchFull(inputText.rectTransform);

            searchInput = inputRect.gameObject.AddComponent<TMP_InputField>();
            searchInput.textViewport = viewport;
            searchInput.textComponent = inputText;
            searchInput.placeholder = placeholder;
            searchInput.caretColor = ContainerUiTheme.InkColor;
            searchInput.selectionColor = new Color(174f / 255f, 130f / 255f, 73f / 255f, 0.35f);
            searchInput.onValueChanged.AddListener(_ => RebuildRows());
            searchInput.onSelect.AddListener(_ => LockInput(true));
            searchInput.onDeselect.AddListener(_ => LockInput(false));
        }

        void BuildScrollList(RectTransform window)
        {
            var scrollContainer = ContainerUiTheme.CreateRect("ListWell", window);
            scrollContainer.anchorMin = Vector2.zero;
            scrollContainer.anchorMax = Vector2.one;
            scrollContainer.offsetMin = new Vector2(18f, 62f);
            scrollContainer.offsetMax = new Vector2(-18f, -114f);

            var wellBg = scrollContainer.gameObject.AddComponent<Image>();
            wellBg.color = ContainerUiTheme.WellColor;
            ContainerUiTheme.AddFramedBorder(scrollContainer, ContainerUiTheme.MutedColor, raycastTarget: false);

            var viewport = ContainerUiTheme.CreateRect("Viewport", scrollContainer);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(6f, 6f);
            viewport.offsetMax = new Vector2(-6f, -6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            gridContent = ContainerUiTheme.CreateRect("Content", viewport);
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0f, 1f);
            gridContent.anchoredPosition = Vector2.zero;
            gridContent.sizeDelta = new Vector2(0f, 0f);

            var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(288f, 44f);
            grid.spacing = new Vector2(10f, 8f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            var fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = scrollContainer.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = gridContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            emptyLabel = ContainerUiTheme.CreateText(
                scrollContainer,
                "EmptyLabel",
                "No unlocked items or currencies match your filter.",
                16f,
                ContainerUiTheme.MutedColor,
                TextAlignmentOptions.Center,
                raycastTarget: false);
            StretchFull(emptyLabel.rectTransform);
            emptyLabel.gameObject.SetActive(false);
        }

        void BuildFooter(RectTransform window)
        {
            var clearBtn = ContainerUiTheme.CreateButton(
                window,
                "ClearButton",
                "Regular Container (No Target)",
                15f,
                SelectNoneAndClose);
            var clearRect = clearBtn.GetComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(0f, 0f);
            clearRect.anchorMax = new Vector2(0f, 0f);
            clearRect.pivot = new Vector2(0f, 0f);
            clearRect.anchoredPosition = new Vector2(18f, 16f);
            clearRect.sizeDelta = new Vector2(246f, 34f);

            var cancelBtn = ContainerUiTheme.CreateButton(
                window,
                "CancelButton",
                "Cancel",
                15f,
                CancelSelection);
            var cancelRect = cancelBtn.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(1f, 0f);
            cancelRect.anchorMax = new Vector2(1f, 0f);
            cancelRect.pivot = new Vector2(1f, 0f);
            cancelRect.anchoredPosition = new Vector2(-18f, 16f);
            cancelRect.sizeDelta = new Vector2(110f, 34f);
        }

        Button CreateTabButton(RectTransform window, string name, string text, Vector2 pos, float width, TargetFilterTab tab)
        {
            var btn = ContainerUiTheme.CreateButton(window, name, text, 14f, () =>
            {
                activeTab = tab;
                UpdateTabVisuals();
                RebuildRows();
            });

            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, 32f);
            return btn;
        }

        void UpdateTabVisuals()
        {
            SetTabColor(allTabBtn, activeTab == TargetFilterTab.All);
            SetTabColor(productTabBtn, activeTab == TargetFilterTab.Products);
            SetTabColor(currencyTabBtn, activeTab == TargetFilterTab.Currencies);
        }

        static void SetTabColor(Button btn, bool active)
        {
            if (btn && btn.targetGraphic)
            {
                btn.targetGraphic.color = active
                    ? ContainerUiTheme.ButtonSelectedColor
                    : ContainerUiTheme.ButtonIdleColor;
            }
        }

        void RebuildCatalog()
        {
            catalog.Clear();

            var currencyList = new List<TargetCatalogEntry>();
            foreach (GameCurrency currency in Enum.GetValues(typeof(GameCurrency)))
            {
                if (!currency.IsUnlocked())
                {
                    continue;
                }

                var target = ContainerTarget.ForCurrency(currency);
                currencyList.Add(new TargetCatalogEntry(target, target.GetDisplayName(), target.GetIcon()));
            }

            var itemList = new List<TargetCatalogEntry>();
            var unlocked = FoundationApi.ItemAccess?.Unlocked;
            if (unlocked != null)
            {
                for (int i = 0; i < unlocked.Count; i++)
                {
                    var item = unlocked[i];
                    if (item == null || string.IsNullOrEmpty(item.Identifier))
                    {
                        continue;
                    }

                    var target = ContainerTarget.ForItem(item.Identifier);
                    itemList.Add(new TargetCatalogEntry(target, target.GetDisplayName(), target.GetIcon()));
                }
            }

            itemList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            catalog.AddRange(currencyList);
            catalog.AddRange(itemList);
        }

        void RebuildRows()
        {
            string query = (searchInput ? searchInput.text : string.Empty).Trim().ToLowerInvariant();
            bool hasQuery = !string.IsNullOrEmpty(query);

            int shown = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog[i];
                if (activeTab == TargetFilterTab.Products && entry.IsCurrency)
                {
                    continue;
                }

                if (activeTab == TargetFilterTab.Currencies && !entry.IsCurrency)
                {
                    continue;
                }

                if (hasQuery && !entry.SearchKey.Contains(query, StringComparison.Ordinal))
                {
                    continue;
                }

                if (shown >= rowPool.Count)
                {
                    rowPool.Add(new TargetOptionRow(gridContent, SelectEntryAndClose));
                }

                bool selected = currentTarget != null && currentTarget.Equals(entry.Target);
                rowPool[shown].Bind(entry, selected);
                shown++;
            }

            for (int i = shown; i < rowPool.Count; i++)
            {
                rowPool[i].Hide();
            }

            if (emptyLabel)
            {
                emptyLabel.gameObject.SetActive(shown == 0);
            }
        }

        void UpdateSubtitle()
        {
            if (!subtitleLabel)
            {
                return;
            }

            if (currentTarget != null && currentTarget.IsAssigned)
            {
                subtitleLabel.text = $"Current target: {currentTarget.GetDisplayName()}";
            }
            else
            {
                subtitleLabel.text = "Choose an End Product or Currency to highlight on this container.";
            }
        }

        void SelectEntryAndClose(TargetCatalogEntry entry)
        {
            FoundationApi.UiAccess?.PlaySound(GameUiSound.Button);
            CommitSelectionAndClose(entry.Target);
        }

        void SelectNoneAndClose()
        {
            CommitSelectionAndClose(ContainerTarget.None);
        }

        void CommitSelectionAndClose(ContainerTarget target)
        {
            var container = activeContainer;
            var callback = onSelected;
            onDismissed = null;
            ClosePopup();

            if (container != null)
            {
                callback?.Invoke(container, target ?? ContainerTarget.None);
            }
        }

        void CancelSelection()
        {
            ClosePopup();
        }

        void LockInput(bool disableGameInput)
        {
            if (disableGameInput == inputDisabledByPopup)
            {
                return;
            }

            inputDisabledByPopup = disableGameInput;
            FoundationApi.UiAccess?.SetGameInputEnabled(!disableGameInput);
        }

        void ReleaseInputLock()
        {
            if (inputDisabledByPopup)
            {
                inputDisabledByPopup = false;
                FoundationApi.UiAccess?.SetGameInputEnabled(true);
            }
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
