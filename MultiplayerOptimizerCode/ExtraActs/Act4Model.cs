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
/// 第 4 层。
///
/// 视觉：地图背景/休息站借用 Hive (act 2)，平时 BGM 也用 act 2。
/// 内容：Encounter/Event/Boss 池目前直接复用 Glory (act 3)；后续 step 4 改为加权混合池。
/// 战斗调整：所有节点 PointType 强制为 Elite（由 MapPointTypeFixupPatch 实现），
///           战斗实际抽 eliteEncounters。第 4 层全是精英战。
/// 顶端 boss：保留为标准 boss，从 Glory boss 池避开重复（DeduplicateCustomActBossesPatch）。
///
/// 为什么 MusicBankPaths 同时加载 act 2 + act 3：
///   - 平时 BGM 用 act 2 event（保持 Hive 风格的氛围）
///   - 但 boss EncounterModel 自带 CustomBgm（如 QueenBoss = "event:/music/act3_boss_queen"），
///     这个 FMOD event 在 act 3 banks 里
///   - 不加载 act 3 banks 的话，boss 战会报 "cannot find music path" 且没 BGM
///   - 同时加载两组 bank 是必要代价
/// </summary>
public class Act4Model : CustomActModel
{
    public Act4Model() : base(actNumber: -1)
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

    // 平时 BGM 用 act 2
    public override string[] BgMusicOptions =>
        new[] { "event:/music/act2_a1_v2", "event:/music/act2_a2_v2" };

    // 同时加载 act2 + act3 banks（act3 给 boss CustomBgm 用）
    public override string[] MusicBankPaths => new[]
    {
        "res://banks/desktop/act2_a1.bank",
        "res://banks/desktop/act2_a2.bank",
        "res://banks/desktop/act3_a1.bank",
        "res://banks/desktop/act3_a2.bank",
    };

    public override string AmbientSfx => "event:/sfx/ambience/act2_ambience";

    // 内容池：复用 Glory
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