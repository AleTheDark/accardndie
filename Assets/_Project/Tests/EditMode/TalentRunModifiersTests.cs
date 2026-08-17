using AccardND.GameData;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// I modificatori dei talenti applicati alla run. Sono tutti sconti e somme su numeri
    /// piccoli, cioe' proprio il tipo di codice che sbaglia in silenzio: un arrotondamento
    /// nella direzione sbagliata regala il doppio di quello che il nodo promette.
    /// </summary>
    public sealed class TalentRunModifiersTests
    {
        private static readonly int[] BaseThresholds = { 50, 75, 100, 125, 150 };

        [Test]
        public void WithoutTalents_EverythingStaysAsConfigured()
        {
            int[] thresholds = TalentRunModifiers.ApplyLevelThresholds(BaseThresholds, null);

            Assert.That(thresholds, Is.EqualTo(BaseThresholds));
            Assert.That(TalentRunModifiers.StartingEssence(75, null), Is.EqualTo(75));
            Assert.That(TalentRunModifiers.StartingGold(null), Is.EqualTo(0));
            Assert.That(TalentRunModifiers.MerchantCost(18, null), Is.EqualTo(18));
            Assert.That(TalentRunModifiers.LootItemCount(null), Is.EqualTo(1));
        }

        [Test]
        public void Apprentice_DiscountsEveryThreshold()
        {
            var talents = new TalentLoadoutSave { masteryThresholdPercent = 10 };

            int[] thresholds = TalentRunModifiers.ApplyLevelThresholds(BaseThresholds, talents);

            Assert.That(thresholds, Is.EqualTo(new[] { 45, 68, 90, 113, 135 }));
        }

        [Test]
        public void OnlyOneNodeTouchesTheThresholds()
        {
            // Il ramo Maestria aveva quattro sconti sulle soglie e ne e' rimasto uno solo:
            // sommati portavano il d20 troppo avanti nella run. Lo sconto e' unico e uguale
            // su tutte le soglie, senza piu' eccezioni per singolo livello.
            var talents = new TalentLoadoutSave { masteryThresholdPercent = 10 };

            int[] thresholds = TalentRunModifiers.ApplyLevelThresholds(BaseThresholds, talents);

            Assert.That(thresholds[0], Is.EqualTo(45));
            Assert.That(thresholds[4], Is.EqualTo(135));
        }

        [Test]
        public void AThresholdNeverDropsToZero()
        {
            // Una soglia a zero farebbe salire di livello all'infinito al primo punto exp.
            var talents = new TalentLoadoutSave { masteryThresholdPercent = 90 };

            int[] thresholds = TalentRunModifiers.ApplyLevelThresholds(new[] { 4 }, talents);

            Assert.That(thresholds[0], Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Focus_GivesManaOnEveryRoomChange()
        {
            var talents = new TalentLoadoutSave { roomChangeMana = 2 };

            Assert.That(TalentRunModifiers.RoomChangeMana(talents), Is.EqualTo(2));
            Assert.That(TalentRunModifiers.RoomChangeMana(null), Is.EqualTo(0));
        }

        [Test]
        public void Reserve_RaisesTheManaCeiling()
        {
            Assert.That(
                TalentRunModifiers.MaximumMana(10, new TalentLoadoutSave { bonusMaximumMana = 1 }),
                Is.EqualTo(11));
            Assert.That(
                TalentRunModifiers.MaximumMana(10, new TalentLoadoutSave { bonusMaximumMana = 2 }),
                Is.EqualTo(12));
            Assert.That(TalentRunModifiers.MaximumMana(10, null), Is.EqualTo(10));
        }

        [Test]
        public void Reserve_NeverGoesPastTheTwoItPromises()
        {
            // Il nodo ha due ranghi da +1: un pacchetto manomesso non deve poter regalare
            // una riserva infinita.
            var talents = new TalentLoadoutSave { bonusMaximumMana = 99 };

            Assert.That(TalentRunModifiers.MaximumMana(10, talents), Is.EqualTo(12));
        }

        [Test]
        public void Trance_IsAnEndowmentAndNotAnAmount()
        {
            var talents = new TalentLoadoutSave { firstAbilityFreeEachRoom = true };

            Assert.That(TalentRunModifiers.FirstAbilityFreeEachRoom(talents), Is.True);
            Assert.That(TalentRunModifiers.FirstAbilityFreeEachRoom(null), Is.False);
        }

        [Test]
        public void DiscountsRoundInFavourOfThePrice()
        {
            // 18 scontato del 10% fa 16.2: si paga 17, non 16. Uno sconto non deve mai
            // regalare piu' di quanto dice.
            var talents = new TalentLoadoutSave { merchantDiscountPercent = 10 };

            Assert.That(TalentRunModifiers.MerchantCost(18, talents), Is.EqualTo(17));
        }

        [Test]
        public void MerchantAndRecoveryUseTheirOwnDiscounts()
        {
            var talents = new TalentLoadoutSave
            {
                merchantDiscountPercent = 20,
                recoveryDiscountPercent = 30
            };

            Assert.That(TalentRunModifiers.MerchantCost(20, talents), Is.EqualTo(16));
            Assert.That(TalentRunModifiers.RecoveryCost(20, talents), Is.EqualTo(14));
        }

        [Test]
        public void InitiativeBonusFollowsTheFormationSlot()
        {
            var talents = new TalentLoadoutSave();
            talents.initiativeBonusBySlot.AddRange(new[] { 3, 2, 0 });

            Assert.That(TalentRunModifiers.InitiativeBonus(0, talents), Is.EqualTo(3));
            Assert.That(TalentRunModifiers.InitiativeBonus(1, talents), Is.EqualTo(2));
            Assert.That(TalentRunModifiers.InitiativeBonus(2, talents), Is.EqualTo(0));
        }

        [Test]
        public void AnOutOfRangeSlotIsWorthNothingInsteadOfThrowing()
        {
            // Il chiamante indicizza con la posizione in formazione: se un giorno la
            // formazione crescesse, deve mancare il bonus, non partire un'eccezione a meta'
            // di uno schieramento.
            var talents = new TalentLoadoutSave();
            talents.initiativeBonusBySlot.AddRange(new[] { 3 });

            Assert.That(TalentRunModifiers.InitiativeBonus(7, talents), Is.EqualTo(0));
            Assert.That(TalentRunModifiers.InitiativeBonus(-1, talents), Is.EqualTo(0));
        }

        [Test]
        public void StartingEssenceAndGoldAddUpToTheConfiguration()
        {
            var talents = new TalentLoadoutSave { startingEssence = 15, startingGold = 10 };

            Assert.That(TalentRunModifiers.StartingEssence(75, talents), Is.EqualTo(90));
            Assert.That(TalentRunModifiers.StartingGold(talents), Is.EqualTo(10));
        }

        [Test]
        public void SeekerAddsDeliveriesToTheLootRoom()
        {
            var talents = new TalentLoadoutSave { extraLootItems = 2 };

            Assert.That(TalentRunModifiers.LootItemCount(talents), Is.EqualTo(3));
        }
    }
}
