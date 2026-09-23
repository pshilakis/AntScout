# Ant Scout: Pheromone Rush - Phased Implementation Plan

This document outlines the step-by-step roadmap to build *Ant Scout: Pheromone Rush* within a focused 7-day development cycle. Each stage is small, self-contained, and builds strictly upon the previous foundation.

---

## 1. Development Stages Overview

| Stage | Day | Focus Area | Key Deliverable |
| :--- | :--- | :--- | :--- |
| **Stage 1** | Day 1 | **Scout Controller & Pheromone Emitter** | Responsive 2D twin-stick scout movement, dash, and visual scent ribbon laying. |
| **Stage 2** | Day 2 | **Nest & Pheromone Swarm Agents** | Allied Workers/Soldiers spawned at Nest following the laid scent gradient. |
| **Stage 3** | Day 3 | **Dynamic Resource Bonanza System** | Timed resource drops (fruit, sugar) that deplete when harvested and reward the nest. |
| **Stage 4** | Day 4 | **Combat, Aggro & Rival Red Ants** | Enemy red colony scouts/soldiers contesting food drops and engaging player/allies. |
| **Stage 5** | Day 5 | **Colony Evolution & Macro Economy** | Food conversion into reinforcements, caste unlocks, and win/loss scoring. |
| **Stage 6** | Day 6 | **Audio, VFX & Screen Juice** | Scent pulses, hit-stop, damage numbers, sound effects, and telegraph circles. |
| **Stage 7** | Day 7 | **Balance, Playtesting & Standalone Build** | Tuning config values in ScriptableObjects and generating a playable release. |

---

## 2. Stage 1 Deep Dive: Scout Controller & Pheromone Emitter

### Objective
Create an agile, responsive top-down character controller for the Scout Ant with dash capability, and a decoupled trail emitter that deposits scent nodes when the player triggers the gland.

### Class Architecture & Contracts

```
Assets/
└── Scripts/
    ├── Config/
    │   └── ScoutConfigSO.cs            <-- Tunable movement/dash/gland parameters
    ├── Core/
    │   ├── Interfaces/
    │   │   ├── IMotor2D.cs             <-- Interface for movement/rotation
    │   │   ├── IDashable.cs            <-- Interface for dash mechanic
    │   │   └── IPheromoneEmitter.cs    <-- Interface for depositing scent nodes
    │   └── Enums/
    │       └── PheromoneType.cs        <-- Recruitment, Alarm, Repellent
    ├── Player/
    │   ├── ScoutMotor2D.cs             <-- Rigidbody2D movement & smooth turning
    │   ├── ScoutDash.cs                <-- Dash cooldown & impulse execution
    │   ├── ScoutGland.cs               <-- Pheromone capacity, recharge & trigger
    │   └── ScoutInputHandler.cs        <-- Translates raw input to actions
    └── Pheromone/
        ├── PheromoneNode.cs            <-- Data struct/class (pos, intensity, timestamp)
        ├── PheromoneTrailEmitter.cs    <-- Implements IPheromoneEmitter; manages node chain
        └── PheromoneTrailRenderer2D.cs <-- LineRenderer / visual representation
```

### Detailed Class Responsibilities

#### `ScoutConfigSO.cs` (ScriptableObject)
Centralized source of truth. Contains no magic numbers:
* `MoveSpeed` (float, e.g., 8.0)
* `Acceleration` (float, e.g., 25.0)
* `RotationSpeed` (float, e.g., 720.0 deg/sec)
* `DashImpulse` (float, e.g., 18.0)
* `DashDuration` (float, e.g., 0.15 sec)
* `DashCooldown` (float, e.g., 1.5 sec)
* `GlandCapacity` (float, e.g., 100.0)
* `GlandDrainRate` (float, e.g., 25.0 / sec)
* `GlandRechargeRate` (float, e.g., 15.0 / sec)
* `NodeDropDistance` (float, e.g., 0.5 units between scent nodes)

#### `ScoutMotor2D.cs`
* **Single Responsibility**: Translating 2D direction vectors into physics movement via `Rigidbody2D.linearVelocity` (or `velocity`) and orienting the ant's sprite toward the movement/aim direction.
* Strict validation: Throws `InvalidOperationException` in `Awake` if `ScoutConfigSO` or `Rigidbody2D` is unassigned.

#### `ScoutDash.cs`
* Handles short burst acceleration with invulnerability or high speed.
* Manages the internal timer and notifies observers (or UI) of cooldown progress.

#### `ScoutGland.cs`
* Tracks chemical reserves.
* When active, queries distance traveled since the last dropped node; if `> NodeDropDistance`, calls `_emitter.EmitNode(position, type)`.

#### `PheromoneTrailEmitter.cs` & `PheromoneTrailRenderer2D.cs`
* Stores the chain of active scent nodes with fading intensity.
* Renders the visible glowing trail using a configured `LineRenderer` or particle system.

---

## 3. Verification & Acceptance Criteria for Stage 1

1. **Controls Verification**:
   - WASD moves the scout ant smoothly with sharp acceleration and turning.
   - Spacebar triggers a snappy dash in the current movement direction, followed by an accurate cooldown timer.
2. **Pheromone Trail Verification**:
   - Holding Right Click (or Shift) deposits continuous scent nodes along the player's path.
   - Releasing the key stops emission immediately.
   - Scent nodes visibly render as a connected glowing ribbon and gradually fade over a configured duration.
3. **Configuration Verification**:
   - Altering values in `ScoutConfigSO` during Play mode immediately changes speed, dash impulse, and trail spacing without needing code recompilation.
   - Zero hardcoded numbers or strings in code.

---

## 4. Next Steps
Once Stage 1 is verified in the Unity Editor, we proceed to **Stage 2: Nest & Pheromone Swarm Agents**, where autonomous worker ants will detect the trail and follow it out of the nest!
