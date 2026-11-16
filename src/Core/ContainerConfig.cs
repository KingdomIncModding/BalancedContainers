using System;
using BepInEx.Configuration;
using KingdomFoundation.Api;

namespace BalancedContainers
{
    public sealed class ContainerConfig
    {
        public ConfigEntry<float> RollingWindowSeconds { get; }
        public ConfigEntry<bool> ShowPerMinute { get; }
        public ConfigEntry<int> MaxHeaderEntries { get; }
        public ConfigEntry<bool> ShowHeaderOnRegularContainers { get; }
        public ConfigEntry<bool> AutoOpenOnContainerCreate { get; }
        public ConfigEntry<float> HeaderScale { get; }
        public ConfigEntry<int> OperationalTolerancePercent { get; }

        public event Action Changed;

        public ContainerConfig(ConfigFile config)
        {
            RollingWindowSeconds = config.Bind(
                "Tracking",
                "RollingWindowSeconds",
                10f,
                new ConfigDescription("Rolling time window in seconds for Current production rates.", new AcceptableValueRange<float>(3f, 30f)));

            ShowPerMinute = config.Bind(
                "Display",
                "ShowPerMinute",
                false,
                "Display production rates per minute (/m) instead of per second (/s).");

            MaxHeaderEntries = config.Bind(
                "Display",
                "MaxHeaderEntries",
                8,
                new ConfigDescription("Maximum number of resource entries shown in a container header.", new AcceptableValueRange<int>(1, 16)));

            ShowHeaderOnRegularContainers = config.Bind(
                "Display",
                "ShowHeaderOnRegularContainers",
                true,
                "Show production summary on containers without an assigned End Product / Currency when they contain active buildings.");

            AutoOpenOnContainerCreate = config.Bind(
                "Behavior",
                "AutoOpenOnContainerCreate",
                true,
                "Automatically prompt to choose an End Product or Currency when creating a new container.");

            HeaderScale = config.Bind(
                "Display",
                "HeaderScale",
                1.15f,
                new ConfigDescription("Scale multiplier of the container production header bar.", new AcceptableValueRange<float>(0.75f, 2.0f)));

            OperationalTolerancePercent = config.Bind(
                "Tracking",
                "OperationalTolerancePercent",
                95,
                new ConfigDescription("Minimum percentage of potential throughput (Current / Potential) considered Operational.", new AcceptableValueRange<int>(50, 100)));

            RollingWindowSeconds.SettingChanged += (_, _) => Changed?.Invoke();
            ShowPerMinute.SettingChanged += (_, _) => Changed?.Invoke();
            MaxHeaderEntries.SettingChanged += (_, _) => Changed?.Invoke();
            ShowHeaderOnRegularContainers.SettingChanged += (_, _) => Changed?.Invoke();
            AutoOpenOnContainerCreate.SettingChanged += (_, _) => Changed?.Invoke();
            HeaderScale.SettingChanged += (_, _) => Changed?.Invoke();
            OperationalTolerancePercent.SettingChanged += (_, _) => Changed?.Invoke();
        }

        public void RegisterModConfig(string modId, string modName)
        {
            var section = FoundationApi.ModConfig?.GetOrCreateSection(
                modId,
                modName,
                "Assign End Products or Currencies to containers and display live Current / (Potential) production numbers.");

            if (section == null)
            {
                return;
            }

            section.AddToggle(AutoOpenOnContainerCreate, "Prompt on New Container");
            section.AddToggle(ShowHeaderOnRegularContainers, "Show Rates on Regular Containers");
            section.AddToggle(ShowPerMinute, "Display Rates per Minute (/m)");
            section.AddSlider(RollingWindowSeconds, 3f, 30f, step: 1f, "Rate Averaging Window (s)", v => $"{v:0}s");
            section.AddSlider(OperationalTolerancePercent, 50, 100, "Operational Tolerance (%)");
            section.AddSlider(MaxHeaderEntries, 1, 16, "Max Resources in Header");
            section.AddSlider(HeaderScale, 0.75f, 2.0f, step: 0.05f, "Header Bar Scale", v => $"{v:0.##}x");
        }
    }
}

