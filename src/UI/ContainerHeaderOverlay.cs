using System;
using System.Collections.Generic;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class ContainerHeaderOverlay : MonoBehaviour
    {
        const float RefreshIntervalSeconds = 0.25f;
        const float TopInset = 8f;
        const float RightInset = 14f;
        const float DefaultLeftInset = 104f;
        const float BarHeight = 40f;
        const float ReservedBarHeight = 52f;

        readonly List<ContainerHeaderEntryView> entryViews = new List<ContainerHeaderEntryView>();

        WorkspaceObject container;
        ContainerTargetStore store;
        ContainerProductionCalculator calculator;
        ContainerConfig config;
        Action<ContainerHeaderOverlay> onDestroyed;

        GameObject barRoot;
        RectTransform barRect;
        Canvas worldCanvas;
        TextMeshProUGUI overflowLabel;
        ContainerProductionSnapshot lastSnapshot = ContainerProductionSnapshot.Empty;
        ResourceProductionEntry hoveredResource;
        float refreshTimer;
        bool dirty = true;
        bool isHovered;

        public WorkspaceObject Container => container;

        public void Initialize(
            WorkspaceObject containerObject,
            ContainerTargetStore targetStore,
            ContainerProductionCalculator productionCalculator,
            ContainerConfig modConfig,
            Action<ContainerHeaderOverlay> destroyedCallback)
        {
            container = containerObject;
            store = targetStore;
            calculator = productionCalculator;
            config = modConfig;
            onDestroyed = destroyedCallback;

            EnsureVisuals();
            UpdateBarGeometry();
            MarkDirty();
        }

        public void MarkDirty()
        {
            dirty = true;
            refreshTimer = 0f;
        }

        public bool TryGetBlockedWorldRect(out Vector2Int position, out Vector2Int size)
        {
            if (container == null || !barRoot || !barRoot.activeInHierarchy)
            {
                position = default;
                size = default;
                return false;
            }

            int width = Mathf.RoundToInt(container.Size.x);
            int height = Mathf.RoundToInt(Mathf.Min(ReservedBarHeight, container.Size.y));
            if (width <= 8 || height <= 8)
            {
                position = default;
                size = default;
                return false;
            }

            position = container.Position;
            size = new Vector2Int(width, height);
            return true;
        }

        public void HighlightBuilding(Vector2Int buildingPos, Vector2 buildingSize)
        {
            if (container == null)
            {
                return;
            }

            BuildingHighlightPulse.Spawn(transform, container.Position, buildingPos, buildingSize);
        }

        void Update()
        {
            if (container == null || !barRoot)
            {
                return;
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (dirty || refreshTimer <= 0f)
            {
                dirty = false;
                refreshTimer = RefreshIntervalSeconds;
                RefreshNow();
            }

            UpdateHoverState();
        }

        void OnDisable()
        {
            ClearHover();
        }

        void OnDestroy()
        {
            ClearHover();
            if (ContainerHoverTooltip.IsPinnedTo(this))
            {
                ContainerHoverTooltip.UnpinIfPinned();
            }

            onDestroyed?.Invoke(this);
            if (barRoot)
            {
                Destroy(barRoot);
                barRoot = null;
            }
        }

        void EnsureVisuals()
        {
            if (barRoot)
            {
                return;
            }

            barRect = ContainerUiTheme.CreateRect("ProductionHeaderBar", transform);
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.localScale = Vector3.one;

            var bg = barRect.gameObject.AddComponent<Image>();
            bg.color = ContainerUiTheme.HeaderBgColor;
            bg.raycastTarget = false;

            barRect.gameObject.AddComponent<RectMask2D>();
            ContainerUiTheme.AddFramedBorder(barRect, ContainerUiTheme.BrassColor, raycastTarget: false);

            var layout = barRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            overflowLabel = ContainerUiTheme.CreateText(
                barRect,
                "OverflowCount",
                string.Empty,
                15f,
                ContainerUiTheme.SecondaryInkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false);
            overflowLabel.textWrappingMode = TextWrappingModes.NoWrap;
            overflowLabel.gameObject.SetActive(false);

            barRoot = barRect.gameObject;
            worldCanvas = barRect.GetComponentInParent<Canvas>();
        }

        void UpdateBarGeometry()
        {
            if (!barRect || container == null)
            {
                return;
            }

            float containerWidth = Mathf.Max(0f, container.Size.x);
            float leftInset = containerWidth >= 220f
                ? DefaultLeftInset
                : Mathf.Clamp(containerWidth * 0.45f, 36f, DefaultLeftInset);
            float rightInset = containerWidth >= 140f ? RightInset : 6f;

            if (containerWidth - leftInset - rightInset < 48f)
            {
                leftInset = Mathf.Max(8f, containerWidth - rightInset - 48f);
            }

            float scale = config?.HeaderScale != null ? Mathf.Clamp(config.HeaderScale.Value, 0.85f, 1.35f) : 1.0f;
            float height = Mathf.Clamp(Mathf.Round(BarHeight * Mathf.Lerp(1f, scale, 0.4f)), 34f, 46f);

            barRect.offsetMin = new Vector2(leftInset, -(TopInset + height));
            barRect.offsetMax = new Vector2(-rightInset, -TopInset);
        }

        void RefreshNow()
        {
            if (store == null || calculator == null || config == null || container == null)
            {
                lastSnapshot = ContainerProductionSnapshot.Empty;
                barRoot.SetActive(false);
                ClearHover();
                return;
            }

            UpdateBarGeometry();

            var target = store.GetTarget(container);
            bool perMinute = config.ShowPerMinute.Value;
            double toleranceRatio = Mathf.Clamp(config.OperationalTolerancePercent.Value, 50, 100) / 100.0d;
            lastSnapshot = calculator.ComputeSnapshot(container, target, config.RollingWindowSeconds.Value, perMinute, toleranceRatio);
            var entries = lastSnapshot.SummaryEntries;

            bool shouldShow = target.IsAssigned
                || (config.ShowHeaderOnRegularContainers.Value && entries.Count > 0);

            if (!shouldShow || entries.Count == 0)
            {
                barRoot.SetActive(false);
                ClearHover();
                if (ContainerHoverTooltip.IsPinnedTo(this))
                {
                    ContainerHoverTooltip.UnpinIfPinned();
                }

                return;
            }

            barRoot.SetActive(true);
            barRoot.transform.localScale = Vector3.one;

            int maxVisible = Math.Max(1, config.MaxHeaderEntries.Value);
            int visibleCount = Math.Min(entries.Count, maxVisible);

            while (entryViews.Count < visibleCount)
            {
                entryViews.Add(new ContainerHeaderEntryView(barRoot.transform, entryViews.Count > 0));
            }

            ContainerHoverTooltip.TryGetPinnedResource(this, out var pinnedResource);

            for (int i = 0; i < visibleCount; i++)
            {
                bool pillHovered = SameResource(entries[i], hoveredResource);
                bool pillPinned = SameResource(entries[i], pinnedResource);

                entryViews[i].Bind(
                    entries[i],
                    perMinute,
                    showSeparator: i > 0,
                    onClicked: OnPillClicked,
                    hovered: pillHovered,
                    pinned: pillPinned);
            }

            for (int i = visibleCount; i < entryViews.Count; i++)
            {
                entryViews[i].Hide();
            }

            int extra = entries.Count - visibleCount;
            if (extra > 0)
            {
                overflowLabel.transform.SetAsLastSibling();
                overflowLabel.text = $"+{extra}";
                overflowLabel.gameObject.SetActive(true);
            }
            else
            {
                overflowLabel.gameObject.SetActive(false);
            }

            if (ContainerHoverTooltip.IsPinnedTo(this))
            {
                ContainerHoverTooltip.RefreshPinned(this, lastSnapshot, config);
            }
        }

        void OnPillClicked(ResourceProductionEntry entry)
        {
            var ui = FoundationApi.UiAccess;
            if (ui == null
                || entry == null
                || !barRoot.activeInHierarchy
                || !ui.IsWorkspaceVisible
                || ui.HasPopup
                || ui.IsDraggingGhost
                || ui.IsDragDropBusy)
            {
                return;
            }

            ContainerHoverTooltip.TogglePinFor(this, lastSnapshot, entry, ui.MouseUi, config);
            SyncPillVisualStates();
        }

        void UpdateHoverState()
        {
            var ui = FoundationApi.UiAccess;
            if (ui == null
                || !barRoot.activeInHierarchy
                || !ui.IsWorkspaceVisible
                || ui.HasPopup
                || ui.IsDraggingGhost
                || ui.IsPointerOverNonWorkspaceUi)
            {
                ClearHover();
                return;
            }

            if (!worldCanvas)
            {
                worldCanvas = barRect.GetComponentInParent<Canvas>();
            }

            var cam = worldCanvas && worldCanvas.worldCamera ? worldCanvas.worldCamera : Camera.main;
            Vector2 mouseScreen = ui.MouseUi;

            if (!RectTransformUtility.RectangleContainsScreenPoint(barRect, mouseScreen, cam))
            {
                ClearHover();
                return;
            }

            isHovered = true;
            hoveredResource = null;

            for (int i = 0; i < entryViews.Count; i++)
            {
                var view = entryViews[i];
                if (!view.Root.activeSelf || view.BoundEntry == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(view.PillRect, mouseScreen, cam))
                {
                    hoveredResource = view.BoundEntry;
                    break;
                }
            }

            SyncPillVisualStates();

            if (!ContainerHoverTooltip.IsAnyPinned)
            {
                if (hoveredResource != null)
                {
                    ContainerHoverTooltip.ShowHoverFor(this, lastSnapshot, hoveredResource, mouseScreen, config);
                }
                else
                {
                    ContainerHoverTooltip.HideUnpinnedIfOwner(this);
                }
            }
        }

        void SyncPillVisualStates()
        {
            ContainerHoverTooltip.TryGetPinnedResource(this, out var pinnedResource);
            for (int i = 0; i < entryViews.Count; i++)
            {
                var view = entryViews[i];
                if (!view.Root.activeSelf || view.BoundEntry == null)
                {
                    continue;
                }

                bool pillHovered = SameResource(view.BoundEntry, hoveredResource);
                bool pillPinned = SameResource(view.BoundEntry, pinnedResource);
                view.SetVisualState(pillHovered, pillPinned);
            }
        }

        void ClearHover()
        {
            if (isHovered || hoveredResource != null)
            {
                isHovered = false;
                hoveredResource = null;
            }

            SyncPillVisualStates();
            ContainerHoverTooltip.HideUnpinnedIfOwner(this);
        }

        static bool SameResource(ResourceProductionEntry a, ResourceProductionEntry b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return a.Kind == b.Kind
                && string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
