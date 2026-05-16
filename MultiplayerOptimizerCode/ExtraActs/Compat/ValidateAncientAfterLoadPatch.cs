// ReSharper disable InconsistentNaming
// __instance 是 Harmony 强制约定，改了会让 PatchAll 抛 "Parameter not found"。
//
// =============================================================================
// COMPAT-PRELAUNCH: 兼容重构前版本 mod 创建的旧存档中 _rooms.Ancient 为 null 的情况。
//   兼容原因: 重构前版本 mod 的 Act4Model.GetUnlockedAncients 返回 Array.Empty，
//             导致 _rooms.Ancient = null 被写入存档；旧存档加载时 NAncientMapPoint
//             会读 act.Ancient 抛 NRE。当前版本已修复 GetUnlockedAncients 返回 Glory ancients，
//             新开 run 不会再产生此问题。
//   删除条件: 那个"正在玩的旧存档"通关或弃坑后。
//   删除方式: 直接删除本文件（无其他位置依赖）。
//
// 详细约定见 SaveCompat/README.md 和 REFACTORING_PLAN.md §7A。
// =============================================================================

using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
///     修复旧版 mod 存档（act4/5 的 _rooms.Ancient 为 null）。
///     问题来源：
///     - 之前版本 Act4/5Model.GetUnlockedAncients 返回 Array.Empty
///     - run 启动时 ActModel.GenerateRooms line 278:
///     _rooms.Ancient = rng.NextItem(GetUnlockedAncients(state).Concat(_sharedAncientSubset ?? new List&lt;&gt;()));
///     空池 → rng.NextItem 返回 default(T) = null → _rooms._ancient 留空
///     - 存档保存 _rooms.Ancient.Id —— null _ancient 写出 AncientId = null
///     - 读存档 RoomSet.FromSave 把 _ancient 设回 null
///     进 act 时 NMapScreen.SetMap → NAncientMapPoint._Ready 读 act.Ancient → 抛
///     "RoomSet.Ancient not set! You must call GenerateRooms"
///     我们已经修了 GetUnlockedAncients（让 act4/5 复用 Glory），但<b>只对新开 run 生效</b>——
///     旧存档里 _rooms._ancient 已经是 null 写到磁盘了。这个 patch 在 saved run 加载时检查并补抽。
///     触发链路：RunManager.InitializeSavedRun → 遍历 acts 调 act.ValidateRoomsAfterLoad → 我们 postfix 补抽。
///     已经有 Ancient 的 act 不动；只补 _ancient == null 的（即出问题的 custom act）。
///     ## 不 honor PatchScope.IsEnabled
///     这是<b>旧存档修复</b>逻辑。如果 honor Enabled 然后用户把 Enabled 设为 false 去加载有问题的
///     旧存档，会直接 crash。不让 Enabled 控制这个 patch——它对正常存档是 no-op，没有副作用。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.ValidateRoomsAfterLoad))]
public static class ValidateAncientAfterLoadPatch
{
    private static readonly FieldInfo? RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    private static readonly FieldInfo? SharedAncientSubsetField =
        AccessTools.Field(typeof(ActModel), "_sharedAncientSubset");

    [HarmonyPriority(Priority.Low)]
    [HarmonyPostfix]
    public static void EnsureAncientFilled(ActModel __instance, Rng rng)
    {
        try
        {
            if (RoomsField == null) return;
            if (RoomsField.GetValue(__instance) is not RoomSet rooms) return;
            if (rooms.HasAncient) return; // 已有 Ancient，不动

            // 没有 Ancient（旧存档遗留），从 act 自己的 unlocked 池补抽
            // RunManager.State 是 private，用反射访问
            var rm = RunManager.Instance;
            if (rm == null) return;
            var state = RunStateAccessor.GetState(rm);
            if (state == null) return;

            var sharedSubset = SharedAncientSubsetField?.GetValue(__instance) as List<AncientEventModel>
                               ?? new List<AncientEventModel>();

            var pool = __instance.GetUnlockedAncients(state.UnlockState)
                .Concat(sharedSubset)
                .Distinct()
                .ToList();

            if (pool.Count == 0)
            {
                MainFile.Logger.Warn(
                    $"Cannot patch missing ancient for {__instance.Id.Entry}: " +
                    "unlocked ancients pool is empty (this act has no fallback)");
                return;
            }

            var picked = rng.NextItem(pool);
            if (picked == null) return;

            rooms.Ancient = picked;
            MainFile.Logger.Info(
                $"Patched missing ancient for {__instance.Id.Entry} on saved run load: " +
                $"{picked.Id.Entry} (likely created by older mod version)");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"ValidateAncientAfterLoadPatch failed: {ex}");
        }
    }
}