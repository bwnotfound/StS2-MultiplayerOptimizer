using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 让自定义 act 的（顶端）boss 不和前面已分配的 boss 重复。
///
/// 当前覆盖范围：
///   - Act4 顶端 boss ≠ Act1/Act2/Act3 boss
///   - Act5 顶端 boss ≠ Act1/Act2/Act3/Act4 boss
///
/// 暂未覆盖（后续 step）：
///   - Act5 中间节点的"伪装 boss"（normalEncounters/eliteEncounters 已替换为 boss 内容）
///     去重 —— 中间内容由 CustomActEncounterReplacementPatch 单独管理，目前允许重复
///   - Act5 final boss ≠ 中间最后一次出现的伪装 boss —— 用户说"不强制要求"
///   - DoubleBoss ascension 下 SecondBoss 去重 —— 边界情况
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
public static class DeduplicateCustomActBossesPatch
{
    [HarmonyPostfix]
    public static void DeduplicateBosses(RunManager __instance)
    {
        var state = RunStateAccessor.GetState(__instance);
        if (state == null) return;

        var rng = state.Rng.UpFront;
        var usedBossIds = new HashSet<string>();

        foreach (var act in state.Acts)
        {
            var isCustomExtra = act is Act4Model || act is Act5Model;

            if (isCustomExtra)
            {
                var available = act.AllBossEncounters
                    .Where(b => !usedBossIds.Contains(b.Id.Entry))
                    .ToList();

                if (available.Count > 0)
                {
                    var picked = rng.NextItem(available);
                    var oldBossId = act.BossEncounter.Id.Entry;
                    act.SetBossEncounter(picked!);
                    MainFile.Logger.Info(
                        $"[ExtraActs] {act.Id.Entry} top-boss: '{oldBossId}' -> '{picked!.Id.Entry}' " +
                        $"(avoided: [{string.Join(", ", usedBossIds)}])");
                }
                else
                {
                    MainFile.Logger.Warn(
                        $"[ExtraActs] No unused boss for {act.Id.Entry}; " +
                        $"keeping default '{act.BossEncounter.Id.Entry}' (will repeat)");
                }
            }

            usedBossIds.Add(act.BossEncounter.Id.Entry);
        }
    }
}