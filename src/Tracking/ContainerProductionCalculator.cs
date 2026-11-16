using System;
using System.Collections.Generic;
using KingdomFoundation.Api;
using UnityEngine;

namespace BalancedContainers
{
    public sealed class ContainerProductionCalculator
    {
        readonly ProductionRateTracker tracker;

        public ContainerProductionCalculator(ProductionRateTracker tracker)
        {
            this.tracker = tracker;
        }

        public IReadOnlyList<ResourceProductionEntry> Compute(
            WorkspaceObject container,
            ContainerTarget target,
            double windowSeconds)
        {
            return ComputeSnapshot(container, target, windowSeconds).SummaryEntries;
        }

        public ContainerProductionSnapshot ComputeSnapshot(
            WorkspaceObject container,
            ContainerTarget target,
            double windowSeconds,
            bool perMinute = false,
            double operationalToleranceRatio = 0.95d)
        {
            if (container == null || !container.IsContainer || FoundationApi.WorkspaceAccess == null || !FoundationApi.WorkspaceAccess.IsReady)
            {
                return ContainerProductionSnapshot.Empty;
            }

            var itemCurrentTotal = new Dictionary<string, double>(StringComparer.Ordinal);
            var itemPotentialTotal = new Dictionary<string, double>(StringComparer.Ordinal);
            var currencyCurrentTotal = new Dictionary<GameCurrency, double>();
            var currencyPotentialTotal = new Dictionary<GameCurrency, double>();
            var buildingEntries = new List<BuildingProductionEntry>();
            var samplesByBuildingKey = new Dictionary<string, BuildingDiagnosticSample>(StringComparer.Ordinal);
            var activeDiagnosticCurrencies = new HashSet<GameCurrency>();

            var bItemCur = new Dictionary<string, double>(StringComparer.Ordinal);
            var bItemPot = new Dictionary<string, double>(StringComparer.Ordinal);
            var bCurCur = new Dictionary<GameCurrency, double>();
            var bCurPot = new Dictionary<GameCurrency, double>();
            var bConsumeCur = new Dictionary<string, double>(StringComparer.Ordinal);
            var bConsumePot = new Dictionary<string, double>(StringComparer.Ordinal);
            var bItemIds = new HashSet<string>(StringComparer.Ordinal);

            var overlaps = FoundationApi.WorkspaceAccess.Overlap(
                container.Position,
                Vector2Int.RoundToInt(container.Size));

            for (int i = 0; i < overlaps.Count; i++)
            {
                var obj = overlaps[i];
                if (obj == null || obj.Kind != WorkspaceObjectKind.Building)
                {
                    continue;
                }

                bItemCur.Clear();
                bItemPot.Clear();
                bCurCur.Clear();
                bCurPot.Clear();
                bConsumeCur.Clear();
                bConsumePot.Clear();
                bItemIds.Clear();

                string buildingName = ContainerUiTheme.TranslateName(obj.Name);
                tracker?.AccumulateBuildingRates(obj, windowSeconds, bItemCur, bCurCur);
                AddBuildingPotential(obj, tracker, bItemPot, bCurPot);
                AddBuildingConsumption(obj, tracker, bItemCur, bItemPot, bCurCur, bCurPot, bConsumeCur, bConsumePot);
                samplesByBuildingKey[ResourceStatusAnalyzer.MakeBuildingKey(obj.Position, buildingName)] = CaptureBuildingDiagnostics(obj, buildingName, tracker);

                bool hasPrimaryCurrency = TryGetPrimaryOutputCurrency(obj.Production, out var primaryCurrency);
                bool dynamicCurrencyBuilding = obj.Production != null && !obj.Production.IsRecipe;
                bool goldCapped = IsShopGoldCapped(obj.Production);

                foreach (GameCurrency currency in Enum.GetValues(typeof(GameCurrency)))
                {
                    bCurCur.TryGetValue(currency, out double cur);
                    bCurPot.TryGetValue(currency, out double pot);
                    bool keepZeroPrimary = hasPrimaryCurrency && currency == primaryCurrency;
                    if (cur <= 0d && pot <= 0d && !keepZeroPrimary)
                    {
                        continue;
                    }

                    if (dynamicCurrencyBuilding && pot < cur && !(currency == GameCurrency.Gold && goldCapped))
                    {
                        pot = cur;
                    }

                    if (keepZeroPrimary)
                    {
                        activeDiagnosticCurrencies.Add(currency);
                    }

                    AddCurrencyTotal(currencyCurrentTotal, currency, cur);
                    AddCurrencyTotal(currencyPotentialTotal, currency, pot);

                    bool isPrimary = target != null && target.IsAssigned && target.Matches(ContainerTargetKind.Currency, currency.ToString());
                    buildingEntries.Add(new BuildingProductionEntry(
                        buildingName,
                        obj.Icon,
                        obj.StackSize,
                        obj.Position,
                        obj.Size,
                        ContainerTargetKind.Currency,
                        currency.ToString(),
                        ContainerTarget.FormatCurrencyName(currency),
                        GetCurrencyIcon(currency),
                        isPrimary,
                        isConsumer: false)
                    {
                        CurrentPerSecond = cur,
                        PotentialPerSecond = pot
                    });
                }

                foreach (string id in bItemPot.Keys)
                {
                    bItemIds.Add(id);
                }

                foreach (string id in bItemCur.Keys)
                {
                    bItemIds.Add(id);
                }

                foreach (string itemId in bItemIds)
                {
                    bItemCur.TryGetValue(itemId, out double cur);
                    bItemPot.TryGetValue(itemId, out double pot);
                    if (cur <= 0d && pot <= 0d)
                    {
                        continue;
                    }

                    AddItemTotal(itemCurrentTotal, itemId, cur);
                    AddItemTotal(itemPotentialTotal, itemId, pot);

                    var itemInfo = ResolveItemInfo(itemId);
                    bool isPrimary = target != null && target.IsAssigned && target.Matches(ContainerTargetKind.Item, itemId);
                    buildingEntries.Add(new BuildingProductionEntry(
                        buildingName,
                        obj.Icon,
                        obj.StackSize,
                        obj.Position,
                        obj.Size,
                        ContainerTargetKind.Item,
                        itemId,
                        itemInfo.DisplayName,
                        itemInfo.Icon,
                        isPrimary,
                        isConsumer: false)
                    {
                        CurrentPerSecond = cur,
                        PotentialPerSecond = pot
                    });
                }

                foreach (var kv in bConsumePot)
                {
                    bConsumeCur.TryGetValue(kv.Key, out double consumeCur);
                    double consumePot = kv.Value;
                    if (consumeCur <= 0d && consumePot <= 0d)
                    {
                        continue;
                    }

                    var itemInfo = ResolveItemInfo(kv.Key);
                    bool isPrimary = target != null && target.IsAssigned && target.Matches(ContainerTargetKind.Item, kv.Key);
                    buildingEntries.Add(new BuildingProductionEntry(
                        buildingName,
                        obj.Icon,
                        obj.StackSize,
                        obj.Position,
                        obj.Size,
                        ContainerTargetKind.Item,
                        kv.Key,
                        itemInfo.DisplayName,
                        itemInfo.Icon,
                        isPrimary,
                        isConsumer: true)
                    {
                        CurrentPerSecond = consumeCur,
                        PotentialPerSecond = consumePot
                    });
                }
            }

            var summaryEntries = BuildSummaryEntries(
                target,
                itemCurrentTotal,
                itemPotentialTotal,
                currencyCurrentTotal,
                currencyPotentialTotal,
                activeDiagnosticCurrencies);

            var resourceOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < summaryEntries.Count; i++)
            {
                resourceOrder[MakeResourceKey(summaryEntries[i].Kind, summaryEntries[i].Id)] = i;
            }

