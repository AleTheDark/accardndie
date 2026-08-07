using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccardND.Localization;
using AccardND.NetProtocol;
using AccardND.Network;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private enum ProfilePage
	{
		Overview,
		Statistics,
		Achievements,
		Messages
	}

	private GameObject profilePanel;
	private RectTransform profileIdentityRoot;
	private RectTransform profileContentRoot;
	private readonly List<GameObject> profileDynamicObjects = new();
	private readonly List<Button> profileTabs = new();
	private ProfileData profileData;
	private StatsData profileStats;
	private AchievementsData profileAchievements;
	private ProfilePage profilePage;
	private bool profileLoading;
	private bool profileLifetimeStats;

	/// <summary>
	/// Le ricompense gia' guadagnate su cui il triplicatore pubblicitario e' ancora in piedi.
	/// Le manda il server: sono le run finite senza che il video sia partito, quasi sempre
	/// perche' la connessione e' caduta proprio alla fine.
	/// </summary>
	private SinglePlayerPendingAdRewardData[] profilePendingRewards = Array.Empty<SinglePlayerPendingAdRewardData>();
	private bool profilePendingLoading;
	private bool profilePendingClaiming;
	private string profileMessagesNotice;
	private GameObject profileMessagesBadge;
	private Text profileMessagesBadgeText;
	private GameObject profileHubBadge;
	private Text profileHubBadgeText;

	private static readonly Color ProfileGold = new(0.95f, 0.79f, 0.34f);
	private static readonly Color ProfileBody = new(0.84f, 0.88f, 0.91f);
	private static readonly Color ProfileGood = new(0.44f, 0.86f, 0.55f);
	private static readonly Color ProfileBad = new(0.92f, 0.42f, 0.38f);

	private void CreateProfileView(Font fallbackFont)
	{
		Image root = CreateImage("Profile", canvasRect, new Color(0.006f, 0.008f, 0.012f, 1f));
		root.raycastTarget = true;
		profilePanel = root.gameObject;
		SetRect(root.rectTransform, Vector2.zero, Vector2.one);
		Canvas canvas = root.gameObject.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 900;
		root.gameObject.AddComponent<GraphicRaycaster>();

		Image backdrop = CreateImage("Profile Backdrop", root.transform, Color.white);
		backdrop.sprite = LoadSpriteResource("UI/Shop/shop_background");
		backdrop.type = Image.Type.Simple;
		backdrop.preserveAspect = true;
		SetRect(backdrop.rectTransform, Vector2.zero, Vector2.one);
		if ((Object)(object)backdrop.sprite != (Object)null)
		{
			AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
			fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			fitter.aspectRatio = backdrop.sprite.rect.width / Mathf.Max(1f, backdrop.sprite.rect.height);
		}

		Image veil = CreateImage("Profile Veil", root.transform, new Color(0f, 0f, 0.01f, 0.22f));
		SetRect(veil.rectTransform, new Vector2(0.018f, 0.018f), new Vector2(0.982f, 0.785f));

		Image frameFill = CreateImage("Profile Rock Fill", root.transform, Color.white);
		frameFill.sprite = LoadSpriteResource("UI/Common/merchant_rock_panel_aaa");
		frameFill.type = Image.Type.Sliced;
		SetRect(frameFill.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));
		frameFill.rectTransform.offsetMax = new Vector2(0f, -22.0389f);

		Image frame = CreateImage("Profile Outer Frame", root.transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(frame);
		SetRect(frame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));

		Image titlePlaque = CreateImage("Profile Title Plaque", root.transform, Color.white);
		titlePlaque.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		titlePlaque.type = Image.Type.Sliced;
		SetRect(titlePlaque.rectTransform, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.9f));

		Text title = CreateText("Profile Title", titlePlaque.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont, 48, FontStyle.Normal, TextAnchor.MiddleCenter);
		title.text = "PROFILO";
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		title.color = ProfileGold;
		SetRect(title.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		title.rectTransform.offsetMin = new Vector2(0f, -23f);
		title.rectTransform.offsetMax = new Vector2(0f, -23f);

		profileIdentityRoot = CreateProfileSection(
			root.transform, "AVVENTURIERO",
			new Vector2(0.055f, 0.515f), new Vector2(0.945f, 0.715f));

		string[] tabNames =
		{
			GameText.GetOrFallbackSilent(GameTextKeys.Profile.TabOverview, "PANORAMICA"),
			GameText.GetOrFallbackSilent(GameTextKeys.Profile.TabStatistics, "STATISTICHE"),
			GameText.GetOrFallbackSilent(GameTextKeys.Profile.TabAchievements, "TRAGUARDI"),
			GameText.GetOrFallbackSilent(GameTextKeys.Profile.TabMessages, "MESSAGGI")
		};
		for (int index = 0; index < tabNames.Length; index++)
		{
			int captured = index;
			Button tab = CreateButton("Profile Tab " + tabNames[index], root.transform, fallbackFont, tabNames[index]);
			tab.onClick.AddListener((UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				SelectProfilePage((ProfilePage)captured);
			});
			float gap = 0.012f;
			float width = (0.89f - gap * (tabNames.Length - 1)) / tabNames.Length;
			float left = 0.055f + captured * (width + gap);
			SetRect((RectTransform)tab.transform, new Vector2(left, 0.447f), new Vector2(left + width, 0.505f));
			profileTabs.Add(tab);
		}
		CreateProfileMessagesBadge(profileTabs[(int)ProfilePage.Messages], fallbackFont);

		Image contentPanel = CreateImage("Profile Content Panel", root.transform, Color.white);
		contentPanel.sprite = LoadSpriteResource("UI/Common/merchant_rock_panel_aaa");
		contentPanel.type = Image.Type.Sliced;
		SetRect(contentPanel.rectTransform, new Vector2(0.055f, 0.055f), new Vector2(0.945f, 0.432f));
		Image content = CreateImage("Profile Content", contentPanel.transform, Color.clear);
		SetRect(content.rectTransform, new Vector2(0.035f, 0.075f), new Vector2(0.965f, 0.925f));
		profileContentRoot = content.rectTransform;

		profilePanel.SetActive(false);
	}

	private RectTransform CreateProfileSection(
		Transform parent, string heading, Vector2 minimum, Vector2 maximum)
	{
		Image panel = CreateImage("Profile " + heading, parent, Color.white);
		panel.sprite = LoadSpriteResource("UI/Common/merchant_offer_rock_panel_aaa");
		panel.type = Image.Type.Sliced;
		SetRect(panel.rectTransform, minimum, maximum);
		Image content = CreateImage(heading + " Content", panel.transform, Color.clear);
		SetRect(content.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.80f));
		return content.rectTransform;
	}

	private void ShowProfile()
	{
		if ((Object)(object)profilePanel == (Object)null)
			return;
		if ((Object)(object)modeSelectionPanel != (Object)null)
			modeSelectionPanel.SetActive(false);
		SetAccountHubHudActive(true);
		EnsureSanctuarySharedHudSorting();
		profilePanel.SetActive(true);
		profilePanel.transform.SetAsLastSibling();
		RefreshAccountBannerView();
		profilePage = ProfilePage.Overview;
		profileMessagesNotice = null;
		// L'annuncio si chiede all'apertura del profilo, non al tocco su TRIPLICA: fra le due
		// cose passano i secondi che servono alla rete per rispondere, e chi apre i messaggi
		// quasi sempre ha qualcosa da riscuotere.
		AccardND.Ads.AdService.Warm(AccardND.Ads.AdPlacement.CampaignExperienceTriple);
		RefreshProfile();
		LoadProfileFromServer();
		_ = LoadPendingAdRewardsAsync();
	}

	/// <summary>
	/// Il profilo si chiude: quello che e' gia' caricato resta buono per la prossima apertura,
	/// ma nessuno ne chiede altri finche' il giocatore non torna qui (o non comincia una run).
	/// </summary>
	private void CoolProfileAds()
	{
		AccardND.Ads.AdService.Cool(AccardND.Ads.AdPlacement.CampaignExperienceTriple);
	}

	private async void LoadProfileFromServer()
	{
		if (profileLoading)
			return;
		profileLoading = true;
		try
		{
			if (!AccountServerSession.TryGet(out _, out PvpServerMessageDispatcher dispatcher, out _))
				throw new InvalidOperationException("serve una connessione al server.");

			Task<Envelope> profileRequest = dispatcher.RequestAsync(
				MessageTypes.ProfileGet, null, MessageTypes.ProfileData, 8f);
			Task<Envelope> statsRequest = dispatcher.RequestAsync(
				MessageTypes.StatsGet, null, MessageTypes.StatsData, 8f);
			Task<Envelope> achievementsRequest = dispatcher.RequestAsync(
				MessageTypes.AchievementsGet, null, MessageTypes.AchievementsData, 8f);
			await Task.WhenAll(profileRequest, statsRequest, achievementsRequest);

			profileData = PvpServerClient.ParsePayload<ProfileData>(profileRequest.Result);
			profileStats = PvpServerClient.ParsePayload<StatsData>(statsRequest.Result);
			profileAchievements = PvpServerClient.ParsePayload<AchievementsData>(achievementsRequest.Result);
		}
		catch (Exception exception)
		{
			AppendLog("PROFILO - caricamento fallito: " + exception.Message);
		}
		finally
		{
			profileLoading = false;
			RefreshProfile();
		}
	}

	private void SelectProfilePage(ProfilePage page)
	{
		// L'esito dell'ultima riscossione vale finche' si guarda la posta: ritrovarlo tornando
		// dai traguardi farebbe sembrare appena successo qualcosa che e' gia' stato letto.
		if (page != ProfilePage.Messages)
			profileMessagesNotice = null;
		profilePage = page;
		RefreshProfile();
	}

	private void RefreshProfile()
	{
		ClearProfileDynamicObjects();
		RefreshProfileTabs();
		RenderProfileIdentity();
		if (profileLoading && profileData == null)
		{
			CreateProfileMessage(
				GameText.GetOrFallbackSilent(GameTextKeys.Profile.Loading, "CARICAMENTO..."),
				ProfileBody);
			return;
		}
		switch (profilePage)
		{
			case ProfilePage.Overview:
				RenderProfileOverview();
				break;
			case ProfilePage.Statistics:
				RenderProfileStatistics();
				break;
			case ProfilePage.Achievements:
				RenderProfileAchievements();
				break;
			case ProfilePage.Messages:
				RenderProfileMessages();
				break;
		}
	}

	/// <summary>
	/// Chiede al server quali triplicatori sono ancora in piedi. Non e' una richiesta che puo'
	/// far fallire l'apertura del profilo: se il server non risponde, la pagina messaggi lo
	/// dice e il resto del profilo resta quello che e'.
	/// </summary>
	private async Task LoadPendingAdRewardsAsync()
	{
		if (profilePendingLoading || profilePendingClaiming)
			return;

		profilePendingLoading = true;
		try
		{
			if (!await EnsureServerProgressAsync())
				throw new InvalidOperationException("server non disponibile.");

			SinglePlayerPendingAdRewardsData data = await serverProgress.GetPendingAdRewardsAsync();
			profilePendingRewards = data?.rewards ?? Array.Empty<SinglePlayerPendingAdRewardData>();
		}
		catch (Exception exception)
		{
			profilePendingRewards = Array.Empty<SinglePlayerPendingAdRewardData>();
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Profile.PendingLoadFailedLog,
				"PROFILO - ricompense in sospeso non caricate: {0}",
				exception.Message));
		}
		finally
		{
			profilePendingLoading = false;
			UpdateProfileMessagesBadges();
			if ((Object)(object)profilePanel != (Object)null && profilePanel.activeSelf)
				RefreshProfile();
		}
	}

	/// <summary>
	/// La posta del giocatore: una riga per ogni ricompensa che ha ancora il x3 da riscuotere.
	/// E' la seconda occasione di un'offerta che a fine run puo' essere saltata senza che
	/// nessuno l'abbia scelto - la rete che cade mentre il popup e' a schermo, l'annuncio che
	/// non arriva in tempo - e che altrimenti sarebbe persa per sempre.
	/// </summary>
	private void RenderProfileMessages()
	{
		if (!string.IsNullOrEmpty(profileMessagesNotice))
		{
			CreateProfileText(profileContentRoot, "Profile Messages Notice", profileMessagesNotice, 24,
				TextAnchor.MiddleCenter, ProfileGold, new Vector2(0f, 0.88f), new Vector2(1f, 1f));
		}
		float listTop = string.IsNullOrEmpty(profileMessagesNotice) ? 1f : 0.86f;

		if (profilePendingRewards.Length == 0)
		{
			CreateProfileText(profileContentRoot, "Profile Messages Empty",
				profilePendingLoading
					? GameText.GetOrFallbackSilent(GameTextKeys.Profile.Loading, "CARICAMENTO...")
					: GameText.GetOrFallbackSilent(GameTextKeys.Profile.NoMessages, "NESSUNA COMUNICAZIONE"),
				24, TextAnchor.MiddleCenter, ProfileBody, Vector2.zero, new Vector2(1f, listTop));
			return;
		}

		RectTransform content = CreateProfileScrollList("Profile Messages", listTop);
		for (int index = 0; index < profilePendingRewards.Length; index++)
		{
			SinglePlayerPendingAdRewardData reward = profilePendingRewards[index];
			if (reward == null)
				continue;

			Image card = CreateImage("Profile Message " + reward.claimId, content,
				new Color(0.09f, 0.07f, 0.03f, 0.98f));
			card.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
			card.type = Image.Type.Sliced;
			LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
			cardLayout.preferredHeight = 190f;
			cardLayout.flexibleHeight = 0f;
			profileDynamicObjects.Add(card.gameObject);

			Text heading = CreateProfileText(card.rectTransform, "Heading",
				ProfileMessageHeading(reward), 28, TextAnchor.MiddleLeft, ProfileGold,
				new Vector2(0.035f, 0.60f), new Vector2(0.70f, 0.94f));
			heading.resizeTextForBestFit = false;

			Text expiry = CreateProfileText(card.rectTransform, "Expiry",
				reward.hoursLeft > 0
					? GameText.GetOrFallbackSilent(
						GameTextKeys.Profile.ExpiresHours,
						"SCADE FRA {0} H",
						reward.hoursLeft)
					: string.Empty,
				22, TextAnchor.MiddleRight, ProfileBody,
				new Vector2(0.62f, 0.60f), new Vector2(0.965f, 0.94f));
			expiry.resizeTextForBestFit = false;

			Text body = CreateProfileText(card.rectTransform, "Body",
				GameText.GetOrFallbackSilent(
					GameTextKeys.Profile.PendingRewardBody,
					"Hai guadagnato +{0} EXP, ma il triplicatore non e' stato riscosso.\nGuarda un video e ricevi altri +{1} EXP.",
					reward.baseAccountExperience,
					reward.extraAccountExperience),
				24, TextAnchor.UpperLeft, ProfileBody,
				new Vector2(0.035f, 0.08f), new Vector2(0.64f, 0.58f));
			body.resizeTextForBestFit = false;
			body.horizontalOverflow = HorizontalWrapMode.Wrap;
			body.verticalOverflow = VerticalWrapMode.Truncate;

			Button claim = CreateButton("Profile Message Claim " + reward.claimId, card.rectTransform,
				AccardND.Battlefield.MmoUiTheme.BodyFont,
				GameText.GetOrFallbackSilent(GameTextKeys.Profile.Triple, "TRIPLICA"));
			Text claimLabel = claim.GetComponentInChildren<Text>();
			if ((Object)(object)claimLabel != (Object)null)
			{
				claimLabel.fontSize = 26;
				claimLabel.resizeTextForBestFit = false;
			}
			SetRect((RectTransform)claim.transform, new Vector2(0.67f, 0.10f), new Vector2(0.965f, 0.52f));
			ApplyMerchantRoomCta(claim, claimLabel, "UI/CampaignRestyle/campaign_cta_blue", preserveAspect: false);
			claim.interactable = !profilePendingClaiming;
			SinglePlayerPendingAdRewardData captured = reward;
			claim.onClick.AddListener((UnityAction)delegate
			{
				PlayGenericButtonClickSfx();
				ClaimPendingAdReward(captured);
			});
			profileDynamicObjects.Add(claim.gameObject);
		}
	}

	private static string ProfileMessageHeading(SinglePlayerPendingAdRewardData reward)
	{
		if (!string.Equals(reward.rewardType, "death", StringComparison.Ordinal))
			return GameText.GetOrFallbackSilent(GameTextKeys.Profile.PendingReward, "RICOMPENSA IN SOSPESO");
		string chapter = string.IsNullOrWhiteSpace(reward.chapterId)
			? GameText.GetOrFallbackSilent(GameTextKeys.Profile.Campaign, "CAMPAGNA")
			: AdventureChapterDisplayName(reward.chapterId).ToUpperInvariant();
		return reward.roomsCleared > 0
			? GameText.GetOrFallbackSilent(
				GameTextKeys.Profile.CampaignEndRooms,
				"FINE {0} · {1} STANZE",
				chapter,
				reward.roomsCleared)
			: GameText.GetOrFallbackSilent(GameTextKeys.Profile.CampaignEnd, "FINE {0}", chapter);
	}

	/// <summary>
	/// Riscuote il x3 rimasto in sospeso. Il video qui e' un cancello, non un accompagnamento:
	/// il giocatore ha premuto sapendo cosa arriva, quindi si aspetta il caricamento invece di
	/// rispondere subito di no. Il claim viaggia fino alla rete pubblicitaria e torna nella
	/// verifica lato server, esattamente come a fine run.
	/// </summary>
	private async void ClaimPendingAdReward(SinglePlayerPendingAdRewardData reward)
	{
		if (profilePendingClaiming || reward == null || string.IsNullOrWhiteSpace(reward.claimId))
			return;

		profilePendingClaiming = true;
		profileMessagesNotice = AccardND.Ads.AdService.RewardsWaivedWithoutAds
			? GameText.GetOrFallbackSilent(GameTextKeys.Profile.Claiming, "Un attimo: sto riscuotendo...")
			: GameText.GetOrFallbackSilent(GameTextKeys.Profile.LoadingAd, "Un attimo: sto caricando la pubblicità...");
		RefreshProfile();
		try
		{
			if (!ServerProgressReady && !await EnsureServerProgressAsync())
			{
				profileMessagesNotice = GameText.GetOrFallbackSilent(
					GameTextKeys.Profile.ConnectionRequired,
					"Connessione al server necessaria: la ricompensa resta qui.");
				return;
			}

			AccardND.Ads.AdResult ad = await AccardND.Ads.AdService.ShowAsync(
				AccardND.Ads.AdPlacement.CampaignExperienceTriple,
				AccardND.Ads.AdRewardContext.ForClaim(reward.claimId),
				asGate: true);
			if (!ad.Grants)
			{
				profileMessagesNotice = ad.Unavailable
					? GameText.GetOrFallbackSilent(
						GameTextKeys.Profile.AdUnavailable,
						"Nessuna pubblicità disponibile: la ricompensa resta qui, riprova più tardi.")
					: GameText.GetOrFallbackSilent(
						GameTextKeys.Profile.AdIncomplete,
						"Il video va guardato per intero per triplicare l'EXP.");
				AppendLog(GameText.GetOrFallbackSilent(
					GameTextKeys.Profile.TripleNotAppliedLog,
					"PROFILO - triplicatore in sospeso non applicato: annuncio {0}.",
					ad.Outcome));
				return;
			}

			AccardND.Network.SinglePlayerRewardOutcome outcome =
				await serverProgress.ClaimAdMultiplierAsync(reward.claimId, ad.ImpressionId);
			MirrorServerProgress();
			RefreshSinglePlayerProgressView();
			RefreshAccountBannerView();
			profileMessagesNotice = GameText.GetOrFallbackSilent(
				GameTextKeys.Profile.TripleApplied,
				"+{0} EXP account: ricompensa triplicata.",
				outcome.GrantedAccountExperience);
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Profile.TripleRecoveredLog,
				"PROFILO - triplicatore recuperato: +{0} EXP account.",
				outcome.GrantedAccountExperience));
		}
		catch (Exception exception)
		{
			profileMessagesNotice = GameText.GetOrFallbackSilent(
				GameTextKeys.Profile.ConnectionRequired,
				"Connessione al server necessaria: la ricompensa resta qui.");
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Profile.TripleRejectedLog,
				"PROFILO - triplicatore in sospeso rifiutato dal server: {0}",
				exception.Message));
		}
		finally
		{
			profilePendingClaiming = false;
			RefreshProfile();
			// La lista la riscrive il server: e' lui a sapere quali claim restano da moltiplicare,
			// e cancellare la riga qui vorrebbe dire fidarsi di un esito che potrebbe non essere
			// arrivato fino in fondo.
			await LoadPendingAdRewardsAsync();
		}
	}

	/// <summary>
	/// Lista scorrevole a tutta pagina del profilo. La usano i traguardi e i messaggi: senza,
	/// la seconda pagina a elenco riscriverebbe le stesse trenta righe di ScrollRect.
	/// </summary>
	private RectTransform CreateProfileScrollList(string name, float topLimit = 1f)
	{
		GameObject scrollObject = new(name + " Scroll",
			typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
		scrollObject.transform.SetParent(profileContentRoot, false);
		RectTransform viewport = (RectTransform)scrollObject.transform;
		SetRect(viewport, Vector2.zero, new Vector2(1f, topLimit));
		Image viewportImage = scrollObject.GetComponent<Image>();
		viewportImage.color = new Color(0f, 0f, 0f, 0.12f);
		viewportImage.raycastTarget = true;
		scrollObject.GetComponent<Mask>().showMaskGraphic = true;
		profileDynamicObjects.Add(scrollObject);

		GameObject contentObject = new(name + " Content",
			typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		contentObject.transform.SetParent(scrollObject.transform, false);
		RectTransform content = (RectTransform)contentObject.transform;
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = new Vector2(10f, 0f);
		content.offsetMax = new Vector2(-10f, 0f);

		VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 12f;
		layout.padding = new RectOffset(8, 8, 10, 10);
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		ScrollRect scrolling = scrollObject.GetComponent<ScrollRect>();
		scrolling.content = content;
		scrolling.viewport = viewport;
		scrolling.horizontal = false;
		scrolling.vertical = true;
		scrolling.movementType = ScrollRect.MovementType.Clamped;
		scrolling.scrollSensitivity = 45f;
		return content;
	}

	private void RefreshProfileTabs()
	{
		for (int index = 0; index < profileTabs.Count; index++)
		{
			Button button = profileTabs[index];
			Image image = button.GetComponent<Image>();
			if ((Object)(object)image != (Object)null)
			{
				image.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
				image.type = Image.Type.Sliced;
				image.color = index == (int)profilePage
					? new Color(0.68f, 0.45f, 0.10f, 1f)
					: new Color(0.12f, 0.15f, 0.20f, 0.98f);
			}
			Text label = button.GetComponentInChildren<Text>();
			if ((Object)(object)label != (Object)null)
			{
				AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
				// Quattro tab su una riga: la scritta piu' lunga ("STATISTICHE") deve starci
				// dentro senza essere tagliata a meta'.
				label.fontSize = 24;
				label.color = index == (int)profilePage ? Color.white : ProfileBody;
			}
		}
	}

	private void RenderProfileIdentity()
	{
		if ((Object)(object)profileIdentityRoot == (Object)null)
			return;
		if (profileData == null)
		{
			CreateProfileText(profileIdentityRoot, "Identity Loading", "DATI PROFILO NON DISPONIBILI",
				22, TextAnchor.MiddleCenter, ProfileBody, Vector2.zero, Vector2.one);
			return;
		}

		Sprite emblemSprite = profileData.ranked && !profileData.placement
			? ProfileRankEmblem(profileData.tier)
			: LoadSpriteResource("UI/info_button");
		Image emblem = CreateImage("Profile Rank Emblem", profileIdentityRoot, Color.white);
		emblem.sprite = emblemSprite;
		emblem.preserveAspect = true;
		SetRect(emblem.rectTransform, new Vector2(0.015f, 0.04f), new Vector2(0.19f, 0.96f));
		profileDynamicObjects.Add(emblem.gameObject);

		string rank = !profileData.ranked
			? "NON CLASSIFICATO"
			: profileData.placement
				? $"PIAZZAMENTO · {profileData.placementRemaining} PARTITE"
				: $"{profileData.tier} {profileData.division} · {profileData.leaguePoints} LP";
		Text username = CreateProfileText(profileIdentityRoot, "Profile Username",
			(profileData.username ?? "AVVENTURIERO").ToUpperInvariant(),
			32, TextAnchor.MiddleLeft, ProfileGold, new Vector2(0.22f, 0.48f), new Vector2(0.72f, 0.96f));
		username.font = Resources.Load<Font>("Fonts/IMFellEnglishSC")
			?? AccardND.Battlefield.MmoUiTheme.BodyFont;
		CreateProfileText(profileIdentityRoot, "Profile Rank", rank,
			30, TextAnchor.MiddleLeft, ProfileBody, new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.51f));
		CreateProfileText(profileIdentityRoot, "Profile Season",
			$"{profileData.seasonName}\n{(profileData.globalRank > 0 ? "#" + profileData.globalRank.ToString("N0") + " GLOBALE" : "SENZA POSIZIONE")}",
			28, TextAnchor.MiddleRight, ProfileBody, new Vector2(0.72f, 0.12f), new Vector2(0.985f, 0.90f));
	}

	private void RenderProfileOverview()
	{
		PlayerStatsDto stats = profileStats?.lifetime;
		if (stats == null)
		{
			CreateProfileMessage("NESSUNA STATISTICA DISPONIBILE", ProfileBody);
			return;
		}
		int completed = 0;
		int total = profileAchievements?.achievements?.Length ?? 0;
		if (profileAchievements?.achievements != null)
			foreach (AchievementDto achievement in profileAchievements.achievements)
				if (achievement.unlocked)
					completed++;

		CreateProfileMetricGrid(new[]
		{
			("PARTITE", stats.matches.ToString("N0"), ProfileBody),
			("VITTORIE", stats.wins.ToString("N0"), ProfileGood),
			("WIN RATE", stats.matches > 0 ? stats.winRatePercent + "%" : "—", ProfileGold),
			("MIGLIOR SERIE", stats.bestStreak.ToString("N0"), ProfileGold),
			("ROUND VINTI", stats.roundsWon.ToString("N0"), ProfileGood),
			("TRAGUARDI", $"{completed} / {total}", new Color(0.72f, 0.52f, 0.95f))
		});
	}

	private void RenderProfileStatistics()
	{
		Button scope = CreateButton("Profile Stats Scope", profileContentRoot,
			AccardND.Battlefield.MmoUiTheme.BodyFont, profileLifetimeStats ? "TOTALI" : "STAGIONE");
		Text scopeLabel = scope.GetComponentInChildren<Text>();
		if ((Object)(object)scopeLabel != (Object)null)
			scopeLabel.fontSize = 28;
		scope.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			profileLifetimeStats = !profileLifetimeStats;
			RefreshProfile();
		});
		SetRect((RectTransform)scope.transform, new Vector2(0.34f, 0.84f), new Vector2(0.66f, 1f));
		profileDynamicObjects.Add(scope.gameObject);

		PlayerStatsDto stats = profileLifetimeStats ? profileStats?.lifetime : profileStats?.season;
		if (stats == null)
		{
			CreateProfileMessage("NESSUNA STATISTICA DISPONIBILE", ProfileBody);
			return;
		}
		CreateProfileMetricGrid(new[]
		{
			("PARTITE", stats.matches.ToString("N0"), ProfileBody),
			("VITTORIE", stats.wins.ToString("N0"), ProfileGood),
			("SCONFITTE", stats.losses.ToString("N0"), ProfileBad),
			("ABBANDONI", stats.forfeits.ToString("N0"), ProfileBad),
			("ROUND VINTI", stats.roundsWon.ToString("N0"), ProfileGood),
			("ROUND PERSI", stats.roundsLost.ToString("N0"), ProfileBad),
			("SERIE ATTUALE", stats.currentStreak.ToString("N0"), new Color(0.62f, 0.72f, 1f)),
			("MIGLIOR SERIE", stats.bestStreak.ToString("N0"), ProfileGold),
			("WIN RATE", stats.matches > 0 ? stats.winRatePercent + "%" : "—", ProfileGold)
		}, 0.80f);
	}

	private void RenderProfileAchievements()
	{
		AchievementDto[] achievements = profileAchievements?.achievements;
		if (achievements == null || achievements.Length == 0)
		{
			CreateProfileMessage("NESSUN TRAGUARDO DISPONIBILE", ProfileBody);
			return;
		}

		RectTransform content = CreateProfileScrollList("Profile Achievements");

		for (int index = 0; index < achievements.Length; index++)
		{
			AchievementDto achievement = achievements[index];
			Image card = CreateImage("Achievement " + achievement.achievementId, content,
				achievement.unlocked ? new Color(0.08f, 0.23f, 0.13f, 0.98f) : new Color(0.06f, 0.08f, 0.12f, 0.98f));
			card.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
			card.type = Image.Type.Sliced;
			LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
			cardLayout.preferredHeight = 180f;
			cardLayout.flexibleHeight = 0f;
			profileDynamicObjects.Add(card.gameObject);

			Text name = CreateProfileText(card.rectTransform, "Name", achievement.name.ToUpperInvariant(), 30,
				TextAnchor.MiddleLeft, achievement.unlocked ? ProfileGood : ProfileGold,
				new Vector2(0.035f, 0.56f), new Vector2(0.70f, 0.95f));
			name.resizeTextForBestFit = false;
			name.horizontalOverflow = HorizontalWrapMode.Wrap;
			name.verticalOverflow = VerticalWrapMode.Truncate;

			Text progress = CreateProfileText(card.rectTransform, "Progress",
				achievement.unlocked ? "COMPLETATO" : $"{achievement.progress} / {achievement.threshold}", 26,
				TextAnchor.MiddleRight, achievement.unlocked ? ProfileGood : ProfileBody,
				new Vector2(0.70f, 0.56f), new Vector2(0.965f, 0.95f));
			progress.resizeTextForBestFit = false;

			Text description = CreateProfileText(card.rectTransform, "Description", achievement.description, 24,
				TextAnchor.MiddleLeft, ProfileBody, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.58f));
			description.resizeTextForBestFit = false;
			description.horizontalOverflow = HorizontalWrapMode.Wrap;
			description.verticalOverflow = VerticalWrapMode.Truncate;
		}
	}

	private void CreateProfileMetricGrid((string label, string value, Color color)[] metrics, float topLimit = 1f)
	{
		int columns = 3;
		float gap = 0.018f;
		float width = (1f - gap * (columns - 1)) / columns;
		int rows = Mathf.CeilToInt(metrics.Length / (float)columns);
		float available = topLimit - gap * (rows - 1);
		float height = available / rows;
		for (int index = 0; index < metrics.Length; index++)
		{
			int row = index / columns;
			int column = index % columns;
			float top = topLimit - row * (height + gap);
			Image tile = CreateImage("Profile Metric " + metrics[index].label, profileContentRoot,
				new Color(0.035f, 0.045f, 0.06f, 0.98f));
			tile.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
			tile.type = Image.Type.Sliced;
			SetRect(tile.rectTransform,
				new Vector2(column * (width + gap), top - height),
				new Vector2(column * (width + gap) + width, top));
			profileDynamicObjects.Add(tile.gameObject);
			CreateProfileText(tile.rectTransform, "Value", metrics[index].value, 28, TextAnchor.MiddleCenter,
				metrics[index].color, new Vector2(0.04f, 0.32f), new Vector2(0.96f, 0.90f));
			CreateProfileText(tile.rectTransform, "Label", metrics[index].label, 28, TextAnchor.MiddleCenter,
				ProfileBody, new Vector2(0.04f, 0.07f), new Vector2(0.96f, 0.35f));
		}
	}

	/// <summary>
	/// Il pallino rosso col numero di comunicazioni da leggere. Ne esistono due copie: una sul
	/// tab MESSAGGI e una sul bottone PROFILO dell'hub, perche' un'offerta che scade fra sette
	/// giorni non deve dipendere dal fatto che il giocatore apra il profilo per caso.
	/// </summary>
	private GameObject CreateProfileNotificationBadge(Button host, Font font, string name, out Text countText)
	{
		Sprite circleSprite = AccardND.Battlefield.MmoUiTheme.GetSolidCircleSprite();
		Image badgeFrame = CreateImage(name, host.transform, Color.black);
		badgeFrame.sprite = circleSprite;
		badgeFrame.type = Image.Type.Simple;
		badgeFrame.raycastTarget = false;

		RectTransform badgeRect = badgeFrame.rectTransform;
		badgeRect.anchorMin = Vector2.one;
		badgeRect.anchorMax = Vector2.one;
		badgeRect.pivot = new Vector2(0.5f, 0.5f);
		badgeRect.localScale = new Vector3(0.8f, 0.8f, 1f);
		badgeRect.sizeDelta = new Vector2(58f, 58f);
		badgeRect.anchoredPosition = new Vector2(-5f, -5f);

		Outline outline = badgeFrame.gameObject.AddComponent<Outline>();
		outline.effectColor = Color.black;
		outline.effectDistance = new Vector2(1f, -1f);

		Shadow shadow = badgeFrame.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
		shadow.effectDistance = new Vector2(2f, -2f);

		Image badge = CreateImage(name + " Inner", badgeFrame.transform,
			new Color(0.82f, 0.035f, 0.025f, 1f));
		badge.sprite = circleSprite;
		badge.type = Image.Type.Simple;
		badge.raycastTarget = false;
		RectTransform innerRect = badge.rectTransform;
		innerRect.anchorMin = new Vector2(0.5f, 0.5f);
		innerRect.anchorMax = new Vector2(0.5f, 0.5f);
		innerRect.pivot = new Vector2(0.5f, 0.5f);
		innerRect.sizeDelta = new Vector2(46f, 46f);
		innerRect.anchoredPosition = Vector2.zero;

		countText = CreateText(name + " Count", badge.transform, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
		countText.color = Color.white;
		countText.raycastTarget = false;
		Stretch(countText.rectTransform);
		badgeFrame.gameObject.SetActive(false);
		return badgeFrame.gameObject;
	}

	private void CreateProfileMessagesBadge(Button tab, Font font)
	{
		profileMessagesBadge = CreateProfileNotificationBadge(
			tab, font, "Profile Messages Notification", out profileMessagesBadgeText);
	}

	/// <summary>Il badge sul bottone PROFILO dell'hub: stessa posta, vista da fuori.</summary>
	private void CreateProfileHubNotificationBadge(Button profileHubButton, Font font)
	{
		profileHubBadge = CreateProfileNotificationBadge(
			profileHubButton, font, "Profile Hub Notification", out profileHubBadgeText);
		RectTransform badgeRect = (RectTransform)profileHubBadge.transform;
		badgeRect.anchorMin = Vector2.one;
		badgeRect.anchorMax = Vector2.one;
		badgeRect.pivot = new Vector2(0.5f, 0.5f);
		badgeRect.anchoredPosition = new Vector2(-116.2f, -44.8f);
		badgeRect.sizeDelta = new Vector2(58f, 58f);
		badgeRect.localScale = new Vector3(0.8f, 0.8f, 1f);
	}

	private void UpdateProfileMessagesBadges()
	{
		int pending = profilePendingRewards.Length;
		SetProfileBadge(profileMessagesBadge, profileMessagesBadgeText, pending);
		SetProfileBadge(profileHubBadge, profileHubBadgeText, pending);
	}

	private static void SetProfileBadge(GameObject badge, Text countText, int pending)
	{
		if ((Object)(object)badge == (Object)null || (Object)(object)countText == (Object)null)
			return;
		countText.text = pending > 9 ? "9+" : pending.ToString();
		badge.SetActive(pending > 0);
		badge.transform.SetAsLastSibling();
	}

	private Text CreateProfileText(
		Transform parent, string name, string value, int size, TextAnchor anchor, Color color,
		Vector2 minimum, Vector2 maximum)
	{
		Text text = CreateText(name, parent, AccardND.Battlefield.MmoUiTheme.BodyFont,
			size, FontStyle.Normal, anchor);
		text.text = value ?? string.Empty;
		text.color = color;
		SetRect(text.rectTransform, minimum, maximum);
		profileDynamicObjects.Add(text.gameObject);
		return text;
	}

	private void CreateProfileMessage(string message, Color color)
	{
		CreateProfileText(profileContentRoot, "Profile Message", message, 24,
			TextAnchor.MiddleCenter, color, Vector2.zero, Vector2.one);
	}

	private static Sprite ProfileRankEmblem(string tier)
	{
		string resource = (tier ?? string.Empty).Trim().ToLowerInvariant() switch
		{
			"apprendista" => "UI/MultiplayerRestyle/Ranks/rank_apprendista_v1",
			"esperto" => "UI/MultiplayerRestyle/Ranks/rank_esperto_v1",
			"divino" => "UI/MultiplayerRestyle/Ranks/rank_divino_v1",
			"onnipotente" => "UI/MultiplayerRestyle/Ranks/rank_onnipotente_v1",
			_ => "UI/MultiplayerRestyle/Ranks/rank_nabbo_v1"
		};
		return LoadSpriteResource(resource);
	}

	private void ClearProfileDynamicObjects()
	{
		foreach (GameObject item in profileDynamicObjects)
			if ((Object)(object)item != (Object)null)
				Object.Destroy(item);
		profileDynamicObjects.Clear();
	}
}
}
