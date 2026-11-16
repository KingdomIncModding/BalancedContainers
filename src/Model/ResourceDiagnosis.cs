using UnityEngine;

namespace BalancedContainers
{
    public sealed class ResourceDiagnosis
    {
        public ContainerTargetKind Kind { get; }
        public string Id { get; }
        public string SubjectName { get; }
        public Sprite SubjectIcon { get; }
        public ResourceDiagnosticState State { get; }
        public string ActionLead { get; }
        public double NeededPerSecond { get; }
        public double CurrentRatePerSecond { get; }
        public string Headline { get; }
        public string Solution { get; }

        public ResourceDiagnosis(
            ContainerTargetKind kind,
            string id,
            string subjectName,
            Sprite subjectIcon,
            ResourceDiagnosticState state,
            string actionLead,
            double neededPerSecond,
            double currentRatePerSecond,
            string headline,
            string solution)
        {
            Kind = kind;
            Id = id ?? string.Empty;
            SubjectName = subjectName ?? string.Empty;
            SubjectIcon = subjectIcon;
            State = state;
            ActionLead = actionLead ?? string.Empty;
            NeededPerSecond = neededPerSecond;
            CurrentRatePerSecond = currentRatePerSecond;
            Headline = headline ?? string.Empty;
            Solution = solution ?? string.Empty;
        }
    }
}

