using System.Collections.Generic;
using KingdomFoundation.Api;

namespace BalancedContainers
{
    public sealed class BuildingRateHistory
    {
        readonly Dictionary<string, BucketedRateCounter> itemCounters =
            new Dictionary<string, BucketedRateCounter>(System.StringComparer.Ordinal);
        readonly Dictionary<GameCurrency, BucketedRateCounter> currencyCounters =
            new Dictionary<GameCurrency, BucketedRateCounter>();

        public int Generation { get; private set; } = -1;
        public double FirstObservedTime { get; private set; } = -1d;
        public double LastMovementTime { get; private set; } = -1d;
        public double LastInputActivityTime { get; private set; } = -1d;

        long lastOutputSignature = -1;
        string lastInputItemId;
        double lastInputSellValue;
        int lastInputManaValue;
        float lastInputQuality = 1f;

        public void EnsureGeneration(int generation, double nowSeconds)
        {
            if (Generation != generation)
            {
                Generation = generation;
                FirstObservedTime = nowSeconds;
                LastMovementTime = nowSeconds;
                LastInputActivityTime = nowSeconds;
                lastOutputSignature = -1;
                lastInputItemId = null;
                lastInputSellValue = 0d;
                lastInputManaValue = 0;
                lastInputQuality = 1f;
                itemCounters.Clear();
                currencyCounters.Clear();
            }
            else if (FirstObservedTime < 0d)
            {
                FirstObservedTime = nowSeconds;
                LastMovementTime = nowSeconds;
                LastInputActivityTime = nowSeconds;
            }
        }

        public void ObserveInputItem(string itemId, double sellValue, int manaValue, float quality, double nowSeconds)
        {
            lastInputItemId = itemId;
            lastInputSellValue = sellValue;
            lastInputManaValue = manaValue;
            lastInputQuality = quality;
            LastInputActivityTime = nowSeconds;
        }

        public void RecordInputActivity(double nowSeconds)
        {
            LastInputActivityTime = nowSeconds;
        }

        public bool TryGetLastInputItem(out string itemId, out double sellValue, out int manaValue, out float quality)
        {
            itemId = lastInputItemId;
            sellValue = lastInputSellValue;
            manaValue = lastInputManaValue;
            quality = lastInputQuality;
            return !string.IsNullOrEmpty(lastInputItemId) || lastInputSellValue > 0d || lastInputManaValue > 0;
        }

        public void ObserveOutputSignature(long signature, double nowSeconds)
        {
            if (lastOutputSignature != signature)
            {
                lastOutputSignature = signature;
                LastMovementTime = nowSeconds;
            }
        }

        public void RecordItem(string itemId, double amount, double nowSeconds)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0d)
            {
                return;
            }

            LastMovementTime = nowSeconds;
            if (!itemCounters.TryGetValue(itemId, out var counter))
            {
                counter = new BucketedRateCounter();
                itemCounters[itemId] = counter;
            }

            counter.Record(nowSeconds, amount);
        }

        public void RecordCurrency(GameCurrency currency, double amount, double nowSeconds)
        {
            if (amount <= 0d)
            {
                return;
            }

            LastMovementTime = nowSeconds;
            if (!currencyCounters.TryGetValue(currency, out var counter))
            {
                counter = new BucketedRateCounter();
                currencyCounters[currency] = counter;
            }

            counter.Record(nowSeconds, amount);
        }

        public void AccumulateRates(
            double nowSeconds,
            double windowSeconds,
            IDictionary<string, double> itemTotals,
            IDictionary<GameCurrency, double> currencyTotals)
        {
            foreach (var pair in itemCounters)
            {
                double rate = pair.Value.RatePerSecond(nowSeconds, windowSeconds, FirstObservedTime);
                if (rate <= 0d)
                {
                    continue;
                }

                itemTotals.TryGetValue(pair.Key, out double existing);
                itemTotals[pair.Key] = existing + rate;
            }

            foreach (var pair in currencyCounters)
            {
                double rate = pair.Value.RatePerSecond(nowSeconds, windowSeconds, FirstObservedTime);
                if (rate <= 0d)
                {
                    continue;
                }

                currencyTotals.TryGetValue(pair.Key, out double existing);
                currencyTotals[pair.Key] = existing + rate;
            }
        }
    }
}

