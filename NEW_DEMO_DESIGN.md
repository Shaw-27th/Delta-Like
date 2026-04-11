# New Demo Design

## Latest Progress

### 2026-04 Recent Changes

- Room-to-room traversal now uses continuous geometric door placement.
- Each linked room projects its direction onto the current room boundary, so room doors align with node-map direction instead of only snapping to fixed 4-way slots.
- Entering a room now uses the opposite of the previous exit direction, so cross-room entry and exit are consistent.

- The overlay node map and backdrop map now scale against the current viewport-derived map rect.
- Backdrop district labels stay hidden on the floor map. Node labels remain on node markers only.

- AI squads and generic monsters are now visually separated.
- AI squads use warm orange/yellow accents.
- Monsters and threat units use gray-white tones.
- Death X markers inherit the corresponding unit color family.

- Hero movement speed inside rooms now doubles while there are no hostile units in the room.
- This only affects exploration movement, not active combat behavior.

- Room containers were moved out of the right sidebar and into the room scene itself.
- Static room containers now spawn at preset scene anchors with a simple in-room model.
- Containers can auto-open at close range during non-combat.
- If auto-search is enabled, opening a nearby container will also auto-start the existing search flow.

- Unit deaths now split into two drop paths.
- Normal soldier / monster deaths always spawn an absorbable resource orb.
- The current resource-orb implementation only grants money, and the looted amount is tracked separately in the side panel.
- Enemy normal units can also roll a low-probability special container drop.
- Player-side normal units do not generate normal containers.
- Elite / boss deaths still always create a separate elite container.

### Current Container/Search Direction

- Sidebar container listing is no longer the main interaction path.
- Search is currently intended for non-combat only.
- The existing grid-based reveal/search/take flow is still in use after a container is opened.
- Container scene presentation is currently functional placeholder art, not final visual design.

### Current Known Follow-up

- Static room container placement still uses preset anchor points, not per-room handcrafted dressing.
- Some old container/sidebar helper code may still remain in the file even though the main interaction path has moved to in-room containers.
- Death-generated container visuals are usable but not yet final and can still be polished.

### Current Death-Drop Direction

- Normal deaths no longer rely on guaranteed corpse containers.
- Normal soldier / monster deaths now primarily resolve through resource orbs.
- Resource orbs are collected by bringing the controlled hero close enough for auto-attraction and pickup.
- Normal enemy monsters and enemy soldiers also have a low-probability special container drop.
- Elite / boss deaths continue to use their own dedicated container flow.

### Proposed Backpack / Stash Direction

- Containers, team backpack, and stash should all use the same width x height grid logic.
- Item size must stay consistent across all inventory contexts.
- If an item is `2 x 3` in a container, it is also `2 x 3` in the team backpack and stash.
- Search loot should enter the team backpack first.
- Successful extraction should move carried items from the team backpack into stash storage.
- Supplies and carried utility items should also consume team backpack space.

- Team backpack should be modeled as squad carry capacity, not one hero wearing one giant bag.
- Carry capacity should come from multiple sources at once.

- Carry capacity sources:
- There is currently no separate base squad capacity block.
- Hero capacity is the main stable source.
- Current chosen hero contribution target: `4 x 4`.
- Hero skills, traits, and equipment can add more carry capacity.
- Soldiers also add carry capacity, but usually much less per unit.
- Standard soldiers should contribute `1 x 2`.
- Advanced or special soldiers can add more than standard troops.
- Special non-combat logistics units such as pack mules / transport carriers are allowed as future unit types.
- Pre-run logistics loadout can add fixed or scaling carry bonuses for the whole squad.

- Current recommended capacity structure:
- Total carry space should be assembled from multiple rectangular capacity blocks.
- Example contributing blocks:
- Hero block: `4 x 4`.
- Soldier block: `1 x 2`.
- Future logistics / equipment blocks: variable.
- These blocks should be auto-packed by the game into one composite team-backpack panel.
- The final composite panel can be irregular in feel internally, but its UI row width should cap at `12` cells.
- Capacity blocks should be visually understandable in the UI even after auto-packing.
- The player should be able to see that the total space is stitched together from several source blocks, not one monolithic bag.

- Current recommended implementation direction:
- Do not start with a literal hero-only Tarkov backpack.
- Start with a squad carry-capacity panel that still uses grid space and item footprint.
- The fiction should be expedition pack load, salvage drag bags, haul frames, or squad logistics support.
- Auto-packing is acceptable for the first implementation.
- Later, manual backpack-capacity arrangement can be considered if it adds real gameplay value.

### Team Backpack UI / Interaction Rules

