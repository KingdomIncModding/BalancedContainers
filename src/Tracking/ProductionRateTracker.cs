using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KingdomFoundation.Api;
using UnityEngine;

namespace BalancedContainers
{
    public sealed class ProductionRateTracker
    {
        static readonly GameCurrency[] AllCurrencies = (GameCurrency[])Enum.GetValues(typeof(GameCurrency));
        static readonly HashSet<string> PassThroughBuildingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Collector",
            "Distributor",
            "Manifold",
            "Filter",
            "TrackSwitch",
            "Portal",
            "Appraiser",
            "Accountant",
            "Inspector",
            "Outfitter",
            "Stockpile",
            "Armory",
            "Ledger"
        };

        readonly ConditionalWeakTable<WorkspaceObject, BuildingRateHistory> histories =
            new ConditionalWeakTable<WorkspaceObject, BuildingRateHistory>();
        readonly Stack<BuildingSnapshotSlot> activeFrames = new Stack<BuildingSnapshotSlot>(8);
        readonly Stack<BuildingSnapshotSlot> freeSlots = new Stack<BuildingSnapshotSlot>(8);

        int generation;

        public void ResetAll()
        {
            generation++;
            while (activeFrames.Count > 0)
            {
                var frame = activeFrames.Pop();
                frame.Clear();
                freeSlots.Push(frame);
            }
        }

        public object BeginBuildingActivity(WorkspaceObject building)
        {
            if (building == null || building.Kind != WorkspaceObjectKind.Building)
            {
                return null;
            }

            foreach (var active in activeFrames)
            {
                if (ReferenceEquals(active.Building, building))
                {
                    return null;
                }
            }

            var slot = freeSlots.Count > 0 ? freeSlots.Pop() : new BuildingSnapshotSlot();
            slot.Capture(building);
            activeFrames.Push(slot);
            return slot;
        }

        public void EndBuildingActivity(WorkspaceObject building, object token)
        {
            if (!(token is BuildingSnapshotSlot slot))
            {
                return;
            }

            if (activeFrames.Count > 0 && ReferenceEquals(activeFrames.Peek(), slot))
            {
                activeFrames.Pop();
            }

            try
            {
                if (building == null || !ReferenceEquals(slot.Building, building))
                {
                    return;
                }

                double now = Time.time;
                var history = histories.GetValue(building, _ => new BuildingRateHistory());
                history.EnsureGeneration(generation, now);

                RecordProducedItems(building, slot, history, now);
                RecordProducedCurrencies(building, slot, history, now);
                ObserveBuildingOutputSignature(building, history, now);
                ObserveBuildingInputActivity(building, slot, history, now);
            }
            finally
            {
                slot.Clear();
                freeSlots.Push(slot);
            }
        }

        public double GetSecondsSinceLastMovement(WorkspaceObject building)
        {
            if (building == null || !histories.TryGetValue(building, out var history) || history.Generation != generation)
            {
                return 0d;
            }

            return history.LastMovementTime >= 0d ? Math.Max(0d, Time.time - history.LastMovementTime) : 0d;
        }

        public double GetSecondsSinceLastInputActivity(WorkspaceObject building)
        {
            if (building == null || !histories.TryGetValue(building, out var history) || history.Generation != generation)
            {
                return 0d;
            }

            return history.LastInputActivityTime >= 0d ? Math.Max(0d, Time.time - history.LastInputActivityTime) : 0d;
        }

        public bool TryGetLastObservedInput(
            WorkspaceObject building,
            out string itemId,
            out double sellValue,
            out int manaValue,
            out float quality)
        {
            if (building != null && histories.TryGetValue(building, out var history) && history.Generation == generation)
            {
                return history.TryGetLastInputItem(out itemId, out sellValue, out manaValue, out quality);
            }

            itemId = null;
            sellValue = 0d;
            manaValue = 0;
            quality = 1f;
            return false;
        }

        static void ObserveBuildingInputActivity(
            WorkspaceObject building,
            BuildingSnapshotSlot slot,
            BuildingRateHistory history,
            double now)
        {
            if (!string.IsNullOrEmpty(slot.ObservedInputItemId) || slot.ObservedInputSellValue > 0d || slot.ObservedInputManaValue > 0)
            {
                history.ObserveInputItem(
                    slot.ObservedInputItemId,
                    slot.ObservedInputSellValue,
                    slot.ObservedInputManaValue,
                    slot.ObservedInputQuality,
                    now);
            }

            var inputs = building.Inputs;
            int afterInputBuffered = inputs != null && inputs.Count > 0 && inputs[0] != null
                ? inputs[0].BufferedItemCount
                : 0;
            var prod = building.Production;
            float afterProgress = prod?.Progress ?? 0f;
            int afterStoredValue = prod?.StoredValue ?? 0;

            if (slot.BeforeInputBuffered > afterInputBuffered
                || Math.Abs(afterProgress - slot.BeforeProgress) > 0.0001f
                || afterStoredValue != slot.BeforeStoredValue)
            {
                history.RecordInputActivity(now);
            }
        }

        static void ObserveBuildingOutputSignature(WorkspaceObject building, BuildingRateHistory history, double now)
        {
            var outputs = building.Outputs;
            if (outputs == null || outputs.Count == 0)
            {
                return;
            }

            long sig = 17;
            for (int i = 0; i < outputs.Count; i++)
            {
                sig = (sig * 31) + (outputs[i]?.BufferedItemCount ?? 0);
            }

            history.ObserveOutputSignature(sig, now);
        }

