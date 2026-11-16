using System;

namespace BalancedContainers
{
    public sealed class BucketedRateCounter
    {
        const int MaxBuckets = 60;

        readonly long[] bucketSeconds = new long[MaxBuckets];
        readonly double[] bucketAmounts = new double[MaxBuckets];
        double firstSampleTime = -1d;

        public BucketedRateCounter()
        {
            Reset();
        }

        public void Reset()
        {
            firstSampleTime = -1d;
            for (int i = 0; i < MaxBuckets; i++)
            {
                bucketSeconds[i] = long.MinValue;
                bucketAmounts[i] = 0d;
            }
        }

        public void Record(double nowSeconds, double amount)
        {
            if (amount <= 0d)
            {
                return;
            }

            if (firstSampleTime < 0d)
            {
                firstSampleTime = nowSeconds;
            }

            long sec = (long)Math.Floor(nowSeconds);
            int idx = (int)((sec % MaxBuckets + MaxBuckets) % MaxBuckets);
            if (bucketSeconds[idx] != sec)
            {
                bucketSeconds[idx] = sec;
                bucketAmounts[idx] = 0d;
            }

            bucketAmounts[idx] += amount;
        }

        public double RatePerSecond(double nowSeconds, double windowSeconds, double buildingFirstObservedTime)
        {
            if (firstSampleTime < 0d || windowSeconds <= 0d)
            {
                return 0d;
            }

            long curSec = (long)Math.Floor(nowSeconds);
            int span = Math.Min(MaxBuckets, Math.Max(1, (int)Math.Ceiling(windowSeconds)));
            long minSec = curSec - span;
            long windowStartSec = minSec + 1;

            double sum = 0d;
            for (int i = 0; i < MaxBuckets; i++)
            {
                long bSec = bucketSeconds[i];
                if (bSec > minSec && bSec <= curSec)
                {
                    sum += bucketAmounts[i];
                }
            }

            if (sum <= 0d)
            {
                return 0d;
            }

            double origin = buildingFirstObservedTime >= 0d
                ? Math.Min(buildingFirstObservedTime, firstSampleTime)
                : firstSampleTime;

            double windowStart = Math.Max(origin, windowStartSec);
            double elapsed = Math.Max(1.0d, nowSeconds - windowStart);
            return sum / elapsed;
        }
    }
}

