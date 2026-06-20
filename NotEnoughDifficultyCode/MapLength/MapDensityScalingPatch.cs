// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// 修复「地图长度越大、特殊房（精英/休息/商店/问号）密度越低」的问题。见 MapLength 施工文档 §9。
///
/// ## 根因
/// <c>StandardActMap.AssignPointTypes</c> 按 <c>MapPointTypeCounts</c> 放<b>固定数量</b>的特殊房，
/// 放完后剩余点一律置为 Monster。<c>MapPointTypeCounts</c> 是每个 act 的<b>固定值</b>，不随
/// <c>_mapLength</c> 缩放 → 行数越多，固定特殊房被越多普通房稀释。
///
/// ## 注入点选择（重要：为什么不 patch 构造器）
/// <c>MapPointTypeCounts</c> 在 <c>StandardActMap</c> 构造器里由 <c>actModel.GetMapPointTypes(mapRng)</c>
/// 产生。曾尝试 prefix 构造器注入 override，但<b>会破坏 MapLengthPatch</b>：构造器里调用的
/// <c>ActModel.GetNumberOfRooms</c> 是非虚的小型具体方法，Harmony patch 构造器、重编译其 IL 时会把它
/// <b>内联</b>进构造器，绕过 MapLengthPatch（postfix on GetNumberOfRooms）→ 地图长度回退原版。
///
/// 因此改为 <b>postfix 各 act 的 <c>GetMapPointTypes</c> override</b>：
/// - 不碰构造器、不碰 GetNumberOfRooms → 不会触发上述内联，长度功能不受影响；
/// - postfix 的 <c>__instance</c> 就是该 act，可直接解析 actIdx；
/// - <c>GetMapPointTypes</c> 是 abstract，但各具体 act（base 的 Underdocks/Overgrowth/Hive/Glory +
///   mod 的 Act4Model/Act5Model）都 override 了它，用 TargetMethods 枚举这些 override 即可。
///
/// ## 多人安全
/// - ratio ≤ 1（默认/缩短）时 postfix 不改 __result → 与原版逐值相等、隐身、不 desync。
/// - ratio &gt; 1 时缩放确定性（比例只取自已被 ConfigSync 同步的 map_length config）；postfix 本身不消耗
///   mapRng，后续 AssignPointTypes 的消耗对 host/client 同 mod 同 config 一致 ⇒ 同一张图。
/// - 硬前提（与 MapLengthPatch 一致）：调过任意 act 长度后两端必须同版本 mod。
///
/// ## 限制
/// - <c>NumOfShops</c> 是 <c>{ get; } = 3</c> 硬编码，无 setter/init/ctor 参，无法缩放，保持 3。
/// - 缩放受 AssignPointTypes 放置规则约束（精英不相邻、只在上部行等），比例过大时多出的特殊房可能放不下
///   而回落为 Monster（软失败，不崩）。
/// - act4 节点类型被 MapPointTypeFixupPatch 强制全精英，密度缩放对 act4 基本无意义，但无害。
/// </summary>
[HarmonyPatch]
public static class MapDensityScalingPatch
{
    // ReSharper disable once UnusedMember.Local
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // 枚举所有需要缩放的 act 的 GetMapPointTypes override。
        // base acts 的类型在 MegaCrit.Sts2.Core.Models.Acts；Act4Model/Act5Model 是 mod 自己的。
        var types = new[]
        {
            typeof(Underdocks), typeof(Overgrowth), typeof(Hive), typeof(Glory),
            typeof(Act4Model), typeof(Act5Model),
        };

        foreach (var t in types)
        {
            // DeclaredMethod 只取该类型自身声明的 override；某 act 若未 override（理论上不会，
            // 因为 GetMapPointTypes 是 abstract）则返回 null，跳过即可。
            var m = AccessTools.DeclaredMethod(t, nameof(ActModel.GetMapPointTypes));
            if (m != null)
            {
                yield return m;
            }
        }
    }

    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, ref MapPointTypeCounts __result)
    {
        if (!PatchScope.IsEnabled) return;
        if (!NotEnoughDifficultyConfig.MapLengthEnabled) return; // 总开关关闭 → 不缩放密度
        if (__instance == null || __result == null) return;

        try
        {
            int actIdx = MapLengthPatch.ResolveActIndex(__instance);
            if (actIdx < 1 || actIdx > 5) return;

            double defaultRows = MapLengthPatch.DefaultRows(actIdx);
            if (defaultRows <= 0) return;
            double ratio = MapLengthPatch.GetConfiguredRows(actIdx) / defaultRows;

            // ★ 默认/缩短：不改 __result → 完全隐身、不 desync
            if (ratio <= 1.0 + 1e-9) return;

            int Scale(int n) => Math.Max(n, (int)Math.Round(n * ratio)); // 只增不减，向上取整保密度

            __result = new MapPointTypeCounts(
                unknownCount: Scale(__result.NumOfUnknowns),
                restCount: Scale(__result.NumOfRests))
            {
                NumOfElites = Scale(__result.NumOfElites),
                PointTypesThatIgnoreRules = __result.PointTypesThatIgnoreRules,
                // NumOfShops 无法设置（硬编码 3），保持默认。
            };
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"MapDensityScalingPatch failed: {ex}");
            // 出错时不改 __result，回退原版行为
        }
    }
}