# Unit Mechanics Reference

## Scope

- This document tracks the current live unit stats and combat rules implemented in code.
- It should stay aligned with the actual state of the playable demo.
- This is the English source document for maintenance.
- A Chinese mirror document should be refreshed after each meaningful update.

## Global Combat Rules

- `HP`: current and maximum health.
- `Damage`: inclusive random attack roll.
- `Armor`: flat post-scaling damage reduction.
- `AttackRange`: melee or ranged engagement distance.
- `Speed`: base move speed before skill overrides.
- `AttackCycleScale`: lower means faster attack cadence.
- `Stamina`: consumed by sprinting and active skills.
- `ProjectileDamageScale`: multiplier applied to ranged damage before armor.
- `BlockAnyDamageChance`: chance to negate a hit completely before damage.

## Shared Soldier Skill Rules

- Most non-ranged, non-specialist line soldiers use `Sprint`.
- Archers do not sprint.
- Shield-line soldiers do not sprint.
- Pike-line soldiers do not sprint.
- Shield-line passive baseline:
- `Missile Guard`: `50%` ranged damage reduction.
- Pike-line passive baseline:
- `Brace`: outer-range hits gain stronger knockback and stagger.

## Controlled Hero

- HP: `24` base hideout template, but live run state persists.
- Damage: `2-5`
- Range: `164`
- Speed: `165`
- Stamina: `88`
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
- no helmet

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
- upgraded shield

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
- enhanced route-aligned shockwave visual
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
- Range: `52`
- Speed: `146`
- Stamina: `76`
- AttackCycleScale: `0.98`
- Active: none
- Passive:
- `Brace`

### Elite Pike

- HP: `12`
- Damage: `3-5`
- Armor: `1`
- Range: `56`
- Speed: `152`
- Stamina: `88`
- AttackCycleScale: `0.92`
- Active:
- `Pike Thrust`
- Passive:
- `Brace`
- Visual:
- armored torso,
- standard upgraded pike,
- no helmet

### Ironhelm Pikeward

- HP: `15`
- Damage: `4-6`
- Armor: `2`
- Range: `58`
- Speed: `156`
- Stamina: `98`
- AttackCycleScale: `0.88`
- Active:
- `Pike Thrust`
- Passive:
- strengthened `Brace`
- Visual:
- armored torso,
- helmet,
- reinforced pike

### Vanguard Pikeward

- HP: `18`
- Damage: `5-8`
- Armor: `3`
- Range: `62`
- Speed: `160`
- Stamina: `110`
- AttackCycleScale: `0.82`
- Active:
- `Pike Thrust`
- upgraded into a wider piercing line strike
- stronger route-aligned wave effect
- Passive:
- strengthened `Brace`
- Visual:
- armored torso,
- helmet,
- ornate spearhead

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
- The shockwave is visual only

## Pike Thrust Rules

- Current users:
- `Elite Pike`
- `Ironhelm Pikeward`
- `Vanguard Pikeward`
- Cooldown: `5s`
- Stamina cost: `18`
- Base trigger window:
- target outside `72%` of attack range
- target within `AttackRange + 26`
- attacker must be able to act and have enough stamina
- Base thrust:
- no dash movement skill
- uses a dedicated windup and recovery
- attacks along a forward narrow line
- Elite and Ironhelm tiers:
- hit the nearest valid enemy in the thrust line
- Vanguard tier:
- wider line,
- longer line,
- can pierce multiple enemies,
- stronger route-aligned wave effect

## Promotion Path

- `Recruit -> Shield`
- `Shield -> Elite Shield`
- `Elite Shield -> Ironhelm Guard`
- `Ironhelm Guard -> Bulwark Guard`
- `Recruit -> Pike`
- `Pike -> Elite Pike`
- `Elite Pike -> Ironhelm Pikeward`
- `Ironhelm Pikeward -> Vanguard Pikeward`
- `Recruit -> Blade`
- `Recruit -> Archer`

## Promotion Costs And XP

- `Shield`: `XP 2`, `18` money
- `Elite Shield`: `XP 6`, `42` money
- `Ironhelm Guard`: `XP 10`, `70` money
- `Bulwark Guard`: `XP 15`, `110` money
- `Pike`: `XP 2`, `18` money
- `Elite Pike`: `XP 6`, `42` money
- `Ironhelm Pikeward`: `XP 10`, `70` money
- `Vanguard Pikeward`: `XP 15`, `110` money
- `Blade`: `XP 2`, `18` money
- `Archer`: `XP 2`, `22` money

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