            buildingEntries.Sort((a, b) =>
            {
                int aOrd = resourceOrder.TryGetValue(MakeResourceKey(a.ResourceKind, a.ResourceId), out int av) ? av : int.MaxValue;
                int bOrd = resourceOrder.TryGetValue(MakeResourceKey(b.ResourceKind, b.ResourceId), out int bv) ? bv : int.MaxValue;
                if (aOrd != bOrd)
                {
                    return aOrd.CompareTo(bOrd);
                }

                int roleCmp = a.IsConsumer.CompareTo(b.IsConsumer);
                if (roleCmp != 0)
                {
                    return roleCmp;
                }

                int yCmp = b.Position.y.CompareTo(a.Position.y);
                if (yCmp != 0)
                {
                    return yCmp;
                }

                int xCmp = a.Position.x.CompareTo(b.Position.x);
                if (xCmp != 0)
                {
                    return xCmp;
                }

                return string.Compare(a.BuildingName, b.BuildingName, StringComparison.OrdinalIgnoreCase);
            });

            var diagnoses = ResourceStatusAnalyzer.AnalyzeAll(
                summaryEntries,
                buildingEntries,
                samplesByBuildingKey,
                perMinute,
                operationalToleranceRatio);

            return new ContainerProductionSnapshot(summaryEntries, buildingEntries, diagnoses);
        }

