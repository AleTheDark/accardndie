using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore.Mana;
using AccardND.GameCore.Pvp;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>Effetti delle abilita' supreme nel motore PvP. Vedi Docs/mana-design.md.</summary>
    public sealed class PvpSupremeTests
    {
        private sealed class QueuedRandom : IRandomSource
        {
            private readonly Queue<int> values;

            public QueuedRandom(IEnumerable<int> values) => this.values = new Queue<int>(values);

            public int NextInclusive(int minimum, int maximum) =>
                values.Count > 0 ? values.Dequeue() : minimum;
        }

        private static CombatCard Card(HeroClass heroClass, int strength, string id) =>
            new(id, id, heroClass, strength);

        private static List<CombatCard> Loadout(string prefix, HeroClass heroClass, int strength)
        {
            var cards = new List<CombatCard>();
            for (int index = 0; index < 9; index++)
                cards.Add(Card(heroClass, strength, $"{prefix}-{index}"));
            return cards;
        }

        private static IEnumerable<int> IdentityShuffles()
        {
            for (int player = 0; player < 2; player++)
                for (int index = 8; index >= 1; index--)
                    yield return index;
        }

        private static IEnumerable<int> DeploymentAndInitiatives()
        {
            foreach (int initiative in new[] { 20, 19, 18 })
            {
                yield return initiative;
                yield return 1;
            }
            foreach (int initiative in new[] { 6, 5, 4 })
            {
                yield return initiative;
                yield return 1;
            }
        }

        private static PvpMatchEngine BattleReady(
            List<CombatCard> loadout0,
            List<CombatCard> loadout1)
        {
            var random = new QueuedRandom(
                IdentityShuffles()
                    .Concat(DeploymentAndInitiatives())
                    .Concat(Enumerable.Repeat(3, 600)));
            var engine = new PvpMatchEngine(loadout0, loadout1, PvpMatchRules.CreateDefault(), random);
            engine.Start();
            while (engine.Phase == PvpMatchPhase.Deployment)
                engine.Deploy(engine.ActivePlayer, 0);
            return engine;
        }

        /// <summary>Salta turni finche' il giocatore ha il mana richiesto ed e' il suo turno.</summary>
        private static void BankMana(PvpMatchEngine engine, int player, int required)
        {
            for (int guard = 0; guard < 60; guard++)
            {
                if (engine.Phase != PvpMatchPhase.Battle)
                    return;
                if (engine.ManaOf(player) >= required && engine.ActivePlayer == player)
                    return;
                engine.Pass(engine.ActivePlayer);
            }
            Assert.Fail($"Non sono riuscito a portare il giocatore {player} a {required} mana.");
        }

        private static PvpMatchEngine Mirror(HeroClass mine, HeroClass theirs, int strength = 5) =>
            BattleReady(Loadout("p0", mine, strength), Loadout("p1", theirs, strength));

        /// <summary>
        /// Dopo un'abilita' non-d'attacco la pedina non puo' passare: deve attaccare.
        /// Chiude l'attivazione sul primo bersaglio ancora vivo.
        /// </summary>
        private static void CloseActivationWithAttack(PvpMatchEngine engine, int player)
        {
            IReadOnlyList<PvpCardState> enemies = engine.BoardOf(1 - player);
            for (int slot = 0; slot < enemies.Count; slot++)
            {
                if (!enemies[slot].IsActive)
                    continue;
                engine.Attack(player, slot);
                return;
            }
            Assert.Fail("Nessun bersaglio disponibile per chiudere l'attivazione.");
        }

        private static void AdvanceTo(PvpMatchEngine engine, int player)
        {
            for (int guard = 0; guard < 40 && engine.Phase == PvpMatchPhase.Battle; guard++)
            {
                if (engine.ActivePlayer == player)
                    return;
                engine.Pass(engine.ActivePlayer);
            }
        }

        // --- Guerriero ---

        [Test]
        public void WarriorEmpower_AddsTwoPower()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Warrior, HeroClass.Warrior);
            PvpCardState actor = engine.ActiveCard;
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Warrior));

            engine.UseSupreme(0, 0, 0);

            Assert.That(actor.PermanentCombatBonus, Is.EqualTo(2));
            Assert.That(engine.ManaOf(0), Is.EqualTo(0), "6 disponibili - 6 di costo");
        }

        [Test]
        public void WarriorEmpower_AddsFourWhenItIsTheLastCardStanding()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Warrior, HeroClass.Warrior);
            PvpCardState actor = engine.ActiveCard;
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Warrior));
            foreach (PvpCardState ally in engine.BoardOf(0))
                if (ally != actor)
                    ally.Eliminated = true;

            engine.UseSupreme(0, 0, 0);

            Assert.That(actor.PermanentCombatBonus, Is.EqualTo(4));
        }

        // --- Ladro ---

        [Test]
        public void RogueStealBuffs_TransfersTheTargetsBonuses()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Rogue, HeroClass.Warrior);
            PvpCardState actor = engine.ActiveCard;
            PvpCardState victim = engine.BoardOf(1)[0];
            victim.PendingAttackBonus = 2;
            victim.PermanentCombatBonus = 1;

            engine.UseSupreme(0, 1, 0);

            Assert.That(victim.PendingAttackBonus, Is.EqualTo(0));
            Assert.That(victim.PermanentCombatBonus, Is.EqualTo(0));
            Assert.That(actor.PermanentCombatBonus, Is.EqualTo(3), "ruba 2 + 1");
        }

        [Test]
        public void RogueStealBuffs_StealsTwoPower_WhenTargetHasNoBuffs()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Rogue, HeroClass.Warrior);
            PvpCardState actor = engine.ActiveCard;
            PvpCardState victim = engine.BoardOf(1)[0];

            engine.UseSupreme(0, 1, 0);

            Assert.That(victim.PermanentCombatBonus, Is.EqualTo(-2));
            Assert.That(actor.PermanentCombatBonus, Is.EqualTo(2), "e' un furto, non una semplice riduzione");
        }

        // --- Mago e Cacciatore ---

        [Test]
        public void Fireball_HitsEveryActiveEnemy()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Mage, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Mage));

            List<AttackResolvedEvent> attacks = engine.UseSupreme(0, 1, 0)
                .OfType<AttackResolvedEvent>()
                .ToList();

            Assert.That(attacks.Count, Is.EqualTo(3), "colpisce tutta la formazione avversaria");
            Assert.That(
                attacks.Select(a => a.DefenderSlot).Distinct().Count(),
                Is.EqualTo(3),
                "un bersaglio diverso ciascuno");
        }

        [Test]
        public void Fireball_LowersTheAttackerDieByOneStep()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Mage, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Mage));

            AttackResolvedEvent first = engine.UseSupreme(0, 1, 0)
                .OfType<AttackResolvedEvent>()
                .First();

            Assert.That(first.AttackerDieSides, Is.LessThan(first.DefenderDieSides));
        }

        [Test]
        public void AttackSupreme_EndsTheActivation()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Mage, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Mage));
            PvpCardState actor = engine.ActiveCard;

            engine.UseSupreme(0, 1, 0);

            Assert.That(engine.ActiveCard, Is.Not.SameAs(actor), "la suprema d'attacco chiude il turno");
        }

        [Test]
        public void Volley_BehavesLikeFireball()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Hunter, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Hunter));

            int hits = engine.UseSupreme(0, 1, 0).OfType<AttackResolvedEvent>().Count();

            Assert.That(hits, Is.EqualTo(3));
        }

        // --- Barbaro ---

        [Test]
        public void WarHorn_BuffsTheWholeParty()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Barbarian, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Barbarian));

            engine.UseSupreme(0, 0, 0);

            foreach (PvpCardState ally in engine.BoardOf(0))
            {
                Assert.That(ally.PendingAttackBonus, Is.GreaterThan(0), $"slot {ally.Slot} non potenziato");
                Assert.That(ally.PendingDefenseBonus, Is.GreaterThan(0), "la cornamusa vale anche in difesa");
            }
        }

        // --- Paladino ---

        [Test]
        public void ManaReserve_RaisesManaToTheThreshold()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Paladin, HeroClass.Warrior);

            engine.UseSupreme(0, 0, 0); // 3 - 2 = 1, poi risale a 6

            Assert.That(engine.ManaOf(0), Is.EqualTo(6));
        }

        [Test]
        public void ManaReserve_IsALossWhenAlreadyAboveTheThreshold()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Paladin, HeroClass.Warrior);
            BankMana(engine, 0, 8);
            int before = engine.ManaOf(0);

            engine.UseSupreme(0, 0, 0);

            Assert.That(engine.ManaOf(0), Is.EqualTo(before - 2), "sopra la soglia paga e non riceve");
        }

        [Test]
        public void ManaReserve_EmitsAReserveEvent()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Paladin, HeroClass.Warrior);

            bool hasReserve = engine.UseSupreme(0, 0, 0)
                .OfType<ManaChangedEvent>()
                .Any(e => e.Reason == ManaChangeReasons.Reserve);

            Assert.That(hasReserve, Is.True);
        }

        // --- Sacerdote ---

        [Test]
        public void Dispel_ClearsEnemyBuffsAndAllyMalus()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Priest, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Priest));

            PvpCardState ally = engine.BoardOf(0)[1];
            ally.InhibitedTurns = 1;
            ally.PermanentCombatBonus = -2;
            PvpCardState foe = engine.BoardOf(1)[0];
            foe.PendingAttackBonus = 3;
            foe.PermanentCombatBonus = 2;

            engine.UseSupreme(0, 0, 0);

            Assert.That(ally.InhibitedTurns, Is.EqualTo(0));
            Assert.That(ally.PermanentCombatBonus, Is.EqualTo(0));
            Assert.That(foe.PendingAttackBonus, Is.EqualTo(0));
            Assert.That(foe.PermanentCombatBonus, Is.EqualTo(0));
        }

        [Test]
        public void Dispel_DoesNotTouchAuras()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Priest, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Priest));
            PvpAuraType enemyAura = engine.AuraOf(1);

            engine.UseSupreme(0, 0, 0);

            Assert.That(engine.AuraOf(1), Is.EqualTo(enemyAura));
        }

        [Test]
        public void Dispel_RemovesEnemyInvisibility()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Priest, HeroClass.Assassin);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Priest));
            PvpCardState assassin = engine.BoardOf(1)[0];
            assassin.IsUntargetable = true;

            engine.UseSupreme(0, 0, 0);

            Assert.That(assassin.IsUntargetable, Is.False, "l'invisibilita' e' un buff, il Dispel la toglie");
        }

        // --- Assassino ---

        [Test]
        public void Vanish_MakesTheCardUntargetable()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Assassin, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Assassin));
            PvpCardState actor = engine.ActiveCard;

            engine.UseSupreme(0, 0, 0);

            Assert.That(actor.IsUntargetable, Is.True);
        }

        [Test]
        public void InvisibleCard_CannotBeAttacked()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Assassin, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Assassin));
            PvpCardState actor = engine.ActiveCard;
            engine.UseSupreme(0, 0, 0);
            CloseActivationWithAttack(engine, 0);
            AdvanceTo(engine, 1);

            Assert.Throws<PvpActionException>(() => engine.Attack(1, actor.Slot));
        }

        [Test]
        public void InvisibleCard_BecomesTargetableWhenItIsTheLastOneLeft()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Assassin, HeroClass.Warrior);
            BankMana(engine, 0, AbilityManaCosts.Supreme(HeroClass.Assassin));
            PvpCardState actor = engine.ActiveCard;
            engine.UseSupreme(0, 0, 0);
            CloseActivationWithAttack(engine, 0);

            foreach (PvpCardState ally in engine.BoardOf(0))
                if (ally != actor)
                    ally.Eliminated = true;

            AdvanceTo(engine, 1);

            Assert.DoesNotThrow(() => engine.Attack(1, actor.Slot));
        }

        // --- Regole trasversali ---

        [Test]
        public void NecromancerSupreme_IsRejected()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Necromancer, HeroClass.Warrior);
            BankMana(engine, 0, 10);

            var error = Assert.Throws<PvpActionException>(() => engine.UseSupreme(0, 0, 0));
            Assert.That(error.ErrorCode, Is.EqualTo(PvpActionErrorCodes.SupremeNotAvailable));
        }

        [Test]
        public void NonAttackSupreme_IsRejectedAfterAnAbilityInTheSameActivation()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Priest, HeroClass.Warrior);
            BankMana(engine, 0, 10);
            engine.UseAbility(0, 0, 1); // Benedizione su un alleato

            var error = Assert.Throws<PvpActionException>(() => engine.UseSupreme(0, 0, 0));
            Assert.That(error.ErrorCode, Is.EqualTo(PvpActionErrorCodes.AbilityRequiresAction));
        }

        [Test]
        public void AttackSupreme_CanChainAfterAnAbility()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Mage, HeroClass.Warrior);
            BankMana(engine, 0, 10);
            int before = engine.ManaOf(0);

            engine.UseAbility(0, 1, 0); // malus: costo 2
            engine.UseSupreme(0, 1, 0); // Palla di fuoco: costo pieno 4, l'abilita' base non lo alza

            Assert.That(before - engine.ManaOf(0), Is.EqualTo(2 + 4 - 1), "costi a listino, meno il recupero di fine turno");
        }

        [Test]
        public void RepeatedSupremeOfTheSameClass_CostsOneMore()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Paladin, HeroClass.Warrior);
            BankMana(engine, 0, 10);
            Assert.That(engine.SupremeCostFor(0), Is.EqualTo(2));

            engine.UseSupreme(0, 0, 0);
            CloseActivationWithAttack(engine, 0);
            BankMana(engine, 0, 10);

            Assert.That(engine.SupremeCostFor(0), Is.EqualTo(3), "seconda suprema di Paladino nel round");
        }

        [Test]
        public void Supreme_IsRejected_WhenManaIsInsufficient()
        {
            // Assassino: costo 5, riserva iniziale 3.
            PvpMatchEngine engine = Mirror(HeroClass.Assassin, HeroClass.Warrior);

            var error = Assert.Throws<PvpActionException>(() => engine.UseSupreme(0, 0, 0));
            Assert.That(error.ErrorCode, Is.EqualTo(PvpActionErrorCodes.NotEnoughMana));
        }

        [Test]
        public void RejectedSupreme_LeavesStateUntouched()
        {
            PvpMatchEngine engine = Mirror(HeroClass.Assassin, HeroClass.Warrior);
            PvpCardState actor = engine.ActiveCard;
            int before = engine.ManaOf(0);

            Assert.Throws<PvpActionException>(() => engine.UseSupreme(0, 0, 0));

            Assert.That(engine.ManaOf(0), Is.EqualTo(before));
            Assert.That(actor.IsUntargetable, Is.False);
        }
    }
}
