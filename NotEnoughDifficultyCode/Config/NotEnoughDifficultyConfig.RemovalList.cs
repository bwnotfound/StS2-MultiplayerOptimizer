using BaseLib.Config;
using BaseLib.Config.UI;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     Partial: 通用敌人移除列表（需求2）的入口按钮。
///     列表数据是 [ConfigHideInUI] 的 ExcludedEncounterIdsCsv（见 .Behaviors.cs），不自动生成 UI；
///     增删通过这个按钮打开的 RemovalListPopup 操作。
/// </summary>
internal partial class NotEnoughDifficultyConfig
{
    [ConfigSection("RemovalList")]
    [ConfigButton("MANAGE_REMOVAL_LIST")]
    public static void OpenRemovalListPopup(NConfigButton button, ModConfig cfg)
    {
        // button 注入用于拿 SceneTree.Root（把弹窗挂到顶层）；cfg 用于增删后落盘。
        RemovalListPopup.Open(button, cfg);
    }
}