        static BuildingDiagnosticSample CaptureBuildingDiagnostics(
            WorkspaceObject building,
            string buildingName,
            ProductionRateTracker rateTracker)
        {
            var prod = building?.Production;
            if (prod == null)
            {
                return null;
            }

            bool hasUnconnectedOutput = false;
            bool hasRejectedOutput = false;
            bool hasFullOutputBuffer = false;
            bool canBufferAnyOutput = false;

            if (building.Outputs != null && building.Outputs.Count > 0)
            {
                for (int i = 0; i < building.Outputs.Count; i++)
                {
                    var output = building.Outputs[i];
                    if (output == null)
                    {
                        continue;
                    }

                    if (output.CanBuffer)
                    {
                        canBufferAnyOutput = true;
                    }
                    else
                    {
                        hasFullOutputBuffer = true;
                    }

                    if (output.ConnectedPipe == null)
                    {
                        hasUnconnectedOutput = true;
                    }
                    else if (output.ConnectedPipe.ItemRejected)
                    {
                        hasRejectedOutput = true;
                    }
                }
            }

            bool visualOutputFull = building.View != null
                && FoundationApi.WorkspaceVisuals != null
                && FoundationApi.WorkspaceVisuals.IsOutputFullIndicatorVisible(building.View);
            bool shopGoldCapped = IsShopGoldCapped(prod);
            bool storageFull = (prod.IsStockpile && prod.HasStorageContents && prod.StoredItemCount >= prod.StorageCapacity)
                || (prod.IsArmory && prod.HasArmoryType && FoundationApi.Kingdom != null && !prod.CanStoreArmoryItem);
            bool progressBlocked = prod.Progress >= 1f && prod.OutputCount > 0 && !canBufferAnyOutput;

            bool isSaturated = visualOutputFull
                || shopGoldCapped
                || storageFull
                || progressBlocked
                || hasFullOutputBuffer
                || hasRejectedOutput;

            bool isStarved = false;
            bool hasUnconnectedInput = false;
            bool missingCostFunds = false;
            bool quotaBlocked = false;
            bool missingConfiguration = false;
            bool isActivelyRunning = false;
            var missingReagentIds = new List<string>();
            var missingReagents = new List<string>();

            double secondsSinceMovement = rateTracker != null ? rateTracker.GetSecondsSinceLastMovement(building) : 0d;

            if (prod.IsRecipe)
            {
                if (!prod.HasRecipe)
                {
                    missingConfiguration = true;
                }
                else if (prod.Crafting || prod.HasCraftingInput)
                {
                    isActivelyRunning = prod.CanAutoCraft;
                    missingConfiguration = !prod.CanAutoCraft;
                }
                else
                {
                    var recipeInputs = prod.Recipe?.Inputs;
                    for (int i = 0; i < prod.ReagentCount; i++)
                    {
                        bool disconnected = building.Inputs == null
                            || i >= building.Inputs.Count
                            || building.Inputs[i]?.ConnectedPipe == null;
                        bool lowBuffer = i >= prod.InputCount || prod.BufferedInputCount(i) < prod.ReagentAmount(i);
                        if (disconnected || lowBuffer)
                        {
                            isStarved = true;
                            if (disconnected)
                            {
                                hasUnconnectedInput = true;
                            }

                            var reagentItem = recipeInputs != null && i < recipeInputs.Count ? recipeInputs[i]?.Item : null;
                            if (reagentItem != null)
                            {
                                string reagentId = reagentItem.Identifier;
                                if (!string.IsNullOrEmpty(reagentId) && !missingReagentIds.Contains(reagentId))
                                {
                                    missingReagentIds.Add(reagentId);
                                }

                                string reagentName = ContainerUiTheme.TranslateName(
                                    reagentItem.GetDisplayName() ?? reagentItem.PreviewDisplayName,
                                    reagentId);
                                if (!string.IsNullOrEmpty(reagentName) && !missingReagents.Contains(reagentName))
                                {
                                    missingReagents.Add(reagentName);
                                }
                            }
                        }
                    }

                    var cost = prod.CraftingCost;
                    if (cost != null && FoundationApi.Kingdom != null && !FoundationApi.Kingdom.CanAfford(cost))
                    {
                        missingCostFunds = true;
                        isStarved = true;
                    }

                    if (!isStarved)
                    {
                        isActivelyRunning = prod.Progress > 0f || prod.CanAutoCraft;
                    }
                }
            }
            else if (prod.IsResource || prod.IsForest)
            {
                bool hasRes = prod.IsForest ? prod.SelectedForestResource != null : prod.HasResource;
                if (!hasRes || (!prod.IsForest && prod.QueuedResourceCount <= 0 && !prod.CanAutoCraft))
                {
                    missingConfiguration = true;
                }
                else if (FoundationApi.Kingdom != null && prod.ResourceQuotaMultiplier <= 0.001f)
                {
                    quotaBlocked = true;
                    isStarved = true;
                }
                else
                {
                    isActivelyRunning = true;
                }
            }
            else if (prod.IsShop || prod.IsRestaurant || prod.IsDisenchanter || prod.IsLuxuryShop || prod.IsMercenaryGuild || prod.IsRequisitionOffice)
            {
                bool hasInputPipe = false;
                bool noInput = false;
                double inputIdleSeconds = (prod.IsDisenchanter || prod.IsRequisitionOffice) && rateTracker != null
                    ? rateTracker.GetSecondsSinceLastInputActivity(building)
                    : secondsSinceMovement;
                if (building.Inputs != null && building.Inputs.Count > 0)
                {
                    var input = building.Inputs[0];
                    hasInputPipe = input?.ConnectedPipe != null;
                    if (!hasInputPipe)
                    {
                        hasUnconnectedInput = true;
                        noInput = true;
                    }
                    else if (input.BufferedItemCount == 0 && prod.Progress <= 0.01f && inputIdleSeconds > 1.5d)
                    {
                        noInput = true;
                    }
                }

                bool passiveOnlyOk = (prod.IsDisenchanter || prod.IsRequisitionOffice) && prod.PassiveGain > 0f && !hasInputPipe;
                if (noInput && !passiveOnlyOk && (!prod.IsShop || prod.CollectedGold <= 0))
                {
                    isStarved = true;
                    string upstreamId = prod.IsRequisitionOffice ? prod.ContractedItem?.Identifier : ResolveInputSource(building, rateTracker).ItemId;
                    if (!string.IsNullOrEmpty(upstreamId))
                    {
                        if (!missingReagentIds.Contains(upstreamId))
                        {
                            missingReagentIds.Add(upstreamId);
                        }

                        string upstreamName = ResolveItemInfo(upstreamId).DisplayName;
                        if (!string.IsNullOrEmpty(upstreamName) && !missingReagents.Contains(upstreamName))
                        {
                            missingReagents.Add(upstreamName);
                        }
                    }
                }
                else if (!shopGoldCapped)
                {
                    isActivelyRunning = true;
                }
            }
            else
            {
                isActivelyRunning = prod.Progress > 0f || prod.PassiveGain > 0f || secondsSinceMovement < 2.0d;
            }

            return new BuildingDiagnosticSample(
                buildingName,
                isActivelyRunning,
                isSaturated,
                shopGoldCapped,
                hasUnconnectedOutput,
                hasRejectedOutput,
                isStarved,
                hasUnconnectedInput,
                missingCostFunds,
                quotaBlocked,
                missingConfiguration,
                secondsSinceMovement,
                missingReagentIds,
                missingReagents);
        }

