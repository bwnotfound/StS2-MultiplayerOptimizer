using BaseLib.Config;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     Partial: 行为开关字段。
/// </summary>
internal partial class NotEnoughDifficultyConfig
{
    // 行为开关
    // ============================================================

    [ConfigSection("BehaviorToggles")] public static bool Act5_ShowDisguisedBossWarning { get; set; } = true;

    public static bool Act5_AvoidFinalBossEqualPenultimate { get; set; } = true;

    /// <summary>
    ///     是否从第 4、5 层 boss 池中去除门扉缔造者（Doormaker）。
    ///     开启后影响三处：
    ///     1. Act4 顶部 boss 抽样（Act4Model.GenerateAllEncounters 构造的 boss 池）
    ///     2. Act5 中部所有战斗（CustomActEncounterReplacementPatch 用的 act1/2/3 boss 混合池）
    ///     3. Act5 顶部最终 boss（Act5Model.GenerateAllEncounters 复用的 Glory.AllEncounters）
    /// </summary>
    public static bool ExcludeDoormakerFromBossPool { get; set; } = false;

    /// <summary>
    ///     是否让 act4/5 玩家走的相邻战斗节点 encounter 不重复（避免连续打同样的怪组合）。
    ///     实现：CustomActEncounterReplacementPatch fill 完两个 list 之后用 EncounterDeduplicator
    ///     贪心重排（"任务调度: 重排相邻字符"算法）。
    ///     - Act4：单 list dedup（只消费 eliteEncounters，无 normalEncounters 路径）
    ///     - Act5：合并 normal+elite → 整体 dedup → 拆回（两个 list 都填 boss 池，cross-list 风险高）
    ///     不可避免的情况：池子小（用户把 act1/2 权重设为 0 只留 act3 等）时某个 encounter 频率
    ///     超过 (N+1)/2，数学上无法完全 dedup——log warn 提示但不影响游戏。
    /// </summary>
    public static bool AvoidAdjacentEncounterDuplicate { get; set; } = true;
}