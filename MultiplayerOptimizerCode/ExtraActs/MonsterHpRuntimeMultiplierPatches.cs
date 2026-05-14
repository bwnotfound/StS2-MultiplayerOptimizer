using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 运行时 HP 倍率 patch——补充 spawn-time 的 <see cref="MonsterHpMultiplierPatch"/>，处理战斗中怪物
/// MaxHp 被改变的场景：千足虫节段激活、实验体 #C29 复活/二阶段、ToughEgg/WaterfallGiant/Doormaker
/// 状态切换等。
///
/// 我们 patch 这三个 CreatureCmd 高层 API 的 prefix，把 amount 加倍并 clamp 到 999_999_999。
///
/// ## 防御 1：Clamp 到 999_999_999
/// SetMaxHpInternal 内部用 `(int)amount` cast，如果 amount > int.MaxValue (≈ 2.1e9) 会
/// OverflowException。Doormaker / WaterfallGiant 等 boss 的"无敌阶段"会传 999_999_999m，
/// 乘以 mult（例如 act5 final boss × 5）后远超 int.MaxValue → crash。Clamp 到游戏自己的上限
/// 999_999_999m 杜绝溢出。
///
/// ## 防御 2：ShowsInfiniteHp 检查（避免 reset 时双重加倍）
/// Doormaker / WaterfallGiant 模式：
///     1) AfterAddedToRoom: OriginalHp = MaxHp（已是 spawn-time 加倍后的值），
///                          SetMaxAndCurrentHp(999_999_999m), ShowsInfiniteHp=true
///     2) DramaticOpen / 类似阶段切换: SetMaxAndCurrentHp(OriginalHp)（仍 ShowsInfiniteHp=true）,
///                                     之后 ShowsInfiniteHp=false
/// 步骤 2 传的 OriginalHp 已经是加倍值，再加倍就是双重缩放。
/// ShowsInfiniteHp=true 期间统一跳过加倍——把这一段视为"游戏代码自己管理的无敌→恢复"语义，
/// 不让我们的 mod 逻辑插手。
///
/// ## 已知 corner case
/// CreatureCmd.GainMaxHp / LoseMaxHp 内部调 SetMaxHp+Heal，amount 已经基于已加倍的 MaxHp，
/// 再加倍会双重缩放。但搜了所有 Monsters/Powers，仅 PaperCutsPower 用 LoseMaxHp 且 target 是 player
/// （被 creature.Monster == null 跳过），实际不会触发。
/// </summary>
internal static class MonsterRuntimeHpHelper
{
    /// <summary>
    /// 跟 SetMaxHpInternal 内部 <c>Math.Min((int)amount, 999999999)</c> 的上限对齐。
    /// 把缩放后的 amount clamp 到这个值，避免 Decimal -&gt; Int32 cast 时 OverflowException。
    /// </summary>
    public const decimal HpAmountCeiling = 999_999_999m;

    public static double GetHpMult(Creature creature)
    {
        var combatState = creature.CombatState;
        if (combatState == null) return 1.0;
        var (hp, _) = DifficultyMultiplierContext.GetCurrentMultipliers(
            combatState.RunState, combatState.Encounter);
        return hp;
    }

    /// <summary>
    /// 对 monster amount 计算缩放值；返回 false 表示该跳过（不是 monster / 倍率=1 / 当前是无敌阶段）。
    /// </summary>
    public static bool TryScaleAmount(Creature creature, decimal originalAmount, out decimal scaled)
    {
        scaled = originalAmount;

        if (creature.Monster == null) return false; // 玩家本体（无 Monster 模型）

        // 关键：召唤物 / 玩家 pet（如亡灵契约师的 Osty）也有 Monster 字段非 null，但属于 Player side。
        // 这些不应被敌人 HP 倍率影响——否则它们的 MaxHp 会被 patch 加倍，再加上 base game 内 GainMaxHp
        // 走 SetMaxHp 路径（amount = MaxHp + delta），会指数级爆炸（每次召唤 ≈ ×倍率）。
        // 敌人召唤的 minion（如 Fabricator/Ovicopter 召的）走 CombatSide.Enemy，会正常加倍。
        if (creature.Side != CombatSide.Enemy) return false;

        // 关键：infinite-HP 阶段（Doormaker/WaterfallGiant 第一阶段）期间，
        // SetMaxAndCurrentHp(OriginalHp) 这种 reset 调用传的是已加倍值，不能再加倍
        if (creature.ShowsInfiniteHp) return false;

        double mult = GetHpMult(creature);
        if (Math.Abs(mult - 1.0) < 1e-6) return false;

        scaled = originalAmount * (decimal)mult;
        if (scaled > HpAmountCeiling) scaled = HpAmountCeiling;
        if (scaled < 1m) scaled = 1m;
        return true;
    }
}

/// <summary>
/// CreatureCmd.SetMaxHp(creature, amount) 的 prefix：amount 加倍 + clamp。
/// 用于 TestSubject.Revive 等"重设 MaxHp"路径。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
public static class CreatureCmdSetMaxHpPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (MonsterRuntimeHpHelper.TryScaleAmount(creature, amount, out var scaled))
            amount = scaled;
    }
}

/// <summary>
/// CreatureCmd.SetMaxAndCurrentHp(creature, amount) 的 prefix：amount 加倍 + clamp。
/// 用于千足虫节段激活、ToughEgg/Doormaker/WaterfallGiant 切换状态等"重设到指定值"路径。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxAndCurrentHp))]
public static class CreatureCmdSetMaxAndCurrentHpPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (MonsterRuntimeHpHelper.TryScaleAmount(creature, amount, out var scaled))
            amount = scaled;
    }
}

/// <summary>
/// CreatureCmd.Heal(creature, amount) 的 prefix：amount 加倍 + clamp。
/// 让复活/治疗后的 CurrentHp 能填满加倍后的 MaxHp（否则只回到原版血量）。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
public static class CreatureCmdHealPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature creature, ref decimal amount)
    {
        if (MonsterRuntimeHpHelper.TryScaleAmount(creature, amount, out var scaled))
            amount = scaled;
    }
}