using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;

namespace AccardND.Presentation
{
    public sealed class BattlePrototypeController : MonoBehaviour
    {
        private const int VigorDieSides = 6;

        private readonly List<CardState> playerCards = new();
        private readonly List<CardState> cpuCards = new();

        private IRandomSource random;
        private CombatResolver combatResolver;
        private int selectedPlayerIndex = -1;
        private string battleLog = "Scegli una tua carta, poi scegli il bersaglio CPU.";
        private bool gameFinished;

        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle cardLabelStyle;
        private GUIStyle messageStyle;
        private GUIStyle overlayStyle;

        private static void CreatePrototype()
        {
            if (FindAnyObjectByType<BattlePrototypeController>() != null)
                return;

            var prototype = new GameObject("Battle Prototype");
            DontDestroyOnLoad(prototype);
            prototype.AddComponent<BattlePrototypeController>();
        }

        private void Awake()
        {
            random = new SeededRandomSource(20260620);
            combatResolver = new CombatResolver(random);

            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            if (database == null)
            {
                battleLog = "Database carte non ancora generato. Esci da Play e usa Accard N' Die > Rebuild Card Database.";
                return;
            }

            AddCard(playerCards, database, "4-animal-assassin");
            AddCard(playerCards, database, "5-darkelf-mage");
            AddCard(playerCards, database, "6-chimera-tank");

            AddCard(cpuCards, database, "4-animal-warrior");
            AddCard(cpuCards, database, "5-darkelf-assassin");
            AddCard(cpuCards, database, "6-chimera-mage");
        }

        private static void AddCard(ICollection<CardState> destination, CardDatabase database, string cardId)
        {
            CardDefinition definition = database.FindById(cardId);
            if (definition == null || !definition.CanEnterCombat)
                return;

            destination.Add(new CardState(definition.CreateCombatCard(), definition.Artwork));
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawBackground();

            float scale = Mathf.Clamp(Screen.height / 900f, 0.7f, 1.25f);
            float padding = 22f * scale;
            float gap = 18f * scale;
            float cardSize = Mathf.Min(
                230f * scale,
                (Screen.width - padding * 2f - gap * 2f) / 3f);

            GUI.Label(new Rect(0f, 12f * scale, Screen.width, 48f * scale), "ACCARD N' DIE", titleStyle);
            GUI.Label(new Rect(padding, 64f * scale, 220f, 34f * scale), "CPU", sectionStyle);

            float cpuY = 102f * scale;
            DrawRow(cpuCards, cpuY, cardSize, gap, padding, false);

            float messageY = cpuY + cardSize + 14f * scale;
            GUI.Label(
                new Rect(padding, messageY, Screen.width - padding * 2f, 72f * scale),
                battleLog,
                messageStyle);

            float playerY = Screen.height - cardSize - 54f * scale;
            GUI.Label(new Rect(padding, playerY - 38f * scale, 260f, 34f * scale), "LA TUA FORMAZIONE", sectionStyle);
            DrawRow(playerCards, playerY, cardSize, gap, padding, true);

            if (playerCards.Count == 0 || cpuCards.Count == 0)
            {
                GUI.Label(
                    new Rect(padding, Screen.height * 0.45f, Screen.width - padding * 2f, 80f),
                    "Le immagini sono ancora in importazione. Ferma Play e riprova tra qualche secondo.",
                    messageStyle);
            }
        }

        private void DrawRow(
            IReadOnlyList<CardState> cards,
            float y,
            float cardSize,
            float gap,
            float padding,
            bool isPlayer)
        {
            float rowWidth = cards.Count * cardSize + Mathf.Max(0, cards.Count - 1) * gap;
            float x = Mathf.Max(padding, (Screen.width - rowWidth) * 0.5f);

            for (int index = 0; index < cards.Count; index++)
            {
                Rect rect = new(x + index * (cardSize + gap), y, cardSize, cardSize);
                DrawCard(cards[index], rect, isPlayer && index == selectedPlayerIndex);

                if (!gameFinished && !cards[index].Eliminated && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    if (isPlayer)
                        SelectAttacker(index);
                    else
                        SelectTarget(index);
                }
            }
        }

