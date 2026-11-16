using System;
using System.Collections.Generic;
using KingdomFoundation.Api;
using UnityEngine;

namespace BalancedContainers
{
    public static class ResourceStatusAnalyzer
    {
        const double DefaultOperationalToleranceRatio = 0.95d;
        const double OversaturatedStallSeconds = 5.0d;

        public static List<ResourceDiagnosis> AnalyzeAll(
            IReadOnlyList<ResourceProductionEntry> summaryEntries,
            IReadOnlyList<BuildingProductionEntry> buildingEntries,
            IReadOnlyDictionary<string, BuildingDiagnosticSample> samplesByBuildingKey,
            bool perMinute,
            double operationalToleranceRatio = DefaultOperationalToleranceRatio)
        {
            double clampedTolerance = Math.Max(0.50d, Math.Min(1.0d, operationalToleranceRatio));
            var results = new List<ResourceDiagnosis>(summaryEntries?.Count ?? 0);
            if (summaryEntries == null || summaryEntries.Count == 0)
            {
                return results;
            }

            var summaryByKey = new Dictionary<string, ResourceProductionEntry>(StringComparer.OrdinalIgnoreCase);
            var producersByKey = new Dictionary<string, List<BuildingProductionEntry>>(StringComparer.OrdinalIgnoreCase);
            var consumersByKey = new Dictionary<string, List<BuildingProductionEntry>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < summaryEntries.Count; i++)
            {
                var summary = summaryEntries[i];
                if (summary != null)
                {
                    summaryByKey[MakeKey(summary.Kind, summary.Id)] = summary;
                }
            }

            if (buildingEntries != null)
            {
                for (int i = 0; i < buildingEntries.Count; i++)
                {
                    var entry = buildingEntries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    string key = MakeKey(entry.ResourceKind, entry.ResourceId);
                    var map = entry.IsConsumer ? consumersByKey : producersByKey;
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<BuildingProductionEntry>();
                        map[key] = list;
                    }

                    list.Add(entry);
                }
            }

            for (int i = 0; i < summaryEntries.Count; i++)
            {
                var summary = summaryEntries[i];
                if (summary == null)
                {
                    continue;
                }

                string key = MakeKey(summary.Kind, summary.Id);
                producersByKey.TryGetValue(key, out var producers);
                consumersByKey.TryGetValue(key, out var consumers);

                var diagnosis = DiagnoseResource(
                    summary,
                    producers,
                    consumers,
                    samplesByBuildingKey,
                    summaryByKey,
                    consumersByKey,
                    perMinute,
                    clampedTolerance);

                summary.Status = diagnosis.State;
                results.Add(diagnosis);
            }

