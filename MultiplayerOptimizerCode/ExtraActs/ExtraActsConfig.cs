using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 池子混合权重 (act1, act2, act3)。归一化后总和应当 = 1。
/// </summary>
internal readonly record struct PoolWeights(double Act1, double Act2, double Act3)
{
    public double Sum => Act1 + Act2 + Act3;
}

/// <summary>
/// 数值倍率（起始值 / 结束值），按 act 内进度做线性插值。
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
/// 把 <see cref="MultiplayerOptimizerConfig"/> 的 BaseLib slider 字段封装为语义化 API。
///
/// 池权重 getter 返回值是**已归一化**的（即使磁盘上的原值不归一，运行时使用值总是 sum=1）。
/// 这是防御性做法——配合 WeightNormalizationPatch 在保存时也归一化，但即使绕过保存（手改 ini），
/// 业务逻辑仍能拿到正常的归一化权重。
/// </summary>
internal static class ExtraActsConfig
{
    // ---------- 池子混合权重 ----------

    /// <summary>
    /// 默认权重值，用于（a）显示；（b）当前权重 sum=0 时的 fallback。
    /// 跟 MultiplayerOptimizerConfig 字段的初始化值保持一致。
    /// </summary>
    public static readonly PoolWeights DefaultWeights = new(0.25, 0.35, 0.40);

