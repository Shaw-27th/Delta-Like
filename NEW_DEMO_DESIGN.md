# New Demo Design

## Document Rule

- This document is for implementation tracking and assistant context.
- From this revision onward, this file should stay English-only.
- This is the English source document maintained for assistant-side tracking.
- A Chinese mirror document should be refreshed after each meaningful update for player-facing review.
- The document should describe the current playable state, not outdated plans that no longer match the code.

## Project Summary

- The demo is a run-based tactical raid prototype.
- The player prepares in the hideout, enters a fixed map, clears or searches rooms in real time, and tries to extract with loot.
- The current main scene entry is `project.godot -> NodeRaidDemo.tscn -> RaidMapDemo.cs`.

## Current Core Loop

1. Prepare in the hideout.
2. Buy, sell, and recruit.
3. Choose a fixed map template and difficulty.
4. Enter the run.
5. Move through connected rooms from the room view, with the strategic map as an overlay.
6. Fight, search, collect loot, manage carry space, and continue or retreat.
7. Extract successfully or fail the run.
8. Return to the hideout state.

## Current Implemented State

### Maps and traversal

- The game currently uses fixed map templates rather than procedural topology.
- Rooms are connected by explicit node links.
- Strategic map overlay scales with the current viewport-derived map area.
- Floor-map district labels stay hidden on the backdrop layer.
- Node labels remain attached to node markers only.
- Room transitions use continuous geometric door placement.
- Exits and entries are paired by actual link direction instead of fixed four-direction snapping.
- Entering the next room uses the opposite of the last exit direction, so traversal is directionally consistent.

### Room gameplay

- The room view is now the primary gameplay layer.
- Combat, looting, container interaction, and room-to-room movement all happen inside the same room state.
- The strategic map is a planning overlay, not the main interaction surface.
- Hero exploration speed doubles when no hostile units are alive in the current room.

### Combat and feedback

- The room combat layer supports heroes, allied soldiers, monsters, and AI squads.
- Melee and ranged units are visually distinct.
- The hit / stagger / knockback loop is already active.
- Melee arc effects were reworked into curved slash feedback.
- Procedural runtime sound effects exist for:
- light melee attacks,
- heavy melee attacks,
- light ranged attacks,
- heavy ranged attacks,
- light hit reactions,
- heavy hit reactions,
- deaths.
- Degenerate melee impact polygons are now guarded to avoid Godot triangulation errors.

### AI presentation

- AI squads use orange / yellow warm colors.
- Generic monsters and threat units use gray-white colors.
- Death X markers inherit the dead unit's color family.

### Containers and search

- Containers no longer live in the right sidebar as the primary interaction path.
- Containers are placed directly in the room scene and can auto-open at close range during non-combat.
- Search is currently intended for non-combat only.
- The existing reveal/search/take loop is still based on a container grid.
- Search panels are now translucent floating panels instead of modal darkened screens.
- The player can keep moving while a container is open.
- Leaving the active container range interrupts the search and closes the panel.
- Active searches cannot be closed manually mid-progress.
- Containers support a `Take All` action for already available items.

### Death drops

- Normal unit deaths no longer always produce corpse containers.
- Normal non-hero deaths spawn resource orbs.
- Resource orbs currently grant money only.
- The controlled hero auto-attracts and collects nearby resource orbs.
- Collected run money is shown separately in the side panel.
- Normal enemy units have a low-probability special container drop.
- Player-side normal soldiers do not create standard corpse containers.
- Elite and boss-like deaths still create separate elite containers.
- Room-side temporary discard containers can be created from backpack item drops.

## Carry Capacity and Team Backpack

### Current live rules

- The run backpack is no longer an unlimited list.
- Loot now enters a real bounded team backpack.
- Item footprint is preserved when moving from container to backpack.
- If an item is `2 x 3` in the container, it stays `2 x 3` in carried storage logic.
- Carry usage and carry limit are displayed in the run UI.

### Current capacity sources

- There is currently no separate base squad capacity block.
- Hero contribution: `2 x 2`
- Standard soldier contribution: `1 x 1`
- The system is currently derived from surviving player-side room units.

### Current composite layout rules

- Capacity is represented as stitched capacity blocks, not one single flat bag.
- The current panel width cap is `12` cells.
- Capacity blocks are visually grouped with dashed boundaries.
- These source-group visuals are explanatory only and do not create hard placement barriers.
- Disabled cells are not rendered.
- Backpack cells were enlarged to improve readability during testing.

### Current block-growth rule

- Capacity blocks are assembled in staged `4 x 4` regions.
- The system first tries to grow the current region toward a square.
- Within that, it now resolves ties in top-to-bottom, then left-to-right order.
- Only after the current `4 x 4` region is effectively filled does growth move to the next region to the right.
- This rule exists to preserve larger contiguous placements for items such as `2 x 3`.

