using System;
using System.Collections.Generic;

namespace BalancedContainers
{
    public sealed class ContainerProductionSnapshot
    {
        public static readonly ContainerProductionSnapshot Empty = new ContainerProductionSnapshot(
            Array.Empty<ResourceProductionEntry>(),
            Array.Empty<BuildingProductionEntry>(),
            Array.Empty<ResourceDiagnosis>());

        public IReadOnlyList<ResourceProductionEntry> SummaryEntries { get; }
        public IReadOnlyList<BuildingProductionEntry> BuildingEntries { get; }
        public IReadOnlyList<ResourceDiagnosis> Diagnoses { get; }

        public ContainerProductionSnapshot(
            IReadOnlyList<ResourceProductionEntry> summaryEntries,
            IReadOnlyList<BuildingProductionEntry> buildingEntries,
            IReadOnlyList<ResourceDiagnosis> diagnoses = null)
        {
            SummaryEntries = summaryEntries ?? Array.Empty<ResourceProductionEntry>();
            BuildingEntries = buildingEntries ?? Array.Empty<BuildingProductionEntry>();
            Diagnoses = diagnoses ?? Array.Empty<ResourceDiagnosis>();
        }

        public ResourceDiagnosis GetDiagnosis(ContainerTargetKind kind, string id)
        {
            if (Diagnoses == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < Diagnoses.Count; i++)
            {
                var diag = Diagnoses[i];
                if (diag != null
                    && diag.Kind == kind
                    && string.Equals(diag.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return diag;
                }
            }

            return null;
        }
    }
}
