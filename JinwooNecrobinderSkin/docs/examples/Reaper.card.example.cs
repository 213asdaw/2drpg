using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace CharModTest;

/// <summary>
/// 원문 예시(사신) 최종 코드 — 네임스페이스/베이스 클래스는 본인 캐릭터 모드에 맞게 바꾸세요.
/// </summary>
[Pool(typeof(IroncladCardPool))]
public class Reaper() : CharModTest.CharModTestCode.Cards.CharModTestCard(
    2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, DamageProps.card) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState, nameof(CombatState));

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource == this && result.UnblockedDamage > 0)
        {
            await CreatureCmd.Heal(base.Owner.Creature, result.UnblockedDamage);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
