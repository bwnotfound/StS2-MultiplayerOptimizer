using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     池子混合权重 (act1, act2, act3)。归一化后总和应当 = 1。
/// </summary>
internal readonly record struct PoolWeights(double Act1, double Act2, double Act3)
{
    public double Sum => Act1 + Act2 + Act3;
}

/// <summary>
///     数值倍率（起始值 / 结束值），按 act 内进度做线性插值。
/// </summary>
internal readonly record struct ScalingRange(double Start, double End)
{
    public double Lerp(double progress)
    {
        progress = Math.Clamp(progress, 0.0, 1.0);
        return Start + (End - Start) * progress;
    }
}

/// <summary>
///     把 <see cref="NotEnoughDifficultyConfig" /> 的 BaseLib slider 字段封装为语义化 API。
///     池权重 getter 返回值是<b>已归一化</b>的（即使磁盘上的原值不归一，运行时使用值总是 sum=1）。
///     这是防御性做法——配合 WeightNormalizationPatch 在保存时也归一化，但即使绕过保存（手改 ini），
///     业务逻辑仍能拿到正常的归一化权重。
/// </summary>
internal static class ExtraActsConfig
{
    // ---------- 池子混合权重 ----------

    /// <summary>
    ///     默认权重值，用于（a）显示；（b）当前权重 sum=0 时的 fallback。
    ///     跟 NotEnoughDifficultyConfig 字段的初始化值保持一致。
    /// </summary>
    public static readonly PoolWeights DefaultWeights = new(0.25, 0.35, 0.40);

    // ============================================================
    // 通用敌人移除列表（需求2）数据层
    // ============================================================
    //
    // 取代旧的 _exclusions（单一 Doormaker 开关）。移除项以 Id.Entry 字符串存于
    // NotEnoughDifficultyConfig.ExcludedEncounterIdsCsv（';' 分隔）。运行时由 ApplyRemovalFilter
    // 在 act4/5 抽取点排除。增删由自建弹窗调用 Add/RemoveExclusion，落盘后即时生效（下次抽取）。

    private const char ExclusionSep = ';';