### Current backpack interactions

- Drag pickup is implemented.
- Rotation while holding an item is implemented.
- Held-item presentation is offset so the cursor does not sit directly on top of the item body.
- Hover naming exists.
- Larger items can render their names directly when readable.
- Auto-organize exists as a UI button.
- Auto-organize includes both backpack contents and overflow contents.
- Auto-organize can rotate items while repacking.

### Overflow behavior

- If new loot does not fit, the game tries repacking first.
- If carry capacity shrinks because allied units die, the game also repacks first.
- Items that still cannot fit are moved into the right-side overflow area labeled `Pending Sort`.
- Any item in overflow counts as overloaded.
- Overload blocks room transition.
- Overload blocks extraction.
- Overload does not block movement inside the current room.

### Current overload recovery tools

- Overflow items can be dropped into the current room.
- Held backpack items can also be dropped into the current room.
- These drops create or reuse a temporary room container labeled `Temporary Discard`.
- This is the current practical way to resolve overload during a run.

## Hideout and Economy

- Hideout buy/sell flow is active.
- Recruitment flow is active.
- Recruitment currently hires `Recruit` soldiers by default.
- A default roster seed exists.
- New-run starting money is currently boosted for testing.
- Current test start money: `3000`

## Lead Hero Progression Snapshot

### Current live first pass

- The lead hero now uses a four-attribute structure aligned with the current Mount-and-Blade-style target:
- `Strength`,
- `Agility`,
- `Intelligence`,
- `Charm`.
- The lead hero now persists:
- level,
- experience,
- unspent stat points,
- the four primary attributes above.
- Leveling up currently grants `2` unspent stat points per level.
- Attribute points can now be assigned in the hideout before deployment.

### Current first-pass attribute roles

- `Strength` currently determines:
- max HP,
- base damage,
- lead-hero base backpack capacity,
- critical-hit multiplier.
- `Agility` currently determines:
- move speed,
- attack speed,
- critical-hit chance,
- search speed.
- `Intelligence` is intentionally reserved for future systems and currently has no direct stat effect.
- `Charm` currently determines:
- secondary-hero slot count,
- soldier count limit,
- soldier stat bonuses,
- recruitment discount.
- Room clear now grants lead-hero experience.
- Successful extraction now grants lead-hero experience.

### Skill-tree direction

- The lead hero skill tree should be organized under the four attributes rather than as one flat list.
- Each attribute should later own its own branch of hero skills.
- `Intelligence` is expected to matter mainly through future non-combat and support skills rather than immediate raw stat scaling.

### First implementation boundary

- The first implementation should focus on the persistent stat scaffold and direct attribute effects only.
- Secondary heroes should remain future work even though `Charm` already reserves design space for them.
- Equipment-driven builds remain part of the long-term hero plan, but not the first implementation pass.

## Soldier Progression Snapshot

### Current live first wave

