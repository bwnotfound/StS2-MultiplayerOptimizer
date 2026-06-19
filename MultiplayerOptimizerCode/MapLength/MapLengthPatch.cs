// ReSharper disable InconsistentNaming

using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// 自定义每个 act 的<b>地图长度</b>（从起点到 boss 的行数）。
///
/// ## 原理
///
/// base game <c>StandardActMap</c> 构造函数：
/// <code>
///   _mapLength = actModel.GetNumberOfRooms(isMultiplayer) + 1;
///   Grid = new MapPoint[7, _mapLength];
/// </code>
/// 地图宽固定 7 列，行数（长度）= <c>GetNumberOfRooms() + 1</c>。
/// 本 patch 在 <c>ActModel.GetNumberOfRooms</c> 的 postfix 按 config 覆盖返回值。
///
/// ## ⚠️ 多人同步：为什么必须保留 isMultiplayer 语义
///
/// <c>GetNumberOfRooms</c> 不只决定地图长度，还决定 <c>ActModel.GenerateRooms</c> 里
/// regular encounter 的生成数量。<c>GenerateRooms</c> 全程用同一个 act 专属 <c>rng</c>，
/// 按顺序消耗：
/// <code>
///   for (i = NumberOfWeakEncounters; i &lt; GetNumberOfRooms(isMultiplayer); i++)
///       AddWithoutRepeatingTags(normalEncounters, grabBag2, rng);   // regular，次数 = GetNumberOfRooms - weak
///   for (i = 0; i &lt; 15; i++)
///       AddWithoutRepeatingTags(eliteEncounters, grabBag3, rng);    // elite，紧接着用同一个 rng
/// </code>
/// 如果 host 和 client 的 <c>GetNumberOfRooms</c> 不一致 → regular encounter 循环次数不同
/// → 消耗 <c>rng</c> 次数不同 → 紧接着的 elite encounter 抽取整体错位 → 两端精英战
/// spawn 出<b>完全不同的怪</b> → 一进精英战 checksum 不匹配 → desync 把队友踢出。
/// （这种 desync 的特征：全局 RNG counter 两端一致——因为 encounter 用的是 act 专属
/// 独立 rng，不计入全局 counter——但战斗里的怪不同。）
///
/// base game 的 <c>GetNumberOfRooms</c> 多人时会 <c>-1</c>（多人地图比单人少 1 行）。
/// 早期版本的本 patch 直接 <c>__result = rows - 1</c> 无视 <c>isMultiplayer</c>，后果：
///   - 多人模式下，即使 config 是默认值，patch 返回值也比原版多人多 1；
///   - 跟没装本功能的旧版 mod（原版多人值）差 1 → 必 desync。
///
/// <b>修正</b>：保留多人 <c>-1</c> 语义。config 表示"单人地图行数"，多人地图按 base game
/// 规律自动少 1 行：
/// <code>
///   单人: _mapLength = rows      → GetNumberOfRooms = rows - 1
///   多人: _mapLength = rows - 1  → GetNumberOfRooms = rows - 2
/// </code>
/// 这样当 config = 默认值（= 原版单人 <c>BaseNumberOfRooms + 1</c>）时：
/// <code>
///   单人 GetNumberOfRooms = (BaseNumberOfRooms+1) - 1 = BaseNumberOfRooms      ✓ 原版单人
///   多人 GetNumberOfRooms = (BaseNumberOfRooms+1) - 2 = BaseNumberOfRooms - 1  ✓ 原版多人
/// </code>
/// patch 在默认配置下与原版逐值相等、完全"隐身"——即使 host/client 的 mod 版本不一致
/// （一端有本 patch、一端没有），只要没人改过 config，两端 <c>GetNumberOfRooms</c> 仍然
/// 相同，不会 desync。
///
/// ## 仍然存在的硬性前提
///
/// 一旦真的调了某个 act 的地图长度（config 不再是默认值），host 和 client 必须装<b>完全
/// 相同版本</b>的 mod。<c>GetNumberOfRooms</c> 影响 encounter 生成，两端差一点就 desync。
/// ConfigSync 会同步 config 值，但前提是两端的 mod 都有这些字段、都有本 patch。
///
/// ## 连带影响（base game 的配套设计，不是 bug）
///
/// <c>GetNumberOfRooms</c> 变大 → regular encounter 池同步变大（地图战斗节点多了本来
/// 就需要更多 encounter）。boss 节点位置 <c>BossMapPoint = (3, _mapLength)</c> 由
/// <c>_mapLength</c> 动态算，自动适配。
///
/// ## 安全
///
/// - 行数（单人 <c>_mapLength</c>）Clamp 到 [<see cref="MinRows"/>, <see cref="MaxRows"/>]
///   = [10, 30]。多人 <c>_mapLength = rows - 1</c> ∈ [9, 29]，仍 ≥ 7，不会触发 base game
///   <c>AssignPointTypes</c> 里 <c>GetRowCount()-7</c> 的数组越界。
/// - 认不出的 act（<see cref="ResolveActIndex"/> 返回 -1）→ 不改，保持原版长度。
/// - mod 总开关 <c>Enabled</c> 关闭 → 不改。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetNumberOfRooms))]
public static class MapLengthPatch
{
    /// <summary>地图行数下限（单人）。低于 7 会让 base game AssignPointTypes 数组越界，取 10 留足余量。</summary>
    public const int MinRows = 10;

