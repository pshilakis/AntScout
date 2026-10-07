# Ant Scout: Pheromone Rush
*A Dynamic Hero-RTS / MOBA Hybrid*

---

## 1. Executive Summary & Vision

*Ant Scout: Pheromone Rush* is an action-strategy hybrid positioned squarely between a **MOBA (Hero-centric action & skill progression)** and an **RTS (macro resource gathering & indirect swarm management)**.

Instead of traditional RTS factions with fixed, asymmetric races (e.g., Terran/Zerg/Protoss), all players operate a biological Colony Nest, but at the start of each match select a specialized **Hero Ant Caste** (e.g., **Scout**, **Soldier**, and future castes). 

The chosen Hero Ant is the player's direct physical commander on the field. During the match, players harvest dynamic resource drops ("Bonanzas") to fuel their colony's economy and earn **Evolutionary Biomass**, unlocking branching in-match **Skill Trees**. Furthermore, the player's Hero Caste directly reshapes the attributes and behaviors of the autonomous minions spawned from their nest:
* **Scout Hero colonies** deploy fast, wide-ranging foragers with expansive sensory detection bubbles.
* **Soldier Hero colonies** deploy armored, hard-hitting phalanxes that hit hard and soak damage at the cost of mobility.

### Key Game Pillars
1. **Hero Ant + Swarm Synergy (MOBA meets RTS Macro)**: You control a powerful, customizable Hero avatar with distinct active abilities while indirectly guiding swarms of autonomous workers and combat escorts via chemical pheromone ribbons.
2. **Asymmetric Hero Castes over Rigid Races**: Matchup diversity comes from player-selected Hero Ants and in-match skilltree adaptations rather than disparate base tech trees.
3. **Dynamic Resource Bonanzas over Static Bases**: High-value food sources (a dropped grape, a dead beetle, an aphid cluster) spawn dynamically in neutral territory, forcing continuous movement, skirmishing, and map control.
4. **Pheromone-Driven Supply Lines**: You draw physical chemical scent highways. If your trail connects to the nest, reinforcements and harvesters stream forward; if severed or contested by rival colonies, your vanguard is cut off.
5. **Minimalist Visual Punch (Zero Asset Fatigue)**: High-contrast silhouettes, glowing neon scent ribbons, kinetic screen juice (hit-stop, screenshake, pheromone pulses), and clean geometric clarity.

---

## 2. Core Gameplay Loop

```
 ┌────────────────────────────────────────────────────────┐
 │ 0. HERO SELECTION & DEPLOYMENT                         │
 │ - Player selects Hero Caste (Scout, Soldier, etc.).    │
 │ - Hero Caste sets base stats, abilities, & swarm bias. │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 1. RECONNAISSANCE & DYNAMIC BONANZA DROP               │
 │ - Hero explores the yard / fog of war.                 │
 │ - Audio/visual pheromone ping announces a food drop.   │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 2. PHEROMONE HIGHWAYS & SWARM MOBILIZATION             │
 │ - Hero sprays recruitment scent connecting drop to Nest│
 │ - Nest spawns specialized caste minions along trail.   │
 │ - Workers harvest; combat escorts secure perimeter.    │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 3. HERO SKIRMISH & OBJECTIVE CONTEST                   │
 │ - Rival Hero and enemy swarm contest the food site.    │
 │ - Heroes clash using active cooldown abilities (Q/W/E) │
 │ - Tactical plays: sever enemy trails, paint targets.   │
 └──────────────────────────┬─────────────────────────────┘
                            │
                            ▼
 ┌────────────────────────────────────────────────────────┐
 │ 4. BIOMASS DELIVERY & IN-MATCH SKILLTREE PROGRESSION   │
 │ - Workers deposit food -> Colony Biomass pool grows.   │
 │ - Level up Hero Skill Tree: unlock active/passive buffs│
 │ - Colony evolves; push toward enemy nest or win score. │
 └────────────────────────────────────────────────────────┘
```

---

## 3. Hero Ant Castes & Swarm Specializations

