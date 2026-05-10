using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 替换自定义 act 的战斗内容池：
///   - Act4: eliteEncounters 用 Glory elite encounter 重新填充
///           （注：act4 所有节点 PointType 已被 MapPointTypeFixupPatch 改成 Elite，
///            所以游戏只会消费 eliteEncounters，normalEncounters 不会被用到）
///   - Act5: normalEncounters + eliteEncounters 都替换为 Glory boss encounter
///           （让所有中间战斗节点 = boss 战；CombatRoom.RoomType 由 Act5MaskBossRoomTypePatch
///            处理，避免触发通关检测）
///   - 顶端 BossMapPoint 用的是 _rooms.Boss（单独字段），不受这个 patch 影响
///
/// 实现：patch ActModel.GenerateRooms postfix，反射访问 protected 字段 _rooms 拿到 RoomSet，
///       用 EncounterListBuilder 填充 list。
///
/// 多人同步：所有客户端装相同 mod，rng 序列一致，替换结果三端一致。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
public static class CustomActEncounterReplacementPatch
{
    private static readonly FieldInfo RoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    [HarmonyPostfix]
    public static void ReplaceEncounters(ActModel __instance, Rng rng)
    {
        if (__instance is not Act4Model && __instance is not Act5Model)
            return;

        var rooms = RoomsField?.GetValue(__instance) as RoomSet;
        if (rooms == null)
        {
            MainFile.Logger.Error(
                $"[ExtraActs] Failed to access _rooms on {__instance.Id.Entry}; encounter replacement skipped");
            return;
        }

        var gloryAct = ModelDb.Act<Glory>();

        if (__instance is Act4Model)
        {
            // 第 4 层：所有节点都是 Elite（PointType 由 MapPointTypeFixupPatch 改），
            // 重新填充 eliteEncounters 让多样性更好（覆盖原 GenerateRooms 的填充）
            int targetCount = rooms.eliteEncounters.Count;
            var elitePool = gloryAct.AllEliteEncounters.ToList();
            EncounterListBuilder.FillWithShuffleBag(
                rooms.eliteEncounters, elitePool, targetCount, rng);

            MainFile.Logger.Info(
                $"[ExtraActs] Act4: refilled {targetCount} elite encounters " +
                $"(elite pool size: {elitePool.Count})");
        }
        else if (__instance is Act5Model)
        {
            // 第 5 层：Monster + Elite 节点战斗都变成 boss
            // （CombatRoom.RoomType 在 Act5MaskBossRoomTypePatch 里被掩盖回 Monster，
            //  所以不会触发通关检测）
            var bossPool = gloryAct.AllBossEncounters.ToList();

            int normalCount = rooms.normalEncounters.Count;
            EncounterListBuilder.FillWithShuffleBag(
                rooms.normalEncounters, bossPool, normalCount, rng);

            int eliteCount = rooms.eliteEncounters.Count;
            EncounterListBuilder.FillWithShuffleBag(
                rooms.eliteEncounters, bossPool, eliteCount, rng);

            MainFile.Logger.Info(
                $"[ExtraActs] Act5: replaced {normalCount} normal + {eliteCount} elite encounters " +
                $"with boss content (boss pool size: {bossPool.Count})");
        }
    }
}