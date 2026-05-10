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
using MegaCrit.Sts2.Core.Unlocks;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 第 5 层。
///
/// 视觉/音乐：背景借 act 3 (Glory)，BGM 也是 act 3。
/// 关卡：中部所有节点都是 boss 战内容（PointType=Monster + RoomType 被 mask 为 Monster），
/// 顶端是真 boss。
///
/// Encounter 池：
///   - 不混合 AllBossEncounters（保持 = Glory boss 池），因为需求 5.3 要求最终 boss 必须从 act3 抽
///   - 中部 boss 战的内容混合在 CustomActEncounterReplacementPatch 里单独处理，
///     直接从 act1+2+3 boss 池构造混合 list 填进 normalEncounters/eliteEncounters
///
/// Event 池：跟 Act4 同样，用 BuildWeightedFlatList 加权混合 act1+2+3 的事件池。
///
/// Ancient 池：复用 Glory。这是必需的——map 起始节点是 MapPointType.Ancient，渲染时需要
/// _runState.Act.Ancient 不为 null，否则进入 act 时 UI 卡死。
/// </summary>
public class Act5Model : CustomActModel
{
    public Act5Model() : base(-1)
    {
    }

    // 地图背景图：Glory
    protected override string CustomMapTopBgPath =>
        ImageHelper.GetImagePath("packed/map/map_bgs/glory/map_top_glory.png");

    protected override string CustomMapMidBgPath =>
        ImageHelper.GetImagePath("packed/map/map_bgs/glory/map_middle_glory.png");

    protected override string CustomMapBotBgPath =>
        ImageHelper.GetImagePath("packed/map/map_bgs/glory/map_bottom_glory.png");

    protected override string CustomRestSiteBackgroundPath =>
        SceneHelper.GetScenePath("rest_site/glory_rest_site");

    public override string[] BgMusicOptions =>
        new[] { "event:/music/act3_a1_v2", "event:/music/act3_a2_v2" };

    public override string[] MusicBankPaths =>
        new[] { "res://banks/desktop/act3_a1.bank", "res://banks/desktop/act3_a2.bank" };

    public override string AmbientSfx => "event:/sfx/ambience/act3_ambience";

    // 关键：保持 = Glory.AllEncounters，不混合 boss
    // Act5 最终 boss 必须从 act3 boss 池抽（需求 5.3）
    public override IEnumerable<EncounterModel> GenerateAllEncounters()
    {
        return ModelDb.Act<Glory>().AllEncounters;
    }

    public override IEnumerable<EventModel> AllEvents
    {
        get
        {
            var w = ExtraActsConfig.GetEventWeights(5);
            var weightedEventPools = new List<(IReadOnlyList<EventModel>, double)>
            {
                (ModelDb.Act<Overgrowth>().AllEvents.ToList(), w.Act1),
                (ModelDb.Act<Hive>().AllEvents.ToList(), w.Act2),
                (ModelDb.Act<Glory>().AllEvents.ToList(), w.Act3)
            };
            return EncounterListBuilder.BuildWeightedFlatList(weightedEventPools);
        }
    }

    // Ancient 池：复用 Glory（同 Act4 注释，避免起始节点崩）
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