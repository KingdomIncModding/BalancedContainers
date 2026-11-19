# Balanced Containers

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/S6V62AX30M)

Balanced Containers adds live throughput tracking and bottleneck diagnostics directly onto flow containers in Kingdom Inc. Assign an End Product or Currency target to a container—or let regular containers summarize everything running inside them—and spot starved inputs or backed-up outputs at a glance.

## Features

- **Container Target Assignment:** Choose an End Product or Currency when creating a container (or via the Container Options panel) to pin that resource as the container's primary readout.
- **Live Header Bar:** Displays `Current / (Potential)` production rates directly along the top bar of each container without blocking building placement beneath it.
- **Gold Formatting:** Automatically formats Gold rates into Copper (`c`), Silver (`s`), and Gold (`g`) matching in-game currency units.
- **Interactive Breakdown Window:**
  - **Hover** any resource pill in a container's header to view aggregated `[Input]`, individual `[Producers]`, and `[Output]` rates along with a live status diagnosis.
  - **Click** a resource pill to pin the window open, **drag** it anywhere you like (it moves smoothly when panning the map), and **click any producer row** to pan to and highlight that building.
- **Bottleneck Diagnostics:**
  - **Operational:** Meets your configured throughput tolerance (default `95%`).
  - **Starved:** Pinpoints missing upstream supply, disconnected input pipes, insufficient funds/quota, or unconfigured buildings, showing needed vs. current rates.
  - **Saturated / Over-Saturated:** Flags full output buffers, rejected pipes, Shop Gold Cap limits, or excess production—highlighting stalls over 5 seconds in purple with Manifold/Incinerator tips.

## Settings

Configurable in **Settings -> Mod Config -> Balanced Containers**:

- **Prompt on New Container:** Auto-open the target selector when placing a new container.
- **Show Rates on Regular Containers:** Display live rates on containers without an assigned target.
- **Display Rates per Minute (/m):** Switch between `/s` and `/m`.
- **Rate Averaging Window (s):** Rolling window length (`3s–30s`, default `10s`).
- **Operational Tolerance (%):** Minimum throughput ratio considered Operational (`50%–100%`, default `95%`).
- **Max Resources in Header / Header Bar Scale:** Customize header density and size.

## Installation

1. Install **BepInEx 5** and **Foundation** (`0.14.5+`).
2. Extract the `BalancedContainers` folder into `BepInEx/plugins/`.
3. Launch Kingdom Inc.

## Compatibility

| Class.Method | Hook Type | Impact |
|---|---|---|
| FlowDeco.ToJson / FromJson / CreateCopy | Postfix | Saves, loads, and copies assigned container End Product / Currency targets. |
| WorldFlowDeco.UpdateStyle | Postfix | Attaches and refreshes the container production header bar overlay. |
| DecoOptionsPopup.OnEnable | Postfix | Adds the Target selector panel to container decoration options and opens the chooser on new containers when enabled. |
| WorldFlowManager.ShowFlow / GamePlayer.FromJson | Postfix | Refreshes container headers and resets rolling rate history on workspace switches or save loads. |
| WorldFlowElement.SetElement / UpdateIO / WorldFlowGhost.OnDragDropEnd | Postfix | Marks container production headers for recalculation after building or port edits. |
| FlowColliderManager.Overlaps | Postfix | Reserves the container header bar strip so buildings do not overlap header readouts. |
| GameUI.ProcessEscape / ProcessCancel | Prefix | Dismisses a pinned container breakdown window on Escape / Cancel. |
| GamePlayer.AddCurrency | Postfix | Attributes live currency income (Gold, Mana, Crowns, Requisition, Ducats, etc.) to the active building's rolling rate. |
| FlowElement.Update / DoManualCraft / DoCollect / QueueCraft / SoldierDied / UpdateQuestProgress | Prefix / Postfix | Measures per-building item and currency throughput and tracks stalls for Starved, Saturated, and Over-Saturated diagnostics. |

## Support

If you enjoy these Kingdom Inc mods and tools, consider supporting development on Ko-fi!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/S6V62AX30M)
