// ReSharper disable InconsistentNaming

using HarmonyLib;
using MegaCrit.Sts2.Core.Map;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// 修复「地图长度拉大后进入地图非常卡」的性能问题。
///
/// ## 根因
/// <c>MapPathPruning.PruneAndRepair</c> → <c>PruneDuplicateSegments</c>（<c>while(PrunePaths)</c> 循环）
/// → <c>FindAllPaths</c>（无记忆化递归枚举从起点到 boss 的<b>所有</b>路径）。地图是共享子节点的 DAG，
/// 路径数随行数<b>指数级膨胀</b>，且每轮 prune 后反复重算。原版地图 ~15 行没问题，拉到 30 行爆炸。
///
/// ## 做法（低耦合、鲁棒）
/// 不重写 pruning 算法，只在「整步骤」边界跳过：当地图行数超过阈值时，prefix 直接 return false
/// 跳过整个 <c>PruneAndRepair</c>（pruning 只是去重美化路径，跳过后地图仍完全可玩，仅可能保留
/// 并行重复段）。
///
/// ## 多人安全
/// - 常规长度（≤ <see cref="PruneRowCap"/>）走原版 pruning → 与原版逐值相等、隐身、不 desync。
/// - 仅超长地图跳过，且阈值基于 <c>grid.GetLength(1)</c>（= _mapLength，已被 ConfigSync 同步），
///   host/client 同 mod 同 config 必然同步跳过 ⇒ 地图一致。
/// - 硬前提（与 MapLengthPatch 一致）：调过长度后两端必须同版本 mod。
/// </summary>
[HarmonyPatch(typeof(MapPathPruning), nameof(MapPathPruning.PruneAndRepair))]
public static class SkipPruningForLongMapsPatch
{
    /// <summary>
    /// 跳过 pruning 的行数阈值。取略高于原版最长地图（原版 ~15 行）。超过则跳过指数级 FindAllPaths。
    /// 经验值，可按实测进图耗时微调。
    /// </summary>
    public const int PruneRowCap = 18;

    [HarmonyPrefix]
    public static bool Prefix(MapPoint?[,] grid)
    {
        if (!PatchScope.IsEnabled) return true; // mod 关闭 → 原样 pruning
        if (grid == null) return true;

        int rows = grid.GetLength(1); // = _mapLength
        if (rows > PruneRowCap) return false; // ★ 超长 → 跳过 PruneAndRepair（含内部 RepairPrunedPointTypes）
        return true; // 常规长度 → 原版 pruning，逐值隐身
    }
}