- Team backpack should use the same item-grid language as containers and stash.
- Team backpack must support drag movement.
- Team backpack must support rotation.
- Rotation should use the same dimensions swap rule everywhere: `W x H` becomes `H x W`.
- Item names must be visible either:
- directly on the item if readable,
- or through mouse hover tooltip if direct labels become too noisy.

- The team-backpack panel should not look like one flat generic rectangle.
- It should visually communicate that the total capacity is assembled from smaller source capacities.
- Recommended presentation:
- The global backing grid still behaves as one shared placement surface.
- Capacity-source boundaries are shown as softer grouping cues such as dashed separators, subtle shading, or grouped background panels.
- A `4 x 4` hero block should read visually as one chunk.
- Soldier-provided `1 x 2` blocks should read as add-on chunks attached to the main load.
- Items are allowed to span across different source blocks.
- Source-block visuals are explanatory only and must not create hard placement barriers.

- Team-backpack UI must avoid side-panel overlap with existing run UI.
- The carry panel and the event log / map controls must have distinct vertical regions.
- The current overlap issue between `世界动态` and the map-toggle row should be treated as a layout bug to fix during the inventory UI pass.

- Current stash target:
- Main stash tab is always present.
- Main stash width is fixed at `12`.
- Level 1 stash starts with `10` rows.
- Stash upgrades can grant two separate benefits:
- Increase base stash rows.
- Increase storage-box slots.

- Storage boxes are special stash items.
- A storage box occupies one stash-box slot and opens as its own stash tab.
- Main stash stays as tab 1.
- Additional storage-box tabs appear after the main stash tab.
- Storage-box tab selectors should live on the left side of the stash UI.

- Each storage box has:
- A fixed capacity.
- A fixed grid shape derived from that capacity.
- An optional item-type restriction.

- Example storage-box behavior:
- A `72`-slot storage box could provide a `12 x 6` sub-tab.
- A typed storage box such as materials / collectibles / equipment can only hold that category.
- An untyped storage box can hold all item categories.

### Overload Rules

- Carried items should always occupy actual team backpack space.
- The panel should clearly display current usage and current limit.
- Example display: `携行占用 34 / 28`.
- If current usage exceeds current limit, the squad enters an `overloaded` state.

- Overloaded state should not instantly destroy or auto-drop loot.
- Overloaded state should not prevent movement inside the current room.
- Overloaded state should prevent room-to-room movement.
- Overloaded state should prevent extraction at extraction points.
- Attempting to move through a door or extract while overloaded should show a clear overload warning.

- If hero or soldier deaths reduce carry capacity, the run should transition into overload instead of deleting items.
- The player should then resolve overload manually.
- Required player-side solutions include:
- Dropping items on the ground.
- Leaving items in the current room container or a temporary floor container.
- Reorganizing carried space.

- This keeps death-linked carry pressure without creating unfair hard-fail situations.

### Team Backpack Inspiration Options

- Option A: Wagon / expedition pack.
- The backpack is not the hero's personal bag.
- It represents the squad's hauled field storage: mule packs, drag bags, folded crates, salvage rolls.
- Better versions increase run carry capacity in a believable squad-level way.

- Option B: Deployment logistics slot.
- Before a run, the player chooses one logistics rig for the squad.
- Example choices:
- Light pack: smaller storage, more speed.
- Standard pack: balanced storage.
- Heavy haul kit: larger storage, but worse mobility / extraction pressure.
- This keeps the Tarkov-like “capacity must be bought” concept while fitting a squad better.

- Option C: Quartermaster support level.
- Capacity is not a literal backpack item.
- The hideout buys expedition support upgrades: handlers, pack frames, hauling nets, transport crates.
- Mechanically it still works like backpack tiers, but fictionally it belongs to the whole team.

- Current recommended direction:
- Start with Option A or B as the presentation layer.
- Mechanically use the multi-source capacity model above.
- They preserve the strong loot-space economy without making the whole system feel like one hero carrying an unrealistic amount alone.

### Phased Implementation Plan

- Phase 1: Carry-capacity foundation.
- Introduce a real team-backpack grid instead of the current unlimited run backpack list.
- Route container loot into the team backpack instead of directly into an unbounded list.
- Add visible carry usage and carry limit to the run UI.
- Compute carry limit from a first-pass squad formula.
- First-pass formula should now be block-based, not just one scalar number:
- No separate base capacity block.
- Hero block `4 x 4`.
- Soldier blocks `1 x 2`.
- Optional flat logistics block can stay deferred or be added later.
- Detect overload when carried usage exceeds current limit.
- Block room-to-room movement and extraction while overloaded.
- Allow staying in the current room while overloaded.
- Add a basic way to drop items back into the current room when overloaded.

