// ReSharper disable InconsistentNaming

using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// 修复 act5 中部 boss-content 战斗后 visited 计数走错分支 →
/// 玩家连续节点都遇到同一个 boss 的 bug。
///
/// ## 问题来源
///
/// base game 的 <c>ActModel.MarkRoomVisited(RoomType)</c> 接收 room 的 RoomType 后路由到
/// <c>RoomSet.MarkVisited(RoomType)</c>：
/// <code>
///     Monster  → normalEncountersVisited++
///     Elite    → eliteEncountersVisited++
///     Boss     → bossEncountersVisited++   ← 不影响 normalEncountersVisited
/// </code>
///
/// 而 <c>RoomSet.NextNormalEncounter</c> 是
/// <c>normalEncounters[normalEncountersVisited % Count]</c>。
///
/// Act5 中部所有节点 PointType=Monster（由 <see cref="MapPointTypeFixupPatch"/> 修正），但
/// encounter 是 boss-content（<see cref="CustomActEncounterReplacementPatch"/> 替换）。所以：
///   - <c>RunManager</c> 创建 CombatRoom 时调
///     <c>State.Act.PullNextEncounter(RoomType.Monster)</c> →
///     取 normalEncounters[0]——第一次 OK
///   - 战斗胜利后 <c>State.Act.MarkRoomVisited(room.RoomType)</c>，但 <c>room.RoomType =
///     Encounter.RoomType = Boss</c>（CombatRoom.RoomType 来自 Encounter）→ 走 Boss 分支 →
///     <b>normalEncountersVisited 没递增</b>
///   - 下一次取 encounter 仍是 normalEncounters[0]——同一个 boss
///
/// 结果：act5 中部连续 6 个 monster 节点全是同一个 boss。
///
/// ## 修复
///
/// patch <c>ActModel.MarkRoomVisited</c> prefix：当当前 act 是 Act5 且<b>不在真 boss 节点</b>时，
/// 把传入的 <c>roomType</c> 从 <c>Boss</c> 改成 <c>Monster</c>，让 visited 计数走 normal 分支。
///
/// 真 boss 节点判断（含 DoubleBoss 第二个 boss）：
///   - <c>CurrentMapCoord == BossMapPoint.coord</c>（第一个真 boss）
///   - <c>CurrentMapCoord == SecondBossMapPoint.coord</c>（DoubleBoss 第二个 boss，进阶 10）
///
/// ## 为什么不在 <c>CombatRoom.RoomType</c> getter 上做
///
/// CombatRoom.RoomType 是<b>高频热路径</b>——UI 更新、BGM 切换、reward 等级、multiplayer scaling
/// 等几十处调用都依赖它。在那里改"act5 中部时返回 Monster"会副作用一大堆：
///   - 多人 boss 缩放 1.3× → 1.2×（怪物变弱）
///   - boss BGM 不播 → 沉闷
///   - boss 奖励等级 → monster 奖励等级
///   - 等等
///
/// patch MarkRoomVisited 是<b>只在战斗胜利后调一次</b>的低频点，影响极小：
///   - ✓ visited 计数正确递增 → encounter 不重复
///   - ✓ Boss BGM 仍触发
///   - ✓ Boss reward 等级保持
///   - ✓ Boss UI 主题保持
///   - ✓ Boss multiplayer scaling 1.3× 保持
///
/// ## 跟 Act5MidBossAdvanceFlowPatch 的协作
///
/// AdvanceFlowPatch 处理"中部 boss-content 胜利后的下一步"（走奖励翻页流程而不是触发通关）。
/// 本 patch 处理"中部 boss-content 胜利后的 visited 计数"。两者协同——前者解决"打完不结束"，
/// 后者解决"下一次抽同一 boss"。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.MarkRoomVisited))]
public static class Act5MidBossMarkVisitedPatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPrefix]
    public static void Prefix(ActModel __instance, ref RoomType roomType)
    {
        if (!PatchScope.IsEnabled) return;
        if (roomType != RoomType.Boss) return;
        if (__instance is not Act5Model) return;

        try
        {
            var rm = RunManager.Instance;
            if (rm == null) return;
            var state = RunStateAccessor.GetState(rm);
            if (state?.Map == null) return;

            var coord = state.CurrentMapCoord;

            // 真 boss 节点（第一个真 boss）→ 放行原方法用 Boss 分支
            if (coord == state.Map.BossMapPoint.coord) return;

            // DoubleBoss 第二个 boss 节点（Ascension 10）→ 也放行
            if (state.Map.SecondBossMapPoint != null
                && coord == state.Map.SecondBossMapPoint.coord) return;

            // 到这里：act5 + Boss roomType + 非真 boss 节点 = 中部"伪装 boss"战斗
            // 把 visited 计数路由到 normal 分支，让下一次 PullNextEncounter 取到不同 boss
            roomType = RoomType.Monster;
            MainFile.Logger.Info(
                "Act5 mid-boss-content victory: rerouting MarkRoomVisited(Boss -> Monster) " +
                "so normalEncountersVisited advances and the next node gets a different boss");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Act5MidBossMarkVisitedPatch failed: {ex}");
        }
    }
}