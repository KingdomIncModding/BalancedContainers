using KingdomFoundation.Api;
using UnityEngine;

namespace BalancedContainers
{
    public sealed class ResourceProductionEntry
    {
        public ContainerTargetKind Kind { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public double CurrentPerSecond { get; set; }
        public double PotentialPerSecond { get; set; }
        public bool IsPrimaryTarget { get; set; }
        public ResourceDiagnosticState Status { get; set; } = ResourceDiagnosticState.Operational;

        public bool IsCurrency => Kind == ContainerTargetKind.Currency;
        public bool IsGold => Kind == ContainerTargetKind.Currency && Id == nameof(GameCurrency.Gold);

        public ResourceProductionEntry(
            ContainerTargetKind kind,
            string id,
            string displayName,
            Sprite icon,
            bool isPrimaryTarget)
        {
            Kind = kind;
            Id = id;
            DisplayName = displayName ?? id ?? string.Empty;
            Icon = icon;
            IsPrimaryTarget = isPrimaryTarget;
        }
    }
}

