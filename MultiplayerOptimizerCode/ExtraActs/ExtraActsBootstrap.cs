namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 把自定义 act 实例化（构造器自动调用 CustomContentDictionary.AddAct 注册到 BaseLib）。
/// 由 MainFile.Initialize 调用。
/// </summary>
internal static class ExtraActsBootstrap
{
    public static Act4Model? Act4 { get; private set; }
    public static Act5Model? Act5 { get; private set; }

    public static void Initialize()
    {
        Act4 = new Act4Model();
        Act5 = new Act5Model();

        MainFile.Logger.Info(
            $"[ExtraActs] Registered custom acts: Act4={Act4.Id.Entry}, Act5={Act5.Id.Entry}");
    }
}