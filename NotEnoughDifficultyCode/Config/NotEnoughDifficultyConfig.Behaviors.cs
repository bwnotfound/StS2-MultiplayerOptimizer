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
    ///     通用「敌人移除列表」的持久化后备字段（需求2）。存被排除 encounter 的 Id.Entry，
    ///     以分隔符 ';' 拼接（例如 "DOORMAKER_BOSS;THE_GUARDIAN"）。
    ///     - 用 string 而非 List&lt;string&gt;：config 序列化走 TypeConverter 转字符串存 Dictionary，
    ///       集合类型无法 round-trip，string 才能正确存读。
    ///     - [ConfigHideInUI]：照常存读到 cfg 文件，但不自动生成 UI——增删由自建弹窗
    ///       （RemovalListPopup，[ConfigButton] 打开）操作，运行时由 ExtraActsConfig.ApplyRemovalFilter
    ///       在 act4/5 抽取点排除。
    ///     - 取代旧的 ExcludeDoormakerFromBossPool 单开关（已删除）；要排除门扉只需把
    ///       DOORMAKER_BOSS 加进本列表即可。旧 cfg 里的 ExcludeDoormakerFromBossPool 值在加载时
    ///       因无对应属性被忽略，不影响读取。
    /// </summary>
    [ConfigHideInUI]
    public static string ExcludedEncounterIdsCsv { get; set; } = "";

    /// <summary>
    ///     移除列表的生效范围开关（需求2追加）。由 RemovalListPopup 里的勾选框控制。
    ///     - false（默认）：<b>全层生效</b>——移除项在 1~5 层都被排除（base act 1~3 由
    ///       BaseActRemovalFilterPatch 替换抽取，4~5 层由 CustomActEncounterReplacementPatch 过滤）。
    ///     - true：<b>只在 4~5 层生效</b>——base act 1~3 不过滤，仅 4~5 层排除。
    ///     多人：该 bool 会被 ConfigSync 同步，两端需一致（连同移除列表本身）才不 desync。
    /// </summary>
    [ConfigHideInUI]
    public static bool RemovalOnlyActs45 { get; set; } = false;

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