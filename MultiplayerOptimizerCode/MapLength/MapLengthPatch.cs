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
///
/// 本 patch 在 <c>ActModel.GetNumberOfRooms</c> 的 postfix 把返回值改成
/// <c>(配置行数 - 1)</c>，于是 <c>_mapLength = (配置行数 - 1) + 1 = 配置行数</c>。
///
/// ## 连带影响（base game 的配套设计，不是 bug）
///
/// <c>GetNumberOfRooms</c> 还决定该 act 的普通 encounter 池大小——
/// <c>ActModel.GenerateRooms</c> 里 <c>for i in [NumberOfWeakEncounters, GetNumberOfRooms)</c>
/// 每次循环生成一个普通 encounter。地图变长 → 战斗节点变多 → encounter 池同步变大，
/// 这正是 base game 想要的配套关系。
///
/// boss 节点位置 <c>BossMapPoint = (3, _mapLength)</c> 由 <c>_mapLength</c> 动态算，
/// 自动适配新长度；地图后处理（MapPathPruning / MapPostProcessing）全部按 <c>grid.GetLength()</c>
/// 动态尺寸工作，无需改动。
///
/// ## 单人 / 多人
///
/// base game 的 <c>GetNumberOfRooms</c> 在多人时会额外 -1。本 patch 是 postfix，<b>直接覆盖</b>
/// <c>__result</c>（不分单人多人），所以配置的行数就是最终行数，所见即所得；且 host/client
/// 一定返回相同值——地图用 seeded RNG 生成，两端长度必须一致才能生成相同地图，否则地图不同步。
/// （配置值本身由 ConfigSync 在 lobby 同步。）
///
/// ## 稳定性要求
///
/// <c>GetNumberOfRooms</c> 会在多个时机被调用（地图生成、encounter 生成、进度计算）。
/// 只要它对同一个 act 每次返回相同值，这些用途就一致——本 patch 读的是 config 值，
/// run 期间不变，天然稳定。
///
/// ## 安全
///
/// - 行数 Clamp 到 [<see cref="MinRows"/>, <see cref="MaxRows"/>]。下限 10 防止 base game
///   <c>AssignPointTypes</c> 里 <c>GetRowCount()-7</c> 在 <c>_mapLength &lt; 7</c> 时数组越界崩溃。
/// - 认不出的 act（<see cref="ResolveActIndex"/> 返回 -1）→ 不改，保持原版长度。
/// - mod 总开关 <c>Enabled</c> 关闭 → 不改，地图恢复原版。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetNumberOfRooms))]
public static class MapLengthPatch
{
    /// <summary>地图行数下限。低于 7 会让 base game AssignPointTypes 数组越界崩溃，取 10 留足余量。</summary>
    public const int MinRows = 10;

    /// <summary>地图行数上限。grid 动态分配，无硬上界，30 是体验上的合理上限。</summary>
    public const int MaxRows = 30;

    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, ref int __result)
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

            // config 存的是地图行数 _mapLength；base game StandardActMap 里
            // _mapLength = GetNumberOfRooms + 1，所以 GetNumberOfRooms 要返回 rows - 1。
            __result = rows - 1;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"MapLengthPatch failed: {ex}");
        }
    }

    /// <summary>
    /// 把 ActModel 实例映射到 1-based act 序号。认不出返回 -1（调用方据此放弃改值）。
    /// 用类型判断：base game 当前 act1/2/3 = Underdocks/Hive/Glory，本 mod 的 = Act4Model/Act5Model。
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