# Unit Mechanics Reference

## Scope

- This document tracks the current live unit stats and combat rules implemented in code.
- It should stay aligned with the actual state of `RaidMapDemo.cs`.
- This is the English source document for assistant-side maintenance.
- A Chinese mirror document should be refreshed after each meaningful update.
- It is intended as a maintenance reference, not a pitch or wishlist file.

## Global Combat Rules

- `HP`: current and maximum health.
- `Damage`: inclusive random range rolled per attack.
- `Armor`: flat damage reduction applied after any special scaling.
- `AttackRange`: melee or ranged engagement distance.
- `Speed`: base movement speed before sprint or skill overrides.
- `AttackCycleScale`: attack cadence multiplier. Lower is faster.
- `Stamina`: required for sprinting and some active skills.
- `ProjectileDamageScale`: multiplier applied before armor against ranged damage.
- `BlockAnyDamageChance`: chance to negate a hit entirely before damage is applied.

## Shared Soldier Skill Rules

- Most non-ranged soldiers use `Sprint` as their default active skill.
- Archers do not sprint.
- Shield-line soldiers do not sprint.
- Shield-line passive baseline:
- `Missile Guard`: `50%` ranged damage reduction.
- Higher shield tiers can also gain `BlockAnyDamageChance`.

## Controlled Hero

- HP: `24` base hideout state, but the live run value persists and can be lower or higher through current run state.
- Damage: `2-5`
- Range: `164`
- Speed: `165`
- Stamina: base hero template uses `88`
- Current role:
- direct player-controlled fighter,
- ranged-capable,
- search and extraction anchor,
- resource orb collector.

## Soldier Roster Classes

### Recruit

- HP: `8`
- Damage: `1-3`
- Armor: `0`
- Range: `28`
- Speed: `152`
- Stamina: `72`
- Active: `Sprint`
- Passive: none

### Shield

- HP: `15`
- Damage: `1-2`
- Armor: `2`
- Range: `26`
- Speed: `122`
- Stamina: `86`
- Active: none
- Passive:
- `Missile Guard`

### Elite Shield

- HP: `20`
- Damage: `2-4`
- Armor: `3`
- Range: `28`
- Speed: `132`
- Stamina: `104`
- AttackCycleScale: `0.9`
- Active:
- `Shield Rush`
- Passive:
- `Missile Guard`
- Visual:
- armored torso,
- larger shield,
- no helmet yet

### Ironhelm Guard

- HP: `24`
- Damage: `3-5`
- Armor: `4`
- Range: `30`
- Speed: `138`
- Stamina: `112`
- AttackCycleScale: `0.86`
- Active:
- `Shield Rush`
- Passive:
- `Missile Guard`
- `BlockAnyDamageChance = 0.18`
- Visual:
- armored torso,
- helmet,
- upgraded shield-line silhouette

### Bulwark Guard

- HP: `28`
- Damage: `4-7`
- Armor: `5`
- Range: `32`
- Speed: `144`
- Stamina: `126`
- AttackCycleScale: `0.8`
- Active:
- `Shield Rush`
- enhanced endpoint shockwave visual
- Passive:
- `Missile Guard`
- `BlockAnyDamageChance = 0.28`
- Ranged damage scale: `0.45`
- Visual:
- armored torso,
- helmet,
- ornate larger shield

### Pike

- HP: `9`
- Damage: `2-4`
- Armor: `0`
- Range: `44`
- Speed: `146`
- Stamina: `76`
- Active: `Sprint`
- Passive: none

### Blade

- HP: `8`
- Damage: `2-5`
- Armor: `0`
- Range: `28`
- Speed: `164`
- Stamina: `78`
- Active: `Sprint`
- Passive: none

### Archer

- HP: `7`
- Damage: `1-4`
- Armor: `0`
- Range: `176`
- Speed: `148`
- Stamina: `0`
- Active: none
- Passive: none

### Cavalry

- HP: `11`
- Damage: `3-6`
- Armor: `0`
- Range: `34`
- Speed: `176`
- Stamina: `104`
- AttackCycleScale: `0.92`
- Active: `Sprint`
- Passive: none
- Special:
- heavier melee profile than baseline line troops,
- heavier attack setup than standard melee soldiers

## Shield Rush Rules

- Current users:
- `Elite Shield`
- `Ironhelm Guard`
- `Bulwark Guard`
- Cooldown: `5s`
- Stamina cost: `24`
- Base trigger window:
- target farther than `AttackRange + 18`
- target closer than or equal to `150`
- attacker must be able to act and have enough stamina
- Base dash:
- duration `0.28`
- move speed `520`
- Path effect:
- knocks units aside,
- affects allied and enemy units physically,
- allied units are pushed but do not take damage
- Endpoint effect:
- stronger knockback and control than along-path contacts

### Bulwark Guard Shield Rush upgrade

- Wider path and endpoint influence radius
- Higher damage
- Higher stagger
- Higher knockback force and duration
- Adds a route-aligned shockwave visual
- The shockwave is visual only and does not add separate collision logic

## Promotion Path

- `Recruit -> Shield`
- `Shield -> Elite Shield`
- `Elite Shield -> Ironhelm Guard`
- `Ironhelm Guard -> Bulwark Guard`

## Promotion Costs And XP

- `Shield`: `XP 2`, `18` money
- `Elite Shield`: `XP 6`, `42` money
- `Ironhelm Guard`: `XP 10`, `70` money
- `Bulwark Guard`: `XP 15`, `110` money
- `Pike`: `XP 2`, `18` money
- `Blade`: `XP 2`, `18` money
- `Archer`: `XP 2`, `22` money
- `Cavalry`: `XP 3`, `40` money

## Soldier Experience Rules

- Surviving soldiers gain `+1 XP` when a room is cleared.
- Surviving soldiers gain another `+1 XP` on successful extraction.
- Dead soldiers do not continue to gain XP because they are removed from the active run roster.

## Enemy Units

### Generic Threat Guard

- Spawn count: `clamp(1 + threat / 2, 2, 6)`
- HP: `6 + threat`
- Damage: `1 + threat / 3` to `3 + threat / 2`
- Range:
- ranged variant: `156`
- melee variant: `28`
- Distribution:
- every third spawn is ranged

### AI Squad Leader

- HP: `12 + squad.Strength / 2`
- Damage: `3-6`
- Range: `172`
- Flags:
- elite,
- AI squad,
- ranged

### AI Squad Trooper

- Spawn count: `clamp(squad.Strength / 2, 3, 7)`
- HP: `7`
- Damage: `1-3`
- Range:
- ranged variant: `152`
- melee variant: `28`
- Distribution:
- roughly one in three is ranged

## Debug-Only Combat Units

- The code still contains debug-only ally and enemy spawns used for combat preview / effects testing.
- These are not part of the normal progression balance and should not be treated as live economy-facing roster units.
