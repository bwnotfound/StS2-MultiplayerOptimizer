// ReSharper disable InconsistentNaming
// 文件中没有用 __instance/__result，但保险起见保留。

using HarmonyLib;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 抑制 act4/5 boss 击败时 ObtainCharUnlockEpoch 打的 Log.Error。
///
/// 背景：
///   ProgressSaveManager.ObtainCharUnlockEpoch(localPlayer, act) 在打 boss 后调用，
///   原版逻辑给 act 0/1/2（即第 1/2/3 层 boss）发对应的 EPOCH 解锁成就。act >= 3 时：
///     case 3: Log.Error("Act 4 is not yet implemented");  // 不抛
///     default: Log.Error("Unsupported Act: ...");          // 不抛
///   然后 epochModel 仍是 null 走入下一段 → 又 Log.Error("EpochModel was not found :(")
///   一次 act4 boss 击败会刷两条 error log。
///
/// 不会 crash 也不影响游戏流程，但日志难看。我们 mod 自己也没定义 act4/5 对应的 EpochModel
/// （这是成就/解锁系统资源，需要 .tres 等），所以**不能假装发 epoch**——发了实际 EpochModel
/// 是 null，base game 后续 try 触发又会失败。
///
/// 最干净的处理：act >= 3 直接 prefix return false 跳过整个方法。不发 epoch（act4/5 boss
/// 没有对应解锁成就，本来也没东西可发），不打 error log。
/// </summary>
[HarmonyPatch(typeof(ProgressSaveManager), "ObtainCharUnlockEpoch")]
public static class SuppressMissingEpochErrorPatch
{
    [HarmonyPrefix]
    public static bool Prefix(int act)
    {
        // act 0/1/2 走原方法发 EPOCH；act 3+ 没有对应 epoch，跳过。
        return act < 3;
    }
}