- Phase 2: Stash foundation.
- Replace the current simple stash list with a real stash grid.
- Move extracted team-backpack contents into the stash grid on successful extraction.
- Set main stash width to `12`.
- Set level 1 stash height to `10` rows.
- Add stash-space growth hooks so later upgrades can increase rows cleanly.

- Phase 3: Shared inventory UI and transfer flow.
- Use one consistent grid UI language for container, team backpack, and stash.
- Add drag / move / swap behavior between grids.
- Add room-container <-> team-backpack transfer flow.
- Add stash <-> hideout transfer flow.
- Surface item footprint clearly so run looting decisions become space-driven.

- Phase 4: Overload recovery and logistics depth.
- Improve overload presentation and warnings.
- Add cleaner floor-drop / temporary room container behavior.
- Add pre-run logistics-plan selection.
- Add flat bonuses and tradeoffs such as movement penalty or health penalty.
- Tune carry formulas so hero contribution is stable and soldier contribution is meaningful but not dominant.

- Phase 5: Storage-box expansion.
- Add stash box slots.
- Add extra stash tabs from inserted storage boxes.
- Support typed storage boxes and unrestricted storage boxes.
- Add stash upgrades for more rows and more storage-box slots.

- Phase 6: Advanced capacity sources.
- Add equipment, traits, and skills that affect carry capacity.
- Add special logistics units such as transport mules or non-combat carriers.
- Decide whether these units can die and push the squad into overload during runs.
- Tune death-linked capacity loss after the overload system is already proven stable.

### Phase 1 Must-Do

- Team backpack must stop being an unlimited list and become a real bounded grid.
- Search/container loot must go into that bounded team backpack.
- Item footprint must remain identical between container, team backpack, and stash.
- The run UI must show `current usage / current limit`.
- The game must calculate a first-pass squad carry layout from:
- no separate base block,
- hero `4 x 4`,
- soldier `1 x 2`.
- The run must enter overload when usage exceeds limit.
- Overload must block room transition.
- Overload must block extraction.
- The player must still be allowed to move inside the current room while overloaded.
- The player must have at least one basic way to remove carried items from the team backpack in-room.
- Team backpack must support drag movement.
- Team backpack must support rotation.
- Team-backpack items must show readable names directly or on hover.
- Team-backpack UI must visually explain stitched capacity blocks without making them placement barriers.

### Phase 1 Explicitly Not Required

- Storage-box tabs.
- Typed storage restrictions.
- Stash upgrades.
- Equipment / skill carry modifiers.
- Special transport units.
- Logistics plans with negative side effects.
- Fancy drag-and-drop polish.
- Final art or final inventory UI layout.

## 文档目的

- 这份文档以当前代码实现为准。
- 它用于替代旧的乱码/过时版本，记录当前 Demo 的真实结构、已完成内容和后续优先级。
- 当前主入口为 `project.godot -> NodeRaidDemo.tscn -> RaidMapDemo.cs`。

## 一句话概述

这是一个“局外整备 -> 进入固定地图 -> 在房间实时玩法中战斗/搜索/过门 -> 撤离结算 -> 回到局外”的可玩 Demo。

核心体验不是单场战斗，而是：

- 在时间压力下选择路线
- 在固定地图中建立空间记忆
- 在房间里做即时战术取舍
- 决定何时继续搜、何时撤离

## 当前主循环

1. 局外整备
2. 选择地图模板
3. 选择难度
4. 入局
5. 在房间实时玩法中推进
6. 搜索容器、与敌交战、通过房门转场
7. 抵达撤离点并撤离，或团灭失败
8. 结算并返回局外

## 当前已实现内容

### 1. 局外系统

- 已有完整的局外整备界面。
- 已接通仓库和本局背包的区分。
- 已接通商店买卖。
- 已接通征募士兵。
- 已有基础初始资金和初始物资。
- 士兵 roster 会进入本局，并在永久死亡后从局外 roster 中移除。

### 2. 地图系统

- 当前有 2 套固定地图模板。
- 地图不是程序随机拓扑，采用手工节点与连线。
- 地图节点类型包括：
  - `Room`
  - `Battle`
  - `Search`
  - `Extract`
- 每张图都有固定出口和固定房间分布。
- 当前支持视野和记忆态显示。
- 当前支持 4 支 AI 小队在同图推进。
- 地图浮窗通过 `M` 键开关。

### 3. 难度系统

- 当前难度为：
  - `试锋`
  - `整军`
  - `倾巢`
- 难度会影响威胁强度和战利品规模。
- 不同地图模板允许进入的难度不同。

### 4. 房间实时玩法

