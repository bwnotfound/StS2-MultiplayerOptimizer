# StS2-NotEnoughDifficulty（还不够难！）

[English](docs/README.en.md) | 中文

为《Slay the Spire 2》把原版 3 层塔扩展为 **5 层塔**，并提供一整套**按层精细调节的难度、地图与敌人池**配置的 mod。单人、多人联机均可用；多人下 host 端配置会自动同步给所有玩家。

> 当前版本 **0.7.0**。本版本已整体迁移适配到新游戏版本，并基于 **BaseLib v3.3.0** 的登记制本地化重构。

---

## 简介

原版（尤其多人）的两个体验痛点：

1. **三层就结束**——run 体量偏小；
2. **难度按单人调校**——多人合力打同一群敌人时强度偏弱。

本 mod 在原版 3 层之后加入**第 4 层（全精英）**与**第 5 层（含最终 boss）**，并提供按层独立的 HP/伤害倍率、难度预设、地图长度调节、敌人移除列表、额外加速等功能。多人联机时通过 ack-based 配置同步确保所有玩家以 host 端设置开局。

---

## 功能特性

### 1. 自定义第 4 / 5 层

- **第 4 层**：全精英关卡，所有地图节点强制为精英战图标；战斗内容从前 3 层的精英池按可配置权重混合抽取。
- **第 5 层**：含最终 boss 的关卡，中段所有战斗节点打 boss 强度战斗，顶端是真正的最终 boss。
- 每层有独立的 ancient / event 池、宝物房、休息点，boss 不与前几层重复。

### 2. 难度倍率（按 act 独立）

- **整体倍率（Overall）**：每层一个 HP / 伤害总旋钮，叠加在其它倍率之上，是调整整体难度最直接的方式。
- **全局倍率**：普通敌人 HP/伤害支持「层内进度起始 → 结束」线性插值；boss 为单值倍率。
- **来源倍率（Src）**：按敌人原属的 act（act1/2/3）再独立加倍——例如第 4 层里遇到的 act1 敌人与 act3 敌人可分别乘不同系数。

### 3. 难度预设（一键）

提供 **简单 / 困难 / 极限** 三个预设按钮，一键设置第 4/5 层的整体 HP/伤害倍率（仅改 Overall 四个值，不动权重与细项，行为可预测）。预设数值在代码中集中定义，便于按平衡意图调整。

### 4. 地图长度与房间密度

- 每个 act 可独立设置地图行数（默认 act1=16 / act2=15 / act3/4/5=14，等于原版）。
- 配有**总开关（默认关闭）**：开启后长度调节才生效，并隐藏各层滑块前的误触风险——避免玩家在不知情下改变地图结构。
- 长度变大时同步**缩放特殊房（精英/休息/问号）密度**，避免地图被普通战稀释；并对超长地图**跳过指数级路径修剪**以保证性能。

### 5. 敌人移除列表

- 一个自建弹窗（配置页「管理」按钮打开），扫描游戏已注册的**普通 / 精英 / boss** encounter，提供三个下拉供选择加入移除列表，支持增删，含重复增删与池排空兜底。
- 列表/下拉中的敌人名带**层后缀**（如 `(第一层)`），帮助判断各层池大小；无法归层的来源标 `(其它)`。
- 含**生效范围开关**：默认「全层（1~5 层）生效」，可勾选改为「只在 4~5 层生效」。
- 被移除的敌人在对应层抽取时被排除（base 层用替换式过滤，避免删空导致抽不出战斗）。

### 6. 额外加速模式

- 在**游戏官方设置界面**注入两行（启用开关 + 倍率滑块），与 mod 配置界面**数值同步**。
- 提供超出原版上限的战斗/动画加速倍率，加快刷局节奏。

### 7. 池子权重混合

第 4/5 层的 encounter / event / boss / ancient 池子，从前 3 层按用户配置的权重混合抽取。权重保存时自动归一化（sum = 1），无需手动计算；全设为 0 时回落默认值避免除 0。

### 8. 多人配置同步（ack-based）

多人联机时 host 端的全部 mod 配置自动同步给所有 client，整局生效；run 结束后 client 从磁盘恢复本地配置。若某 client 版本过旧或同步失败，host 弹窗拒绝开 run，避免战斗中数值不一致被踢。

