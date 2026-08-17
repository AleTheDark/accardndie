using System.Collections.Generic;

namespace AccardND.Localization
{
    /// <summary>Fonte unica dei testi mostrati dai tutorial di classe.</summary>
    public static class TutorialClassTextCatalog
    {
        public readonly struct Entry
        {
            public Entry(string id, string italian, string english)
            {
                Id = id;
                Italian = italian;
                English = english;
            }
            public string Id { get; }
            public string Italian { get; }
            public string English { get; }
            public string Key => KeyFor(Id);
        }

        public static string KeyFor(string id) => "tutorial.class." + id;

        public static readonly IReadOnlyList<Entry> Entries = new Entry[]
        {
            new("rogue_unavailable", "Lezione non disponibile: mancano le carte Ladro nel database.", "Lesson unavailable: Thief cards are missing from the database."),
            new("rogue_scenario", "Lezione - Il Ladro", "Lesson - The Thief"),
            new("mana_title", "IL MANA", "MANA"),
            new("mana_body", "Le abilita' si pagano in mana. La riserva non supera i 10 punti e si recupera fra una stanza e l'altra, non a ogni turno: spenderlo bene e' meta' del gioco.", "Abilities cost mana. Your reserve cannot exceed 10 points and recovers between rooms, not every turn: spending it wisely is half the game."),
            new("technique_title", "LA TECNICA", "THE TECHNIQUE"),
            new("aura_title", "L'AURA", "THE AURA"),
            new("faction_triangle_title", "IL TRIANGOLO DELLE FAZIONI", "THE FACTION TRIANGLE"),
            new("faction_triangle_body", "Ora le conosci tutte e tre: Forzuta batte Astuta, Astuta batte Magica, Magica batte Forzuta. Il triangolo si chiude, e nessuna fazione e' la piu' forte.", "Now you know all three: Might beats Cunning, Cunning beats Magic, and Magic beats Might. The triangle is complete, and no faction is the strongest."),
            new("target_colors_title", "I COLORI DEL BERSAGLIO", "TARGET COLORS"),
            new("target_colors_body", "Quando provi ad attaccare, si accende un'aura intorno alle pedine nemiche: verde significa che hai vantaggio, gialla che siete della stessa fazione, rossa che hai svantaggio.", "When you attack, an aura appears around enemy pawns: green means you have advantage, yellow means both belong to the same faction, and red means you have disadvantage."),
            new("warrior_primary_effect", "tira il dado Vigore piu' un dado di uno step inferiore e li somma alla potenza", "roll the Vigor die plus a die one step lower and add both results to Power"),
            new("mage_primary_effect", "abbassa di uno step il dado Vigore di un nemico per il suo prossimo combattimento", "reduce an enemy's Vigor die by one step for its next combat"),
            new("generic_primary_effect", "attiva l'effetto della sua classe", "activate the class effect"),
            new("expensive_ability_note", " E' l'abilita' piu' cara del gioco: mezza riserva in un colpo, quindi si usa quando conta.", " It is the most expensive ability in the game: half your reserve at once, so use it when it matters."),
            new("ability_title", "L'ABILITA'", "THE ABILITY"),
            new("active_ability_body", "{0}: premi ABILITA' e {1}. Costa {2} mana.{3}", "{0}: press ABILITY and {1}. It costs {2} mana.{3}"),
            new("rogue_passive_effect", "quando tira male, ritira il dado da solo", "when it rolls poorly, it rerolls the die automatically"),
            new("barbarian_passive_effect", "accumula furia quando una pedina cade", "build Fury whenever a pawn falls"),
            new("generic_passive_effect", "agisce da solo", "trigger automatically"),
            new("passive_title", "UN'ABILITA' CHE NON SI PREME", "AN ABILITY YOU DO NOT PRESS"),
            new("passive_body", "{0} non ha un pulsante ABILITA': la sua e' passiva. {1}, sempre, senza costare mana.", "{0} has no ABILITY button: its ability is passive. It will {1}, at all times and without spending mana."),
            new("supreme_body", "Ogni classe ha una seconda abilita', la suprema. Quella del {0} e' {1} ({2} mana): {3}\n\nQui te la faccio provare, ma non e' ancora tua: si impara al Santuario, all'altare delle Tecniche.", "Every class has a second ability: its Supreme. The {0}'s Supreme is {1} ({2} mana): {3}\n\nYou can try it here, but you do not own it yet. Learn it at the Techniques altar in the Sanctuary."),
            new("aura_body", "{0} appartiene alla fazione {1}, che batte {2} e perde contro {3}. Chi ha vantaggio tira due dadi Vigore e tiene il migliore; chi ha svantaggio tiene il peggiore.", "{0} belongs to the {1} faction, which beats {2} and loses to {3}. With advantage, roll two Vigor dice and keep the higher result; with disadvantage, keep the lower result."),
            new("mage_unavailable", "Lezione del Mago non disponibile: mancano le pedine nel database.", "Magician lesson unavailable: pawns are missing from the database."),
            new("mage_intro_title", "IL MAGO", "THE MAGICIAN"),
            new("mage_intro_body", "Impariamo il mago. Userai prima l'attacco base, poi l'abilita' del Mago e infine la sua Suprema contro una nuova ondata di nemici.", "Learn how to use the Magician. First use a basic attack, then the Magician's ability, and finally its Supreme against a new wave of enemies."),
            new("mage_scenario", "Lezione - Il Mago", "Lesson - The Magician"),
            new("mage_base_attack_title", "ATTACCO BASE", "BASIC ATTACK"),
            new("mage_base_attack_body", "Cominciamo dal colpo normale. Premi CONTINUA, poi ATTACCA e scegli soltanto la pedina Mago da 2.", "Start with a normal strike. Press CONTINUE, then ATTACK, and select only the Power-2 Magician pawn."),
            new("mage_ability_title", "ABILITA' DEL MAGO", "MAGICIAN ABILITY"),
            new("mage_ability_body", "Ora usa ABILITA' sulla pedina Mago da 4. Costa {0} mana e abbassa di uno step il suo prossimo dado Vigore.", "Now use ABILITY on the Power-4 Magician pawn. It costs {0} mana and reduces its next Vigor die by one step."),
            new("mage_exploit_penalty_title", "SFRUTTA IL MALUS", "EXPLOIT THE PENALTY"),
            new("mage_exploit_penalty_body", "Il suo dado Vigore e' stato ridotto. Premi CONTINUA, poi ATTACCA e colpisci la stessa pedina da 4.", "Its Vigor die has been reduced. Press CONTINUE, then ATTACK, and strike the same Power-4 pawn."),
            new("mage_supreme_title", "NUOVA ONDATA: SUPREMA", "NEW WAVE: SUPREME"),
            new("mage_supreme_body", "Sono comparse altre tre pedine Mago. Premi CONTINUA e usa SUPREMA: Palla di Fuoco costa {0} mana e affronta tutti i nemici insieme.", "Three more Magician pawns have appeared. Press CONTINUE and use SUPREME: Fireball costs {0} mana and attacks all enemies at once."),
            new("mage_complete_title", "LEZIONE DEL MAGO COMPLETATA", "MAGICIAN LESSON COMPLETE"),
            new("mage_complete_body", "Hai usato attacco base, abilita' e Suprema del Mago. Premi CONTINUA per ricevere la ricompensa e proseguire verso il Ladro.", "You used the Magician's basic attack, ability, and Supreme. Press CONTINUE to receive your reward and continue toward the Thief."),
            new("rogue_practice_intro_title", "VEDIAMO IL LADRO IN AZIONE", "SEE THE THIEF IN ACTION"),
            new("rogue_practice_intro_body", "Vediamo l'abilita' passiva del Ladro in azione: combattiamo! Attaccherai una pedina per ogni fazione e vedrai un tiro basso trasformarsi in una vittoria grazie al reroll.", "See the Thief's passive ability in action: let us fight! You will attack one pawn from each faction and watch a low roll turn into a victory thanks to the reroll."),
            new("rogue_practice_same_faction_title", "STESSA FAZIONE: GIALLO", "SAME FACTION: YELLOW"),
            new("rogue_practice_same_faction_body", "Il primo tiro avrebbe perso, ma il Ladro lo ha ritirato e ha vinto. Ora ripetiamo contro il Ladro: l'aura gialla indica la stessa fazione.", "The first roll would have lost, but the Thief rerolled it and won. Now repeat the attack against the Thief: the yellow aura indicates the same faction."),
            new("rogue_practice_disadvantage_title", "SVANTAGGIO: ROSSO", "DISADVANTAGE: RED"),
            new("rogue_practice_disadvantage_body", "Anche il secondo scontro e' stato ribaltato dal reroll. Ora attacca il Guerriero: l'aura rossa indica svantaggio, ma la passiva puo' ancora salvare un tiro basso.", "The reroll also turned the second fight around. Now attack the Warrior: the red aura indicates disadvantage, but the passive ability can still save a low roll."),
            new("rogue_practice_complete_title", "STAI ANDANDO ALLA GRANDE", "YOU ARE DOING GREAT"),
            new("rogue_practice_complete_body", "Hai la stoffa adatta! Vai nel Negozio per ottenere la tua ricompensa.", "You have what it takes! Go to the Shop to receive your reward."),
            new("warrior_room_mage", "TUTORIAL MAGO", "MAGICIAN TUTORIAL"),
            new("warrior_room_rogue", "TUTORIAL LADRO", "THIEF TUTORIAL"),
            new("warrior_room_warrior", "TUTORIAL GUERRIERO", "WARRIOR TUTORIAL"),
            new("warrior_cards_missing", "Lezione non disponibile: mancano le carte nel database.", "Lesson unavailable: required cards are missing from the database."),
            new("warrior_welcome_title", "BENVENUTO", "WELCOME"),
            new("warrior_welcome_body", "Sei in una stanza guidata: ti spiego alcuni elementi dell'interfaccia e poi combattiamo insieme.", "This is a guided room: I will explain a few interface elements, then we will fight together."),
            new("scenario_name", "Lezione - Il Guerriero", "Lesson - The Warrior"),
            new("warrior_mana_title", "LA RISERVA DI MANA", "THE MANA POOL"),
            new("warrior_mana_body", "Quella blu è la tua riserva di mana, quella rossa è dell'avversario. Il mana serve per usare abilità, supreme e attacchi base. Si recupera parando o alla fine del proprio turno.", "The blue pool is your mana; the red one belongs to your opponent. Mana powers abilities, supreme techniques, and basic attacks. You recover it when you block or at the end of your turn."),
            new("warrior_vigor_title", "I DADI VIGORE", "VIGOR DICE"),
            new("warrior_vigor_body", "Queste due icone mostrano il tuo dado Vigore e quello dell'avversario. A ogni scontro entrambi lo tirano e sommano il risultato alla Potenza della propria pedina: vince il totale più alto.", "These icons show your Vigor die and your opponent's. In each clash, both dice are rolled and added to the pawn's Power. The highest total wins."),
            new("warrior_attack_weak_title", "ATTACCA IL PIÙ DEBOLE", "ATTACK THE WEAKEST"),
            new("warrior_attack_weak_body", "Attacca il mostro con Potenza 4: sei più forte e hai poche probabilità di perdere. Premi ATTACCA e seleziona la pedina con Potenza 4.", "Attack the monster with 4 Power. You are stronger and unlikely to lose. Press ATTACK and select the pawn with 4 Power."),
            new("warrior_attack_strong_title", "PROVA COL PIÙ FORTE", "TRY THE STRONGEST"),
            new("warrior_attack_strong_body", "Resta il Guerriero da 10. Tu sei un 6: attaccalo e guarda cosa succede.", "The 10-Power Warrior remains. You have 6 Power: attack him and see what happens."),
            new("warrior_retry_title", "ORA RIPROVA", "NOW TRY AGAIN"),
            new("warrior_retry_body", "Stessa pedina, stesso attacco base: l'unica cosa cambiata è la tua Potenza. Colpisci il Guerriero da 10.", "Same pawn, same basic attack: only your Power has changed. Strike the 10-Power Warrior."),
            new("warrior_complete_title", "LEZIONE FINITA", "LESSON COMPLETE"),
            new("warrior_complete_body", "Hai visto le tre cose che contano: il mana che paga le abilità, il Colpo pesante che raddoppia i dadi e la tecnica che aumenta la Potenza. La suprema però non è ancora tua: si impara al Santuario.", "You have seen the three essentials: mana pays for abilities, Heavy Strike doubles the dice, and the technique raises Power. The supreme technique is not yours yet: you learn it at the Sanctuary."),
            new("warrior_ability_title", "L'ABILITÀ DEL GUERRIERO", "THE WARRIOR'S ABILITY"),
            new("warrior_ability_body", "Il Guerriero da 4 è a terra. Il prossimo è un 7 e con la sola Potenza perderesti. Premi ABILITÀ: Colpo pesante costa {0} mana, tira due dadi Vigore e ne somma i risultati.", "The 4-Power Warrior is down. The next has 7 Power, and Power alone would not be enough. Press ABILITY: Heavy Strike costs {0} mana, rolls two Vigor dice, and adds their results."),
            new("warrior_impossible_title", "MATEMATICAMENTE IMPOSSIBILE", "MATHEMATICALLY IMPOSSIBLE"),
            new("warrior_impossible_body", "Hai ottenuto il massimo del Vigore, 4, ma 6 + 4 arriva soltanto a 10. Il Guerriero da 10 ottiene almeno 1 e arriva a 11: con un D4 non puoi superarlo.", "You rolled the maximum Vigor result, 4, but 6 + 4 only reaches 10. The 10-Power Warrior rolls at least 1 and reaches 11, so you cannot beat him with a D4."),
            new("warrior_supreme_title", "LA SUPREMA DEL GUERRIERO", "THE WARRIOR'S SUPREME TECHNIQUE"),
            new("warrior_supreme_body", "Le Supreme sono tecniche speciali che si sbloccano al Santuario. In questa lezione proverai Potenziamento ({0} mana): aumenta la Potenza di 2, oppure di 4 se è l'ultima pedina in campo. Sei rimasto solo, quindi diventi un 10. Premi SUPREMA.", "Supreme techniques are special moves unlocked at the Sanctuary. In this lesson you will try Empowerment ({0} mana): it raises Power by 2, or by 4 when this is your last pawn. You are alone, so your Power becomes 10. Press SUPREME."),
        };

        private static readonly Dictionary<string, Entry> ById = BuildIndex();

        public static bool TryGet(string id, out Entry entry) =>
            ById.TryGetValue(id ?? string.Empty, out entry);

        private static Dictionary<string, Entry> BuildIndex()
        {
            var index = new Dictionary<string, Entry>(System.StringComparer.Ordinal);
            foreach (Entry entry in Entries)
                index[entry.Id] = entry;
            return index;
        }
    }
}
