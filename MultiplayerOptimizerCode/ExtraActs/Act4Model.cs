using System;
using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Unlocks;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 第 4 层。
///
/// 视觉/音乐：背景借 act 2 (Hive)，平时 BGM 用 act 2，同时加载 act 3 banks 给 boss CustomBgm 用。
/// 关卡：所有节点 PointType 强制为 Elite（由 MapPointTypeFixupPatch 实现），战斗实际抽 eliteEncounters。
///
/// Encounter 池（在本类中处理）：
///   - non-boss encounters: 复用 Glory 的（实际 elite 战斗内容由 CustomActEncounterReplacementPatch 用
///     加权混合池替换，所以这里返回什么不重要）
///   - boss encounters: 把 act1+2+3 的 boss encounter 按权重重复添加进 AllEncounters，
///     这样 act.AllBossEncounters（继承自 ActModel 的 filter 属性）自带权重，
///     DeduplicateCustomActBossesPatch 用 rng.NextItem 抽样时等价加权抽样
///
/// Event 池：用 EncounterListBuilder.BuildWeightedFlatList 加权混合 act1+2+3 的事件池。
///
/// Ancient 池：复用 Glory（每个 act 的起始节点都是 MapPointType.Ancient，UI 渲染为 NAncientMapPoint,
/// 它在 _Ready 里需要读 _runState.Act.Ancient.MapIcon。如果 AllAncients 为空，
/// _rooms.Ancient 会未填充，渲染时 NullReferenceException 卡住地图）。
///
/// 顶端 boss 不与前 3 层 boss 重复 + 应用 boss 池过滤开关（如 ExcludeDoormakerFromBossPool）
/// 由 DeduplicateCustomActBossesPatch 保证。注意 boss 池过滤**不能放在 GenerateAllEncounters 里**——
/// ActModel.AllEncounters 是 lazy 缓存，加载时跑一次后不再重新生成，运行时改开关不会生效。
/// </summary>
public class Act4Model : CustomActModel
{
    public Act4Model() : base(-1)
    {
    }

    // 地图背景图：Hive
    protected override string CustomMapTopBgPath =>
        ImageHelper.GetImagePath("packed/map/map_bgs/hive/map_top_hive.png");

    protected override string CustomMapMidBgPath =>
        ImageHelper.GetImagePath("packed/map/map_bgs/hive/map_middle_hive.png");

    protected override string CustomMapBotBgPath =>
        ImageHelper.GetImagePath("packed/map/map_bgs/hive/map_bottom_hive.png");

    protected override string CustomRestSiteBackgroundPath =>
        SceneHelper.GetScenePath("rest_site/hive_rest_site");

    public override string[] BgMusicOptions =>
        new[] { "event:/music/act2_a1_v2", "event:/music/act2_a2_v2" };

    // act2 banks 给平时 BGM，act3 banks 给 boss CustomBgm 用
    public override string[] MusicBankPaths => new[]
    {
        "res://banks/desktop/act2_a1.bank",
        "res://banks/desktop/act2_a2.bank",
        "res://banks/desktop/act3_a1.bank",
        "res://banks/desktop/act3_a2.bank"
    };

    public override string AmbientSfx => "event:/sfx/ambience/act2_ambience";

    public override IEnumerable<EncounterModel> GenerateAllEncounters()
    {
        var result = new List<EncounterModel>();

        // 1) Non-boss encounters: 复用 Glory（实际 elite 战内容由 patch 替换）
        foreach (var e in ModelDb.Act<Glory>().AllEncounters)
            if (e.RoomType != RoomType.Boss)
                result.Add(e);

        // 2) Boss encounters: 按权重重复添加 act1+2+3 的 boss
        // 注意这里**不**调用 ApplyBossPoolFilters——AllEncounters 是 lazy 缓存（mod 加载时跑一次后定型），
        // 在这里过滤会让运行时的开关变化不生效。boss 池过滤放在 DeduplicateCustomActBossesPatch 里做。
        var bossWeights = ExtraActsConfig.GetBossWeights(4);
        var weightedBossPools = new List<(IReadOnlyList<EncounterModel>, double)>
        {
            (ModelDb.Act<Overgrowth>().AllBossEncounters.ToList(), bossWeights.Act1),
            (ModelDb.Act<Hive>().AllBossEncounters.ToList(), bossWeights.Act2),
            (ModelDb.Act<Glory>().AllBossEncounters.ToList(), bossWeights.Act3)
        };
        // baseFactor 取较小值（30）避免 boss 列表过长——boss 抽样只需要权重比例正确即可
        result.AddRange(EncounterListBuilder.BuildWeightedFlatList(weightedBossPools, 30));

        return result;
    }

    public override IEnumerable<EventModel> AllEvents
    {
        get
        {
            var w = ExtraActsConfig.GetEventWeights(4);
            var weightedEventPools = new List<(IReadOnlyList<EventModel>, double)>
            {
                (ModelDb.Act<Overgrowth>().AllEvents.ToList(), w.Act1),
                (ModelDb.Act<Hive>().AllEvents.ToList(), w.Act2),
                (ModelDb.Act<Glory>().AllEvents.ToList(), w.Act3)
            };
            return EncounterListBuilder.BuildWeightedFlatList(weightedEventPools);
        }
    }

    // Ancient 池：复用 Glory。这是必需的——map 起始节点是 MapPointType.Ancient，
    // 渲染时需要 _runState.Act.Ancient 不为 null。返回空集合会导致进入 act 时卡死。
    public override IEnumerable<AncientEventModel> AllAncients =>
        ModelDb.Act<Glory>().AllAncients;

    public override IEnumerable<AncientEventModel> GetUnlockedAncients(UnlockState state)
    {
        return ModelDb.Act<Glory>().GetUnlockedAncients(state);
    }

    protected override int BaseNumberOfRooms => 13;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng)
    {
        var restCount = mapRng.NextInt(5, 7);
        var unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng) - 1;
        return new MapPointTypeCounts(unknownCount, restCount);
    }
}