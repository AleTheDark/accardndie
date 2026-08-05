using System;
using AccardND.Battlefield;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Classifica competitiva aperta direttamente dall'Hub. Mostra soltanto dati
    /// autoritativi ricevuti dal server: profilo, progresso account, ladder attiva
    /// e stagioni archiviate nella Hall of Fame.
    /// </summary>
    internal sealed class PvpLeaderboardScreen
    {
        public sealed class Callbacks
        {
            public Action OnClose;
            public Action OnRequestProfile;
            public Action OnRequestAccountProgress;
            public Action OnRequestLeaderboard;
            public Action OnRequestHallOfFameSeasons;
            public Action<int> OnRequestHallOfFame;
        }

        private const string TabRanked = "ranked";
        private const string TabHallOfFame = "halloffame";
        private const string BackgroundResource = "UI/HallOfFame/hall_of_fame_background";
        private const string PortraitBackgroundResource = "UI/HallOfFame/hall_of_fame_background_portrait";
        private const string OrnateFrameResource = "UI/MultiplayerRestyle/ornate_panel_frame";
        private const string FallbackAvatarResource = "UI/MultiplayerRestyle/multiplayer_hooded_avatar";
        private const string GoldPodiumFrameResource =
            "UI/HallOfFame/Frames/leaderboard_podium_frame_gold";
        private const string SilverPodiumFrameResource =
            "UI/HallOfFame/Frames/leaderboard_podium_frame_silver";
        private const string BronzePodiumFrameResource =
            "UI/HallOfFame/Frames/leaderboard_podium_frame_bronze";

        private static readonly Color Gold = new Color32(0xF2, 0xC9, 0x57, 0xFF);
        private static readonly Color PaleGold = new(0.94f, 0.86f, 0.68f, 1f);
        private static readonly Color Silver = new(0.68f, 0.76f, 0.84f, 1f);
        private static readonly Color Bronze = new(0.72f, 0.38f, 0.19f, 1f);
        private static readonly Color Violet = new(0.58f, 0.25f, 0.84f, 1f);
        private static readonly Color Ink = new(0.006f, 0.007f, 0.011f, 0.96f);
        private static readonly Color Row = new(0.025f, 0.028f, 0.035f, 0.95f);
        private static readonly Color Muted = new(0.72f, 0.7f, 0.66f, 1f);

        private readonly Callbacks callbacks;
        private readonly Func<string, Sprite> iconArtwork;
        private readonly RectTransform root;
        private readonly Image backgroundImage;
        private readonly RectTransform viewRoot;
        private readonly Button rankedTab;
        private readonly Button hallOfFameTab;
        private readonly Text statusText;

        private string myPlayerId;
        private string currentTab = TabRanked;
        private ProfileData profile;
        private LeaderboardData leaderboard;
        private HallOfFameSeasonsData hallOfFameSeasons;
        private HallOfFameData hallOfFame;
        private Text countdownText;
        private int countdownInitialSeconds;
        private float countdownStartedAt;
        private int countdownLastRendered = int.MinValue;
        private bool backgroundIsPortrait;

        public PvpLeaderboardScreen(
            Transform parent,
            string myPlayerId,
            Callbacks callbacks,
            Func<string, Sprite> iconArtwork = null)
        {
            this.callbacks = callbacks;
            this.iconArtwork = iconArtwork;
            this.myPlayerId = myPlayerId ?? string.Empty;

            // Come la Lobby, il fondale della Classifica deve essere full-bleed:
            // il layout mantiene margini interni propri, senza ritagliare lo sfondo.
            Transform screenParent = parent;
            if (parent != null && parent.GetComponent<SafeAreaRect>() != null && parent.parent != null)
                screenParent = parent.parent;

            var rootObject = new GameObject("Classifica", typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(screenParent, false);
            root = (RectTransform)rootObject.transform;
            PvpUiFactory.Stretch(root);

            backgroundImage = rootObject.GetComponent<Image>();
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = true;
            ApplyBackgroundForOrientation(force: true);

            // La cornice termina sotto la targa, come nelle altre schermate principali.
            PvpUiFactory.CreateScreenOuterFrame(root, 0.795f);

            // Il banner account resta quello unico dell'Hub, sopra questo canvas.
            // Qui c'è solo la targa della schermata.
            RectTransform titlePanel = PvpUiFactory.CreateScreenTitlePanel(
                root,
                "Classifica Title",
                "CLASSIFICA",
                null,
                42);
            Text screenTitle = titlePanel.Find("Title")?.GetComponent<Text>();
            if (screenTitle != null)
                screenTitle.color = Gold;
            PvpUiFactory.SetAnchors(
                titlePanel, new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.9f));

            rankedTab = PvpUiFactory.CreateTabButton(
                root, "Tab Ranked", "STAGIONE IN CORSO", () => SwitchTab(TabRanked), 23);
            PvpUiFactory.SetAnchors(
                (RectTransform)rankedTab.transform,
                new Vector2(0.18f, 0.715f), new Vector2(0.5f, 0.775f));

            hallOfFameTab = PvpUiFactory.CreateTabButton(
                root, "Tab Hall Of Fame", "HALL OF FAME", () => SwitchTab(TabHallOfFame), 23);
            PvpUiFactory.SetAnchors(
                (RectTransform)hallOfFameTab.transform,
                new Vector2(0.5f, 0.715f), new Vector2(0.82f, 0.775f));

            var viewObject = new GameObject("Classifica Content", typeof(RectTransform));
            viewObject.transform.SetParent(root, false);
            viewRoot = (RectTransform)viewObject.transform;
            PvpUiFactory.SetAnchors(viewRoot, new Vector2(0.055f, 0.065f), new Vector2(0.945f, 0.695f));

            statusText = PvpUiFactory.CreateLabel(
                root, "Connection Status", "Connessione al server...", 16, TextAnchor.MiddleCenter);
            statusText.color = new Color(0.83f, 0.78f, 0.68f, 1f);
            PvpUiFactory.SetAnchors(
                (RectTransform)statusText.transform,
                new Vector2(0.055f, 0.018f), new Vector2(0.945f, 0.052f));

            PvpUiFactory.SetTabActive(rankedTab, true);
            PvpUiFactory.SetTabActive(hallOfFameTab, false);
            Render();
        }

        public void SetIdentity(string playerId)
        {
            myPlayerId = playerId ?? string.Empty;
        }

        public void RequestInitialData()
        {
            SetStatus("Sincronizzazione della classifica...");
            callbacks.OnRequestProfile?.Invoke();
            callbacks.OnRequestAccountProgress?.Invoke();
            callbacks.OnRequestLeaderboard?.Invoke();
        }

        public void SetStatus(string message)
        {
            statusText.text = message ?? string.Empty;
        }

        public void SetProfile(ProfileData data)
        {
            profile = data;
            if (data != null)
            {
                SetIdentity(data.playerId);
                countdownInitialSeconds = Mathf.Max(0, data.seasonSecondsRemaining);
                countdownStartedAt = Time.unscaledTime;
                countdownLastRendered = int.MinValue;
            }
            RenderIf(TabRanked);
        }

        public void SetAccountProgress(SinglePlayerProgressData data)
        {
            // Visualizzato dal banner account standard dell'Hub.
        }

        public void SetLeaderboard(LeaderboardData data)
        {
            leaderboard = data;
            SetStatus(string.Empty);
            RenderIf(TabRanked);
        }

        public void SetHallOfFameSeasons(HallOfFameSeasonsData data)
        {
            hallOfFameSeasons = data;
            if (data?.seasons != null && data.seasons.Length > 0)
            {
                int latestSeason = data.seasons[0].seasonId;
                if (hallOfFame == null || hallOfFame.seasonId != latestSeason)
                    callbacks.OnRequestHallOfFame?.Invoke(latestSeason);
                else
                    RenderIf(TabHallOfFame);
            }
            else
            {
                SetStatus(string.Empty);
                RenderIf(TabHallOfFame);
            }
        }

        public void SetHallOfFame(HallOfFameData data)
        {
            hallOfFame = data;
            SetStatus(string.Empty);
            RenderIf(TabHallOfFame);
        }

        public void Tick()
        {
            ApplyBackgroundForOrientation(force: false);
            if (countdownText == null || currentTab != TabRanked)
                return;

            int elapsed = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - countdownStartedAt));
            int remaining = Mathf.Max(0, countdownInitialSeconds - elapsed);
            if (remaining == countdownLastRendered)
                return;

            countdownLastRendered = remaining;
            countdownText.text = remaining > 0
                ? $"La stagione termina tra  {FormatDuration(remaining)}"
                : "Fine stagione in aggiornamento";
        }

        public void Destroy()
        {
            if (root != null)
                UnityEngine.Object.Destroy(root.gameObject);
        }

        private void SwitchTab(string tab)
        {
            if (currentTab == tab)
                return;

            currentTab = tab;
            PvpUiFactory.SetTabActive(rankedTab, tab == TabRanked);
            PvpUiFactory.SetTabActive(hallOfFameTab, tab == TabHallOfFame);
            SetStatus("Caricamento dati dal server...");
            Render();

            if (tab == TabRanked)
            {
                callbacks.OnRequestProfile?.Invoke();
                callbacks.OnRequestAccountProgress?.Invoke();
                callbacks.OnRequestLeaderboard?.Invoke();
            }
            else
            {
                callbacks.OnRequestHallOfFameSeasons?.Invoke();
            }
        }

        private void RenderIf(string tab)
        {
            if (currentTab == tab)
                Render();
        }

        private void Render()
        {
            countdownText = null;
            PvpUiFactory.Clear(viewRoot);
            if (currentTab == TabHallOfFame)
                RenderHallOfFame();
            else
                RenderRanked();
        }

        private void RenderRanked()
        {
            RectTransform ladderPanel = CreateFramedPanel(
                viewRoot, "Ladder", new Vector2(0f, 0f), new Vector2(0.72f, 1f));
            RectTransform personalPanel = CreateFramedPanel(
                viewRoot, "Your Season", new Vector2(0.735f, 0f), new Vector2(1f, 1f));

            string seasonName = leaderboard?.seasonName ?? profile?.seasonName ?? "Stagione in corso";
            Text title = PvpUiFactory.CreateTitleText(
                ladderPanel, "Season Title", seasonName.ToUpperInvariant(), 25, TextAnchor.MiddleLeft);
            title.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)title.transform,
                new Vector2(0.025f, 0.925f), new Vector2(0.56f, 0.99f));

            countdownText = PvpUiFactory.CreateLabel(
                ladderPanel, "Season Countdown", "Fine stagione in aggiornamento", 16, TextAnchor.MiddleRight);
            countdownText.color = PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)countdownText.transform,
                new Vector2(0.56f, 0.925f), new Vector2(0.975f, 0.99f));
            Tick();

            RectTransform podium = PvpUiFactory.CreateSoftPanel(
                ladderPanel, "Podium", new Color(0.012f, 0.012f, 0.018f, 0.76f));
            PvpUiFactory.SetAnchors(podium, new Vector2(0.025f, 0.515f), new Vector2(0.975f, 0.915f));

            LeaderboardEntry first = EntryAt(leaderboard?.entries, 0);
            LeaderboardEntry second = EntryAt(leaderboard?.entries, 1);
            LeaderboardEntry third = EntryAt(leaderboard?.entries, 2);
            CreateRankedPodiumCard(podium, second, 2, new Vector2(0.02f, 0.05f), new Vector2(0.323f, 0.84f), Silver);
            CreateRankedPodiumCard(podium, first, 1, new Vector2(0.343f, 0.05f), new Vector2(0.657f, 0.98f), Gold);
            CreateRankedPodiumCard(podium, third, 3, new Vector2(0.677f, 0.05f), new Vector2(0.98f, 0.84f), Bronze);

            RectTransform listContent = CreateScrollContent(
                ladderPanel, "Ladder List",
                new Vector2(0.025f, 0.105f), new Vector2(0.975f, 0.5f));

            if (leaderboard?.entries == null)
            {
                AddMessageRow(listContent, "Classifica in caricamento...");
            }
            else if (leaderboard.entries.Length <= 3)
            {
                AddMessageRow(listContent, "Nessun altro giocatore classificato.");
            }
            else
            {
                for (int index = 3; index < leaderboard.entries.Length; index++)
                    AddRankedRow(listContent, leaderboard.entries[index]);
            }

            CreatePersonalRankedRow(
                ladderPanel, new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.092f));
            RenderRankedPersonalPanel(personalPanel);
        }

        private void RenderHallOfFame()
        {
            RectTransform hallPanel = CreateFramedPanel(
                viewRoot, "Hall Of Fame Ladder", new Vector2(0f, 0f), new Vector2(0.72f, 1f));
            RectTransform archivePanel = CreateFramedPanel(
                viewRoot, "Season Archive", new Vector2(0.735f, 0f), new Vector2(1f, 1f));

            string seasonName = hallOfFame?.seasonName ?? "Hall of Fame";
            Text title = PvpUiFactory.CreateTitleText(
                hallPanel, "Hall Title", seasonName.ToUpperInvariant(), 25, TextAnchor.MiddleLeft);
            title.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)title.transform,
                new Vector2(0.025f, 0.925f), new Vector2(0.62f, 0.99f));

            HallOfFameSeasonDto selectedSeason = FindSeason(hallOfFame?.seasonId ?? 0);
            Text summary = PvpUiFactory.CreateLabel(
                hallPanel, "Hall Summary", SeasonSummary(selectedSeason), 15, TextAnchor.MiddleRight);
            summary.color = PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)summary.transform,
                new Vector2(0.54f, 0.925f), new Vector2(0.975f, 0.99f));

            RectTransform podium = PvpUiFactory.CreateSoftPanel(
                hallPanel, "Historic Podium", new Color(0.012f, 0.012f, 0.018f, 0.76f));
            PvpUiFactory.SetAnchors(podium, new Vector2(0.025f, 0.515f), new Vector2(0.975f, 0.915f));

            HallOfFameEntry first = EntryAt(hallOfFame?.entries, 0);
            HallOfFameEntry second = EntryAt(hallOfFame?.entries, 1);
            HallOfFameEntry third = EntryAt(hallOfFame?.entries, 2);
            CreateHallPodiumCard(podium, second, 2, new Vector2(0.02f, 0.05f), new Vector2(0.323f, 0.84f), Silver);
            CreateHallPodiumCard(podium, first, 1, new Vector2(0.343f, 0.05f), new Vector2(0.657f, 0.98f), Gold);
            CreateHallPodiumCard(podium, third, 3, new Vector2(0.677f, 0.05f), new Vector2(0.98f, 0.84f), Bronze);

            RectTransform listContent = CreateScrollContent(
                hallPanel, "Historic List",
                new Vector2(0.025f, 0.105f), new Vector2(0.975f, 0.5f));

            if (hallOfFameSeasons?.seasons == null || hallOfFameSeasons.seasons.Length == 0)
            {
                AddMessageRow(listContent, "Nessuna stagione conclusa: la Hall of Fame è ancora vuota.");
            }
            else if (hallOfFame?.entries == null)
            {
                AddMessageRow(listContent, "Classifica storica in caricamento...");
            }
            else if (hallOfFame.entries.Length <= 3)
            {
                AddMessageRow(listContent, "Nessun altro piazzamento archiviato.");
            }
            else
            {
                for (int index = 3; index < hallOfFame.entries.Length; index++)
                    AddHallRow(listContent, hallOfFame.entries[index]);
            }

            CreatePersonalHallRow(
                hallPanel, new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.092f));
            RenderArchivePanel(archivePanel, selectedSeason);
        }

        private void RenderRankedPersonalPanel(RectTransform panel)
        {
            Text heading = PvpUiFactory.CreateTitleText(
                panel, "Personal Heading", "LA TUA STAGIONE", 21, TextAnchor.MiddleCenter);
            heading.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)heading.transform,
                new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.985f));

            Image emblem = CreateImage(panel, "Rank Emblem");
            emblem.sprite = profile != null && profile.ranked && !profile.placement
                ? PvpUiFactory.RankEmblem(profile.tier)
                : null;
            emblem.enabled = emblem.sprite != null;
            emblem.preserveAspect = true;
            PvpUiFactory.SetAnchors(
                (RectTransform)emblem.transform,
                new Vector2(0.26f, 0.65f), new Vector2(0.74f, 0.9f));

            string rankText = profile == null || profile.globalRank <= 0
                ? "NON CLASSIFICATO"
                : $"#{profile.globalRank:N0}";
            Text rank = PvpUiFactory.CreateValueText(
                panel, "Global Rank", rankText, 37, TextAnchor.MiddleCenter);
            rank.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)rank.transform,
                new Vector2(0.07f, 0.565f), new Vector2(0.93f, 0.67f));

            string tier = profile == null
                ? "Dati in caricamento"
                : profile.placement
                    ? $"Piazzamento · {profile.placementRemaining} partite"
                    : profile.ranked
                        ? $"{profile.tier} {profile.division} · {profile.leaguePoints} LP"
                        : "Gioca una ranked per entrare";
            Text tierText = PvpUiFactory.CreateTitleText(
                panel, "Tier", tier, 19, TextAnchor.MiddleCenter);
            tierText.color = PvpUiFactory.TierAccent(profile?.tier);
            PvpUiFactory.SetAnchors(
                (RectTransform)tierText.transform,
                new Vector2(0.05f, 0.505f), new Vector2(0.95f, 0.57f));

            int games = profile == null ? 0 : profile.wins + profile.losses;
            AddPersonalStat(panel, "VITTORIE", profile == null ? "—" : profile.wins.ToString("N0"), 0.43f);
            AddPersonalStat(panel, "SCONFITTE", profile == null ? "—" : profile.losses.ToString("N0"), 0.36f);
            AddPersonalStat(panel, "WIN RATE", games <= 0 ? "—" : $"{profile.winRatePercent}%", 0.29f);
            AddPersonalStat(panel, "SERIE ATTUALE", profile == null ? "—" : profile.currentStreak.ToString("N0"), 0.22f);
            AddPersonalStat(panel, "MIGLIOR SERIE", profile == null ? "—" : profile.bestStreak.ToString("N0"), 0.15f);

            string population = profile == null || profile.globalPlayers <= 0
                ? "Partecipanti in aggiornamento"
                : $"{profile.globalPlayers:N0} giocatori in classifica";
            Text participants = PvpUiFactory.CreateLabel(
                panel, "Participants", population, 16, TextAnchor.MiddleCenter);
            participants.color = Muted;
            PvpUiFactory.SetAnchors(
                (RectTransform)participants.transform,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.11f));
        }

        private void RenderArchivePanel(RectTransform panel, HallOfFameSeasonDto selectedSeason)
        {
            Text heading = PvpUiFactory.CreateTitleText(
                panel, "Archive Heading", "STAGIONI CONCLUSE", 20, TextAnchor.MiddleCenter);
            heading.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)heading.transform,
                new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.985f));

            RectTransform seasonsContent = CreateScrollContent(
                panel, "Seasons",
                new Vector2(0.055f, 0.43f), new Vector2(0.945f, 0.9f));

            if (hallOfFameSeasons?.seasons == null || hallOfFameSeasons.seasons.Length == 0)
            {
                AddMessageRow(seasonsContent, "Nessuna stagione archiviata.");
            }
            else
            {
                foreach (HallOfFameSeasonDto season in hallOfFameSeasons.seasons)
                {
                    int seasonId = season.seasonId;
                    bool selected = hallOfFame != null && hallOfFame.seasonId == seasonId;
                    Button button = PvpUiFactory.CreateButton(
                        seasonsContent,
                        $"Season {seasonId}",
                        season.name,
                        selected ? Violet : new Color(0.2f, 0.16f, 0.12f, 0.96f),
                        () =>
                        {
                            SetStatus("Caricamento della stagione selezionata...");
                            callbacks.OnRequestHallOfFame?.Invoke(seasonId);
                        },
                        17);
                    var element = button.gameObject.AddComponent<LayoutElement>();
                    element.preferredHeight = 52f;
                    element.flexibleWidth = 1f;
                }
            }

            Text detailsTitle = PvpUiFactory.CreateTitleText(
                panel, "Season Details Title", "DATI DELLA STAGIONE", 19, TextAnchor.MiddleCenter);
            detailsTitle.color = PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)detailsTitle.transform,
                new Vector2(0.06f, 0.35f), new Vector2(0.94f, 0.41f));

            AddPersonalStat(
                panel, "PARTECIPANTI",
                selectedSeason == null ? "—" : selectedSeason.participants.ToString("N0"),
                0.27f);
            AddPersonalStat(
                panel, "INIZIO",
                selectedSeason == null ? "—" : FormatServerDate(selectedSeason.startedAt),
                0.20f);
            AddPersonalStat(
                panel, "FINE",
                selectedSeason == null ? "—" : FormatServerDate(selectedSeason.endedAt),
                0.13f);

            Text note = PvpUiFactory.CreateLabel(
                panel, "Archive Note",
                "I risultati sono snapshot definitivi salvati dal server al termine della stagione.",
                14, TextAnchor.MiddleCenter);
            note.color = Muted;
            PvpUiFactory.SetAnchors(
                (RectTransform)note.transform,
                new Vector2(0.07f, 0.025f), new Vector2(0.93f, 0.105f));
        }

        private void CreateRankedPodiumCard(
            RectTransform parent,
            LeaderboardEntry entry,
            int rank,
            Vector2 minimum,
            Vector2 maximum,
            Color accent)
        {
            RectTransform card = CreatePodiumCard(parent, $"Rank {rank}", minimum, maximum);
            AddPodiumPortrait(card, entry?.selectedIconId, entry?.tier);
            AddPodiumFrame(card, rank);
            AddPodiumRank(card, rank, accent);

            Text username = PvpUiFactory.CreateTitleText(
                card, "Username", entry?.username ?? "POSTO LIBERO", 21, TextAnchor.MiddleCenter);
            username.color = entry != null && IsMe(entry.playerId) ? Gold : PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)username.transform,
                new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.3f));

            string tier = entry == null
                ? "Nessun piazzamento"
                : entry.placement
                    ? "In piazzamento"
                    : $"{entry.tier} {entry.division}";
            Text tierText = PvpUiFactory.CreateLabel(
                card, "Tier", tier, 16, TextAnchor.MiddleCenter);
            tierText.color = entry == null ? Muted : PvpUiFactory.TierAccent(entry.tier);
            PvpUiFactory.SetAnchors(
                (RectTransform)tierText.transform,
                new Vector2(0.08f, 0.095f), new Vector2(0.92f, 0.19f));

            Text points = PvpUiFactory.CreateValueText(
                card, "League Points", entry == null ? "—" : $"{entry.leaguePoints:N0} LP",
                20, TextAnchor.MiddleCenter);
            points.color = Violet;
            PvpUiFactory.SetAnchors(
                (RectTransform)points.transform,
                new Vector2(0.08f, 0.025f), new Vector2(0.92f, 0.105f));
        }

        private void CreateHallPodiumCard(
            RectTransform parent,
            HallOfFameEntry entry,
            int rank,
            Vector2 minimum,
            Vector2 maximum,
            Color accent)
        {
            RectTransform card = CreatePodiumCard(parent, $"Historic Rank {rank}", minimum, maximum);
            AddPodiumPortrait(card, entry?.selectedIconId, entry?.tier);
            AddPodiumFrame(card, rank);
            AddPodiumRank(card, rank, accent);

            Text username = PvpUiFactory.CreateTitleText(
                card, "Username", entry?.username ?? "POSTO LIBERO", 21, TextAnchor.MiddleCenter);
            username.color = entry != null && IsMe(entry.playerId) ? Gold : PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)username.transform,
                new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.3f));

            Text tierText = PvpUiFactory.CreateLabel(
                card, "Tier", entry == null ? "Nessun piazzamento" : $"{entry.tier} {entry.division}",
                16, TextAnchor.MiddleCenter);
            tierText.color = entry == null ? Muted : PvpUiFactory.TierAccent(entry.tier);
            PvpUiFactory.SetAnchors(
                (RectTransform)tierText.transform,
                new Vector2(0.08f, 0.095f), new Vector2(0.92f, 0.19f));

            Text points = PvpUiFactory.CreateValueText(
                card, "Final MMR", entry == null ? "—" : $"{entry.finalMmr:N0} MMR",
                20, TextAnchor.MiddleCenter);
            points.color = Violet;
            PvpUiFactory.SetAnchors(
                (RectTransform)points.transform,
                new Vector2(0.08f, 0.025f), new Vector2(0.92f, 0.105f));
        }

        private static RectTransform CreatePodiumCard(
            Transform parent, string name, Vector2 minimum, Vector2 maximum)
        {
            RectTransform card = PvpUiFactory.CreateSoftPanel(
                parent, name, new Color(0.004f, 0.005f, 0.009f, 0.9f));
            PvpUiFactory.SetAnchors(card, minimum, maximum);
            return card;
        }

        private void AddPodiumPortrait(RectTransform card, string selectedIconId, string tier)
        {
            Image portrait = CreateImage(card, "Player Portrait");
            Sprite artwork = iconArtwork?.Invoke(selectedIconId);
            if (artwork == null && !string.IsNullOrWhiteSpace(tier))
                artwork = PvpUiFactory.RankEmblem(tier);
            if (artwork == null)
                artwork = Resources.Load<Sprite>(FallbackAvatarResource);

            portrait.sprite = artwork;
            portrait.enabled = artwork != null;
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            PvpUiFactory.SetAnchors(
                (RectTransform)portrait.transform,
                new Vector2(0.14f, 0.29f), new Vector2(0.86f, 0.79f));
        }

        private static void AddPodiumFrame(RectTransform card, int rank)
        {
            string resource = rank switch
            {
                1 => GoldPodiumFrameResource,
                2 => SilverPodiumFrameResource,
                _ => BronzePodiumFrameResource
            };
            Sprite sprite = Resources.Load<Sprite>(resource);
            if (sprite == null)
                return;

            Image frame = CreateImage(card, "Podium Frame Artwork");
            frame.sprite = sprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            frame.color = Color.white;
            PvpUiFactory.Stretch((RectTransform)frame.transform);
        }

        private static void AddPodiumRank(RectTransform card, int rank, Color accent)
        {
            Text rankText = PvpUiFactory.CreateTitleText(
                card, "Placement", rank.ToString(), rank == 1 ? 38 : 32, TextAnchor.MiddleCenter);
            rankText.color = accent;
            PvpUiFactory.SetAnchors(
                (RectTransform)rankText.transform,
                new Vector2(0.36f, 0.805f), new Vector2(0.64f, 0.975f));
        }

        private void AddRankedRow(RectTransform content, LeaderboardEntry entry)
        {
            bool isMe = entry != null && IsMe(entry.playerId);
            RectTransform row = CreateListRow(content, isMe, $"Rank {entry?.rank ?? 0}");

            Text rank = PvpUiFactory.CreateValueText(
                row, "Placement", entry == null ? "—" : entry.rank.ToString(), 24, TextAnchor.MiddleCenter);
            rank.color = isMe ? Gold : PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)rank.transform,
                new Vector2(0.01f, 0.05f), new Vector2(0.075f, 0.95f));

            Image emblem = CreateImage(row, "Tier Emblem");
            emblem.sprite = entry == null ? null : iconArtwork?.Invoke(entry.selectedIconId);
            if (emblem.sprite == null && entry != null && !entry.placement)
                emblem.sprite = PvpUiFactory.RankEmblem(entry.tier);
            emblem.enabled = emblem.sprite != null;
            emblem.preserveAspect = true;
            PvpUiFactory.SetAnchors(
                (RectTransform)emblem.transform,
                new Vector2(0.082f, 0.1f), new Vector2(0.13f, 0.9f));

            Text username = PvpUiFactory.CreateTitleText(
                row, "Username", entry?.username ?? "—", 19, TextAnchor.MiddleLeft);
            username.color = isMe ? Gold : Color.white;
            PvpUiFactory.SetAnchors(
                (RectTransform)username.transform,
                new Vector2(0.145f, 0.05f), new Vector2(0.52f, 0.95f));

            string tier = entry == null
                ? "—"
                : entry.placement ? "In piazzamento" : $"{entry.tier} {entry.division}";
            Text league = PvpUiFactory.CreateLabel(
                row, "League", tier, 17, TextAnchor.MiddleLeft);
            league.color = PvpUiFactory.TierAccent(entry?.tier);
            PvpUiFactory.SetAnchors(
                (RectTransform)league.transform,
                new Vector2(0.52f, 0.05f), new Vector2(0.78f, 0.95f));

            Text points = PvpUiFactory.CreateValueText(
                row, "Points", entry == null ? "—" : $"{entry.leaguePoints:N0} LP", 20);
            points.color = Violet;
            PvpUiFactory.SetAnchors(
                (RectTransform)points.transform,
                new Vector2(0.78f, 0.05f), new Vector2(0.97f, 0.95f));
        }

        private void AddHallRow(RectTransform content, HallOfFameEntry entry)
        {
            bool isMe = entry != null && IsMe(entry.playerId);
            RectTransform row = CreateListRow(content, isMe, $"Historic Rank {entry?.rank ?? 0}");

            Text rank = PvpUiFactory.CreateValueText(
                row, "Placement", entry == null ? "—" : entry.rank.ToString(), 24, TextAnchor.MiddleCenter);
            rank.color = isMe ? Gold : PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)rank.transform,
                new Vector2(0.01f, 0.05f), new Vector2(0.075f, 0.95f));

            Image emblem = CreateImage(row, "Tier Emblem");
            emblem.sprite = entry == null ? null : iconArtwork?.Invoke(entry.selectedIconId);
            if (emblem.sprite == null && entry != null)
                emblem.sprite = PvpUiFactory.RankEmblem(entry.tier);
            emblem.enabled = emblem.sprite != null;
            emblem.preserveAspect = true;
            PvpUiFactory.SetAnchors(
                (RectTransform)emblem.transform,
                new Vector2(0.082f, 0.1f), new Vector2(0.13f, 0.9f));

            Text username = PvpUiFactory.CreateTitleText(
                row, "Username", entry?.username ?? "—", 19, TextAnchor.MiddleLeft);
            username.color = isMe ? Gold : Color.white;
            PvpUiFactory.SetAnchors(
                (RectTransform)username.transform,
                new Vector2(0.145f, 0.05f), new Vector2(0.47f, 0.95f));

            Text league = PvpUiFactory.CreateLabel(
                row, "League", entry == null ? "—" : $"{entry.tier} {entry.division}",
                17, TextAnchor.MiddleLeft);
            league.color = PvpUiFactory.TierAccent(entry?.tier);
            PvpUiFactory.SetAnchors(
                (RectTransform)league.transform,
                new Vector2(0.47f, 0.05f), new Vector2(0.67f, 0.95f));

            Text record = PvpUiFactory.CreateLabel(
                row, "Record", entry == null ? "—" : $"{entry.wins}V / {entry.losses}S",
                16, TextAnchor.MiddleCenter);
            record.color = Muted;
            PvpUiFactory.SetAnchors(
                (RectTransform)record.transform,
                new Vector2(0.67f, 0.05f), new Vector2(0.82f, 0.95f));

            Text points = PvpUiFactory.CreateValueText(
                row, "Final MMR", entry == null ? "—" : $"{entry.finalMmr:N0} MMR", 19);
            points.color = Violet;
            PvpUiFactory.SetAnchors(
                (RectTransform)points.transform,
                new Vector2(0.82f, 0.05f), new Vector2(0.97f, 0.95f));
        }

        private void CreatePersonalRankedRow(RectTransform parent, Vector2 minimum, Vector2 maximum)
        {
            RectTransform row = PvpUiFactory.CreateSoftPanel(
                parent, "Your Placement", new Color(0.19f, 0.075f, 0.27f, 0.96f));
            PvpUiFactory.SetAnchors(row, minimum, maximum);

            string rank = profile == null || profile.globalRank <= 0 ? "—" : $"#{profile.globalRank:N0}";
            string name = profile?.username ?? "Il tuo profilo";
            string league = profile == null
                ? "Dati in caricamento"
                : profile.placement
                    ? $"Piazzamento · {profile.placementRemaining} rimaste"
                    : profile.ranked ? $"{profile.tier} {profile.division}" : "Non classificato";
            string points = profile != null && profile.ranked ? $"{profile.leaguePoints:N0} LP" : "—";

            AddStickyRowText(row, rank, name, league, points);
        }

        private void CreatePersonalHallRow(RectTransform parent, Vector2 minimum, Vector2 maximum)
        {
            RectTransform row = PvpUiFactory.CreateSoftPanel(
                parent, "Your Historic Placement", new Color(0.19f, 0.075f, 0.27f, 0.96f));
            PvpUiFactory.SetAnchors(row, minimum, maximum);

            HallOfFameEntry personal = hallOfFame?.you;
            if (personal == null && hallOfFame?.entries != null)
            {
                foreach (HallOfFameEntry entry in hallOfFame.entries)
                    if (entry != null && IsMe(entry.playerId))
                    {
                        personal = entry;
                        break;
                    }
            }

            string rank = personal == null ? "—" : $"#{personal.rank:N0}";
            string name = personal?.username ?? profile?.username ?? "Il tuo profilo";
            string league = personal == null ? "Nessun piazzamento archiviato" : $"{personal.tier} {personal.division}";
            string points = personal == null ? "—" : $"{personal.finalMmr:N0} MMR";
            AddStickyRowText(row, rank, name, league, points);
        }

        private static void AddStickyRowText(
            RectTransform row, string rank, string name, string league, string points)
        {
            Text placement = PvpUiFactory.CreateValueText(
                row, "Placement", rank, 24, TextAnchor.MiddleCenter);
            placement.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)placement.transform,
                new Vector2(0.015f, 0.05f), new Vector2(0.09f, 0.95f));

            Text caption = PvpUiFactory.CreateTitleText(
                row, "Username", name, 20, TextAnchor.MiddleLeft);
            caption.color = Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)caption.transform,
                new Vector2(0.11f, 0.05f), new Vector2(0.51f, 0.95f));

            Text tier = PvpUiFactory.CreateLabel(
                row, "League", league, 17, TextAnchor.MiddleLeft);
            tier.color = new Color(0.82f, 0.62f, 0.94f, 1f);
            PvpUiFactory.SetAnchors(
                (RectTransform)tier.transform,
                new Vector2(0.51f, 0.05f), new Vector2(0.78f, 0.95f));

            Text score = PvpUiFactory.CreateValueText(
                row, "Score", points, 21, TextAnchor.MiddleRight);
            score.color = PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)score.transform,
                new Vector2(0.78f, 0.05f), new Vector2(0.97f, 0.95f));
        }

        private static void AddPersonalStat(
            RectTransform parent, string label, string value, float anchorY)
        {
            Text caption = PvpUiFactory.CreateLabel(
                parent, label, label, 15, TextAnchor.MiddleLeft);
            caption.color = Muted;
            PvpUiFactory.SetAnchors(
                (RectTransform)caption.transform,
                new Vector2(0.08f, anchorY), new Vector2(0.58f, anchorY + 0.06f));

            Text valueText = PvpUiFactory.CreateValueText(
                parent, $"{label} Value", value, 18, TextAnchor.MiddleRight);
            valueText.color = PaleGold;
            PvpUiFactory.SetAnchors(
                (RectTransform)valueText.transform,
                new Vector2(0.58f, anchorY), new Vector2(0.92f, anchorY + 0.06f));

            var separatorObject = new GameObject($"{label} Separator", typeof(RectTransform), typeof(Image));
            separatorObject.transform.SetParent(parent, false);
            Image separator = separatorObject.GetComponent<Image>();
            separator.color = new Color(0.65f, 0.48f, 0.22f, 0.28f);
            separator.raycastTarget = false;
            PvpUiFactory.SetAnchors(
                (RectTransform)separatorObject.transform,
                new Vector2(0.08f, anchorY - 0.005f), new Vector2(0.92f, anchorY - 0.003f));
        }

        private static RectTransform CreateScrollContent(
            Transform parent, string name, Vector2 minimum, Vector2 maximum)
        {
            RectTransform viewport = PvpUiFactory.CreateSoftPanel(
                parent, name, new Color(0.006f, 0.008f, 0.012f, 0.88f));
            PvpUiFactory.SetAnchors(viewport, minimum, maximum);
            viewport.gameObject.AddComponent<RectMask2D>();

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 34f;

            var contentObject = new GameObject(
                "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        private static RectTransform CreateListRow(Transform content, bool highlighted, string name)
        {
            RectTransform row = PvpUiFactory.CreateSoftPanel(
                content, name,
                highlighted ? new Color(0.19f, 0.075f, 0.27f, 0.97f) : Row);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 58f;
            element.flexibleWidth = 1f;
            return row;
        }

        private static void AddMessageRow(RectTransform content, string message)
        {
            RectTransform row = CreateListRow(content, false, "Message");
            Text text = PvpUiFactory.CreateLabel(
                row, "Text", message, 18, TextAnchor.MiddleCenter);
            text.color = Muted;
            PvpUiFactory.Stretch((RectTransform)text.transform, 12f, 2f);
        }

        private static RectTransform CreateFramedPanel(
            Transform parent, string name, Vector2 minimum, Vector2 maximum)
        {
            return CreateFramedPanel(parent, name, minimum, maximum, Gold);
        }

        private static RectTransform CreateFramedPanel(
            Transform parent, string name, Vector2 minimum, Vector2 maximum, Color accent)
        {
            RectTransform panel = PvpUiFactory.CreateContainer(parent, name);
            PvpUiFactory.SetAnchors(panel, minimum, maximum);
            AddOrnateFrame(panel, new Color(accent.r, accent.g, accent.b, 0.46f));
            return panel;
        }

        private static void AddOrnateFrame(RectTransform parent, Color tint)
        {
            Sprite sprite = Resources.Load<Sprite>(OrnateFrameResource);
            if (sprite == null)
                return;

            Image frame = CreateImage(parent, "Ornate Frame");
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.color = tint;
            frame.raycastTarget = false;
            PvpUiFactory.Stretch((RectTransform)frame.transform);
            frame.transform.SetAsLastSibling();
        }

        private static Image CreateImage(Transform parent, string name)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            Image image = holder.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private bool IsMe(string playerId)
        {
            return !string.IsNullOrEmpty(myPlayerId)
                && string.Equals(playerId, myPlayerId, StringComparison.Ordinal);
        }

        private HallOfFameSeasonDto FindSeason(int seasonId)
        {
            if (hallOfFameSeasons?.seasons == null)
                return null;
            foreach (HallOfFameSeasonDto season in hallOfFameSeasons.seasons)
                if (season != null && season.seasonId == seasonId)
                    return season;
            return null;
        }

        private static LeaderboardEntry EntryAt(LeaderboardEntry[] entries, int index)
        {
            return entries != null && index >= 0 && index < entries.Length ? entries[index] : null;
        }

        private static HallOfFameEntry EntryAt(HallOfFameEntry[] entries, int index)
        {
            return entries != null && index >= 0 && index < entries.Length ? entries[index] : null;
        }

        private static string SeasonSummary(HallOfFameSeasonDto season)
        {
            if (season == null)
                return "Seleziona una stagione";
            return $"{season.participants:N0} partecipanti  ·  {FormatServerDate(season.endedAt)}";
        }

        private static string FormatServerDate(string value)
        {
            if (DateTime.TryParse(value, out DateTime date))
                return date.ToLocalTime().ToString("dd/MM/yyyy");
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string FormatDuration(int seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
            if (duration.TotalDays >= 1d)
                return $"{(int)duration.TotalDays}g {duration.Hours:00}h {duration.Minutes:00}m";
            if (duration.TotalHours >= 1d)
                return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
            return $"{duration.Minutes:00}m {duration.Seconds:00}s";
        }

        private static Sprite LoadRuntimeSprite(string resourcePath)
        {
            Sprite imported = Resources.Load<Sprite>(resourcePath);
            if (imported != null)
                return imported;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                return null;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void ApplyBackgroundForOrientation(bool force)
        {
            bool portrait = Screen.height > Screen.width;
            if (!force && portrait == backgroundIsPortrait)
                return;

            backgroundIsPortrait = portrait;
            backgroundImage.sprite = LoadRuntimeSprite(
                portrait ? PortraitBackgroundResource : BackgroundResource);
        }
    }
}
