using System;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Hub del profilo a schede (Profilo, Icone, Amici, Classifica, Hall of Fame,
    /// Achievement). Non parla direttamente col server: chiede dati e invia azioni
    /// tramite le callback, e riceve i dati dal PvpBootstrap con i metodi Set*.
    /// </summary>
    internal sealed class PvpProfileScreen
    {
        /// <summary>Richieste dati e azioni verso il PvpBootstrap.</summary>
        public sealed class Callbacks
        {
            public Action OnClose;
            public Action OnRequestProfile;
            public Action OnRequestIcons;
            public Action OnRequestFriends;
            public Action OnRequestLeaderboard;
            public Action OnRequestHallOfFameSeasons;
            public Action<int> OnRequestHallOfFame;
            public Action OnRequestAchievements;
            public Action<string> OnSelectIcon;
            public Action<string> OnAddFriend;
            public Action<string, bool> OnRespondFriend;
            public Action<string> OnRemoveFriend;
            public Action<string> OnChallengeFriend;
        }

        private const string TabProfile = "profile";
        private const string TabIcons = "icons";
        private const string TabFriends = "friends";
        private const string TabLeaderboard = "leaderboard";
        private const string TabHallOfFame = "halloffame";
        private const string TabAchievements = "achievements";

        private static readonly Color Gold = new(1f, 0.85f, 0.3f);
        private static readonly Color Dim = new(0.7f, 0.78f, 0.88f);
        private static readonly Color Good = new(0.4f, 1f, 0.55f);
        private static readonly Color Bad = new(1f, 0.5f, 0.45f);

        private readonly Callbacks callbacks;
        private readonly Func<string, Sprite> iconArtwork;
        private readonly RectTransform root;
        private readonly RectTransform content;
        private readonly Text statusText;
        private readonly string myPlayerId;

        private string currentTab = TabProfile;
        private ProfileData profile;
        private IconsData icons;
        private FriendsData friends;
        private LeaderboardData leaderboard;
        private HallOfFameSeasonsData hallOfFameSeasons;
        private HallOfFameData hallOfFame;
        private AchievementsData achievements;

        public PvpProfileScreen(
            Transform parent, string myPlayerId, Callbacks callbacks, Func<string, Sprite> iconArtwork = null)
        {
            this.callbacks = callbacks;
            this.iconArtwork = iconArtwork;
            this.myPlayerId = myPlayerId;

            root = PvpUiFactory.CreatePanel(parent, "Profile", PvpUiFactory.Ink);
            PvpUiFactory.Stretch(root);

            RectTransform titleBand = PvpUiFactory.CreateTitleBand(root, "PROFILO E STAGIONE", "Rank, amici, icone e gloria permanente");
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.05f, 0.92f), new Vector2(0.95f, 0.99f));

            Button close = PvpUiFactory.CreateButton(
                root, "Close", "X", new Color(0.5f, 0.12f, 0.12f, 0.98f), () => callbacks.OnClose?.Invoke(), 26);
            PvpUiFactory.SetAnchors((RectTransform)close.transform, new Vector2(0.93f, 0.925f), new Vector2(0.98f, 0.985f));

            BuildTabs();

            RectTransform scrollPanel = PvpUiFactory.CreateSoftPanel(root, "Scroll", new Color(0.018f, 0.028f, 0.045f, 0.92f));
            PvpUiFactory.SetAnchors(scrollPanel, new Vector2(0.02f, 0.09f), new Vector2(0.98f, 0.82f));
            var scroll = scrollPanel.gameObject.AddComponent<ScrollRect>();
            scrollPanel.gameObject.AddComponent<RectMask2D>();

            var contentHolder = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentHolder.transform.SetParent(scrollPanel, false);
            content = (RectTransform)contentHolder.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = contentHolder.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentHolder.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = scrollPanel;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;

            statusText = PvpUiFactory.CreateText(
                root, "Status", string.Empty, 18, TextAnchor.MiddleCenter, FontStyle.Normal);
            statusText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)statusText.transform, new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.08f));

            SwitchTab(TabProfile);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);
        public void Destroy() => UnityEngine.Object.Destroy(root.gameObject);
        public void SetStatus(string message) => statusText.text = message ?? string.Empty;

        public void SetProfile(ProfileData data) { profile = data; RenderIf(TabProfile); }
        public void SetIcons(IconsData data) { icons = data; RenderIf(TabIcons); }

        /// <summary>Allinea l'icona selezionata dopo un cambio confermato dal server.</summary>
        public void SyncSelectedIcon(string iconId)
        {
            if (icons != null && iconId != null)
            {
                icons.selectedIconId = iconId;
                RenderIf(TabIcons);
            }
        }

        public void SetFriends(FriendsData data) { friends = data; RenderIf(TabFriends); }
        public void SetLeaderboard(LeaderboardData data) { leaderboard = data; RenderIf(TabLeaderboard); }
        public void SetAchievements(AchievementsData data) { achievements = data; RenderIf(TabAchievements); }

        public void SetHallOfFameSeasons(HallOfFameSeasonsData data)
        {
            hallOfFameSeasons = data;
            // Carica automaticamente la stagione più recente disponibile.
            if (data?.seasons != null && data.seasons.Length > 0)
                callbacks.OnRequestHallOfFame?.Invoke(data.seasons[0].seasonId);
            else
                RenderIf(TabHallOfFame);
        }

        public void SetHallOfFame(HallOfFameData data) { hallOfFame = data; RenderIf(TabHallOfFame); }

        public void ApplyPresence(string playerId, string presence)
        {
            if (friends?.friends == null)
                return;
            foreach (FriendDto friend in friends.friends)
                if (friend.playerId == playerId)
                    friend.presence = presence;
            RenderIf(TabFriends);
        }

        private void BuildTabs()
        {
            (string key, string label)[] tabs =
            {
                (TabProfile, "PROFILO"), (TabIcons, "ICONE"), (TabFriends, "AMICI"),
                (TabLeaderboard, "CLASSIFICA"), (TabHallOfFame, "HALL OF FAME"), (TabAchievements, "TRAGUARDI")
            };
            float width = 0.96f / tabs.Length;
            for (int index = 0; index < tabs.Length; index++)
            {
                string key = tabs[index].key;
                float xMin = 0.02f + index * width;
                Button tab = PvpUiFactory.CreateButton(
                    root, $"Tab{key}", tabs[index].label,
                    new Color(0.075f, 0.13f, 0.19f, 0.98f), () => SwitchTab(key), 15);
                PvpUiFactory.SetAnchors(
                    (RectTransform)tab.transform,
                    new Vector2(xMin + 0.004f, 0.835f), new Vector2(xMin + width - 0.004f, 0.905f));
            }
        }

        private void SwitchTab(string tab)
        {
            currentTab = tab;
            PvpUiFactory.Clear(content);
            AddInfoRow("Caricamento...", Dim, 44);
            SetStatus(string.Empty);
            switch (tab)
            {
                case TabProfile: callbacks.OnRequestProfile?.Invoke(); break;
                case TabIcons: callbacks.OnRequestIcons?.Invoke(); break;
                case TabFriends: callbacks.OnRequestFriends?.Invoke(); break;
                case TabLeaderboard: callbacks.OnRequestLeaderboard?.Invoke(); break;
                case TabHallOfFame: callbacks.OnRequestHallOfFameSeasons?.Invoke(); break;
                case TabAchievements: callbacks.OnRequestAchievements?.Invoke(); break;
            }
        }

        private void RenderIf(string tab)
        {
            if (currentTab != tab)
                return;
            PvpUiFactory.Clear(content);
            switch (tab)
            {
                case TabProfile: RenderProfile(); break;
                case TabIcons: RenderIcons(); break;
                case TabFriends: RenderFriends(); break;
                case TabLeaderboard: RenderLeaderboard(); break;
                case TabHallOfFame: RenderHallOfFame(); break;
                case TabAchievements: RenderAchievements(); break;
            }
        }

        // --- Rendering delle schede ---

        private void RenderProfile()
        {
            if (profile == null) { AddInfoRow("Nessun dato profilo.", Dim, 44); return; }

            PvpUiFactory.CreateSectionHeader(content, profile.username, profile.seasonName);
            string rank = !profile.ranked
                ? "Non classificato"
                : profile.placement
                    ? $"In piazzamento ({profile.placementRemaining} partite rimaste)"
                    : $"{profile.tier} {profile.division} — {profile.leaguePoints} LP";
            AddInfoRow($"Rank: {rank}", profile.ranked && !profile.placement ? PvpUiFactory.Gold : Dim, 44, 22);

            int games = profile.wins + profile.losses;
            AddMetricRow(
                ("VITTORIE", profile.wins.ToString(), PvpUiFactory.Good),
                ("SCONFITTE", profile.losses.ToString(), Bad),
                ("WIN RATE", games > 0 ? profile.winRatePercent + "%" : "-", PvpUiFactory.Gold));
            AddMetricRow(
                ("SERIE", profile.currentStreak.ToString(), PvpUiFactory.Arcane),
                ("MIGLIOR SERIE", profile.bestStreak.ToString(), PvpUiFactory.Gold),
                ("ABBANDONI", profile.forfeits.ToString(), Bad));
            AddMetricRow(
                ("ROUND VINTI", profile.roundsWon.ToString(), PvpUiFactory.Good),
                ("ROUND PERSI", profile.roundsLost.ToString(), Bad),
                ("ICONE", $"{profile.iconsUnlocked}/{profile.iconsTotal}", PvpUiFactory.Arcane));
            AddInfoRow($"Icona attuale: {IconName(profile.selectedIconId)}", Dim, 38);
        }

        private void RenderIcons()
        {
            if (icons?.icons == null) { AddInfoRow("Nessuna icona.", Dim, 44); return; }
            PvpUiFactory.CreateSectionHeader(content, "Icone account", "tocca una sbloccata");

            const int perRow = 3;
            RectTransform row = null;
            for (int index = 0; index < icons.icons.Length; index++)
            {
                if (index % perRow == 0)
                    row = AddHorizontalRow(150f);
                IconDto icon = icons.icons[index];
                bool selected = icon.iconId == icons.selectedIconId;
                Color tile = !icon.unlocked
                    ? new Color(0.1f, 0.12f, 0.16f, 0.95f)
                    : selected ? new Color(0.7f, 0.48f, 0.12f, 0.97f) : new Color(0.08f, 0.16f, 0.22f, 0.96f);
                var cell = PvpUiFactory.CreatePanel(row, $"Icon{icon.iconId}", tile);
                AddFlexible(cell);

                Sprite art = icon.unlocked ? iconArtwork?.Invoke(icon.iconId) : null;
                bool hasArt = art != null;
                if (hasArt)
                {
                    var artHolder = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artHolder.transform.SetParent(cell, false);
                    var image = artHolder.GetComponent<Image>();
                    image.sprite = art;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.97f));
                }

                string sub = icon.unlocked
                    ? (selected ? "SELEZIONATA" : SourceLabel(icon.source))
                    : "[bloccata] " + UnlockHint(icon);
                Text label = PvpUiFactory.CreateText(
                    cell, "Label", $"{icon.name}\n{sub}", 15, TextAnchor.MiddleCenter);
                label.color = icon.unlocked ? Color.white : new Color(0.6f, 0.6f, 0.65f);
                label.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)label.transform,
                    hasArt ? new Vector2(0.02f, 0.02f) : new Vector2(0.02f, 0.02f),
                    hasArt ? new Vector2(0.98f, 0.38f) : new Vector2(0.98f, 0.98f));

                if (icon.unlocked && !selected)
                {
                    string captured = icon.iconId;
                    Button button = cell.gameObject.AddComponent<Button>();
                    button.onClick.AddListener(() => callbacks.OnSelectIcon?.Invoke(captured));
                }
            }
        }

        private void RenderFriends()
        {
            PvpUiFactory.CreateSectionHeader(content, "Compagnia", "amici e sfide");
            AddInfoRow("Aggiungi amici dalla Classifica o dalla Hall of Fame.", Dim, 32);
            if (friends?.friends == null || friends.friends.Length == 0)
            {
                AddInfoRow("Nessun amico ancora.", Dim, 44);
                return;
            }

            foreach (FriendDto friend in friends.friends)
            {
                RectTransform row = AddHorizontalRow(64f);
                var info = PvpUiFactory.CreateSoftPanel(row, "Info", new Color(0.06f, 0.1f, 0.15f, 0.94f));
                AddFlexible(info, 2.4f);
                Text text = PvpUiFactory.CreateText(
                    info, "Name", $"{friend.username}   [{StatusLabel(friend.status)}]   {PresenceLabel(friend.presence)}",
                    18, TextAnchor.MiddleLeft, FontStyle.Bold);
                text.color = friend.presence == "online" || friend.presence == "in_match" ? Good : Dim;
                PvpUiFactory.Stretch((RectTransform)text.transform, 12f, 2f);

                string id = friend.playerId;
                if (friend.status == "incoming")
                {
                    AddRowButton(row, "Accetta", new Color(0.1f, 0.5f, 0.25f, 0.98f), () => callbacks.OnRespondFriend?.Invoke(id, true));
                    AddRowButton(row, "Rifiuta", new Color(0.5f, 0.15f, 0.15f, 0.98f), () => callbacks.OnRespondFriend?.Invoke(id, false));
                }
                else if (friend.status == "accepted")
                {
                    AddRowButton(row, "Sfida", new Color(0.28f, 0.12f, 0.45f, 0.98f), () => callbacks.OnChallengeFriend?.Invoke(id));
                    AddRowButton(row, "Rimuovi", new Color(0.4f, 0.2f, 0.15f, 0.98f), () => callbacks.OnRemoveFriend?.Invoke(id));
                }
                else if (friend.status == "requested")
                {
                    AddRowButton(row, "Annulla", new Color(0.4f, 0.2f, 0.15f, 0.98f), () => callbacks.OnRemoveFriend?.Invoke(id));
                }
            }
        }

        private void RenderLeaderboard()
        {
            if (leaderboard?.entries == null) { AddInfoRow("Nessun dato classifica.", Dim, 44); return; }
            AddInfoRow($"Classifica — {leaderboard.seasonName}", Gold, 40);
            if (leaderboard.entries.Length == 0) { AddInfoRow("Nessun giocatore classificato.", Dim, 44); return; }

            foreach (LeaderboardEntry entry in leaderboard.entries)
            {
                RectTransform row = AddHorizontalRow(56f);
                var info = PvpUiFactory.CreatePanel(row, "Info", new Color(0.1f, 0.15f, 0.22f, 0.9f));
                AddFlexible(info, 3f);
                string rank = entry.placement ? "Piazzamento" : $"{entry.tier} {entry.division} - {entry.leaguePoints} LP";
                Text text = PvpUiFactory.CreateText(
                    info, "Row", $"#{entry.rank}  {entry.username}    {rank}", 18, TextAnchor.MiddleLeft);
                text.color = entry.playerId == myPlayerId ? Gold : Color.white;
                PvpUiFactory.Stretch((RectTransform)text.transform, 12f, 2f);
                AddAddFriendButton(row, entry.playerId, entry.username);
            }
        }

        private void RenderHallOfFame()
        {
            if (hallOfFameSeasons?.seasons == null || hallOfFameSeasons.seasons.Length == 0)
            {
                AddInfoRow("Nessuna stagione conclusa: la Hall of Fame è vuota.", Dim, 44);
                return;
            }

            RectTransform seasonRow = AddHorizontalRow(50f);
            foreach (HallOfFameSeasonDto season in hallOfFameSeasons.seasons)
            {
                int id = season.seasonId;
                bool active = hallOfFame != null && hallOfFame.seasonId == id;
                AddRowButton(
                    seasonRow, season.name,
                    active ? new Color(0.75f, 0.55f, 0.1f, 0.97f) : new Color(0.12f, 0.2f, 0.28f, 0.96f),
                    () => callbacks.OnRequestHallOfFame?.Invoke(id));
            }

            if (hallOfFame?.entries == null) { AddInfoRow("Seleziona una stagione.", Dim, 40); return; }
            AddInfoRow($"Hall of Fame — {hallOfFame.seasonName}", Gold, 38);
            foreach (HallOfFameEntry entry in hallOfFame.entries)
                AddHallOfFameRow(entry);
            if (hallOfFame.you != null)
            {
                AddInfoRow("Il tuo piazzamento:", Dim, 32);
                AddHallOfFameRow(hallOfFame.you);
            }
        }

        private void AddHallOfFameRow(HallOfFameEntry entry)
        {
            RectTransform row = AddHorizontalRow(56f);
            var info = PvpUiFactory.CreatePanel(row, "Info", new Color(0.1f, 0.15f, 0.22f, 0.9f));
            AddFlexible(info, 3f);
            Text text = PvpUiFactory.CreateText(
                info, "Row",
                $"#{entry.rank}  {entry.username}    {entry.tier} {entry.division}   {entry.wins}V/{entry.losses}S",
                18, TextAnchor.MiddleLeft);
            text.color = entry.playerId == myPlayerId ? Gold : Color.white;
            PvpUiFactory.Stretch((RectTransform)text.transform, 12f, 2f);
            AddAddFriendButton(row, entry.playerId, entry.username);
        }

        private void RenderAchievements()
        {
            if (achievements?.achievements == null) { AddInfoRow("Nessun traguardo.", Dim, 44); return; }
            foreach (AchievementDto ach in achievements.achievements)
            {
                RectTransform row = AddHorizontalRow(70f);
                var info = PvpUiFactory.CreatePanel(
                    row, "Info",
                    ach.unlocked ? new Color(0.12f, 0.28f, 0.16f, 0.95f) : new Color(0.12f, 0.15f, 0.2f, 0.92f));
                AddFlexible(info);
                string mark = ach.unlocked ? "[OK] " : $"{ach.progress}/{ach.threshold}  ";
                Text text = PvpUiFactory.CreateText(
                    info, "Row", $"{mark}{ach.name}\n{ach.description}", 16, TextAnchor.MiddleLeft);
                text.color = ach.unlocked ? Good : Color.white;
                PvpUiFactory.Stretch((RectTransform)text.transform, 12f, 4f);
            }
        }

        // --- Helper di layout ---

        private void AddMetricRow(
            (string label, string value, Color color) first,
            (string label, string value, Color color) second,
            (string label, string value, Color color) third)
        {
            RectTransform row = AddHorizontalRow(86f);
            AddMetricCard(row, first.label, first.value, first.color);
            AddMetricCard(row, second.label, second.value, second.color);
            AddMetricCard(row, third.label, third.value, third.color);
        }

        private void AddMetricCard(RectTransform row, string label, string value, Color color)
        {
            RectTransform card = PvpUiFactory.CreateSoftPanel(row, label, new Color(0.045f, 0.075f, 0.11f, 0.94f));
            AddFlexible(card);
            Text valueText = PvpUiFactory.CreateText(card, "Value", value, 28, TextAnchor.MiddleCenter);
            valueText.color = color;
            PvpUiFactory.SetAnchors((RectTransform)valueText.transform, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.94f));
            Text labelText = PvpUiFactory.CreateLabel(card, "Label", label, 13, TextAnchor.MiddleCenter);
            PvpUiFactory.SetAnchors((RectTransform)labelText.transform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.36f));
        }

        private void AddAddFriendButton(RectTransform row, string playerId, string username)
        {
            if (playerId == myPlayerId)
                return;
            AddRowButton(row, "Aggiungi", new Color(0.12f, 0.4f, 0.45f, 0.98f), () => callbacks.OnAddFriend?.Invoke(username));
        }

        private RectTransform AddHorizontalRow(float height)
        {
            var holder = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            holder.transform.SetParent(content, false);
            var layout = holder.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            holder.GetComponent<LayoutElement>().preferredHeight = height;
            return (RectTransform)holder.transform;
        }

        private void AddRowButton(RectTransform row, string label, Color color, Action onClick)
        {
            Button button = PvpUiFactory.CreateButton(row, label, label, color, () => onClick?.Invoke(), 16);
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 150f;
            element.flexibleWidth = 0f;
        }

        private void AddFlexible(RectTransform target, float weight = 1f)
        {
            var element = target.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = weight;
        }

        private void AddInfoRow(string message, Color color, float height, int fontSize = 22)
        {
            RectTransform row = AddHorizontalRow(height);
            Text text = PvpUiFactory.CreateText(row, "Info", message, fontSize, TextAnchor.MiddleLeft);
            text.color = color;
            // Figlio diretto del layout group: la larghezza/altezza le gestisce il layout.
            AddFlexible((RectTransform)text.transform);
        }

        // --- Etichette ---

        private string IconName(string iconId)
        {
            if (icons?.icons != null)
                foreach (IconDto icon in icons.icons)
                    if (icon.iconId == iconId)
                        return icon.name;
            return iconId;
        }

        private static string SourceLabel(string source) => source switch
        {
            "free" => "Classe",
            "tier" => "Tier",
            "campaign" => "Campagna",
            "halloffame" => "Hall of Fame",
            "achievement" => "Traguardo",
            _ => source
        };

        private static string UnlockHint(IconDto icon) => icon.source switch
        {
            "tier" => $"Raggiungi {icon.name}",
            "campaign" => $"Sconfiggi {icon.name} in campagna",
            "halloffame" => "Piazzati a fine stagione",
            "achievement" => "Sblocca il traguardo",
            _ => "Bloccata"
        };

        private static string StatusLabel(string status) => status switch
        {
            "accepted" => "amico",
            "incoming" => "richiesta ricevuta",
            "requested" => "richiesta inviata",
            _ => status
        };

        private static string PresenceLabel(string presence) => presence switch
        {
            "online" => "Online",
            "in_match" => "In partita",
            _ => "Offline"
        };
    }
}
