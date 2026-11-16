using KingdomFoundation.Api;

namespace BalancedContainers
{
    public sealed class BuildingSnapshotSlot
    {
        public const int MaxInlinePorts = 8;
        public const int CurrencySlotCount = 9;

        public WorkspaceObject Building;
        public int OutputCount;
        public readonly int[] BeforeBufferedCounts = new int[MaxInlinePorts];
        public long BeforeGold;
        public long BeforeMana;
        public long BeforeCrowns;
        public long BeforeRequisition;
        public int BeforeInputBuffered;
        public float BeforeProgress;
        public int BeforeStoredValue;
        public string ObservedInputItemId;
        public double ObservedInputSellValue;
        public int ObservedInputManaValue;
        public float ObservedInputQuality = 1f;
        public readonly long[] DirectCurrencyAdded = new long[CurrencySlotCount];

        public void Capture(WorkspaceObject building)
        {
            Building = building;
            var outputs = building.Outputs;
            OutputCount = outputs != null && outputs.Count < MaxInlinePorts ? outputs.Count : (outputs == null ? 0 : MaxInlinePorts);

            for (int i = 0; i < OutputCount; i++)
            {
                BeforeBufferedCounts[i] = outputs[i]?.BufferedItemCount ?? 0;
            }

            var inputs = building.Inputs;
            var firstInput = inputs != null && inputs.Count > 0 ? inputs[0] : null;
            BeforeInputBuffered = firstInput?.BufferedItemCount ?? 0;
            if (BeforeInputBuffered > 0)
            {
                var item = firstInput.BufferedItems[0];
                ObservedInputItemId = item?.Type?.Identifier;
                ObservedInputSellValue = item?.SellValue ?? 0;
                ObservedInputManaValue = item?.ManaValue ?? 0;
                ObservedInputQuality = item?.Quality ?? 1f;
            }
            else
            {
                ObservedInputItemId = null;
                ObservedInputSellValue = 0d;
                ObservedInputManaValue = 0;
                ObservedInputQuality = 1f;
            }

            var prod = building.Production;
            if (prod != null)
            {
                BeforeGold = prod.CollectedCurrency(GameCurrency.Gold);
                BeforeMana = prod.CollectedCurrency(GameCurrency.Mana);
                BeforeCrowns = prod.CollectedCurrency(GameCurrency.Crown);
                BeforeRequisition = prod.CollectedCurrency(GameCurrency.Requisition);
                BeforeProgress = prod.Progress;
                BeforeStoredValue = prod.StoredValue;
            }
            else
            {
                BeforeGold = 0;
                BeforeMana = 0;
                BeforeCrowns = 0;
                BeforeRequisition = 0;
                BeforeProgress = 0f;
                BeforeStoredValue = 0;
            }

            for (int i = 0; i < CurrencySlotCount; i++)
            {
                DirectCurrencyAdded[i] = 0;
            }
        }

        public void Clear()
        {
            Building = null;
            OutputCount = 0;
            BeforeInputBuffered = 0;
            BeforeProgress = 0f;
            BeforeStoredValue = 0;
            ObservedInputItemId = null;
            ObservedInputSellValue = 0d;
            ObservedInputManaValue = 0;
            ObservedInputQuality = 1f;
        }
    }
}

