using System;
using System.Runtime.CompilerServices;
using KingdomFoundation.Api;
using KingdomFoundation.Json;

namespace BalancedContainers
{
    public sealed class ContainerTargetStore
    {
        public const string JsonKey = "urakka.balancedcontainers";
        const string LegacyJsonKey = "urakka.productioncontainer";

        readonly ConditionalWeakTable<WorkspaceObject, ContainerTargetEntry> entries =
            new ConditionalWeakTable<WorkspaceObject, ContainerTargetEntry>();

        public static bool IsSupportedContainer(WorkspaceObject obj)
        {
            return obj != null && obj.Kind == WorkspaceObjectKind.Container && obj.IsContainer;
        }

        public ContainerTarget GetTarget(WorkspaceObject container)
        {
            if (!IsSupportedContainer(container))
            {
                return ContainerTarget.None;
            }

            return entries.TryGetValue(container, out var entry)
                ? entry.Target ?? ContainerTarget.None
                : ContainerTarget.None;
        }

        public bool ShouldAutoPrompt(WorkspaceObject container)
        {
            if (!IsSupportedContainer(container))
            {
                return false;
            }

            if (!entries.TryGetValue(container, out var entry))
            {
                return true;
            }

            return !entry.Dismissed && (entry.Target == null || !entry.Target.IsAssigned);
        }

        public void SetTarget(WorkspaceObject container, ContainerTarget target, bool markDismissed = true)
        {
            if (!IsSupportedContainer(container))
            {
                return;
            }

            var entry = GetOrCreateEntry(container);
            entry.Target = target ?? ContainerTarget.None;
            if (markDismissed || entry.Target.IsAssigned)
            {
                entry.Dismissed = true;
            }
        }

        public void MarkDismissed(WorkspaceObject container)
        {
            if (!IsSupportedContainer(container))
            {
                return;
            }

            GetOrCreateEntry(container).Dismissed = true;
        }

        public void WriteToJson(WorkspaceObject container, JsonValue serialized)
        {
            if (!IsSupportedContainer(container) || !serialized.IsJsonObject)
            {
                return;
            }

            if (!entries.TryGetValue(container, out var entry) || entry.Target == null || !entry.Target.IsAssigned)
            {
                return;
            }

            var payload = new JsonObject
            {
                ["kind"] = entry.Target.Kind == ContainerTargetKind.Currency ? "currency" : "item",
                ["id"] = entry.Target.Id
            };

            var root = serialized.AsJsonObject;
            if (root != null)
            {
                root[JsonKey] = payload;
            }
        }

        public void ReadFromJson(JsonValue json, WorkspaceObject container)
        {
            if (!IsSupportedContainer(container))
            {
                return;
            }

            var entry = GetOrCreateEntry(container);
            entry.Dismissed = true;
            entry.Target = ContainerTarget.None;

            if (!json.IsJsonObject)
            {
                return;
            }

            var node = json[JsonKey];
            if (!node.IsJsonObject)
            {
                node = json[LegacyJsonKey];
            }

            if (!node.IsJsonObject)
            {
                return;
            }

            string kindRaw = node["kind"].IsString ? node["kind"].AsString : null;
            string id = node["id"].IsString ? node["id"].AsString : null;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (string.Equals(kindRaw, "currency", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<GameCurrency>(id, ignoreCase: true, out var currency))
                {
                    entry.Target = ContainerTarget.ForCurrency(currency);
                }
            }
            else if (string.Equals(kindRaw, "item", StringComparison.OrdinalIgnoreCase))
            {
                entry.Target = ContainerTarget.ForItem(id);
            }
        }

        public void Copy(WorkspaceObject source, WorkspaceObject copy)
        {
            if (!IsSupportedContainer(copy))
            {
                return;
            }

            if (source == null || !entries.TryGetValue(source, out var sourceEntry))
            {
                return;
            }

            var copyEntry = GetOrCreateEntry(copy);
            copyEntry.Target = sourceEntry.Target ?? ContainerTarget.None;
            copyEntry.Dismissed = sourceEntry.Dismissed;
        }

        ContainerTargetEntry GetOrCreateEntry(WorkspaceObject container)
        {
            return entries.GetValue(container, _ => new ContainerTargetEntry());
        }
    }
}

