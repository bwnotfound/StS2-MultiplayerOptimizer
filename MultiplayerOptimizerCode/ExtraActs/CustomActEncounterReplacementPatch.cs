using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 替换自定义 act 的战斗内容池，用加权混合池：
///
///   - Act4: 重新填充 eliteEncounters，从 (act1.elites, act2.elites, act3.elites) 按 enc 权重混合
///           (act4 没有 monster 节点，normalEncounters 不会被消费，所以不用动)
///
///   - Act5: 重新填充 normalEncounters + eliteEncounters，
///           都从 (act1.bosses, act2.bosses, act3.bosses) 按 boss 权重混合
///           （act5 中部所有战斗节点都打 boss 内容；顶端 BossMapPoint 由 _rooms.Boss 单独控制，不受影响）
///
/// 实现：patch ActModel.GenerateRooms postfix，反射访问 protected 字段 _rooms 拿到 RoomSet，
///       用 EncounterListBuilder.FillWithWeightedPools 填充 list。
///
/// 多人同步：rng 序列各端一致 → 替换结果一致。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
public static class CustomActEncounterReplacementPatch
{
    private static readonly FieldInfo RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    [HarmonyPostfix]
    public static void ReplaceEncounters(ActModel __instance, Rng rng)
    {
        if (__instance is not Act4Model && __instance is not Act5Model)
            return;

        var rooms = RoomsField?.GetValue(__instance) as RoomSet;
        if (rooms == null)
        {
            MainFile.Logger.Error(
                $"[ExtraActs] Failed to access _rooms on {__instance.Id.Entry}; encounter replacement skipped");
            return;
        }

        if (__instance is Act4Model)
        {
            // Act4: elite 内容用加权混合的 1+2+3 elite encounters 填充
            var w = ExtraActsConfig.GetEncounterWeights(4);
            var pools = new List<(IReadOnlyList<EncounterModel>, double)>
            {
                (ModelDb.Act<Overgrowth>().AllEliteEncounters.ToList(), w.Act1),
                (ModelDb.Act<Hive>().AllEliteEncounters.ToList(), w.Act2),
                (ModelDb.Act<Glory>().AllEliteEncounters.ToList(), w.Act3)
            };
            var targetCount = rooms.eliteEncounters.Count;
            EncounterListBuilder.FillWithWeightedPools(
                rooms.eliteEncounters, targetCount, pools, rng);

            var totalPoolSize = pools.Sum(p => p.Item1.Count);
            MainFile.Logger.Info(
                $"[ExtraActs] Act4: refilled {targetCount} elite encounters " +
                $"(weights {w.Act1}/{w.Act2}/{w.Act3}, distinct pool size {totalPoolSize})");
        }
        else if (__instance is Act5Model)
        {
            // Act5: normal + elite 内容都用加权混合的 1+2+3 boss encounters 填充
            var w = ExtraActsConfig.GetBossWeights(5);
            var pools = new List<(IReadOnlyList<EncounterModel>, double)>
            {
                (ModelDb.Act<Overgrowth>().AllBossEncounters.ToList(), w.Act1),
                (ModelDb.Act<Hive>().AllBossEncounters.ToList(), w.Act2),
                (ModelDb.Act<Glory>().AllBossEncounters.ToList(), w.Act3)
            };

            var normalCount = rooms.normalEncounters.Count;
            EncounterListBuilder.FillWithWeightedPools(
                rooms.normalEncounters, normalCount, pools, rng);

            var eliteCount = rooms.eliteEncounters.Count;
            EncounterListBuilder.FillWithWeightedPools(
                rooms.eliteEncounters, eliteCount, pools, rng);

            var totalPoolSize = pools.Sum(p => p.Item1.Count);
            MainFile.Logger.Info(
                $"[ExtraActs] Act5: replaced {normalCount} normal + {eliteCount} elite with boss content " +
                $"(weights {w.Act1}/{w.Act2}/{w.Act3}, distinct pool size {totalPoolSize})");
        }
    }
}