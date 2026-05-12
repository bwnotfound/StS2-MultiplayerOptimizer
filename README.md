# StS2-MultiplayerOptimizer

[English](docs/README.en.md) | 中文

为《Slay the Spire 2》多人联机模式提供扩展层数和难度调控的 mod。

## 简介

base game 多人模式遇到的两个痛点：

1. **三层后就结束了**——多人 run 跟单人 run 一样只有 3 层，体量偏小；
2. **难度对单人调校**——多人多个玩家一起打同一群敌人时，怪物 HP/伤害没有相应放大，整体战斗强度偏弱。

本 mod 加入了第 4/5 层，对所有敌人和 boss 提供按层精细调节的 HP/伤害倍率，并通过完整的配置同步机制确保所有联机玩家在 host
端的设置生效。

## 功能特性

### 自定义第 4/5 层

- **第 4 层**：全精英战斗的关卡。所有地图节点强制为精英战斗图标。战斗内容从前 3 层的精英战池按可配置权重混合抽取。
- **第 5 层**：含最终 boss 的关卡。中间所有战斗节点打 boss 强度战斗，顶端是真正的 boss。
- **支持的关键功能**：每层独立的 ancient、event 池、宝物房、boss 不重复等。

### 难度倍率（按 act 独立配置）

- **全局倍率**：普通敌人 HP/伤害可设置「层内起始 → 结束」线性插值，boss HP/伤害单值倍率；
- **来源倍率**：根据敌人原属的 act（act1/2/3）独立加倍——比如 act4 中遇到的 act1 敌人 HP × 1.4 × 1.8，act3 敌人 HP × 1.4 ×
  1.0。

### 池子权重混合

第 4/5 层的 encounter / event / boss / ancient 池子从前 3 层按用户配置的权重混合抽取。权重保存时自动归一化到 sum =
1，无需手动算。

### 自动配置同步（ack-based）

多人联机时，**host 端的所有 mod 配置自动同步到所有 client**，整局 run 期间生效；run 结束后 client 端从磁盘恢复自己原本的配置。如果某个
client mod 版本太旧或安装异常导致同步失败，host 端会弹窗提示拒绝开 run，避免战斗中数值不一致导致玩家被踢。

## 安装

### 依赖

- 《Slay the Spire 2》基础游戏
- [BaseLib](https://github.com/Alchyr/BaseLib-StS2) **v3.1.2**（严格版本——base game 联机会校验 mod 版本字符串完全一致）

### 步骤

1. 在 `<游戏根目录>/mods/` 下解压 `MultiplayerOptimizer/` 文件夹，确保其中包含：
    - `MultiplayerOptimizer.dll`
    - `MultiplayerOptimizer.pck`
    - `MultiplayerOptimizer.json`
2. 同样方式安装 BaseLib `v3.1.2`
3. 启动游戏，主菜单 → 设置 → Mods 启用 MultiplayerOptimizer 和 BaseLib

启动后游戏 log 第一行确认 mod 加载成功：

```
[INFO] [MultiplayerOptimizer] [Init] Loading MultiplayerOptimizer version 0.3.0
```

## 配置

主菜单 → 设置 → Mods → **MultiplayerOptimizer** → Configure

按分类组织的滑块：

| 分类                                                      | 说明                                          |
|---------------------------------------------------------|---------------------------------------------|
| `Act4_EncWeights` / `EventWeights` / `BossWeights`      | 第 4 层 encounter / event / boss 池从前 3 层混合的权重 |
| `Act4_NormalEnemyMultipliers`                           | 第 4 层普通敌人 HP / 伤害倍率（按层进度起始→结束线性插值）          |
| `Act4_BossMultipliers`                                  | 第 4 层 boss HP / 伤害倍率                        |
| `Act4_NormalEnemySrcMultipliers` / `BossSrcMultipliers` | 按敌人原属 act 的独立倍率                             |
| `Act5_*`                                                | 同上，配置第 5 层                                  |
| `BehaviorToggles`                                       | act5 boss 警告开关、final boss 去重开关等行为类设置        |

**池子权重设为 0 时**：归一化逻辑会回落到默认值（`Act1=0.25, Act2=0.35, Act3=0.40`），避免除 0。

## 多人联机

### 重要：所有玩家的 mod 版本必须严格一致

base game 用 `<mod_id>-<version>` 拼字符串校验联机双方的 mod 列表，任何一个字符不一致（包括是否有 `v` 前缀、点号位置）都会被判为
ModMismatch 拒绝加入。

**最稳的做法**：host 把整个 mod 文件夹打包发给所有玩家，让大家**完全替换**自己本地的 `MultiplayerOptimizer/` 目录。

### 配置同步原理

```
host 点 ready 开 run
  ↓
host 端 mod 把所有配置打包广播给 client
  ↓ ≤ 3 秒
所有 client 收到，apply 到本地静态字段，回 ack
  ↓
host 收齐 ack → 调用原 begin run 流程 → 进入战斗
              ↓ 否则
              弹窗"Mod 版本不兼容，请让以下玩家升级"，run 不会启动
```

同步发生在 lobby 阶段，对玩家是无感的（除非出错弹窗）。run 结束后所有 client 从磁盘 reload 自己的配置，不会污染本地设置。

### 联机故障排查

| 现象                                     | 可能原因                                                    |
|----------------------------------------|---------------------------------------------------------|
| 加入 lobby 时直接被踢 "Mod Mismatch"          | 玩家间 manifest 文件不一致（version 字符串不同 / 多装一个 mod / 少装一个 mod） |
| host 弹窗"Mod 版本不兼容" + run 没启动           | 某个 client mod 没装好或版本过旧，sync 消息没回 ack                    |
| 战斗中出现 "State divergence, disconnected" | host/client 实际计算结果不一致——通常是 client 没启用 mod，或者 sync 没生效   |

如果遇到 sync 失败的弹窗，让 client 重新安装最新 mod 文件夹后重启游戏。

## 已知问题

- **加载多人少人存档可能卡黑屏**：例如 3 人存档但只有 2 个玩家上线时加载，base game 的 `CombatStateSynchronizer` 内部 sync
  可能死锁，导致黑屏不结束。**临时解决方案**：等所有原玩家齐了再加载，或者新开 run。
- **不同版本 mod 之间无法兼容**：升级 mod 后旧版本的同伴需要同步升级，不存在"我用新版他用旧版"的协议层兼容。

## 版本历史

| 版本    | 主要变化                                                                                   |
|-------|----------------------------------------------------------------------------------------|
| 0.3.0 | LoadRunLobby 路径也启用 ack-based config sync；version 改为运行时从 manifest 读取（避免代码/json 两处版本号漂移） |
| 0.2.0 | ConfigSync 改为 ack-based，client 没响应时 host 拒绝开 run + popup                               |
| 0.1.0 | 初版，第 4/5 层基础功能 + 配置广播（fire-and-forget）                                                 |

## 反馈 / 贡献

bug 上报、功能建议请到 [GitHub Issues](https://github.com/bwnotfound/StS2-MultiplayerOptimizer/issues)。

报 bug 时请附上：

- mod 版本号（log 第一行）
- 复现步骤
- host 完整 godot.log
- 如果可能，client 完整 godot.log

## 致谢

- [Alchyr](https://github.com/Alchyr) 的 [BaseLib](https://github.com/Alchyr/BaseLib-StS2)
  和 [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2) 模板
- [GlitchedReme](https://github.com/GlitchedReme)
  的 [中文 STS2 modding 教程](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)