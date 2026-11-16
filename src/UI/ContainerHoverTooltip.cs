using System;
using System.Collections.Generic;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class ContainerHoverTooltip : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        sealed class AggregatedInput
        {
            public ContainerTargetKind Kind;
            public string Id;
            public string Name;
            public Sprite Icon;
            public double CurrentPerSecond;
            public double PotentialPerSecond;
        }

        const int MaxVisibleProducerRows = 16;

        static readonly Color PinnedFrameColor = new Color(224f / 255f, 178f / 255f, 94f / 255f, 1f);
        static readonly Color OperationalAccentColor = new Color(138f / 255f, 212f / 255f, 122f / 255f, 1f);
        static readonly Color StarvedAccentColor = new Color(232f / 255f, 118f / 255f, 98f / 255f, 1f);
        static readonly Color IdleAccentColor = new Color(156f / 255f, 143f / 255f, 128f / 255f, 1f);

        static ContainerHoverTooltip instance;

        readonly List<ContainerHoverRowView> rows = new List<ContainerHoverRowView>();
        readonly List<TextMeshProUGUI> sectionHeaders = new List<TextMeshProUGUI>();

        RectTransform panelRect;
        CanvasGroup canvasGroup;
        Image frameImage;
        TextMeshProUGUI headerText;
        Button closeButton;

        GameObject statusBannerGo;
        TextMeshProUGUI statusPrefixText;
        Image statusSubjectIcon;
        TextMeshProUGUI statusSubjectText;
        TextMeshProUGUI statusDetailText;
        TextMeshProUGUI statusFixText;

        RectTransform rowsContainer;
        TextMeshProUGUI emptyText;
        TextMeshProUGUI overflowText;

        object currentOwner;
        ContainerHeaderOverlay pinnedOwner;
        ResourceProductionEntry pinnedResource;
        Vector2 pinnedWorldPosition;
        Vector2 dragPointerOffset;
        bool hasPinnedWorldAnchor;
        bool isDragging;
        bool isPinned;
        bool uiBuilt;

        public static bool IsAnyPinned => instance && instance.isPinned && instance.gameObject.activeSelf;

        public static bool IsPinnedTo(object owner)
        {
            return IsAnyPinned && ReferenceEquals(instance.pinnedOwner, owner);
        }

        public static bool TryGetPinnedResource(object owner, out ResourceProductionEntry resource)
        {
            if (IsPinnedTo(owner))
            {
                resource = instance.pinnedResource;
                return resource != null;
            }

            resource = null;
            return false;
        }

        public static void ShowHoverFor(
            object owner,
            ContainerProductionSnapshot snapshot,
            ResourceProductionEntry hoveredResource,
            Vector2 mouseScreenPosition,
            ContainerConfig config)
        {
            if (hoveredResource == null)
            {
                HideUnpinnedIfOwner(owner);
                return;
            }

            if (IsAnyPinned)
            {
                return;
            }

            var tooltip = GetOrCreate();
            if (!tooltip)
            {
                return;
            }

            tooltip.Render(owner, snapshot, hoveredResource, mouseScreenPosition, config, pinnedMode: false);
        }

        public static void HideUnpinnedIfOwner(object owner)
        {
            if (instance && !instance.isPinned && ReferenceEquals(instance.currentOwner, owner))
            {
                instance.Hide();
            }
        }

        public static void TogglePinFor(
            ContainerHeaderOverlay owner,
            ContainerProductionSnapshot snapshot,
            ResourceProductionEntry resource,
            Vector2 anchorScreenPosition,
            ContainerConfig config)
        {
            if (!owner || resource == null)
            {
                return;
            }

            if (IsPinnedTo(owner) && MatchesKey(instance.pinnedResource, resource))
            {
                instance.UnpinAndHide();
                FoundationApi.UiAccess?.PlaySound(GameUiSound.WindowClose);
                return;
            }

            var tooltip = GetOrCreate();
            if (!tooltip)
            {
                return;
            }

            tooltip.isPinned = true;
            tooltip.isDragging = false;
            tooltip.hasPinnedWorldAnchor = false;
            tooltip.pinnedOwner = owner;
            tooltip.pinnedResource = resource;
            tooltip.Render(owner, snapshot, resource, anchorScreenPosition, config, pinnedMode: true, reposition: true);
            tooltip.CaptureWorldAnchorFromPanel();
            FoundationApi.UiAccess?.PlaySound(GameUiSound.Button);
        }

        public static void RefreshPinned(
            ContainerHeaderOverlay owner,
            ContainerProductionSnapshot snapshot,
            ContainerConfig config)
        {
            if (!IsPinnedTo(owner) || instance.pinnedResource == null)
            {
                return;
            }

            var targetResource = instance.pinnedResource;
            var summaries = snapshot?.SummaryEntries;
            if (summaries != null)
            {
                for (int i = 0; i < summaries.Count; i++)
                {
                    if (MatchesKey(summaries[i], targetResource))
                    {
                        targetResource = summaries[i];
                        instance.pinnedResource = targetResource;
                        break;
                    }
                }
            }

            instance.Render(owner, snapshot, targetResource, default, config, pinnedMode: true, reposition: false);
        }

        public static bool UnpinIfPinned()
        {
            if (!IsAnyPinned)
            {
                return false;
            }

            instance.UnpinAndHide();
            FoundationApi.UiAccess?.PlaySound(GameUiSound.WindowClose);
            return true;
        }

        public static void DestroyIfCreated()
        {
            if (instance)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        static ContainerHoverTooltip GetOrCreate()
        {
            if (instance)
            {
                return instance;
            }

            var root = FoundationApi.UiAccess?.Root;
            if (!root)
            {
                return null;
            }

            var go = new GameObject("BalancedContainers_HoverTooltip", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root.transform, worldPositionStays: false);
            rect.localScale = Vector3.one;

            instance = go.AddComponent<ContainerHoverTooltip>();
            instance.EnsureBuilt();
            go.SetActive(false);
            return instance;
        }

        void Update()
        {
            if (!isPinned || !gameObject.activeSelf)
            {
                return;
            }

            var ui = FoundationApi.UiAccess;
            if (ui == null || !ui.IsWorkspaceVisible || ui.HasPopup || ui.IsDraggingGhost || !pinnedOwner)
            {
                UnpinAndHide();
                return;
            }

            Vector2 mouseScreen = ui.MouseUi;
            var uiCam = GetUiCamera(panelRect ? panelRect.parent as RectTransform : null);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.Root.activeSelf || !row.IsInteractive)
                {
                    continue;
                }

                bool inside = !isDragging && RectTransformUtility.RectangleContainsScreenPoint(row.RowRect, mouseScreen, uiCam);
                row.SetHovered(inside);
            }
        }

        void LateUpdate()
        {
            if (!isPinned || !gameObject.activeSelf || isDragging || !hasPinnedWorldAnchor)
            {
                return;
            }

            SyncPinnedWorldToScreen();
        }

        void OnDisable()
        {
            isDragging = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isPinned)
            {
                transform.SetAsLastSibling();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isPinned || !gameObject.activeSelf || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (eventData.pointerPress != gameObject || !TryGetParentLocalPoint(eventData.position, out _, out var pointerLocal))
            {
                return;
            }

            isDragging = true;
            dragPointerOffset = panelRect.anchoredPosition - pointerLocal;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || !isPinned || !gameObject.activeSelf)
            {
                return;
            }

            if (!TryGetParentLocalPoint(eventData.position, out var parentRect, out var pointerLocal))
            {
                return;
            }

            panelRect.anchoredPosition = ClampLocalToParent(parentRect, pointerLocal + dragPointerOffset);
            CaptureWorldAnchorFromPanel();
        }

        bool TryGetParentLocalPoint(Vector2 screenPoint, out RectTransform parentRect, out Vector2 localPoint)
        {
            parentRect = panelRect ? panelRect.parent as RectTransform : null;
            if (!parentRect)
            {
                localPoint = default;
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                GetUiCamera(parentRect),
                out localPoint);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging)
            {
                return;
            }

            isDragging = false;
            CaptureWorldAnchorFromPanel();
        }

        void OnDestroy()
        {
            if (ReferenceEquals(instance, this))
            {
                instance = null;
            }
        }

        void EnsureBuilt()
        {
            if (uiBuilt)
            {
                return;
            }

            uiBuilt = true;
            panelRect = (RectTransform)transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(465f, 40f);

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var bg = gameObject.AddComponent<Image>();
            bg.color = ContainerUiTheme.PanelColor;
            bg.raycastTarget = true;

            frameImage = CreateFrameBorder(panelRect, ContainerUiTheme.BrassColor);

            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headerRow = ContainerUiTheme.CreateRect("HeaderRow", panelRect);
            var headerRowLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerRowLayout.spacing = 6f;
            headerRowLayout.childAlignment = TextAnchor.MiddleLeft;
            headerRowLayout.childControlWidth = true;
            headerRowLayout.childControlHeight = true;
            headerRowLayout.childForceExpandWidth = false;
            headerRowLayout.childForceExpandHeight = false;
            var headerRowElem = headerRow.gameObject.AddComponent<LayoutElement>();
            headerRowElem.preferredHeight = 24f;

            headerText = ContainerUiTheme.CreateText(
                headerRow,
                "Header",
                string.Empty,
                14.5f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            headerText.fontStyle = FontStyles.Bold;
            headerText.textWrappingMode = TextWrappingModes.NoWrap;
            headerText.overflowMode = TextOverflowModes.Ellipsis;
            var headerTextElem = headerText.gameObject.AddComponent<LayoutElement>();
            headerTextElem.flexibleWidth = 1f;
            headerTextElem.preferredHeight = 22f;

            closeButton = ContainerUiTheme.CreateButton(
                headerRow,
                "ClosePinnedButton",
                "×",
                16f,
                () => UnpinIfPinned());
            var closeElem = closeButton.gameObject.AddComponent<LayoutElement>();
            closeElem.preferredWidth = 24f;
            closeElem.preferredHeight = 22f;
            closeButton.gameObject.SetActive(false);

            BuildStatusBanner();

            rowsContainer = ContainerUiTheme.CreateRect("RowsContainer", panelRect);
            var rowsLayout = rowsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 3f;
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            emptyText = ContainerUiTheme.CreateText(
                panelRect,
                "Empty",
                "No buildings affecting this resource inside this container.",
                13f,
                ContainerUiTheme.MutedColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            var emptyElem = emptyText.gameObject.AddComponent<LayoutElement>();
            emptyElem.preferredHeight = 20f;
            emptyText.gameObject.SetActive(false);

            overflowText = ContainerUiTheme.CreateText(
                panelRect,
                "Overflow",
                string.Empty,
                12f,
                ContainerUiTheme.SecondaryInkColor,
                TextAlignmentOptions.Right,
                raycastTarget: false,
                cleanFont: true);
            var overElem = overflowText.gameObject.AddComponent<LayoutElement>();
            overElem.preferredHeight = 18f;
            overflowText.gameObject.SetActive(false);
        }

        void BuildStatusBanner()
        {
            var bannerRect = ContainerUiTheme.CreateRect("StatusBanner", panelRect);
            statusBannerGo = bannerRect.gameObject;

            var bannerBg = statusBannerGo.AddComponent<Image>();
            bannerBg.color = ContainerUiTheme.StatusWellColor;
            bannerBg.raycastTarget = false;

            var bannerFrame = CreateFrameBorder(bannerRect, ContainerUiTheme.BrassColor);
            if (bannerFrame)
            {
                bannerFrame.transform.SetAsFirstSibling();
            }

            var bannerLayout = statusBannerGo.AddComponent<VerticalLayoutGroup>();
            bannerLayout.padding = new RectOffset(10, 10, 8, 8);
            bannerLayout.spacing = 3f;
            bannerLayout.childAlignment = TextAnchor.UpperLeft;
            bannerLayout.childControlWidth = true;
            bannerLayout.childControlHeight = true;
            bannerLayout.childForceExpandWidth = true;
            bannerLayout.childForceExpandHeight = false;

            var titleLine = ContainerUiTheme.CreateRect("StatusLine", bannerRect);
            var titleLayout = titleLine.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 4f;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = false;
            var titleLineElem = titleLine.gameObject.AddComponent<LayoutElement>();
            titleLineElem.minHeight = 20f;
            titleLineElem.preferredHeight = 20f;

            statusPrefixText = ContainerUiTheme.CreateText(
                titleLine,
                "StatusPrefix",
                string.Empty,
                13.5f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            statusPrefixText.textWrappingMode = TextWrappingModes.NoWrap;

            var subIconRect = ContainerUiTheme.CreateRect("SubjectIcon", titleLine);
            statusSubjectIcon = subIconRect.gameObject.AddComponent<Image>();
            statusSubjectIcon.preserveAspect = true;
            statusSubjectIcon.raycastTarget = false;
            var subIconElem = subIconRect.gameObject.AddComponent<LayoutElement>();
            subIconElem.preferredWidth = 17f;
            subIconElem.preferredHeight = 17f;

            statusSubjectText = ContainerUiTheme.CreateText(
                titleLine,
                "SubjectName",
                string.Empty,
                13.5f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            statusSubjectText.fontStyle = FontStyles.Bold;
            statusSubjectText.textWrappingMode = TextWrappingModes.NoWrap;
            statusSubjectText.overflowMode = TextOverflowModes.Ellipsis;
            var subNameElem = statusSubjectText.gameObject.AddComponent<LayoutElement>();
            subNameElem.flexibleWidth = 1f;

            statusDetailText = ContainerUiTheme.CreateText(
                bannerRect,
                "StatusDetail",
                string.Empty,
                13f,
                ContainerUiTheme.InkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            statusDetailText.textWrappingMode = TextWrappingModes.Normal;

            statusFixText = ContainerUiTheme.CreateText(
                bannerRect,
                "StatusFix",
                string.Empty,
                12f,
                ContainerUiTheme.SecondaryInkColor,
                TextAlignmentOptions.Left,
                raycastTarget: false,
                cleanFont: true);
            statusFixText.textWrappingMode = TextWrappingModes.Normal;
        }

        static Image CreateFrameBorder(Transform parent, Color tint)
        {
            var border = ContainerUiTheme.FindFrameSprite();
            if (!border)
            {
                return null;
            }

            var frameImage = ContainerUiTheme.CreateImage("Frame", parent, tint, raycastTarget: false);
            frameImage.sprite = border;
            frameImage.type = Image.Type.Sliced;

            var ignoreLayout = frameImage.gameObject.AddComponent<LayoutElement>();
            ignoreLayout.ignoreLayout = true;

            var rect = frameImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return frameImage;
        }

        void Render(
            object owner,
            ContainerProductionSnapshot snapshot,
            ResourceProductionEntry resource,
            Vector2 anchorScreenPosition,
            ContainerConfig config,
            bool pinnedMode,
            bool reposition = true)
        {
            if (resource == null)
            {
                Hide();
                return;
            }

            EnsureBuilt();
            currentOwner = owner;

            canvasGroup.blocksRaycasts = pinnedMode;
            canvasGroup.interactable = pinnedMode;
            if (frameImage)
            {
                frameImage.color = pinnedMode ? PinnedFrameColor : ContainerUiTheme.BrassColor;
            }

            if (closeButton)
            {
                closeButton.gameObject.SetActive(pinnedMode);
            }

            var diagnosis = snapshot?.GetDiagnosis(resource.Kind, resource.Id);
            BindDiagnosis(diagnosis, resource);

            bool perMinute = config != null && config.ShowPerMinute.Value;
            var allEntries = snapshot?.BuildingEntries ?? Array.Empty<BuildingProductionEntry>();

            var producers = new List<BuildingProductionEntry>();
            var consumers = new List<BuildingProductionEntry>();
            var producerKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < allEntries.Count; i++)
            {
                var entry = allEntries[i];
                if (!MatchesResource(entry, resource))
                {
                    continue;
                }

                if (entry.IsConsumer)
                {
                    consumers.Add(entry);
                }
                else
                {
                    producers.Add(entry);
                    producerKeys.Add(ResourceStatusAnalyzer.MakeBuildingKey(entry.Position, entry.BuildingName));
                }
            }

            var inputTotals = CollectAggregatedInputs(allEntries, producerKeys);
            if (producers.Count == 0 && consumers.Count > 0)
            {
                inputTotals.Add(new AggregatedInput
                {
                    Kind = resource.Kind,
                    Id = resource.Id,
                    Name = resource.DisplayName,
                    Icon = resource.Icon,
                    CurrentPerSecond = SumCurrent(consumers),
                    PotentialPerSecond = SumPotential(consumers)
                });
            }

            string pinSuffix = pinnedMode ? " (Drag • Click producer to jump)" : " (Click resource to pin)";
            headerText.text = $"{resource.DisplayName}{pinSuffix}";

            int headerCursor = 0;
            int rowCursor = 0;
            int childOrder = 0;

            if (inputTotals.Count > 0)
            {
                var inputHeader = ObtainSectionHeader(ref headerCursor, "[-------Input-------]");
                inputHeader.transform.SetSiblingIndex(childOrder++);

                for (int i = 0; i < inputTotals.Count; i++)
                {
                    var input = inputTotals[i];
                    var row = ObtainRow(ref rowCursor);
                    row.BindSummary(input.Kind, input.Id, input.Icon, input.Name, input.CurrentPerSecond, input.PotentialPerSecond, perMinute, isInput: true);
                    row.RowRect.SetSiblingIndex(childOrder++);
                }
            }

            int visibleProducers = Math.Min(producers.Count, MaxVisibleProducerRows);
            if (visibleProducers > 0)
            {
                var prodHeader = ObtainSectionHeader(ref headerCursor, "[-----Producers-----]");
                prodHeader.transform.SetSiblingIndex(childOrder++);

                for (int i = 0; i < visibleProducers; i++)
                {
                    var prod = producers[i];
                    var row = ObtainRow(ref rowCursor);
                    row.BindBuilding(
                        prod,
                        perMinute,
                        highlighted: resource.IsPrimaryTarget,
                        interactive: pinnedMode,
                        onClicked: pinnedMode ? HandleRowClicked : (Action<BuildingProductionEntry>)null);
                    row.RowRect.SetSiblingIndex(childOrder++);
                }
            }

            bool hasAnyData = inputTotals.Count > 0 || producers.Count > 0 || resource.PotentialPerSecond > 0d || resource.CurrentPerSecond > 0d;
            if (producers.Count > 0 || (producers.Count == 0 && consumers.Count == 0 && hasAnyData))
            {
                var outputHeader = ObtainSectionHeader(ref headerCursor, "[-------Output-------]");
                outputHeader.transform.SetSiblingIndex(childOrder++);

                double outCur = producers.Count > 0 ? SumCurrent(producers) : resource.CurrentPerSecond;
                double outPot = producers.Count > 0 ? SumPotential(producers) : resource.PotentialPerSecond;
                var outRow = ObtainRow(ref rowCursor);
                outRow.BindSummary(resource.Kind, resource.Id, resource.Icon, resource.DisplayName, outCur, outPot, perMinute, isInput: false);
                outRow.RowRect.SetSiblingIndex(childOrder++);
            }

            for (int i = headerCursor; i < sectionHeaders.Count; i++)
            {
                sectionHeaders[i].gameObject.SetActive(false);
            }

            for (int i = rowCursor; i < rows.Count; i++)
            {
                rows[i].Hide();
            }

            emptyText.gameObject.SetActive(!hasAnyData);

            int extraProducers = producers.Count - visibleProducers;
            if (extraProducers > 0)
            {
                overflowText.text = $"+{extraProducers} more producers...";
                overflowText.gameObject.SetActive(true);
            }
            else
            {
                overflowText.gameObject.SetActive(false);
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.SetAsLastSibling();
            if (reposition || !pinnedMode || !hasPinnedWorldAnchor)
            {
                PositionNearScreenPoint(anchorScreenPosition);
            }
            else
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
                if (!isDragging)
                {
                    SyncPinnedWorldToScreen();
                }
            }
        }

        void BindDiagnosis(ResourceDiagnosis diagnosis, ResourceProductionEntry resource)
        {
            if (diagnosis == null)
            {
                statusBannerGo.SetActive(false);
                return;
            }

            statusBannerGo.SetActive(true);

            Color accent;
            string label;
            switch (diagnosis.State)
            {
                case ResourceDiagnosticState.Operational:
                    accent = OperationalAccentColor;
                    label = "Operational";
                    break;
                case ResourceDiagnosticState.Starved:
                    accent = StarvedAccentColor;
                    label = "Starved";
                    break;
                case ResourceDiagnosticState.Oversaturated:
                    accent = ContainerUiTheme.OversaturatedColor;
                    label = "Over-Saturated";
                    break;
                case ResourceDiagnosticState.Saturated:
                    accent = ContainerUiTheme.WarnRateColor;
                    label = "Saturated";
                    break;
                default:
                    accent = IdleAccentColor;
                    label = "Idle";
                    break;
            }

            string statusHex = ContainerUiTheme.ColorToHex(accent);
            string leadPart = string.IsNullOrEmpty(diagnosis.ActionLead) ? string.Empty : $"{diagnosis.ActionLead} ";
            statusPrefixText.text = $"Status: <color={statusHex}><b>{label}</b></color> -- {leadPart}";

            Sprite subjectIcon = diagnosis.SubjectIcon ?? resource.Icon;
            string subjectName = !string.IsNullOrWhiteSpace(diagnosis.SubjectName)
                ? diagnosis.SubjectName
                : resource.DisplayName;

            statusSubjectIcon.enabled = subjectIcon != null;
            statusSubjectIcon.sprite = subjectIcon;
            statusSubjectText.text = diagnosis.State == ResourceDiagnosticState.Operational
                ? subjectName
                : $"{subjectName}:";

            statusDetailText.text = diagnosis.Headline;

            if (!string.IsNullOrWhiteSpace(diagnosis.Solution))
            {
                statusFixText.gameObject.SetActive(true);
                statusFixText.text = $"<color=#e3d5ba>Fix:</color> {diagnosis.Solution}";
            }
            else
            {
                statusFixText.gameObject.SetActive(false);
            }
        }

        TextMeshProUGUI ObtainSectionHeader(ref int index, string title)
        {
            while (sectionHeaders.Count <= index)
            {
                var label = ContainerUiTheme.CreateText(
                    rowsContainer,
                    "SectionHeader",
                    string.Empty,
                    12.5f,
                    ContainerUiTheme.SecondaryInkColor,
                    TextAlignmentOptions.Center,
                    raycastTarget: false,
                    cleanFont: true);
                var elem = label.gameObject.AddComponent<LayoutElement>();
                elem.preferredHeight = 18f;
                sectionHeaders.Add(label);
            }

            var target = sectionHeaders[index++];
            target.text = title;
            target.gameObject.SetActive(true);
            return target;
        }

        ContainerHoverRowView ObtainRow(ref int index)
        {
            while (rows.Count <= index)
            {
                rows.Add(new ContainerHoverRowView(rowsContainer));
            }

            return rows[index++];
        }

        static List<AggregatedInput> CollectAggregatedInputs(
            IReadOnlyList<BuildingProductionEntry> allEntries,
            HashSet<string> producerKeys)
        {
            var list = new List<AggregatedInput>();
            if (producerKeys == null || producerKeys.Count == 0)
            {
                return list;
            }

            var byKey = new Dictionary<string, AggregatedInput>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < allEntries.Count; i++)
            {
                var entry = allEntries[i];
                if (!entry.IsConsumer)
                {
                    continue;
                }

                string bKey = ResourceStatusAnalyzer.MakeBuildingKey(entry.Position, entry.BuildingName);
                if (!producerKeys.Contains(bKey))
                {
                    continue;
                }

                string resKey = $"{(int)entry.ResourceKind}:{entry.ResourceId}";
                if (!byKey.TryGetValue(resKey, out var agg))
                {
                    agg = new AggregatedInput
                    {
                        Kind = entry.ResourceKind,
                        Id = entry.ResourceId,
                        Name = entry.ResourceName,
                        Icon = entry.ResourceIcon
                    };
                    byKey[resKey] = agg;
                    list.Add(agg);
                }

                agg.CurrentPerSecond += entry.CurrentPerSecond;
                agg.PotentialPerSecond += entry.PotentialPerSecond;
            }

            return list;
        }

        static double SumCurrent(IReadOnlyList<BuildingProductionEntry> entries)
        {
            double total = 0d;
            for (int i = 0; i < entries.Count; i++)
            {
                total += entries[i].CurrentPerSecond;
            }

            return total;
        }

        static double SumPotential(IReadOnlyList<BuildingProductionEntry> entries)
        {
            double total = 0d;
            for (int i = 0; i < entries.Count; i++)
            {
                total += entries[i].PotentialPerSecond;
            }

            return total;
        }

        void HandleRowClicked(BuildingProductionEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var ui = FoundationApi.UiAccess;
            if (ui == null)
            {
                return;
            }

            float centerX = entry.Position.x + Mathf.Max(32f, entry.BuildingSize.x) * 0.5f;
            float centerY = entry.Position.y - Mathf.Max(32f, entry.BuildingSize.y) * 0.5f;
            ui.FocusWorldPosition(new Vector2(centerX, centerY));
            ui.PlaySound(GameUiSound.Button);

            if (pinnedOwner)
            {
                pinnedOwner.HighlightBuilding(entry.Position, entry.BuildingSize);
            }
        }

        void PositionNearScreenPoint(Vector2 anchorScreenPosition)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            var parentRect = panelRect.parent as RectTransform;
            if (!parentRect)
            {
                return;
            }

            var targetScreen = new Vector2(anchorScreenPosition.x + 18f, anchorScreenPosition.y - 18f);
            var uiCam = GetUiCamera(parentRect);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, targetScreen, uiCam, out var localPoint))
            {
                return;
            }

            panelRect.anchoredPosition = ClampLocalToParent(parentRect, localPoint);
        }

        Vector2 ClampLocalToParent(RectTransform parentRect, Vector2 localPoint)
        {
            var bounds = parentRect.rect;
            float width = panelRect.rect.width;
            float height = panelRect.rect.height;
            float margin = 10f;

            if (localPoint.x + width > bounds.xMax - margin)
            {
                localPoint.x = bounds.xMax - margin - width;
            }

            if (localPoint.x < bounds.xMin + margin)
            {
                localPoint.x = bounds.xMin + margin;
            }

            if (localPoint.y - height < bounds.yMin + margin)
            {
                localPoint.y = bounds.yMin + margin + height;
            }

            if (localPoint.y > bounds.yMax - margin)
            {
                localPoint.y = bounds.yMax - margin;
            }

            return localPoint;
        }

        void CaptureWorldAnchorFromPanel()
        {
            var parentRect = panelRect ? panelRect.parent as RectTransform : null;
            var worldCam = Camera.main;
            if (!parentRect || !worldCam)
            {
                return;
            }

            var uiCam = GetUiCamera(parentRect);
            Vector2 panelScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, panelRect.position);
            Vector3 worldPos = worldCam.ScreenToWorldPoint(new Vector3(panelScreenPos.x, panelScreenPos.y, 0f));
            if (float.IsNaN(worldPos.x) || float.IsNaN(worldPos.y) || float.IsInfinity(worldPos.x) || float.IsInfinity(worldPos.y))
            {
                return;
            }

            pinnedWorldPosition = new Vector2(worldPos.x, worldPos.y);
            hasPinnedWorldAnchor = true;
        }

        void SyncPinnedWorldToScreen()
        {
            var parentRect = panelRect ? panelRect.parent as RectTransform : null;
            var worldCam = Camera.main;
            if (!parentRect || !worldCam || !hasPinnedWorldAnchor)
            {
                return;
            }

            Vector3 screenPos3 = worldCam.WorldToScreenPoint(new Vector3(pinnedWorldPosition.x, pinnedWorldPosition.y, 0f));
            if (float.IsNaN(screenPos3.x) || float.IsNaN(screenPos3.y) || float.IsInfinity(screenPos3.x) || float.IsInfinity(screenPos3.y))
            {
                return;
            }

            var uiCam = GetUiCamera(parentRect);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, new Vector2(screenPos3.x, screenPos3.y), uiCam, out var localPoint))
            {
                panelRect.anchoredPosition = localPoint;
            }
        }

        static Camera GetUiCamera(RectTransform parentRect)
        {
            var canvas = parentRect ? parentRect.GetComponentInParent<Canvas>() : null;
            return canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        void UnpinAndHide()
        {
            isPinned = false;
            isDragging = false;
            hasPinnedWorldAnchor = false;
            pinnedOwner = null;
            pinnedResource = null;
            Hide();
        }

        void Hide()
        {
            currentOwner = null;
            isDragging = false;
            if (canvasGroup)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        static bool MatchesResource(BuildingProductionEntry building, ResourceProductionEntry resource)
        {
            return building.ResourceKind == resource.Kind
                && string.Equals(building.ResourceId, resource.Id, StringComparison.OrdinalIgnoreCase);
        }

        static bool MatchesKey(ResourceProductionEntry a, ResourceProductionEntry b)
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
