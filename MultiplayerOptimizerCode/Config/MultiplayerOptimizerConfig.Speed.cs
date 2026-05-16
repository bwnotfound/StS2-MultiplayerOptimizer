using BaseLib.Config;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
///     Partial: 全局速度倍率字段（[ConfigSyncIgnore]，不参与联机同步）。
/// </summary>
internal partial class MultiplayerOptimizerConfig
{
    // ============================================================
    // 全局游戏速度（叠加在 base game FastMode 之上）
    // ============================================================
    //
    // 通过 Godot Engine.TimeScale 实现整个游戏引擎层面的统一加速——影响所有 timer / tween /
    // animation / spine 动画 / process delta。不影响真实时间（系统时间）、UI 输入响应、FMOD 音频。
    //
    // 跟 base game FastMode（None/Normal/Fast/Instant）独立叠加：例如 FastMode=Fast 让某段
    // 动画原本 0.2s，再 ×2 倍率后实际 0.1s。两者自然乘起来，无需协调。
    //
    // ## 不参与联机同步（[ConfigSyncIgnore]）
    //
    // 这两个字段标记为 [ConfigSyncIgnore]——host 不广播、client 不被覆盖，每个玩家独立设置自己
    // 看到的速度。理由：Engine.TimeScale 是纯客户端表现层（影响本地动画/timer 速度），跟游戏
    // state 演进无关：
    //   - ChecksumTracker 在回合事件触发（"After player turn start" 等），不依赖时间
    //   - 网络消息全部走 Time.GetTicksMsec()（wall-clock），不受 TimeScale 影响
    //   - PeerInputSynchronizer / HeartbeatTracker 也走 wall-clock
    //
    // 各玩家本地看到的速度不同 = 各自看到动画跑得快/慢，但事件最终都触发、state 同步。
    //
    // 应用机制见 SpeedMultiplierController.OnProcessFrame：每帧检查 config 跟 Engine.TimeScale
    // 是否一致，不一致就更新。因此本地拖 slider 立即生效（&lt; 16ms 内下一帧应用）。

    [ConfigSyncIgnore]
    [ConfigSection("Speed")]
    public static bool EnableSpeedMultiplier { get; set; } = true;

    [ConfigSyncIgnore]
    [ConfigSlider(0.5, 10.0, 0.1)]
    public static double SpeedMultiplier { get; set; } = 2.0;
}