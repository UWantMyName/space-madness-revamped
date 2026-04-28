# Space Madness — Level Authoring Guide

## Overview

A level in Space Madness is built entirely from **ScriptableObjects** — no code changes needed per level. The hierarchy is:

```
LevelDefinition
  └── WaveDefinition  (one per wave)
        ├── SlotDefinition    (where aliens park)
        └── AlienDefinition[] (one per alien)
```

Waves play in order. The next wave only begins once **every alien in the current wave is dead**. Each wave has its own slot layout and its own list of alien definitions.

---

## Step 1 — Create an AlienDefinition

**Right-click in Project → Space Madness → Alien Definition**

This defines a single alien type: where it spawns, how it enters the screen, and how it behaves in the Active state.

| Field | Description |
|---|---|
| `Spawn Edge` | Which screen edge the alien enters from: Top, Left, or Right |
| `Spawn Position` | 0–1 fraction along that edge (0 = left/bottom end, 1 = right/top end) |
| `Segments` | The entry path — a list of movement segments the alien follows before slotting |
| `Max HP` | How many bullet hits the alien takes before dying |
| `Lissajous Freq X/Y` | Oscillation frequencies during the Active state. Different ratios (e.g. 1:1.5, 2:3) produce complex non-repeating figure-8 paths |
| `Lissajous Amplitude X/Y` | Max drift distance from the slot in world units |
| `Lissajous Phase Offset` | Seed offset so aliens don't all oscillate in sync. Give each alien a different value |
| `Shoot Interval` | Average seconds between shots during Active state |
| `Shoot Interval Variance` | ± seconds of randomness added to each interval |
| `Preview Screen Height` | Camera `orthographicSize × 2`. Used to draw the entry path gizmo in the Scene view |

### Authoring Segments

Each segment is a step in the entry path. Segments chain together — the alien travels each one in order, then transitions to Slotting.

**Linear segment** — travels in a straight line at a given angle.

| Field | Description |
|---|---|
| `Type` | Linear |
| `Angle In Degrees` | World-space direction. 0 = right, 90 = up, 180 = left, 270 = down |
| `Distance Fraction` | Distance to travel as a fraction of screen height (e.g. 0.3 = 30% of screen height) |
| `Speed` | World units per second |

**Arc segment** — sweeps along a circular arc.

| Field | Description |
|---|---|
| `Type` | Arc |
| `Turn Direction` | Left (counter-clockwise) or Right (clockwise) |
| `Sweep Degrees` | How many degrees to sweep (e.g. 90 = quarter circle, 180 = half circle) |
| `Distance Fraction` | Arc length as a fraction of screen height. The radius is derived from this and Sweep Degrees |
| `Speed` | World units per second |

> **Tip:** Select the alien's GameObject in the Scene view to see the full entry path drawn as a cyan gizmo. Green dot = spawn point. Magenta dot = where Slotting begins.

---

## Step 2 — Create a SlotDefinition

**Right-click in Project → Space Madness → Slot Definition**

This is a list of world-space positions — one position per alien. Aliens travel to these positions after their entry path completes and stay there for the rest of the wave.

| Field | Description |
|---|---|
| `Slots` | Array of `Vector2` world-space positions |

The number of slots in this asset **must match** the number of `AlienDefinitions` in the WaveDefinition that uses it. The WaveManager will warn you in the console if they don't match.

### Slot placement tips

Your camera's orthographic size determines the world bounds. For `orthographicSize = 5`:

- Screen goes from **Y = -5 to +5** (vertical)
- Screen goes from **X ≈ -8.9 to +8.9** (horizontal, at 16:9)

Example — 6 aliens in two rows:

| Slot | X | Y |
|---|---|---|
| S0 | -4.0 | 2.0 |
| S1 | -1.3 | 2.0 |
| S2 | 1.3 | 2.0 |
| S3 | 4.0 | 2.0 |
| S4 | -2.0 | 0.5 |
| S5 | 2.0 | 0.5 |

Slots don't need to form a grid. Irregular layouts make each level feel visually distinct.

> **Tip:** Add a `SlotManager` to the scene and assign the SlotDefinition to it. Yellow spheres labelled S0, S1, S2... appear in the Scene view at each slot position. Red = occupied, Yellow = free.

---

## Step 3 — Create a WaveDefinition

**Right-click in Project → Space Madness → Wave Definition**

One WaveDefinition = one wave of enemies.

| Field | Description |
|---|---|
| `Slot Definition` | The slot layout for this wave |
| `Alien Definitions` | Array of AlienDefinitions — one entry per alien. Must match the slot count |
| `Group Size` | How many aliens spawn together as a group |
| `Delay Before First Group` | Seconds before the first group appears |
| `Delay Between Groups` | Seconds between each group spawning |

