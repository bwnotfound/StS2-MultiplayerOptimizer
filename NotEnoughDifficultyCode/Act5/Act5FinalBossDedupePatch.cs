using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     软性约束：第 5 层最终 boss ≠ 倒数第二关的 boss（避免连续打两次同一个 boss）。
///     实现思路：
///     - Act5 中部"伪装 boss"内容是从 normalEncounters/eliteEncounters 抽的，预生成时无法预测
///     玩家走哪条路径到 final boss、上一关 boss 是谁，所以<b>只能在玩家点击 final boss 节点时</b>
///     做实时检查
///     - 此时 state.MapPointHistory[CurrentActIndex] 包含了玩家在 act5 内访问过的所有节点；
///     倒数第一项的 Rooms 里能找到上一关战斗的 encounter Id
///     - 如果上一关 encounter Id 跟当前 act.BossEncounter.Id 相同，重抽 final boss 避开它
///     - 重抽时也避开前 4 层 boss（保持需求 5.3 第一条约束）
///     时机：patch RunManager.EnterMapCoord prefix（玩家点击节点进入房间的入口）
///     多人同步：state.Rng.UpFront 在所有客户端是同一序列，重抽确定性。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapCoord))]
public static class Act5FinalBossDedupePatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPrefix]
    public static void RerollFinalBossIfDuplicate(MapCoord coord)
    {
        if (!PatchScope.IsEnabled) return;
        if (!ExtraActsConfig.ShouldAvoidAct5FinalBossEqualPenultimate) return;

        PatchScope.Run(nameof(Act5FinalBossDedupePatch), () =>
        {
            if (!PatchScope.TryEnter(out var ctx)) return;
            if (!ctx.IsAct5) return;

            var state = ctx.State;
            if (state.Map == null) return;

            // 即将进入的 coord 必须是 final boss 节点
            var bossCoord = state.Map.BossMapPoint.coord;
            if (coord.col != bossCoord.col || coord.row != bossCoord.row) return;

            // 拿到 act5 的访问历史
            if (state.MapPointHistory.Count <= state.CurrentActIndex) return;
            var act5History = state.MapPointHistory[state.CurrentActIndex];
            if (act5History.Count == 0) return;

            // 倒数第一项就是"上一个访问过的节点"，找它最后的战斗 room（Monster/Boss）
            var lastEntry = act5History[act5History.Count - 1];
            var lastCombat = lastEntry.Rooms.LastOrDefault(r =>
                r.RoomType == RoomType.Monster || r.RoomType == RoomType.Boss);
            if (lastCombat?.ModelId == null) return;

            var lastEncounterEntry = lastCombat.ModelId.Entry;
            var currentBossEntry = state.Act.BossEncounter.Id.Entry;

            if (!string.Equals(lastEncounterEntry, currentBossEntry)) return;

            // 撞了——重抽 final boss，避开 (上一关 + 前 4 层 boss)
            var avoidIds = new HashSet<string> { lastEncounterEntry };
            for (var i = 0; i < state.CurrentActIndex; i++)
                avoidIds.Add(state.Acts[i].BossEncounter.Id.Entry);

            var alternatives = state.Act.AllBossEncounters
                .Where(b => !avoidIds.Contains(b.Id.Entry))
                .ToList();

            if (alternatives.Count == 0)
            {
                MainFile.Logger.Warn(
                    $"Cannot reroll Act5 final boss '{currentBossEntry}': " +
                    $"no alternative not in [{string.Join(", ", avoidIds)}]");
                return;
            }

            var picked = state.Rng.UpFront.NextItem(alternatives);
            state.Act.SetBossEncounter(picked!);
            MainFile.Logger.Info(
                $"Act5 final boss re-rolled: '{currentBossEntry}' -> '{picked!.Id.Entry}' " +
                $"(penultimate room had encounter '{lastEncounterEntry}')");
        });
    }
}