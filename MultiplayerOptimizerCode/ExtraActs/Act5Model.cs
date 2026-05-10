using System;
using System.Collections.Generic;
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
/// 视觉：借用 Glory (act 3) 的资源。
/// 内容：复用 Glory；后续 step 4 改为加权混合池。
/// 战斗调整：所有 Monster + Elite 节点战斗时抽 Boss encounter（由 CustomActEncounterReplacementPatch 实现）。
/// 顶端最终 boss 保持真 boss，由 DeduplicateCustomActBossesPatch 保证不和前 4 层重复。
/// 地图：用 StandardActMap，参数对齐 Glory。
/// 节点 UI：所有 Elite 节点改为 Monster（由 Act5MapPointTypeFixupPatch 处理），让中间节点 UI 统一为 Monster 图标。
/// </summary>
public class Act5Model : CustomActModel
{
    public Act5Model() : base(actNumber: -1)
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

    // 音乐 / 音效：act 3 (Glory) 的实际路径
    public override string[] BgMusicOptions =>
        new[] { "event:/music/act3_a1_v2", "event:/music/act3_a2_v2" };

    public override string[] MusicBankPaths =>
        new[] { "res://banks/desktop/act3_a1.bank", "res://banks/desktop/act3_a2.bank" };

    public override string AmbientSfx => "event:/sfx/ambience/act3_ambience";

    // 内容池：复用 Glory（后续混合池在 step 4）
    public override IEnumerable<EncounterModel> GenerateAllEncounters() =>
        ModelDb.Act<Glory>().AllEncounters;

    public override IEnumerable<EventModel> AllEvents =>
        ModelDb.Act<Glory>().AllEvents;

    public override IEnumerable<AncientEventModel> AllAncients =>
        Array.Empty<AncientEventModel>();

    public override IEnumerable<AncientEventModel> GetUnlockedAncients(UnlockState state) =>
        Array.Empty<AncientEventModel>();

    protected override int BaseNumberOfRooms => 13;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng)
    {
        int restCount = mapRng.NextInt(5, 7);
        int unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng) - 1;
        return new MapPointTypeCounts(unknownCount, restCount);
    }
}