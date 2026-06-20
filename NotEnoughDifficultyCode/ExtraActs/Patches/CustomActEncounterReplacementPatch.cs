using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     替换自定义 act 的战斗内容池，用加权混合池：
///     - Act4: 重新填充 eliteEncounters，从 (act1.elites, act2.elites, act3.elites) 按 enc 权重混合
///     (act4 没有 monster 节点，normalEncounters 不会被消费，所以不用动)
///     - Act5: 重新填充 normalEncounters + eliteEncounters，
///     都从 (act1.bosses, act2.bosses, act3.bosses) 按 boss 权重混合
///     （act5 中部所有战斗节点都打 boss 内容；顶端 BossMapPoint 由 _rooms.Boss 单独控制，不受影响）
///     Act5 的 boss 混合池经过 ExtraActsConfig.ApplyBossPoolFilters 过滤，应用如
///     ExcludeDoormakerFromBossPool 等开关。Act4 的 elite 池不过滤——开关只影响 boss 池。
///     实现：patch ActModel.GenerateRooms postfix，反射访问 protected 字段 _rooms 拿到 RoomSet，
///     用 EncounterListBuilder.FillWithWeightedPools 填充 list。
///     多人同步：rng 序列各端一致 → 替换结果一致。
///     ## Harmony ordering
///     [HarmonyAfter("BaseLib")]：BaseLib 的 ActModelGenerateRoomsPatch 也 patch ActModel.GenerateRooms
///     postfix，处理 Ancient 注入。它对 normalEncounters/eliteEncounters 不动，跟我们正交。
///     显式声明 After 是为了 robustness——万一 BaseLib 将来也改 encounters，我们的逻辑作为更晚的
///     patch 来"修正"它的输出。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
[HarmonyAfter("BaseLib")]
public static class CustomActEncounterReplacementPatch
{
    private static readonly FieldInfo? RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    [HarmonyPriority(Priority.Low)]
    [HarmonyPostfix]
    public static void ReplaceEncounters(ActModel __instance, Rng rng)
    {
        if (!PatchScope.IsEnabled) return;
        if (__instance is not Act4Model && __instance is not Act5Model) return;

        PatchScope.Run(nameof(CustomActEncounterReplacementPatch), () =>
        {
            var rooms = RoomsField?.GetValue(__instance) as RoomSet;
            if (rooms == null)
            {
                MainFile.Logger.Error(
                    $"Failed to access _rooms on {__instance.Id.Entry}; encounter replacement skipped");
                return;
            }

            if (__instance is Act4Model)
            {
                // Act4: elite 内容用加权混合的 1+2+3 elite encounters 填充
                // 不过滤 boss 池开关——这里用的是 elite 池
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
                    $"Act4: refilled {targetCount} elite encounters " +
                    $"(weights {w.Act1}/{w.Act2}/{w.Act3}, distinct pool size {totalPoolSize})");

                // 相邻去重：让 act4 玩家走的连续 elite 战斗不重复。act4 只消费 eliteEncounters，
                // 单 list dedup 即可（无 normalEncounters 路径要考虑）。
                if (NotEnoughDifficultyConfig.AvoidAdjacentEncounterDuplicate)
                {
                    var dups = EncounterDeduplicator.DeduplicateAdjacent(rooms.eliteEncounters);
                    if (dups == 0)
                        MainFile.Logger.Info("Act4: elite list dedup OK (no adjacent duplicates)");
                    else
                        MainFile.Logger.Warn(
                            $"Act4: elite list dedup partial — {dups} adjacent duplicate(s) remain " +
                            "(pool size too small relative to list length)");
                }
            }
            else // Act5Model
            {
                // Act5: normal + elite 内容都用加权混合的 1+2+3 boss encounters 填充
                // 经过 ApplyBossPoolFilters 应用 ExcludeDoormakerFromBossPool 等 boss 池过滤开关
                var w = ExtraActsConfig.GetBossWeights(5);
                var pools = new List<(IReadOnlyList<EncounterModel>, double)>
                {
                    (ExtraActsConfig.ApplyBossPoolFilters(ModelDb.Act<Overgrowth>().AllBossEncounters), w.Act1),
                    (ExtraActsConfig.ApplyBossPoolFilters(ModelDb.Act<Hive>().AllBossEncounters), w.Act2),
                    (ExtraActsConfig.ApplyBossPoolFilters(ModelDb.Act<Glory>().AllBossEncounters), w.Act3)
                };

                var normalCount = rooms.normalEncounters.Count;
                EncounterListBuilder.FillWithWeightedPools(
                    rooms.normalEncounters, normalCount, pools, rng);

                var eliteCount = rooms.eliteEncounters.Count;
                EncounterListBuilder.FillWithWeightedPools(
                    rooms.eliteEncounters, eliteCount, pools, rng);

                var totalPoolSize = pools.Sum(p => p.Item1.Count);
                MainFile.Logger.Info(
                    $"Act5: replaced {normalCount} normal + {eliteCount} elite with boss content " +
                    $"(weights {w.Act1}/{w.Act2}/{w.Act3}, distinct pool size {totalPoolSize})");

                // 相邻去重：act5 中部节点会混合消费 normal+elite 两个 list（取决于节点类型），
                // 两个 list 都用同一个 boss 池填充，cross-list 重复风险高。
                // 合并 → 整体 dedup → 拆回，最大化 unique pattern。
                if (NotEnoughDifficultyConfig.AvoidAdjacentEncounterDuplicate)
                {
                    var dups = EncounterDeduplicator.DeduplicateMerged(
                        rooms.normalEncounters, rooms.eliteEncounters);
                    if (dups == 0)
                        MainFile.Logger.Info(
                            "Act5: normal+elite merged dedup OK (no adjacent duplicates in combined sequence)");
                    else
                        MainFile.Logger.Warn(
                            $"Act5: merged dedup partial — {dups} adjacent duplicate(s) in combined sequence " +
                            "(pool size too small; cross-list path duplicates may also occur)");
                }
            }
        });
    }
}