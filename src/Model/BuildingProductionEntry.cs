using UnityEngine;

namespace BalancedContainers
{
    public sealed class BuildingProductionEntry
    {
        public string BuildingName { get; }
        public Sprite BuildingIcon { get; }
        public int StackSize { get; }
        public Vector2Int Position { get; }
        public Vector2 BuildingSize { get; }
        public ContainerTargetKind ResourceKind { get; }
        public string ResourceId { get; }
        public string ResourceName { get; }
        public Sprite ResourceIcon { get; }
        public bool IsConsumer { get; }
        public double CurrentPerSecond { get; set; }
        public double PotentialPerSecond { get; set; }
        public bool IsPrimaryTarget { get; set; }

        public BuildingProductionEntry(
            string buildingName,
            Sprite buildingIcon,
            int stackSize,
            Vector2Int position,
            Vector2 buildingSize,
            ContainerTargetKind resourceKind,
            string resourceId,
            string resourceName,
            Sprite resourceIcon,
            bool isPrimaryTarget,
            bool isConsumer = false)
        {
            BuildingName = string.IsNullOrWhiteSpace(buildingName) ? "Building" : buildingName;
            BuildingIcon = buildingIcon;
            StackSize = stackSize < 1 ? 1 : stackSize;
            Position = position;
            BuildingSize = buildingSize;
            ResourceKind = resourceKind;
            ResourceId = resourceId ?? string.Empty;
            ResourceName = resourceName ?? resourceId ?? string.Empty;
            ResourceIcon = resourceIcon;
            IsPrimaryTarget = isPrimaryTarget;
            IsConsumer = isConsumer;
        }
    }
}
