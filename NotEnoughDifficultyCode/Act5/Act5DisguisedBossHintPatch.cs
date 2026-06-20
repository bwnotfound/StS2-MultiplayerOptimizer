using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     进入第 5 层时给玩家明确提示：中部所有战斗节点 UI 显示为小怪图标，但实际都是 boss 内容。
///     当前实现：用 logger 输出（玩家可以在 mod log 看到）+ 依赖用户在 acts.json 里把
///     act5 标题加上警告标记（如 "终极试炼·伪装 Boss"），让 NActBanner 进入 act 时自然展示警告。
///     未来可以扩展：自定义 Godot 节点做屏幕中心 banner（工作量较大，目前用现成机制）。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
public static class Act5DisguisedBossHintPatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPostfix]
    public static void ShowHint(int currentActIndex)
    {
        if (!PatchScope.IsEnabled) return;

        PatchScope.Run(nameof(Act5DisguisedBossHintPatch), () =>
        {
            // currentActIndex 是 0-indexed: act5 = 4
            if (currentActIndex != 4) return;
            if (!ExtraActsConfig.ShouldShowAct5DisguisedBossWarning) return;

            MainFile.Logger.Info(
                "进入第 5 层 ⚠️ —— 中部所有战斗节点 UI 显示为小怪图标，" +
                "但实际是 boss 难度战斗（来自 act1/2/3 boss 池混合）。请做好准备。");
        });
    }
}