        private void DrawCard(CardState state, Rect rect, bool selected)
        {
            Color previousColor = GUI.color;
            GUI.color = state.Eliminated ? new Color(0.25f, 0.25f, 0.25f, 0.65f) : Color.white;

            GUI.Box(rect, GUIContent.none);
            if (state.Art != null)
            {
                Rect imageRect = new(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
                GUI.DrawTexture(imageRect, state.Art.texture, ScaleMode.ScaleToFit, false);
            }

            GUI.color = previousColor;

            Rect labelRect = new(rect.x + 5f, rect.yMax - 52f, rect.width - 10f, 47f);
            GUI.Box(labelRect, GUIContent.none);
            GUI.Label(
                labelRect,
                $"{state.Card.Name}\n{ClassName(state.Card.HeroClass)}  •  Forza {state.Card.Strength}",
                cardLabelStyle);

            if (selected)
                DrawBorder(rect, new Color(0.2f, 0.95f, 0.65f), 5f);

            if (state.Eliminated)
                GUI.Label(rect, "ELIMINATA", overlayStyle);
        }

        private void SelectAttacker(int index)
        {
            if (playerCards[index].Eliminated)
                return;

            selectedPlayerIndex = index;
            battleLog = $"{playerCards[index].Card.Name} è pronta. Scegli una carta CPU da attaccare.";
        }

        private void SelectTarget(int cpuIndex)
        {
            if (selectedPlayerIndex < 0)
            {
                battleLog = "Prima scegli una carta della tua formazione.";
                return;
            }

            CardState attacker = playerCards[selectedPlayerIndex];
            CardState defender = cpuCards[cpuIndex];
            CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, VigorDieSides);

            if (result.DefenderIsDefeated)
                defender.Eliminated = true;

            battleLog = FormatResult("TU", attacker, defender, result);
            selectedPlayerIndex = -1;

            if (CheckEndGame())
                return;

            ExecuteCpuTurn();
            CheckEndGame();
        }

        private void ExecuteCpuTurn()
        {
            int attackerIndex = PickAliveIndex(cpuCards);
            int defenderIndex = PickAliveIndex(playerCards);
            if (attackerIndex < 0 || defenderIndex < 0)
                return;

            CardState attacker = cpuCards[attackerIndex];
            CardState defender = playerCards[defenderIndex];
            CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, VigorDieSides);

            if (result.DefenderIsDefeated)
                defender.Eliminated = true;

            battleLog += "\n" + FormatResult("CPU", attacker, defender, result);
        }

        private int PickAliveIndex(IReadOnlyList<CardState> cards)
        {
            var alive = new List<int>();
            for (int index = 0; index < cards.Count; index++)
            {
                if (!cards[index].Eliminated)
                    alive.Add(index);
            }

            return alive.Count == 0 ? -1 : alive[random.NextInclusive(0, alive.Count - 1)];
        }

        private bool CheckEndGame()
        {
            bool playerAlive = HasAliveCard(playerCards);
            bool cpuAlive = HasAliveCard(cpuCards);

            if (playerAlive && cpuAlive)
                return false;

            gameFinished = true;
            battleLog = playerAlive
                ? "VITTORIA! Hai eliminato la formazione CPU."
                : "SCONFITTA. La CPU ha eliminato la tua formazione.";
            return true;
        }

        private static bool HasAliveCard(IReadOnlyList<CardState> cards)
        {
            foreach (CardState card in cards)
            {
                if (!card.Eliminated)
                    return true;
            }

            return false;
        }

        private static string FormatResult(
            string actor,
            CardState attacker,
            CardState defender,
            CombatResult result)
        {
            string outcome = result.DefenderIsDefeated ? "eliminata" : "resiste";
            return $"{actor}: {attacker.Card.Name} attacca {defender.Card.Name} — "
                + $"{result.AttackerTotal} contro {result.DefenderTotal}: {outcome}.";
        }

        private static string ClassName(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => "Assassino",
                HeroClass.Warrior => "Guerriero",
                HeroClass.Mage => "Mago",
                HeroClass.Paladin => "Paladino",
                HeroClass.Rogue => "Ladro",
                HeroClass.Hunter => "Cacciatore",
                HeroClass.Barbarian => "Barbaro",
                HeroClass.Necromancer => "Negromante",
                HeroClass.Priest => "Sacerdote",
                _ => heroClass.ToString()
            };
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.82f, 0.47f) }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            cardLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.9f, 0.94f) }
            };
            overlayStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.35f, 0.3f) }
            };
        }

        private static void DrawBackground()
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.035f, 0.055f, 0.075f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private sealed class CardState
        {
            public CardState(CombatCard card, Sprite art)
            {
                Card = card;
                Art = art;
            }

            public CombatCard Card { get; }
            public Sprite Art { get; }
            public bool Eliminated { get; set; }
        }
    }
}
