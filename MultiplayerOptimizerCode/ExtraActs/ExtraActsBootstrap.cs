using System;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 把自定义 act 实例化（构造器自动调用 CustomContentDictionary.AddAct 注册到 BaseLib）。
/// 由 MainFile.Initialize 调用。
///
/// 注意：act 注册<b>不受 MultiplayerOptimizerConfig.Enabled 控制</b>。原因：
///   - 自定义 act 一旦注册到 ModelDb 就成为 mod manifest 的一部分；运行时 enable/disable
///     无法卸载已注册的 ModelDb 条目。
///   - 联机校验依赖 mod manifest 一致——动态 enable/disable 会导致 host/client manifest 不同
///     从而连不上。
///   - Enabled=false 时具体的"自定义 act 数值/池子"行为靠各个 patch 自己早返来 disable，
///     act 本身仍然存在但是个"空壳"。
/// </summary>
internal static class ExtraActsBootstrap
{
    public static Act4Model? Act4 { get; private set; }
    public static Act5Model? Act5 { get; private set; }

    public static void Initialize()
    {
        // 两个 act 的构造分别 try——一个失败不影响另一个
        try
        {
            Act4 = new Act4Model();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Failed to construct Act4Model: {ex}");
        }

        try
        {
            Act5 = new Act5Model();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Failed to construct Act5Model: {ex}");
        }

        MainFile.Logger.Info(
            $"Registered custom acts: Act4={Act4?.Id.Entry ?? "<null>"}, Act5={Act5?.Id.Entry ?? "<null>"}");
    }
}