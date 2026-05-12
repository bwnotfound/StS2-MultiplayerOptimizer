using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 让自定义 act 的（顶端）boss 不和前面已分配的 boss 重复，并应用 boss 池过滤开关
/// （如 ExcludeDoormakerFromBossPool）。
///
/// ## 为什么 boss 池过滤要放在这里
/// ActModel.AllEncounters 是 lazy 缓存（_allEncounters ?? 生成 + 缓存）—— 也就是说
/// Act4Model.GenerateAllEncounters / Act5Model.GenerateAllEncounters **只在 mod 加载时
/// 跑一次**，结果存进缓存后不再重新生成。如果把过滤放在 GenerateAllEncounters 里，
/// 默认状态（开关 = false）下缓存了完整池子，用户运行时打开开关后**不生效**。
///
/// 解决：让 GenerateAllEncounters 始终返回完整池子（包含所有 boss 包括 Doormaker），
/// 在这里——每次新 run 都跑——根据当前开关状态过滤。
///
/// ## 当前覆盖范围
///   - Act4 顶端 boss ≠ Act1/Act2/Act3 boss + 受 ApplyBossPoolFilters 控制
///   - Act5 顶端 boss ≠ Act1/Act2/Act3/Act4 boss + 受 ApplyBossPoolFilters 控制
///
/// ## 暂未覆盖
///   - Act5 中间节点的"伪装 boss"（normalEncounters/eliteEncounters 已替换为 boss 内容）
///     去重 —— 中间内容由 CustomActEncounterReplacementPatch 单独管理，目前允许重复
///     （那里也已经 runtime 调 ApplyBossPoolFilters，所以 Doormaker 过滤是生效的）
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
                // 先应用 boss 池过滤开关（如 ExcludeDoormakerFromBossPool），再做 dedup 过滤
                var filtered = ExtraActsConfig.ApplyBossPoolFilters(act.AllBossEncounters);
                var available = filtered
                    .Where(b => !usedBossIds.Contains(b.Id.Entry))
                    .ToList();

                if (available.Count > 0)
                {
                    var picked = rng.NextItem(available);
                    var oldBossId = act.BossEncounter.Id.Entry;
                    act.SetBossEncounter(picked!);
                    MainFile.Logger.Info(
                        $"[ExtraActs] {act.Id.Entry} top-boss: '{oldBossId}' -> '{picked!.Id.Entry}' " +
                        $"(avoided: [{string.Join(", ", usedBossIds)}], pool size after filters: {filtered.Count})");
                }
                else
                {
                    MainFile.Logger.Warn(
                        $"[ExtraActs] No unused boss for {act.Id.Entry} after filters; " +
                        $"keeping default '{act.BossEncounter.Id.Entry}' " +
                        "(may include filtered-out boss like Doormaker — pool exhausted)");
                }
            }

            usedBossIds.Add(act.BossEncounter.Id.Entry);
        }
    }
}