using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Config;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 联机配置同步状态管理器。
///
/// ## 整体状态机
///
///   IsActive=false （默认）：所有 patch 用本地 config，Save/Load 正常
///   ↓ client 收到 ConfigSyncMessage（在 BeginRunLocally 之前）
///   IsActive=true：本地静态字段已被 host 值覆盖，Save 被 WeightNormalizationPatch 拦截
///   ↓ RunManager.CleanUpRun（run 结束）
///   IsActive=false：从磁盘 reload 恢复 client 原配置
///
/// host 自己 IsActive 永远是 false——它用本地 config 即可，且需要正常保存。
///
/// ## v0.2.0 新增：host 侧 ack 跟踪
///
/// host 在 broadcast ConfigSyncMessage 时调 <see cref="RegisterPending"/> 分配 syncId 并记录所有 client；
/// 每收到一个 <see cref="ConfigSyncAckMessage"/> 调 <see cref="RecordAck"/> 从 pending 中移除该 client。
/// host 调 <see cref="WaitForAcksAsync"/> 异步等所有 ack（带超时）——超时即说明那个 client 的
/// mod 版本太旧（没有 EarlyRegister patch 收不到 wrapper 消息），host 端 popup 警告并拒绝开 run。
/// </summary>
internal static class ConfigSyncManager
{
    /// <summary>
    /// true 表示当前 client 的 MultiplayerOptimizerConfig 字段已被 host 配置覆盖。
    /// WeightNormalizationPatch 用它判断是否拦截 Save（避免把 host 值写到 client 磁盘）。
    /// </summary>
    public static bool IsActive { get; private set; }

    /// <summary>
    /// 当前激活的 NetService 引用。在 lobby 创建时由 EarlyRegister patch 设置，
    /// run 启动后跟 RunManager.NetService 是同一个对象（INetGameService 在 lobby 和 run 间共用）。
    ///
    /// **为什么不用 RunManager.NetService**：RunManager.NetService 在 InitializeShared 才被赋值，
    /// 那是 run 启动后的事。但 ConfigSync 的整个 lifecycle 都在 lobby 阶段——broadcast、apply、
    /// ack 都发生在 RunManager.NetService 还是 null 的时候。如果 SendAck / IsLocalHost 依赖
    /// RunManager.NetService，lobby 期收到消息时找不到 NetService，sync 完全失效。
    /// </summary>
    private static INetGameService? _activeNetService;

    /// <summary>EarlyRegister patch 在 lobby 创建时调用，让 ConfigSync 在 lobby 期就能拿到 NetService。</summary>
    public static void SetActiveNetService(INetGameService netService)
    {
        _activeNetService = netService;
    }

    private static INetGameService? CurrentNetService =>
        _activeNetService ?? RunManager.Instance?.NetService;

    /// <summary>
    /// 当前是否是 host 端。优先用 lobby 注册的 NetService；fallback 到 RunManager.NetService。
    /// </summary>
    public static bool IsLocalHost()
    {
        var net = CurrentNetService;
        return net != null && net.Type == NetGameType.Host;
    }

    // ============================================================
    // Apply（client 侧）
    // ============================================================

    /// <summary>Apply 返回值，用于 client 生成 ack。</summary>
    public readonly struct ApplyResult
    {
        public int AppliedDoubles { get; init; }
        public int SkippedDoubles { get; init; }
        public int AppliedBools { get; init; }
        public int SkippedBools { get; init; }
    }

    /// <summary>
    /// 把消息里携带的 host 配置 apply 到本地静态字段。
    /// 找不到对应 property 的 key 会被 ignore（mod 版本兼容）。
    /// </summary>
    public static ApplyResult Apply(ConfigSyncMessage msg)
    {
        var configType = typeof(MultiplayerOptimizerConfig);

        // 版本对比 log——方便用户排查
        if (msg.HostModVersion != MainFile.ModVersion)
        {
            MainFile.Logger.Warn(
                $"[Sync] Mod version mismatch: host='{msg.HostModVersion}' vs me='{MainFile.ModVersion}'. " +
                "Trying to apply host config field-by-field; unknown fields will be ignored.");
        }

        var doublesApplied = 0;
        var doublesSkipped = 0;
        foreach (var kv in msg.Doubles)
        {
            var prop = configType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(double))
            {
                doublesSkipped++;
                continue;
            }

            try
            {
                prop.SetValue(null, kv.Value);
                doublesApplied++;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[Sync] Failed to apply {kv.Key}={kv.Value}: {ex.Message}");
                doublesSkipped++;
            }
        }

        var boolsApplied = 0;
        var boolsSkipped = 0;
        foreach (var kv in msg.Bools)
        {
            var prop = configType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(bool))
            {
                boolsSkipped++;
                continue;
            }

