using Godot;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.SettingsInjection;

/// <summary>
/// 标识 mod 注入到 NSettingsScreen 的 row 类型。通过节点 Name 路由用——参考 SteamRandomMatch
/// mod 的做法：复制 base game 已有 row 作为模板，给每个新 row 起一个独特 Name，patch base game
/// 控件的行为方法（如 NUploadDataTickbox.OnTick）时按 Name 判断要不要走我们 mod 的逻辑。
///
/// 这个工具类集中管理：
///   - row Name 常量（避免散落到各 patch 文件容易写错）
///   - GetKindOf 工具方法（向上爬节点祖先链找匹配的 row）
/// </summary>
internal static class InjectedRowKind
{
    /// <summary>"启用额外加速倍率" tickbox 所在 row 的 Name。</summary>
    public const string EnableExtraSpeedRowName = "MO_EnableExtraSpeedRow";

    /// <summary>"额外加速倍率" slider 所在 row 的 Name。</summary>
    public const string ExtraSpeedMultiplierRowName = "MO_ExtraSpeedMultiplierRow";

    /// <summary>
    /// 给定一个 row 内部的控件（tickbox/slider 节点等），向上爬祖先链，看是不是在我们注入的 row 里。
    /// 不是则返回 null，调用方应该走 base game 原逻辑（return true 跳过 prefix）。
    ///
    /// 比 SteamRandomMatch 的实现稍微 robust 一点：用 while loop 而不是固定爬几层，
    /// 防止 base game 升级时 row 内部嵌套层级变化。
    /// </summary>
    public static Kind? GetKindOf(Node? control)
    {
        var cur = control;
        while (cur != null)
        {
            var name = cur.Name.ToString();
            if (name == EnableExtraSpeedRowName) return Kind.EnableExtraSpeed;
            if (name == ExtraSpeedMultiplierRowName) return Kind.ExtraSpeedMultiplier;
            cur = cur.GetParent();
        }

        return null;
    }

    public enum Kind
    {
        EnableExtraSpeed,
        ExtraSpeedMultiplier,
    }
}