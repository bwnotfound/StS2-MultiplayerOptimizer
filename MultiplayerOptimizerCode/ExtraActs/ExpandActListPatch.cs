using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Unlocks;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 把第 4/5 层追加到 ActModel.GetRandomList 的结果末尾，让游戏识别为 5 个 act 的 run。
///
/// 为什么需要这个 patch：
///   - BaseLib 自带的 ActModelGetRandomListPatch 是 REPLACE 机制，只在原有 3 个 act 位置上替换，
///     不会扩展 list 长度。
///   - 我们的 Act4/Act5 的 ActNumber=-1，BaseLib 不会在循环中命中它们，所以不会被误用作替换品。
///   - 这里用 Postfix（After 关系）保证在 BaseLib 的 patch 之后执行，
///     拿到 BaseLib 处理完的 3 个 act，再 append 我们的两个。
///
/// 多人同步：所有客户端都装这个 mod，host/client 各自独立调用 GetRandomList，
/// 用同一种子 → 各端独立得到完全相同的 5-act 列表。无需额外网络同步代码。
///
/// ## 不 honor PatchScope.IsEnabled
/// 这个 patch 决定 run manifest 包含哪些 act。Enabled 切换不能改变 manifest——否则同一存档
/// 在 Enabled=true 和 false 之间切换时 act 数量会变，存档加载错乱。所以无论 Enabled 状态，
/// 都正常 append。具体的"自定义 act 行为"由其他 patch 在运行期分别 honor Enabled。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetRandomList))]
[HarmonyAfter("BaseLib")] // 确保排在 BaseLib 的 ActModelGetRandomListPatch 之后
public static class ExpandActListPatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPostfix]
    public static IEnumerable<ActModel> AppendExtraActs(
        IEnumerable<ActModel> __result,
        Rng rng,
        UnlockState unlockState,
        bool isMultiplayer)
    {
        // 即使出错也要返回原 __result，避免 base game 拿到 null
        try
        {
            var list = __result.ToList();

            if (ExtraActsBootstrap.Act4 != null) list.Add(ExtraActsBootstrap.Act4);
            if (ExtraActsBootstrap.Act5 != null) list.Add(ExtraActsBootstrap.Act5);

            MainFile.Logger.Info(
                $"Final act list ({list.Count}): " +
                string.Join(" -> ", list.Select(a => a.Id.Entry)));

            return list;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"ExpandActListPatch failed: {ex}");
            return __result;
        }
    }
}