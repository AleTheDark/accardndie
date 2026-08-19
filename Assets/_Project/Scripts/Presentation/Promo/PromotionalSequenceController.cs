using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>
    /// Trailer promozionale che si registra da solo: si apre la scena, si preme
    /// Play e la sequenza parte. Nessun bootstrap di gioco, nessun input.
    ///
    /// Segue il beat sheet di Docs/trailer-lancio.md §3, che dimostra una frase
    /// sola — "la fortuna conta, ma e' la tattica a piegarla" — in tre tempi:
    /// il dado ti frega, scopri che puoi truccarlo, il tiro impossibile passa.
    ///
    /// Non imita niente: le pedine sono <see cref="PrototypeCardView"/> create
    /// come in partita, il boss usa la sua presentazione a fondale, i colpi
    /// passano da <see cref="BattlePresentationAnimationPlayer"/> e i suoni da
    /// <see cref="BattleSfxPlayer"/>. Quello che si registra qui e' quello che
    /// il giocatore vede, ed e' l'unico modo perche' il trailer resti vero
    /// quando il gioco cambia.
    /// </summary>
    public sealed class PromotionalSequenceController : MonoBehaviour
    {
        private const int ShotCount = 4;

        [Header("Riproduzione")]
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool showFramingGuides;

        [Header("Le pedine")]
        [SerializeField] private HeroClass[] playerClasses = { HeroClass.Warrior, HeroClass.Rogue, HeroClass.Mage };
        [SerializeField] private HeroClass[] enemyClasses = { HeroClass.Necromancer, HeroClass.Barbarian, HeroClass.Hunter };

        [Header("Il boss del payoff")]
        [SerializeField] private string bossCardId = "boss-jurinashor";
        [SerializeField] private string bossBackground = "Backgrounds/bg_jurinashor_phase_1";
        [SerializeField] private string swordCardId = "boss-jurinashor-sword";
        [SerializeField] private int summonedSwords = 3;

        [Header("Scenari")]
        [SerializeField] private string coldOpenBackground = "Backgrounds/bg_arena_1";
        [SerializeField] private string montageBackground = "Backgrounds/bg_arena_2";

        [Header("I due tiri")]
        [SerializeField] private int dieSides = 20;
        [SerializeField] private int coldOpenResult = 3;
        [SerializeField] private float coldOpenRollDuration = 1.5f;
        [SerializeField] private int payoffResult = 20;
        [SerializeField] private float payoffRollDuration = 2.6f;

        private GameConfiguration configuration;
        private CardDatabase database;
        private BattlePresentationAnimationPlayer animations;
        private BattleSfxPlayer sfx;

        private CanvasGroup fade;
        private Image background;
        private Image flash;
        private RectTransform playerRow;
        private RectTransform enemyRow;
        private RectTransform swordLayer;
        private readonly List<PrototypeCardView> swordViews = new List<PrototypeCardView>();

        // Misure del campo, calcolate come le calcola la partita vera
        // (BattleBoardController.ApplyResponsiveLayout): stessa dimensione di
        // pedina, stesso gioco d'aria, stessa altezza delle due file. Con la
        // pedina alla misura giusta il numero di Potenza torna al suo posto
        // sulla carta invece di scivolare in basso.
        private float cardSize = 320f;
        private float cardGap = 30f;
        private float rowWidth = 1050f;
        private float enemyAnchor = 0.895f;
        private float playerAnchor = 0.19f;
        private readonly List<PrototypeCardView> playerViews = new List<PrototypeCardView>();
        private readonly List<PrototypeCardView> enemyViews = new List<PrototypeCardView>();
        private RectTransform dieSlot;
        private CanvasGroup dieGroup;
        private Dice3DRollView dieView;
        private CanvasGroup cardGroup;
        private Text cardText;
        private Text cardCaption;
        private RectTransform guides;
        private int currentShot;

        private HeroClass LeadClass =>
            playerClasses != null && playerClasses.Length > 0 ? playerClasses[0] : HeroClass.Warrior;

        private HeroClass EnemyLeadClass =>
            enemyClasses != null && enemyClasses.Length > 0 ? enemyClasses[0] : HeroClass.Necromancer;

        private IEnumerator Start()
        {
            // Il trailer e' un'animazione continua: tiene il frame rate alto per
            // tutta la sua durata invece di lasciarlo decidere al governor.
            FrameRateGovernor.Acquire(this);
            configuration = Resources.Load<GameConfiguration>("GameConfiguration")
                ?? ScriptableObject.CreateInstance<GameConfiguration>();
            database = Resources.Load<CardDatabase>("CardDatabase");
            if (database == null)
                Debug.LogError("[Trailer] CardDatabase non trovato in Resources: le pedine non si possono costruire.");

            EnsureEventSystem();
            BuildView();

            animations = gameObject.AddComponent<BattlePresentationAnimationPlayer>();
            sfx = new BattleSfxPlayer();
            sfx.Initialize(transform, "Trailer SFX");

            yield return null;
            yield return RunFrom(0);
        }

        private void OnDisable() => FrameRateGovernor.Release(this);

        // I comandi di cattura: sono documentati in Docs/trailer-lancio.md §5.
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.rKey.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame)
                Restart(0);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                Restart(1);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                Restart(2);
            else if (keyboard.digit4Key.wasPressedThisFrame)
                Restart(3);
            else if (keyboard.spaceKey.wasPressedThisFrame)
                Restart(currentShot);

            if (keyboard.gKey.wasPressedThisFrame)
            {
                showFramingGuides = !showFramingGuides;
                guides.gameObject.SetActive(showFramingGuides);
            }
        }

        private void Restart(int shot)
        {
            // StopAllCoroutines e non StopCoroutine: le riprese lanciano colpi in
            // parallelo, e un riavvio deve portarsi via anche quelli. Le
            // animazioni di battaglia girano su un componente loro e vanno
            // fermate a parte.
            StopAllCoroutines();
            if (animations != null)
                animations.StopAllCoroutines();
            dieView.Hide();
            StartCoroutine(RunFrom(shot));
        }

        /// <summary>
        /// Riproduce dalla ripresa indicata fino in fondo. Ogni ripresa rimonta
        /// da se' il campo che le serve, cosi' saltarci dentro col tastierino
        /// da' la stessa immagine che si vedrebbe arrivandoci dall'inizio.
        /// </summary>
        private IEnumerator RunFrom(int shot)
        {
            int start = Mathf.Clamp(shot, 0, ShotCount - 1);
            do
            {
                for (int index = start; index < ShotCount; index++)
                {
                    currentShot = index;
                    switch (index)
                    {
                        case 0: yield return ColdOpen(); break;
                        case 1: yield return Card("Il dado decide."); break;
                        case 2: yield return Montage(); break;
                        default: yield return Payoff(); break;
                    }
                }

                yield return TitleCard();
                start = 0;
                if (loop)
                    yield return Wait(0.8f);
            }
            while (loop);
        }

        // ---------------------------------------------------------------- 0:00
        // Cold open. Il primo fotogramma e' il dado, non il logo: su Play il
        // video parte muto e in autoplay, e i primi tre secondi sono tutto.
        private IEnumerator ColdOpen()
        {
            SetBackground(coldOpenBackground);
            cardGroup.alpha = 0f;
            ClearRows();
            SpawnPlayerRow();
            SpawnEnemyRow();

            dieSlot.sizeDelta = new Vector2(420f, 420f);
            yield return FadeTo(0f, 0.3f);

            // Il dado del cold open porta i colori dell'avversario: e' il suo
            // tiro, ed e' quello che ti frega.
            yield return RollDie(EnemyLeadClass, coldOpenResult, coldOpenRollDuration);
            yield return Wait(0.45f);
            yield return FadeDie(0.35f);

            // Il colpo e' quello vero della classe che attacca, con il suo VFX e
            // il suo suono, esattamente come lo vedresti in partita.
            PrototypeCardView attacker = enemyViews.FirstOrDefault();
            PrototypeCardView victim = playerViews.Count > 1 ? playerViews[1] : playerViews.FirstOrDefault();
            if (attacker != null && victim != null)
            {
                sfx.PlayAttackResult(EnemyLeadClass, hit: true);
                yield return animations.PlayClassAttack(attacker, victim, EnemyLeadClass, hit: true);
                sfx.PlayLoseFirstHp();
                yield return Wait(0.3f);
                sfx.PlayDeath();
                yield return FadeOut(victim, 0.45f);
            }
            yield return Wait(0.5f);
        }

        // ---------------------------------------------------------------- 0:10
        // Montaggio: tre battute, un'idea per battuta. Nessun testo esplicativo,
        // il regolamento sta sul sito.
        private IEnumerator Montage()
        {
            SetBackground(montageBackground);
            cardGroup.alpha = 0f;
            ClearRows();
            yield return FadeTo(0f, 0.25f);

            // 1. Schieramento: le pedine entrano una alla volta, ognuna col suono
            //    di ingresso della sua classe, e sono il triangolo delle fazioni.
            yield return DeployOneByOne();
            yield return Wait(0.4f);

            // 2. Un'abilita' di classe vera: la costellazione del Paladino sulla
            //    pedina di mezzo, col suo suono e il callout di forza.
            PrototypeCardView blessed = playerViews.Count > 1 ? playerViews[1] : playerViews.FirstOrDefault();
            if (blessed != null)
            {
                sfx.PlayClassAbility(HeroClass.Paladin);
                yield return animations.PlayPaladinProtectionConstellation(blessed);
                blessed.PlayStrengthIncreaseCallout(2);
                yield return Wait(0.5f);
            }

            // 3. Il marchio del Cacciatore su un bersaglio: si vede che il piano
            //    dell'avversario si puo' rompere prima ancora di tirare.
            PrototypeCardView marked = enemyViews.FirstOrDefault();
            if (marked != null)
            {
                sfx.PlayClassAbility(HeroClass.Hunter);
                yield return animations.PlayHunterMarkReticle(marked);
                yield return Wait(0.4f);
            }
        }

        // ---------------------------------------------------------------- 0:21
        // Payoff: lo stesso tiro del cold open, stesso dado, ma stavolta e' il
        // tuo e la build lo regge. Il tiro e' piu' lungo apposta: il dado anima
        // a tempo non scalato, quindi il rallentatore si fa allungando la durata
        // in cattura, non abbassando Time.timeScale.
        private IEnumerator Payoff()
        {
            yield return Card("Tu decidi il dado.");
            SetBackground(bossBackground);
            cardGroup.alpha = 0f;
            ClearRows();
            SpawnPlayerRow();
            SpawnBoss();

            dieSlot.sizeDelta = new Vector2(520f, 520f);
            yield return FadeTo(0f, 0.25f);

            PrototypeCardView boss = enemyViews.FirstOrDefault();
            PrototypeCardView hero = playerViews.FirstOrDefault();

            // Jurinashor evoca le sue spade: sono la prima fase, e sono anche
            // la ragione per cui il colpo che segue non passa.
            yield return SummonSwords(summonedSwords);

            // Il primo colpo viene deviato da una lama. Senza questo rifiuto il
            // tiro che segue non ha niente da ribaltare, e il payoff diventa un
            // attacco qualunque andato a segno.
            if (boss != null && hero != null)
            {
                Coroutine incoming = StartCoroutine(
                    animations.PlayClassAttack(hero, boss, LeadClass, hit: false));
                // La lama entra in rotazione quando il colpo raggiunge la
                // sagoma del boss, non al lancio: sono gli stessi 0,28s della
                // partita.
                yield return Wait(0.28f);
                sfx.PlayAttackResult(HeroClass.Necromancer, hit: false);
                yield return animations.PlayJurinashorPhaseOneDeflection(hero, boss);
                yield return incoming;
                yield return Wait(0.3f);
            }

            yield return RollDie(LeadClass, payoffResult, payoffRollDuration);

            // Il colpo piu' forte del brano va montato qui, sul dado che si
            // ferma: non un fotogramma dopo.
            yield return Flash(0.12f, 0.85f);
            yield return FadeDie(0.25f);

            if (hero != null)
            {
                hero.PlaySupremeActionCallout();
                sfx.PlayWarriorSupreme();
                WarriorSupremeVfx.Activate(hero, 2);
                yield return Wait(0.5f);
            }

            if (hero != null && boss != null)
            {
                sfx.PlayAttackResult(LeadClass, hit: true);
                yield return animations.PlayClassAttack(hero, boss, LeadClass, hit: true, abilityAttack: true);
                sfx.PlayDeath();
                yield return FadeOut(boss, 0.6f);
            }
            yield return Wait(0.5f);
        }

        // ---------------------------------------------------------------- 0:26
        private IEnumerator TitleCard()
        {
            yield return FadeTo(1f, 0.4f);
            ClearRows();
            cardText.text = "AcCard N' Die";
            cardCaption.text = "Tre carte in campo. Nove classi.\nUn dado che decide tutto.";
            cardGroup.alpha = 1f;
            yield return FadeTo(0f, 0.4f);
            yield return Wait(2f);
            yield return FadeTo(1f, 0.5f);
            cardGroup.alpha = 0f;
        }

        // Le due card di testo. Niente voce fuori campo: spediamo in sei lingue
        // e una card si riesporta in dieci minuti, un doppiaggio no. La card
        // chiude sul nero, cosi' la ripresa dopo comincia sempre da li'.
        private IEnumerator Card(string line)
        {
            yield return FadeTo(1f, 0.25f);
            ClearRows();
            cardText.text = line;
            cardCaption.text = string.Empty;
            cardGroup.alpha = 0f;
            yield return FadeTo(0f, 0.2f);
            yield return FadeGroup(cardGroup, 1f, 0.3f);
            yield return Wait(1.4f);
            yield return FadeTo(1f, 0.3f);
        }

        // -------------------------------------------------------------- pedine

        /// <summary>
        /// Schieramento a una pedina per volta, ognuna col suono di ingresso
        /// della sua classe: e' la battuta di apertura del montaggio.
        /// </summary>
        private IEnumerator DeployOneByOne()
        {
            for (int index = 0; index < SafeLength(playerClasses); index++)
            {
                PrototypeCardView view = SpawnPawn(playerRow, playerClasses[index], playerViews);
                LayoutRow(playerRow, playerViews);
                if (view != null)
                    sfx.PlayJoinBattlefield(playerClasses[index]);
                yield return Wait(0.28f);
            }
            for (int index = 0; index < SafeLength(enemyClasses); index++)
            {
                SpawnPawn(enemyRow, enemyClasses[index], enemyViews);
                LayoutRow(enemyRow, enemyViews);
                yield return Wait(0.16f);
            }
        }

        private void SpawnPlayerRow()
        {
            for (int index = 0; index < SafeLength(playerClasses); index++)
                SpawnPawn(playerRow, playerClasses[index], playerViews);
            LayoutRow(playerRow, playerViews);
        }

        private void SpawnEnemyRow()
        {
            for (int index = 0; index < SafeLength(enemyClasses); index++)
                SpawnPawn(enemyRow, enemyClasses[index], enemyViews);
            LayoutRow(enemyRow, enemyViews);
        }

        /// <summary>
        /// Il boss come lo vede il giocatore: la carta a fondale dietro la
        /// formazione, non una pedina in fila.
        /// </summary>
        private void SpawnBoss()
        {
            CardDefinition definition = database != null ? database.FindById(bossCardId) : null;
            if (definition == null)
            {
                Debug.LogWarning($"[Trailer] Boss '{bossCardId}' non trovato nel CardDatabase: il payoff resta senza bersaglio.");
                return;
            }

            PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview(enemyRow, definition, configuration);
            ConfigureBossPresentation(view, definition.Id);
            if (string.Equals(definition.Id, "boss-jurinashor", System.StringComparison.OrdinalIgnoreCase))
                view.SetJurinashorPhaseTwoContour(false);
            view.SetStrengthValue(definition.Strength);
            enemyViews.Add(view);
            LayoutRow(enemyRow, enemyViews);
        }

        /// <summary>
        /// Le spade maledette della prima fase di Jurinashor, evocate una alla
        /// volta con la loro animazione necromantica e il loro suono. Stanno
        /// appese alla radice del canvas alle stesse quote della partita, non
        /// in fila con le pedine.
        /// </summary>
        private IEnumerator SummonSwords(int count)
        {
            CardDefinition definition = database != null ? database.FindById(swordCardId) : null;
            if (definition == null)
            {
                Debug.LogWarning($"[Trailer] Spada '{swordCardId}' non trovata nel CardDatabase: il boss resta disarmato.");
                yield break;
            }

            AudioClip evocation = Resources.Load<AudioClip>("SFX/jurinashor_weapon_evocation");
            // Con due evocazioni la partita lascia piu' aria attorno al boss,
            // con tre tiene una distribuzione compatta: stessi numeri.
            float spacing = count == 2 ? 0.30f : 0.20f;
            float startX = 0.5f - spacing * (count - 1) * 0.5f;
            for (int index = 0; index < count; index++)
            {
                PrototypeCardView sword = PrototypeCardView.CreateBattlefieldPreview(swordLayer, definition, configuration);
                sword.ConfigureJurinashorSwordPresentation();
                swordViews.Add(sword);

                RectTransform rect = sword.RectTransform;
                var anchor = new Vector2(startX + spacing * index, 0.47f);
                rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 37f);
                rect.sizeDelta = new Vector2(230f, 330f);
                rect.localScale = Vector3.one;

                JurinashorSwordFloatVfx floatVfx = sword.gameObject.AddComponent<JurinashorSwordFloatVfx>();
                floatVfx.SetLayoutAnchor(anchor);
                sfx.PlayClip(evocation);
                StartCoroutine(floatVfx.PlayNecromanticSummon());
                yield return Wait(0.35f);
            }
            yield return Wait(0.4f);
        }

        private static void ConfigureBossPresentation(PrototypeCardView view, string id)
        {
            switch (id.ToLowerInvariant())
            {
                case "trentor": view.ConfigureTrentorBackdropPresentation(); break;
                case "boss-bragus": view.ConfigureBragusBackdropPresentation(); break;
                case "boss-palatir": view.ConfigurePalatirBackdropPresentation(); break;
                case "boss-jurinashor": view.ConfigureJurinashorBackdropPresentation(); break;
                case "boss-seraphel": view.ConfigureSeraphelBackdropPresentation(); break;
            }
        }

        /// <summary>
        /// La carta piu' forte della classe: il trailer non deve mai mostrare
        /// materiale da tutorial, ed e' la ragione per cui qui non si prende
        /// "la prima che c'e'".
        /// </summary>
        private PrototypeCardView SpawnPawn(RectTransform row, HeroClass heroClass, List<PrototypeCardView> destination)
        {
            if (database == null)
                return null;

            CardDefinition definition = database.Cards
                .Where(card => card != null && card.CanEnterCombat && card.HasHeroClass && card.HeroClass == heroClass)
                .OrderByDescending(card => card.Strength)
                .FirstOrDefault();
            if (definition == null)
            {
                Debug.LogWarning($"[Trailer] Nessuna carta giocabile per {heroClass}.");
                return null;
            }

            PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview(row, definition, configuration);
            view.SetStrengthValue(definition.Strength);
            destination.Add(view);
            return view;
        }

        private void ClearRows()
        {
            playerViews.Clear();
            enemyViews.Clear();
            swordViews.Clear();
            Clear(playerRow);
            Clear(enemyRow);
            Clear(swordLayer);
        }

        // Si stacca dal genitore prima di distruggere: Destroy e' differito a
        // fine frame, e senza questo il layout conterebbe per un frame le
        // pedine vecchie insieme a quelle nuove.
        private static void Clear(RectTransform row)
        {
            for (int index = row.childCount - 1; index >= 0; index--)
            {
                GameObject child = row.GetChild(index).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }

        private IEnumerator FadeOut(PrototypeCardView view, float duration)
        {
            CanvasGroup group = view != null ? view.GetComponent<CanvasGroup>() : null;
            if (group == null)
                yield break;

            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Delta();
                group.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = 0f;
        }

        private static int SafeLength(HeroClass[] classes) => classes?.Length ?? 0;

        // ---------------------------------------------------------------- dadi

        private IEnumerator RollDie(HeroClass heroClass, int result, float duration)
        {
            dieSlot.gameObject.SetActive(true);
            dieSlot.anchoredPosition = Vector2.zero;
            SetDieAlpha(1f);
            dieView.SetBounceArea(dieSlot, null);
            sfx.PlayRollingDice();
            dieView.StartScriptedRoll(dieSides, heroClass, Mathf.Clamp(result, 1, dieSides), duration / SpeedFactor());
            yield return Wait(duration + 0.55f);
        }

        private IEnumerator FadeDie(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Delta();
                SetDieAlpha(1f - Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            dieView.Hide();
            dieSlot.gameObject.SetActive(false);
            SetDieAlpha(1f);
        }

        private void SetDieAlpha(float alpha)
        {
            if (dieGroup != null)
                dieGroup.alpha = alpha;
        }

        // ------------------------------------------------------------ costruz.

        private void SetBackground(string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Trailer] Fondale '{resourcePath}' non trovato: resta quello precedente.");
                return;
            }
            background.sprite = sprite;
        }

        private void BuildView()
        {
            var canvasObject = new GameObject("Promo Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            // Lo scaler e' quello della partita: se qui fosse diverso, le pedine
            // uscirebbero di una misura sbagliata anche con i numeri giusti.
            ResponsiveLayoutConfiguration responsive = configuration.ResponsiveLayout;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = responsive.LandscapeReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.25f;
            Canvas.ForceUpdateCanvases();
            ComputeBoardMetrics((RectTransform)canvas.transform, responsive);

            background = CreateImage("Background", canvas.transform, Color.white, Vector2.zero, Vector2.one);
            background.preserveAspect = false;
            CreateImage("Arena Shade", canvas.transform, new Color(0.006f, 0.012f, 0.028f, 0.5f), Vector2.zero, Vector2.one);

            // Le due file stanno dove stanno in partita: chi conosce il gioco
            // deve riconoscere il campo al primo fotogramma.
            enemyRow = CreateRow("Enemy Formation", canvas.transform, enemyAnchor);
            playerRow = CreateRow("Player Formation", canvas.transform, playerAnchor);

            // Le spade non stanno in fila con le pedine: fluttuano sul campo,
            // appese alla radice del canvas come in partita.
            swordLayer = new GameObject("Jurinashor Swords", typeof(RectTransform)).GetComponent<RectTransform>();
            swordLayer.SetParent(canvas.transform, false);
            SetRect(swordLayer, Vector2.zero, Vector2.one);

            BuildDieSlot(canvas.transform);
            flash = CreateImage("Impact Flash", canvas.transform, new Color(1f, 0.97f, 0.88f, 0f), Vector2.zero, Vector2.one);
            BuildCardLayer(canvas.transform);
            guides = BuildGuides(canvas.transform);
            guides.gameObject.SetActive(showFramingGuides);

            Image fadeImage = CreateImage("Fade", canvas.transform, Color.black, Vector2.zero, Vector2.one);
            fade = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
        }

        /// <summary>
        /// La misura della pedina e la posa delle due file, calcolate con la
        /// stessa formula di ApplyResponsiveLayout. Ricopiare i numeri a occhio
        /// dava pedine piu' piccole e schiacciate, e il numero di Potenza
        /// finiva a meta' altezza invece che sulla carta.
        /// </summary>
        private void ComputeBoardMetrics(RectTransform canvasRect, ResponsiveLayoutConfiguration responsive)
        {
            float width = canvasRect.rect.width;
            float height = canvasRect.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = responsive.LandscapeReferenceResolution.x;
                height = responsive.LandscapeReferenceResolution.y;
            }

            bool wide = width / Mathf.Max(1f, height) >= 1.65f;
            float usableWidth = Mathf.Min(
                width * (wide ? 0.82f : responsive.LandscapeRowWidth),
                responsive.LandscapeMaximumRowWidth);
            cardGap = Mathf.Clamp(usableWidth * responsive.GapFraction, responsive.MinimumGap, responsive.MaximumGap);

            int formation = Mathf.Max(1, configuration.Gameplay.FormationSize);
            float byWidth = (usableWidth - cardGap * (formation - 1)) / formation;
            float byHeight = height * (wide ? 0.305f : responsive.LandscapeCardHeight * 0.92f);
            cardSize = Mathf.Min(byWidth, byHeight);
            rowWidth = cardSize * formation + cardGap * (formation - 1);

            // Le due quote comprendono il sollevamento che la partita applica
            // in orizzontale a entrambe le formazioni.
            enemyAnchor = (wide ? 0.845f : 0.79f) + 0.05f;
            playerAnchor = (wide ? 0.145f : 0.15f) + 0.045f;
        }

        private RectTransform CreateRow(string name, Transform parent, float verticalAnchor)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, verticalAnchor);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(rowWidth, cardSize);
            return row;
        }

        /// <summary>
        /// Dispone le pedine sulla fila a mano, come ConfigureBattlefieldRow:
        /// niente HorizontalLayoutGroup, perche' quello le stira per riempire
        /// e una pedina stirata non e' piu' quella del gioco.
        /// </summary>
        private void LayoutRow(RectTransform row, List<PrototypeCardView> views)
        {
            int count = Mathf.Max(1, views.Count);
            float step = count == 2 ? (cardSize + cardGap) * 0.82f : cardSize + cardGap;
            float start = -step * (count - 1) * 0.5f;
            row.sizeDelta = new Vector2(cardSize * count + cardGap * (count - 1), cardSize);
            for (int index = 0; index < views.Count; index++)
            {
                RectTransform rect = views[index].RectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cardSize, cardSize);
                rect.anchoredPosition = new Vector2(start + step * index, 0f);
                rect.localScale = Vector3.one;
            }
        }

        private void BuildDieSlot(Transform parent)
        {
            // Slot a misura fissa e ancore al centro, come nella scena di prova
            // dei dadi: il render del dado e' 1:1 e lo slot ne decide la scala.
            dieSlot = new GameObject("Die Slot", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            dieSlot.SetParent(parent, false);
            dieGroup = dieSlot.GetComponent<CanvasGroup>();
            dieSlot.anchorMin = dieSlot.anchorMax = new Vector2(0.5f, 0.5f);
            dieSlot.pivot = new Vector2(0.5f, 0.5f);
            dieSlot.anchoredPosition = Vector2.zero;
            dieSlot.sizeDelta = new Vector2(420f, 420f);
            dieView = Dice3DRollView.Create(dieSlot);
            dieView.SetBounceArea(dieSlot, null);
            dieSlot.gameObject.SetActive(false);
        }

        private void BuildCardLayer(Transform parent)
        {
            var layer = new GameObject("Card Layer", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            SetRect(layer, Vector2.zero, Vector2.one);
            cardGroup = layer.GetComponent<CanvasGroup>();
            cardGroup.alpha = 0f;

            CreateImage("Card Backdrop", layer, new Color(0.005f, 0.008f, 0.018f, 0.92f), Vector2.zero, Vector2.one);
            cardText = CreateLabel("Card Line", layer, 148, new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.66f));
            cardCaption = CreateLabel("Card Caption", layer, 52, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.42f));
            cardCaption.color = new Color(0.82f, 0.85f, 0.9f);
        }

        /// <summary>
        /// Guide di inquadratura: il ritaglio 9:16 al centro. Serve a girare una
        /// volta sola per due formati — quello che deve restare leggibile su
        /// TikTok va tenuto dentro il rettangolo interno.
        /// </summary>
        private static RectTransform BuildGuides(Transform parent)
        {
            var layer = new GameObject("Framing Guides", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            SetRect(layer, Vector2.zero, Vector2.one);

            var crop = new GameObject("Vertical Crop", typeof(RectTransform)).GetComponent<RectTransform>();
            crop.SetParent(layer, false);
            crop.anchorMin = crop.anchorMax = new Vector2(0.5f, 0.5f);
            crop.pivot = new Vector2(0.5f, 0.5f);
            crop.sizeDelta = new Vector2(1080f * 9f / 16f, 1080f);
            Color line = new Color(1f, 0.85f, 0.2f, 0.55f);
            CreateImage("Left", crop, line, Vector2.zero, new Vector2(0f, 1f)).rectTransform.sizeDelta = new Vector2(3f, 0f);
            CreateImage("Right", crop, line, new Vector2(1f, 0f), Vector2.one).rectTransform.sizeDelta = new Vector2(3f, 0f);
            return layer;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ------------------------------------------------------------ utilita'

        private float SpeedFactor() => Mathf.Max(0.1f, playbackSpeed);

        private float Delta() => Time.unscaledDeltaTime * SpeedFactor();

        private IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds / SpeedFactor());
        }

        private IEnumerator Flash(float duration, float peak)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Delta();
                float t = Mathf.Clamp01(elapsed / duration);
                flash.color = new Color(1f, 0.97f, 0.88f, Mathf.Sin(t * Mathf.PI) * peak);
                yield return null;
            }
            flash.color = new Color(1f, 0.97f, 0.88f, 0f);
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            return FadeGroup(fade, target, duration);
        }

        private IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
        {
            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Delta();
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            group.alpha = target;
        }

        private static Image CreateImage(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRect(image.rectTransform, min, max);
            return image;
        }

        private static Text CreateLabel(string name, Transform parent, int size, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline), typeof(Shadow));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            // IM Fell English SC: e' un maiuscoletto, quindi le righe delle card
            // vanno scritte in maiuscolo/minuscolo. Su una stringa gia' tutta
            // in caps il font non aggiunge niente e si perde il motivo di usarlo.
            text.font = MmoUiTheme.LoreFont;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.Lerp(new Color(0.94f, 0.85f, 0.5f), Color.white, 0.25f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = gameObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.01f, 0f, 0.96f);
            outline.effectDistance = new Vector2(2.8f, -2.8f);
            outline.useGraphicAlpha = true;
            Shadow shadow = gameObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
