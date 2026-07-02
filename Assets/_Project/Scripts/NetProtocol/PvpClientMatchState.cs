using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;

namespace AccardND.NetProtocol
{
    public enum PvpClientPhase
    {
        Waiting,
        Deployment,
        Battle,
        DecisiveSelection,
        Finished
    }

    /// <summary>Stato di una carta schierata come lo vede il client.</summary>
    public sealed class PvpClientCard
    {
        public int Slot;
        public string CardId;
        public string CardName;
        public HeroClass HeroClass;
        public int Strength;
        public int Lives;
        public int Initiative;
        public bool Eliminated;
        public bool IsSpirit;
        public bool Inhibited;
        public bool Marked;
        public bool Protecting;
        public int PermanentBonus;
        public int PendingBonus;
        public int DiePenaltySteps;
    }

    /// <summary>
    /// Ricostruisce lo stato del match dagli eventi del server. La UI legge da
    /// qui e non contiene logica di gioco: qualunque discrepanza col server è
    /// un bug di questo replay, non una decisione del client.
    /// </summary>
    public sealed class PvpClientMatchState
    {
        private readonly List<string> log = new();

        public int MyIndex { get; private set; } = -1;
        public string OpponentName { get; private set; } = string.Empty;
        public PvpClientPhase Phase { get; private set; } = PvpClientPhase.Waiting;
        public int MatchRound { get; private set; }
        public int VigorDieSides { get; private set; }
        public int Cycle { get; private set; }
        public int DeployPlayer { get; private set; } = -1;
        public int ActivePlayer { get; private set; } = -1;
        public int ActiveSlot { get; private set; } = -1;
        public int Winner { get; private set; } = -1;
        public int DecisiveRequiredCount { get; private set; }
        public IReadOnlyList<string> Log => log;

        public List<PvpClientCard>[] Boards { get; } = { new(), new() };
        public int[] Wins { get; } = new int[2];
        public PvpAuraType[] Auras { get; } = new PvpAuraType[2];

        /// <summary>Mano corrente come coppie (indice loadout, id definizione).</summary>
        public List<(int LoadoutIndex, string DefinitionId)> Hand { get; } = new();

        public bool IsMyDeployTurn => Phase == PvpClientPhase.Deployment && DeployPlayer == MyIndex;
        public bool IsMyBattleTurn => Phase == PvpClientPhase.Battle && ActivePlayer == MyIndex;

        public event Action Changed;

        public void ApplyMatchStart(MatchStart start)
        {
            MyIndex = start.yourPlayerIndex;
            OpponentName = start.opponentName ?? string.Empty;
            AddLog($"Match contro {OpponentName}.");
            Changed?.Invoke();
        }

        public void ApplyHand(MatchHand hand)
        {
            Hand.Clear();
            if (hand.handIndices != null && hand.handDefinitionIds != null)
            {
                for (int position = 0; position < hand.handIndices.Length; position++)
                    Hand.Add((hand.handIndices[position], hand.handDefinitionIds[position]));
            }
            Changed?.Invoke();
        }

