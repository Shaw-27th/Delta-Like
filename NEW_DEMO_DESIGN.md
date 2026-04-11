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

- Unit deaths now feed scene containers directly.
- Both player-side and enemy-side deaths can generate container loot.
- Normal deaths try to merge into an existing nearby corpse pile.
- Elite / boss deaths always create a separate elite container.
- The current death-loot contribution is intentionally simple and randomized for now; the merge mechanism exists and can be expanded later.

### Current Container/Search Direction

- Sidebar container listing is no longer the main interaction path.
- Search is currently intended for non-combat only.
- The existing grid-based reveal/search/take flow is still in use after a container is opened.
- Container scene presentation is currently functional placeholder art, not final visual design.

### Current Known Follow-up

- Static room container placement still uses preset anchor points, not per-room handcrafted dressing.
- Some old container/sidebar helper code may still remain in the file even though the main interaction path has moved to in-room containers.
- Death-generated container visuals are usable but not yet final and can still be polished.

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
