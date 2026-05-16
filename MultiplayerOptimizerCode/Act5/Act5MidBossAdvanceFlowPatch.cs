// ReSharper disable InconsistentNaming
// __instance / ___fieldName 是 Harmony 的特殊参数名约定，不能改成 instance/fieldName。

using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
///     第 5 层中间节点 boss 战的 act 推进拦截。
///     ## 问题
///     CombatRoom.RoomType 实现是 <c>Encounter.RoomType</c>。Act5 把 normalEncounters/eliteEncounters
///     替换为 Boss EncounterModel 后，中间节点战斗的 CombatRoom.RoomType 是 Boss。
///     <see cref="NRewardsScreen" />.OnProceedButtonPressed line 314 看到 <c>_isTerminal &amp;&amp; RoomType==Boss</c>
///     就触发 SetLocalPlayerReady → MoveToNextAct → EnterNextAct → 通关结束。
///     ## 旧方案（v0.3）：patch CombatRoom.RoomType getter，act5 中部把 Boss mask 为 Monster
///     缺点：
///     - getter 是<b>极高频热路径</b>（UI 更新、BGM 切换、reward 生成、score 计算等多处调）
///     - 改 getter 会影响所有调用点的语义，包括：
///     * MultiplayerScalingModel.GetMultiplayerScaling boss 1.3x → monster 1.2x
///     * RewardsSet boss-level reward → monster-level reward
///     * ProgressSaveManager epoch unlock check
///     * NCombatUi boss UI 主题
///     * Pantograph / AmethystAubergine 等 boss relic 触发
///     * ScoreUtility / NMapPointHistoryEntry 历史记录
///     - 其他 mod 如果依赖"看到 Boss 就处理 X"的语义会被错误绕开
///     ## 新方案（v0.4+）：只 patch NRewardsScreen.OnProceedButtonPressed
///     优点：
///     - <b>只有这一个调用点</b>需要不触发 act 推进；其他所有 RoomType 调用保持 boss 语义
///     - 玩家获得 boss-level 奖励（更刺激）
///     - 多人 boss 缩放 1.3x（更难，符合"伪装 boss"语义）
///     - Boss BGM/UI/relic 触发（视听更刺激）
///     副作用相对旧版的<b>行为改变</b>：
///     - 中间节点奖励从 monster 级提升到 boss 级
///     - 与之前版本的存档行为不一致（不影响读档，但中部奖励数量会变）
///     ## 实现细节
///     _isTerminal 是 private field，用 ___ 三下划线 + 字段名注入参数；
///     但字段名以 _ 开头，C# 标识符不能起头 4 个下划线，所以用 AccessTools.FieldRefAccess。
/// </summary>
[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
public static class Act5MidBossAdvanceFlowPatch
{
    private static readonly AccessTools.FieldRef<NRewardsScreen, bool> IsTerminalRef =
        AccessTools.FieldRefAccess<NRewardsScreen, bool>("_isTerminal");

    private static readonly FieldInfo? RunStateField =
        AccessTools.Field(typeof(NRewardsScreen), "_runState");

    [HarmonyPriority(Priority.Low)]
    [HarmonyPrefix]
    public static bool Prefix(NRewardsScreen __instance, NButton _)
    {
        // PatchScope.Run<bool> fallback: 出异常时放行原方法
        return PatchScope.Run(nameof(Act5MidBossAdvanceFlowPatch), () =>
        {
            if (!PatchScope.IsEnabled) return true;

            // 读 _isTerminal（private 字段）
            bool isTerminal;
            try
            {
                isTerminal = IsTerminalRef(__instance);
            }
            catch
            {
                return true; // 反射失败 → 放行
            }

            if (!isTerminal) return true; // 非战斗结算的奖励界面不管

            // 读 _runState（private 字段，不一定等于 RunManager.Instance.State）
            var runState = RunStateField?.GetValue(__instance) as IRunState;
            if (runState == null) return true;

            // 必须是 act5
            if (runState.Act is not Act5Model) return true;
            if (runState.Map == null) return true;

            // 必须是 boss 战（不然这条 prefix 也没意义——原方法本来就走 monster 分支）
            if (runState.CurrentRoom is not CombatRoom cr) return true;
            if (cr.RoomType != RoomType.Boss) return true;

            // 顶端真 boss 节点？放行让原方法触发通关流程
            if (runState.CurrentMapCoord == runState.Map.BossMapPoint.coord) return true;

            // 中间节点的"伪装 boss"——按 monster 战处理，直接走奖励翻页 / 退出 reward screen 流程。
            // 这跟原方法 _isTerminal=true && !boss 分支等价（line 328-346）：
            //   TaskHelper.RunSafely(RunManager.Instance.ProceedFromTerminalRewardsScreen());
            var rm = RunManager.Instance;
            if (rm == null) return true; // 没 RunManager 不能跳过原方法，放行避免卡屏

            TaskHelper.RunSafely(rm.ProceedFromTerminalRewardsScreen());
            return false; // 跳过原方法
        }, true);
    }
}