        public void Apply(MatchEventDto e)
        {
            switch (e.type)
            {
                case "RoundStarted":
                    MatchRound = e.matchRound;
                    VigorDieSides = e.vigorDieSides;
                    Cycle = 1;
                    Boards[0].Clear();
                    Boards[1].Clear();
                    Auras[0] = PvpAuraType.None;
                    Auras[1] = PvpAuraType.None;
                    Phase = PvpClientPhase.Waiting;
                    ActivePlayer = -1;
                    DeployPlayer = -1;
                    AddLog($"--- Round {MatchRound}: dado vigore D{VigorDieSides} ---");
                    break;

                case "DecisiveSelectionStarted":
                    Phase = PvpClientPhase.DecisiveSelection;
                    DecisiveRequiredCount = e.requiredCount;
                    AddLog($"Round decisivo: scegli {e.requiredCount} carte tra tutte le 9.");
                    break;

                case "DeploymentStarted":
                    Phase = PvpClientPhase.Deployment;
                    AddLog($"Iniziativa: {e.rollPlayer0} contro {e.rollPlayer1}. Schiera prima {PlayerName(e.firstPlayer)}.");
                    break;

                case "DeployTurn":
                    Phase = PvpClientPhase.Deployment;
                    DeployPlayer = e.player;
                    break;

                case "CardDeployed":
                {
                    Boards[e.player].Add(new PvpClientCard
                    {
                        Slot = e.slot,
                        CardId = e.cardId,
                        CardName = e.cardName,
                        HeroClass = (HeroClass)e.heroClass,
                        Strength = e.strength,
                        Lives = e.lives
                    });
                    if (e.player == MyIndex)
                        RemoveFromHand(e.cardId);
                    AddLog($"{PlayerName(e.player)} schiera {e.cardName} ({e.strength}).");
                    break;
                }

                case "BattleStarted":
                    Phase = PvpClientPhase.Battle;
                    Auras[0] = (PvpAuraType)e.auraPlayer0;
                    Auras[1] = (PvpAuraType)e.auraPlayer1;
                    if (Auras[0] != PvpAuraType.None || Auras[1] != PvpAuraType.None)
                        AddLog($"Aure: {Auras[0]} contro {Auras[1]}.");
                    break;

                case "CardInitiative":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                        card.Initiative = e.initiative;
                    break;
                }

                case "TurnStarted":
                    Phase = PvpClientPhase.Battle;
                    ActivePlayer = e.player;
                    ActiveSlot = e.slot;
                    Cycle = e.cycle;
                    break;

                case "TurnSkipped":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.Inhibited = false;
                        AddLog($"{card.CardName} è inibito e salta il turno.");
                    }
                    break;
                }

                case "AbilityUsed":
                    ApplyAbility(e);
                    break;

