using AccardND.GameCore;

namespace AccardND.Battlefield
{
    public sealed class BattlePresentationSfxRouter
    {
        private readonly BattleSfxPlayer sfx;

        public BattlePresentationSfxRouter(BattleSfxPlayer sfx)
        {
            this.sfx = sfx;
        }

        public void Play(BattlePresentationEvent battleEvent)
        {
            if (battleEvent == null || sfx == null)
                return;

            switch (battleEvent.Type)
            {
                case "CardInitiative":
                    sfx.PlayRollingDice();
                    break;
                case "CardDeployed":
                    if (battleEvent.HasHeroClass)
                        sfx.PlayJoinBattlefield(battleEvent.HeroClass);
                    else
                        sfx.PlayJoinBattlefield();
                    break;
                case "AbilityUsed":
                    if (battleEvent.HasAbilityClass)
                        sfx.PlayClassAbility(battleEvent.AbilityClass);
                    break;
                case "CardRevived":
                    sfx.PlayClassAbility(HeroClass.Necromancer);
                    sfx.PlayJoinBattlefield(HeroClass.Necromancer);
                    break;
                case "AttachmentApplied":
                    sfx.PlayAttachment();
                    break;
                case "FuryGained":
                    sfx.PlayBarbarianFury();
                    break;
            }
        }
    }
}
