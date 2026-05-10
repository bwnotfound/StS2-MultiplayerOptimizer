using System.Collections.Generic;
using System.Reflection;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 把 host 的 MultiplayerOptimizerConfig 字段值打包广播给所有 client。
///
/// 流程：
///   - host 在 BeginRunForAllPlayers 之前调 CaptureCurrent() 构造消息，broadcast
///   - 因为传输是 reliable+ordered，client 一定在 LobbyBeginRunMessage 之前收到这条
///   - client HandleMessage 把字段值 apply 到本地静态字段（不写磁盘）
///   - run 结束时（RunManager.CleanUp postfix）调 ConfigSyncManager.Restore 从磁盘
///     reload 恢复 client 原配置
///
/// 序列化用 (字段名, 值) 字典而不是固定顺序数组——这样 mod 版本之间字段增删时也能容错：
/// 反序列化时找不到对应 property 的 key 会被 ignore；缺失的 key 保持 client 当前值。
///
/// 注意 ShouldBuffer = false：BaseLib 默认 buffer 用于 in-game 消息，我们这条是 lobby 期，
/// 不能 buffer 否则会被推迟到 run 启动后处理——那时 BeginRunLocally 已经跑完了。
/// </summary>
public sealed class ConfigSyncMessage : ICustomMessage
{
    public Dictionary<string, double> Doubles { get; } = new();
    public Dictionary<string, bool> Bools { get; } = new();

    public bool ShouldBroadcast => true;
    public bool ShouldBuffer => false; // lobby 阶段消息，不 buffer
    public NetTransferMode Mode => NetTransferMode.Reliable;

    /// <summary>构造一条消息，把当前 MultiplayerOptimizerConfig 的所有 static double/bool 字段塞进去。</summary>
    public static ConfigSyncMessage CaptureCurrent()
    {
        var msg = new ConfigSyncMessage();
        var props = typeof(MultiplayerOptimizerConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Static);

        foreach (var prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            var value = prop.GetValue(null);
            if (value is double d) msg.Doubles[prop.Name] = d;
            else if (value is bool b) msg.Bools[prop.Name] = b;
            // 其他类型（string/enum 等）目前没有，未来加字段时这里要扩展
        }

        return msg;
    }

    public void Serialize(PacketWriter writer)
    {
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
    }

    public void Deserialize(PacketReader reader)
    {
        Doubles.Clear();
        Bools.Clear();
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
    }

    /// <summary>
    /// 收到消息时被调用（BaseLib 3.1.2+ 接口签名带 senderId）。
    /// 我们不依赖 senderId，用 ConfigSyncManager.IsLocalHost() 判断 host 跳过 echo。
    /// </summary>
    public void HandleMessage(ulong senderId)
    {
        // host 不接收自己 broadcast（如果 BaseLib 有 echo back，IsLocalHost 拦掉）
        if (ConfigSyncManager.IsLocalHost())
        {
            MainFile.Logger.Info("[Sync] Host received own config sync message, ignored");
            return;
        }

        ConfigSyncManager.Apply(this);
    }
}