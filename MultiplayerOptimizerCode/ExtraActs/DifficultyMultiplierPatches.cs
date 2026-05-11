using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Models;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// Encounter → 源 act 反查（lazy 缓存）。
/// </summary>
internal static class SourceActResolver
{
    private static Dictionary<string, int>? _cache;

    public static int? GetSourceActIndex(EncounterModel? encounter)
    {
        if (encounter == null) return null;
        var cache = GetOrBuildCache();
        return cache.TryGetValue(encounter.Id.Entry, out var idx) ? idx : null;
    }

    private static Dictionary<string, int> GetOrBuildCache()
    {
        if (_cache != null) return _cache;

        var c = new Dictionary<string, int>();
        AddAct(c, ModelDb.Act<Overgrowth>(), 1);
        AddAct(c, ModelDb.Act<Hive>(), 2);
        AddAct(c, ModelDb.Act<Glory>(), 3);
        _cache = c;
        return c;
    }

    private static void AddAct(Dictionary<string, int> cache, ActModel act, int idx)
    {
        foreach (var e in act.AllEncounters) cache.TryAdd(e.Id.Entry, idx);
    }
}

/// <summary>
/// 数值倍率公共逻辑。
///
/// 倍率 = 全局 × 来源：全局基于"是不是 boss 节点 + 层内进度"决定；来源基于该怪物所属 encounter 的源 act。
/// </summary>
internal static class DifficultyMultiplierContext
{
    public static (double hp, double dmg) GetCurrentMultipliers(IRunState state, EncounterModel? encounter)
    {
        int actIdx;
        if (state.Act is Act4Model) actIdx = 4;
        else if (state.Act is Act5Model) actIdx = 5;
        else return (1.0, 1.0);

        var isBossNode = IsAtFinalBossNode(state);

        double globalHp, globalDmg;
        if (isBossNode)
        {
            globalHp = ExtraActsConfig.GetBossHpMult(actIdx);
            globalDmg = ExtraActsConfig.GetBossDmgMult(actIdx);
        }
        else
        {
            var progress = GetActProgress(state);
            globalHp = ExtraActsConfig.GetNormalEnemyHpMult(actIdx).Lerp(progress);
            globalDmg = ExtraActsConfig.GetNormalEnemyDmgMult(actIdx).Lerp(progress);
        }

        double srcHp = 1.0, srcDmg = 1.0;
        var srcAct = SourceActResolver.GetSourceActIndex(encounter);
        if (srcAct.HasValue)
        {
            if (isBossNode)
            {
                srcHp = ExtraActsConfig.GetSourceBossHpMult(actIdx, srcAct.Value);
                srcDmg = ExtraActsConfig.GetSourceBossDmgMult(actIdx, srcAct.Value);
            }
            else
            {
                srcHp = ExtraActsConfig.GetSourceNormalEnemyHpMult(actIdx, srcAct.Value);
                srcDmg = ExtraActsConfig.GetSourceNormalEnemyDmgMult(actIdx, srcAct.Value);
            }
        }

        return (globalHp * srcHp, globalDmg * srcDmg);
    }

    private static bool IsAtFinalBossNode(IRunState state)
    {
        if (state.Map == null) return false;
        return state.CurrentMapCoord == state.Map.BossMapPoint.coord;
    }

    private static double GetActProgress(IRunState state)
    {
        var actFloor = state.ActFloor;
        var totalRooms = state.Act.GetNumberOfRooms(state.Players.Count > 1);
        if (totalRooms <= 0) return 0;
        return Math.Clamp((double)actFloor / totalRooms, 0.0, 1.0);
    }
}

/// <summary>
/// 怪物 spawn 时 HP 倍率：patch CombatState.CreateCreature 的 postfix。
/// 在游戏自己的 SetUniqueMonsterHpValue / ScaleMonsterHpForMultiplayer 跑完之后再加倍。
///
/// 同样需要 clamp 到 999_999_999——理论上 spawn 时怪物 HP 不会接近 int.MaxValue，
/// 但稳健起见加 clamp 跟 runtime patch 行为一致。
/// </summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
public static class MonsterHpMultiplierPatch
{
    [HarmonyPostfix]
    public static void ApplyHpMultiplier(CombatState __instance, CombatSide side, Creature __result)
    {
        if (side != CombatSide.Enemy) return;
        if (__result.Monster == null) return;

        var (hpMult, _) = DifficultyMultiplierContext.GetCurrentMultipliers(
            __instance.RunState, __instance.Encounter);

        if (Math.Abs(hpMult - 1.0) < 1e-6) return;

        // 用 decimal 计算并 clamp，避免极端值溢出 int 范围
        var scaled = (decimal)__result.MaxHp * (decimal)hpMult;
        if (scaled > MonsterRuntimeHpHelper.HpAmountCeiling) scaled = MonsterRuntimeHpHelper.HpAmountCeiling;
        if (scaled < 1m) scaled = 1m;

        __result.SetMaxHpInternal(scaled);
        __result.SetCurrentHpInternal(scaled);
    }
}

/// <summary>
/// 怪物伤害倍率：patch Hook.ModifyDamage 的 postfix。
/// 战斗实际伤害和 AttackIntent 显示都走这条线，一个 patch 同时搞定两个场景。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
public static class MonsterDamageMultiplierPatch
{
    [HarmonyPostfix]
    public static void ApplyDamageMultiplier(
        IRunState runState,
        CombatState? combatState,
        Creature? dealer,
        ref decimal __result)
    {
        if (dealer?.Monster == null) return;

        var (_, dmgMult) = DifficultyMultiplierContext.GetCurrentMultipliers(
            runState, combatState?.Encounter);

        if (Math.Abs(dmgMult - 1.0) < 1e-6) return;

        __result *= (decimal)dmgMult;
    }
}