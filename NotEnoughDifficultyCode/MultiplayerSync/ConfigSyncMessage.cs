using System.Reflection;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     标记 <see cref="NotEnoughDifficultyConfig" /> 的 property 不参与 ConfigSync——
///     即 host 不会广播给 client，client 也不会被 host 的值覆盖，各玩家保持各自的本地值。
///     适用场景：<b>纯本地客户端表现设置，跟游戏 state 演进无关</b>，玩家之间不一致也不会破坏游戏正确性。
///     当前用例：<c>EnableSpeedMultiplier</c> / <c>SpeedMultiplier</c>——通过 <c>Engine.TimeScale</c>
///     控制本地引擎时间，每个玩家看到的动画速度不同但事件层面完全同步（checksum 触发点都是回合事件，
///     网络消息走 wall-clock 时间）。
///     <b>不要</b>标记影响游戏 state 的字段（HP/Dmg 倍率、boss 池权重等），否则 host/client 数值不一致
///     会触发 ChecksumTracker desync 检测。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ConfigSyncIgnoreAttribute : Attribute
{
}

/// <summary>
///     把 host 的 NotEnoughDifficultyConfig 字段值打包广播给所有 client。
///     流程：
///     - host 在 BeginRunForAllPlayers 之前调 CaptureCurrent() 构造消息，broadcast
///     - 因为传输是 reliable+ordered，client 一定在 LobbyBeginRunMessage 之前收到这条
///     - client HandleMessage 把字段值 apply 到本地静态字段（不写磁盘），然后回 ConfigSyncAckMessage
///     - host 端等待所有 client ack；超时则 popup 拒绝开 run（见 MultiplayerSyncPatches）
///     - run 结束时（RunManager.CleanUp postfix）调 ConfigSyncManager.Restore 从磁盘
///     reload 恢复 client 原配置
///     序列化用 (字段名, 值) 字典而不是固定顺序数组——这样 mod 版本之间字段增删时也能容错：
///     反序列化时找不到对应 property 的 key 会被 ignore；缺失的 key 保持 client 当前值。
///     SyncId 和 HostModVersion 是 v0.2.0 新增字段，旧 client 拿不到这条消息（没 wrapper handler），
///     所以不需要 wire-format 后向兼容。
///     注意 ShouldBuffer = false：BaseLib 默认 buffer 用于 in-game 消息，我们这条是 lobby 期，
///     不能 buffer 否则会被推迟到 run 启动后处理——那时 BeginRunLocally 已经跑完了。
/// </summary>
public sealed class ConfigSyncMessage : ICustomMessage
{
    public Dictionary<string, double> Doubles { get; } = new();

    public Dictionary<string, bool> Bools { get; } = new();

    // string 通道：重构后新增了 string 配置（ExcludedEncounterIdsCsv 移除列表）。
    // 早期版本只有 double/bool，导致移除列表静默不同步 → host/client encounter 池不一致 → desync。
    public Dictionary<string, string> Strings { get; } = new();

    /// <summary>host 端分配的 sync 标识，client ack 时回带，host 用来匹配 pending 状态。</summary>
    public ulong SyncId { get; set; }

    /// <summary>host 的 mod 版本号，仅用于 log 和 popup 显示，不参与计算。</summary>
    public string HostModVersion { get; set; } = "";

    public bool ShouldBroadcast => true;
    public bool ShouldBuffer => false; // lobby 阶段消息，不 buffer
    public NetTransferMode Mode => NetTransferMode.Reliable;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(SyncId);
        writer.WriteString(HostModVersion);

        writer.WriteInt(Doubles.Count);
        foreach (var kv in Doubles)
        {
            writer.WriteString(kv.Key);
            writer.WriteDouble(kv.Value);
        }

        writer.WriteInt(Bools.Count);
        foreach (var kv in Bools)
        {
            writer.WriteString(kv.Key);
            writer.WriteBool(kv.Value);
        }

        writer.WriteInt(Strings.Count);
        foreach (var kv in Strings)
        {
            writer.WriteString(kv.Key);
            writer.WriteString(kv.Value);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        Doubles.Clear();
        Bools.Clear();
        Strings.Clear();

        SyncId = reader.ReadULong();
        HostModVersion = reader.ReadString();

        var dc = reader.ReadInt();
        for (var i = 0; i < dc; i++)
        {
            var k = reader.ReadString();
            var v = reader.ReadDouble();
            Doubles[k] = v;
        }

        var bc = reader.ReadInt();
        for (var i = 0; i < bc; i++)
        {
            var k = reader.ReadString();
            var v = reader.ReadBool();
            Bools[k] = v;
        }

        var sc = reader.ReadInt();
        for (var i = 0; i < sc; i++)
        {
            var k = reader.ReadString();
            var v = reader.ReadString();
            Strings[k] = v;
        }
    }

    /// <summary>
    ///     Client 端收到时调用——apply 配置到本地静态字段，然后回 ack 给 host。
    ///     Host 端自己 broadcast 时如果 BaseLib echo back（通常不会）会被 IsLocalHost 跳过。
    /// </summary>
    public void HandleMessage(ulong senderId)
    {
        if (ConfigSyncManager.IsLocalHost())
        {
            MainFile.Logger.Info("[Sync] Host received own config sync message, ignored");
            return;
        }

        var result = ConfigSyncManager.Apply(this);
        ConfigSyncManager.SendAck(SyncId, result);
    }

    /// <summary>构造一条消息，把当前 NotEnoughDifficultyConfig 的所有 static double/bool 字段塞进去。</summary>
    public static ConfigSyncMessage CaptureCurrent()
    {
        var msg = new ConfigSyncMessage
        {
            HostModVersion = MainFile.ModVersion
        };
        var props = typeof(NotEnoughDifficultyConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Static);

        foreach (var prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            // 跳过明确标记不同步的字段（如本地 SpeedMultiplier——纯客户端表现，各玩家独立）
            if (prop.IsDefined(typeof(ConfigSyncIgnoreAttribute), false)) continue;
            var value = prop.GetValue(null);
            if (value is double d) msg.Doubles[prop.Name] = d;
            else if (value is bool b) msg.Bools[prop.Name] = b;
            else if (value is string s) msg.Strings[prop.Name] = s;
            // 其他类型（enum 等）若未来新增，这里要继续扩展并同步 wire format + 版本号。
        }

        return msg;
    }
}