using BaseLib.Config;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// Partial: 每个 act 的<b>地图长度</b>（玩家从起点爬到 boss 经过的行数，即 StandardActMap 的 _mapLength）。
///
/// ## 背景
///
/// base game <c>StandardActMap</c> 构造函数：
/// <code>
///   _mapLength = actModel.GetNumberOfRooms(isMultiplayer) + 1;
///   Grid = new MapPoint[7, _mapLength];
/// </code>
/// 地图宽固定 7 列，<b>长度（行数）完全由 GetNumberOfRooms 决定</b>。<see cref="MapLengthPatch"/>
/// 会 patch <c>GetNumberOfRooms</c>，让它返回 <c>(这里配置的行数 - 1)</c>，于是
/// <c>_mapLength = 配置行数</c>。
///
/// ## 字段语义
///
/// 每个字段 = 该 act 地图的<b>行数</b>。默认值 = 原版单人长度，即各 act 原版
/// <c>BaseNumberOfRooms + 1</c>：
///   - act1 Underdocks: 15 + 1 = 16
///   - act2 Hive:       14 + 1 = 15
///   - act3 Glory:      13 + 1 = 14
///   - act4 (本 mod):   13 + 1 = 14
///   - act5 (本 mod):   13 + 1 = 14
///
/// ## 范围 10~30
///
/// 下限 10：base game <c>StandardActMap.AssignPointTypes</c> 里有
/// <c>ForEachInRow(Grid, GetRowCount()-7, ...)</c>（固定宝箱/精英行），<c>_mapLength &lt; 7</c>
/// 会数组越界<b>崩溃</b>，<c>_mapLength == 8</c> 时该行还会与第 1 行怪物行重叠。下限 10 留足余量。
/// 上限 30：地图 grid 动态分配，无上界硬编码问题。
/// <see cref="MapLengthPatch"/> 里还会再 Clamp 一次，防止手改 cfg 文件填了越界值。
///
/// ## 多人同步
///
/// 这些字段是 <c>public static double</c> 且未标 <c>[ConfigSyncIgnore]</c>，会被 ConfigSync
/// 自动纳入 lobby 同步（进 Doubles 桶）。地图用 seeded RNG 生成，host/client 必须用<b>相同</b>
/// 的长度值才能生成相同地图——所以同步是必须的，不能标 ignore。
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