            return results;
        }

        static ResourceDiagnosis DiagnoseResource(
            ResourceProductionEntry summary,
            List<BuildingProductionEntry> producers,
            List<BuildingProductionEntry> consumers,
            IReadOnlyDictionary<string, BuildingDiagnosticSample> samplesByBuildingKey,
            IReadOnlyDictionary<string, ResourceProductionEntry> summaryByKey,
            IReadOnlyDictionary<string, List<BuildingProductionEntry>> consumersByKey,
            bool perMinute,
            double operationalToleranceRatio)
        {
            string unit = perMinute ? "/m" : "/s";
            double cur = summary.CurrentPerSecond;
            double pot = summary.PotentialPerSecond;
            string curText = ContainerUiTheme.FormatResourceRate(summary.Kind, summary.Id, cur, perMinute) + unit;
            string potText = ContainerUiTheme.FormatResourceRate(summary.Kind, summary.Id, pot, perMinute) + unit;
            string summaryName = summary.DisplayName;
            int pct = pot > 0.0001d ? (int)Math.Round(Math.Min(100d, (cur / pot) * 100d)) : 100;

            if (producers == null || producers.Count == 0)
            {
                return MakeDiagnosis(
                    summary,
                    summaryName,
                    summary.Icon,
                    ResourceDiagnosticState.Idle,
                    "No Producers",
                    pot,
                    cur,
                    $"Current: {curText}. No active producers in container.",
                    $"Place a building producing {summaryName} below the bar.");
            }

            int saturated = 0;
            int shopCapped = 0;
            int unconnectedOut = 0;
            int rejectedOut = 0;
            int starved = 0;
            int unconnectedIn = 0;
            int costBlocked = 0;
            int quotaBlocked = 0;
            int unconfigured = 0;
            int activeRunning = 0;
            int stalledOver5sCount = 0;
            double maxIdleOnBlocked = 0d;

            var missingReagentIds = new List<string>();
            var missingReagents = new List<string>();
            var producerNames = new List<string>();

            for (int i = 0; i < producers.Count; i++)
            {
                var p = producers[i];
                AddUnique(producerNames, p.BuildingName);

                string bKey = MakeBuildingKey(p.Position, p.BuildingName);
                if (samplesByBuildingKey == null || !samplesByBuildingKey.TryGetValue(bKey, out var sample) || sample == null)
                {
                    continue;
                }

                if (sample.IsActivelyRunning) activeRunning++;
                if (sample.IsSaturated) saturated++;
                if (sample.IsShopGoldCapped) shopCapped++;
                if (sample.HasUnconnectedOutput) unconnectedOut++;
                if (sample.HasRejectedOutput) rejectedOut++;
                if (sample.IsStarved) starved++;
                if (sample.HasUnconnectedInput) unconnectedIn++;
                if (sample.MissingCostFunds) costBlocked++;
                if (sample.QuotaBlocked) quotaBlocked++;
                if (sample.MissingConfiguration) unconfigured++;

                if ((sample.IsSaturated || sample.IsShopGoldCapped || sample.HasUnconnectedOutput || sample.HasRejectedOutput)
                    && sample.SecondsSinceMovement >= OversaturatedStallSeconds)
                {
                    stalledOver5sCount++;
                    if (sample.SecondsSinceMovement > maxIdleOnBlocked)
                    {
                        maxIdleOnBlocked = sample.SecondsSinceMovement;
                    }
                }

                if (sample.MissingReagentIds != null)
                {
                    for (int r = 0; r < sample.MissingReagentIds.Count; r++)
                    {
                        AddUnique(missingReagentIds, sample.MissingReagentIds[r]);
                    }
                }

                if (sample.MissingReagentNames != null)
                {
                    for (int r = 0; r < sample.MissingReagentNames.Count; r++)
                    {
                        AddUnique(missingReagents, sample.MissingReagentNames[r]);
                    }
                }
            }

            bool hasAnyIssue = starved > 0
                || unconnectedIn > 0
                || costBlocked > 0
                || quotaBlocked > 0
                || unconfigured > 0
                || shopCapped > 0
                || saturated > 0
                || unconnectedOut > 0
                || rejectedOut > 0;

            if (pot <= 0.0001d && cur <= 0.0001d && !hasAnyIssue)
            {
                return MakeDiagnosis(
                    summary,
                    summaryName,
                    summary.Icon,
                    ResourceDiagnosticState.Idle,
                    "No Producers",
                    pot,
                    cur,
                    $"Current: {curText}. No active producers in container.",
                    $"Place a building producing {summaryName} below the bar.");
            }

            double consumerPot = 0d;
            var consumerNames = new List<string>();
            if (consumers != null)
            {
                for (int i = 0; i < consumers.Count; i++)
                {
                    consumerPot += consumers[i].PotentialPerSecond;
                    AddUnique(consumerNames, consumers[i].BuildingName);
                }
            }

            string consumerSummary = consumerNames.Count > 0 ? string.Join("/", consumerNames) : "consumer";
            string producerSummary = producerNames.Count > 0 ? string.Join("/", producerNames) : "producer";
            string consumerPotText = ContainerUiTheme.FormatResourceRate(summary.Kind, summary.Id, consumerPot, perMinute) + unit;

            bool underTolerance = cur < pot * operationalToleranceRatio;
            bool isOversaturated = stalledOver5sCount > 0 && starved == 0;
            bool clearSaturated = isOversaturated
                || shopCapped > 0
                || ((saturated > 0 || unconnectedOut > 0 || rejectedOut > 0) && underTolerance && starved == 0)
                || (underTolerance && consumerPot > 0.0001d && consumerPot < pot * operationalToleranceRatio && starved == 0);

            if (clearSaturated)
            {
                ResourceDiagnosticState satState = isOversaturated
                    ? ResourceDiagnosticState.Oversaturated
                    : ResourceDiagnosticState.Saturated;
                string steadyFix = "Use a Manifold / Incinerator for steady production";

                if (shopCapped > 0)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        satState,
                        "Gold Cap",
                        pot,
                        cur,
                        $"Current: {curText}. Capacity: {potText} (Shop full)",
                        "Buy Coffers / Gold Cap upgrades or clear shop sales.");
                }

                if (unconnectedOut > 0)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        satState,
                        "Open Output",
                        pot,
                        cur,
                        $"Current: {curText}. Potential: {potText} ({unconnectedOut} output open)",
                        $"Connect {producerSummary} output pipe to a consumer, collector, or {steadyFix}.");
                }

                if (rejectedOut > 0)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        satState,
                        "Rejected",
                        pot,
                        cur,
                        $"Current: {curText}. Capacity: {potText} (Output rejected)",
                        $"Check downstream input filter for {summaryName}, or {steadyFix}.");
                }

                if (consumerPot > 0.0001d && consumerPot < pot * operationalToleranceRatio)
                {
                    string actionLead = isOversaturated ? "Over-Saturated" : "Saturated";
                    string hint = isOversaturated
                        ? $"{steadyFix}, or add {consumerSummary}(s)."
                        : $"Items still moving cleanly — add {consumerSummary}(s) or a Manifold / Incinerator to burn excess.";
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        satState,
                        actionLead,
                        consumerPot,
                        pot,
                        $"Producing: {potText}. Used: {consumerPotText}",
                        hint);
                }

                string idleNote = isOversaturated
                    ? $"No items moved for {maxIdleOnBlocked:0}s"
                    : "Buffers full, items moving";
                return MakeDiagnosis(
                    summary,
                    summaryName,
                    summary.Icon,
                    satState,
                    isOversaturated ? "Stalled" : "Buffered",
                    pot,
                    cur,
                    $"Current: {curText}. Capacity: {potText} ({idleNote})",
                    $"{steadyFix}, or add downstream consumers/storage.");
            }

            bool clearStarved = quotaBlocked > 0
                || costBlocked > 0
                || unconfigured > 0
                || (starved > 0 && (underTolerance || starved * 2 >= producers.Count || pot <= 0.0001d))
                || (unconnectedIn > 0 && (underTolerance || pot <= 0.0001d));

            if (clearStarved)
            {
                var (reagentName, reagentIcon) = ResolveMissingSubject(summary, missingReagentIds, missingReagents);

                if (unconnectedIn > 0)
                {
                    string headline = pot > 0.0001d
                        ? $"Need {potText}. Current: {curText} ({unconnectedIn} input open)"
                        : $"Current: {curText} ({unconnectedIn} input pipe not connected)";
                    return MakeDiagnosis(
                        summary,
                        reagentName,
                        reagentIcon,
                        ResourceDiagnosticState.Starved,
                        "Unconnected",
                        pot,
                        cur,
                        headline,
                        $"Connect pipe supplying {reagentName} to {producerSummary}.");
                }

                if (costBlocked > 0)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        ResourceDiagnosticState.Starved,
                        "Low Funds",
                        pot,
                        cur,
                        $"Need currency cost. Current: {curText}",
                        "Increase Gold / Kingdom currency income to sustain crafting.");
                }

                if (quotaBlocked > 0)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        ResourceDiagnosticState.Starved,
                        "No Quota",
                        pot,
                        cur,
                        $"Need quota capacity. Current: {curText}",
                        "Unlock quota expansion upgrades or wait for quota recovery.");
                }

                if (unconfigured > 0)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        ResourceDiagnosticState.Starved,
                        "Unconfigured",
                        pot,
                        cur,
                        $"Need recipe/work. Current: {curText} ({unconfigured} idle)",
                        "Assign a recipe or enable Auto-Craft on those buildings.");
                }

                if (TryBuildUpstreamSupplyGap(
                        missingReagentIds,
                        missingReagents,
                        summaryByKey,
                        consumersByKey,
                        perMinute,
                        unit,
                        out var gapDiagnosis))
                {
                    return new ResourceDiagnosis(
                        summary.Kind,
                        summary.Id,
                        gapDiagnosis.SubjectName,
                        gapDiagnosis.SubjectIcon ?? reagentIcon,
                        ResourceDiagnosticState.Starved,
                        "Lacking",
                        gapDiagnosis.NeededPerSecond,
                        gapDiagnosis.CurrentRatePerSecond,
                        gapDiagnosis.Headline,
                        gapDiagnosis.Solution);
                }

                string starvedHeadline = pot > 0.0001d
                    ? $"Need {potText}. Current: {curText}"
                    : $"Current: {curText} (No {reagentName} arriving)";
                return MakeDiagnosis(
                    summary,
                    reagentName,
                    reagentIcon,
                    ResourceDiagnosticState.Starved,
                    "Lacking",
                    pot,
                    cur,
                    starvedHeadline,
                    $"Increase upstream {reagentName} supply or check Collector/Distributor pipes.");
            }

            if (underTolerance)
            {
                bool warmingUpCleanly = activeRunning == producers.Count
                    && saturated == 0
                    && starved == 0
                    && unconnectedOut == 0
                    && unconnectedIn == 0;

                if (warmingUpCleanly)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        ResourceDiagnosticState.Operational,
                        string.Empty,
                        pot,
                        cur,
                        $"Current: {curText}. Target: {potText} ({pct}% — warming up)",
                        string.Empty);
                }

                if (consumerPot > 0.0001d && consumerPot < pot * operationalToleranceRatio)
                {
                    return MakeDiagnosis(
                        summary,
                        summaryName,
                        summary.Icon,
                        ResourceDiagnosticState.Saturated,
                        "Excess",
                        consumerPot,
                        pot,
                        $"Producing: {potText}. Used: {consumerPotText}",
                        $"Use a Manifold / Incinerator on excess for steady production, or add {consumerSummary}(s).");
                }

                return MakeDiagnosis(
                    summary,
                    summaryName,
                    summary.Icon,
                    ResourceDiagnosticState.Starved,
                    "Lacking",
                    pot,
                    cur,
                    $"Need {potText}. Current: {curText}",
                    "Check upstream item supply, buffer levels, and pipe connections.");
            }

            return MakeDiagnosis(
                summary,
                summaryName,
                summary.Icon,
                ResourceDiagnosticState.Operational,
                string.Empty,
                pot,
                cur,
                $"Current: {curText}. Target: {potText} ({pct}% — within tolerance)",
                string.Empty);
        }

        static ResourceDiagnosis MakeDiagnosis(
            ResourceProductionEntry summary,
            string subjectName,
            Sprite subjectIcon,
            ResourceDiagnosticState state,
            string actionLead,
            double neededPerSecond,
            double currentRatePerSecond,
            string headline,
            string solution)
        {
            return new ResourceDiagnosis(
                summary.Kind,
                summary.Id,
                subjectName,
                subjectIcon,
                state,
                actionLead,
                neededPerSecond,
                currentRatePerSecond,
                headline,
                solution);
        }

        static (string Name, Sprite Icon) ResolveMissingSubject(
            ResourceProductionEntry summary,
            List<string> missingReagentIds,
            List<string> missingReagents)
        {
            string id = missingReagentIds != null && missingReagentIds.Count > 0 ? missingReagentIds[0] : null;
            Sprite icon = !string.IsNullOrEmpty(id) ? FoundationApi.ItemAccess?.Find(id)?.GetIcon() : null;
            string name = missingReagents != null && missingReagents.Count > 0 ? missingReagents[0] : summary.DisplayName;
            return (name, icon ?? summary.Icon);
        }

        static bool TryBuildUpstreamSupplyGap(
            List<string> missingReagentIds,
            List<string> missingReagents,
            IReadOnlyDictionary<string, ResourceProductionEntry> summaryByKey,
            IReadOnlyDictionary<string, List<BuildingProductionEntry>> consumersByKey,
            bool perMinute,
            string unit,
            out ResourceDiagnosis gapDiagnosis)
        {
            gapDiagnosis = null;
            if (summaryByKey == null)
            {
                return false;
            }

            if (missingReagentIds != null)
            {
                for (int i = 0; i < missingReagentIds.Count; i++)
                {
                    string key = MakeKey(ContainerTargetKind.Item, missingReagentIds[i]);
                    if (summaryByKey.TryGetValue(key, out var match) && TryEvalSupplyGap(key, match, consumersByKey, perMinute, unit, out gapDiagnosis))
                    {
                        return true;
                    }
                }
            }

            if (missingReagents != null)
            {
                for (int r = 0; r < missingReagents.Count; r++)
                {
                    string reagentName = missingReagents[r];
                    foreach (var kv in summaryByKey)
                    {
                        var s = kv.Value;
                        if (s != null && string.Equals(s.DisplayName, reagentName, StringComparison.OrdinalIgnoreCase)
                            && TryEvalSupplyGap(kv.Key, s, consumersByKey, perMinute, unit, out gapDiagnosis))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool TryEvalSupplyGap(
            string key,
            ResourceProductionEntry s,
            IReadOnlyDictionary<string, List<BuildingProductionEntry>> consumersByKey,
            bool perMinute,
            string unit,
            out ResourceDiagnosis gapDiagnosis)
        {
            gapDiagnosis = null;
            if (s == null)
            {
                return false;
            }

            double demandPot = 0d;
            if (consumersByKey != null && consumersByKey.TryGetValue(key, out var cons))
            {
                for (int c = 0; c < cons.Count; c++)
                {
                    demandPot += cons[c].PotentialPerSecond;
                }
            }

            if (demandPot > s.PotentialPerSecond * 1.05d || s.CurrentPerSecond < demandPot * 0.85d)
            {
                string supplyStr = ContainerUiTheme.FormatResourceRate(s.Kind, s.Id, s.CurrentPerSecond, perMinute) + unit;
                string demandStr = ContainerUiTheme.FormatResourceRate(s.Kind, s.Id, demandPot, perMinute) + unit;
                gapDiagnosis = new ResourceDiagnosis(
                    s.Kind,
                    s.Id,
                    s.DisplayName,
                    s.Icon,
                    ResourceDiagnosticState.Starved,
                    "Lacking",
                    demandPot,
                    s.CurrentPerSecond,
                    $"Need {demandStr}. Current: {supplyStr}",
                    $"Add more {s.DisplayName} producers, boost speed, or widen input pipes.");
                return true;
            }

            return false;
        }

        public static string MakeBuildingKey(Vector2Int position, string buildingName)
        {
            return $"{position.x},{position.y}:{buildingName}";
        }

        static string MakeKey(ContainerTargetKind kind, string id)
        {
            return $"{(int)kind}:{id}";
        }

        static void AddUnique(List<string> list, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            list.Add(value);
        }
    }
}

