using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
///     让 TreasureRoom 构造函数接受 act4/5（actIndex 3/4）。
///     背景：
///     TreasureRoom..ctor(int actIndex) 写死 validation：
///     if (actIndex &lt; 0 || actIndex &gt; 2)
///     throw new ArgumentOutOfRangeException("actIndex", "must be between 0 and 2");
///     而且 actIndex 只用于 validation，<b>不存到任何字段</b>——后续 EnterInternal/资源加载
///     都用 runState.Act 实例引用，跟构造时的 actIndex 数值无关。
///     修复：prefix 改 actIndex 参数，把 ≥3 的值改成 2，绕过 validation。
///     因为参数没被存，"假装是 act3" 不影响 treasure room 实际行为——资源、奖励、UI 都按
///     runState.Act（真实的 act4/5）加载。
///     ## 不 honor PatchScope.IsEnabled
///     跟 MultiplayerScalingForCustomActsPatch / SuppressMissingEpochErrorPatch 同样原因：
///     自定义 act 始终存在，禁用此 patch 会让 act4/5 treasure room 一构造就 throw。
/// </summary>
[HarmonyPatch(typeof(TreasureRoom), MethodType.Constructor, typeof(int))]
public static class TreasureRoomCustomActsPatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPrefix]
    public static void Prefix(ref int actIndex)
    {
        try
        {
            if (actIndex >= 3) actIndex = 2;
        }
        catch
        {
            // 不会发生（ref int 不会抛），保险起见保留 try
        }
    }
}