using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     读档防御兜底（坏档修复）。
///
///     ## 现象
///     启用本 mod 的存档「保存退出 → 继续」时崩溃：
///     <code>
///     ArgumentNullException: source
///       at Enumerable.Select(...)
///       at RoomSet.FromSave(SerializableRoomSet save)
///       at ActModel.FromSave(...)
///       at RunState.FromSerializable_Patch4(...)   // 被其它 mod patch 过
///     </code>
///
///     ## 根因（结合调用栈）
///     <c>RoomSet.FromSave</c> 对 <c>save.EventIds / NormalEncounterIds / EliteEncounterIds</c> 之一
///     调用 <c>.Select(...)</c>，而该列表为 <b>null</b>。这几个是 SerializableRoomSet 的无默认值
///     auto-property，序列化时没被填就是 null。栈里 <c>FromSerializable_Patch4</c> + CznCore 预加载链
///     表明<b>另一个 mod 在拦截 RunState.FromSerializable 时丢了数据</b>——很可能是它不认识本 mod 注入的
///     额外 act（act4/5），处理这些 act 的 RoomSet 时把 id 列表丢成了 null。本 mod 自身不 patch 序列化。
///
///     ## 兜底策略
///     - <see cref="RoomSetFromSaveNullGuardPatch"/>：FromSave 前把 null 的 id 列表补成空列表，
///       消除 <c>ArgumentNullException</c> 硬崩，让存档至少能读进来。
///     - <see cref="ActModelFromSaveDiagnosticPatch"/>：读档后若某 act 的 encounter 列表为空，打印其 id，
///       便于确认是哪个 act（act4/5 还是 base）受影响，决定要不要进一步重建。
///
///     ## 已知限制
///     补空只是止崩。如果受影响的是<b>当前/即将进入</b>的 act，空 encounter 列表在进房间时
///     （<c>NextNormalEncounter</c> 用 <c>list[i % list.Count]</c>）仍可能除零崩。若日志显示是这种情况，
///     再加「按 act 池重建空列表」的二段兜底。对已通关的旧 act，空列表无害。
///     - 这两个 patch 是<b>纯防御</b>，不受难度总开关影响、始终生效（只在出现 null/空时动作）。
/// </summary>
[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.FromSave))]
public static class RoomSetFromSaveNullGuardPatch
{
    [HarmonyPrefix]
    public static void Prefix(SerializableRoomSet save)
    {
        if (save == null) return;

        try
        {
            var fixedLists = new List<string>();

            if (save.EventIds == null)
            {
                save.EventIds = new List<ModelId>();
                fixedLists.Add("EventIds");
            }

            if (save.NormalEncounterIds == null)
            {
                save.NormalEncounterIds = new List<ModelId>();
                fixedLists.Add("NormalEncounterIds");
            }

            if (save.EliteEncounterIds == null)
            {
                save.EliteEncounterIds = new List<ModelId>();
                fixedLists.Add("EliteEncounterIds");
            }

            if (fixedLists.Count > 0)
            {
                MainFile.Logger.Warn(
                    "RoomSet.FromSave: 存档里有 null id 列表 [" + string.Join(", ", fixedLists) +
                    "]，已补空以避免读档崩溃（疑似其它 mod 在 FromSerializable 上丢了 run 数据）");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"RoomSetFromSaveNullGuardPatch failed: {ex}");
        }
    }
}

/// <summary>读档后诊断：某 act 的 encounter 列表为空时打印其 id，便于定位受影响的 act。</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.FromSave))]
public static class ActModelFromSaveDiagnosticPatch
{
    private static readonly System.Reflection.FieldInfo? RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    [HarmonyPostfix]
    public static void Postfix(ActModel __result)
    {
        try
        {
            if (__result == null) return;
            if (RoomsField?.GetValue(__result) is not RoomSet rooms) return;

            int normal = rooms.normalEncounters?.Count ?? 0;
            int elite = rooms.eliteEncounters?.Count ?? 0;

            if (normal == 0 || elite == 0)
            {
                MainFile.Logger.Warn(
                    $"ActModel.FromSave: act '{__result.Id?.Entry ?? "?"}' 读出空 encounter " +
                    $"(normal={normal}, elite={elite})。若这是当前/将进入的 act，进房间时可能除零崩——" +
                    "请把本行连同上面的 null 列表日志反馈。");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"ActModelFromSaveDiagnosticPatch failed: {ex}");
        }
    }
}