At match start, players lock in their Hero Ant. The selection determines the Hero's direct kit and modifies the colony's minion swarm:

```
                      ┌──────────────────────┐
                      │ HERO ANT SELECTION   │
                      └──────────┬───────────┘
                                 │
            ┌────────────────────┴────────────────────┐
            ▼                                         ▼
 ┌──────────────────────┐                  ┌──────────────────────┐
 │   THE SCOUT HERO     │                  │   THE SOLDIER HERO   │
 ├──────────────────────┤                  ├──────────────────────┤
 │ • Agile & Fragile    │                  │ • Armored Bruiser    │
 │ • Deep Scent Glands  │                  │ • High Melee Damage  │
 │ • Multi-Dash Mobility│                  │ • Slow Base Speed    │
 └──────────┬───────────┘                  └──────────┬───────────┘
            │                                         │
            ▼                                         ▼
 ┌──────────────────────┐                  ┌──────────────────────┐
 │ SCOUT SWARM MINIONS  │                  │ SOLDIER SWARM MINIONS│
 ├──────────────────────┤                  ├──────────────────────┤
 │ • +35% Move Speed    │                  │ • +50% HP & Defense  │
 │ • 12m+ Scent Bubble  │                  │ • Heavy Mandible Bite│
 │ • Wide Area Search   │                  │ • -20% Move Speed    │
 │ • Rapid Food Hauling │                  │ • Tight Perimeter Hub│
 └──────────────────────┘                  └──────────────────────┘
```

---

### Caste 1: The Scout (Recon & High-Speed Macro)

* **Hero Playstyle**: Fast, evasive, and opportunistic. Excels at hit-and-run tactics, cutting enemy supply trails, and claiming distant food drops before opponents can react.
* **Hero Base Attributes**:
  * Very high base speed (~8.5 m/s) and rapid turning.
  * Fragile chitin (low health pool); vulnerable to direct soldier surrounds.
  * Large pheromone reservoir with rapid chemical recharge.
* **Colony Swarm Modifiers**:
  * **Swift Foragers**: Worker and escort ants move +35% faster.
  * **Expansive Antennal Reach**: Minion sensory sampling radius increased to 12.0m+; wider Area-Restricted Search (6.0m+ wander radius at trail ends).
  * **Efficient Carrying**: Workers harvest and deposit food chunks with lower turnaround latency.
* **Hero Skill Tree Paths**:
  * *Mobility & Evasion*: Multi-charge dash, terrain hops/scurrying, trail-haste passive (movement speed boost while running on active pheromone trails).
  * *Chemical Trailcraft*: Dense highways (trails last longer and cost less energy), blinding repellent scent (disrupts enemy worker tracking), speed-boosting scent ribbons.
  * *Scouting Radar*: Pheromone pulse (briefly reveals distant bonanzas and enemy hero location through fog of war).

---

### Caste 2: The Soldier (Frontline Enforcer & Area Denial)

* **Hero Playstyle**: A durable brawler designed to anchor contested resource nodes, crush enemy harvesters, and duel rival heroes in brutal close-quarters combat.
* **Hero Base Attributes**:
  * High health pool and passive damage resistance (thick chitin).
  * Crushing mandibles dealing heavy single-target damage and cleave.
  * Lower movement speed (~5.0 m/s); relies on positioning and crowd control.
* **Colony Swarm Modifiers**:
  * **Chitin Phalanx**: Minions spawn with +50% health and increased mandible strike power.
  * **Escort Protocol**: Soldier minions prioritize forming defensive rings around active food clusters.
  * **Weighted March**: Minions move ~20% slower; tighter, more disciplined area-restricted wander radii (3.5m) to maintain defensive cohesion.
* **Hero Skill Tree Paths**:
  * *Mandible Mastery*: Armor-crushing bite (reduces target defense), sweeping cleave attack, jaw-clamp root (temporarily immobilizes fleeing scouts).
  * *Pheromone Roar / Command*: Battle frenzy aura (temporarily buffs allied minion attack speed and tenacity), alarm paint (focus-fires all nearby allied soldiers onto a marked enemy).
  * *Juggernaut Carapace*: Reactive chitin plating (reduces incoming burst damage), shockwave ground stomp (stuns or knocks back enemy swarms).