    /// <summary>当前移除列表（Id.Entry 集合），从 csv 解析。Ordinal 匹配，与游戏 Id.Entry 大小写一致。</summary>
    public static HashSet<string> GetExcludedIds()
    {
        return new HashSet<string>(
            NotEnoughDifficultyConfig.ExcludedEncounterIdsCsv
                .Split(ExclusionSep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);
    }

    /// <summary>添加一个排除 id。兜底：已存在则不加、返回 false。</summary>
    public static bool AddExclusion(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var set = GetExcludedIds();
        if (!set.Add(id.Trim())) return false;
        NotEnoughDifficultyConfig.ExcludedEncounterIdsCsv = string.Join(ExclusionSep, set);
        return true;
    }

    /// <summary>移除一个排除 id。兜底：不存在则不动、返回 false（防重复删除）。</summary>
    public static bool RemoveExclusion(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var set = GetExcludedIds();
        if (!set.Remove(id.Trim())) return false;
        NotEnoughDifficultyConfig.ExcludedEncounterIdsCsv = string.Join(ExclusionSep, set);
        return true;
    }

    /// <summary>
    ///     枚举游戏中已注册、属于指定 tier 的 encounter，返回 (id, 显示名) 列表（供 UI 下拉）。
    ///     - 数据源 ModelDb.AllEncounters（含其它 mod），按 EncounterModel.RoomType 过滤。
    ///     - 显示名优先用 EncounterModel.Title 的本地化文本，取不到回退 Id.Entry。
    ///     - 时机：在「打开移除列表弹窗」时实时调用——那时所有 mod act 已注册、AllEncounters 完整。
    ///     - 容错：任何访问抛异常都跳过该项，绝不让 UI 崩。
    /// </summary>
    public static IReadOnlyList<(string id, string name)> ListEncounters(RoomType tier)
    {
        var result = new List<(string id, string name)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var layerMap = BuildLayerMap();
        IEnumerable<EncounterModel> all;
        try
        {
            all = ModelDb.AllEncounters;
        }
        catch
        {
            return result;
        }

        var unmapped = new List<string>();
        foreach (var e in all)
        {
            try
            {
                if (e == null || e.RoomType != tier) continue;
                var id = e.Id?.Entry;
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;

                var name = ResolveDisplayName(e, id);
                // 追加层后缀，例如 " (第一层)"；映射不到层的（其它 mod/特殊来源）标 "(其它)"，
                // 保证每个都有后缀，并收集无主 id 供诊断
                if (layerMap.TryGetValue(id, out var layer))
                {
                    name += " " + LayerSuffix(layer);
                }
                else
                {
                    name += " " + LayerSuffix(0);
                    unmapped.Add(id);
                }

                result.Add((id, name));
            }
            catch
            {
                // 单个 encounter 访问异常（Id/RoomType/Title）不影响其它项
            }
        }

        if (unmapped.Count > 0)
        {
            MainFile.Logger.Info(
                $"ListEncounters({tier}): {unmapped.Count} encounter(s) 无层映射 → 标记(其它): " +
                string.Join(", ", unmapped));
        }

        result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.CurrentCulture));
        return result;
    }

    /// <summary>
    ///     建立 encounter id → 所属层(1/2/3) 的映射。用 <c>ModelDb.ActsByIndex</c> 按 act 的 Index 分组：
    ///     同一层可能有多个变体 act（StS2 第一层就有 Overgrowth 和 Underdocks 两个区域，Index 都是 0），
    ///     全部映射到该层，避免「某变体独有的 encounter 没后缀」。
    ///     只取前 3 个 base 层（Index 0/1/2 → 第1/2/3层）：act4/5 复用 1~3 层 encounter、不单独成层，
    ///     若把它们也扫进来会把复用的 encounter 误标成第4/5层。其它 mod 的 act 若挂在 Index 0/1/2 也会被正确归层。
    ///     全程 try 包裹，取不到就少几个后缀，不崩。
    /// </summary>
    private static Dictionary<string, int> BuildLayerMap()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            var byIndex = ModelDb.ActsByIndex;
            int layers = System.Math.Min(byIndex.Count, 3); // 只映射 base 三层
            for (int i = 0; i < layers; i++)
            {
                int layer = i + 1; // Index 0 = 第1层
                foreach (var act in byIndex[i])
                {
                    if (act == null) continue;
                    try
                    {
                        foreach (var e in act.AllEncounters)
                        {
                            var id = e?.Id?.Entry;
                            if (!string.IsNullOrEmpty(id)) map[id] = layer;
                        }
                    }
                    catch
                    {
                        // 单个 act 取池异常 → 跳过该 act
                    }
                }
            }
        }
        catch
        {
            // ActsByIndex 取不到 → 空表，全部无后缀（不崩）
        }

        return map;
    }

    /// <summary>层后缀文本，例如 "(第一层)"。layer&lt;=0 表示无层映射 → "(其它)"。取不到本地化回退英文。</summary>
    private static string LayerSuffix(int layer)
    {
        var key = layer >= 1
            ? $"NOTENOUGHDIFFICULTY-LAYER_{layer}.title"
            : "NOTENOUGHDIFFICULTY-LAYER_OTHER.title";
        try
        {
            var s = LocString.GetIfExists("settings_ui", key);
            var text = s?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        catch
        {
            // ignore
        }

        return layer >= 1 ? $"(Act {layer})" : "(Other)";
    }

    /// <summary>显示名：优先 EncounterModel.Title 本地化文本，取不到/为空/等于原始 key 时回退 Id.Entry。</summary>
    public static string ResolveDisplayName(EncounterModel e, string fallbackId)
    {
        try
        {
            var title = e.Title;
            var text = title?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(text) &&
                !text.Contains(".title")) // 未命中本地化时 LocString 常回显原始 key，含 ".title" 视为未翻译
            {
                return text;
            }
        }
        catch
        {
            // ignore，回退 id
        }

        return fallbackId;
    }

    // ============================================================
    // BGM Bank "额外可播放 event" 白名单
    // ============================================================
    //
    // ## 背景
    // 自定义 act（Act4/5）的 BGM bank 是直接复用 act3 (Glory) 的 bank，所以 act3 boss 的
    // event ID 必然包含在 bank 中。但混合战斗会拿 act1/2 的 boss encounter，act3 的 bank
    // 不一定包含它们的 BgmEvent，调用 PlayCustomMusic 会失败。
    //
    // CustomActMissingBgmFallbackPatch 的策略：
    //   1. 优先尝试播放 encounter 自己的 BgmEvent
    //   2. 失败则回落到 act 的默认 boss BGM
    //
    // 这里的白名单解决一个反向问题：act3 bank 包含但**不属于** act3 boss 的 event（比如
    // CeremonialBeast，它是 act2 boss 但也被 act3 bank 包含）。这些 event 直接播放即可。
    //
    // 白名单是数据驱动的，未来新增"跨 act 复用"的 event 在这里加一行字符串即可。

    /// <summary>
    ///     act3 bank（被自定义 act 复用）实际包含但不属于 act3 boss 的 event ID。
    ///     这些 event 即使是从其他 act 的 boss 取过来的，也能直接在自定义 act 中播放。
    /// </summary>
    public static readonly HashSet<string> Act3BankCrossActExtras =
        new(StringComparer.Ordinal)
        {
            "event:/music/boss/ceremonial_beast"
            // 未来发现其他 act 跨 bank 复用的 event 可以加在这里
        };

    // ---------- 行为开关 ----------

    public static bool ShouldShowAct5DisguisedBossWarning =>
        NotEnoughDifficultyConfig.Act5_ShowDisguisedBossWarning;

    public static bool ShouldAvoidAct5FinalBossEqualPenultimate =>
        NotEnoughDifficultyConfig.Act5_AvoidFinalBossEqualPenultimate;

    public static PoolWeights GetEncounterWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                NotEnoughDifficultyConfig.Act4_EncWeight_Act1,
                NotEnoughDifficultyConfig.Act4_EncWeight_Act2,
                NotEnoughDifficultyConfig.Act4_EncWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    public static PoolWeights GetEventWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                NotEnoughDifficultyConfig.Act4_EventWeight_Act1,
                NotEnoughDifficultyConfig.Act4_EventWeight_Act2,
                NotEnoughDifficultyConfig.Act4_EventWeight_Act3),
            5 => new PoolWeights(
                NotEnoughDifficultyConfig.Act5_EventWeight_Act1,
                NotEnoughDifficultyConfig.Act5_EventWeight_Act2,
                NotEnoughDifficultyConfig.Act5_EventWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    public static PoolWeights GetBossWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                NotEnoughDifficultyConfig.Act4_BossWeight_Act1,
                NotEnoughDifficultyConfig.Act4_BossWeight_Act2,
                NotEnoughDifficultyConfig.Act4_BossWeight_Act3),
            5 => new PoolWeights(
                NotEnoughDifficultyConfig.Act5_BossWeight_Act1,
                NotEnoughDifficultyConfig.Act5_BossWeight_Act2,
                NotEnoughDifficultyConfig.Act5_BossWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    /// <summary>
    ///     归一化到 sum=1。sum 接近 0 时（用户手动改成全 0）回落到默认权重，避免除 0。
    /// </summary>
    private static PoolWeights Normalize(PoolWeights raw)
    {
        var sum = raw.Sum;
        if (sum <= 1e-9) return DefaultWeights;
        if (Math.Abs(sum - 1.0) < 1e-6) return raw;
        return new PoolWeights(raw.Act1 / sum, raw.Act2 / sum, raw.Act3 / sum);
    }

    // ---------- 全局数值倍率 ----------

    public static ScalingRange GetNormalEnemyHpMult(int actIdx)
    {
        return actIdx switch
        {
            4 => new ScalingRange(
                NotEnoughDifficultyConfig.Act4_NormalEnemyHpMultStart,
                NotEnoughDifficultyConfig.Act4_NormalEnemyHpMultEnd),
            5 => new ScalingRange(
                NotEnoughDifficultyConfig.Act5_NormalEnemyHpMultStart,
                NotEnoughDifficultyConfig.Act5_NormalEnemyHpMultEnd),
            _ => new ScalingRange(1.0, 1.0)
        };
    }

    public static ScalingRange GetNormalEnemyDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => new ScalingRange(
                NotEnoughDifficultyConfig.Act4_NormalEnemyDmgMultStart,
                NotEnoughDifficultyConfig.Act4_NormalEnemyDmgMultEnd),
            5 => new ScalingRange(
                NotEnoughDifficultyConfig.Act5_NormalEnemyDmgMultStart,
                NotEnoughDifficultyConfig.Act5_NormalEnemyDmgMultEnd),
            _ => new ScalingRange(1.0, 1.0)
        };
    }

    public static double GetBossHpMult(int actIdx)
    {
        return actIdx switch
        {
            4 => NotEnoughDifficultyConfig.Act4_BossHpMult,
            5 => NotEnoughDifficultyConfig.Act5_FinalBossHpMult,
            _ => 1.0
        };
    }

    public static double GetBossDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => NotEnoughDifficultyConfig.Act4_BossDmgMult,
            5 => NotEnoughDifficultyConfig.Act5_FinalBossDmgMult,
            _ => 1.0
        };
    }

    // ---------- 全局总倍率 ----------

    /// <summary>
    ///     全局 HP 倍率（叠加在所有其他 HP 倍率最末尾）。
    ///     用于快速调整后两层整体难度，不破坏已经平衡好的细节倍率。
    ///     默认 1.0，act1-3 不适用返回 1.0。
    /// </summary>
    public static double GetOverallHpMult(int actIdx)
    {
        return actIdx switch
        {
            4 => NotEnoughDifficultyConfig.Act4_OverallHpMult,
            5 => NotEnoughDifficultyConfig.Act5_OverallHpMult,
            _ => 1.0
        };
    }

    /// <summary>
    ///     全局伤害倍率（叠加在所有其他伤害倍率最末尾）。
    ///     用于快速调整后两层整体难度，不破坏已经平衡好的细节倍率。
    ///     默认 1.0，act1-3 不适用返回 1.0。
    /// </summary>
    public static double GetOverallDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => NotEnoughDifficultyConfig.Act4_OverallDmgMult,
            5 => NotEnoughDifficultyConfig.Act5_OverallDmgMult,
            _ => 1.0
        };
    }

    // ---------- 来源 act 倍率（普通敌人） ----------

    public static double GetSourceNormalEnemyHpMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => NotEnoughDifficultyConfig.Act4_NormalEnemySrcHpMult_Act1,
            (4, 2) => NotEnoughDifficultyConfig.Act4_NormalEnemySrcHpMult_Act2,
            (4, 3) => NotEnoughDifficultyConfig.Act4_NormalEnemySrcHpMult_Act3,
            (5, 1) => NotEnoughDifficultyConfig.Act5_NormalEnemySrcHpMult_Act1,
            (5, 2) => NotEnoughDifficultyConfig.Act5_NormalEnemySrcHpMult_Act2,
            (5, 3) => NotEnoughDifficultyConfig.Act5_NormalEnemySrcHpMult_Act3,
            _ => 1.0
        };
    }

    public static double GetSourceNormalEnemyDmgMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => NotEnoughDifficultyConfig.Act4_NormalEnemySrcDmgMult_Act1,
            (4, 2) => NotEnoughDifficultyConfig.Act4_NormalEnemySrcDmgMult_Act2,
            (4, 3) => NotEnoughDifficultyConfig.Act4_NormalEnemySrcDmgMult_Act3,
            (5, 1) => NotEnoughDifficultyConfig.Act5_NormalEnemySrcDmgMult_Act1,
            (5, 2) => NotEnoughDifficultyConfig.Act5_NormalEnemySrcDmgMult_Act2,
            (5, 3) => NotEnoughDifficultyConfig.Act5_NormalEnemySrcDmgMult_Act3,
            _ => 1.0
        };
    }

    // ---------- 来源 act 倍率（boss） ----------

    public static double GetSourceBossHpMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => NotEnoughDifficultyConfig.Act4_BossSrcHpMult_Act1,
            (4, 2) => NotEnoughDifficultyConfig.Act4_BossSrcHpMult_Act2,
            (4, 3) => NotEnoughDifficultyConfig.Act4_BossSrcHpMult_Act3,
            (5, 1) => NotEnoughDifficultyConfig.Act5_FinalBossSrcHpMult_Act1,
            (5, 2) => NotEnoughDifficultyConfig.Act5_FinalBossSrcHpMult_Act2,
            (5, 3) => NotEnoughDifficultyConfig.Act5_FinalBossSrcHpMult_Act3,
            _ => 1.0
        };
    }

    public static double GetSourceBossDmgMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => NotEnoughDifficultyConfig.Act4_BossSrcDmgMult_Act1,
            (4, 2) => NotEnoughDifficultyConfig.Act4_BossSrcDmgMult_Act2,
            (4, 3) => NotEnoughDifficultyConfig.Act4_BossSrcDmgMult_Act3,
            (5, 1) => NotEnoughDifficultyConfig.Act5_FinalBossSrcDmgMult_Act1,
            (5, 2) => NotEnoughDifficultyConfig.Act5_FinalBossSrcDmgMult_Act2,
            (5, 3) => NotEnoughDifficultyConfig.Act5_FinalBossSrcDmgMult_Act3,
            _ => 1.0
        };
    }

    /// <summary>
    ///     把所有启用的 boss 池过滤规则应用到一个 encounter 序列。
    ///     适用场景：
    ///     - Act4 顶部 boss 抽样池
    ///     - Act5 中部所有战斗用的 act1/2/3 boss 混合池
    ///     - Act5 顶部最终 boss 抽样池（即 Glory.AllEncounters 中的 boss 部分）
    ///     对非 boss encounter 不影响（只按 Id.Entry 字符串匹配，普通战斗/精英战斗的 ID 不会跟
    ///     boss 排除列表里的 ID 撞）。
    ///     任何步骤抛异常都不会向上传播——返回原列表（不应用过滤）。这是 hot path 不能阻塞游戏。
    /// </summary>
    /// <summary>
    ///     按「敌人移除列表」过滤一个 encounter 池：剔除 Id.Entry 命中 GetExcludedIds() 的 encounter。
    ///     用于 act4 elite 池、act5 boss 混合池、顶端 boss 池三处运行时抽取点。
    ///
    ///     ## 关键兜底（池排空回退）
    ///     若过滤会把一个<b>非空</b>池清空（玩家把该池能选的都加进了移除列表），则<b>回退返回原池</b>，
    ///     避免抽不出战斗导致生成失败。代价是被排除项在该极端情况下仍可能出现——这是「有战斗」优先于
    ///     「严格排除」的安全取舍。
    ///
    ///     ## 容错
    ///     - 列表为空 → 直接返回原列表（跳过 enumeration）。
    ///     - 任何异常 → 返回原列表（hot path 不阻塞游戏）。
    ///     - 只按 Id.Entry 字符串匹配，与具体 EncounterModel 子类型完全解耦；列表里残留的「已不存在 id」
    ///       永远匹配不到，是 no-op。
    /// </summary>
    public static List<EncounterModel> ApplyRemovalFilter(IEnumerable<EncounterModel> source)
    {
        var list = source as List<EncounterModel> ?? source.ToList();

        HashSet<string> excluded;
        try
        {
            excluded = GetExcludedIds();
        }
        catch
        {
            return list;
        }

        if (excluded.Count == 0) return list; // 移除列表为空 → 跳过

        try
        {
            var filtered = list.Where(e =>
            {
                var entry = e.Id?.Entry;
                return entry == null || !excluded.Contains(entry); // Id 为 null 保守不剔除
            }).ToList();

            // ★ 池排空回退：非空池被清空 → 返回原池，保证有战斗可抽
            if (filtered.Count == 0 && list.Count > 0) return list;
            return filtered;
        }
        catch
        {
            return list;
        }
    }

    // ============================================================
    // 移除列表过滤说明
    // ============================================================
    //
    // ## 解耦设计（沿用旧 boss 过滤的思想）
    // 移除项用字符串 Id.Entry 匹配，跟 base game 的具体 EncounterModel 子类型完全解耦：
    //   - 编译期：mod 不 reference 任何具体 EncounterModel 子类型。
    //   - 运行期：列表里残留的「已不存在 id」永远匹配不到 → no-op，不崩溃。
    //
    // ## Id.Entry 的字符串值
    // base game 的 ModelDb.GetEntry 用 StringHelper.Slugify(type.Name) 生成 ID（CamelCase→下划线、
    // 大写化、移除特殊字符），即 `DoormakerBoss` 的 Id.Entry == "DOORMAKER_BOSS"。UI 下拉直接展示
    // ListEncounters() 扫描到的真实 id，无需硬编码。
}