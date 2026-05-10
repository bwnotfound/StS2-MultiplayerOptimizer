using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 反射访问 RunManager.State（私有属性）的 helper。
///
/// 为什么需要这个：
///   - RunManager.State 是 private RunState? State { get; set; }
///   - mod 项目的 csproj 没启用 publicize（&lt;Publicize ... Condition="False"&gt;）
///   - 直接访问会编译报错"不能在此处访问 private 属性 'State'"
///
/// BaseLib 自己也是用同样的反射方式（参见 CurrentGeneratingRunState）。
/// 我们独立定义一份，避免依赖 BaseLib 内部 patch 的状态字段时序。
///
/// 性能：MethodInfo 只解析一次（静态字段），调用 Invoke 比直接访问稍慢
/// 但相比 GenerateRooms / GenerateMap 这种"每 run 几次"的低频场景完全可忽略。
/// </summary>
internal static class RunStateAccessor
{
    private static readonly MethodInfo? StateGetter =
        AccessTools.PropertyGetter(typeof(RunManager), "State");

    public static RunState? GetState(RunManager runManager)
    {
        if (StateGetter == null)
        {
            MainFile.Logger.Error(
                "[ExtraActs] Could not resolve RunManager.State getter via reflection!");
            return null;
        }

        return (RunState?)StateGetter.Invoke(runManager, null);
    }
}