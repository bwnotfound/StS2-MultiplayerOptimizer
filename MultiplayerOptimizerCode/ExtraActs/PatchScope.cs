using System;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 所有 Harmony patch 的统一 guard / scope 工具。
///
/// ## 设计目标
/// 每个 patch 在入口都需要回答两个问题：
///   1. "我应该工作吗？"——总开关 Enabled、是否在 mod 自定义 act 内、run state 是否可用
///   2. "如果出问题怎么办？"——任何 patch 异常都不应该泄漏到原方法的调用者，否则别的 mod
///      或 base game 代码会拿到意外异常
///
/// 把这两件事集中到这里，让单个 patch 文件只关心自己的业务逻辑。每个 patch 用：
///   <code>
///   if (!PatchScope.TryEnter(out var ctx)) return;
///   // 业务逻辑用 ctx.State / ctx.IsAct4 等
///   </code>
///
/// 或对需要更细粒度的场景：
///   <code>
///   PatchScope.Run("MyPatchName", () => { ... });  // 异常自动 catch + log
///   </code>
///
/// ## 关于 Enabled 开关
/// 当前实现的 Enabled 控制的是**行为**（patch 是否执行业务逻辑），而非"是否绑定 patch"。
/// 这是因为 Harmony 不支持运行时 unpatch；且即使支持，运行中切换会让 ConfigSync 状态机
/// 等异步流程错乱。最简单一致的语义是：Enabled=false 时所有 patch 的入口都早返，原方法
/// 完整跑——等价于 mod "睡眠"。
///
/// 但有几个 patch 即使 Enabled=false 也必须保留行为，否则会有兼容性事故：
///   - <see cref="ValidateAncientAfterLoadPatch"/>：旧存档的 ancient null 补救，禁用会让旧存档卡死
///   - <see cref="ExpandActListPatch"/>：禁用相当于 act4/5 不出现，可接受但走另一条路径
///
/// 这些 patch 自己决定要不要 honor Enabled（一般是不 honor，文档注释里会说明）。
/// </summary>
internal static class PatchScope
{
    /// <summary>当前 run 的上下文快照——一次 patch 入口的 guard 检查产物，供业务逻辑使用。</summary>
    public readonly struct Context
    {
        public RunState State { get; init; }
        public bool IsAct4 { get; init; }
        public bool IsAct5 { get; init; }
        public bool IsAtFinalBossNode { get; init; }
    }

    /// <summary>
    /// 标准 guard：检查 Enabled / RunManager / State 可用 + 计算当前是否在 Act4/5。
    ///
    /// 业务想要"只在 Act4/5 工作"的 patch 都用这个。返回 false 时调用方应立刻 return。
    ///
    /// 注意：返回 true 不代表 IsAct4 || IsAct5 —— 它只保证 State 可读。如果 patch 关心
    /// "必须在自定义 act 内"，要再判断 ctx.IsAct4/ctx.IsAct5。
    /// </summary>
    public static bool TryEnter(out Context ctx)
    {
        ctx = default;
        if (!MultiplayerOptimizerConfig.Enabled) return false;

        var rm = RunManager.Instance;
        if (rm == null) return false;

        RunState? state;
        try
        {
            state = RunStateAccessor.GetState(rm);
        }
        catch
        {
            // 反射失败：RunManager.State getter 改名/移除等极端情况，安全退出
            return false;
        }

        if (state == null) return false;

        var act = state.Act;
        var isAct4 = act is Act4Model;
        var isAct5 = act is Act5Model;

        var isAtFinalBoss = false;
        if ((isAct4 || isAct5) && state.Map != null)
            isAtFinalBoss = state.CurrentMapCoord == state.Map.BossMapPoint.coord;

        ctx = new Context
        {
            State = state,
            IsAct4 = isAct4,
            IsAct5 = isAct5,
            IsAtFinalBossNode = isAtFinalBoss
        };
        return true;
    }

    /// <summary>
    /// 简化版：只检查 Enabled + 当前 act 类型，不需要 RunState 时使用。
    /// </summary>
    public static bool IsEnabled => MultiplayerOptimizerConfig.Enabled;

    /// <summary>
    /// 把一段 patch 业务逻辑包在 try/catch 里跑。异常会被 log 但不会传播——保护原方法的调用者。
    ///
    /// 设计原则：Harmony patch 永远不应该让原方法的调用者看到 mod 内部异常。即使 patch 出错，
    /// base game / 其他 mod 也应该当作"patch 没改东西"继续往下跑。
    /// </summary>
    public static void Run(string patchName, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            // 永不抛——Harmony postfix/prefix 异常会破坏整个 patch 链
            try
            {
                MainFile.Logger.Error($"[{patchName}] Unhandled exception (suppressed): {ex}");
            }
            catch
            {
                // logger 也挂了——彻底放弃
            }
        }
    }

    /// <summary>
    /// 同 <see cref="Run(string, Action)"/>，但返回业务逻辑结果。
    /// 异常时返回 <paramref name="fallback"/>（默认是 <c>default(T)</c>）。
    /// 主要用于 Prefix patch 需要 return bool（true=放行原方法）的场景——异常时通常 fallback 给 true，
    /// 安全放行原方法。
    /// </summary>
    public static T Run<T>(string patchName, Func<T> body, T fallback = default!)
    {
        try
        {
            return body();
        }
        catch (Exception ex)
        {
            try
            {
                MainFile.Logger.Error($"[{patchName}] Unhandled exception (suppressed): {ex}");
            }
            catch
            {
                // ignore
            }

            return fallback;
        }
    }
}