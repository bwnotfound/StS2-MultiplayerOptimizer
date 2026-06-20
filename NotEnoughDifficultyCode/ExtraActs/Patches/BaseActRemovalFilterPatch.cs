using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     「敌人移除列表」在 <b>base act 1~3</b> 的生效（需求2追加，对应弹窗里「只在4~5层生效」勾选框）。
///
///     ## 何时启用
///     仅当 <c>RemovalOnlyActs45 == false</c>（默认，全层生效）时运行；勾上「只在4~5层生效」则整段跳过。
///     act4/5 由 <see cref="CustomActEncounterReplacementPatch"/> 处理（始终生效，与本开关无关），这里只管 1~3。
///
///     ## 为什么用「替换」而不是「删除」
///     base game <c>GenerateRooms</c> 把各 act 自己的池填进 <c>_rooms.normalEncounters/eliteEncounters</c>
///     （定长 List）和 <c>_rooms.Boss</c>。<c>RoomSet.NextNormalEncounter</c> 用 <c>list[i % list.Count]</c>
///     取战斗，<b>把 list 删空会除零崩溃</b>。所以这里对被排除的槽位做<b>原地替换</b>（用同 act、同 tier 的
///     非排除 encounter 顶替），list 长度不变，绝不出空。
///
///     ## 多人安全
///     替换是<b>确定性</b>的（按槽位顺序从非排除池循环取，不消耗 rng，也不改变 base game 后续 rng 序列），
///     只依赖已被 ConfigSync 同步的「移除列表 + RemovalOnlyActs45」。host/client 同 mod 同 config ⇒ 同结果。
///
///     ## 兜底
///     - 移除列表为空 → 直接返回。
///     - 某 tier 的非排除池为空（该 act 该 tier 全被排除）→ 该 tier 不替换、保留原样，保证有战斗可抽。
///     - normal 槽位尽量按原 IsWeak 归类替换（弱怪槽换弱怪、普通槽换普通），子池空则退到另一子池。
///     - 整段 PatchScope.Run 包裹，异常只记日志不影响游戏。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
public static class BaseActRemovalFilterPatch
{
    private static readonly FieldInfo? RoomsField = AccessTools.Field(typeof(ActModel), "_rooms");

    [HarmonyPostfix]
    public static void Postfix(ActModel __instance)
    {
        if (!PatchScope.IsEnabled) return;
        if (NotEnoughDifficultyConfig.RemovalOnlyActs45) return; // 勾选「只在4~5层」→ 不动 base act
        if (__instance is Act4Model or Act5Model) return; // 4/5 由替换 patch 负责

        PatchScope.Run(nameof(BaseActRemovalFilterPatch), () =>
        {
            int actIdx = MapLengthPatch.ResolveActIndex(__instance);
            if (actIdx < 1 || actIdx > 3) return; // 只处理 base act 1~3

            var excluded = ExtraActsConfig.GetExcludedIds();
            if (excluded.Count == 0) return;

            if (RoomsField?.GetValue(__instance) is not RoomSet rooms) return;

            // 同 act、各 tier 的「非排除」候选池
            var weakPool = NonExcluded(__instance.AllWeakEncounters, excluded);
            var regularPool = NonExcluded(__instance.AllRegularEncounters, excluded);
            var elitePool = NonExcluded(__instance.AllEliteEncounters, excluded);
            var bossPool = NonExcluded(__instance.AllBossEncounters, excluded);

            int n = ReplaceNormal(rooms.normalEncounters, excluded, weakPool, regularPool);
            int e = ReplaceSingleTier(rooms.eliteEncounters, excluded, elitePool);
            int b = ReplaceBoss(rooms, excluded, bossPool);

            if (n + e + b > 0)
            {
                MainFile.Logger.Info(
                    $"BaseActRemovalFilter act{actIdx}: replaced {n} normal / {e} elite / {b} boss " +
                    $"(pools non-excluded: weak={weakPool.Count} reg={regularPool.Count} " +
                    $"elite={elitePool.Count} boss={bossPool.Count})");
            }
        });
    }

    private static List<EncounterModel> NonExcluded(IEnumerable<EncounterModel> src, HashSet<string> excluded)
    {
        return src.Where(e =>
        {
            var id = e?.Id?.Entry;
            return id != null && !excluded.Contains(id);
        }).ToList();
    }

    /// <summary>normal 槽位：被排除则替换；尽量保持 IsWeak 归类。返回替换数。</summary>
    private static int ReplaceNormal(
        List<EncounterModel> list, HashSet<string> excluded,
        List<EncounterModel> weakPool, List<EncounterModel> regularPool)
    {
        int wi = 0, ri = 0, count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var id = list[i]?.Id?.Entry;
            if (id == null || !excluded.Contains(id)) continue;

            bool wasWeak = list[i].IsWeak;
            // 弱怪槽优先弱怪池，普通槽优先普通池；首选池空则退另一池；都空则保留原样（兜底）
            if (wasWeak && weakPool.Count > 0)
            {
                list[i] = weakPool[wi++ % weakPool.Count];
                count++;
            }
            else if (!wasWeak && regularPool.Count > 0)
            {
                list[i] = regularPool[ri++ % regularPool.Count];
                count++;
            }
            else if (regularPool.Count > 0)
            {
                list[i] = regularPool[ri++ % regularPool.Count];
                count++;
            }
            else if (weakPool.Count > 0)
            {
                list[i] = weakPool[wi++ % weakPool.Count];
                count++;
            }
            // 两池皆空 → 不动
        }

        return count;
    }

    /// <summary>elite 槽位：被排除则用非排除 elite 循环顶替。返回替换数。</summary>
    private static int ReplaceSingleTier(
        List<EncounterModel> list, HashSet<string> excluded, List<EncounterModel> pool)
    {
        if (pool.Count == 0) return 0; // 全排除 → 保留原样
        int k = 0, count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var id = list[i]?.Id?.Entry;
            if (id != null && excluded.Contains(id))
            {
                list[i] = pool[k++ % pool.Count];
                count++;
            }
        }

        return count;
    }

    /// <summary>act boss：被排除且有非排除 boss 则替换。返回 0/1。</summary>
    private static int ReplaceBoss(RoomSet rooms, HashSet<string> excluded, List<EncounterModel> pool)
    {
        try
        {
            var boss = rooms.Boss;
            var id = boss?.Id?.Entry;
            if (id != null && excluded.Contains(id) && pool.Count > 0)
            {
                rooms.Boss = pool[0];
                return 1;
            }
        }
        catch
        {
            // Boss 未设置等异常 → 忽略
        }

        return 0;
    }
}