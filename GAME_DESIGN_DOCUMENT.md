# Ant Scout: Pheromone Rush
*A Dynamic Resource-Spawn Micro-RTS / Action Hybrid*

---

## 1. Executive Summary & Vision

*Ant Scout: Pheromone Rush* is an action-strategy hybrid designed to eliminate traditional RTS "turtling" and base-building bloat in favor of continuous, dynamic skirmishes. 

Instead of constructing static bases and harvesting fixed crystal patches, players control a fast, fragile **Scout Ant** in a bustling backyard/pavement environment. High-value food sources (a dropped grape, a dead beetle, an aphid cluster) spawn dynamically across the map, triggering high-stakes scrambles between your Black Ant colony and rival Red Ants. The player does not micromanage units with a selection box; instead, the player lays **chemical pheromone scent trails** connecting active discovery sites back to the home nest, guiding swarms of autonomous workers and soldier ants into the fray.

### Key Game Pillars
1. **Dynamic Bonanzas over Static Bases**: Resources are ephemeral, sudden, and contested. Map control is fluid rather than static.
2. **The Vanguard Scout**: You are the eyes and nervous system of the colony. High-speed kiting, evasion, and reconnaissance take precedence over brute-force tanking.
3. **Pheromone-Driven Swarm Macro**: You draw your own supply and reinforcement highways. If your trail reaches the nest, backup arrives; if severed by enemies, you are isolated.
4. **Minimalist Visual Punch (Zero Asset Fatigue)**: Polished geometric silhouettes, high-contrast team colors (Black/Cyan vs. Crimson Red), glowing neon scent ribbons, and kinetic screenshake / hit-stop over laborious hand-drawn art.
5. **Scope Discipline**: A focused 7-day development roadmap designed to avoid burnout while establishing a rock-solid, extensible codebase.

---

## 2. Core Gameplay Loop

```
 ┌────────────────────────────────────────────────────────┐
 │ 1. RECONNAISSANCE & DISCOVERY                          │
 │ - Scout explores uncharted yard / fog of war.          │
 │ - Audio/visual ping alerts map of dynamic food drop.   │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 2. TRAIL LAYING & RECRUITMENT                          │
 │ - Scout activates pheromone gland while scurrying.     │
 │ - Trail connects discovery site back to Colony Nest.   │
 │ - Colony catches scent; Workers & Soldiers deploy.     │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 3. PERIMETER CONTEST & HARASSMENT                      │
 │ - Rival Red Ants arrive to contest the drop.           │
 │ - Scout kites, dodges, and tags priority targets.      │
 │ - Allied Soldiers clash with Red Ants; Workers strip.  │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 4. SECURE, REINFORCE & EVOLVE                          │
 │ - Workers haul food chunks back to Nest.               │
 │ - Nest biomass grows -> Unlocks swarm buffs/upgrades.  │
 └────────────────────────────────────────────────────────┘
```

---

## 3. The Player: The Scout Ant

The player controls an agile, specialized reconnaissance ant. 

### Stats & Movement
* **High Base Velocity**: Faster than worker and soldier ants.
* **Scurry / Dash (Spacebar)**: A burst of acceleration with a brief cooldown to escape enemy surrounds or cross open hazards.
* **Fragile Health**: Cannot face-tank enemy soldiers in direct melee combat.

### Pheromone Gland System
* **Pheromone Capacity**: A regenerating resource meter representing the chemical reserve in the scout's abdomen.
* **Trail Laying (Hold Right Click / Shift)**: Deplete gland reserves to drop a connected chain of scent nodes as you run.
* **Chemical Types**:
  * **Recruitment / Highway Scent**: Draws workers and soldiers from the nest along the drawn vector path.
  * **Alarm / Target Paint (Left Click on Target)**: Marks a specific enemy or carcass. Allied soldiers within range enter a frenzy state and focus fire.

---

## 4. World & Dynamic Resource Mechanics

### Dynamic Food Drops ("Bonanzas")
* **Spawn Intervals**: Periodic events (every 30–60 seconds) at semi-random map coordinates.
* **Visual & Audio Announcement**: Scent waves ripple across the screen edge pointing toward the drop location.
* **Food Types**:
  * *Sugar Granules / Honeydew*: Lightweight, rapid collection for quick worker boosts.
  * *Fallen Fruit (Grapes/Berries)*: Massive resource pools requiring prolonged perimeter defense.
  * *Bug Carcasses (Beetles/Moths)*: Requires multiple ants working together to harvest or drag.

### Nest & Swarm Units
* **The Colony Nest**: The home base. Acts as the delivery depot and spawning ground for:
  * **Worker Ants**: Non-combatants. Follow pheromone trails to harvest food chunks and haul them back.
  * **Soldier Ants**: Heavy combatants. Defend workers along the trail and engage hostile units.

---

## 5. Architectural Standards & Design Philosophy

To prevent codebase rot and maintain strict professional quality, the project follows these mandatory engineering guidelines:

### A. SOLID Principles
* **Single Responsibility (SRP)**: Each class manages only one domain concern (e.g. `ScoutMotor` moves the player, `PheromoneGland` manages chemical pools, `TrailRenderer` draws visuals).
* **Open/Closed (OCP)**: New ant castes or resource types implement common interfaces (`ISwarmAgent`, `IHarvestable`) without modifying the core simulation loops.
* **Liskov Substitution (LSP)**: Any unit implementing `ISwarmAgent` can be navigated by the pheromone steering system interchangeably.
* **Interface Segregation (ISP)**: Interfaces are kept lean and focused (e.g. `IDamageable`, `IPheromoneSensor`, `IControllable`).
* **Dependency Inversion (DIP)**: High-level systems interact with pure domain abstractions rather than concrete engine implementations.

### B. Anti-Hardcoding & Configuration Standards
* **No Magic Numbers or Strings**: Speeds, timers, health totals, radii, and decay rates are strictly defined in dedicated `ScriptableObject` assets with clear tooltips and ranges.
* **Fail-Fast & Strict Exceptions**: Missing configuration assets, unassigned references, or invalid simulation states throw explicit exceptions (e.g. `InvalidOperationException`) immediately rather than failing silently with default fallbacks.
* **Inspector Tunability**: Game balance can be tuned live during Play mode by editing configuration assets.

---

## 6. Industry Benchmarks & References

* **Sim Ant (Maxis)**: Biological authenticity, pheromone trail recruitment, nest dynamics.
* **Tooth and Tail (Pocketwatch Games)**: Indirect RTS command via a physical field commander rather than cursor drag-selection.
* **StarCraft (Blizzard)**: Visceral Zergling surround tactics, dynamic skirmish micro, distinct asymmetric roles.
* **Pikmin (Nintendo)**: Swarm escort, dividing labor between carrying resources and fighting off wildlife.
