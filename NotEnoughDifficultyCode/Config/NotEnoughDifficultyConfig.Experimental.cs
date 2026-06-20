using BaseLib.Config;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// Partial: 实验性配置。
///
/// 这个 section 下的功能尚在试验阶段——它们通常是为了绕过 base game 的某些脆弱点，
/// 行为可能不像核心功能那样稳定，或在后续版本里调整。默认全部关闭。
/// </summary>
internal partial class NotEnoughDifficultyConfig
{
    /// <summary>
    /// 【实验性】让 ModelDb hash 的计算变成确定性的，缓解联机握手时的
    /// "ModelDb hash mismatch" 报错。
    ///
    /// ## 背景
    ///
    /// base game 计算 ModelDb hash 时，把所有 AbstractModel 子类型按 Type.Name 用
    /// <c>List.Sort</c>（不稳定排序）排序。而 base game 自己有同名类型
    /// （Byrdpip / LostWisp / PaelsLegion，各有一个 Monsters.* 和一个 Relics.* 版本）。
    /// 不稳定排序对这些同名类型的先后顺序不固定，取决于待排序列表的内容——装上任何
    /// 加 model 的 mod 都会改变这个列表，使两台机器算出不同的 hash，握手被拒。
    ///
    /// 启用本开关后，<see cref="DeterministicModelHashPatch"/> 会把那次排序换成全序
    /// （Type.Name 相同时再用完整命名空间 tiebreak），排序结果唯一确定、与输入顺序无关。
    ///
    /// ## ⚠️ 使用须知
    ///
    /// - 启用后 ModelDb hash 的算法被改变。<b>联机双方必须都装本 mod 且都启用此开关</b>，
    ///   双方才用同一套规则算 hash；一方开一方不开会照样 hash 不一致。
    /// - hash 在游戏启动时（连接握手前）就算好了。<b>改此开关后必须重启游戏才生效。</b>
    /// - 本字段标记 <c>[ConfigSyncIgnore]</c>：它是纯本地设置。ConfigSync 发生在 lobby
    ///   阶段，远晚于 hash 计算，无法用同步解决；只能联机双方各自在本地开启。
    /// - 这是绕过 base game 脆弱点的兜底手段，不是修 base game 本身。最稳妥的做法仍是
    ///   让联机双方的 mod 环境逐字节一致；本功能用于"难以保证完全一致"的场景。
    /// </summary>
    [ConfigSyncIgnore]
    [ConfigSection("Experimental")]
    public static bool DeterministicModelHash { get; set; } = false;
}