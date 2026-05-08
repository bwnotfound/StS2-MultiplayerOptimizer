using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

[Pool(typeof(IroncladCardPool))]
public class TestCard : CustomCardModel
{
    private const int InitDamage = 100;
    private const int DamageDelta = 100;
    private const int MaxDamage = 1000;

    private int CurrentDamage { get; set; } = InitDamage;

    public TestCard() : base(
        0,
        CardType.Attack,
        CardRarity.Uncommon,
        TargetType.AllEnemies)
    {
        CurrentDamage = InitDamage;
    }

    public override void AfterCreated()
    {
        base.AfterCreated();
        GD.Print($"[MultiplayerOptimizer] TestCard AfterCreated. Id={Id}");
    }

    public override string PortraitPath => CardModel.MissingPortraitPath;

    public override List<(string, string)>? Localization =>
    [
        ("title", "测试攻击"),
        ("description", "#*固有。造成 !D! 点伤害。每次打出后，本场战斗中伤害增加 100，最多 1000。战斗结束后重置。")
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        CardTag.Strike
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Innate
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(InitDamage, ValueProp.Move)
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay play)
    {
        SyncDamageVar();

        GD.Print($"[MultiplayerOptimizer] TestCard OnPlay. CurrentDamage={CurrentDamage}");

        await CommonActions
            .CardAttack(this, play.Target, vfx: "vfx/vfx_attack_slash")
            .Execute(choiceContext);

        IncreaseDamage();
    }

    public override Task BeforeCombatStart()
    {
        ResetDamage("BeforeCombatStart");
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetDamage("AfterCombatEnd");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
    }

    public override Task AfterAttack(AttackCommand command)
    {
        return base.AfterAttack(command);
    }

    private void IncreaseDamage()
    {
        int oldDamage = CurrentDamage;
        int newDamage = Math.Min(CurrentDamage + DamageDelta, MaxDamage);

        if (newDamage == oldDamage)
            return;

        CurrentDamage = newDamage;
        SyncDamageVar();

        GD.Print(
            $"[MultiplayerOptimizer] TestCard damage increased: {oldDamage} -> {CurrentDamage}");
    }

    private void ResetDamage(string reason)
    {
        if (CurrentDamage != InitDamage)
        {
            GD.Print(
                $"[MultiplayerOptimizer] TestCard damage reset by {reason}: {CurrentDamage} -> {InitDamage}");
        }

        CurrentDamage = InitDamage;
        SyncDamageVar();
    }

    private void SyncDamageVar()
    {
        if (!IsMutable)
            return;

        DynamicVars.Damage.BaseValue = CurrentDamage;
        Owner?.PlayerCombatState?.RecalculateCardValues();
    }
}