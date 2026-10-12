# Ant Scout: Pheromone Rush (`AntScout`)

A dynamic **Hero-RTS / MOBA hybrid** built in Unity, combining direct top-down action control of a specialized Hero Ant with indirect macro swarm guidance via world-space chemical pheromone ribbons.

---

## 1. Architectural Overview & System Design

```
┌────────────────────────────────────────────────────────────────────────┐
│                        [MANAGERS] SCENE ENGINES                        │
│   • PgsTime (Centralized simulation ticks on TimeChannel.Player)      │
│   • PgsInputReader (Continuous 2D streams & temporal action buffer)    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                    SCOUT HERO RIG (Scout.prefab)                       │
│                                                                        │
│   [Root Components (5 Only)]                                          │
│   1. CapsuleCollider         - Physical sweep volume                   │
│   2. CharacterCollider3D     - Body dimensions & clearance             │
│   3. KinematicMotor3D        - Non-alloc sweep-and-slide solver        │
│   4. PlayerController        - Traversal module orchestrator           │
│   5. ScoutHero               - Domain aggregate root                   │
│                                                                        │
│   [Child Hierarchy]                                                    │
│   └── Visual                 - Render mesh interpolated at 144Hz+      │
│                                                                        │
│   [Dynamic Runtime Bootstrapping (From ScoutConfigSO)]                 │
│   ├── ScoutPlayerInputBridge - Connects PgsInputReader to PlayerController│
│   ├── ScoutGland             - Pheromone energy & rate tracking        │
│   ├── PheromoneTrail.prefab  - Spawned in world-space (Decoupled)      │
│   └── RVOController          - A* swarm avoidance & velocity sync      │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Shared Package Integrations in AntScout

AntScout is built on top of decoupled, production-grade PGS shared packages:

### A. Locomotion & Physics (`com.pgs.player`)
* **Kinematic Sweep-and-Slide (`KinematicMotor3D`)**: Eliminates Unity's legacy `CharacterController` snags and `Rigidbody` floatiness. Solves surface tangent slide planes ($\vec{V}_{\text{slide}} = \vec{V} - (\vec{V}\cdot\hat{N})\hat{N}$) with 0 GC allocations.
* **Capsule Dimension Management (`CharacterCollider3D`)**: Maintains explicit body height ($0.5\text{m}$) and radius ($0.3\text{m}$) for insect chassis dimensions.
* **Centralized Simulation via `PGS.Core.Time`**: Motor simulation runs strictly via `IFixedUpdatable` on `TimeChannel.Player`, decoupling character physics from frame rate.
* **High-Refresh Visual Mesh Interpolation**: The root capsule moves on the fixed physics tick ($50\text{Hz}$), while the child `Visual` mesh is interpolated smoothly at $144\text{Hz}+$ in `OnLateUpdate`, eliminating camera judder.
* **Modular Traversal Pipeline (`ITraversalModule`)**: Traversal logic is encapsulated in `ScoutTraversalDefinition.asset` using `GroundTraversalModule` (`[SerializeReference, SubclassSelector]`).
* **32-Bit Action Channels (`TraversalActionChannel`)**: Decouples hardware keys from character verbs:
  * **Channel 1 (`Secondary`)**: Spray Pheromone Ribbon.
  * **Channel 3 (`Utility`)**: Scurry Dash.

### B. Input Abstraction (`com.pgs.input`)
* **`PgsInputReader`**: Centralized scene reader listening to `PgsControls.inputactions`.
* **Continuous Spatial Streams**: Reads `Primary2D` with circular deadzones and dynamic magnitude clamping.
* **Temporal Input Buffering**: Native buffering records actions against `PgsTime.CurrentTick`, ensuring dash impulses queue smoothly before cooldown completion without dropping inputs.
* **`ScoutPlayerInputBridge`**: Game-level adapter on the Scout converting `PgsInputReader` streams into camera-relative `PlayerInputContext`.

### C. Isometric Camera Tracking (`com.pgs.camera`)
* **`ThirdPersonVirtualCamera`**: Smooth orbital follow camera with configurable distance, elevation angle, and position damping.
* **`IsometricCameraInputBridge`**: Project adapter that auto-targets the GameObject tagged `"Player"`, calculating camera forward/right tangent vectors so WASD movement maps cleanly to isometric screen space.

### D. Centralized Simulation & Serialization (`com.pgs.core`)
* **`PgsTime`**: Host ticker managing simulation updates without native Unity `Update()`/`FixedUpdate()` overhead. Automatically instantiates via `[RuntimeInitializeOnLoadMethod]`.
* **`[GenerateGuid]` & `[SubclassSelector]`**: Ensures immutable asset identifiers and clean polymorphic inspector dropdowns for modular abilities.

---

## 3. The Streamlined Scout Chassis (`Scout.prefab`)

To eliminate component bloat and configuration confusion, `Scout.prefab` has been stripped down to a minimal physical chassis containing only **5 components on the root**:

| Component | Responsibility |
| :--- | :--- |
| **`CapsuleCollider`** | Physical volume swept by `KinematicMotor3D` non-alloc queries. |
| **`CharacterCollider3D`** | Defines insect dimensions (height $0.5\text{m}$, radius $0.3\text{m}$, skin width $0.02\text{m}$). |
| **`KinematicMotor3D`** | Solves sweep-and-slide movement, slope climbing, and sub-tick visual mesh interpolation. |
| **`PlayerController`** | Reads input intent from `IPlayerInputProvider` and drives `GroundTraversalModule`. |
| **`ScoutHero`** | Domain aggregate root coordinating abilities, scent gland, and swarm avoidance. |

### Eliminated Legacy Bloat
* ❌ **Built-in `CharacterController`**: Removed (superseded by `CharacterCollider3D` + `KinematicMotor3D`).
* ❌ **`ScoutMotor` & `IMotor`**: Removed (superseded by `PlayerController` & `KinematicMotor3D`).
* ❌ **`ScoutDash` & `IDashable`**: Removed (dash impulses execute natively via `ScoutHero.Abilities.cs`).
* ❌ **`ScoutInputHandler`**: Removed (superseded by zero-alloc `ScoutPlayerInputBridge`).
* ❌ **Hardcoded `RVOController` on Prefab**: Removed (dynamically attached and configured at runtime by `ScoutHero.RVO.cs`).
* ❌ **Child `PheromoneTrail` inside Prefab**: Removed (world scent ribbons spawn as decoupled world-space objects via `PheromoneTrail.prefab`).

---

## 4. Dynamic Runtime Bootstrapping & Partial Class Architecture

All hero capabilities are configured in data assets (`ScoutConfigSO.asset`) and bootstrapped dynamically at runtime by `ScoutHero`:

```
Assets/Scripts/Player/
├── ScoutHero.cs              # Core chassis validation, input bridge bootstrap, locomotion linkage
├── ScoutHero.Abilities.cs    # Scurry dash impulse injection, cooldown timers, dash events
├── ScoutHero.Pheromones.cs   # Dynamic ScoutGland bootstrap & world-space scent ribbon spawning
└── ScoutHero.RVO.cs          # Dynamic RVOController bootstrap & deterministic velocity sync
```

### 1. Locomotion Bootstrap (`ScoutHero.cs`)
* In `Awake()`, `ScoutHero` validates required references, links `_modelTransform` to `KinematicMotor3D`, loads `ScoutTraversalDefinition.asset` into `PlayerController`, and configures `ScoutPlayerInputBridge`.
* Automatically synchronizes base movement speed from `ScoutConfigSO.MoveSpeed` into `GroundTraversalModule.WalkSpeed` and `RunSpeed`.

### 2. Scurry Dash Ability (`ScoutHero.Abilities.cs`)
* Triggered when Action Channel 3 (`Utility`) is pressed (Spacebar / Gamepad South).
* Queries `TryDash()`:
  - Validates dash cooldown against `ScoutConfigSO.DashCooldown`.
  - Calculates impulse vector $\vec{V}_{\text{dash}} = \text{MoveVector} \times \text{DashImpulse}$.
  - Injects horizontal velocity directly into `KinematicMotor3D.SetVelocity()`.
  - Fires `OnDashStarted` and `OnDashCompleted` for VFX/audio hooks.

### 3. Pheromone Gland & World Ribbons (`ScoutHero.Pheromones.cs`)
* Dynamically adds and initializes `ScoutGland` on the Scout root.
* Spawns `PheromoneTrail.prefab` into world space (detached from player transform hierarchy) so scent ribbons persist naturally where deposited.
* When Action Channel 1 (`Secondary`) is held (Left Shift / Gamepad Click):
  - Emits recruitment pheromone nodes along the path at regular intervals (`NodeSpacing = 0.5m`).
  - Worker ants detect and follow the gradient of these world nodes toward food drops.

### 4. Swarm Avoidance Synchronization (`ScoutHero.RVO.cs`)
* Dynamically attaches an A* Pathfinding `RVOController` to the Scout.
* In `UpdateRVOVelocity()`, copies instantaneous physical velocity from `KinematicMotor3D.Velocity` into `RVOController.velocity`.
* Result: autonomous worker ants navigating via RVO actively steer around the Scout without physical pushing or snagging.

---

## 5. Playtesting & Verification Guide in Unity Editor

### Scene Setup Checklist (`SampleScene.unity`)
1. Open `AntScout/Assets/Game Assets/Scenes/SampleScene.unity`.
2. Verify **`[Managers]`** GameObject exists with `PgsInputReader`. (`PgsTime` automatically initializes at runtime before scene load).
3. Verify **`MainCamera`** has `Camera`, `ThirdPersonVirtualCamera`, and `IsometricCameraInputBridge` attached.
4. Verify **`Scout`** has `ScoutHero` with assets assigned:
   - **Config**: `ScoutConfig.asset`
   - **Model Transform**: Child `Visual`
   - **Motor Settings**: `ScoutMotorSettings.asset`
   - **Traversal Definition**: `ScoutTraversalDefinition.asset`
   - **Pheromone Trail Prefab**: `PheromoneTrail.prefab`

### Controls Reference

| Action | Keyboard / Mouse | Gamepad | Description |
| :--- | :--- | :--- | :--- |
| **Move** | `W` / `A` / `S` / `D` | Left Stick | Camera-relative planar insect locomotion. |
| **Scurry / Dash** | `Spacebar` | South Button (`A` / `✕`) | Instant kinematic burst in movement direction. |
| **Spray Scent Ribbon** | `Left Shift` | Left Stick Click (`L3`) | Deposits recruitment pheromone trail for worker ants. |
| **Camera Follow** | Automatic | Automatic | Smooth top-down isometric tracking with collision safety. |

### In-Game Verification Steps
1. **Enter Play Mode** in the Unity Editor.
2. **Move the Scout**: Verify smooth, responsive movement in all directions without snags or stutter.
3. **Trigger Dash**: Press Spacebar while moving. Verify instant forward surge followed by normal movement after cooldown.
4. **Deposit Pheromone Trail**: Hold Left Shift while moving. Verify a glowing scent ribbon appears behind the Scout.
5. **Observe Swarm Workers**:
   - Verify worker ants spawned at the Nest detect the trail and follow it.
   - Verify worker ants steer cleanly around the player without clipping, driven by RVO velocity sync.
