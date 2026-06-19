using BaseLib.Config;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// Partial: 每个 act 的<b>地图长度</b>（玩家从起点爬到 boss 经过的行数）。
///
/// ## 背景
///
/// base game <c>StandardActMap</c> 构造函数：
/// <code>
///   _mapLength = actModel.GetNumberOfRooms(isMultiplayer) + 1;
///   Grid = new MapPoint[7, _mapLength];
/// </code>
/// 地图宽固定 7 列，长度（行数）完全由 <c>GetNumberOfRooms</c> 决定。
/// <see cref="MapLengthPatch"/> 会 patch <c>GetNumberOfRooms</c>，按这里的配置覆盖返回值。
///
/// ## 字段语义
///
/// 每个字段 = 该 act <b>单人模式</b>下的地图行数。默认值 = 原版单人长度，
/// 即各 act 原版 <c>BaseNumberOfRooms + 1</c>：
///   - act1 Underdocks: 15 + 1 = 16
///   - act2 Hive:       14 + 1 = 15
///   - act3 Glory:      13 + 1 = 14
///   - act4 (本 mod):   13 + 1 = 14
///   - act5 (本 mod):   13 + 1 = 14
///
/// <b>多人模式</b>下地图按 base game 一贯规律比单人少 1 行（原版多人地图本来就比
/// 单人短 1）。即 config 填 16 时，单人 16 行、多人 15 行。这样设计是为了让
/// "默认配置"在单人和多人下都精确等于原版——详见 <see cref="MapLengthPatch"/> 的说明。
///
/// ## 范围 10~30
///
/// 下限 10：base game <c>StandardActMap.AssignPointTypes</c> 里有
/// <c>ForEachInRow(Grid, GetRowCount()-7, ...)</c>（固定宝箱/精英行），<c>_mapLength &lt; 7</c>
/// 会数组越界崩溃。下限 10（多人 <c>_mapLength</c> 仍有 9）留足余量。
/// 上限 30：地图 grid 动态分配，无上界硬编码问题。
/// <see cref="MapLengthPatch"/> 里还会再 Clamp 一次，防止手改 cfg 文件填了越界值。
///
/// ## 多人同步
///
/// 这些字段是 <c>public static double</c> 且未标 <c>[ConfigSyncIgnore]</c>，会被 ConfigSync
/// 自动纳入 lobby 同步。地图与 encounter 都用 seeded RNG 生成，host/client 必须用相同
/// 长度值才能生成相同内容，否则会 desync——所以同步是必须的。
/// <b>前提</b>：这要求 host/client 装<b>相同版本</b>的 mod（两端都得有这些字段、都得有
/// <see cref="MapLengthPatch"/>）。只要有一端是没有本功能的旧版，而另一端改过地图长度，
/// 两端 <c>GetNumberOfRooms</c> 就会不一致 → encounter 生成错位 → desync。
///
/// ## UI
///
/// 带 <c>[ConfigSlider]</c> 的字段，BaseLib 会在 mod 配置界面自动生成对应滑块。
/// </summary>
internal partial class MultiplayerOptimizerConfig
{
    [ConfigSection("MapLength")]
    [ConfigSlider(MapLengthPatch.MinRows, MapLengthPatch.MaxRows, 1)]
    public static double Act1_MapLength { get; set; } = 16;

    [ConfigSlider(MapLengthPatch.MinRows, MapLengthPatch.MaxRows, 1)]
    public static double Act2_MapLength { get; set; } = 15;

    [ConfigSlider(MapLengthPatch.MinRows, MapLengthPatch.MaxRows, 1)]
    public static double Act3_MapLength { get; set; } = 14;

    [ConfigSlider(MapLengthPatch.MinRows, MapLengthPatch.MaxRows, 1)]
    public static double Act4_MapLength { get; set; } = 14;

    [ConfigSlider(MapLengthPatch.MinRows, MapLengthPatch.MaxRows, 1)]
    public static double Act5_MapLength { get; set; } = 14;
}