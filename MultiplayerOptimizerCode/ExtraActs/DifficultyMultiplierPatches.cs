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
///
/// 一个 encounter 只会属于 act1/2/3 中的一个，第一次调用时遍历三个 act 的 AllEncounters
/// 建立 Dictionary&lt;encounterId, sourceActIdx&gt;，后续 O(1)。
/// </summary>
internal static class SourceActResolver
{
    private static Dictionary<string, int>? _cache;

    /// <summary>
    /// 找出 encounter 来自哪个源 act (1/2/3)。
    /// 返回 null 表示 encounter 不属于任何 base act（不应发生，但作为防御）。
    /// </summary>
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
        foreach (var e in act.AllEncounters)
            // 同一 entry id 不会跨 act 重复；如果重复则保留先添加的（act1 优先）
            cache.TryAdd(e.Id.Entry, idx);
    }
}

/// <summary>
/// 数值倍率公共逻辑。
///
/// 倍率 = 全局 × 来源：
///   - 全局：基于"是不是 boss 节点"+"层内进度"决定；boss 单值，普通敌人按进度 lerp
///   - 来源：基于该怪物所属 encounter 的源 act（1/2/3），各自独立倍率
///
/// 例：Act4 全局 HP 1.4，act1 来源敌人 HP 倍率 1.8 → 该敌人 HP × 1.4 × 1.8 = × 2.52
/// </summary>
internal static class DifficultyMultiplierContext
{
    /// <summary>
    /// 返回当前敌人应用的 (HP 倍率, 伤害倍率)。
    /// encounter 可以为 null（伤害预览没有 combatState 时），此时 source 倍率回退到 1.0。
    /// </summary>
    public static (double hp, double dmg) GetCurrentMultipliers(IRunState state, EncounterModel? encounter)
    {
        int actIdx;
        if (state.Act is Act4Model) actIdx = 4;
        else if (state.Act is Act5Model) actIdx = 5;
        else return (1.0, 1.0);

        var isBossNode = IsAtFinalBossNode(state);

        // 全局倍率
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

        // 来源 act 倍率
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
        // CurrentMapCoord 是 MapCoord?，跟 BossMapPoint.coord (MapCoord) 通过 == 运算符比较
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
/// 怪物 HP 倍率：patch CombatState.CreateCreature 的 postfix。
///
/// 之前用 Creature.SetUniqueMonsterHpValue postfix 但该位置 creature.CombatState 还没 attach，
/// 拿不到 encounter 信息。CombatState.CreateCreature 时 __instance.Encounter 已经存在，
/// 而且此时 SetUniqueMonsterHpValue 和 ScaleMonsterHpForMultiplayer 都已在 body 内调过，
/// 我们的倍率叠加在它们之上。
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

        decimal newMaxHp = Math.Max(1, (int)Math.Round(__result.MaxHp * hpMult));
        __result.SetMaxHpInternal(newMaxHp);
        __result.SetCurrentHpInternal(newMaxHp);
    }
}

/// <summary>
/// 怪物伤害倍率：patch Hook.ModifyDamage 的 postfix。
///
/// Hook.ModifyDamage 是 STS2 所有伤害计算的统一入口（战斗实际伤害和 AttackIntent 都走它），
/// 所以一个 patch 同时搞定"战斗加倍"+"intent 显示加倍"。
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
        if (dealer?.Monster == null) return; // 只加倍怪物造成的伤害

        var (_, dmgMult) = DifficultyMultiplierContext.GetCurrentMultipliers(
            runState, combatState?.Encounter);

        if (Math.Abs(dmgMult - 1.0) < 1e-6) return;

        __result *= (decimal)dmgMult;
    }
}