using UnityEngine;

namespace BalancedContainers
{
    public sealed class TargetCatalogEntry
    {
        public ContainerTarget Target { get; }
        public string DisplayName { get; }
        public string SearchKey { get; }
        public Sprite Icon { get; }
        public bool IsCurrency => Target.Kind == ContainerTargetKind.Currency;

        public TargetCatalogEntry(ContainerTarget target, string displayName, Sprite icon)
        {
            Target = target;
            DisplayName = displayName ?? target.Id;
            SearchKey = $"{DisplayName} {target.Id}".ToLowerInvariant();
            Icon = icon;
        }
    }
}

