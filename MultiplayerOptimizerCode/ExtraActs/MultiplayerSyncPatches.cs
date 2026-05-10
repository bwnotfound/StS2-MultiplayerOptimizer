using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// Host 端：在广播 LobbyBeginRunMessage 之前先 broadcast 自己的 mod 配置，
/// 让所有 client 在 BeginRunLocally 之前完成 config sync。
///
/// 时序保证：传输是 reliable + ordered，host 在 BeginRunForAllPlayers 内连续 SendMessage：
///   1. （我们的 prefix）broadcast ConfigSyncMessage
///   2. （原方法 line 399）broadcast LobbyBeginRunMessage
/// client 一定按顺序收到 1 → 2，HandleMessage 1 把字段覆盖完毕，再处理 2 的 BeginRunLocally。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
public static class HostBroadcastConfigPatch
{
    [HarmonyPrefix]
    public static void BroadcastBeforeBeginRun(StartRunLobby __instance)
    {
        // 单机模式：NetSingleplayerGameService 的 SendMessage 是 no-op，但既然是单机，跳过省事
        // Host：是我们要的路径
        // Client：BeginRunForAllPlayers 不会被 client 调（line 382-385 throw），保险起见也跳过
        if (__instance.NetService.Type != NetGameType.Host) return;

        var msg = ConfigSyncMessage.CaptureCurrent();
        var wrapper = new CustomMessageWrapper { Message = msg };
        __instance.NetService.SendMessage(wrapper);

        MainFile.Logger.Info(
            $"[Sync] Host broadcasting config to clients " +
            $"({msg.Doubles.Count} doubles + {msg.Bools.Count} bools)");
    }
}

/// <summary>
/// 任意 run 结束（胜利/失败/退出/断线）→ 恢复 client 原配置。
/// RunManager.CleanUp 是 RunManager 的统一清理入口，所有 run 结束路径都会跑。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
public static class RestoreConfigOnRunEndPatch
{
    [HarmonyPostfix]
    public static void RestoreAfterRunEnd()
    {
        // ConfigSyncManager.Restore 内部会检查 IsActive：
        //   - host 端 / 单机端：IsActive=false，无操作
        //   - client 端：IsActive=true，从磁盘 reload 恢复原配置
        ConfigSyncManager.Restore();
    }
}