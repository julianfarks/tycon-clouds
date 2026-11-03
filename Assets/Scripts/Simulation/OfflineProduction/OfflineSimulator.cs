using System;
using CloudGarden.Core;
using CloudGarden.Economy;

namespace CloudGarden.OfflineProduction
{
    public sealed class OfflineReport
    {
        public double ElapsedSeconds;     // real time away (before cap)
        public double SimulatedSeconds;   // seconds actually simulated (after cap)
        public bool Capped;
        public bool ClockTamperDetected;
        public double CoinsGained;
        public readonly double[] Delta = new double[ResourceInfo.Count];

        public bool HasGains
        {
            get
            {
                if (SimulatedSeconds < 5) return false;
                if (CoinsGained > 0) return true;
                for (int i = 1; i < Delta.Length; i++) if (Delta[i] > 0.5) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Offline progress. The elapsed time is capped by the offline capacity (2 h, up to 8 h with research)
    /// and protected against device-clock manipulation.
    /// </summary>
    public static class OfflineSimulator
    {
        public const double TicksPerSecond = 10000000.0;
        public const double MaxStepSeconds = 15.0;
        public const int MaxSteps = 2400;

        /// <summary>
        /// Seconds to simulate. If the clock went back behind the last time we saw, nothing is granted,
        /// so rewinding and advancing the clock cannot duplicate rewards.
        /// </summary>
        public static double ComputeElapsed(long nowTicks, long lastSaveTicks, long maxSeenTicks, out bool tamper)
        {
            tamper = false;
            if (lastSaveTicks <= 0) return 0;
            if (nowTicks < maxSeenTicks || nowTicks < lastSaveTicks)
            {
                tamper = true;
                return 0;
            }
            double seconds = (nowTicks - lastSaveTicks) / TicksPerSecond;
            if (double.IsNaN(seconds) || seconds < 0) return 0;
            return seconds;
        }

        /// <summary>Computes the elapsed time since the last session, simulates it and updates the timestamps.</summary>
        public static OfflineReport Apply(GameState s, long nowTicks)
        {
            bool tamper;
            double elapsed = ComputeElapsed(nowTicks, s.LastSaveUtcTicks, s.MaxSeenUtcTicks, out tamper);
            OfflineReport report = Simulate(s, elapsed);
            report.ClockTamperDetected = tamper;
            if (nowTicks > s.MaxSeenUtcTicks) s.MaxSeenUtcTicks = nowTicks;
            if (!tamper) s.LastSaveUtcTicks = nowTicks;
            return report;
        }

        public static OfflineReport Simulate(GameState s, double elapsedSeconds)
        {
            OfflineReport report = new OfflineReport();
            if (double.IsNaN(elapsedSeconds) || elapsedSeconds < 0) elapsedSeconds = 0;
            report.ElapsedSeconds = elapsedSeconds;

            if (s.ModsDirty) s.RecomputeMods();
            double cap = s.Mods.OfflineHours * 3600.0;
            double seconds = elapsedSeconds;
            if (seconds > cap)
            {
                seconds = cap;
                report.Capped = true;
            }
            report.SimulatedSeconds = seconds;
            if (seconds < 1.0) return report;

            double[] before = (double[])s.Res.Amount.Clone();
            double coinsBefore = s.Res.CoinsEarnedLifetime;

            int steps = (int)Math.Ceiling(seconds / MaxStepSeconds);
            if (steps < 1) steps = 1;
            if (steps > MaxSteps) steps = MaxSteps;
            double dt = seconds / steps;
            for (int i = 0; i < steps; i++) s.Tick(dt);

            report.CoinsGained = s.Res.CoinsEarnedLifetime - coinsBefore;
            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                double d = s.Res.Amount[r] - before[r];
                report.Delta[r] = d > 0 ? d : 0;
            }
            s.Stats.OfflineSecondsTotal += seconds;
            s.RaiseChanged();
            return report;
        }
    }
}