        public void OnCurrencyAdded(GameCurrency currency, long amount)
        {
            if (amount <= 0 || activeFrames.Count == 0)
            {
                return;
            }

            int idx = (int)currency;
            if ((uint)idx < BuildingSnapshotSlot.CurrencySlotCount)
            {
                activeFrames.Peek().DirectCurrencyAdded[idx] += amount;
            }
        }

        public void AccumulateBuildingRates(
            WorkspaceObject building,
            double windowSeconds,
            IDictionary<string, double> itemTotals,
            IDictionary<GameCurrency, double> currencyTotals)
        {
            if (building == null || !histories.TryGetValue(building, out var history) || history.Generation != generation)
            {
                return;
            }

            history.AccumulateRates(Time.time, windowSeconds, itemTotals, currencyTotals);
        }

        static void RecordProducedItems(
            WorkspaceObject building,
            BuildingSnapshotSlot slot,
            BuildingRateHistory history,
            double now)
        {
            if (IsNonProducerLogistics(building))
            {
                return;
            }

            var outputs = building.Outputs;
            if (outputs == null || slot.OutputCount <= 0)
            {
                return;
            }

            int count = slot.OutputCount < outputs.Count ? slot.OutputCount : outputs.Count;
            for (int i = 0; i < count; i++)
            {
                var port = outputs[i];
                if (port == null)
                {
                    continue;
                }

                int after = port.BufferedItemCount;
                int delta = after - slot.BeforeBufferedCounts[i];
                if (delta <= 0)
                {
                    continue;
                }

                string itemId = ResolveProducedItemId(building, port);
                if (!string.IsNullOrEmpty(itemId))
                {
                    history.RecordItem(itemId, delta, now);
                }
            }
        }

        static bool IsNonProducerLogistics(WorkspaceObject building)
        {
            if (building == null || building.IsJunction)
            {
                return true;
            }

            var prod = building.Production;
            if (prod != null && (prod.IsStockpile || prod.IsArmory))
            {
                return true;
            }

            return !string.IsNullOrEmpty(building.TypeId) && PassThroughBuildingTypes.Contains(building.TypeId);
        }

        static string ResolveProducedItemId(WorkspaceObject building, WorkspacePort port)
        {
            var buffered = port.BufferedItems;
            if (buffered != null && buffered.Count > 0)
            {
                var last = buffered[buffered.Count - 1];
                if (last?.Type != null)
                {
                    return last.Type.Identifier;
                }
            }

            var avail = port.AvailableItem;
            if (avail?.Type != null)
            {
                return avail.Type.Identifier;
            }

            var prod = building.Production;
            if (prod == null)
            {
                return null;
            }

            if (prod.Resource != null)
            {
                return prod.Resource.Identifier;
            }

            if (prod.SelectedForestResource != null)
            {
                return prod.SelectedForestResource.Identifier;
            }

            return prod.Recipe?.PreviewResultItem?.Identifier;
        }

        static void RecordProducedCurrencies(
            WorkspaceObject building,
            BuildingSnapshotSlot slot,
            BuildingRateHistory history,
            double now)
        {
            var prod = building.Production;
            if (prod == null)
            {
                return;
            }

            RecordSingleCurrency(history, now, GameCurrency.Gold, slot.BeforeGold, prod.CollectedCurrency(GameCurrency.Gold), slot.DirectCurrencyAdded);
            RecordSingleCurrency(history, now, GameCurrency.Mana, slot.BeforeMana, prod.CollectedCurrency(GameCurrency.Mana), slot.DirectCurrencyAdded);
            RecordSingleCurrency(history, now, GameCurrency.Crown, slot.BeforeCrowns, prod.CollectedCurrency(GameCurrency.Crown), slot.DirectCurrencyAdded);
            RecordSingleCurrency(history, now, GameCurrency.Requisition, slot.BeforeRequisition, prod.CollectedCurrency(GameCurrency.Requisition), slot.DirectCurrencyAdded);

            for (int i = 0; i < AllCurrencies.Length; i++)
            {
                var c = AllCurrencies[i];
                if (c == GameCurrency.Gold || c == GameCurrency.Mana || c == GameCurrency.Crown || c == GameCurrency.Requisition)
                {
                    continue;
                }

                int idx = (int)c;
                if ((uint)idx < BuildingSnapshotSlot.CurrencySlotCount && slot.DirectCurrencyAdded[idx] > 0)
                {
                    history.RecordCurrency(c, slot.DirectCurrencyAdded[idx], now);
                }
            }
        }

        static void RecordSingleCurrency(
            BuildingRateHistory history,
            double now,
            GameCurrency currency,
            long beforeCollected,
            long afterCollected,
            long[] directAdded)
        {
            long collectedDelta = afterCollected - beforeCollected;
            long netEarned = 0;

            if (collectedDelta > 0)
            {
                netEarned += collectedDelta;
            }

            int idx = (int)currency;
            if ((uint)idx < BuildingSnapshotSlot.CurrencySlotCount)
            {
                long direct = directAdded[idx];
                if (direct > 0)
                {
                    long drained = collectedDelta < 0 ? -collectedDelta : 0;
                    long freshDirect = direct - drained;
                    if (freshDirect > 0)
                    {
                        netEarned += freshDirect;
                    }
                }
            }

            if (netEarned > 0)
            {
                history.RecordCurrency(currency, netEarned, now);
            }
        }
    }
}