                case "CardRevived":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.Eliminated = false;
                        card.Lives = e.lives;
                        AddLog($"{card.CardName} torna in gioco con {e.lives} vita.");
                    }
                    break;
                }

                case "ProtectionTriggered":
                {
                    PvpClientCard paladin = CardAt(e.player, e.slot);
                    if (paladin != null)
                    {
                        paladin.Protecting = false;
                        AddLog(e.redirected
                            ? $"{paladin.CardName} devia l'attacco su di sé."
                            : $"{paladin.CardName} si difende con vantaggio.");
                    }
                    break;
                }

                case "AttackResolved":
                    ApplyAttack(e);
                    break;

                case "AttachmentApplied":
                {
                    PvpClientCard source = CardAt(e.player, e.slot);
                    PvpClientCard target = CardAt(e.player, e.targetSlot);
                    if (source != null)
                    {
                        source.Eliminated = true;
                        source.Lives = 0;
                    }
                    if (target != null)
                        target.PermanentBonus += e.bonus;
                    AddLog($"{source?.CardName} si sacrifica: +{e.bonus} a {target?.CardName}.");
                    break;
                }

                case "FuryGained":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.PendingBonus = e.amount;
                        AddLog($"{card.CardName} entra in Furia (+{e.amount}).");
                    }
                    break;
                }

                case "MightAuraBonus":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.PermanentBonus++;
                        AddLog($"Aura Might: {card.CardName} guadagna +1 permanente.");
                    }
                    break;
                }

                case "SpiritExpired":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.IsSpirit = false;
                        card.Eliminated = true;
                        AddLog($"Lo spirito di {card.CardName} svanisce.");
                    }
                    break;
                }

                case "ActionTimeout":
                    AddLog($"Tempo scaduto per {PlayerName(e.player)} ({e.amount}° timeout).");
                    break;

                case "MatchForfeited":
                    AddLog($"{PlayerName(e.player)} abbandona: vittoria a {PlayerName(e.winner)}.");
                    break;

                case "RoundEnded":
                    Wins[0] = e.winsPlayer0;
                    Wins[1] = e.winsPlayer1;
                    AddLog($"Round {e.matchRound} a {PlayerName(e.winner)}. Parziale {Wins[0]}-{Wins[1]}.");
                    break;

                case "MatchEnded":
                    Wins[0] = e.winsPlayer0;
                    Wins[1] = e.winsPlayer1;
                    Winner = e.winner;
                    Phase = PvpClientPhase.Finished;
                    AddLog(Winner == MyIndex
                        ? $"HAI VINTO IL MATCH {Wins[MyIndex]}-{Wins[1 - MyIndex]}!"
                        : $"Hai perso il match {Wins[MyIndex]}-{Wins[1 - MyIndex]}.");
                    break;
            }
            Changed?.Invoke();
        }

        private void ApplyAbility(MatchEventDto e)
        {
            PvpClientCard actor = CardAt(e.player, e.slot);
            PvpClientCard target = CardAt(e.targetPlayer, e.targetSlot);
            var ability = (HeroClass)e.ability;
            switch (ability)
            {
                case HeroClass.Assassin:
                    if (target != null)
                    {
                        target.Inhibited = true;
                        if (e.magnitude > 0)
                            target.PermanentBonus -= e.magnitude;
                    }
                    AddLog($"{actor?.CardName} inibisce {target?.CardName}.");
                    break;
                case HeroClass.Mage:
                    if (target != null)
                        target.DiePenaltySteps = e.magnitude;
                    AddLog($"{actor?.CardName} abbassa il dado di {target?.CardName} di {e.magnitude} step.");
                    break;
                case HeroClass.Hunter:
                    if (target != null)
                        target.Marked = true;
                    AddLog($"{actor?.CardName} marca {target?.CardName} (+{e.magnitude} a chi lo attacca).");
                    break;
                case HeroClass.Paladin:
                    if (actor != null)
                        actor.Protecting = true;
                    AddLog(actor == target
                        ? $"{actor?.CardName} si prepara a difendersi con vantaggio."
                        : $"{actor?.CardName} protegge {target?.CardName}.");
                    break;
                case HeroClass.Priest:
                    if (target != null)
                        target.PendingBonus += e.magnitude;
                    AddLog($"{actor?.CardName} benedice {target?.CardName} (+{e.magnitude}).");
                    break;
                case HeroClass.Warrior:
                    AddLog($"{actor?.CardName} prepara un colpo a due dadi.");
                    break;
                case HeroClass.Necromancer:
                    AddLog($"{actor?.CardName} rialza {target?.CardName}.");
                    break;
            }
        }

        private void ApplyAttack(MatchEventDto e)
        {
            PvpClientCard attacker = CardAt(e.player, e.slot);
            PvpClientCard defender = CardAt(e.targetPlayer, e.targetSlot);
            if (attacker != null)
            {
                attacker.PendingBonus = 0;
                attacker.DiePenaltySteps = 0;
            }
            if (defender != null)
            {
                defender.DiePenaltySteps = 0;
                defender.Lives = e.defenderRemainingLives;
                if (e.defenderEliminated)
                {
                    defender.Eliminated = true;
                    defender.IsSpirit = false;
                }
                else if (e.becameSpirit)
                {
                    defender.IsSpirit = true;
                }
            }

            string prefix = e.isCounter ? "CONTRATTACCO: " : string.Empty;
            string outcome = e.certainty switch
            {
                "Impossible" => "attacco impossibile, turno perso",
                "Guaranteed" => "eliminazione automatica",
                _ => $"{e.attackerTotal} contro {e.defenderTotal} (D{e.attackerDieSides}/D{e.defenderDieSides})"
            };
            string effect = !e.defenderLostLife
                ? defender != null ? $" {defender.CardName} resiste." : string.Empty
                : e.becameSpirit
                    ? $" {defender?.CardName} resta come spirito!"
                    : e.defenderEliminated
                        ? $" {defender?.CardName} eliminato!"
                        : $" {defender?.CardName} perde una vita ({e.defenderRemainingLives}).";
            AddLog($"{prefix}{attacker?.CardName} attacca {defender?.CardName}: {outcome}.{effect}");
        }

        private void RemoveFromHand(string definitionId)
        {
            for (int position = 0; position < Hand.Count; position++)
            {
                if (Hand[position].DefinitionId == definitionId)
                {
                    Hand.RemoveAt(position);
                    return;
                }
            }
        }

        private PvpClientCard CardAt(int player, int slot)
        {
            if (player is < 0 or > 1)
                return null;
            foreach (PvpClientCard card in Boards[player])
            {
                if (card.Slot == slot)
                    return card;
            }
            return null;
        }

        private string PlayerName(int player) =>
            player == MyIndex ? "TU" : OpponentName.Length > 0 ? OpponentName : $"G{player}";

        private void AddLog(string message)
        {
            log.Add(message);
            if (log.Count > 200)
                log.RemoveAt(0);
        }
    }
}
