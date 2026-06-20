# SaveCompat：COMPAT-PRELAUNCH 清单

本目录是**文档目录**——集中记录所有带 `COMPAT-PRELAUNCH` 标签的代码位置，方便用户在
"那个正在玩的旧存档"通关或弃坑后，一键搜索 `COMPAT-PRELAUNCH` 删除全部遗留兼容代码。

约定细节见 `REFACTORING_PLAN.md §7A`。

## 当前清单

| # | 位置 | 类型 | 兼容什么 | 删除方式 |
|---|------|------|---------|---------|
| 1 | `ExtraActs/Compat/ValidateAncientAfterLoadPatch.cs` | 整文件 | 重构前 mod 创建的旧存档中 `_rooms.Ancient` 为 null | 整文件直接删除（无其他位置依赖） |

## 删除流程

1. 确认那个正在玩的旧存档已通关或弃坑（不会再加载它）
2. 全局搜索 `COMPAT-PRELAUNCH`，定位所有标记位置
3. 按上表逐个处理：整文件 → 删文件；单行/段 → 按头部"删除方式"注释操作
4. 重新编译验证：搜索关键字应只剩本 README 自己，代码内无残留
5. 跑一遍新开 run 全流程测试，确认功能正常

## 关于"为什么只有这一处"

本次重构严格遵守了"以重构后版本为兼容起点"原则，没有引入额外的向前兼容包袱：
- `NotEnoughDifficultyConfig` 所有字段名保持不变 → 旧 cfg 文件能读
- `Act4Model` / `Act5Model` 类名不变 → 旧存档里的 `Acts[i].Id == "ACT4"/"ACT5"` 能匹配
- `GetUnlockedAncients` 返回 Glory ancients（正确实现）→ 新 run 不再产生 null ancient

所以只剩下"旧存档已经存进磁盘的 null AncientId"这一个遗留状态需要兼容，即上面唯一一条。

## 不属于 COMPAT-PRELAUNCH 的"看起来像但不是"

以下代码看起来像兼容代码但**不是**向前兼容——属于运行时鲁棒性或未来兼容，长期保留：

- `ConfigSyncMessage` 字段名字典 + ignore unknown — host/client 跨版本协议兼容（未来兼容）
- `WeightNormalizationPatch.LoadProps` 反射 null fallback — 反射失败鲁棒性
- `RunStateAccessor` 反射 fallback — 应对 base game 内部字段重命名
- `Act4Model.GetUnlockedAncients` 返回 Glory ancients — **正确实现**而非兜底
- `SourceActResolver` cache 每 run 失效 — 应对其他 mod 后注册 encounter
- 各 patch 的 try/catch + log — 保护原方法调用者
