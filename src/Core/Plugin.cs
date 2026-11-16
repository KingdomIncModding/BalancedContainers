using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using KingdomFoundation.Api;
using KingdomFoundation.Json;
using UnityEngine;

namespace BalancedContainers
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency("urakka.kingdominc.foundation", "0.14.5")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "urakka.kingdominc.balancedcontainers";
        public const string ModName = "Balanced Containers";
        public const string ModVersion = "0.1.0";

        readonly List<IDisposable> subscriptions = new List<IDisposable>();
        readonly List<ContainerHeaderOverlay> activeOverlays = new List<ContainerHeaderOverlay>();

        ContainerConfig containerConfig;
        ContainerTargetStore targetStore;
        ProductionRateTracker rateTracker;
        ContainerProductionCalculator calculator;

        void Awake()
        {
            containerConfig = new ContainerConfig(Config);
            containerConfig.RegisterModConfig(ModGuid, ModName);
            containerConfig.Changed += MarkAllHeadersDirty;

            targetStore = new ContainerTargetStore();
            rateTracker = new ProductionRateTracker();
            calculator = new ContainerProductionCalculator(rateTracker);

            RegisterHooks();
            EnsureAllActiveContainersHaveOverlays();

            Logger.LogInfo($"{ModName} {ModVersion} initialized.");
        }

        void OnDestroy()
        {

            if (containerConfig != null)
            {
                containerConfig.Changed -= MarkAllHeadersDirty;
            }

            for (int i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i]?.Dispose();
            }

            subscriptions.Clear();

            for (int i = activeOverlays.Count - 1; i >= 0; i--)
            {
                var overlay = activeOverlays[i];
                if (overlay)
                {
                    Destroy(overlay);
                }
            }

            activeOverlays.Clear();
            TargetSelectorPopup.DestroyIfCreated();
            ContainerHoverTooltip.DestroyIfCreated();
        }

        void RegisterHooks()
        {
            var hooks = FoundationApi.GameHooks;
            if (hooks != null)
            {
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.DecorationSerialize, after: OnDecorationSerialize));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.DecorationDeserialize, after: OnDecorationDeserialize));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.DecorationCopy, after: OnDecorationCopy));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.DecorationStyleUpdate, after: OnDecorationStyleUpdate));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.DecorationPopupOpen, after: OnDecorationPopupOpen));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.WorkspaceShown, after: OnWorkspaceShown));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.KingdomDeserialized, after: OnKingdomDeserialized));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.BuildingBound, after: _ => MarkAllHeadersDirty()));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.BuildingUpdatePorts, after: _ => MarkAllHeadersDirty()));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.GhostCommit, after: _ => MarkAllHeadersDirty()));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.PlacementOverlapRect, after: OnPlacementOverlapRect));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.PlacementOverlapElement, after: OnPlacementOverlapElement));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.Escape, before: OnUiCancelOrEscape));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.Cancel, before: OnUiCancelOrEscape));
                Subscribe(hooks.Subscribe(ModGuid, GameHookPoints.CurrencyAdded, after: OnCurrencyAdded));
            }

            var exec = FoundationApi.ExecutionHooks;
            if (exec != null)
            {
                var sub = exec.Subscribe(
                    ModGuid,
                    GameExecutionPoints.BuildingActivity,
                    call => rateTracker.BeginBuildingActivity(call.Subject as WorkspaceObject),
                    (call, token) => rateTracker.EndBuildingActivity(call.Subject as WorkspaceObject, token));
                Subscribe(sub);
            }
        }

        void Subscribe(IDisposable subscription)
        {
            if (subscription != null)
            {
                subscriptions.Add(subscription);
            }
        }

        void OnDecorationSerialize(GameHookCall call)
        {
            if (call.Subject is WorkspaceObject container)
            {
                targetStore.WriteToJson(container, call.Result<JsonValue>());
            }
        }

        void OnDecorationDeserialize(GameHookCall call)
        {
            var json = call.Argument<JsonValue>(0);
            var container = call.Result<WorkspaceObject>();
            if (container != null)
            {
                targetStore.ReadFromJson(json, container);
            }
        }

        void OnDecorationCopy(GameHookCall call)
        {
            if (call.Subject is WorkspaceObject source)
            {
                targetStore.Copy(source, call.Result<WorkspaceObject>());
            }
        }

        void OnDecorationStyleUpdate(GameHookCall call)
        {
            var view = call.View;
            if (!view)
            {
                return;
            }

            var obj = FoundationApi.WorkspaceAccess?.FromView(view);
            if (!ContainerTargetStore.IsSupportedContainer(obj))
            {
                return;
            }

            AttachOverlay(obj, view);
        }

        void OnDecorationPopupOpen(GameHookCall call)
        {
            var popup = call.View;
            if (!popup)
            {
                return;
            }

            var panel = popup.GetComponent<DecoOptionsTargetPanel>();
            if (!panel)
            {
                panel = popup.gameObject.AddComponent<DecoOptionsTargetPanel>();
            }

            panel.Initialize(popup, targetStore, ApplyTargetSelection, DismissAutoPrompt);
            panel.Refresh();

            var current = FoundationApi.Decorations?.Current(popup);
            if (containerConfig.AutoOpenOnContainerCreate.Value && targetStore.ShouldAutoPrompt(current))
            {
                var selector = TargetSelectorPopup.GetOrCreate();
                if (selector)
                {
                    selector.OpenFor(
                        current,
                        targetStore.GetTarget(current),
                        (targetContainer, selected) =>
                        {
                            ApplyTargetSelection(targetContainer, selected);
                            if (panel)
                            {
                                panel.Refresh();
                            }
                        },
                        DismissAutoPrompt,
                        playSound: false);
                }
            }
        }

        void OnWorkspaceShown(GameHookCall call)
        {
            ContainerHoverTooltip.UnpinIfPinned();
            EnsureAllActiveContainersHaveOverlays();
            MarkAllHeadersDirty();
        }

        void OnKingdomDeserialized(GameHookCall call)
        {
            ContainerHoverTooltip.UnpinIfPinned();
            rateTracker.ResetAll();
            ContainerUiTheme.InvalidateCache();
            StartCoroutine(RefreshNextFrame());
        }

        IEnumerator RefreshNextFrame()
        {
            yield return null;
            EnsureAllActiveContainersHaveOverlays();
            MarkAllHeadersDirty();
        }

        void OnCurrencyAdded(GameHookCall call)
        {
            var currency = call.Argument<GameCurrency>(0);
            long amount = call.Argument<long>(1);
            rateTracker.OnCurrencyAdded(currency, amount);
        }

        void OnUiCancelOrEscape(GameHookCall call)
        {
            if (ContainerHoverTooltip.UnpinIfPinned())
            {
                call.Cancel();
            }
        }

        void OnPlacementOverlapRect(GameHookCall call)
        {
            if (call.Result<bool>())
            {
                return;
            }

            var pos = call.Argument<Vector2Int>(0);
            var size = call.Argument<Vector2Int>(1);
            if (ContainerPlacementBlocker.OverlapsAnyBar(activeOverlays, pos, size))
            {
                call.SetResult(true);
            }
        }

        void OnPlacementOverlapElement(GameHookCall call)
        {
            if (call.Result<bool>())
            {
                return;
            }

            if (FoundationApi.UiAccess != null && FoundationApi.UiAccess.IsMovingDecoration)
            {
                return;
            }

            var view = call.Argument<Component>(0);
            var element = view ? FoundationApi.WorkspaceAccess?.FromView(view) : null;
            if (element == null)
            {
                return;
            }

            if (ContainerPlacementBlocker.OverlapsAnyBar(activeOverlays, element.Position, Vector2Int.RoundToInt(element.Size)))
            {
                call.SetResult(true);
            }
        }

        void ApplyTargetSelection(WorkspaceObject container, ContainerTarget target)
        {
            if (!ContainerTargetStore.IsSupportedContainer(container))
            {
                return;
            }

            targetStore.SetTarget(container, target, markDismissed: true);
            if (container.View)
            {
                AttachOverlay(container, container.View);
            }

            MarkAllHeadersDirty();
        }

        void DismissAutoPrompt(WorkspaceObject container)
        {
            targetStore.MarkDismissed(container);
        }

        void EnsureAllActiveContainersHaveOverlays()
        {
            var ws = FoundationApi.WorkspaceAccess;
            if (ws == null || !ws.IsReady)
            {
                return;
            }

            var objects = ws.ActiveObjects;
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (ContainerTargetStore.IsSupportedContainer(obj) && obj.View)
                {
                    AttachOverlay(obj, obj.View);
                }
            }
        }

        void AttachOverlay(WorkspaceObject container, Component view)
        {
            if (!view || !ContainerTargetStore.IsSupportedContainer(container))
            {
                return;
            }

            var overlay = view.GetComponent<ContainerHeaderOverlay>();
            if (!overlay)
            {
                overlay = view.gameObject.AddComponent<ContainerHeaderOverlay>();
                activeOverlays.Add(overlay);
            }

            overlay.Initialize(container, targetStore, calculator, containerConfig, OnOverlayDestroyed);
        }

        void OnOverlayDestroyed(ContainerHeaderOverlay overlay)
        {
            activeOverlays.Remove(overlay);
        }

        void MarkAllHeadersDirty()
        {
            for (int i = activeOverlays.Count - 1; i >= 0; i--)
            {
                var overlay = activeOverlays[i];
                if (!overlay)
                {
                    activeOverlays.RemoveAt(i);
                    continue;
                }

                overlay.MarkDirty();
            }
        }
    }
}