    /// <summary>地图行数上限。grid 动态分配，无硬上界，30 是体验上的合理上限。</summary>
    public const int MaxRows = 30;

    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, bool isMultiplayer, ref int __result)
    {
        if (!PatchScope.IsEnabled) return;
        if (__instance == null) return;

        try
        {
            int actIdx = ResolveActIndex(__instance);
            if (actIdx < 1 || actIdx > 5) return; // 认不出的 act：保持原版长度

            double configured = GetConfiguredRows(actIdx);
            int rows = (int)Math.Round(configured);
            rows = Math.Clamp(rows, MinRows, MaxRows);

            // config 存的是"单人地图行数"。base game 里 _mapLength = GetNumberOfRooms + 1，
            // 且多人地图比单人少 1 行。沿用这个规律：
            //   单人: _mapLength = rows      → GetNumberOfRooms = rows - 1
            //   多人: _mapLength = rows - 1  → GetNumberOfRooms = rows - 2
            // 默认 config（= 原版单人 BaseNumberOfRooms+1）下，单人/多人都精确等于原版，
            // patch 完全"隐身"——这是避免与旧版 mod desync 的关键。
            __result = isMultiplayer ? rows - 2 : rows - 1;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"MapLengthPatch failed: {ex}");
        }
    }

    /// <summary>
    /// 把 ActModel 实例映射到 1-based act 序号。认不出返回 -1。
    /// base game 当前 act1/2/3 = Underdocks/Hive/Glory，本 mod 的 = Act4Model/Act5Model。
    /// Overgrowth 也映射到 act1，兼容旧版/其它模式下 act1 可能是该类型的情况。
    /// </summary>
    private static int ResolveActIndex(ActModel act)
    {
        return act switch
        {
            Underdocks => 1,
            Overgrowth => 1,
            Hive => 2,
            Glory => 3,
            Act4Model => 4,
            Act5Model => 5,
            _ => -1,
        };
    }

    private static double GetConfiguredRows(int actIdx) => actIdx switch
    {
        1 => MultiplayerOptimizerConfig.Act1_MapLength,
        2 => MultiplayerOptimizerConfig.Act2_MapLength,
        3 => MultiplayerOptimizerConfig.Act3_MapLength,
        4 => MultiplayerOptimizerConfig.Act4_MapLength,
        5 => MultiplayerOptimizerConfig.Act5_MapLength,
        _ => 14,
    };
}