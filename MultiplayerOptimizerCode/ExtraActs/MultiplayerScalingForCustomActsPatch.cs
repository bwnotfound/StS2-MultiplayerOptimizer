// ReSharper disable InconsistentNaming
// __result 是 Harmony 的特殊参数名约定（用来读写原方法返回值），不能改成 result——
// 否则 PatchAll 抛 "Parameter 'result' not found"，整个 mod 加载失败。

using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Rooms;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 让 base game 的 MultiplayerScalingModel 支持 act4/5（actIndex 3/4）。
///
/// 背景：
///   MultiplayerScalingModel.GetMultiplayerScaling 是 base game 的"每多一个玩家增加多少 HP"系数：
///     - act1 (idx 0): 1.1×
///     - act2 (idx 1): 1.2×
///     - act3 (idx 2): 1.2×，boss 1.3×
///     - 其他: throw ArgumentOutOfRangeException
///
///   我们加的 act4 (idx 3) / act5 (idx 4) 走 default 分支抛异常，进入战斗时 crash。
///
/// 修复：act4/5 沿用 act3 同样的系数（boss 1.3×，其他 1.2×）。
/// 这一层 base game 缩放跟我们 mod 自己的 HP 倍率（DifficultyMultiplierContext）是两套独立机制：
///   - base 缩放：每多 1 玩家加多少 HP（按玩家数 scale）
///   - mod 倍率：act4/5 怪整体加多少 HP（按 act 难度 scale）
/// 两者乘起来生效。
/// </summary>
[HarmonyPatch(typeof(MultiplayerScalingModel), nameof(MultiplayerScalingModel.GetMultiplayerScaling))]
public static class MultiplayerScalingForCustomActsPatch
{
    [HarmonyPrefix]
    public static bool Prefix(EncounterModel? encounter, int actIndex, ref decimal __result)
    {
        // act1/2/3 让原方法处理
        if (actIndex <= 2) return true;

        // act4/5 沿用 act3 的系数：boss 1.3×，其他 1.2×
        __result = (encounter != null && encounter.RoomType == RoomType.Boss) ? 1.3m : 1.2m;
        return false; // skip 原方法
    }
}