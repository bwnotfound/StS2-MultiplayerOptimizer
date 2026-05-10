using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 让 TreasureRoom 构造函数接受 act4/5（actIndex 3/4）。
///
/// 背景：
///   TreasureRoom..ctor(int actIndex) 写死 validation：
///     if (actIndex &lt; 0 || actIndex &gt; 2)
///         throw new ArgumentOutOfRangeException("actIndex", "must be between 0 and 2");
///   而且 actIndex 只用于 validation，**不存到任何字段**——后续 EnterInternal/资源加载
///   都用 runState.Act 实例引用，跟构造时的 actIndex 数值无关。
///
/// 修复：prefix 改 actIndex 参数，把 ≥3 的值改成 2，绕过 validation。
/// 因为参数没被存，"假装是 act3" 不影响 treasure room 实际行为——资源、奖励、UI 都按
/// runState.Act（真实的 act4/5）加载。
/// </summary>
[HarmonyPatch(typeof(TreasureRoom), MethodType.Constructor, new[] { typeof(int) })]
public static class TreasureRoomCustomActsPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int actIndex)
    {
        if (actIndex >= 3) actIndex = 2;
    }
}