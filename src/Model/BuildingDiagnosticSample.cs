using System.Collections.Generic;

namespace BalancedContainers
{
    public sealed class BuildingDiagnosticSample
    {
        public string BuildingName { get; }
        public bool IsActivelyRunning { get; }
        public bool IsSaturated { get; }
        public bool IsShopGoldCapped { get; }
        public bool HasUnconnectedOutput { get; }
        public bool HasRejectedOutput { get; }
        public bool IsStarved { get; }
        public bool HasUnconnectedInput { get; }
        public bool MissingCostFunds { get; }
        public bool QuotaBlocked { get; }
        public bool MissingConfiguration { get; }
        public double SecondsSinceMovement { get; }
        public IReadOnlyList<string> MissingReagentIds { get; }
        public IReadOnlyList<string> MissingReagentNames { get; }

        public BuildingDiagnosticSample(
            string buildingName,
            bool isActivelyRunning,
            bool isSaturated,
            bool isShopGoldCapped,
            bool hasUnconnectedOutput,
            bool hasRejectedOutput,
            bool isStarved,
            bool hasUnconnectedInput,
            bool missingCostFunds,
            bool quotaBlocked,
            bool missingConfiguration,
            double secondsSinceMovement,
            IReadOnlyList<string> missingReagentIds,
            IReadOnlyList<string> missingReagentNames)
        {
            BuildingName = buildingName ?? "Building";
            IsActivelyRunning = isActivelyRunning;
            IsSaturated = isSaturated;
            IsShopGoldCapped = isShopGoldCapped;
            HasUnconnectedOutput = hasUnconnectedOutput;
            HasRejectedOutput = hasRejectedOutput;
            IsStarved = isStarved;
            HasUnconnectedInput = hasUnconnectedInput;
            MissingCostFunds = missingCostFunds;
            QuotaBlocked = quotaBlocked;
            MissingConfiguration = missingConfiguration;
            SecondsSinceMovement = secondsSinceMovement;
            MissingReagentIds = missingReagentIds;
            MissingReagentNames = missingReagentNames;
        }
    }
}