- Soldiers now persist with both class and experience.
- The first live class set is:
- `Recruit`,
- `Shield`,
- `Elite Shield`,
- `Ironhelm Guard`,
- `Bulwark Guard`,
- `Pike`,
- `Elite Pike`,
- `Ironhelm Pikeward`,
- `Vanguard Pikeward`,
- `Blade`,
- `Elite Blade`,
- `Ironhelm Bladeward`,
- `Vanguard Bladeward`,
- `Archer`,
- `Elite Archer`,
- `Ironhelm Huntsman`,
- `Sky-Piercer Archer`,
- The hideout now includes a simple soldier roster panel.
- Recruits can be promoted out of run from that roster panel.
- Promotion currently consumes money and requires stored experience.
- Surviving soldiers gain experience when clearing rooms and when extracting successfully.
- In room combat, soldier class now affects core combat parameters such as:
- health,
- speed,
- damage,
- range,
- stamina,
- ranged or melee behavior.
- A first minimal soldier skill framework now exists with active and passive slots.
- Sprint is treated as a default active skill for most non-ranged soldiers.
- Base shield soldiers currently do not sprint and only keep the ranged-damage mitigation passive.
- The shield line now has a four-step progression:
- `Shield`,
- `Elite Shield`,
- `Ironhelm Guard`,
- `Bulwark Guard`.
- `Elite Shield` is the first shield upgrade and now owns the base `Shield Rush` active skill.
- `Ironhelm Guard` adds a helmet and an upgraded passive with a chance to fully block any incoming damage.
- `Bulwark Guard` keeps the helmeted armor silhouette, upgrades the shield into a more ornate large version, and upgrades `Shield Rush` into a wider and more powerful impact with a visible route-aligned shockwave.
- The pike line now also has a four-step progression:
- `Pike`,
- `Elite Pike`,
- `Ironhelm Pikeward`,
- `Vanguard Pikeward`.
- Base `Pike` now keeps only the `Brace` passive and does not sprint.
- `Elite Pike` is the first pike upgrade and now owns the base `Pike Thrust` active skill.
- `Ironhelm Pikeward` adds a helmet and strengthens the `Brace` passive into a higher-impact first-contact control tool.
- `Vanguard Pikeward` keeps the helmeted armored silhouette, upgrades the spear into a more ornate weapon, and upgrades `Pike Thrust` into a wider route-piercing strike with a stronger route-aligned wave effect.
- The blade line now also has a four-step progression:
- `Blade`,
- `Elite Blade`,
- `Ironhelm Bladeward`,
- `Vanguard Bladeward`.
- Base `Blade` now keeps only the `Executioner` passive and does not sprint.
- `Elite Blade` is the first blade upgrade and now owns the base `Blade Rush` active skill.
- `Ironhelm Bladeward` adds a helmet and strengthens the `Executioner` passive into a better wounded-target finisher with cooldown refund.
- `Vanguard Bladeward` keeps the armored silhouette, upgrades the weapon into a more ornate blade, and upgrades `Blade Rush` into a stronger cleaving assault with a short shockwave flourish.
- `Shield Rush` spends stamina, has a `5s` cooldown, knocks units aside along the path, and applies a stronger endpoint impact.
- `Pike Thrust` also spends stamina, uses a `5s` cooldown, and now attacks along a narrow forward line instead of acting as a generic melee swing.
- `Blade Rush` spends stamina, uses a `4.5s` cooldown, lunges into melee, and the highest blade tier can cleave nearby enemies on impact.
- The archer line now also has a four-step progression:
- `Archer`,
- `Elite Archer`,
- `Ironhelm Huntsman`,
- `Sky-Piercer Archer`.
- Base `Archer` now keeps only the `Deadeye` passive.
- `Elite Archer` is the first archer upgrade and now owns the base `Split Arrow` active skill.
- `Ironhelm Huntsman` adds a helmet and strengthens the `Deadeye` passive into a stronger critical-hit profile.
- `Sky-Piercer Archer` keeps the armored silhouette, upgrades the bow into a more ornate version, and upgrades `Split Arrow` into a denser multishot volley.
- `Split Arrow` spends stamina, uses a `5s` cooldown, and fires at multiple nearby targets from a single attack window.
- Shield soldiers also have a live passive that reduces ranged damage by `50%`.
- Armor is now a real combat stat in room combat damage resolution.
- Higher shield, pike, blade, and archer tiers now also change the visual silhouette with armor, helmet, and upgraded weapon treatment.

## Inventory Progress Snapshot

### Completed milestone: stash foundation

- The stash now has a real fixed grid foundation instead of a simple string list.
- Current live stash size is `12 x 10`.
- Buying from the shop now tries to place the purchased item into the stash grid.
- Hideout stash UI now displays the stash as a real item grid.
- Stash items can be selected and sold from the grid view.
- Stash items can also be manually rearranged inside the stash grid.

### In-progress milestone: shared transfer logic

- Shop and stash now already share:
- item size language,
- item rarity color presentation,
- grid-based item representation.
- Hideout stash and the outside-run backpack now share a bidirectional drag-style transfer flow.
- Hideout and post-run settlement now support press-move-release dragging instead of only two-click pickup/drop.
- Drag preview anchoring now follows the original grab point, and target placement uses the same preview origin plus valid/invalid placement highlights.
- The hideout layout was reworked so the stash, shop, and outside-run backpack fit inside separate panels.
- The old explicit `Bring Into Run` / `Return To Stash` buttons are no longer required for the main flow.
- Hideout preparation now expects direct grid-to-grid movement between stash and the outside-run backpack area.
- Dragged hideout items are rendered as a top-layer overlay so they do not disappear under later UI panels.
- Container-to-backpack transfer is active.
- Backpack-to-container return is now also active for the currently open container popup.
- Backpack-to-container return now tries:
- the clicked cell first,
- then a nearest available automatic placement pass,
- then a rotated placement pass if needed.
- The open container popup also provides a direct on-screen hint while a backpack item is being held.
- This means room-side storage is no longer strictly one-way.
- Successful extraction no longer auto-dumps run loot into the stash.
- Instead, extracted items are carried back into the persistent outside-run backpack first.
- A post-result settlement transfer screen is now the intended path for stash transfer after extraction.
- The settlement transfer screen now includes outside-run backpack repacking and a quick `move all to stash` action.

### Full target for the current inventory push

- The near-term target is not just to have separate working panels.
- The target is one coherent inventory language across:
- room containers,
- team backpack,
- stash,
- shop acquisition path.
- The intended result is:
- the same item footprint rules,
- the same item presentation logic,
- the same transfer expectations,
- and fewer one-off special cases per panel.