    public static PoolWeights GetEncounterWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_EncWeight_Act1,
                MultiplayerOptimizerConfig.Act4_EncWeight_Act2,
                MultiplayerOptimizerConfig.Act4_EncWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    public static PoolWeights GetEventWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_EventWeight_Act1,
                MultiplayerOptimizerConfig.Act4_EventWeight_Act2,
                MultiplayerOptimizerConfig.Act4_EventWeight_Act3),
            5 => new PoolWeights(
                MultiplayerOptimizerConfig.Act5_EventWeight_Act1,
                MultiplayerOptimizerConfig.Act5_EventWeight_Act2,
                MultiplayerOptimizerConfig.Act5_EventWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    public static PoolWeights GetBossWeights(int actIdx)
    {
        var raw = actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_BossWeight_Act1,
                MultiplayerOptimizerConfig.Act4_BossWeight_Act2,
                MultiplayerOptimizerConfig.Act4_BossWeight_Act3),
            5 => new PoolWeights(
                MultiplayerOptimizerConfig.Act5_BossWeight_Act1,
                MultiplayerOptimizerConfig.Act5_BossWeight_Act2,
                MultiplayerOptimizerConfig.Act5_BossWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx))
        };
        return Normalize(raw);
    }

    /// <summary>
    /// 归一化到 sum=1。sum 接近 0 时（用户手动改成全 0）回落到默认权重，避免除 0。
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
                MultiplayerOptimizerConfig.Act4_NormalEnemyHpMultStart,
                MultiplayerOptimizerConfig.Act4_NormalEnemyHpMultEnd),
            5 => new ScalingRange(
                MultiplayerOptimizerConfig.Act5_NormalEnemyHpMultStart,
                MultiplayerOptimizerConfig.Act5_NormalEnemyHpMultEnd),
            _ => new ScalingRange(1.0, 1.0)
        };
    }

    public static ScalingRange GetNormalEnemyDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => new ScalingRange(
                MultiplayerOptimizerConfig.Act4_NormalEnemyDmgMultStart,
                MultiplayerOptimizerConfig.Act4_NormalEnemyDmgMultEnd),
            5 => new ScalingRange(
                MultiplayerOptimizerConfig.Act5_NormalEnemyDmgMultStart,
                MultiplayerOptimizerConfig.Act5_NormalEnemyDmgMultEnd),
            _ => new ScalingRange(1.0, 1.0)
        };
    }

    public static double GetBossHpMult(int actIdx)
    {
        return actIdx switch
        {
            4 => MultiplayerOptimizerConfig.Act4_BossHpMult,
            5 => MultiplayerOptimizerConfig.Act5_FinalBossHpMult,
            _ => 1.0
        };
    }

    public static double GetBossDmgMult(int actIdx)
    {
        return actIdx switch
        {
            4 => MultiplayerOptimizerConfig.Act4_BossDmgMult,
            5 => MultiplayerOptimizerConfig.Act5_FinalBossDmgMult,
            _ => 1.0
        };
    }

    // ---------- 来源 act 倍率（普通敌人） ----------

    public static double GetSourceNormalEnemyHpMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcHpMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcHpMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcHpMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcHpMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcHpMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcHpMult_Act3,
            _ => 1.0
        };
    }

    public static double GetSourceNormalEnemyDmgMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcDmgMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcDmgMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_NormalEnemySrcDmgMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcDmgMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcDmgMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_NormalEnemySrcDmgMult_Act3,
            _ => 1.0
        };
    }

    // ---------- 来源 act 倍率（boss） ----------

    public static double GetSourceBossHpMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_BossSrcHpMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_BossSrcHpMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_BossSrcHpMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_FinalBossSrcHpMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_FinalBossSrcHpMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_FinalBossSrcHpMult_Act3,
            _ => 1.0
        };
    }

    public static double GetSourceBossDmgMult(int actIdx, int sourceActIdx)
    {
        return (actIdx, sourceActIdx) switch
        {
            (4, 1) => MultiplayerOptimizerConfig.Act4_BossSrcDmgMult_Act1,
            (4, 2) => MultiplayerOptimizerConfig.Act4_BossSrcDmgMult_Act2,
            (4, 3) => MultiplayerOptimizerConfig.Act4_BossSrcDmgMult_Act3,
            (5, 1) => MultiplayerOptimizerConfig.Act5_FinalBossSrcDmgMult_Act1,
            (5, 2) => MultiplayerOptimizerConfig.Act5_FinalBossSrcDmgMult_Act2,
            (5, 3) => MultiplayerOptimizerConfig.Act5_FinalBossSrcDmgMult_Act3,
            _ => 1.0
        };
    }

    // ---------- 行为开关 ----------

    public static bool ShouldShowAct5DisguisedBossWarning =>
        MultiplayerOptimizerConfig.Act5_ShowDisguisedBossWarning;

    public static bool ShouldAvoidAct5FinalBossEqualPenultimate =>
        MultiplayerOptimizerConfig.Act5_AvoidFinalBossEqualPenultimate;

    // ============================================================
    // Boss 池过滤
    // ============================================================
    //
    // ## 设计目标
    // 让"开关 → 该开关启用时要从 boss 池排除哪些 encounter"的映射跟 base game 的具体 EncounterModel
    // 子类**完全解耦**：
    //   - **编译期解耦**：mod 不 reference 任何具体的 EncounterModel 子类型（如 DoormakerBoss），
    //     即使将来 base game 删除这些类型，mod 也能正常编译。
    //   - **运行期容错**：用字符串 ID (Id.Entry) 匹配；如果 base game 那个 encounter 实际不存在了，
    //     字符串永远匹配不到 → filter 是 no-op → 既不崩溃也不影响其他 boss 抽样。
    //
    // ## 注册新过滤开关的步骤
    // 1. 在 MultiplayerOptimizerConfig.cs 加 `public static bool` 字段
    // 2. 在 _exclusions 数组里加一行 BossPoolExclusion
    // 3. 完。Act4Model / Act5Model / CustomActEncounterReplacementPatch 三处调用点无需改动
    //
    // ## 关于 Id.Entry 的字符串值
    // base game 的 ModelDb.GetEntry 用 StringHelper.Slugify(type.Name) 生成 ID:
    //   - CamelCase 拆分为下划线分隔
    //   - 大写化
    //   - 移除特殊字符
    // 即 `DoormakerBoss` 类的 Id.Entry == "DOORMAKER_BOSS"。
    //
    // 想确认某个 boss 的精确 ID 字符串，看其在 log 里的 encounter ID（如 "SOUL_NEXUS_ELITE"）
    // 或者直接对类名跑 Slugify。

    /// <summary>
    /// 一条 boss 池过滤规则：当 <see cref="IsEnabled"/> 返回 true 时，
    /// <see cref="ExcludedEntries"/> 列出的 Id.Entry 会从所有 boss 池中被剔除。
    /// </summary>
    /// <param name="Description">用于 log/调试的可读名字，不影响匹配逻辑。</param>
    private sealed record BossPoolExclusion(
        Func<bool> IsEnabled,
        string[] ExcludedEntries,
        string Description);

    /// <summary>所有注册的 boss 池过滤规则。新增过滤开关在这里追加一行 tuple 即可。</summary>
    private static readonly BossPoolExclusion[] _exclusions =
    {
        new(
            () => MultiplayerOptimizerConfig.ExcludeDoormakerFromBossPool,
            new[] { "DOORMAKER_BOSS" },
            "Doormaker")
        // 未来加新 boss 排除：
        // new(() => MultiplayerOptimizerConfig.ExcludeQueenFromBossPool,
        //     new[] { "QUEEN_BOSS" }, "Queen"),
    };

    /// <summary>
    /// 把所有启用的 boss 池过滤规则应用到一个 encounter 序列。
    ///
    /// 适用场景：
    ///   - Act4 顶部 boss 抽样池
    ///   - Act5 中部所有战斗用的 act1/2/3 boss 混合池
    ///   - Act5 顶部最终 boss 抽样池（即 Glory.AllEncounters 中的 boss 部分）
    ///
    /// 对非 boss encounter 不影响（只按 Id.Entry 字符串匹配，普通战斗/精英战斗的 ID 不会跟
    /// boss 排除列表里的 ID 撞）。
    /// </summary>
    public static List<EncounterModel> ApplyBossPoolFilters(IEnumerable<EncounterModel> source)
    {
        var list = source as List<EncounterModel> ?? source.ToList();

        // 收集当前所有启用规则对应的排除 ID
        HashSet<string>? excluded = null;
        foreach (var rule in _exclusions)
        {
            if (!rule.IsEnabled()) continue;
            excluded ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in rule.ExcludedEntries) excluded.Add(id);
        }

        if (excluded == null) return list; // 所有过滤开关都关闭 → 跳过 enumeration

        // Id 可能为 null（未注册的 encounter），保守不过滤；
        // 只有 Id.Entry 命中排除列表才剔除。
        return list.Where(e =>
        {
            var entry = e.Id?.Entry;
            return entry == null || !excluded.Contains(entry);
        }).ToList();
    }
}