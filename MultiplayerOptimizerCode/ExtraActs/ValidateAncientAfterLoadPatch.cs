// ReSharper disable InconsistentNaming
// 上一行抑制 IDE 关于 __instance 的命名规则警告：
// __instance 是 Harmony 的特殊参数名约定，用来注入 patched 实例 —— 必须严格 __instance，
// 改成其他名字（比如 instance）会导致 PatchAll 抛
//   "Parameter 'instance' not found in method ..."
// 整个 mod 加载失败。

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 修复旧版 mod 存档（act4/5 的 _rooms.Ancient 为 null）。
///
/// 问题来源：
///   - 之前版本 Act4/5Model.GetUnlockedAncients 返回 Array.Empty
///   - run 启动时 ActModel.GenerateRooms line 278:
///       _rooms.Ancient = rng.NextItem(GetUnlockedAncients(state).Concat(_sharedAncientSubset ?? new List&lt;&gt;()));
///     空池 → rng.NextItem 返回 default(T) = null → _rooms._ancient 留空
///   - 存档保存 _rooms.Ancient.Id —— null _ancient 写出 AncientId = null
///   - 读存档 RoomSet.FromSave 把 _ancient 设回 null
///
/// 进 act 时 NMapScreen.SetMap → NAncientMapPoint._Ready 读 act.Ancient → 抛
///   "RoomSet.Ancient not set! You must call GenerateRooms"
///
/// 我们已经修了 GetUnlockedAncients（让 act4/5 复用 Glory），但**只对新开 run 生效**——
/// 旧存档里 _rooms._ancient 已经是 null 写到磁盘了。这个 patch 在 saved run 加载时检查并补抽。
///
/// 触发链路：RunManager.InitializeSavedRun → 遍历 acts 调 act.ValidateRoomsAfterLoad → 我们 postfix 补抽。
/// 已经有 Ancient 的 act 不动；只补 _ancient == null 的（即出问题的 custom act）。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.ValidateRoomsAfterLoad))]
public static class ValidateAncientAfterLoadPatch
{
    private static readonly FieldInfo RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    private static readonly FieldInfo SharedAncientSubsetField =
        AccessTools.Field(typeof(ActModel), "_sharedAncientSubset");

    [HarmonyPostfix]
    public static void EnsureAncientFilled(ActModel __instance, Rng rng)
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
                $"[ExtraActs] Cannot patch missing ancient for {__instance.Id.Entry}: " +
                "unlocked ancients pool is empty (this act has no fallback)");
            return;
        }

        var picked = rng.NextItem(pool);
        if (picked == null) return;

        rooms.Ancient = picked;
        MainFile.Logger.Info(
            $"[ExtraActs] Patched missing ancient for {__instance.Id.Entry} on saved run load: " +
            $"{picked.Id.Entry} (likely created by older mod version)");
    }
}