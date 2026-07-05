using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// Verifica che il replay client (PvpClientMatchState) ricostruisca fedelmente
    /// lo stato del motore consumando solo i DTO che il server trasmette.
    /// </summary>
    public sealed class PvpClientMatchStateTests
    {
        [Test]
        public void FullSeededMatch_ClientMirrorsEngineState()
        {
            var random = new SeededRandomSource(20260702);
            var engine = new PvpMatchEngine(
                Loadout("p0"), Loadout("p1"), PvpMatchRules.CreateDefault(), random);

            var clients = new[] { new PvpClientMatchState(), new PvpClientMatchState() };
            clients[0].ApplyMatchStart(new MatchStart { opponentName = "avversario", yourPlayerIndex = 0 });
            clients[1].ApplyMatchStart(new MatchStart { opponentName = "avversario", yourPlayerIndex = 1 });

            Feed(clients, engine, engine.Start());
            int guard = 0;
            while (engine.Phase != PvpMatchPhase.Finished && guard++ < 2000)
            {
                switch (engine.Phase)
                {
                    case PvpMatchPhase.Deployment:
                    {
                        int player = engine.ActivePlayer;
                        Assert.That(clients[player].IsMyDeployTurn, Is.True,
                            "il client deve sapere che tocca a lui schierare");
                        Feed(clients, engine, engine.Deploy(player, 0));
                        break;
                    }
                    case PvpMatchPhase.Battle:
                    {
                        int player = engine.ActivePlayer;
                        Assert.That(clients[player].IsMyBattleTurn, Is.True,
                            "il client deve sapere che è il suo turno");
                        Assert.That(clients[player].ActiveSlot, Is.EqualTo(engine.ActiveCard.Slot));
                        int target = FirstActive(engine, 1 - player);
                        Feed(clients, engine, engine.Attack(player, target));
                        break;
                    }
                    case PvpMatchPhase.DecisiveSelection:
                        Assert.That(clients[0].Phase, Is.EqualTo(PvpClientPhase.DecisiveSelection));
                        Feed(clients, engine, engine.SubmitDecisiveSelection(0, new[] { 0, 1, 2 }));
                        Feed(clients, engine, engine.SubmitDecisiveSelection(1, new[] { 0, 1, 2 }));
                        break;
                }
            }

            Assert.That(guard, Is.LessThan(2000), "il match non termina");
            foreach (PvpClientMatchState client in clients)
            {
                Assert.That(client.Phase, Is.EqualTo(PvpClientPhase.Finished));
                Assert.That(client.Winner, Is.EqualTo(engine.MatchWinner));
                Assert.That(client.Wins[0], Is.EqualTo(engine.WinsOf(0)));
                Assert.That(client.Wins[1], Is.EqualTo(engine.WinsOf(1)));
                for (int player = 0; player < 2; player++)
                {
                    Assert.That(client.Boards[player].Count, Is.EqualTo(engine.BoardOf(player).Count));
                    foreach (PvpCardState engineCard in engine.BoardOf(player))
                    {
                        PvpClientCard clientCard = client.Boards[player]
                            .Single(card => card.Slot == engineCard.Slot);
                        Assert.That(clientCard.Eliminated, Is.EqualTo(engineCard.Eliminated),
                            $"eliminazione discordante per {clientCard.CardName}");
                        Assert.That(clientCard.Lives, Is.EqualTo(System.Math.Max(engineCard.Lives, 0)),
                            $"vite discordanti per {clientCard.CardName}");
                        Assert.That(clientCard.Initiative, Is.EqualTo(engineCard.Initiative));
                    }
                }
            }
        }

        [Test]
        public void HandTracking_RemovesDeployedCardsFromLocalHand()
        {
            var random = new SeededRandomSource(42);
            var engine = new PvpMatchEngine(
                Loadout("p0"), Loadout("p1"), PvpMatchRules.CreateDefault(), random);
            var client = new PvpClientMatchState();
            client.ApplyMatchStart(new MatchStart { opponentName = "x", yourPlayerIndex = 0 });

            Feed(new[] { client, new PvpClientMatchState() }, engine, engine.Start());
            Assert.That(client.Hand, Has.Count.EqualTo(6));

            var expected = new List<int>(engine.HandOf(0));
            while (engine.Phase == PvpMatchPhase.Deployment && engine.BoardOf(0).Count < 3)
            {
                int player = engine.ActivePlayer;
                var events = engine.Deploy(player, 0);
                foreach (PvpEvent gameEvent in events)
                {
                    if (gameEvent is HandReadyEvent)
                        continue;
                    client.Apply(PvpEventMapper.ToDto(gameEvent));
                }
            }
            Assert.That(client.Hand, Has.Count.EqualTo(6 - engine.BoardOf(0).Count));
        }

        [Test]
        public void AbilityReplay_PriestMarksAbilityUsedAndBlessingBonus()
        {
            var client = new PvpClientMatchState();
            client.ApplyMatchStart(new MatchStart { opponentName = "x", yourPlayerIndex = 0 });
            client.Apply(new MatchEventDto
            {
                type = "CardDeployed",
                player = 0,
                slot = 0,
                cardId = "priest",
                cardName = "Priest",
                heroClass = (int)HeroClass.Priest,
                strength = 8,
                lives = 2
            });

            client.Apply(new MatchEventDto
            {
                type = "AbilityUsed",
                player = 0,
                slot = 0,
                ability = (int)HeroClass.Priest,
                targetPlayer = 0,
                targetSlot = 0,
                magnitude = 2
            });

            PvpClientCard card = client.Boards[0].Single();
            Assert.That(card.AbilityUsed, Is.True);
            Assert.That(card.AbilityArmed, Is.False);
            Assert.That(card.PendingBonus, Is.EqualTo(2));
            Assert.That(card.PendingBonusKind, Is.EqualTo(PvpPendingBonusKind.Blessing));

            var presentedCard = new BattlePresentationCard
            {
                PendingBonus = card.PendingBonus,
                PendingBonusKind = card.PendingBonusKind,
                AbilityUsed = card.AbilityUsed
            };
            Assert.That(presentedCard.AbilityUsed, Is.True);
            Assert.That(BattlePresentationViewStateMapper.CardStatuses(presentedCard)
                .Any(status => status.Label == "BENEDIZIONE +2"), Is.True);
        }

        [Test]
        public void AbilityReplay_WarriorButtonStaysUnavailableAfterArmedAttack()
        {
            var client = new PvpClientMatchState();
            client.ApplyMatchStart(new MatchStart { opponentName = "x", yourPlayerIndex = 0 });
            client.Apply(new MatchEventDto
            {
                type = "CardDeployed",
                player = 0,
                slot = 0,
                cardId = "warrior",
                cardName = "Warrior",
                heroClass = (int)HeroClass.Warrior,
                strength = 5,
                lives = 2
            });
            client.Apply(new MatchEventDto
            {
                type = "CardDeployed",
                player = 1,
                slot = 0,
                cardId = "enemy",
                cardName = "Enemy",
                heroClass = (int)HeroClass.Mage,
                strength = 5,
                lives = 2
            });
            client.Apply(new MatchEventDto
            {
                type = "AbilityUsed",
                player = 0,
                slot = 0,
                ability = (int)HeroClass.Warrior,
                targetPlayer = 0,
                targetSlot = 0
            });

            PvpClientCard card = client.Boards[0].Single();
            Assert.That(card.AbilityArmed, Is.True);

            client.Apply(new MatchEventDto
            {
                type = "AttackResolved",
                player = 0,
                slot = 0,
                targetPlayer = 1,
                targetSlot = 0,
                certainty = "Normal",
                defenderRemainingLives = 2
            });

            Assert.That(card.AbilityArmed, Is.False);
            Assert.That(card.AbilityUsed, Is.True);
        }

        private static void Feed(
            PvpClientMatchState[] clients, PvpMatchEngine engine, IReadOnlyList<PvpEvent> events)
        {
            foreach (PvpEvent gameEvent in events)
            {
                if (gameEvent is HandReadyEvent handReady)
                {
                    // Replica dell'invio privato della mano fatto dal server.
                    IReadOnlyList<int> hand = engine.HandOf(handReady.Player);
                    clients[handReady.Player].ApplyHand(new MatchHand
                    {
                        roundNumber = engine.MatchRound,
                        handIndices = hand.ToArray(),
                        handDefinitionIds = hand
                            .Select(index => engine.LoadoutCard(handReady.Player, index).Id)
                            .ToArray()
                    });
                    continue;
                }
                MatchEventDto dto = PvpEventMapper.ToDto(gameEvent);
                clients[0].Apply(dto);
                clients[1].Apply(dto);
            }
        }

        private static int FirstActive(PvpMatchEngine engine, int player)
        {
            IReadOnlyList<PvpCardState> board = engine.BoardOf(player);
            for (int slot = 0; slot < board.Count; slot++)
            {
                if (board[slot].IsActive)
                    return slot;
            }
            return 0;
        }

        private static List<CombatCard> Loadout(string prefix)
        {
            // Nove classi diverse, valori misti: attiva matchup e abilità varie.
            var classes = new[]
            {
                HeroClass.Warrior, HeroClass.Rogue, HeroClass.Mage,
                HeroClass.Barbarian, HeroClass.Assassin, HeroClass.Priest,
                HeroClass.Paladin, HeroClass.Hunter, HeroClass.Necromancer
            };
            var cards = new List<CombatCard>();
            for (int index = 0; index < 9; index++)
                cards.Add(new CombatCard(
                    $"{prefix}-{index}", $"{prefix}-{index}", classes[index], 2 + index % 5));
            return cards;
        }
    }
}
