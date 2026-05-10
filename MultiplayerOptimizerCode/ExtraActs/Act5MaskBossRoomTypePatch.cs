using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 第 5 层中间节点的 boss 战 RoomType 掩盖：
///
/// 问题：CombatRoom.RoomType 实现是 `Encounter.RoomType`。我们把 act5 的
///       normalEncounters/eliteEncounters 替换为 Boss EncounterModel 后，
///       中间节点战斗的 CombatRoom.RoomType 是 Boss。
///       NRewardsScreen.OnProceedButtonPressed 看到 `_isTerminal && RoomType==Boss`
///       会触发 SetLocalPlayerReady → MoveToNextAct → EnterNextAct → 通关结束。
///
/// 修复：patch CombatRoom.RoomType getter，act5 中间节点把 Boss 报告为 Monster。
///       顶端真 boss 节点（BossMapPoint）保持 Boss 不动。
///
/// 副作用：被掩盖为 Monster 后，奖励界面也按 monster 战处理（少奖励、走 Proceed 按钮逻辑）。
///   - 用户要"中间 boss 显示为 monster ui"，行为一致即可
///   - 如果以后想要中间 boss 给 boss 级奖励，需要单独 patch reward 逻辑
/// </summary>
[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.RoomType), MethodType.Getter)]
public static class Act5MaskBossRoomTypePatch
{
    [HarmonyPostfix]
    public static void MaskBossAsMonster(CombatRoom __instance, ref RoomType __result)
    {
        // 快速路径：不是 Boss 类型就直接返回（绝大多数调用都走这里）
        if (__result != RoomType.Boss) return;

        // 检查是否在 act5
        var rm = RunManager.Instance;
        if (rm == null) return;
        var state = RunStateAccessor.GetState(rm);
        if (state?.Act is not Act5Model) return;
        if (state.Map == null) return;

        // 只对当前正在打的房间做掩盖（避免误改其他 CombatRoom 实例的 RoomType）
        if (state.CurrentRoom != __instance) return;

        // 是不是顶端真 boss？MapCoord 实现了 == 运算符（按 col/row 比较），
        // state.CurrentMapCoord 是 MapCoord? 但 C# 会自动提升 MapCoord 到 MapCoord? 做比较，
        // null 时比较结果是 false（不是 boss 节点 → 继续往下掩盖）。
        // 这跟 NRewardsScreen 源码里的写法一致。
        if (state.CurrentMapCoord == state.Map.BossMapPoint.coord)
        {
            // 顶端 boss，保持 Boss
            return;
        }

        // 中间节点的伪装 boss → 报告为 Monster
        __result = RoomType.Monster;
    }
}