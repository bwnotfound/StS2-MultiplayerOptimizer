using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 运行时 HP 倍率 patch——补充 spawn-time 的 <see cref="MonsterHpMultiplierPatch"/>（在
/// <see cref="DifficultyMultiplierPatches"/> 里），处理战斗中怪物 MaxHp 被改变的场景。
///
/// 战斗中改 MaxHp 的 path 不走 CombatState.CreateCreature，绕过了 spawn 时的 patch：
///   - 千足虫节段激活：DecimillipedeSegment.SegmentBecomesActive → CreatureCmd.SetMaxAndCurrentHp
///   - 实验体 #C29 二阶段/复活：TestSubject.Revive → CreatureCmd.SetMaxHp + CreatureCmd.Heal
///   - 其他 boss 状态切换（ToughEgg/WaterfallGiant/Doormaker）→ CreatureCmd.SetMaxAndCurrentHp
///
/// 我们 patch 这三个高层 API 的 prefix，把 amount 参数加倍，让所有"运行时重设/治疗"
/// 都按倍率工作。
///
/// 已知 corner case：CreatureCmd.GainMaxHp/LoseMaxHp 内部会调 SetMaxHp+Heal，amount 已经基于
/// 当前（已加倍的）MaxHp，再加倍会双重缩放。但 monster 极少用这两个方法（搜了所有
/// Monsters/Powers 目录只有 PaperCutsPower 用 LoseMaxHp 且 target 是 player），实际不会触发。
/// </summary>
internal static class MonsterRuntimeHpHelper
{
    public static double GetHpMult(Creature creature)
    {
        var combatState = creature.CombatState;
        if (combatState == null) return 1.0;
        var (hp, _) = DifficultyMultiplierContext.GetCurrentMultipliers(
            combatState.RunState, combatState.Encounter);
        return hp;
    }

    public static bool ShouldApply(Creature creature, out double mult)
    {
        mult = 1.0;
        if (creature.Monster == null) return false; // 玩家不动
        mult = GetHpMult(creature);
        return Math.Abs(mult - 1.0) >= 1e-6;
    }
}

/// <summary>
/// CreatureCmd.SetMaxHp(creature, amount) 的 prefix：把 amount 按 monster 的 HP 倍率加倍。
/// 用于 TestSubject.Revive 等"重设 MaxHp"路径。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
public static class CreatureCmdSetMaxHpPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (!MonsterRuntimeHpHelper.ShouldApply(creature, out var mult)) return;
        amount = amount * (decimal)mult;
    }
}

/// <summary>
/// CreatureCmd.SetMaxAndCurrentHp(creature, amount) 的 prefix：amount 加倍。
/// 用于千足虫节段激活、ToughEgg 切换状态等"重设到指定值"路径。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxAndCurrentHp))]
public static class CreatureCmdSetMaxAndCurrentHpPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (!MonsterRuntimeHpHelper.ShouldApply(creature, out var mult)) return;
        amount = amount * (decimal)mult;
    }
}

/// <summary>
/// CreatureCmd.Heal(creature, amount) 的 prefix：amount 加倍。
/// 让复活/治疗后的 CurrentHp 能填满加倍后的 MaxHp（否则只回到原版血量）。
///
/// 副作用：怪物 power 主动治疗自己时治疗量也加倍——这跟"怪物整体加倍"的语义一致，合理。
/// 玩家被 Heal 不动（creature.Monster == null）。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
public static class CreatureCmdHealPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (!MonsterRuntimeHpHelper.ShouldApply(creature, out var mult)) return;
        amount = amount * (decimal)mult;
    }
}