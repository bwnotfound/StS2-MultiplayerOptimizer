using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
///     自定义 act 的地图节点 UI 修正：
///     - Act4: 所有 Monster 节点 → Elite（让"全是精英"的设定从 UI 层就一致）
///     - Act5: 所有 Elite 节点 → Monster（让中间战斗节点 UI 统一为小怪图标）
///     关键点：
///     - 顶端 BossMapPoint 是 ActMap 的单独 property，不在 Grid 数组里，
///     GetAllMapPoints() 不会枚举到它，所以最终 boss 节点不会被改
///     - Act4 改 Monster→Elite 时<b>包括 CanBeModified=false 的节点</b>
///     （StandardActMap 把第 1 行强制设为 Monster + 不可改，但 act4 想全是精英所以也要改）
///     - Act5 改 Elite→Monster 时只改 CanBeModified=true 的节点
///     （倒数第 7 行如果是 ShouldReplaceTreasureWithElites 模式会强制 Elite + 不可改，
///     这种特殊位保留原样）
///     时机：patch RunManager.GenerateMap postfix，地图生成完毕后修改 PointType。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateMap))]
public static class MapPointTypeFixupPatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPostfix]
    public static void FixupActMapPointTypes(RunManager __instance)
    {
        if (!PatchScope.IsEnabled) return;

        PatchScope.Run(nameof(MapPointTypeFixupPatch), () =>
        {
            var state = RunStateAccessor.GetState(__instance);
            if (state?.Map == null) return;

            if (state.Act is Act4Model)
            {
                var changed = 0;
                foreach (var point in state.Map.GetAllMapPoints())
                    // Act4 全是精英：Monster 全部转 Elite，无视 CanBeModified
                    if (point.PointType == MapPointType.Monster)
                    {
                        point.PointType = MapPointType.Elite;
                        changed++;
                    }

                MainFile.Logger.Info(
                    $"Act4 map UI fixup: changed {changed} Monster->Elite (final boss kept)");
            }
            else if (state.Act is Act5Model)
            {
                var changed = 0;
                foreach (var point in state.Map.GetAllMapPoints())
                    if (point.PointType == MapPointType.Elite && point.CanBeModified)
                    {
                        point.PointType = MapPointType.Monster;
                        changed++;
                    }

                MainFile.Logger.Info(
                    $"Act5 map UI fixup: changed {changed} Elite->Monster (final boss kept)");
            }
        });
    }
}