### 9. 读档健壮性兜底

针对多 mod 环境下（其它 mod 在 `FromSerializable` 链上处理额外 act 时丢数据）可能出现的读档崩溃，提供防御性兜底：读档时补全 null 的房间 id 列表以避免硬崩，并打印受影响 act 的诊断信息。

---

## 安装

### 依赖

- 《Slay the Spire 2》基础游戏
- [BaseLib](https://github.com/Alchyr/BaseLib-StS2) **v3.3.0**（严格版本——联机会校验 mod 版本字符串完全一致）

### 步骤

1. 在 `<游戏根目录>/mods/` 下解压 `NotEnoughDifficulty/` 文件夹，确保包含：
   - `NotEnoughDifficulty.dll`
   - `NotEnoughDifficulty.pck`
   - `NotEnoughDifficulty.json`
2. 同样方式安装 BaseLib **v3.3.0**
3. 启动游戏，主菜单 → 设置 → Mods 启用 NotEnoughDifficulty 和 BaseLib

启动后 log 确认加载成功（版本号在运行时从 manifest 读取，避免代码与 json 漂移）：

```
[INFO] [NotEnoughDifficulty] Loading NotEnoughDifficulty 0.7.0
```

---

## 配置

主菜单 → 设置 → Mods → **NotEnoughDifficulty** → Configure。配置按分区组织：

| 分区 | 说明 |
|------|------|
| `General` | 总开关（启用/禁用整套难度功能） |
| `Presets` | 简单 / 困难 / 极限 三个一键预设按钮 |
| `Act4Act5Scaling` | 折叠开关，控制下方 4/5 层细项是否展开，避免一进页面信息过载 |
| `Act4_OverallMultipliers` / `Act5_OverallMultipliers` | 每层整体 HP / 伤害倍率 |
| `Act4_NormalEnemyMultipliers` / `Act5_NormalEnemyMultipliers` | 普通敌人 HP/伤害（层内进度起始→结束线性插值） |
| `Act4_BossMultipliers` / `Act5_FinalBossMultipliers` | boss / 最终 boss HP/伤害倍率 |
| `Act4_NormalEnemySrcMultipliers` / `Act4_BossSrcMultipliers` 等 | 按敌人原属 act 的独立倍率（普通 / boss，4/5 层各一组） |
| `Act4_EncWeights` / `Act4_EventWeights` / `Act4_BossWeights` / `Act5_*` | 各层池子从前 3 层混合的权重 |
| `MapLength` | 地图长度总开关 + 各 act 行数滑块 |
| `RemovalList` | 敌人移除列表入口（「管理」按钮打开弹窗） |
| `Speed` | 额外加速倍率（与游戏设置界面注入的两行同步） |
| `BehaviorToggles` | act5 boss 警告、final boss 去重等行为类开关 |
| `Experimental` | 实验性选项 |

> **池子权重设为 0 时**：归一化逻辑回落默认值（`Act1=0.25, Act2=0.35, Act3=0.40`），避免除 0。

---

## 多人联机

### 重要：所有玩家的 mod 版本必须严格一致

base game 用 `<mod_id>-<version>` 拼字符串校验联机双方的 mod 列表，任何字符不一致（包括 `v` 前缀、点号位置）都会被判为 ModMismatch 拒绝加入。

**最稳做法**：host 把整个 mod 文件夹打包发给所有玩家，让大家**完全替换**本地的 `NotEnoughDifficulty/` 目录（连同相同版本的 BaseLib）。

### 配置同步流程

```
host 点 ready 开 run
  ↓ host 端把所有配置打包广播给 client
  ↓ ≤ 3 秒
所有 client 收到 → apply 到本地静态字段 → 回 ack
  ↓ host 收齐 ack → 走原 begin run 流程 → 进入战斗
  ↓ 否则
  弹窗「Mod 版本不兼容，请让以下玩家升级」，run 不启动
```

同步发生在 lobby 阶段，对玩家无感（除非出错弹窗）。run 结束后 client 从磁盘 reload 自己的配置，不污染本地设置。

### 故障排查

| 现象 | 可能原因 |
|------|---------|
| 加入 lobby 直接被踢 "Mod Mismatch" | 玩家间 manifest 不一致（version 字符串不同 / 多装少装某 mod） |
| host 弹窗「Mod 版本不兼容」+ run 没启动 | 某 client 没装好或版本过旧，sync 没回 ack |
| 战斗中 "State divergence, disconnected" | host/client 计算结果不一致——通常是 client 没启用 mod 或 sync 未生效 |

遇到 sync 失败弹窗，让 client 重装最新 mod 文件夹后重启游戏。

---

## 项目结构（迁移重构后）

代码均在 `NotEnoughDifficultyCode/`，按功能分目录：

| 目录 | 职责 |
|------|------|
| `Core/` | 入口 `MainFile`、逐类隔离 patch、`PatchScope` 总开关、run 状态访问 |
| `Config/` | `NotEnoughDifficultyConfig`（按 partial 拆分各分区）+ `ExtraActsConfig` 逻辑层 |
| `ExtraActs/` | 第 4/5 层：`Bootstrap`（注入 act 列表）、`Models`（Act4/5Model）、`Patches`（encounter 替换/去重/地图节点）、`Pool`（混合/去重工具）、`Compat`（兼容兜底） |
| `Difficulty/` | HP/伤害倍率运行时应用、desync 诊断 |
| `MapLength/` | 地图长度 patch、密度缩放、超长地图跳过路径修剪 |
| `RemovalList/` | 敌人移除列表弹窗 UI |
| `SpeedControl/` | 额外加速倍率控制 |
| `SettingsInjection/` | 向游戏官方设置界面注入速度两行（与配置同步） |
| `MultiplayerSync/` | ack-based 配置同步、确定性模型哈希 |
| `Act5/` | 第 5 层中段 boss 流程/奖励/去重等专项 patch |
| `SaveCompat/` | `COMPAT-PRELAUNCH` 旧存档兼容代码清单（文档目录） |

---

## 已知问题

- **多 mod 环境下读档崩溃**：某些 mod 在 `FromSerializable` 链上处理本 mod 的额外 act 时可能丢失房间数据，导致读档报 `ArgumentNullException`。本 mod 已加防御兜底避免硬崩，但若受影响的是当前 act 仍可能有后续问题——遇到时请反馈日志（见 `ExtraActs/Compat/RoomSetLoadNullGuardPatch.cs` 打印的内容）。
- **加载多人少人存档可能卡黑屏**：例如 3 人存档只有 2 人上线时加载，base game 的 `CombatStateSynchronizer` 内部可能死锁。临时方案：等原玩家齐了再加载，或新开 run。
- **不同版本 mod 之间不兼容**：升级后同伴需同步升级，不存在协议层向后兼容。

---

## 版本历史

| 版本 | 主要变化 |
|------|---------|
| 0.7.0 | 整体迁移适配新游戏版本 + BaseLib v3.3.0（登记制本地化）；新增难度预设、地图长度/密度调节、敌人移除列表（含层后缀与生效范围开关）、额外加速模式、读档健壮性兜底；目录与配置重构 |
| 0.4–0.6 | 难度系统扩充（整体/来源倍率、按层细项）、配置 UI 折叠分区、多项兼容性修复（增量迭代） |
| 0.3.0 | LoadRunLobby 路径也启用 ack-based config sync；version 改为运行时从 manifest 读取 |
| 0.2.0 | ConfigSync 改为 ack-based，client 没响应时 host 拒绝开 run + popup |
| 0.1.0 | 初版：第 4/5 层基础功能 + 配置广播（fire-and-forget） |

> 0.4–0.6 为增量迭代，未保留逐版本精确变更记录；上表对该区间做归纳。

---

## 反馈 / 贡献

bug 上报、功能建议请到 [GitHub Issues](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/issues)。报 bug 请附：

- mod 版本号（log 首行）
- 复现步骤
- host 完整 godot.log（如可能，附 client 的）

---

## 致谢

- [Alchyr](https://github.com/Alchyr) 的 [BaseLib](https://github.com/Alchyr/BaseLib-StS2) 与 [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2)
- [GlitchedReme](https://github.com/GlitchedReme) 的 [中文 STS2 modding 教程](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)