- 战斗、搜索、过门已经合并为同一个实时房间状态。
- 玩家平时主要操作的是房间层，而不是一直在节点图上点击。
- 地图作为战略浮窗存在，用于观察局势和规划下一步。
- 房门转场已实现，并遵循“对门入场”规则。
- 英雄穿门时，存活士兵会同步转移到新房间。

### 5. 搜索与容器

- 房间容器已接通。
- 容器支持逐件揭示。
- 支持自动搜索开关。
- 搜索按时间推进，不再是旧的“房间送免费搜索次数”逻辑。
- 存在独立尸体/精英尸体容器。
- 精英尸体容器支持装备栏和背包区分。

### 6. 战斗系统

- 当前战斗发生在房间内，不再走旧弹窗战斗主路径。
- 单位包括英雄、士兵和敌方单位。
- 当前已区分近战与远程。
- 当前已有命中、受击、硬直、短停顿、击退等反馈。
- 近战斩击特效已替换为基于弧线的 `点 -> 线 -> 点` 渐变表现。
- 近战命中反馈明显强于远程击中反馈。

### 7. AI 行为

- 地图层 AI 小队会移动、清图、搜刮、交战和撤离。
- 房间内单位行为已不再只是最原始的贴脸换血。
- 近战单位已切到轻量状态机：
  - `Advance`
  - `AttackCommit`
  - `Retreat`
  - `Regroup`
- 近战单位会在冷却中后撤，在接近可攻击时重新贴近。
- 已补上极近距离分离兜底，避免两个近战面贴面原地绕圈。
- 远程单位也有轻量状态机：
  - 超出射程会逼近
  - 过近会后撤
  - 冷却中会横移整位

### 8. UI 与分辨率

- 默认窗口分辨率已调整为 `1600x900`。
- 主地图区、侧栏和局外整备布局会按视口动态重算。
- 侧栏、按钮、时间条、容器列表等核心 UI 已整体放大一档，更适配当前分辨率。

## 当前已经明确废弃的旧内容

- 旧的“近战特效调试场”不再属于正式流程。
- 当前不会再用调试节点替换正常地图入口。
- 当前不会再用测试专用角色替换正常初始队伍。
- 旧的“先切入弹窗战斗，再返回地图”的主路径已经废弃。
- 旧的“必须先清空威胁才能搜索/过门”的设计不再成立。
- 旧的“进房间赠送免费搜索次数”逻辑已经废弃。

## 当前的正式入口与初始配置

- 游戏启动后进入正常主场景 `NodeRaidDemo.tscn`。
- 地图入口为正常地图首节点，不再是调试房间。
- 初始队伍来自正常 hideout roster。
- 默认初始 roster 为 2 名士兵。

## 当前手感层结论

### 命中与击退

- 目前只有受击方会被击退。
- 攻击者不会被击退，但会在命中时吃到更明显的短僵直。
- 受击方会获得更长的受击停顿、更长的硬直和更远的击退。
- 近战击退明显强于远程。

### 当前效果方向

- 近战单位行为目标已经从“贴脸站桩”改为“出手、后撤、再切入”的简单拉扯。
- 远程单位目标是“保持舒服距离，不在最前线原地罚站”。
- 当前已经是可玩版本，但还没到特别聪明或特别有职业感的阶段。

## 当前仍然薄弱的部分

- 局外成长还比较浅，缺少完整的英雄成长、配装和长期养成。
- 士兵系统还没有形成真正的兵种树和清晰职业差异。
- 房间战斗虽然已可玩，但职业分工、技能层和目标选择还偏薄。
- 地图层信息已经够用，但状态图标、日志语言和结算提示仍可继续统一。
- 旧代码里仍有一部分不可达的历史分支，需要后续清理。

## 下一阶段建议优先级

### 第一优先级：继续打磨正式战斗

- 继续强化近战与远程的职业差异。
- 补目标选择、仇恨倾向、阵型感和技能层。
- 继续打磨命中、受击、硬直和击退反馈。

### 第二优先级：做更清晰的兵种与成长

- 让士兵不再只是“多带几个近战单位”。
- 做出近战、远程、精锐、廉价炮灰等明确定位。
- 补经验、晋升、装备适配和局外成长。

### 第三优先级：继续做地图复玩

- 强化固定地图上的空间记忆价值。
- 强化 AI 小队对局势的动态扰动。
- 让不同地图模板在节奏和路线决策上更有差异。

## 当前判断

- 项目已经不在“搭主框架”的阶段。
- 第一版可玩闭环已经跑通。
- 当前最值得投入的是：继续打磨正式战斗与单位行为，而不是再做临时调试入口。
