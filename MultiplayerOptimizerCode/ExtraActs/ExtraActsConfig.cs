using System;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 池子混合权重 (act1, act2, act3)。
/// 总和不需要等于 1——内部按归一化处理。全 0 视为退化（结果空）。
/// </summary>
internal readonly record struct PoolWeights(double Act1, double Act2, double Act3)
{
    public double Sum => Act1 + Act2 + Act3;
}

/// <summary>
/// 把 <see cref="MultiplayerOptimizerConfig"/> 的 BaseLib slider 字段封装为语义化 API。
/// 业务代码读 <c>ExtraActsConfig.GetBossWeights(4)</c> 而不是直接读
/// <c>MultiplayerOptimizerConfig.Act4_BossWeight_Act1</c>，
/// 后续重命名或调整 schema 时只动这一层。
/// </summary>
internal static class ExtraActsConfig
{
    /// <summary>
    /// 敌人池权重。
    /// - Act4：用于 elite encounters 加权混合
    /// - Act5：暂不暴露此项（act5 全是 boss 内容，用 GetBossWeights）
    /// </summary>
    public static PoolWeights GetEncounterWeights(int actIdx)
    {
        return actIdx switch
        {
            4 => new PoolWeights(
                MultiplayerOptimizerConfig.Act4_EncWeight_Act1,
                MultiplayerOptimizerConfig.Act4_EncWeight_Act2,
                MultiplayerOptimizerConfig.Act4_EncWeight_Act3),
            _ => throw new ArgumentOutOfRangeException(nameof(actIdx),
                $"GetEncounterWeights only supports actIdx=4 (got {actIdx})")
        };
    }

    /// <summary>事件池权重。Act4 / Act5 各自配置。</summary>
    public static PoolWeights GetEventWeights(int actIdx)
    {
        return actIdx switch
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
    }

    /// <summary>
    /// Boss 池权重。
    /// - Act4：用于顶端 boss 加权抽样（通过在 GenerateAllEncounters 里重复添加 boss 实现）
    /// - Act5：用于中部战斗节点的 boss 内容混合；Act5 最终 boss 不参与（必须从 Glory 抽，需求 5.3）
    /// </summary>
    public static PoolWeights GetBossWeights(int actIdx)
    {
        return actIdx switch
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
    }
}