---

### Future Hero Castes (Open/Closed Architecture)
* **The Weaver / Formicine (Artillery & Trapper)**: Ranged formic acid spit, slowing silk snares, lingering toxic chemical puddles.
* **The Queen's Attendant / Nurse (Bio-Surgeon & Support)**: Accelerated egg incubation, healing pheromone mist, bio-conversion doubling harvested resource yield.

---

## 4. In-Match Skill Tree & Progression System

Progression in *Ant Scout* is session-based and dynamic, mirroring modern MOBA/action-RTS mechanics:

1. **Biomass Collection**:
   - Every food chunk delivered to the Colony Nest grants shared **Evolutionary Biomass**.
   - Defeating enemy heroes, soldiers, or wildlife also yields concentrated biomass droplets.
2. **Tiered Upgrades (Tiers 1 – 4)**:
   - As colony biomass thresholds are met, the player unlocks a Skill Point.
   - Players select from mutually exclusive or branching upgrades in the field (via hotkeys or quick-select UI).
3. **Strategic Counter-Building**:
   - If a Scout faces a heavy Soldier, they can tech into *Acidic Trail Dissolver* or *Multi-Charge Sprint*.
   - If a Soldier faces an evasive Scout, they can tech into *Jaw-Clamp Root* or *Pheromone Alarm Roar*.

---

## 5. Architectural Standards & SOLID Grounding

To support this Hero-RTS hybrid without system sprawl, the codebase adheres to strict software design principles:

### A. SOLID Principles in Practice
* **Single Responsibility (SRP)**:
  * `HeroMotor`: Pure 2D movement and physical translation.
  * `HeroAbilitySystem`: Manages cooldowns, energy costs, and ability triggering.
  * `HeroSkillTree`: Tracks unlocked tiers and dispatches stat/ability modifiers.
  * `SwarmColonyModifier`: Applies caste-specific stat multipliers to spawned minions.
* **Open/Closed (OCP)**:
  * New Hero Castes are created by implementing polymorphic contracts (`IHeroCaste`, `IAbility`, `ISkillNode`) and ScriptableObject definitions without rewriting core movement or nest spawning loops.
* **Liskov Substitution (LSP)**:
  * Any hero ant caste swaps transparently into player input controllers, camera followers, and HUD elements.
* **Interface Segregation (ISP)**:
  * Modular interfaces such as `IDashable`, `IPheromoneEmitter`, `IDamageable`, `ISkillTreeTarget` keep class footprints lean and testable.
* **Dependency Inversion (DIP)**:
  * High-level gameplay systems depend on pure abstractions (`IPheromoneEmitter`, `ISwarmAgent`, `IHarvestable`) rather than concrete Monobehaviours.

### B. Anti-Hardcoding & Configuration Standards
* **ScriptableObject Single Source of Truth**: All hero speeds, health values, ability cooldowns, minion stat multipliers, and skilltree nodes are configured in dedicated `ConfigSO` assets.
* **Fail-Fast Standard**: Mandatory references throw descriptive `InvalidOperationException` or `ArgumentNullException` immediately on `Awake`/`Initialize` rather than silently degrading with magic numbers.

---

## 6. Industry Benchmarks & Case Studies

* **Warcraft III (Blizzard)**: Hero-centric army synergy, field leveling, and ability-based skirmish micro.
* **Tooth and Tail (Pocketwatch Games)**: Indirect RTS command executed strictly through the player's physical avatar rather than cursor boxes.
* **Battlerite / Heroes of the Storm (Stunlock / Blizzard)**: Kinetic, crisp skillshots and talent tree customization without tedious inventory/item management.
* **StarCraft II (Blizzard)**: Visceral Zergling swarm fluid dynamics, surround behaviors, and macro economic rhythm.
* **SimAnt (Maxis)**: Authentic biological foundation—chemical pheromone recruitment and colony caste division.
