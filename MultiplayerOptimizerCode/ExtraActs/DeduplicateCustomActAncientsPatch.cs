using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 让 act4/5 的 ancient（起始节点对应的事件）跟前面 act 不重复。
///
/// 背景：
///   - 每个 act 的 map 起始节点 PointType = MapPointType.Ancient（StandardActMap.cs:285），
///     UI 渲染为 NAncientMapPoint，玩家点击会触发 act.Ancient 这个事件，给玩家选择奖励
///   - act4/5 复用 Glory 的 AllAncients (Nonupeipe / Tanx / Vakuu)，再加上各自从 SharedAncients
///     分到的 _sharedAncientSubset，由 ActModel.GenerateRooms 用 rng.NextItem 抽一个出来
///   - 不去重的话，act4/5 经常抽到跟 act3 / 互相 / 前面 act 重复的 ancient 事件，
///     体验上"每层第一关都长一样"
///
/// 实现：patch RunManager.GenerateRooms postfix，在所有 act.GenerateRooms() 完成后跑。
/// 跟 DeduplicateCustomActBossesPatch 同样模式，遍历 acts 累积已用 ancient id，
/// 给 act4/5 重抽避开前面用过的（包括 act1/2/3 + 前一个 custom act）。
///
/// 用 state.Rng.UpFront 保证多人确定性。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
public static class DeduplicateCustomActAncientsPatch
{
    private static readonly FieldInfo RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    private static readonly FieldInfo SharedAncientSubsetField =
        AccessTools.Field(typeof(ActModel), "_sharedAncientSubset");

    [HarmonyPostfix]
    public static void DeduplicateAncients(RunManager __instance)
    {
        var state = RunStateAccessor.GetState(__instance);
        if (state == null) return;

        var usedAncientIds = new HashSet<string>();

        for (int i = 0; i < state.Acts.Count; i++)
        {
            var act = state.Acts[i];
            var rooms = RoomsField?.GetValue(act) as RoomSet;
            if (rooms == null || !rooms.HasAncient) continue;

            string currentId = rooms.Ancient.Id.Entry;

            // 只对 custom act 重抽；act1-3 / 其他 base act 保持原结果（但仍把它们的 ancient 加入 used set）
            bool isCustomAct = act is Act4Model || act is Act5Model;

            if (isCustomAct && usedAncientIds.Contains(currentId))
            {
                // 构造可选池：act 自己的 GetUnlockedAncients + sharedSubset
                var sharedSubset = SharedAncientSubsetField?.GetValue(act) as List<AncientEventModel>
                                   ?? new List<AncientEventModel>();

                var available = act.GetUnlockedAncients(state.UnlockState)
                    .Concat(sharedSubset)
                    .Where(a => !usedAncientIds.Contains(a.Id.Entry))
                    .Distinct()
                    .ToList();

                if (available.Count > 0)
                {
                    var picked = state.Rng.UpFront.NextItem(available);
                    rooms.Ancient = picked!;
                    MainFile.Logger.Info(
                        $"[ExtraActs] {act.Id.Entry} ancient re-rolled to avoid duplicate: " +
                        $"'{currentId}' -> '{picked!.Id.Entry}' (avoided: [{string.Join(", ", usedAncientIds)}])");
                    currentId = picked.Id.Entry;
                }
                else
                {
                    MainFile.Logger.Warn(
                        $"[ExtraActs] {act.Id.Entry} cannot reroll ancient: " +
                        $"no alternatives in pool (avoided: [{string.Join(", ", usedAncientIds)}])");
                }
            }

            usedAncientIds.Add(currentId);
        }
    }
}