        static List<ResourceProductionEntry> BuildSummaryEntries(
            ContainerTarget target,
            Dictionary<string, double> itemCurrent,
            Dictionary<string, double> itemPotential,
            Dictionary<GameCurrency, double> currencyCurrent,
            Dictionary<GameCurrency, double> currencyPotential,
            HashSet<GameCurrency> activeDiagnosticCurrencies)
        {
            var entries = new List<ResourceProductionEntry>();
            var usedCurrencies = new HashSet<GameCurrency>();
            var usedItems = new HashSet<string>(StringComparer.Ordinal);

            if (target != null && target.IsAssigned)
            {
                if (target.TryGetCurrency(out var targetCurrency))
                {
                    usedCurrencies.Add(targetCurrency);
                    currencyCurrent.TryGetValue(targetCurrency, out double cur);
                    currencyPotential.TryGetValue(targetCurrency, out double pot);
                    entries.Add(CreateCurrencyEntry(targetCurrency, cur, pot, isPrimary: true));
                }
                else
                {
                    usedItems.Add(target.Id);
                    itemCurrent.TryGetValue(target.Id, out double cur);
                    itemPotential.TryGetValue(target.Id, out double pot);
                    entries.Add(CreateItemEntry(target.Id, cur, pot, isPrimary: true));
                }
            }

            foreach (GameCurrency currency in Enum.GetValues(typeof(GameCurrency)))
            {
                if (usedCurrencies.Contains(currency))
                {
                    continue;
                }

                currencyCurrent.TryGetValue(currency, out double cur);
                currencyPotential.TryGetValue(currency, out double pot);
                bool hasDiagnosticProducer = activeDiagnosticCurrencies != null && activeDiagnosticCurrencies.Contains(currency);
                if (cur <= 0d && pot <= 0d && !hasDiagnosticProducer)
                {
                    continue;
                }

                usedCurrencies.Add(currency);
                entries.Add(CreateCurrencyEntry(currency, cur, pot, isPrimary: false));
            }

            var allItemIds = new HashSet<string>(itemPotential.Keys, StringComparer.Ordinal);
            foreach (string id in itemCurrent.Keys)
            {
                allItemIds.Add(id);
            }

            var nonTargetItems = new List<ResourceProductionEntry>();
            foreach (string itemId in allItemIds)
            {
                if (usedItems.Contains(itemId))
                {
                    continue;
                }

                itemCurrent.TryGetValue(itemId, out double cur);
                itemPotential.TryGetValue(itemId, out double pot);
                if (cur <= 0d && pot <= 0d)
                {
                    continue;
                }

                nonTargetItems.Add(CreateItemEntry(itemId, cur, pot, isPrimary: false));
            }

            nonTargetItems.Sort((a, b) =>
            {
                int potCmp = b.PotentialPerSecond.CompareTo(a.PotentialPerSecond);
                if (potCmp != 0)
                {
                    return potCmp;
                }

                int curCmp = b.CurrentPerSecond.CompareTo(a.CurrentPerSecond);
                if (curCmp != 0)
                {
                    return curCmp;
                }

                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            entries.AddRange(nonTargetItems);
            return entries;
        }

        static string MakeResourceKey(ContainerTargetKind kind, string id)
        {
            return $"{(int)kind}:{id}";
        }

        static Sprite GetCurrencyIcon(GameCurrency currency)
        {
            return currency == GameCurrency.Gold
                ? FoundationApi.UiAccess?.CurrencyIcon("Gold", 0) ?? currency.GetIcon()
                : currency.GetIcon();
        }

        static (string DisplayName, Sprite Icon) ResolveItemInfo(string itemId)
        {
            var item = FoundationApi.ItemAccess?.Find(itemId);
            string displayName = item?.GetDisplayName();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = item?.PreviewDisplayName;
            }

            return (ContainerUiTheme.TranslateName(displayName, itemId), item?.GetIcon());
        }

        static ResourceProductionEntry CreateCurrencyEntry(
            GameCurrency currency,
            double current,
            double potential,
            bool isPrimary)
        {
            return new ResourceProductionEntry(
                ContainerTargetKind.Currency,
                currency.ToString(),
                ContainerTarget.FormatCurrencyName(currency),
                GetCurrencyIcon(currency),
                isPrimary)
            {
                CurrentPerSecond = current,
                PotentialPerSecond = potential
            };
        }

        static ResourceProductionEntry CreateItemEntry(
            string itemId,
            double current,
            double potential,
            bool isPrimary)
        {
            var info = ResolveItemInfo(itemId);
            return new ResourceProductionEntry(
                ContainerTargetKind.Item,
                itemId,
                info.DisplayName,
                info.Icon,
                isPrimary)
            {
                CurrentPerSecond = current,
                PotentialPerSecond = potential
            };
        }

        static bool IsShopGoldCapped(GameProductionState prod)
        {
            if (prod == null || !prod.IsShop)
            {
                return false;
            }

            long cap = prod.MaximumGold > 0 ? prod.MaximumGold : prod.ShopGoldCap;
            return cap > 0 && prod.CollectedGold >= cap;
        }

        static bool TryGetPrimaryOutputCurrency(GameProductionState prod, out GameCurrency currency)
        {
            if (prod == null)
            {
                currency = default;
                return false;
            }

            if (prod.IsShop || prod.IsRestaurant || prod.IsLuxuryShop || prod.IsMercenaryGuild)
            {
                currency = GameCurrency.Gold;
                return true;
            }

            if (prod.IsDisenchanter)
            {
                currency = GameCurrency.Mana;
                return true;
            }

            if (prod.IsRequisitionOffice)
            {
                currency = GameCurrency.Requisition;
                return true;
            }

            currency = default;
            return false;
        }

        static void AddBuildingPotential(
            WorkspaceObject building,
            ProductionRateTracker rateTracker,
            IDictionary<string, double> itemPotential,
            IDictionary<GameCurrency, double> currencyPotential)
        {
            var prod = building.Production;
            if (prod == null)
            {
                return;
            }

            if (prod.IsRecipe && prod.Recipe != null)
            {
                AddRecipePotential(prod, itemPotential, currencyPotential);
                return;
            }

            if (!prod.IsForest && prod.Resource != null)
            {
                double effective = prod.ResourceEffectiveOutputPerSecond;
                double rate = effective * (1.0d + Math.Max(0.0d, prod.Productivity));
                AddItemTotal(itemPotential, prod.Resource.Identifier, rate);
                return;
            }

            if (prod.IsForest && prod.SelectedForestResource != null)
            {
                double effective = prod.ResourceEffectiveOutputPerSecond;
                double rate = effective * (1.0d + Math.Max(0.0d, prod.Productivity));
                AddItemTotal(itemPotential, prod.SelectedForestResource.Identifier, rate);
                return;
            }

            if (prod.IsShop)
            {
                if (IsShopGoldCapped(prod))
                {
                    long cap = prod.MaximumGold > 0 ? prod.MaximumGold : prod.ShopGoldCap;
                    double capPerSec = cap * (Math.Max(0.0d, prod.Speed) / 10.0d);
                    if (capPerSec > 0d)
                    {
                        AddCurrencyTotal(currencyPotential, GameCurrency.Gold, capPerSec);
                    }
                }

                return;
            }

            if (prod.IsRestaurant)
            {
                return;
            }

            if (prod.IsDisenchanter)
            {
                double manaPerSec = Math.Max(0.0d, prod.PassiveGain);
                var source = ResolveInputSource(building, rateTracker);
                if (source.ManaValue > 0)
                {
                    double itemRate = Math.Max(0.0d, prod.Speed);
                    manaPerSec += itemRate * source.ManaValue * Math.Max(0.0d, prod.ProfitMultiplier);
                }

                if (manaPerSec > 0d)
                {
                    AddCurrencyTotal(currencyPotential, GameCurrency.Mana, manaPerSec);
                }

                return;
            }

            if (prod.IsRequisitionOffice)
            {
                double reqPerSec = Math.Max(0.0d, prod.PassiveGain);
                if (reqPerSec > 0d)
                {
                    AddCurrencyTotal(currencyPotential, GameCurrency.Requisition, reqPerSec);
                }
            }
        }

        static void AddRecipePotential(
            GameProductionState prod,
            IDictionary<string, double> itemPotential,
            IDictionary<GameCurrency, double> currencyPotential)
        {
            var recipe = prod.Recipe;
            double craftTime = Math.Max(0.01d, recipe.CraftingTime);
            double speed = Math.Max(0.0d, prod.Speed);
            double craftsPerSec = speed / craftTime;
            if (craftsPerSec <= 0d)
            {
                return;
            }

            double prodFactor = prod.ProductivityDisabled
                ? 1.0d
                : 1.0d + Math.Max(0.0d, prod.Productivity) * (prod.IsEquipmentRecipe ? 0.5d : 1.0d);

            var outputs = recipe.Outputs;
            if (outputs != null)
            {
                for (int i = 0; i < outputs.Count; i++)
                {
                    var output = outputs[i];
                    if (output == null || !output.HasOutput || output.Item == null || output.Count <= 0 || output.Probability <= 0f)
                    {
                        continue;
                    }

                    double rate = craftsPerSec * output.Count * output.Probability * prodFactor;
                    AddItemTotal(itemPotential, output.Item.Identifier, rate);
                }
            }

            var reward = recipe.CraftingReward;
            if (reward != null && !reward.IsEmpty)
            {
                var rewardEntries = reward.Entries;
                for (int i = 0; i < rewardEntries.Count; i++)
                {
                    var entry = rewardEntries[i];
                    if (entry != null && entry.Amount > 0 && Enum.TryParse<GameCurrency>(entry.CurrencyId, true, out var currency))
                    {
                        AddCurrencyTotal(currencyPotential, currency, craftsPerSec * entry.Amount);
                    }
                }
            }
        }

        static void AddBuildingConsumption(
            WorkspaceObject building,
            ProductionRateTracker rateTracker,
            IDictionary<string, double> itemCur,
            IDictionary<string, double> itemPot,
            IDictionary<GameCurrency, double> curCur,
            IDictionary<GameCurrency, double> curPot,
            IDictionary<string, double> consumeCur,
            IDictionary<string, double> consumePot)
        {
            var prod = building.Production;
            if (prod == null)
            {
                return;
            }

            if (prod.IsRecipe && prod.Recipe != null)
            {
                var inputs = prod.Recipe.Inputs;
                if (inputs == null || inputs.Count == 0)
                {
                    return;
                }

                double craftTime = Math.Max(0.01d, prod.Recipe.CraftingTime);
                double craftsPerSec = Math.Max(0.0d, prod.Speed) / craftTime;
                if (craftsPerSec <= 0d)
                {
                    return;
                }

                double activityRatio = ComputeActivityRatio(itemCur, itemPot, curCur, curPot);
                for (int i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    if (input?.Item == null || string.IsNullOrEmpty(input.Item.Identifier) || input.Count <= 0)
                    {
                        continue;
                    }

                    double pot = craftsPerSec * input.Count;
                    double cur = pot * activityRatio;
                    AddItemTotal(consumePot, input.Item.Identifier, pot);
                    AddItemTotal(consumeCur, input.Item.Identifier, cur);
                }

                return;
            }

            if (prod.IsDisenchanter)
            {
                double pot = Math.Max(0.0d, prod.Speed);
                var inputSource = ResolveInputSource(building, rateTracker);
                if (pot <= 0d || string.IsNullOrEmpty(inputSource.ItemId))
                {
                    return;
                }

                curCur.TryGetValue(GameCurrency.Mana, out double manaCur);
                curPot.TryGetValue(GameCurrency.Mana, out double manaPot);
                double passive = Math.Max(0.0d, prod.PassiveGain);
                double disenchantCur = Math.Max(0.0d, manaCur - passive);
                double disenchantPot = Math.Max(0.0d, manaPot - passive);
                double ratio = disenchantPot > 0d ? Math.Min(1.5d, Math.Max(0d, disenchantCur / disenchantPot)) : 0d;
                AddItemTotal(consumePot, inputSource.ItemId, pot);
                AddItemTotal(consumeCur, inputSource.ItemId, pot * ratio);
                return;
            }

            if ((prod.IsShop || prod.IsRestaurant || prod.IsLuxuryShop || prod.IsMercenaryGuild)
                && building.Inputs != null
                && building.Inputs.Count > 0)
            {
                var inputSource = ResolveInputSource(building, rateTracker);
                if (string.IsNullOrEmpty(inputSource.ItemId) || inputSource.SellValue <= 0d)
                {
                    return;
                }

                curCur.TryGetValue(GameCurrency.Gold, out double goldCur);
                if (goldCur <= 0d)
                {
                    return;
                }

                double unitGold = ResolveInstantSellerUnitGold(prod, inputSource);
                if (unitGold <= 0d)
                {
                    return;
                }

                double itemsPerSec = goldCur / unitGold;
                AddItemTotal(consumePot, inputSource.ItemId, itemsPerSec);
                AddItemTotal(consumeCur, inputSource.ItemId, itemsPerSec);
                return;
            }

            if (prod.IsRequisitionOffice && prod.ContractedItem != null && !string.IsNullOrEmpty(prod.ContractedItem.Identifier))
            {
                curCur.TryGetValue(GameCurrency.Requisition, out double reqCur);
                double passive = Math.Max(0.0d, prod.PassiveGain);
                double officeCur = Math.Max(0.0d, reqCur - passive);
                if (officeCur <= 0d || prod.ValuePerRequisition <= 0 || prod.ContractedItem.BaseValue <= 0)
                {
                    return;
                }

                double reqPerItem = (prod.ContractedItem.BaseValue * Math.Max(0.0d, prod.ProfitMultiplier)) / prod.ValuePerRequisition;
                if (reqPerItem <= 0d)
                {
                    return;
                }

                double itemsPerSec = officeCur / reqPerItem;
                AddItemTotal(consumePot, prod.ContractedItem.Identifier, itemsPerSec);
                AddItemTotal(consumeCur, prod.ContractedItem.Identifier, itemsPerSec);
            }
        }

        static double ResolveInstantSellerUnitGold(
            GameProductionState prod,
            (string ItemId, double SellValue, int ManaValue, float Quality) inputSource)
        {
            if (prod.IsMercenaryGuild)
            {
                return Math.Max(1.0d, inputSource.SellValue * 0.5d);
            }

            if (prod.IsLuxuryShop)
            {
                double qualityFactor = Math.Max(1.0f, inputSource.Quality) * (inputSource.Quality > 1.2f ? 1.2d : 1.0d);
                return Math.Max(1.0d, inputSource.SellValue * qualityFactor * Math.Max(1.0d, prod.PrestigeSellValueMultiplier));
            }

            if (prod.IsRestaurant)
            {
                double goldPerItem = inputSource.SellValue * Math.Max(0.0d, prod.ProfitMultiplier);
                double crownGoldVal = Math.Max(1.0d, GameCurrency.Crown.GetGoldValue());
                double crownChance = (goldPerItem / crownGoldVal) * Math.Max(0.0d, prod.CrownChanceMultiplier) * 0.5d;
                double goldFactor = crownChance >= 1.0d ? 0.5d : (1.0d - 0.5d * crownChance);
                return Math.Max(1.0d, goldPerItem * goldFactor);
            }

            return Math.Max(1.0d, inputSource.SellValue * Math.Max(0.0d, prod.ProfitMultiplier) * Math.Max(1.0d, prod.PrestigeSellValueMultiplier));
        }

        static double ComputeActivityRatio(
            IDictionary<string, double> itemCur,
            IDictionary<string, double> itemPot,
            IDictionary<GameCurrency, double> curCur,
            IDictionary<GameCurrency, double> curPot)
        {
            double totalPot = 0d;
            double totalCur = 0d;
            foreach (var kv in itemPot)
            {
                totalPot += kv.Value;
                itemCur.TryGetValue(kv.Key, out double c);
                totalCur += c;
            }

            if (totalPot <= 0d)
            {
                foreach (var kv in curPot)
                {
                    totalPot += kv.Value;
                    curCur.TryGetValue(kv.Key, out double c);
                    totalCur += c;
                }
            }

            return totalPot > 0d ? Math.Min(1.5d, Math.Max(0d, totalCur / totalPot)) : 0d;
        }

        static (string ItemId, double SellValue, int ManaValue, float Quality) ResolveInputSource(
            WorkspaceObject building,
            ProductionRateTracker rateTracker = null)
        {
            if (building.Inputs == null || building.Inputs.Count == 0)
            {
                return (null, 0d, 0, 1f);
            }

            (string ItemId, double SellValue, int ManaValue, float Quality) resolved = (null, 0d, 0, 1f);
            var visited = new HashSet<WorkspaceObject>();
            for (int i = 0; i < building.Inputs.Count; i++)
            {
                var input = building.Inputs[i];
                if (input == null)
                {
                    continue;
                }

                if (input.BufferedItemCount > 0)
                {
                    var buffered = input.BufferedItems[0];
                    if (buffered != null && (buffered.SellValue > 0 || buffered.ManaValue > 0 || buffered.Type != null))
                    {
                        return (buffered.Type?.Identifier, buffered.SellValue, buffered.ManaValue, buffered.Quality);
                    }
                }

                var upstream = ResolveUpstreamSource(input.ConnectedPipe, visited, 0);
                if (upstream.SellValue > 0 || upstream.ManaValue > 0 || !string.IsNullOrEmpty(upstream.ItemId))
                {
                    resolved = upstream;
                    break;
                }
            }

            if (rateTracker != null
                && rateTracker.TryGetLastObservedInput(building, out var obsId, out var obsSell, out var obsMana, out var obsQual))
            {
                bool matchesResolved = string.IsNullOrEmpty(resolved.ItemId)
                    || string.Equals(obsId, resolved.ItemId, StringComparison.OrdinalIgnoreCase);
                if (matchesResolved && (string.IsNullOrEmpty(resolved.ItemId) || obsSell > resolved.SellValue || obsMana > resolved.ManaValue || obsQual > resolved.Quality))
                {
                    return (!string.IsNullOrEmpty(obsId) ? obsId : resolved.ItemId, obsSell, obsMana, obsQual);
                }
            }

            return resolved;
        }

        static (string ItemId, double SellValue, int ManaValue, float Quality) ResolveUpstreamSource(WorkspacePipe pipe, HashSet<WorkspaceObject> visited, int depth)
        {
            if (pipe == null || depth > 8)
            {
                return (null, 0d, 0, 1f);
            }

            var outputPort = pipe.ConnectedOutput;
            if (outputPort == null)
            {
                return (null, 0d, 0, 1f);
            }

            var available = outputPort.AvailableItem;
            if (available != null && (available.SellValue > 0 || available.ManaValue > 0 || available.Type != null))
            {
                return (available.Type?.Identifier, available.SellValue, available.ManaValue, available.Quality);
            }

            var parent = outputPort.Parent;
            if (parent == null || !visited.Add(parent))
            {
                return (null, 0d, 0, 1f);
            }

            if (IsPassThroughNode(parent) && parent.Inputs != null)
            {
                for (int i = 0; i < parent.Inputs.Count; i++)
                {
                    var input = parent.Inputs[i];
                    if (input != null && input.BufferedItemCount > 0)
                    {
                        var buffered = input.BufferedItems[0];
                        if (buffered != null && (buffered.SellValue > 0 || buffered.ManaValue > 0 || buffered.Type != null))
                        {
                            return (buffered.Type?.Identifier, buffered.SellValue, buffered.ManaValue, buffered.Quality);
                        }
                    }

                    var upstream = ResolveUpstreamSource(input?.ConnectedPipe, visited, depth + 1);
                    if (upstream.SellValue > 0 || upstream.ManaValue > 0 || !string.IsNullOrEmpty(upstream.ItemId))
                    {
                        return upstream;
                    }
                }

                return (null, 0d, 0, 1f);
            }

            var prod = parent.Production;
            if (prod != null)
            {
                if (prod.Resource != null && (prod.Resource.BaseValue > 0 || !string.IsNullOrEmpty(prod.Resource.Identifier)))
                {
                    return (prod.Resource.Identifier, prod.Resource.BaseValue, prod.Resource.PreviewItem?.ManaValue ?? 0, prod.Resource.PreviewItem?.Quality ?? 1f);
                }

                if (prod.SelectedForestResource != null && (prod.SelectedForestResource.BaseValue > 0 || !string.IsNullOrEmpty(prod.SelectedForestResource.Identifier)))
                {
                    return (prod.SelectedForestResource.Identifier, prod.SelectedForestResource.BaseValue, prod.SelectedForestResource.PreviewItem?.ManaValue ?? 0, prod.SelectedForestResource.PreviewItem?.Quality ?? 1f);
                }

                if (prod.Recipe?.PreviewResultItem != null && (prod.Recipe.PreviewResultItem.BaseValue > 0 || !string.IsNullOrEmpty(prod.Recipe.PreviewResultItem.Identifier)))
                {
                    return (prod.Recipe.PreviewResultItem.Identifier, prod.Recipe.PreviewResultItem.BaseValue, prod.Recipe.PreviewResultItem.PreviewItem?.ManaValue ?? 0, prod.Recipe.PreviewResultItem.PreviewItem?.Quality ?? 1f);
                }
            }

            return (null, 0d, 0, 1f);
        }

        static bool IsPassThroughNode(WorkspaceObject node)
        {
            if (node == null || node.IsJunction)
            {
                return true;
            }

            var prod = node.Production;
            if (prod != null && (prod.IsStockpile || prod.IsArmory))
            {
                return true;
            }

            if (string.Equals(node.TypeId, "Filter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "TrackSwitch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Portal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Appraiser", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Accountant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Collector", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Distributor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Manifold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Inspector", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.TypeId, "Ledger", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.Inputs != null
                && node.Inputs.Count > 0
                && node.Outputs != null
                && node.Outputs.Count > 0
                && !string.Equals(node.TypeId, "QuestBoard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(node.TypeId, "Dungeon", StringComparison.OrdinalIgnoreCase)
                && (prod == null
                    || (!prod.IsRecipe
                        && prod.Resource == null
                        && prod.SelectedForestResource == null
                        && !prod.IsRefiner
                        && !prod.IsArena
                        && !prod.IsTrainingGrounds));
        }

        static void AddItemTotal(IDictionary<string, double> totals, string id, double amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0d)
            {
                return;
            }

            totals.TryGetValue(id, out double existing);
            totals[id] = existing + amount;
        }

        static void AddCurrencyTotal(IDictionary<GameCurrency, double> totals, GameCurrency currency, double amount)
        {
            if (amount <= 0d)
            {
                return;
            }

            totals.TryGetValue(currency, out double existing);
            totals[currency] = existing + amount;
        }
    }
}
