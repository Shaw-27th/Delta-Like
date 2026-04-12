# 单位机制参考

## 作用范围

- 这份文档记录当前代码里已经真实生效的单位数值与战斗规则。
- 它应始终与 `RaidMapDemo.cs` 的实际状态保持一致。
- 它是维护参考文档，不是提案或愿景文档。

## 全局战斗规则

- `HP`：当前与最大生命值。
- `Damage`：每次攻击从该区间中随机取值。
- `Armor`：在特殊倍率之后生效的固定减伤。
- `AttackRange`：近战或远程交战距离。
- `Speed`：基础移动速度，不含跑动和技能覆盖。
- `AttackCycleScale`：攻击节奏倍率，越低越快。
- `Stamina`：跑动与部分主动技能会消耗体力。
- `ProjectileDamageScale`：远程伤害在计算护甲前先乘上的倍率。
- `BlockAnyDamageChance`：在结算伤害前，有概率直接完全格挡这次攻击。

## 小兵通用技能规则

- 大部分非远程小兵默认主动技能是 `跑动`。
- 弓兵不跑动。
- 盾系兵种不跑动。
- 盾系被动基础版：
- `Missile Guard`：远程伤害减半。
- 更高盾阶会额外获得 `BlockAnyDamageChance`。

## 主控英雄

- HP：据点基础状态是 `24`，但实际单局会沿用当前持久状态，可能更高或更低。
- Damage：`2-5`
- Range：`164`
- Speed：`165`
- Stamina：基础模板 `88`
- 当前定位：
- 玩家直接控制的核心作战单位
- 可进行远程攻击
- 搜索与撤离的核心锚点
- 资源球吸附与收取者

## 士兵 roster 兵种

### 新兵

- HP：`8`
- Damage：`1-3`
- Armor：`0`
- Range：`28`
- Speed：`152`
- Stamina：`72`
- Active：`跑动`
- Passive：无

### 盾兵

- HP：`15`
- Damage：`1-2`
- Armor：`2`
- Range：`26`
- Speed：`122`
- Stamina：`86`
- Active：无
- Passive：
- `Missile Guard`

### 精锐盾兵

- HP：`20`
- Damage：`2-4`
- Armor：`3`
- Range：`28`
- Speed：`132`
- Stamina：`104`
- AttackCycleScale：`0.9`
- Active：
- `盾冲`
- Passive：
- `Missile Guard`
- Visual：
- 胸甲
- 更大的盾
- 暂无头盔

### 钢盔盾卫

- HP：`24`
- Damage：`3-5`
- Armor：`4`
- Range：`30`
- Speed：`138`
- Stamina：`112`
- AttackCycleScale：`0.86`
- Active：
- `盾冲`
- Passive：
- `Missile Guard`
- `BlockAnyDamageChance = 0.18`
- Visual：
- 胸甲
- 头盔
- 强化盾系轮廓

### 壁垒盾卫

- HP：`28`
- Damage：`4-7`
- Armor：`5`
- Range：`32`
- Speed：`144`
- Stamina：`126`
- AttackCycleScale：`0.8`
- Active：
- `盾冲`
- 强化版路线冲击波特效
- Passive：
- `Missile Guard`
- `BlockAnyDamageChance = 0.28`
- Ranged damage scale：`0.45`
- Visual：
- 胸甲
- 头盔
- 更大且更华丽的盾

### 枪兵

- HP：`9`
- Damage：`2-4`
- Armor：`0`
- Range：`44`
- Speed：`146`
- Stamina：`76`
- Active：`跑动`
- Passive：无

### 刀兵

- HP：`8`
- Damage：`2-5`
- Armor：`0`
- Range：`28`
- Speed：`164`
- Stamina：`78`
- Active：`跑动`
- Passive：无

### 弓兵

- HP：`7`
- Damage：`1-4`
- Armor：`0`
- Range：`176`
- Speed：`148`
- Stamina：`0`
- Active：无
- Passive：无

### 骑兵

- HP：`11`
- Damage：`3-6`
- Armor：`0`
- Range：`34`
- Speed：`176`
- Stamina：`104`
- AttackCycleScale：`0.92`
- Active：`跑动`
- Passive：无
- Special：
- 比普通线列步兵更重的近战轮廓
- 比普通近战更重的攻击进入手感

## 盾冲规则

- 当前使用者：
- `精锐盾兵`
- `钢盔盾卫`
- `壁垒盾卫`
- 冷却：`5s`
- 体力消耗：`24`
- 基础触发窗口：
- 目标距离大于 `AttackRange + 18`
- 目标距离小于等于 `150`
- 施放者必须处于可行动状态，且体力足够
- 基础冲刺：
- 持续时间 `0.28`
- 移动速度 `520`
- 路径效果：
- 会把沿路单位撞开
- 会对敌我双方都产生物理推开
- 同阵营单位只被推开，不吃伤害
- 终点效果：
- 比路径中的接触有更强的击退和控制

### 壁垒盾卫的强化盾冲

- 路径和终点影响半径更大
- 伤害更高
- 硬直更强
- 击退力度和持续时间更强
- 带路线对齐的冲击波特效
- 冲击波只做视觉表现，不额外增加碰撞逻辑

## 升阶路径

- `新兵 -> 盾兵`
- `盾兵 -> 精锐盾兵`
- `精锐盾兵 -> 钢盔盾卫`
- `钢盔盾卫 -> 壁垒盾卫`

## 升阶需求

- `盾兵`：`XP 2`，`18` 金钱
- `精锐盾兵`：`XP 6`，`42` 金钱
- `钢盔盾卫`：`XP 10`，`70` 金钱
- `壁垒盾卫`：`XP 15`，`110` 金钱
- `枪兵`：`XP 2`，`18` 金钱
- `刀兵`：`XP 2`，`18` 金钱
- `弓兵`：`XP 2`，`22` 金钱
- `骑兵`：`XP 3`，`40` 金钱

## 士兵经验规则

- 存活士兵清空一个房间时获得 `+1 XP`。
- 成功撤离时，存活士兵再获得 `+1 XP`。
- 死亡士兵会从本局 roster 中移除，不会继续获得经验。

## 敌方单位

### 普通威胁守军

- 刷新数量：`clamp(1 + threat / 2, 2, 6)`
- HP：`6 + threat`
- Damage：`1 + threat / 3` 到 `3 + threat / 2`
- Range：
- 远程变体：`156`
- 近战变体：`28`
- 分布：
- 大约每三个里有一个远程

### AI 小队队长

- HP：`12 + squad.Strength / 2`
- Damage：`3-6`
- Range：`172`
- Flags：
- 精英
- AI 小队
- 远程

### AI 小队普通成员

- 数量：`clamp(squad.Strength / 2, 3, 7)`
- HP：`7`
- Damage：`1-3`
- Range：
- 远程变体：`152`
- 近战变体：`28`
- 分布：
- 大约每三个里有一个远程

## 仅调试用途的战斗单位

- 代码里仍然保留了一些只用于战斗预览 / 特效测试的调试友军与敌军。
- 这些不属于正常成长或经济系统的一部分，不应当被当作正式平衡数据。