### Alien order matters

Aliens spawn in index order and are assigned slots in the same order. Alien 0 gets Slot 0, Alien 1 gets Slot 1, and so on. If you want a specific alien type to end up at a specific slot, order the `Alien Definitions` array accordingly.

### Group spawning example

With 6 aliens, `Group Size = 2`, and `Delay Between Groups = 1.5s`:

```
t = 0.5s  → aliens 0, 1 spawn
t = 2.0s  → aliens 2, 3 spawn
t = 3.5s  → aliens 4, 5 spawn
```

---

## Step 4 — Create a LevelDefinition

**Right-click in Project → Space Madness → Level Definition**

One LevelDefinition = one full level, as an ordered list of waves.

| Field | Description |
|---|---|
| `Waves` | Array of WaveDefinitions, played in order |
| `Delay Between Waves` | Seconds to wait after the last alien of one wave dies before the next wave begins |

---

## Step 5 — Scene Setup

You need two GameObjects in the scene for the level to run.

### SlotManager

1. Create an empty GameObject, name it `SlotManager`
2. Add the `SlotManager` component
3. Assign any SlotDefinition (the WaveManager will swap it per wave automatically)

### WaveManager

1. Create an empty GameObject, name it `WaveManager`
2. Add the `WaveManager` component
3. Fill in the three required fields:

| Field | What to assign |
|---|---|
| `Level Definition` | The LevelDefinition asset for this level |
| `Alien Prefab` | The alien prefab (must have `AlienController`, `AlienHealth`, `Collider2D`) |
| `Slot Manager` | Drag the SlotManager GameObject here |

The WaveManager starts automatically on `Play`. It sequences all waves, swaps the slot layout per wave, spawns groups with the configured delays, and fires events when each wave clears and when the level is complete.

---

## Alien Prefab Requirements

The alien prefab must have these components:

| Component | Required | Notes |
|---|---|---|
| `SpriteRenderer` | ✅ | Any sprite |
| `AlienController` | ✅ | Reads from `AlienDefinition` assigned at spawn time |
| `AlienHealth` | ✅ | Reads `maxHP` from the `AlienDefinition` |
| `Collider2D` | ✅ | `CircleCollider2D` recommended, **Is Trigger = ON** |
| `AlienHitReaction` | Optional | Flash + shake on hit. Works without it |
| `Animator` | Optional | Needed only when death animations are ready |

> The `AlienDefinition` is assigned by the WaveManager at spawn time — leave it blank on the prefab.

---

## Events (for UI, scoring, etc.)

`WaveManager` exposes three C# events you can subscribe to from other scripts:

```csharp
waveManager.OnWaveStarted  += (waveIndex) => { /* e.g. show wave number */ };
waveManager.OnWaveCleared  += (waveIndex) => { /* e.g. award bonus */ };
waveManager.OnLevelComplete += ()         => { /* e.g. load next scene */ };
```

---

## Quick Checklist

Before hitting Play, verify:

- [ ] Every `WaveDefinition` has the same number of `AlienDefinitions` and slots in its `SlotDefinition`
- [ ] Every `AlienDefinition` has at least one segment
- [ ] `previewScreenHeight` on each `AlienDefinition` matches your camera's `orthographicSize × 2`
- [ ] The alien prefab has a `Collider2D` with **Is Trigger = ON**
- [ ] `WaveManager` has `Level Definition`, `Alien Prefab`, and `Slot Manager` assigned
- [ ] The player bullet prefab has a `Rigidbody2D` (Kinematic, Continuous) and a `Collider2D`

---

## File Summary

| Script | Purpose |
|---|---|
| `AlienData.cs` | Enums, `AlienSegment`, and `AlienDefinition` ScriptableObject |
| `AlienPathSimulator.cs` | Pure math path simulation (used by gizmos and future editor window) |
| `ScreenUtils.cs` | Screen-space helpers (spawn positions, angle → direction) |
| `AlienController.cs` | State machine: Entry → Slotting → Active (Lissajous + Kamikaze stub) |
| `AlienHealth.cs` | HP tracking, hit detection, death sequence |
| `AlienHitReaction.cs` | Flash white + shake on hit |
| `SlotDefinition.cs` | ScriptableObject — list of slot positions for one wave |
| `SlotManager.cs` | Runtime slot tracker — assigns and frees slots |
| `WaveDefinition.cs` | ScriptableObject — one wave (aliens, slots, group timing) |
| `LevelDefinition.cs` | ScriptableObject — ordered list of waves for a level |
| `WaveManager.cs` | Sequences waves, spawns groups, tracks alien deaths |