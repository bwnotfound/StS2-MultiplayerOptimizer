// ReSharper disable InconsistentNaming

using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// 让 Act5 中部"伪装 boss"战斗打完能正常拿到 <b>boss 级奖励</b>（全额金币 + boss 品质卡牌 + 药水 roll）。
///
/// ## 问题
///
/// base game <c>RewardsSet.WithRewardsFromRoom</c>（src/Core/Rewards/RewardsSet.cs:56）开头有个特判：
/// <code>
///   if (room.RoomType == RoomType.Boss
///       &amp;&amp; Player.RunState.CurrentActIndex >= Player.RunState.Acts.Count - 1)
///   {
///       return this;   // 直接返回，不生成任何奖励
///   }
/// </code>
///
/// base game 的意图：<b>最后一个 act 的 boss = 最终 boss，打完就通关，通关画面单独结算，
/// 所以不发常规战斗奖励</b>。
///
/// 但 Act5 中部"伪装 boss"节点同时满足两个条件：
///   - <c>room.RoomType == Boss</c>（CombatRoom.RoomType = Encounter.RoomType，中部 encounter 是 boss-content）
///   - <c>CurrentActIndex</c>（act5）<c>>= Acts.Count - 1</c>（在最后一个 act）
///
/// → 全部命中特判 → 中部 6 场 boss 强度的硬仗打完<b>一点奖励都没有</b>（金币/卡牌/药水全无）。
/// 这显然不是预期——玩家打 boss 必须给 boss 奖励。
///
/// ## 修复
///
/// patch <c>WithRewardsFromRoom</c>：检测到当前是 act5 中部 boss-content 节点（非真 boss）时，
/// 临时把 <c>CurrentActIndex</c> 改成一个 <c>&lt; Acts.Count - 1</c> 的合法值，让 line 56 的条件
/// 不成立 → 原方法正常往下走 → <c>GenerateRewardsFor</c> 的 <c>switch (room.RoomType)</c>
/// 命中 <c>case Boss</c> → 生成 boss 级奖励（<c>Encounter.MinGoldReward~MaxGoldReward</c> 全额金币
/// + boss 品质 <c>CardReward</c> + 药水 roll）。postfix 恢复。
///
/// 真 boss 节点（含 DoubleBoss 第二个 boss）<b>不动</b>——它们打完确实是通关，base game
/// 不发战斗奖励是正确的。
///
/// ⚠️ <b>关键</b>：临时改 <c>CurrentActIndex</c> 必须用
/// <see cref="RunStateActIndexWriter.WriteRaw"/> 绕过 property setter。base game 的
/// <c>CurrentActIndex</c> setter 会在值改变时清空 <c>_visitedMapCoords</c> / 把 <c>ActFloor</c>
/// 归零——走 setter 临时改值会清掉玩家的地图进度。详见 <see cref="RunStateActIndexWriter"/>。
///
/// <c>WithRewardsFromRoom</c> 是<b>同步方法</b>（返回 RewardsSet 而非 Task），prefix → 方法 →
/// postfix 之间无 await。且方法内部除 line 56 外不使用 <c>CurrentActIndex</c>
/// （<c>case Boss</c> 用的是 <c>Encounter.MinGoldReward</c> 等，与 act index 无关），
/// 所以临时改值只影响 line 56 的判断，安全。
///
/// postfix 带 <c>Exception __exception</c> 参数确保即使原方法抛异常也能恢复 CurrentActIndex。
/// </summary>
[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
public static class Act5MidBossRewardPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    public static void Prefix(RewardsSet __instance, AbstractRoom room, out int __state)
    {
        __state = -1; // sentinel：未改动 / 改动失败

        if (!PatchScope.IsEnabled) return;

        try
        {
            if (room is not CombatRoom) return;
            if (room.RoomType != RoomType.Boss) return; // 只有 boss-content 节点会命中 line 56 特判

            // 用 RewardsSet 自己的 Player.RunState（line 56 用的就是它），确保改对对象
            var runState = __instance?.Player?.RunState;
            if (runState == null) return;
            if (runState.Map == null) return;
            if (runState.Act is not Act5Model) return;

            var coord = runState.CurrentMapCoord;

            // 真 boss 节点（第一个真 boss）→ 不动：打完是通关，base game 不发奖励是对的
            if (coord == runState.Map.BossMapPoint.coord) return;
            // DoubleBoss 第二个 boss 节点（Ascension 10）→ 同样不动
            if (runState.Map.SecondBossMapPoint != null
                && coord == runState.Map.SecondBossMapPoint.coord) return;

            // 到这里：act5 中部 boss-content 节点。临时把 CurrentActIndex 改到
            // "不是最后一个 act" 让 line 56 不早返。
            int cur = runState.CurrentActIndex;
            int target = runState.Acts.Count - 2; // 倒数第二个 act 的 index，必 < Acts.Count-1
            if (target < 0) return; // act 数量异常，放弃
            if (cur == target) return; // 已经是目标值（理论上不会），无需改

            if (RunStateActIndexWriter.WriteRaw(runState, target))
            {
                __state = cur; // 只有写成功才记录原值
                MainFile.Logger.Info(
                    $"Act5 mid-boss-content reward: temporarily mapping CurrentActIndex " +
                    $"{cur} -> {target} so RewardsSet generates full boss-tier rewards " +
                    $"(via raw field write, bypasses setter side effects)");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Act5MidBossRewardPatch.Prefix failed: {ex}");
        }
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    public static void Postfix(RewardsSet __instance, int __state, Exception __exception)
    {
        // 带 __exception → 即使原方法抛异常 postfix 也跑，保证 CurrentActIndex 恢复
        if (__state < 0) return;

        try
        {
            var runState = __instance?.Player?.RunState;
            if (runState == null) return;
            RunStateActIndexWriter.WriteRaw(runState, __state);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Act5MidBossRewardPatch.Postfix failed: {ex}");
        }
    }
}