using System;
using AccardND.Battlefield;
using AccardND.Localization;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Hub multiplayer: intestazione con ritratto e lega, due schede (RANKED e
    /// STANZE) e barra di stato. La scheda ranked mostra l'emblema della lega,
    /// il conto alla rovescia della stagione e le statistiche; la scheda stanze
    /// gestisce creazione, ingresso con codice ed elenco delle stanze aperte.
    /// Non parla col server: chiede e riceve tutto tramite callback e metodi Set*.
    /// </summary>
    internal sealed class PvpLobbyScreen
    {
        /// <summary>Richieste e azioni verso il PvpBootstrap.</summary>
        public sealed class Callbacks
        {
            public Action OnClose;

            /// <summary>Nome, modalità (vedi RoomModes) e visibilità scelti nel dialogo di creazione.</summary>
            public Action<string, string, bool> OnCreateRoom;

            /// <summary>Ingresso in una stanza col codice indicato.</summary>
            public Action<string> OnJoinRoom;

            public Action OnQueue;
            public Action OnCancelQueue;
            public Action OnLeaveRoom;
            public Action OnLoadout;
            public Action OnProfile;
            public Action OnSettings;
            public Action OnRefreshRooms;
            public Action OnRefreshProfile;
        }

        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 6;
        private const string TabRanked = "ranked";
        private const string TabRooms = "rooms";
        private const string BackdropResource = "UI/MultiplayerRestyle/multiplayer_gothic_hall";
        private const string FallbackAvatarResource = "UI/MultiplayerRestyle/multiplayer_hooded_avatar";
        private const string FallbackCrestResource = "UI/MultiplayerRestyle/rank_diamond";
        private const string AvatarFrameResource = "UI/MultiplayerRestyle/avatar_frame";
        private const string TitleCrestResource = "UI/MultiplayerRestyle/multiplayer_title_crest";
        private const string RankedTrophyResource = "UI/MultiplayerRestyle/ranked_trophy_icon";
        private const string RoomsGroupResource = "UI/MultiplayerRestyle/rooms_group_icon";
        private const string HeaderActionStripResource = "UI/MultiplayerRestyle/header_action_icons_v2";
        private const string RankedCtaFrameResource = "UI/MultiplayerRestyle/ranked_cta_frame_v3";
        private const string RoomFeatureStripResource = "UI/MultiplayerRestyle/room_feature_icons_v2";

        /// <summary>Intervallo di aggiornamento automatico dell'elenco stanze.</summary>
        private const float RoomRefreshSeconds = 8f;

        private readonly Callbacks callbacks;
        private readonly Func<string, Sprite> iconArtwork;
        private readonly RectTransform root;
        private readonly Sprite fallbackAvatar;
        private readonly Sprite fallbackCrest;

        // Intestazione.
        private readonly Image avatarPortrait;
        private readonly Text playerText;
        private readonly Text levelText;
        private readonly PvpUiFactory.ProgressBar xpBar;
        private readonly Text leagueText;

        // Schede.
        private readonly Button rankedTab;
        private readonly Button roomsTab;
        private readonly RectTransform roomsBadge;
        private readonly Text roomsBadgeText;
        private string currentTab = TabRanked;

        // Scheda ranked.
        private readonly RectTransform rankedPanel;
        private readonly Image crestImage;
        private readonly PvpUiVfx rankVfx;
        private readonly Text tierText;
        private readonly Image[] divisionStars = new Image[5];
        private readonly Text seasonNameText;
        private readonly Text countdownText;
        private readonly Text globalRankText;
        private readonly PvpUiFactory.ProgressBar leagueBar;
        private readonly Text winsValue;
        private readonly Text playedValue;
        private readonly Text winRateValue;

        // Scheda stanze (costruita da BuildRoomsTab).
        private readonly RectTransform roomsPanel;
        private Text typedCodeText;
        private RectTransform codeEntryPanel;
        private RectTransform activeRoomPanel;
        private Text activeRoomText;
        private RectTransform roomListContent;
        private Text roomListEmptyText;
        private RectTransform listCaptionRect;
        private RectTransform refreshRect;
        private RectTransform roomListRect;

        // Dialogo di creazione (costruito da BuildCreateDialog).
        private readonly RectTransform createDialog;
        private InputField createNameField;
        private Button modeStandardButton;
        private Button modeHardcoreButton;
        private Text modeDescriptionText;
        private Button visibilityPublicButton;
        private Button visibilityPrivateButton;
        private Text visibilityDescriptionText;
        private string createMode = RoomModes.Standard;
        private bool createIsPublic = true;

        // Attesa avversario.
        private readonly RectTransform waitingPanel;
        private readonly Image[] waitingDiceImages = new Image[3];
        private readonly int[] waitingDiceSides = { 4, 6, 8, 10, 12, 20 };
        private float waitingDiceTimer;
        private bool waitingForOpponent;

        private readonly Text statusText;
        private readonly Text loadoutAvailabilityText;

        private string typedCode = string.Empty;
        private TouchScreenKeyboard nativeKeyboard;
        private string activeRoomCode;
        private RoomSummary[] rooms = Array.Empty<RoomSummary>();
        private float roomRefreshTimer;
        private float seasonSecondsRemaining;
        private float countdownRedrawTimer;

        public PvpLobbyScreen(
            Transform parent, string username, Callbacks callbacks, Func<string, Sprite> iconArtwork = null)
        {
            this.callbacks = callbacks;
            this.iconArtwork = iconArtwork;
            fallbackAvatar = Resources.Load<Sprite>(FallbackAvatarResource);
            fallbackCrest = PvpUiFactory.RankEmblem("Nabbo")
                ?? Resources.Load<Sprite>(FallbackCrestResource);

            // Il mockup è full-bleed. La lobby ignora soltanto per il proprio fondale
            // la safe area del bootstrap; i controlli restano comunque lontani dal notch.
            Transform lobbyParent = parent;
            if (parent != null && parent.GetComponent<SafeAreaRect>() != null && parent.parent != null)
                lobbyParent = parent.parent;

            root = PvpUiFactory.CreatePanel(lobbyParent, "Lobby", PvpUiFactory.Ink);
            PvpUiFactory.Stretch(root);
            CreateBackdrop(root);

            RectTransform content = PvpUiFactory.CreateContainer(root, "Lobby Content");
            PvpUiFactory.Stretch(content);
            PvpUiFactory.CreateScreenOuterFrame(root, 0.795f, content);

            // L'account header, Settings e Honey Pot sono quelli condivisi dell'Hub.
            // Il PvP non ne costruisce una seconda copia nel proprio canvas.
            avatarPortrait = null;
            playerText = null;
            levelText = null;
            xpBar = null;
            leagueText = null;
            statusText = null;

            // --- Fascia titolo del portale multiplayer ---
            RectTransform titleBand = PvpUiFactory.CreateScreenTitlePanel(
                content,
                "Arena Title Band",
                GameText.GetOrFallbackSilent(GameTextKeys.Hub.Arena, "ARENA"),
                null,
                48);
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.9f));

            // --- Schede ---
            rankedTab = PvpUiFactory.CreateTabButton(
                content, "Tab Ranked", "RANKED", () => SwitchTab(TabRanked), 29);
            MmoUiTheme.StyleAsScreenTitle(rankedTab.GetComponentInChildren<Text>());
            PvpUiFactory.SetAnchors((RectTransform)rankedTab.transform, new Vector2(0.215f, 0.699f), new Vector2(0.505f, 0.752f));
            AddTabRankIcon(rankedTab);

            roomsTab = PvpUiFactory.CreateTabButton(
                content, "Tab Rooms", "STANZE", () => SwitchTab(TabRooms), 29);
            MmoUiTheme.StyleAsScreenTitle(roomsTab.GetComponentInChildren<Text>());
            PvpUiFactory.SetAnchors((RectTransform)roomsTab.transform, new Vector2(0.505f, 0.699f), new Vector2(0.795f, 0.752f));
            AddTabPeopleIcon(roomsTab);

            roomsBadgeText = PvpUiFactory.CreateBadge(
                roomsTab.transform, "Rooms Badge", "0", new Color(0.55f, 0.12f, 0.14f, 0.88f), 20);
            roomsBadge = (RectTransform)roomsBadgeText.transform.parent;
            Image roomsBadgeImage = roomsBadge.GetComponent<Image>();
            roomsBadgeImage.sprite = MmoUiTheme.GetRadialGlowSprite();
            roomsBadgeImage.type = Image.Type.Simple;
            roomsBadgeImage.color = new Color(0.74f, 0.08f, 0.055f, 0.98f);
            PvpUiFactory.SetAnchors(roomsBadge, new Vector2(0.91f, 0.62f), new Vector2(1.045f, 1.18f));
            roomsBadge.gameObject.SetActive(false);
            // Il contatore è decoro: i tocchi devono arrivare alla linguetta sotto.
            roomsBadge.GetComponent<Image>().raycastTarget = false;
            roomsBadgeText.raycastTarget = false;

            // --- Contenuti delle schede: una sola sezione visibile per volta ---
            rankedPanel = PvpUiFactory.CreateContainer(content, "Ranked Panel");
            PvpUiFactory.SetAnchors(rankedPanel, new Vector2(0.025f, 0.012f), new Vector2(0.982f, 0.709f));

            roomsPanel = PvpUiFactory.CreateContainer(content, "Rooms Panel");
            PvpUiFactory.SetAnchors(roomsPanel, new Vector2(0.025f, 0.012f), new Vector2(0.985f, 0.709f));

            rankVfx = PvpUiVfx.CreateRankAura(
                rankedPanel,
                new Vector2(0.035f, 0.385f),
                new Vector2(0.435f, 0.935f),
                PvpUiFactory.Violet);

            crestImage = CreateCrest(rankedPanel);
            tierText = PvpUiFactory.CreateTitleText(rankedPanel, "Tier", "NON CLASSIFICATO", 32);
            PvpUiFactory.SetAnchors((RectTransform)tierText.transform, new Vector2(0.05f, 0.31f), new Vector2(0.49f, 0.43f));

            CreateDivisionStars(rankedPanel);

            seasonNameText = PvpUiFactory.CreateTitleText(
                rankedPanel, "Season Name", "STAGIONE IN CORSO", 55, TextAnchor.MiddleLeft);
            MmoUiTheme.StyleAsScreenTitle(seasonNameText);
            seasonNameText.color = PvpUiFactory.Violet;
            PvpUiFactory.SetAnchors((RectTransform)seasonNameText.transform, new Vector2(0.51f, 0.845f), new Vector2(0.98f, 0.945f));

            countdownText = PvpUiFactory.CreateText(
                rankedPanel, "Countdown", "Durata stagione in aggiornamento", 35, TextAnchor.MiddleLeft, FontStyle.Normal);
            countdownText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)countdownText.transform, new Vector2(0.51f, 0.79f), new Vector2(0.98f, 0.875f));

            Text rankCaption = PvpUiFactory.CreateLabel(
                rankedPanel, "Rank Caption", "IL TUO RANK", 35, TextAnchor.LowerLeft);
            PvpUiFactory.SetAnchors((RectTransform)rankCaption.transform, new Vector2(0.51f, 0.73f), new Vector2(0.98f, 0.8f));

            globalRankText = PvpUiFactory.CreateValueText(rankedPanel, "Global Rank", "-", 40, TextAnchor.MiddleCenter);
            globalRankText.color = Color.white;
            PvpUiFactory.SetAnchors((RectTransform)globalRankText.transform, new Vector2(0.61f, 0.65f), new Vector2(0.98f, 0.74f));
            AddSmallRankCrest(rankedPanel);

            leagueBar = PvpUiFactory.CreateProgressBar(rankedPanel, "League Bar", PvpUiFactory.Violet, 18);
            PvpUiFactory.SetAnchors(leagueBar.Root, new Vector2(0.61f, 0.59f), new Vector2(0.96f, 0.64f));
            leagueBar.SetValue(0f, "-");

            PvpUiFactory.StatRow wins = PvpUiFactory.CreateStatRow(rankedPanel, "Wins Row", "VITTORIE", "-");
            PvpUiFactory.SetAnchors(wins.Root, new Vector2(0.51f, 0.48f), new Vector2(0.98f, 0.57f));
            wins.Caption.fontSize = 40;
            winsValue = wins.Value;
            winsValue.fontSize = 40;
            winsValue.alignment = TextAnchor.MiddleCenter;

            PvpUiFactory.StatRow played = PvpUiFactory.CreateStatRow(rankedPanel, "Played Row", "PARTITE GIOCATE", "-");
            PvpUiFactory.SetAnchors(played.Root, new Vector2(0.51f, 0.38f), new Vector2(0.98f, 0.47f));
            played.Caption.fontSize = 40;
            playedValue = played.Value;
            playedValue.fontSize = 40;
            playedValue.alignment = TextAnchor.MiddleCenter;

            PvpUiFactory.StatRow winRate = PvpUiFactory.CreateStatRow(rankedPanel, "Win Rate Row", "WIN RATE", "-");
            PvpUiFactory.SetAnchors(winRate.Root, new Vector2(0.51f, 0.28f), new Vector2(0.98f, 0.37f));
            winRate.Caption.fontSize = 40;
            winRateValue = winRate.Value;
            winRateValue.fontSize = 40;
            winRateValue.alignment = TextAnchor.MiddleCenter;

            Button loadout = PvpUiFactory.CreateButton(
                rankedPanel, "Loadout", "LOADOUT",
                new Color(0.1f, 0.55f, 0.25f, 0.98f), () => callbacks.OnLoadout?.Invoke(), 30);
            RectTransform loadoutRect = (RectTransform)loadout.transform;
            PvpUiFactory.SetAnchors(
                loadoutRect, new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.22f));
            loadoutRect.sizeDelta = new Vector2(0f, 96f);
            MmoUiTheme.ApplyConfirmButtonStyle(loadout, loadout.GetComponentInChildren<Text>());

            loadoutAvailabilityText = PvpUiFactory.CreateLabel(
                rankedPanel, "Loadout Availability", string.Empty, 18, TextAnchor.MiddleCenter);
            PvpUiFactory.SetAnchors(
                (RectTransform)loadoutAvailabilityText.transform,
                new Vector2(0.15f, 0.275f), new Vector2(0.85f, 0.325f));

            Button playRanked = PvpUiFactory.CreateButton(
                rankedPanel, "Play Ranked", "GIOCA RANKED",
                new Color(0.32f, 0.13f, 0.54f, 0.98f), () => callbacks.OnQueue?.Invoke(), 38);
            RectTransform playRankedRect = (RectTransform)playRanked.transform;
            PvpUiFactory.SetAnchors(
                playRankedRect, new Vector2(0.14f, 0.115f), new Vector2(0.86f, 0.115f));
            playRankedRect.sizeDelta = new Vector2(0f, 160f);
            ApplyRankedCtaFrame(playRanked);
            PvpUiVfx.CreateRankedButton(playRankedRect, PvpUiFactory.Violet);
            Text playLabel = playRanked.GetComponentInChildren<Text>();
            if (playLabel != null)
            {
                MmoUiTheme.StyleAsScreenTitle(playLabel);
                playLabel.alignment = TextAnchor.MiddleCenter;
                PvpUiFactory.SetAnchors(
                    (RectTransform)playLabel.transform, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.97f));
                ((RectTransform)playLabel.transform).anchoredPosition = new Vector2(18f, 0f);
            }
            AddCrossedSwords(playRanked.transform, new Vector2(0.18f, 0.5f), new Vector2(42f, 54f));

            BuildRoomsTab();

            // --- Attesa avversario: copre il corpo, qualunque scheda sia aperta ---
            waitingPanel = PvpUiFactory.CreateSoftPanel(
                content, "Waiting Opponent Panel", new Color(0.05f, 0.03f, 0.09f, 0.89f));
            PvpUiFactory.SetAnchors(waitingPanel, new Vector2(0.025f, 0.012f), new Vector2(0.982f, 0.709f));
            waitingPanel.gameObject.SetActive(false);
            BuildWaitingPanel();

            // --- Dialogo di creazione stanza (sopra tutto) ---
            createDialog = PvpUiFactory.CreatePanel(root, "Create Room Overlay", new Color(0f, 0f, 0f, 0.96f));
            PvpUiFactory.Stretch(createDialog);
            createDialog.gameObject.SetActive(false);
            BuildCreateDialog();

            SwitchTab(TabRanked);
        }

        // ---------- API usata dal PvpBootstrap ----------

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy() => UnityEngine.Object.Destroy(root.gameObject);

        public void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message ?? string.Empty;
        }

        public void SetLoadoutAvailability(string message, bool rankedEligible)
        {
            loadoutAvailabilityText.text = message ?? string.Empty;
            loadoutAvailabilityText.color = rankedEligible ? PvpUiFactory.Good : PvpUiFactory.Gold;
        }

        public void SetPlayerName(string username)
        {
            if (playerText != null)
                playerText.text = username;
        }

        public void SetAccountProgress(SinglePlayerProgressData progress)
        {
            if (progress == null)
                return;

            int level = Mathf.Max(1, progress.accountLevel);
            int currentExperience = Mathf.Max(0, progress.accountExperience);
            int experienceToNextLevel = Mathf.Max(1, progress.accountExperienceToNextLevel);

            if (levelText != null)
                levelText.text = $"Lv. {level}";
            xpBar?.SetValue(
                Mathf.Clamp01((float)currentExperience / experienceToNextLevel),
                $"{FormatCount(currentExperience)} / {FormatCount(experienceToNextLevel)}");
        }

        public void SetProfile(ProfileData profile)
        {
            if (profile == null)
                return;

            if (!string.IsNullOrWhiteSpace(profile.username))
                SetPlayerName(profile.username);

            Color accent = PvpUiFactory.TierAccent(profile.tier);
            bool showLeague = profile.ranked && !profile.placement;

            if (leagueText != null)
            {
                leagueText.text = FormatLeagueLine(profile);
                leagueText.color = showLeague ? accent : PvpUiFactory.TextMuted;
            }

            tierText.text = showLeague
                ? $"{profile.tier} {profile.division}".Trim().ToUpperInvariant()
                : profile.placement ? "PIAZZAMENTO" : "NON CLASSIFICATO";
            tierText.color = showLeague ? accent : PvpUiFactory.TextMuted;

            crestImage.sprite = PvpUiFactory.RankEmblem(profile.tier) ?? fallbackCrest;
            crestImage.color = new Color(1f, 1f, 1f, showLeague ? 1f : 0.62f);
            rankVfx.SetTint(accent, showLeague ? 1f : 0.32f);

            ApplyDivisionStars(showLeague ? profile.division : null, accent);

            seasonNameText.text = string.IsNullOrWhiteSpace(profile.seasonName)
                ? "STAGIONE IN CORSO"
                : profile.seasonName.ToUpperInvariant();
            seasonSecondsRemaining = Mathf.Max(0, profile.seasonSecondsRemaining);
            RedrawCountdown();

            globalRankText.text = profile.globalRank > 0 ? $"#{FormatCount(profile.globalRank)}" : "-";
            globalRankText.color = profile.globalRank == 1 ? PvpUiFactory.Gold : Color.white;

            if (profile.placement)
                leagueBar.SetValue(0f, $"Piazzamento: {profile.placementRemaining} partite rimaste");
            else if (showLeague)
                leagueBar.SetValue(profile.leaguePoints / 100f, $"{profile.leaguePoints} / 100 LP");
            else
                leagueBar.SetValue(0f, "Nessuna partita ranked giocata");

            int played = profile.wins + profile.losses;
            winsValue.text = profile.wins.ToString();
            playedValue.text = played.ToString();
            winRateValue.text = played > 0 ? $"{profile.winRatePercent}%" : "-";
            winRateValue.color = played == 0
                ? PvpUiFactory.TextMuted
                : profile.winRatePercent >= 50
                    ? PvpUiFactory.Good
                    : PvpUiFactory.Bad;

            ApplyAvatar(profile.selectedIconId, accent);
        }

        /// <summary>Elenco stanze aperte ricevuto dal server.</summary>
        public void SetRooms(RoomsData data)
        {
            rooms = data?.rooms ?? Array.Empty<RoomSummary>();
            RenderRooms();
        }

        public void ShowRoom(RoomCreated created)
        {
            if (created == null)
                return;
            activeRoomCode = created.code;
            activeRoomPanel.gameObject.SetActive(true);
            ApplyRoomListLayout();
            activeRoomText.text =
                $"{created.roomName ?? "Stanza"}  -  CODICE {created.code}\n"
                + $"{RoomModes.DisplayName(created.mode)} - {(created.isPublic ? "pubblica" : "solo con codice")}";
            SwitchTab(TabRooms);
            RenderRooms();
        }

        public void ClearRoomCode()
        {
            activeRoomCode = null;
            activeRoomPanel.gameObject.SetActive(false);
            ApplyRoomListLayout();
            RenderRooms();
        }

        public void SetWaitingForOpponent(bool waiting)
        {
            waitingForOpponent = waiting;
            if (waitingPanel != null)
                waitingPanel.gameObject.SetActive(waiting);
            if (waiting)
                RollWaitingDice();
        }

        public void Tick()
        {
            if (waitingForOpponent)
                TickWaitingDice();

            TickCountdown();
            TickRoomRefresh();

            if (nativeKeyboard != null)
            {
                typedCode = Sanitize(nativeKeyboard.text);
                RefreshTypedCode();
                TouchScreenKeyboard.Status keyboardStatus = nativeKeyboard.status;
                if (keyboardStatus == TouchScreenKeyboard.Status.Done)
                {
                    nativeKeyboard = null;
                    SubmitTypedCode();
                }
                else if (keyboardStatus != TouchScreenKeyboard.Status.Visible)
                {
                    nativeKeyboard = null;
                    if (codeEntryPanel != null)
                        codeEntryPanel.gameObject.SetActive(false);
                }
                return;
            }

            // Il dialogo di creazione ha il suo InputField: non rubargli i tasti.
            if (!TouchScreenKeyboard.isSupported && !createDialog.gameObject.activeSelf)
                ApplyPhysicalKeyboardInput();
        }

        // ---------- Costruzione ----------

        private static void AddTitleCrest(Transform parent)
        {
            Sprite crest = Resources.Load<Sprite>(TitleCrestResource);
            if (crest == null)
                return;

            CreateAnchoredIcon(
                parent,
                "Multiplayer Heraldic Crest",
                crest,
                new Vector2(0.455f, 0.862f),
                new Vector2(0.545f, 0.935f),
                Color.white);
        }

        private static Sprite LoadCroppedSprite(
            string resourcePath, Rect crop, Vector2 referenceSize)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                return null;

            if (referenceSize.x <= 0f || referenceSize.y <= 0f)
                return null;

            float scaleX = texture.width / referenceSize.x;
            float scaleY = texture.height / referenceSize.y;
            Rect scaledCrop = new Rect(
                crop.x * scaleX,
                crop.y * scaleY,
                crop.width * scaleX,
                crop.height * scaleY);

            return Sprite.Create(
                texture,
                scaledCrop,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
        }

        private static Sprite LoadStripIcon(string resourcePath, int index, int count)
        {
            Texture2D strip = Resources.Load<Texture2D>(resourcePath);
            if (strip == null || count <= 0 || index < 0 || index >= count)
                return null;

            int cellWidth = strip.width / count;
            return Sprite.Create(
                strip,
                new Rect(index * cellWidth, 0f, cellWidth, strip.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
        }

        private static Button CreateHeaderActionButton(
            Transform parent,
            string name,
            int stripIndex,
            UnityAction onClick,
            Vector2 min,
            Vector2 max)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);
            Image image = holder.GetComponent<Image>();
            Rect crop;
            switch (stripIndex)
            {
                case 0: crop = new Rect(166f, 136f, 422f, 422f); break;
                case 1: crop = new Rect(814f, 136f, 423f, 422f); break;
                default: crop = new Rect(1464f, 136f, 422f, 422f); break;
            }
            image.sprite = LoadCroppedSprite(
                HeaderActionStripResource, crop, new Vector2(2048f, 684f));
            image.preserveAspect = true;
            image.color = Color.white;

            Button button = holder.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);
            PvpUiFactory.SetAnchors((RectTransform)holder.transform, min, max);
            return button;
        }

        private static Button CreateSharedHeaderActionButton(
            Transform parent,
            string name,
            string resourcePath,
            UnityAction onClick,
            Vector2 min,
            Vector2 max)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);
            Image image = holder.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(resourcePath);
            image.preserveAspect = true;
            image.color = Color.white;

            Button button = holder.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);
            PvpUiFactory.SetAnchors((RectTransform)holder.transform, min, max);
            return button;
        }

        private static void AddMailNotification(Transform parent)
        {
            RectTransform badge = PvpUiFactory.CreateSoftPanel(
                parent, "Mail Notification", new Color(0.68f, 0.06f, 0.035f, 1f));
            Image image = badge.GetComponent<Image>();
            image.sprite = MmoUiTheme.GetRadialGlowSprite();
            image.type = Image.Type.Simple;
            PvpUiFactory.SetAnchors(badge, new Vector2(0.793f, 0.901f), new Vector2(0.818f, 0.927f));

            Text exclamation = PvpUiFactory.CreateTitleText(
                badge, "Exclamation", "!", 13, TextAnchor.MiddleCenter);
            exclamation.color = Color.white;
            PvpUiFactory.Stretch((RectTransform)exclamation.transform);
        }

        private static void ApplyRankedCtaFrame(Button button)
        {
            if (button == null)
                return;

            Sprite frame = LoadCroppedSprite(
                RankedCtaFrameResource,
                new Rect(145f, 196f, 1692f, 400f),
                new Vector2(2048f, 820f));
            Image image = button.GetComponent<Image>();
            if (frame == null || image == null)
                return;

            image.sprite = frame;
            image.type = Image.Type.Simple;
            image.color = Color.white;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.68f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
        }

        private static void ConfigureAvatarFrame(RectTransform avatarRoot, Image portrait)
        {
            Image oldPanel = avatarRoot.GetComponent<Image>();
            if (oldPanel != null)
            {
                oldPanel.sprite = null;
                oldPanel.color = Color.clear;
                oldPanel.raycastTarget = false;
            }

            var maskObject = new GameObject("Circular Portrait Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskObject.transform.SetParent(avatarRoot, false);
            RectTransform maskRect = (RectTransform)maskObject.transform;
            PvpUiFactory.SetAnchors(maskRect, new Vector2(0.145f, 0.145f), new Vector2(0.855f, 0.855f));
            Image maskImage = maskObject.GetComponent<Image>();
            maskImage.sprite = MmoUiTheme.GetRadialGlowSprite();
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            maskObject.GetComponent<Mask>().showMaskGraphic = false;

            portrait.transform.SetParent(maskRect, false);
            portrait.preserveAspect = false;
            PvpUiFactory.Stretch((RectTransform)portrait.transform);

            Sprite frameSprite = Resources.Load<Sprite>(AvatarFrameResource);
            if (frameSprite == null)
                return;

            var frameObject = new GameObject("Avatar Gold Violet Frame", typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(avatarRoot, false);
            Image frameImage = frameObject.GetComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = false;
            PvpUiFactory.Stretch((RectTransform)frameObject.transform);
        }

        private static void AddTabRankIcon(Button tab)
        {
            Text label = tab.GetComponentInChildren<Text>();
            if (label != null)
                PvpUiFactory.SetAnchors(
                    (RectTransform)label.transform, new Vector2(0.23f, 0.04f), new Vector2(0.96f, 0.96f));

            CreateAnchoredIcon(
                tab.transform,
                "Rank Emblem",
                Resources.Load<Sprite>(RankedTrophyResource),
                new Vector2(0.055f, 0.1f),
                new Vector2(0.245f, 0.9f),
                Color.white);
        }

        private static void AddTabPeopleIcon(Button tab)
        {
            Text label = tab.GetComponentInChildren<Text>();
            if (label != null)
                PvpUiFactory.SetAnchors(
                    (RectTransform)label.transform, new Vector2(0.25f, 0.04f), new Vector2(0.96f, 0.96f));

            CreateAnchoredIcon(
                tab.transform,
                "Players Icon",
                Resources.Load<Sprite>(RoomsGroupResource),
                new Vector2(0.05f, 0.08f),
                new Vector2(0.25f, 0.92f),
                Color.white);
        }

        private static void AddRankInfoIcon(Transform parent)
        {
            Sprite info = Resources.Load<Sprite>("UI/info_button");
            if (info == null)
                return;
            CreateAnchoredIcon(
                parent,
                "Rank Info",
                info,
                new Vector2(0.89f, 0.85f),
                new Vector2(0.95f, 0.94f),
                new Color(0.82f, 0.71f, 0.5f, 0.92f));
        }

        private static void AddSmallRankCrest(Transform parent)
        {
            Image smallRankCrest = CreateAnchoredIcon(
                parent,
                "Small Rank Crest",
                MmoUiTheme.GetRankCrestSprite(),
                new Vector2(0.51f, 0.64f),
                new Vector2(0.59f, 0.75f),
                new Color(0.86f, 0.72f, 0.46f, 1f));
            RectTransform rect = smallRankCrest.rectTransform;
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(8f, 0f);
        }

        private static void AddCrossedSwords(
            Transform parent, Vector2 anchor, Vector2 size, Color? tint = null)
        {
            Sprite sword = Resources.Load<Sprite>("UI/warrior_sword");
            if (sword == null)
                return;

            Color color = tint ?? new Color(0.9f, 0.75f, 0.46f, 1f);
            for (int index = 0; index < 2; index++)
            {
                var swordObject = new GameObject(
                    index == 0 ? "Sword Left" : "Sword Right", typeof(RectTransform), typeof(Image));
                swordObject.transform.SetParent(parent, false);
                RectTransform rect = (RectTransform)swordObject.transform;
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, index == 0 ? 42f : -42f);
                Image image = swordObject.GetComponent<Image>();
                image.sprite = sword;
                image.preserveAspect = true;
                image.color = color;
                image.raycastTarget = false;
            }
        }

        private static Image CreateAnchoredIcon(
            Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max, Color color)
        {
            var iconObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            PvpUiFactory.SetAnchors((RectTransform)iconObject.transform, min, max);
            return image;
        }

        private static void CreateBackdrop(Transform parent)
        {
            Sprite sprite = Resources.Load<Sprite>(BackdropResource);
            if (sprite == null)
                return;

            var holder = new GameObject(
                "Gothic Hall Backdrop", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            holder.transform.SetParent(parent, false);

            var image = holder.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(0.94f, 0.94f, 0.97f, 1f);

            RectTransform rect = (RectTransform)holder.transform;
            PvpUiFactory.Stretch(rect);
            var fitter = holder.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            rect.SetAsFirstSibling();
        }

        private Image CreateCrest(Transform parent)
        {
            var holder = new GameObject("Crest", typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            var image = holder.GetComponent<Image>();
            image.sprite = fallbackCrest != null ? fallbackCrest : MmoUiTheme.GetRankCrestSprite();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = fallbackCrest != null ? Color.white : PvpUiFactory.TierAccent(null);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.075f, 0.395f);
            rect.anchorMax = new Vector2(0.425f, 0.89f);
            rect.offsetMin = new Vector2(-14f, 36f);
            rect.offsetMax = new Vector2(-14f, 36f);
            return image;
        }

        private void CreateDivisionStars(Transform parent)
        {
            const float left = 0.09f;
            const float width = 0.056f;
            const float gap = 0.014f;
            for (int index = 0; index < divisionStars.Length; index++)
            {
                var holder = new GameObject($"Division Star {index + 1}", typeof(RectTransform), typeof(Image));
                holder.transform.SetParent(parent, false);
                var image = holder.GetComponent<Image>();
                image.sprite = MmoUiTheme.GetStarSprite();
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0.16f);
                float xMin = left + index * (width + gap);
                PvpUiFactory.SetAnchors(
                    (RectTransform)holder.transform,
                    new Vector2(xMin, 0.27f), new Vector2(xMin + width, 0.33f));
                divisionStars[index] = image;
            }
        }

        private void BuildRoomsTab()
        {
            Button create = PvpUiFactory.CreateButton(
                roomsPanel, "Create Room", "CREA STANZA",
                new Color(0.34f, 0.22f, 0.07f, 0.98f), OpenCreateDialog, 27);
            RectTransform createRect = (RectTransform)create.transform;
            createRect.anchorMin = createRect.anchorMax = Vector2.zero;
            createRect.pivot = new Vector2(0.5f, 0.5f);
            createRect.anchoredPosition = new Vector2(274.5f, 172f);
            createRect.sizeDelta = new Vector2(467f, 168f);
            StyleFeatureButton(
                create, 0, "Imposta le regole e invita", new Color(0.88f, 0.78f, 0.62f, 1f));
            MmoUiTheme.StyleAsScreenTitle(create.GetComponentInChildren<Text>());

            Button joinByCode = PvpUiFactory.CreateButton(
                roomsPanel, "Join With Code", "ENTRA CON CODICE",
                new Color(0.05f, 0.26f, 0.38f, 0.98f), OpenNativeKeyboard, 26);
            RectTransform joinByCodeRect = (RectTransform)joinByCode.transform;
            joinByCodeRect.anchorMin = joinByCodeRect.anchorMax = new Vector2(1f, 0f);
            joinByCodeRect.pivot = new Vector2(0.5f, 0.5f);
            joinByCodeRect.anchoredPosition = new Vector2(-274.5f, 172f);
            joinByCodeRect.sizeDelta = new Vector2(467f, 168f);
            StyleFeatureButton(
                joinByCode, 1, "Inserisci il codice stanza", new Color(0.66f, 0.82f, 0.9f, 1f));
            MmoUiTheme.StyleAsScreenTitle(joinByCode.GetComponentInChildren<Text>());

            activeRoomPanel = PvpUiFactory.CreateSoftPanel(
                roomsPanel, "Active Room Panel", new Color(0.06f, 0.05f, 0.02f, 0.89f));
            PvpUiFactory.SetAnchors(activeRoomPanel, new Vector2(0.025f, 0.72f), new Vector2(0.975f, 0.85f));
            activeRoomPanel.gameObject.SetActive(false);

            activeRoomText = PvpUiFactory.CreateText(
                activeRoomPanel, "Active Room", string.Empty, 22, TextAnchor.MiddleLeft, FontStyle.Normal);
            activeRoomText.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)activeRoomText.transform, new Vector2(0.03f, 0.08f), new Vector2(0.73f, 0.92f));

            Button leave = PvpUiFactory.CreateButton(
                activeRoomPanel, "Leave Room", "CHIUDI", new Color(0.5f, 0.15f, 0.15f, 0.98f),
                () => callbacks.OnLeaveRoom?.Invoke(), 22);
            PvpUiFactory.SetAnchors((RectTransform)leave.transform, new Vector2(0.75f, 0.16f), new Vector2(0.97f, 0.84f));

            Text listCaption = PvpUiFactory.CreateTitleText(
                roomsPanel, "List Caption", "STANZE DISPONIBILI", 45, TextAnchor.MiddleLeft);
            MmoUiTheme.StyleAsScreenTitle(listCaption);
            listCaption.color = PvpUiFactory.Gold;
            listCaptionRect = (RectTransform)listCaption.transform;

            Button refresh = PvpUiFactory.CreateButton(
                roomsPanel, "Refresh Rooms", "AGGIORNA", new Color(0.5f, 0.32f, 0.05f, 0.98f),
                RequestRooms, 28);
            Text refreshLabel = refresh.GetComponentInChildren<Text>();
            if (refreshLabel != null)
                refreshLabel.color = Color.white;
            refreshRect = (RectTransform)refresh.transform;

            RectTransform scrollPanel = PvpUiFactory.CreateSoftPanel(
                roomsPanel, "Room List", new Color(0.01f, 0.018f, 0.03f, 0.88f));
            roomListRect = scrollPanel;
            var scroll = scrollPanel.gameObject.AddComponent<ScrollRect>();
            scrollPanel.gameObject.AddComponent<RectMask2D>();

            var contentHolder = new GameObject(
                "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentHolder.transform.SetParent(scrollPanel, false);
            roomListContent = (RectTransform)contentHolder.transform;
            roomListContent.anchorMin = new Vector2(0f, 1f);
            roomListContent.anchorMax = new Vector2(1f, 1f);
            roomListContent.pivot = new Vector2(0.5f, 1f);
            roomListContent.offsetMin = Vector2.zero;
            roomListContent.offsetMax = Vector2.zero;
            var layout = contentHolder.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentHolder.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = roomListContent;
            scroll.viewport = scrollPanel;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;

            roomListEmptyText = PvpUiFactory.CreateText(
                scrollPanel, "Empty Hint", "Nessuna stanza aperta: creane una tu.",
                45, TextAnchor.MiddleCenter, FontStyle.Normal);
            roomListEmptyText.color = PvpUiFactory.TextMuted;
            roomListEmptyText.raycastTarget = false;
            PvpUiFactory.Stretch((RectTransform)roomListEmptyText.transform, 20f, 10f);

            codeEntryPanel = PvpUiFactory.CreateSoftPanel(
                roomsPanel, "Code Entry Overlay", new Color(0.008f, 0.012f, 0.018f, 0.985f));
            PvpUiFactory.SetAnchors(codeEntryPanel, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.69f));

            Text codeCaption = PvpUiFactory.CreateTitleText(
                codeEntryPanel, "Code Caption", "CODICE STANZA", 20, TextAnchor.MiddleCenter);
            codeCaption.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors(
                (RectTransform)codeCaption.transform, new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.94f));

            Button codeField = PvpUiFactory.CreateButton(
                codeEntryPanel, "Typed Code", "______", new Color(0.08f, 0.12f, 0.17f, 0.98f), OpenNativeKeyboard, 32);
            PvpUiFactory.SetAnchors(
                (RectTransform)codeField.transform, new Vector2(0.06f, 0.34f), new Vector2(0.63f, 0.68f));
            typedCodeText = codeField.GetComponentInChildren<Text>();
            typedCodeText.color = new Color(0.72f, 0.88f, 0.96f, 1f);

            Button join = PvpUiFactory.CreateButton(
                codeEntryPanel, "Join Room", "ENTRA", new Color(0.08f, 0.38f, 0.24f, 0.98f),
                SubmitTypedCode, 22);
            PvpUiFactory.SetAnchors(
                (RectTransform)join.transform, new Vector2(0.66f, 0.34f), new Vector2(0.94f, 0.68f));

            Button closeCode = PvpUiFactory.CreateButton(
                codeEntryPanel, "Close Code Entry", "ANNULLA", new Color(0.42f, 0.11f, 0.1f, 0.98f),
                () => codeEntryPanel.gameObject.SetActive(false), 16);
            PvpUiFactory.SetAnchors(
                (RectTransform)closeCode.transform, new Vector2(0.3f, 0.07f), new Vector2(0.7f, 0.27f));
            codeEntryPanel.gameObject.SetActive(false);

            ApplyRoomListLayout();
        }

        private static void StyleFeatureButton(
            Button button, int artworkIndex, string subtitle, Color subtitleColor)
        {
            Text title = button.GetComponentInChildren<Text>();
            if (title != null)
                PvpUiFactory.SetAnchors(
                    (RectTransform)title.transform, new Vector2(0.23f, 0.44f), new Vector2(0.94f, 0.94f));

            Text hint = PvpUiFactory.CreateText(
                button.transform, "Subtitle", subtitle, 19, TextAnchor.MiddleCenter, FontStyle.Normal);
            hint.color = subtitleColor;
            hint.raycastTarget = false;
            PvpUiFactory.SetAnchors(
                (RectTransform)hint.transform, new Vector2(0.23f, 0.08f), new Vector2(0.94f, 0.48f));

            Rect crop = artworkIndex == 0
                ? new Rect(94f, 326f, 462f, 602f)
                : new Rect(700f, 309f, 457f, 621f);
            CreateAnchoredIcon(
                button.transform,
                "Feature Artwork",
                LoadCroppedSprite(
                    RoomFeatureStripResource, crop, new Vector2(1280f, 1280f)),
                new Vector2(0.015f, 0.02f),
                new Vector2(0.235f, 0.98f),
                Color.white);
        }

        /// <summary>
        /// L'elenco si allunga verso l'alto quando non c'è una stanza propria aperta,
        /// così lo spazio del riquadro "stanza attiva" non resta vuoto.
        /// </summary>
        private void ApplyRoomListLayout()
        {
            float listTop = activeRoomCode == null ? 0.855f : 0.705f;
            PvpUiFactory.SetAnchors(listCaptionRect, new Vector2(0.075f, 0.855f), new Vector2(0.7f, 0.95f));
            PvpUiFactory.SetAnchors(refreshRect, new Vector2(0.73f, 0.855f), new Vector2(0.97f, 0.955f));
            PvpUiFactory.SetAnchors(roomListRect, new Vector2(0.025f, 0.155f), new Vector2(0.975f, listTop));
        }

        private void BuildWaitingPanel()
        {
            CreateWaitingDiceRoll(waitingPanel);

            Text waitingTitle = PvpUiFactory.CreateTitleText(
                waitingPanel, "Waiting Title", "CERCO AVVERSARIO", 34, TextAnchor.MiddleLeft);
            waitingTitle.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)waitingTitle.transform, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.72f));

            Text waitingHint = PvpUiFactory.CreateText(
                waitingPanel, "Waiting Hint",
                "Preparati: il duello parte appena troviamo un avversario del tuo livello.",
                20, TextAnchor.MiddleLeft, FontStyle.Normal);
            waitingHint.color = new Color(0.72f, 0.86f, 0.95f);
            PvpUiFactory.SetAnchors((RectTransform)waitingHint.transform, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.56f));

            Button cancelQueue = PvpUiFactory.CreateButton(
                waitingPanel, "Cancel Queue", "ANNULLA", new Color(0.5f, 0.15f, 0.15f, 0.98f),
                () => callbacks.OnCancelQueue?.Invoke(), 24);
            PvpUiFactory.SetAnchors((RectTransform)cancelQueue.transform, new Vector2(0.34f, 0.1f), new Vector2(0.66f, 0.22f));
        }

        private void BuildCreateDialog()
        {
            RectTransform dialog = PvpUiFactory.CreateSoftPanel(
                createDialog, "Create Room Dialog", new Color(0.03f, 0.05f, 0.075f, 1f));
            PvpUiFactory.SetAnchors(dialog, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.84f));

            Text title = PvpUiFactory.CreateTitleText(dialog, "Title", "CREA STANZA", 30);
            MmoUiTheme.StyleAsScreenTitle(title);
            title.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f));

            Text nameCaption = PvpUiFactory.CreateLabel(dialog, "Name Caption", "NOME DELLA STANZA", 16, TextAnchor.MiddleLeft);
            PvpUiFactory.SetAnchors((RectTransform)nameCaption.transform, new Vector2(0.06f, 0.81f), new Vector2(0.94f, 0.87f));

            createNameField = PvpUiFactory.CreateInputField(
                dialog, "Name Field", "Sfida Leggendaria", RoomModes.NameMaxLength);
            PvpUiFactory.SetAnchors((RectTransform)createNameField.transform, new Vector2(0.06f, 0.71f), new Vector2(0.94f, 0.8f));

            Text modeCaption = PvpUiFactory.CreateLabel(dialog, "Mode Caption", "MODALITA", 16, TextAnchor.MiddleLeft);
            PvpUiFactory.SetAnchors((RectTransform)modeCaption.transform, new Vector2(0.06f, 0.63f), new Vector2(0.94f, 0.69f));

            modeStandardButton = PvpUiFactory.CreateTabButton(
                dialog, "Mode Standard", "STANDARD", () => SetCreateMode(RoomModes.Standard), 22);
            PvpUiFactory.SetAnchors((RectTransform)modeStandardButton.transform, new Vector2(0.06f, 0.53f), new Vector2(0.49f, 0.62f));

            modeHardcoreButton = PvpUiFactory.CreateTabButton(
                dialog, "Mode Hardcore", "HARDCORE", () => SetCreateMode(RoomModes.Hardcore), 22);
            PvpUiFactory.SetAnchors((RectTransform)modeHardcoreButton.transform, new Vector2(0.51f, 0.53f), new Vector2(0.94f, 0.62f));

            modeDescriptionText = PvpUiFactory.CreateText(
                dialog, "Mode Description", string.Empty, 17, TextAnchor.UpperLeft, FontStyle.Normal);
            modeDescriptionText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)modeDescriptionText.transform, new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.52f));

            Text visibilityCaption = PvpUiFactory.CreateLabel(
                dialog, "Visibility Caption", "VISIBILITA", 16, TextAnchor.MiddleLeft);
            PvpUiFactory.SetAnchors((RectTransform)visibilityCaption.transform, new Vector2(0.06f, 0.37f), new Vector2(0.94f, 0.43f));

            visibilityPublicButton = PvpUiFactory.CreateTabButton(
                dialog, "Visibility Public", "PUBBLICA", () => SetCreateVisibility(true), 22);
            PvpUiFactory.SetAnchors((RectTransform)visibilityPublicButton.transform, new Vector2(0.06f, 0.27f), new Vector2(0.49f, 0.36f));

            visibilityPrivateButton = PvpUiFactory.CreateTabButton(
                dialog, "Visibility Private", "SOLO CODICE", () => SetCreateVisibility(false), 22);
            PvpUiFactory.SetAnchors((RectTransform)visibilityPrivateButton.transform, new Vector2(0.51f, 0.27f), new Vector2(0.94f, 0.36f));

            visibilityDescriptionText = PvpUiFactory.CreateText(
                dialog, "Visibility Description", string.Empty, 17, TextAnchor.UpperLeft, FontStyle.Normal);
            visibilityDescriptionText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)visibilityDescriptionText.transform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.26f));

            Button cancel = PvpUiFactory.CreateButton(
                dialog, "Cancel Create", "ANNULLA", new Color(0.5f, 0.15f, 0.15f, 0.98f), CloseCreateDialog, 24);
            PvpUiFactory.SetAnchors((RectTransform)cancel.transform, new Vector2(0.06f, 0.04f), new Vector2(0.49f, 0.15f));

            Button confirm = PvpUiFactory.CreateButton(
                dialog, "Confirm Create", "APRI LA STANZA", new Color(0.08f, 0.38f, 0.32f, 0.98f), ConfirmCreate, 24);
            PvpUiFactory.SetAnchors((RectTransform)confirm.transform, new Vector2(0.51f, 0.04f), new Vector2(0.94f, 0.15f));
            MmoUiTheme.ApplyConfirmButtonStyle(confirm);
        }

        // ---------- Schede ----------

        public void ShowRankedTab()
        {
            SwitchTab(TabRanked);
        }

        private void SwitchTab(string tab)
        {
            bool returningToRanked = currentTab != TabRanked && tab == TabRanked;
            currentTab = tab;
            bool ranked = tab == TabRanked;
            rankedPanel.gameObject.SetActive(ranked);
            roomsPanel.gameObject.SetActive(!ranked);
            PvpUiFactory.SetTabActive(rankedTab, ranked);
            PvpUiFactory.SetTabActive(roomsTab, !ranked);
            if (returningToRanked)
                callbacks.OnRefreshProfile?.Invoke();
            else if (!ranked)
                RequestRooms();
        }

        private void RequestRooms()
        {
            roomRefreshTimer = RoomRefreshSeconds;
            callbacks.OnRefreshRooms?.Invoke();
        }

        private void TickRoomRefresh()
        {
            if (currentTab != TabRooms || waitingForOpponent || createDialog.gameObject.activeSelf)
                return;
            roomRefreshTimer -= Time.unscaledDeltaTime;
            if (roomRefreshTimer <= 0f)
                RequestRooms();
        }

        private void RenderRooms()
        {
            PvpUiFactory.Clear(roomListContent);

            int listed = rooms?.Length ?? 0;
            roomListEmptyText.gameObject.SetActive(listed == 0);
            roomsBadge.gameObject.SetActive(listed > 0);
            roomsBadgeText.text = listed > 9 ? "9+" : listed.ToString();

            if (listed == 0)
                return;

            foreach (RoomSummary room in rooms)
                CreateRoomRow(room);
        }

        private void CreateRoomRow(RoomSummary room)
        {
            bool hardcore = RoomModes.IsHardcore(room.mode);
            RectTransform row = PvpUiFactory.CreateSoftPanel(
                roomListContent, $"Room {room.roomName}",
                hardcore
                    ? new Color(0.026f, 0.014f, 0.012f, 0.95f)
                    : new Color(0.012f, 0.012f, 0.01f, 0.95f));
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 78f;
            element.flexibleWidth = 1f;
            AddCrossedSwords(
                row,
                new Vector2(0.06f, 0.52f),
                new Vector2(28f, 38f),
                hardcore ? new Color(0.86f, 0.48f, 0.38f, 1f) : new Color(0.84f, 0.7f, 0.44f, 1f));

            Text name = PvpUiFactory.CreateTitleText(
                row, "Name", string.IsNullOrWhiteSpace(room.roomName) ? "Stanza" : room.roomName,
                24, TextAnchor.LowerLeft);
            name.color = hardcore ? new Color(1f, 0.62f, 0.5f) : PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)name.transform, new Vector2(0.12f, 0.52f), new Vector2(0.64f, 0.95f));

            Text details = PvpUiFactory.CreateText(
                row, "Details", FormatRoomDetails(room), 18, TextAnchor.UpperLeft, FontStyle.Normal);
            details.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)details.transform, new Vector2(0.12f, 0.08f), new Vector2(0.64f, 0.5f));

            Text seats = PvpUiFactory.CreateValueText(
                row, "Seats", $"{room.players}/{Mathf.Max(room.capacity, room.players)}", 24, TextAnchor.MiddleCenter);
            seats.color = Color.white;
            PvpUiFactory.SetAnchors((RectTransform)seats.transform, new Vector2(0.64f, 0.25f), new Vector2(0.72f, 0.75f));
            CreateAnchoredIcon(
                row,
                "Players",
                Resources.Load<Sprite>(RoomsGroupResource),
                new Vector2(0.715f, 0.27f),
                new Vector2(0.77f, 0.73f),
                Color.white);

            // Le stanze aperte si uniscono con un tocco; per quelle col lucchetto
            // il bottone porta al tastierino, perché il codice va conosciuto.
            string code = room.code;
            UnityAction onAction = room.isPublic
                ? () => callbacks.OnJoinRoom?.Invoke(code)
                : OpenNativeKeyboard;
            Button action = PvpUiFactory.CreateButton(
                row, "Join", room.isPublic ? "ENTRA" : "VEDI",
                room.isPublic
                    ? new Color(0.08f, 0.38f, 0.32f, 0.98f)
                    : new Color(0.42f, 0.28f, 0.06f, 0.98f),
                onAction,
                room.isPublic ? 26 : 21);
            PvpUiFactory.SetAnchors((RectTransform)action.transform, new Vector2(0.79f, 0.18f), new Vector2(0.95f, 0.82f));
            // Con una stanza propria già aperta il server rifiuterebbe l'ingresso.
            action.interactable = activeRoomCode == null
                && (!room.isPublic || !string.IsNullOrEmpty(code));
        }

        private static string FormatRoomDetails(RoomSummary room)
        {
            return $"Modalità: {RoomModes.DisplayName(room.mode)}";
        }

        // ---------- Dialogo di creazione ----------

        private void OpenCreateDialog()
        {
            createDialog.gameObject.SetActive(true);
            SetCreateMode(createMode);
            SetCreateVisibility(createIsPublic);
            createNameField.text = string.Empty;
            createNameField.ActivateInputField();
        }

        private void CloseCreateDialog() => createDialog.gameObject.SetActive(false);

        private void SetCreateMode(string mode)
        {
            createMode = RoomModes.Normalize(mode);
            bool hardcore = RoomModes.IsHardcore(createMode);
            PvpUiFactory.SetTabActive(modeStandardButton, !hardcore);
            PvpUiFactory.SetTabActive(modeHardcoreButton, hardcore);
            modeDescriptionText.text = hardcore
                ? "Regole spietate: una sola vita per carta schierata e turni piu corti."
                : "Regole classiche del duello: due vite per carta e turno pieno.";
        }

        private void SetCreateVisibility(bool isPublic)
        {
            createIsPublic = isPublic;
            PvpUiFactory.SetTabActive(visibilityPublicButton, isPublic);
            PvpUiFactory.SetTabActive(visibilityPrivateButton, !isPublic);
            visibilityDescriptionText.text = isPublic
                ? "Compare nell'elenco e chiunque puo entrare con un tocco."
                : "Compare nell'elenco col lucchetto: entra solo chi ha il codice.";
        }

        private void ConfirmCreate()
        {
            string roomName = createNameField != null ? createNameField.text : string.Empty;
            CloseCreateDialog();
            callbacks.OnCreateRoom?.Invoke(roomName, createMode, createIsPublic);
        }

        // ---------- Stagione ----------

        private void TickCountdown()
        {
            if (seasonSecondsRemaining <= 0f)
                return;
            seasonSecondsRemaining = Mathf.Max(0f, seasonSecondsRemaining - Time.unscaledDeltaTime);
            countdownRedrawTimer -= Time.unscaledDeltaTime;
            if (countdownRedrawTimer <= 0f)
                RedrawCountdown();
        }

        private void RedrawCountdown()
        {
            countdownRedrawTimer = 1f;
            countdownText.text = seasonSecondsRemaining <= 0f
                ? "Fine stagione imminente"
                : $"Termina tra {FormatRemaining((int)seasonSecondsRemaining)}";
        }

        /// <summary>Durata leggibile a due unità: "18g 12h", "12h 30m", "45m 20s".</summary>
        private static string FormatRemaining(int totalSeconds)
        {
            int days = totalSeconds / 86400;
            int hours = totalSeconds % 86400 / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int seconds = totalSeconds % 60;
            if (days > 0)
                return $"{days}g {hours}h";
            if (hours > 0)
                return $"{hours}h {minutes}m";
            return $"{minutes}m {seconds}s";
        }

        private static string FormatLeagueLine(ProfileData profile)
        {
            if (!profile.ranked)
                return "Non classificato - gioca una ranked per entrare in classifica";
            if (profile.placement)
                return $"Piazzamento - {profile.placementRemaining} partite alla lega";
            string rank = profile.globalRank > 0 ? $"  -  #{FormatCount(profile.globalRank)}" : string.Empty;
            return $"{profile.tier} {profile.division}  -  {profile.leaguePoints} LP{rank}";
        }

        /// <summary>
        /// Interi col separatore delle migliaia alla italiana: 1258 -> "1.258".
        /// Raggruppa a mano perché sulle build con globalizzazione invariante
        /// (WebGL in primis) le culture specifiche non sono garantite.
        /// </summary>
        private static string FormatCount(int value)
        {
            string digits = Mathf.Abs(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var builder = new System.Text.StringBuilder(digits.Length + digits.Length / 3);
            if (value < 0)
                builder.Append('-');
            for (int index = 0; index < digits.Length; index++)
            {
                if (index > 0 && (digits.Length - index) % 3 == 0)
                    builder.Append('.');
                builder.Append(digits[index]);
            }
            return builder.ToString();
        }

        private void ApplyDivisionStars(string division, Color accent)
        {
            int filled = ParseDivision(division);
            for (int index = 0; index < divisionStars.Length; index++)
            {
                Image star = divisionStars[index];
                if (star == null)
                    continue;
                bool lit = index < filled;
                star.color = lit
                    ? accent
                    : new Color(accent.r, accent.g, accent.b, 0.16f);
            }
        }

        /// <summary>
        /// Numero di stelle accese: la divisione I è la più alta del tier, quindi
        /// accende tutte le stelle, la IV due. 0 se la lega è nascosta.
        /// </summary>
        private static int ParseDivision(string division)
        {
            switch ((division ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "I": return 5;
                case "II": return 4;
                case "III": return 3;
                case "IV": return 2;
                default: return 0;
            }
        }

        private void ApplyAvatar(string iconId, Color accent)
        {
            if (avatarPortrait == null)
                return;
            Sprite artwork = iconArtwork?.Invoke(iconId) ?? fallbackAvatar;
            avatarPortrait.sprite = artwork;
            avatarPortrait.enabled = artwork != null;
            if (artwork == null)
                return;
            avatarPortrait.color = Color.white;
            var glow = avatarPortrait.transform.parent.Find("Glow")?.GetComponent<Image>();
            if (glow != null)
                glow.color = new Color(accent.r, accent.g, accent.b, 0.32f);
        }

        // ---------- Dadi di attesa ----------

        private void CreateWaitingDiceRoll(Transform parent)
        {
            for (int index = 0; index < waitingDiceImages.Length; index++)
            {
                RectTransform slot = PvpUiFactory.CreateSoftPanel(
                    parent, $"Waiting Dice Slot {index + 1}", new Color(0.02f, 0.06f, 0.075f, 0.84f));
                float left = 0.32f + index * 0.13f;
                PvpUiFactory.SetAnchors(slot, new Vector2(left, 0.76f), new Vector2(left + 0.11f, 0.96f));

                var diceObject = new GameObject("Dice", typeof(RectTransform), typeof(Image));
                diceObject.transform.SetParent(slot, false);
                Image image = diceObject.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)diceObject.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
                waitingDiceImages[index] = image;
            }

            RollWaitingDice();
        }

        private void TickWaitingDice()
        {
            waitingDiceTimer -= Time.unscaledDeltaTime;
            if (waitingDiceTimer <= 0f)
                RollWaitingDice();

            for (int index = 0; index < waitingDiceImages.Length; index++)
            {
                Image image = waitingDiceImages[index];
                if (image == null)
                    continue;

                float direction = index % 2 == 0 ? 1f : -1f;
                image.rectTransform.Rotate(0f, 0f, direction * (38f + index * 9f) * Time.unscaledDeltaTime);
            }
        }

        private void RollWaitingDice()
        {
            waitingDiceTimer = 3f;
            Color[] colors =
            {
                new Color(0.72f, 0.95f, 1f, 1f),
                new Color(1f, 0.82f, 0.25f, 1f),
                new Color(0.76f, 0.55f, 1f, 1f),
                new Color(0.42f, 1f, 0.62f, 1f),
                new Color(1f, 0.42f, 0.36f, 1f),
                new Color(1f, 1f, 1f, 1f)
            };

            for (int index = 0; index < waitingDiceImages.Length; index++)
            {
                int sides = waitingDiceSides[UnityEngine.Random.Range(0, waitingDiceSides.Length)];
                Image image = waitingDiceImages[index];
                if (image != null)
                {
                    image.sprite = RandomWaitingDiceSprite(sides);
                    image.color = colors[UnityEngine.Random.Range(0, colors.Length)];
                    image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-24f, 24f));
                    image.enabled = image.sprite != null;
                }
            }
        }

        private static Sprite RandomWaitingDiceSprite(int sides)
        {
            Sprite player = Resources.Load<Sprite>($"UI/D{sides}_Player");
            Sprite cpu = Resources.Load<Sprite>($"UI/D{sides}_Cpu");
            if (player == null)
                return cpu;
            if (cpu == null)
                return player;
            return UnityEngine.Random.value < 0.5f ? player : cpu;
        }

        // ---------- Codice stanza ----------

        private void OpenNativeKeyboard()
        {
            SwitchTab(TabRooms);
            if (codeEntryPanel != null)
            {
                codeEntryPanel.gameObject.SetActive(true);
                codeEntryPanel.SetAsLastSibling();
            }
            if (!TouchScreenKeyboard.isSupported)
                return;
            nativeKeyboard = TouchScreenKeyboard.Open(
                typedCode,
                TouchScreenKeyboardType.ASCIICapable,
                autocorrection: false,
                multiline: false,
                secure: false,
                alert: false,
                textPlaceholder: "CODICE");
            nativeKeyboard.characterLimit = CodeLength;
        }

        private void SubmitTypedCode()
        {
            if (typedCode.Length != CodeLength)
            {
                SetStatus($"Inserisci un codice stanza di {CodeLength} caratteri.");
                return;
            }

            callbacks.OnJoinRoom?.Invoke(typedCode);
            if (codeEntryPanel != null)
                codeEntryPanel.gameObject.SetActive(false);
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var builder = new System.Text.StringBuilder(CodeLength);
            foreach (char character in raw.ToUpperInvariant())
            {
                if (builder.Length >= CodeLength)
                    break;
                if (CodeAlphabet.IndexOf(character) >= 0)
                    builder.Append(character);
            }
            return builder.ToString();
        }

        private void ApplyPhysicalKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                SubmitTypedCode();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (codeEntryPanel != null)
                    codeEntryPanel.gameObject.SetActive(false);
                return;
            }

            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
                EraseChar();

            TryAppendKey(keyboard.aKey, 'A');
            TryAppendKey(keyboard.bKey, 'B');
            TryAppendKey(keyboard.cKey, 'C');
            TryAppendKey(keyboard.dKey, 'D');
            TryAppendKey(keyboard.eKey, 'E');
            TryAppendKey(keyboard.fKey, 'F');
            TryAppendKey(keyboard.gKey, 'G');
            TryAppendKey(keyboard.hKey, 'H');
            TryAppendKey(keyboard.jKey, 'J');
            TryAppendKey(keyboard.kKey, 'K');
            TryAppendKey(keyboard.mKey, 'M');
            TryAppendKey(keyboard.nKey, 'N');
            TryAppendKey(keyboard.pKey, 'P');
            TryAppendKey(keyboard.qKey, 'Q');
            TryAppendKey(keyboard.rKey, 'R');
            TryAppendKey(keyboard.sKey, 'S');
            TryAppendKey(keyboard.tKey, 'T');
            TryAppendKey(keyboard.uKey, 'U');
            TryAppendKey(keyboard.vKey, 'V');
            TryAppendKey(keyboard.wKey, 'W');
            TryAppendKey(keyboard.xKey, 'X');
            TryAppendKey(keyboard.yKey, 'Y');
            TryAppendKey(keyboard.zKey, 'Z');
            TryAppendKey(keyboard.digit2Key, '2');
            TryAppendKey(keyboard.digit3Key, '3');
            TryAppendKey(keyboard.digit4Key, '4');
            TryAppendKey(keyboard.digit5Key, '5');
            TryAppendKey(keyboard.digit6Key, '6');
            TryAppendKey(keyboard.digit7Key, '7');
            TryAppendKey(keyboard.digit8Key, '8');
            TryAppendKey(keyboard.digit9Key, '9');
            TryAppendKey(keyboard.numpad2Key, '2');
            TryAppendKey(keyboard.numpad3Key, '3');
            TryAppendKey(keyboard.numpad4Key, '4');
            TryAppendKey(keyboard.numpad5Key, '5');
            TryAppendKey(keyboard.numpad6Key, '6');
            TryAppendKey(keyboard.numpad7Key, '7');
            TryAppendKey(keyboard.numpad8Key, '8');
            TryAppendKey(keyboard.numpad9Key, '9');
        }

        private void TryAppendKey(KeyControl key, char character)
        {
            if (key == null || !key.wasPressedThisFrame || typedCode.Length >= CodeLength)
                return;
            if (CodeAlphabet.IndexOf(character) < 0)
                return;
            typedCode += character;
            RefreshTypedCode();
        }

        private void EraseChar()
        {
            if (typedCode.Length == 0)
                return;
            typedCode = typedCode.Substring(0, typedCode.Length - 1);
            RefreshTypedCode();
        }

        private void RefreshTypedCode() =>
            typedCodeText.text = typedCode.PadRight(CodeLength, '_');
    }
}