            try
            {
                prop.SetValue(null, kv.Value);
                boolsApplied++;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[Sync] Failed to apply {kv.Key}={kv.Value}: {ex.Message}");
                boolsSkipped++;
            }
        }

        IsActive = true;
        MainFile.Logger.Info(
            $"[Sync] Applied host config (syncId={msg.SyncId}, hostVersion={msg.HostModVersion}): " +
            $"{doublesApplied}/{msg.Doubles.Count} doubles, {boolsApplied}/{msg.Bools.Count} bools " +
            $"(skipped {doublesSkipped} doubles + {boolsSkipped} bools as unknown to this version)");

        return new ApplyResult
        {
            AppliedDoubles = doublesApplied,
            SkippedDoubles = doublesSkipped,
            AppliedBools = boolsApplied,
            SkippedBools = boolsSkipped,
        };
    }

    /// <summary>Client 端 apply 完后回 ack 给 host。</summary>
    public static void SendAck(ulong syncId, ApplyResult result)
    {
        var net = CurrentNetService;
        if (net == null)
        {
            MainFile.Logger.Warn(
                "[Sync] Cannot send ack: no active NetService (lobby not registered, RunManager not initialized). " +
                "This shouldn't happen if EarlyRegister patch ran. ConfigSync will not complete.");
            return;
        }

        var ack = new ConfigSyncAckMessage
        {
            SyncId = syncId,
            ClientModVersion = MainFile.ModVersion,
            AppliedDoubles = result.AppliedDoubles,
            SkippedDoubles = result.SkippedDoubles,
            AppliedBools = result.AppliedBools,
            SkippedBools = result.SkippedBools,
        };
        net.SendMessage(new CustomMessageWrapper { Message = ack });
        MainFile.Logger.Info($"[Sync] Sent ack for syncId={syncId} to host");
    }

    /// <summary>
    /// 从磁盘 reload 恢复 client 原配置，并清除 active 标记。
    /// 调用安全：IsActive=false 时是 no-op（host 端 / 单机也调用此方法）。
    /// </summary>
    public static void Restore()
    {
        if (!IsActive) return;

        try
        {
            ModConfig.Load<MultiplayerOptimizerConfig>();
            MainFile.Logger.Info("[Sync] Restored client config from disk");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[Sync] Failed to reload config from disk: {ex.Message}");
        }
        finally
        {
            IsActive = false;
        }
    }

    // ============================================================
    // Host 端 ack 跟踪
    // ============================================================

    /// <summary>
    /// Host 端进入 BeginRunForAllPlayers 时设为 true，避免我们的 patch 在异步任务回调原方法时重入。
    /// </summary>
    public static bool IsReenteringBeginRun { get; set; }

    private static ulong _nextSyncId = 1;
    private static readonly Dictionary<ulong, PendingSync> _pendingSyncs = new();

    private sealed class PendingSync
    {
        public HashSet<ulong> RemainingClients = new();
        public Dictionary<ulong, ConfigSyncAckMessage> ReceivedAcks = new();
        public TaskCompletionSource<bool> Completion = new();
    }

    /// <summary>等 ack 的结果。</summary>
    public sealed class AckResult
    {
        public bool AllAcked { get; init; }
        public IReadOnlyCollection<ulong> MissingClients { get; init; } = Array.Empty<ulong>();

        public IReadOnlyDictionary<ulong, ConfigSyncAckMessage> ReceivedAcks { get; init; } =
            new Dictionary<ulong, ConfigSyncAckMessage>();
    }

    /// <summary>
    /// Host 端：分配新 syncId，记录 pending 的 client 集合。clientIds 不应包含 host 自己。
    /// 如果 clientIds 为空（单人 lobby），立即标记为已完成。
    /// </summary>
    public static ulong RegisterPending(IEnumerable<ulong> clientIds)
    {
        var syncId = _nextSyncId++;
        var pending = new PendingSync
        {
            RemainingClients = new HashSet<ulong>(clientIds)
        };
        _pendingSyncs[syncId] = pending;

        if (pending.RemainingClients.Count == 0)
        {
            pending.Completion.TrySetResult(true);
        }

        return syncId;
    }

    /// <summary>
    /// Host 端：收到 client 的 ack 时调用，从对应 pending 移除该 client。
    /// 全部 ack 收齐后自动解锁 WaitForAcksAsync。
    /// </summary>
    public static void RecordAck(ConfigSyncAckMessage ack, ulong senderId)
    {
        if (!_pendingSyncs.TryGetValue(ack.SyncId, out var pending))
        {
            MainFile.Logger.Warn(
                $"[Sync] Received ack for unknown syncId={ack.SyncId} from {senderId} " +
                $"(version={ack.ClientModVersion})");
            return;
        }

        pending.RemainingClients.Remove(senderId);
        pending.ReceivedAcks[senderId] = ack;

        var versionStr = ack.ClientModVersion == MainFile.ModVersion
            ? ack.ClientModVersion
            : $"{ack.ClientModVersion} (host is {MainFile.ModVersion})";
        MainFile.Logger.Info(
            $"[Sync] Received ack for syncId={ack.SyncId} from {senderId} version={versionStr}, " +
            $"applied {ack.AppliedDoubles}D+{ack.AppliedBools}B, skipped {ack.SkippedDoubles}D+{ack.SkippedBools}B");

        if (pending.RemainingClients.Count == 0)
        {
            pending.Completion.TrySetResult(true);
        }
    }

    /// <summary>
    /// Host 端：异步等待所有 client ack；超时返回部分结果。
    /// 调用方拿到 AckResult 后清理 pending（避免泄漏）。
    /// </summary>
    public static async Task<AckResult> WaitForAcksAsync(ulong syncId, int timeoutMs)
    {
        if (!_pendingSyncs.TryGetValue(syncId, out var pending))
        {
            return new AckResult { AllAcked = false };
        }

        try
        {
            var timeoutTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(pending.Completion.Task, timeoutTask);

            var allAcked = completed == pending.Completion.Task;
            return new AckResult
            {
                AllAcked = allAcked,
                MissingClients = new List<ulong>(pending.RemainingClients),
                ReceivedAcks = new Dictionary<ulong, ConfigSyncAckMessage>(pending.ReceivedAcks),
            };
        }
        finally
        {
            _pendingSyncs.Remove(syncId);
        }
    }

    /// <summary>测试钩子：检查指定 syncId 的 pending 是否存在（防止单元测试或调试需要）。</summary>
    public static bool HasPending(ulong syncId) => _pendingSyncs.ContainsKey(syncId);
}