## Current Known Gaps

### Stash system

- The stash now has a real grid foundation, but not full parity with the team backpack yet.
- Post-run settlement transfer now exists in principle, but the surrounding management UX is still basic.
- Storage-box tabs and typed storage boxes are not implemented.

### Backpack management depth

- Backpack-to-container transfer is now functional for the currently open container popup, but still not polished as a full management UI.
- Overflow and overload recovery are usable but still rough.
- Fine manual sorting workflows are still limited compared with the intended final inventory management depth.
- Direct manual rearrangement of capacity-source blocks is not implemented.

### Shared transfer UX

- Transfer behavior is still split between:
- direct take buttons,
- take-all,
- drag-hold backpack placement,
- stash selection actions.
- The code is moving toward a unified inventory language, but the interaction model is not fully unified yet.
- Full parity across room containers, run backpack, outside-run backpack, and stash is still incomplete.

### Logistics and advanced capacity sources

- Logistics plan selection before deployment is not implemented.
- Capacity bonuses from traits, equipment, and skills are not implemented.
- Special logistics units such as transport mules are not implemented.

### Hero progression depth

- The lead hero attribute framework described above is a confirmed design target, but is not fully implemented yet.
- Hero skill progression is not implemented yet.
- Equipment-driven hero builds are not implemented yet.
- Secondary heroes are still not implemented.
- Lead hero and future secondary heroes do not yet share one unified hero-grade data framework in code and data.

### Presentation cleanup

- Some old helper code from earlier UI/container approaches may still remain in `RaidMapDemo.cs`.
- The codebase still needs cleanup once the current systems stabilize.

## Current Strategic Priorities

### Current first priority: progression systems

- Inventory work is no longer the immediate focus.
- The next major push is progression, split into three unit layers:
- the controlled lead hero,
- secondary heroes,
- soldiers.

### Lead hero target

- The controlled lead hero should remain the primary direct-play identity.
- The lead hero should support:
- level progression,
- primary stats,
- skill progression,
- equipment-driven builds.
- This layer should be the strongest source of direct combat expression and long-term player identity.

### Secondary hero target

- Secondary heroes should be hero-grade units rather than upgraded soldiers.
- They should follow the lead hero in combat but still use the same core hero-style progression model:
- levels,
- stats,
- skills,
- equipment.
- Secondary heroes should be limited by a party-slot cap rather than treated as general troop count.
- The current intended cap is a small fixed number, such as `3`.
- Their role is to provide party composition depth, specialist functions, and long-term roster investment without replacing the controlled lead hero.

### Soldier progression target

- Soldiers should become more than disposable headcount.
- The soldier layer should support:
- troop classes,
- per-soldier experience gain,
- out-of-run class advancement.
- Soldiers should still be more replaceable than heroes, but not fully anonymous.
- The system should create meaningful differences between low-tier recruits and upgraded veterans.
- Soldiers are expected to fill the line-unit layer of the squad rather than the hero-specialist layer.

### First soldier-class implementation target

- The first live soldier roster should start from `Recruit`.
- Recruits should be promotable out of run into a small set of clear battlefield roles:
- `Shield`,
- `Pike`,
- `Blade`,
- `Archer`,
- The first implementation should prioritize tactical clarity over roster breadth.
- Class identity should come from battlefield behavior and combat parameters, not only raw stat inflation.

### Intended first-wave class roles

- `Recruit`: cheap baseline unit and default hiring result.
- `Shield`: slower, tougher, better at holding the line and absorbing pressure.
- `Pike`: longer melee reach, stronger anti-charge and doorway control role.
- `Blade`: faster close-range attacker with higher damage but lower staying power.
- `Archer`: rear-line ranged support that requires protection.

### Intended first soldier progression loop

- Soldiers gain experience by surviving combat and returning from runs.
- Experience should persist per soldier rather than only by class bucket.
- Promotion should happen out of run in the hideout rather than instantly in combat.
- Hiring should initially create `Recruit` units only.
- Promotion is what creates differentiated line troops.

### Progression structure rule

- Lead hero and secondary heroes should ideally share one common hero-grade progression framework in code and data.
- The lead hero is differentiated by direct player control.
- Secondary heroes are differentiated by hero-slot limits and battlefield role, not by becoming a separate lower-grade system.

### Other near-term pillars after progression

1. Room events and search-event content density.
2. Stronger map-presence behavior for AI squads.
3. Extraction pressure and escalation systems.
4. Further cleanup after the current gameplay direction stabilizes.

## Notes for Future Updates

- Keep this file English-only.
- Prefer replacing outdated design text instead of stacking contradictory historical notes.
- Document only behavior that exists in code or has been clearly re-approved as the current target.
