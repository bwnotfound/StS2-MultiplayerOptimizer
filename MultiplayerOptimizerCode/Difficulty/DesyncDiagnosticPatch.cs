// ReSharper disable InconsistentNaming
//
// ===================================================================================================
// 【临时诊断 patch — 定位咬人卷轴战斗 desync 用】
//
// 用途：在每场战斗开始时（CombatManager.SetUpCombat postfix）打印怪物 + 倍率的完整状态，
//       并在每次怪物造成伤害时（Hook.ModifyDamage postfix）打印伤害倍率。
//
// 使用步骤：
//   1. 把本文件放进 MultiplayerOptimizerCode/Difficulty/，编译
//   2. host 和 client 两台机器都装这个编译版本，联机
//   3. 打到 act4 咬人卷轴战斗，触发 desync
//   4. 各自找到游戏日志文件（Godot 控制台日志），搜索 "[DESYNC-DIAG]"，
//      把咬人卷轴那场战斗的所有 DIAG 段落各截一份
//   5. 把 host 那份 + client 那份一起发回对比
//
// 对比要点（只要有任一项两端不同，就锁定了 desync 源头）：
//   - hpMult / dmgMult 是否一致
//   - 4 个咬人卷轴的 MaxHp / CurrentHp / StarterMoveIdx 是否一致
//   - ActFloor / progress / srcAct 是否一致
//   - 每次伤害的 origDamage / multipliedDamage 是否一致
//
// 定位完成后删除本文件即可——它纯打印，不改任何游戏状态。
// ===================================================================================================

using System;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// 战斗开始诊断：打印 SetUpCombat 后所有怪物的最终状态 + mod 算出的倍率。
/// </summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
public static class DesyncDiagnosticSetUpCombatPatch
{
    [HarmonyPriority(Priority.Last)] // 在 MonsterHpMultiplierPatch 之后跑，看到最终 HP
    [HarmonyPostfix]
    public static void Postfix(CombatState state)
    {
        try
        {
            if (state?.RunState == null) return;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("========== [DESYNC-DIAG] SetUpCombat ==========");

            // ---- 玩家信息（用于区分 host / client 两台机器的 log）----
            sb.Append("Players: ");
            try
            {
                foreach (var p in state.RunState.Players)
                    sb.Append($"[NetId={p?.NetId} Char={p?.Character?.Id?.Entry} " +
                              $"HP={p?.Creature?.CurrentHp}/{p?.Creature?.MaxHp}] ");
            }
            catch (Exception ex)
            {
                sb.Append($"<players enum failed: {ex.Message}>");
            }

            sb.AppendLine();

            // ---- act / 位置 / encounter ----
            var act = state.RunState.Act;
            sb.AppendLine($"Act={act?.GetType().Name} ActFloor={state.RunState.ActFloor} " +
                          $"PlayersCount={state.RunState.Players?.Count}");
            sb.AppendLine($"Encounter={state.Encounter?.Id?.Entry} " +
                          $"CurrentMapCoord={state.RunState.CurrentMapCoord}");

            // ---- 倍率：调一次跟实际 patch 相同的逻辑 ----
            try
            {
                int srcActDbg = -1;
                try
                {
                    var srcOpt = SourceActResolver.GetSourceActIndex(state.Encounter);
                    srcActDbg = srcOpt ?? -1;
                }
                catch
                {
                    /* ignore */
                }

                int totalRooms = -1;
                try
                {
                    totalRooms = act?.GetNumberOfRooms((state.RunState.Players?.Count ?? 1) > 1) ?? -1;
                }
                catch
                {
                    /* ignore */
                }

                var (hp, dmg) = DifficultyMultiplierContext.GetCurrentMultipliers(
                    state.RunState, state.Encounter);

                // ":R" round-trip 格式——能暴露 host/client 之间 double 的 ULP 级差异
                sb.AppendLine($"Multipliers: hpMult={hp:R} dmgMult={dmg:R}");
                sb.AppendLine($"  inputs: srcAct={srcActDbg} totalRooms={totalRooms} " +
                              $"actFloor={state.RunState.ActFloor}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"GetCurrentMultipliers threw: {ex}");
            }

            // ---- 每个 creature 的最终状态 ----
            try
            {
                foreach (var c in state.Creatures)
                {
                    if (c == null) continue;
                    var sob = c.Monster as ScrollOfBiting;
                    sb.AppendLine(
                        $"  Creature side={c.Side} monster={c.Monster?.Id?.Entry} " +
                        $"MaxHp={c.MaxHp} CurrentHp={c.CurrentHp} " +
                        $"HpDisplay={c.HpDisplay}" +
                        (sob != null ? $" StarterMoveIdx={sob.StarterMoveIdx}" : ""));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"<creatures enum failed: {ex.Message}>");
            }

            sb.AppendLine("===============================================");
            MainFile.Logger.Info(sb.ToString());
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"DesyncDiagnosticSetUpCombatPatch failed: {ex}");
        }
    }
}

/// <summary>
/// 伤害诊断：每次怪物造成伤害时打印倍率前后的值。
///
/// 只打印 dealer 是 act4/5 enemy monster 的调用（跟 MonsterDamageMultiplierPatch 同样的过滤），
/// 避免日志被无关伤害刷屏。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
public static class DesyncDiagnosticDamagePatch
{
    [HarmonyPriority(Priority.First)] // 在 MonsterDamageMultiplierPatch 之前——拿到它改之前的值
    [HarmonyPostfix]
    public static void Postfix(
        IRunState runState,
        ICombatState? combatState,
        Creature? target,
        Creature? dealer,
        decimal __result)
    {
        try
        {
            if (dealer?.Monster == null) return;
            if (dealer.Side != CombatSide.Enemy) return;
            if (runState?.Act is not Act4Model && runState?.Act is not Act5Model) return;

            // 注意：本 patch Priority.First，postfix 比 MonsterDamageMultiplierPatch(Low) 先跑，
            // 所以这里的 __result 是「mod 倍率应用之前」的基础伤害。
            // 对比 host/client 两份 log 里同一次攻击的这个值即可。
            double dmgMult = 1.0;
            try
            {
                var (_, d) = DifficultyMultiplierContext.GetCurrentMultipliers(
                    runState, combatState?.Encounter);
                dmgMult = d;
            }
            catch
            {
                /* ignore */
            }

            MainFile.Logger.Info(
                $"[DESYNC-DIAG] ModifyDamage dealer={dealer.Monster?.Id?.Entry} " +
                $"target={target?.Monster?.Id?.Entry ?? (target?.IsPlayer == true ? "PLAYER" : "?")} " +
                $"baseDamage={__result} dmgMult={dmgMult:R} " +
                $"=> expectedFinal={__result * (decimal)dmgMult}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"DesyncDiagnosticDamagePatch failed: {ex}");
        }
    }
}