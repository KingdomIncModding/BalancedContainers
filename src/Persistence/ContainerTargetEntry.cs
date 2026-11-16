namespace BalancedContainers
{
    public sealed class ContainerTargetEntry
    {
        public ContainerTarget Target { get; set; } = ContainerTarget.None;
        public bool Dismissed { get; set; }
    }
}

