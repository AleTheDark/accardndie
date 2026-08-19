using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccardND.Localization;
using AccardND.NetProtocol;
using AccardND.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private enum ProfilePage
	{
		Overview,
		Talents,
		Achievements,
		Messages
	}

	private GameObject profilePanel;
	private RectTransform profileIdentityRoot;
	private RectTransform profileContentPanelRoot;
	private RectTransform profileContentRoot;
	private readonly List<GameObject> profileDynamicObjects = new();
	private readonly List<Button> profileTabs = new();
	private ProfileData profileData;
	private AchievementsData profileAchievements;
	private ProfilePage profilePage;
	private bool profileLoading;

	/// <summary>
	/// Le ricompense gia' guadagnate su cui il triplicatore pubblicitario e' ancora in piedi.
	/// Le manda il server: sono le run finite senza che il video sia partito, quasi sempre
	/// perche' la connessione e' caduta proprio alla fine.
	/// </summary>
	private SinglePlayerPendingAdRewardData[] profilePendingRewards = Array.Empty<SinglePlayerPendingAdRewardData>();
	private bool profilePendingLoading;
	private bool profilePendingClaiming;
	private string profileMessagesNotice;
	private GameObject profileTalentsBadge;
	private Text profileTalentsBadgeText;
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

		Image veil = CreateImage("Profile Veil", root.transform, new Color(0f, 1f / 255f, 4f / 255f, 0.6f));
		veil.raycastTarget = false;
		SetRect(veil.rectTransform, Vector2.zero, Vector2.one);

		Image frameFill = CreateImage("Profile Frame Fill", root.transform, Color.clear);
		SetRect(frameFill.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));
		frameFill.rectTransform.offsetMax = new Vector2(0f, -22.0389f);

		Image frame = CreateImage("Profile Outer Frame", root.transform, Color.white);
		AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(frame);
		frame.raycastTarget = false;
		SetRect(frame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.795f));

		Image titlePlaque = CreateImage("Profile Title Plaque", root.transform, Color.white);
		titlePlaque.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
		titlePlaque.type = Image.Type.Sliced;
		SetRect(titlePlaque.rectTransform, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.9f));

		Text title = CreateText("Profile Title", titlePlaque.transform,
			AccardND.Battlefield.MmoUiTheme.TitleFont ?? fallbackFont, 48, FontStyle.Normal, TextAnchor.MiddleCenter);
		SetLocalizedText(title, GameTextKeys.Profile.Title, "PROFILO");
		AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
		title.color = ProfileGold;
		SetRect(title.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.72f));
		title.rectTransform.offsetMin = new Vector2(0f, -23f);
		title.rectTransform.offsetMax = new Vector2(0f, -23f);

		CreateProfileNavigation(root.transform, fallbackFont);

		profileIdentityRoot = CreateProfileSection(
			root.transform, GameText.Get(GameTextKeys.Profile.Adventurer),
			new Vector2(0.055f, 0.475f), new Vector2(0.945f, 0.645f));

		Image contentPanel = CreateImage("Profile Content Panel", root.transform, Color.clear);
		SetRect(contentPanel.rectTransform, new Vector2(0.055f, 0.045f), new Vector2(0.945f, 0.455f));
		// Limite fisico di tutte le pagine: nessun contenuto dinamico puo' oltrepassare
		// l'area interna dell'Outer Frame o invadere la navbar.
		contentPanel.gameObject.AddComponent<RectMask2D>();
		profileContentPanelRoot = contentPanel.rectTransform;
		Image content = CreateImage("Profile Content", contentPanel.transform, new Color(0f, 0f, 0f, 1f));
		SetRect(content.rectTransform, new Vector2(0.035f, 0.06f), new Vector2(0.965f, 0.94f));
		profileContentRoot = content.rectTransform;

		// La cornice e' l'ultimo livello visivo della schermata: copre sempre i bordi
		// del contenuto senza intercettare input grazie al raycast disabilitato.
		frame.transform.SetAsLastSibling();

		profilePanel.SetActive(false);
	}

	private void CreateProfileNavigation(Transform parent, Font fallbackFont)
	{
		string[] tabNames =
		{
			GameText.Get(GameTextKeys.Profile.TabOverview),
			GameText.Get(GameTextKeys.Profile.TabTalents),
			GameText.Get(GameTextKeys.Profile.TabAchievements),
			GameText.Get(GameTextKeys.Profile.TabMessages)
		};
		for (int index = 0; index < tabNames.Length; index++)
		{
			int captured = index;
			bool isMessagesTab = captured == (int)ProfilePage.Messages;
			Button tab = isMessagesTab
				? CreateButton("Profile Tab " + tabNames[index], parent, fallbackFont, string.Empty)
				: CreateButton("Profile Tab " + tabNames[index], parent, fallbackFont, tabNames[index]);
			UnityAction selectTab = delegate
			{
				RectTransform tabTarget = (RectTransform)tab.transform;
				bool guidedTabTap = IsGuidedTourWaitingForTarget(tabTarget);
				PlayGenericButtonClickSfx();
				SelectProfilePage((ProfilePage)captured);
				if (guidedTabTap)
					NotifyGuidedTourTargetTapped();
			};
			if (isMessagesTab)
			{
				Image tabImage = tab.GetComponent<Image>();
				tab.targetGraphic = null;
				if ((Object)(object)tabImage != (Object)null)
					Object.Destroy(tabImage);
			}
			tab.onClick.AddListener(selectTab);
			float gap = 0.012f;
			float messagesWidth = 0.075f;
			float regularWidth = (0.89f - messagesWidth - gap * (tabNames.Length - 1)) / (tabNames.Length - 1);
			float regularGroupWidth = regularWidth * (tabNames.Length - 1) + gap * (tabNames.Length - 2);
			float regularGroupLeft = (1f - regularGroupWidth) * 0.5f;
			float width = isMessagesTab ? messagesWidth : regularWidth;
			float left = isMessagesTab
				? 0.945f - messagesWidth
				: regularGroupLeft + captured * (regularWidth + gap);
			SetRect((RectTransform)tab.transform, new Vector2(left, 0.665f), new Vector2(left + width, 0.735f));
			if (isMessagesTab)
			{
				RectTransform tabRect = (RectTransform)tab.transform;
				tabRect.anchorMin = new Vector2(0.87f, 0.665f);
				tabRect.anchorMax = new Vector2(0.945f, 0.735f);
				tabRect.offsetMin = new Vector2(23f, 294f);
				tabRect.offsetMax = new Vector2(23f, 294f);
				tabRect.localScale = new Vector3(1.2f, 1.2f, 1.2f);
				Image envelope = CreateImage("Profile Messages Envelope", tab.transform, Color.white);
				envelope.sprite = LoadSpriteResource("UI/MultiplayerRestyle/profile_message_envelope");
				envelope.preserveAspect = true;
				envelope.raycastTarget = true;
				tab.targetGraphic = envelope;
				SetRect(envelope.rectTransform, new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.90f));
				envelope.rectTransform.offsetMin = new Vector2(0f, -43.6f);
				envelope.rectTransform.offsetMax = new Vector2(0f, -43.6f);
			}
			profileTabs.Add(tab);
		}
		CreateProfileTalentsBadge(profileTabs[(int)ProfilePage.Talents], fallbackFont);
		CreateProfileMessagesBadge(profileTabs[(int)ProfilePage.Messages], fallbackFont);
	}

	private RectTransform CreateProfileSection(
		Transform parent, string heading, Vector2 minimum, Vector2 maximum)
	{
		Image panel = CreateImage("Profile " + heading, parent, Color.clear);
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
		LoadTalentsFromServer();
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
			PvpServerMessageDispatcher dispatcher = await WaitForAccountSessionAsync();
			if (dispatcher == null)
				throw new InvalidOperationException("serve una connessione al server.");

			Task<Envelope> profileRequest = dispatcher.RequestAsync(
				MessageTypes.ProfileGet, null, MessageTypes.ProfileData, 8f);
			Task<Envelope> achievementsRequest = dispatcher.RequestAsync(
				MessageTypes.AchievementsGet, null, MessageTypes.AchievementsData, 8f);

			// Le due richieste sono indipendenti: se i traguardi non arrivano, le statistiche
			// del profilo non devono sparire con loro (e viceversa).
			ProfileData profile = PvpServerClient.ParsePayload<ProfileData>(
				await AwaitResponseAsync(profileRequest, "statistiche non caricate"));
			AchievementsData achievements = PvpServerClient.ParsePayload<AchievementsData>(
				await AwaitResponseAsync(achievementsRequest, "traguardi non caricati"));
			if (profile != null)
				profileData = profile;
			if (achievements != null)
				profileAchievements = achievements;
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

	/// <summary>
	/// Aspetta che la sessione account sia in piedi, invece di mollare al primo colpo.
	/// Il profilo puo' essere aperto mentre il socket si sta ancora riaprendo (rientro
	/// da una caduta di rete, app ripresa dal secondo piano): senza questa attesa la
	/// schermata restava senza statistiche fino a che il giocatore non passava
	/// dall'arena, che e' l'unica a riaprire la sessione per conto suo.
	/// </summary>
	private static async Task<PvpServerMessageDispatcher> WaitForAccountSessionAsync(
		float waitSeconds = 12f)
	{
		// L'attesa e' quella condivisa da tutte le schermate: se la sessione non puo'
		// piu' tornare non c'e' niente da aspettare, la sessione va rifatta dal login e
		// il profilo lo dice invece di restare a caricare.
		if (!await AccountServerSession.WaitUntilReadyAsync(waitSeconds))
			return null;
		return AccountServerSession.TryGet(out _, out PvpServerMessageDispatcher dispatcher, out _)
			? dispatcher
			: null;
	}

	/// <summary>Aspetta una risposta del server annotando l'esito, senza far cadere le altre.</summary>
	private async Task<Envelope> AwaitResponseAsync(Task<Envelope> request, string failureLabel)
	{
		try
		{
			return await request;
		}
		catch (Exception exception)
		{
			AppendLog($"PROFILO - {failureLabel}: {exception.Message}");
			return null;
		}
	}

	private void SelectProfilePage(ProfilePage page)
	{
		// L'esito dell'ultima riscossione vale finche' si guarda la posta: ritrovarlo tornando
		// dai traguardi farebbe sembrare appena successo qualcosa che e' gia' stato letto.
		if (page != ProfilePage.Messages)
			profileMessagesNotice = null;
		if (page == ProfilePage.Talents && profilePage != ProfilePage.Talents)
		{
			// Ogni apertura parte dal favo neutro: dettaglio e ramo compaiono solo dopo
			// una scelta esplicita del giocatore.
			selectedTalentBranch = null;
			selectedTalentId = null;
		}
		profilePage = page;
		RefreshProfile();
	}

	private void RefreshProfile()
	{
		ClearProfileDynamicObjects();
		RefreshProfileTabs();
		bool showsIdentity = profilePage == ProfilePage.Overview;
		if ((Object)(object)profileIdentityRoot != (Object)null)
			profileIdentityRoot.parent.gameObject.SetActive(showsIdentity);
		if ((Object)(object)profileContentPanelRoot != (Object)null)
		{
			bool fullFrameContent = profilePage == ProfilePage.Talents;
			SetRect(profileContentPanelRoot,
				fullFrameContent ? new Vector2(0.035f, 0.025f) : new Vector2(0.055f, 0.045f),
				fullFrameContent
					? new Vector2(0.965f, 0.655f)
					: new Vector2(0.945f, showsIdentity ? 0.455f : 0.645f));
			if ((Object)(object)profileContentRoot != (Object)null)
			{
				SetRect(profileContentRoot,
					fullFrameContent ? Vector2.zero : new Vector2(0.035f, 0.06f),
					fullFrameContent ? Vector2.one : new Vector2(0.965f, 0.94f));
			}
		}
		if (showsIdentity)
			RenderProfileIdentity();
		if (profileLoading && profileData == null)
		{
			CreateProfileMessage(
				GameText.Get(GameTextKeys.Profile.Loading),
				ProfileBody);
			return;
		}
		switch (profilePage)
		{
			case ProfilePage.Overview:
				RenderProfileOverview();
				break;
			case ProfilePage.Talents:
				RenderProfileTalents();
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
			AppendLog(GameText.Format(GameTextKeys.Profile.PendingLoadFailedLog, exception.Message));
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
					? GameText.Get(GameTextKeys.Profile.Loading)
					: GameText.Get(GameTextKeys.Profile.NoMessages),
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
					? GameText.Format(GameTextKeys.Profile.ExpiresHours, reward.hoursLeft)
					: string.Empty,
				22, TextAnchor.MiddleRight, ProfileBody,
				new Vector2(0.62f, 0.60f), new Vector2(0.90f, 0.94f));
			expiry.resizeTextForBestFit = false;

			SinglePlayerPendingAdRewardData captured = reward;
			Button dismiss = CreateButton("Profile Message Dismiss " + reward.claimId, card.rectTransform,
				AccardND.Battlefield.MmoUiTheme.BodyFont, "X");
			SetRect((RectTransform)dismiss.transform, new Vector2(0.91f, 0.72f), new Vector2(0.975f, 0.94f));
			dismiss.interactable = !profilePendingClaiming;
			dismiss.onClick.AddListener((UnityAction)(() => DismissPendingAdReward(captured)));
			profileDynamicObjects.Add(dismiss.gameObject);

			Text body = CreateProfileText(card.rectTransform, "Body",
				string.Equals(reward.rewardType, "tavern", StringComparison.Ordinal)
					? $"Hai riscosso {reward.baseHoney} vasetti di miele con x1.\n"
						+ $"Guarda il video per recuperare gli altri {reward.extraHoney} vasetti."
					: GameText.Format(GameTextKeys.Profile.PendingRewardBody, reward.baseAccountExperience, reward.extraAccountExperience),
				24, TextAnchor.UpperLeft, ProfileBody,
				new Vector2(0.035f, 0.08f), new Vector2(0.64f, 0.58f));
			body.resizeTextForBestFit = false;
			body.horizontalOverflow = HorizontalWrapMode.Wrap;
			body.verticalOverflow = VerticalWrapMode.Truncate;

			Button claim = CreateButton("Profile Message Claim " + reward.claimId, card.rectTransform,
				AccardND.Battlefield.MmoUiTheme.BodyFont,
				string.Equals(reward.rewardType, "tavern", StringComparison.Ordinal)
					? "x5 \u25b6"
					: GameText.Get(GameTextKeys.Profile.Triple));
			Text claimLabel = claim.GetComponentInChildren<Text>();
			if ((Object)(object)claimLabel != (Object)null)
			{
				claimLabel.fontSize = 26;
				claimLabel.resizeTextForBestFit = false;
			}
			SetRect((RectTransform)claim.transform, new Vector2(0.67f, 0.10f), new Vector2(0.965f, 0.52f));
			ApplyMerchantRoomCta(claim, claimLabel, "UI/CampaignRestyle/campaign_cta_blue", preserveAspect: false);
			claim.interactable = !profilePendingClaiming;
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
		if (string.Equals(reward.rewardType, "tavern", StringComparison.Ordinal))
			return "QUEST TAVERNA · MIELE DA RECUPERARE";
		if (!string.Equals(reward.rewardType, "death", StringComparison.Ordinal))
			return GameText.Get(GameTextKeys.Profile.PendingReward);
		string chapter = string.IsNullOrWhiteSpace(reward.chapterId)
			? GameText.Get(GameTextKeys.Profile.Campaign)
			: AdventureChapterDisplayName(reward.chapterId).ToUpperInvariant();
		return reward.roomsCleared > 0
			? GameText.Format(GameTextKeys.Profile.CampaignEndRooms, chapter, reward.roomsCleared)
			: GameText.Format(GameTextKeys.Profile.CampaignEnd, chapter);
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
			? GameText.Get(GameTextKeys.Profile.Claiming)
			: GameText.Get(GameTextKeys.Profile.LoadingAd);
		RefreshProfile();
		try
		{
			if (!ServerProgressReady && !await EnsureServerProgressAsync())
			{
				profileMessagesNotice = GameText.Get(GameTextKeys.Profile.ConnectionRequired);
				return;
			}

			AccardND.Ads.AdPlacement placement = string.Equals(
				reward.rewardType, "tavern", StringComparison.Ordinal)
				? AccardND.Ads.AdPlacement.TavernQuestClaim
				: AccardND.Ads.AdPlacement.CampaignExperienceTriple;
			AccardND.Ads.AdResult ad = await AccardND.Ads.AdService.ShowAsync(
				placement,
				AccardND.Ads.AdRewardContext.ForClaim(reward.claimId),
				asGate: true);
			if (!ad.Grants)
			{
				profileMessagesNotice = ad.Unavailable
					? GameText.Get(GameTextKeys.Profile.AdUnavailable)
					: GameText.Get(GameTextKeys.Profile.AdIncomplete);
				AppendLog(GameText.Format(GameTextKeys.Profile.TripleNotAppliedLog, ad.Outcome));
				return;
			}

			AccardND.Network.SinglePlayerRewardOutcome outcome =
				await serverProgress.ClaimAdMultiplierAsync(reward.claimId, ad.ImpressionId);
			MirrorServerProgress();
			RefreshSinglePlayerProgressView();
			RefreshAccountBannerView();
			profileMessagesNotice = string.Equals(reward.rewardType, "tavern", StringComparison.Ordinal)
				? $"+{outcome.GrantedHoney} honey: reward increased to x5."
				: GameText.Format(GameTextKeys.Profile.TripleApplied, outcome.GrantedAccountExperience);
			AppendLog(GameText.Format(GameTextKeys.Profile.TripleRecoveredLog, outcome.GrantedAccountExperience));
		}
		catch (Exception exception)
		{
			profileMessagesNotice = GameText.Get(GameTextKeys.Profile.ConnectionRequired);
			AppendLog(GameText.Format(GameTextKeys.Profile.TripleRejectedLog, exception.Message));
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

	private async void DismissPendingAdReward(SinglePlayerPendingAdRewardData reward)
	{
		if (profilePendingClaiming || reward == null || string.IsNullOrWhiteSpace(reward.claimId))
			return;
		profilePendingClaiming = true;
		try
		{
			await serverProgress.DismissPendingAdRewardAsync(reward.claimId);
			profileMessagesNotice = string.Empty;
		}
		catch (Exception exception)
		{
			profileMessagesNotice = "Impossibile eliminare il messaggio: " + exception.Message;
		}
		finally
		{
			profilePendingClaiming = false;
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
			bool isMessagesTab = index == (int)ProfilePage.Messages;
			Image image = button.GetComponent<Image>();
			if (isMessagesTab && (Object)(object)image != (Object)null)
			{
				image.sprite = LoadSpriteResource("UI/ProfileTabs/profile_tab_messages");
				image.type = Image.Type.Simple;
				image.preserveAspect = true;
				image.color = profilePage == ProfilePage.Messages
					? Color.white
					: new Color(0.82f, 0.82f, 0.82f, 1f);
				button.targetGraphic = image;
			}
			Text label = button.GetComponentInChildren<Text>();
			if ((Object)(object)label != (Object)null)
			{
				AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(label);
				label.fontSize = 21;
				if (isMessagesTab)
					label.color = profilePage == ProfilePage.Messages ? Color.white : ProfileBody;
			}
			if (!isMessagesTab)
				SetProfileNavigationTabActive(button, index == (int)profilePage);
		}
	}

	private static void SetProfileNavigationTabActive(Button tab, bool active)
	{
		if ((Object)(object)tab == (Object)null)
			return;

		Image image = tab.GetComponent<Image>();
		if ((Object)(object)image != (Object)null)
		{
			image.sprite = AccardND.Battlefield.MmoUiTheme.GetButtonSprite(
				active
					? AccardND.Battlefield.MmoUiTheme.ButtonVariant.Violet
					: AccardND.Battlefield.MmoUiTheme.ButtonVariant.Gold);
			image.type = Image.Type.Sliced;
			image.preserveAspect = false;
			image.color = active ? Color.white : new Color(0.44f, 0.42f, 0.38f, 0.96f);
			tab.targetGraphic = image;
		}

		Text label = tab.GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
			label.color = active
				? new Color(0.98f, 0.87f, 0.64f, 1f)
				: new Color(0.78f, 0.74f, 0.68f, 1f);
	}

	private void RenderProfileIdentity()
	{
		if ((Object)(object)profileIdentityRoot == (Object)null)
			return;
		if (profileData == null)
		{
			CreateProfileText(profileIdentityRoot, "Identity Loading", ProfileUnavailableMessage(),
				22, TextAnchor.MiddleCenter, ProfileBody, Vector2.zero, Vector2.one);
			return;
		}

		Sprite emblemSprite = ProfileRankEmblem(profileData.tier);
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

	/// <summary>
	/// Perche' le statistiche non ci sono. Un "non disponibili" secco lasciava il dubbio
	/// che il profilo fosse vuoto: quasi sempre e' la sessione col server che manca, e
	/// il giocatore deve sapere se aspettare o rifare l'accesso.
	/// </summary>
	private static string ProfileUnavailableMessage()
	{
		if (AccountServerSession.IsReconnecting)
			return "RICONNESSIONE AL SERVER IN CORSO...";
		if (!AccountServerSession.IsReady)
			return "NESSUNA SESSIONE COL SERVER · RIENTRA DAL LOGIN";
		return "DATI PROFILO NON DISPONIBILI";
	}

	private void RenderProfileOverview()
	{
		if (profileData == null)
		{
			CreateProfileMessage(ProfileUnavailableMessage(), ProfileBody);
			return;
		}
		int matches = profileData.wins + profileData.losses;
		int completed = 0;
		int total = profileAchievements?.achievements?.Length ?? 0;
		if (profileAchievements?.achievements != null)
			foreach (AchievementDto achievement in profileAchievements.achievements)
				if (achievement.unlocked)
					completed++;

		CreateProfileMetricGrid(new[]
		{
			("PARTITE", matches.ToString("N0"), ProfileBody),
			("VITTORIE", profileData.wins.ToString("N0"), ProfileGood),
			("WIN RATE", matches > 0 ? profileData.winRatePercent + "%" : "—", ProfileGold),
			("MIGLIOR SERIE", profileData.bestStreak.ToString("N0"), ProfileGold),
			("ROUND VINTI", profileData.roundsWon.ToString("N0"), ProfileGood),
			("TRAGUARDI", $"{completed} / {total}", new Color(0.72f, 0.52f, 0.95f))
		});
	}

	/// <summary>
	/// Il favo dei talenti. Catalogo, ranghi, prezzi e motivi di blocco arrivano gia' risolti
	/// dal server: qui non si calcola niente, si disegna quello che e' tornato.
	/// </summary>
	private void RenderProfileTalents()
	{
		Image backing = CreateImage("Talent Hive Backing", profileContentRoot,
			new Color(0.015f, 0.025f, 0.032f, 0.88f));
		backing.sprite = LoadSpriteResource("UI/MultiplayerRestyle/ornate_panel_frame");
		backing.type = Image.Type.Sliced;
		SetRect(backing.rectTransform, Vector2.zero, Vector2.one);
		profileDynamicObjects.Add(backing.gameObject);

		// Sfondo dedicato dell'alveare, se e' stato disegnato. Va dentro la cornice e sotto
		// tutto il resto: finche' non esiste resta il fondo scuro, che e' gia' leggibile.
		Sprite hiveBackground = LoadSpriteResource("UI/ProfileTalents/talents_background");
		if ((Object)(object)hiveBackground != (Object)null)
		{
			Image background = CreateImage("Talent Hive Background", backing.transform, new Color(1f, 1f, 1f, 0.5f));
			background.sprite = hiveBackground;
			background.type = Image.Type.Simple;
			background.raycastTarget = false;
			// Il bordo superiore dello sfondo si ferma a meta' della targa Talent Detail.
			SetRect(background.rectTransform, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.8875f));
			AspectRatioFitter backgroundFitter = background.gameObject.AddComponent<AspectRatioFitter>();
			backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
			backgroundFitter.aspectRatio =
				hiveBackground.rect.width / Mathf.Max(1f, hiveBackground.rect.height);
			profileDynamicObjects.Add(background.gameObject);
		}

		Image viewportImage = CreateImage("Talent Hive Viewport", backing.transform, new Color(0f, 0f, 0f, 0.01f));
		viewportImage.raycastTarget = true;
		// Il favo usa tutta la superficie sotto la navbar. Dettaglio, propoli e dock dei
		// rami sono HUD sovrapposti e non sottraggono spazio alla tavola navigabile.
		SetRect(viewportImage.rectTransform, new Vector2(0.012f, 0.012f), new Vector2(0.988f, 0.988f));
		Mask viewportMask = viewportImage.gameObject.AddComponent<Mask>();
		viewportMask.showMaskGraphic = false;
		profileDynamicObjects.Add(viewportImage.gameObject);

		Image hiveSurface = CreateImage("Talent Hive Draggable Surface", viewportImage.transform, Color.clear);
		RectTransform hiveRoot = hiveSurface.rectTransform;
		hiveRoot.anchorMin = new Vector2(-0.22f, -0.22f);
		hiveRoot.anchorMax = new Vector2(1.22f, 1.22f);
		hiveRoot.offsetMin = Vector2.zero;
		hiveRoot.offsetMax = Vector2.zero;
		hiveRoot.anchoredPosition = Vector2.zero;
		hiveSurface.raycastTarget = false;
		profileDynamicObjects.Add(hiveSurface.gameObject);

		TalentHiveDragSurface dragSurface = viewportImage.gameObject.AddComponent<TalentHiveDragSurface>();
		dragSurface.Initialize(
			viewportImage.rectTransform, hiveRoot,
			talentHivePanPosition, talentHiveZoom,
			position => talentHivePanPosition = position,
			zoom => talentHiveZoom = zoom);

		// Livello dei frammenti sotto la griglia metallica. Ha lo stesso identico fitter
		// 4:3 del reticolo, cosi' le illustrazioni restano dentro gli alveoli mentre la
		// cornice dello scacchiere viene disegnata davanti.
		Image nodeLayer = CreateImage("Talent Hive Node Layer", hiveRoot, Color.clear);
		nodeLayer.raycastTarget = false;
		SetRect(nodeLayer.rectTransform, Vector2.zero, Vector2.one);
		AspectRatioFitter nodeLayerFitter = nodeLayer.gameObject.AddComponent<AspectRatioFitter>();
		nodeLayerFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		nodeLayerFitter.aspectRatio = 4f / 3f;
		profileDynamicObjects.Add(nodeLayer.gameObject);

		Image lattice = CreateImage("Talent Hive Lattice", hiveRoot, new Color(1f, 1f, 1f, 0.52f));
		lattice.sprite = LoadSpriteResource("UI/ProfileTalents/talent_hive_lattice");
		lattice.type = Image.Type.Simple;
		lattice.preserveAspect = true;
		lattice.raycastTarget = false;
		SetRect(lattice.rectTransform, Vector2.zero, Vector2.one);
		AspectRatioFitter latticeFitter = lattice.gameObject.AddComponent<AspectRatioFitter>();
		latticeFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		latticeFitter.aspectRatio = 4f / 3f;
		profileDynamicObjects.Add(lattice.gameObject);

		Image rankLayer = CreateImage("Talent Hive Rank Overlay", hiveRoot, Color.clear);
		rankLayer.raycastTarget = false;
		SetRect(rankLayer.rectTransform, Vector2.zero, Vector2.one);
		AspectRatioFitter rankLayerFitter = rankLayer.gameObject.AddComponent<AspectRatioFitter>();
		rankLayerFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		rankLayerFitter.aspectRatio = 4f / 3f;
		profileDynamicObjects.Add(rankLayer.gameObject);

		RenderTalentPropolisSlot(lattice.rectTransform);

		if (talentData == null)
		{
			CreateProfileText(backing.rectTransform, "Talent Loading",
				talentLoading
					? GameText.Get(GameTextKeys.Talents.Loading)
					: GameText.Get(GameTextKeys.Talents.Unavailable),
				24, TextAnchor.MiddleCenter, ProfileBody,
				new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.6f));
			return;
		}

		RenderTalentBranchSelector(lattice.rectTransform);
		RenderTalentNodes(nodeLayer.rectTransform);
		RenderTalentProgressVfx(lattice.rectTransform);
		RenderTalentRanks(rankLayer.rectTransform);
		RenderTalentDetail(backing.rectTransform);
	}

	/// <summary>Il contatore dei punti, in alto a destra: e' la valuta di questa schermata.</summary>
	private void RenderTalentPropolisSlot(Transform parent)
	{
		Image propolisSlot = CreateImage("Talent Propolis Slot", parent, Color.clear);
		SetRect(propolisSlot.rectTransform, new Vector2(0.405f, 0.365f), new Vector2(0.595f, 0.625f));
		profileDynamicObjects.Add(propolisSlot.gameObject);

		Image propolis = CreateImage("Propolis Currency", propolisSlot.transform, Color.white);
		propolis.sprite = LoadSpriteResource("UI/ProfileTalents/propolis_currency");
		propolis.preserveAspect = true;
		propolis.raycastTarget = false;
		propolis.rectTransform.anchorMin = new Vector2(0.28f, 0.4765f);
		propolis.rectTransform.anchorMax = new Vector2(0.7272f, 0.91f);
		propolis.rectTransform.offsetMin = new Vector2(-131.5316f, -177.601f);
		propolis.rectTransform.offsetMax = new Vector2(131.5316f, 82.2418f);
		profileDynamicObjects.Add(propolis.gameObject);

		Text propolisCount = CreateProfileText(propolisSlot.rectTransform, "Propolis Count",
			(talentData?.talentPoints ?? 0).ToString("N0"),
			90, TextAnchor.MiddleCenter, Color.white, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.54f));
		propolisCount.rectTransform.offsetMin = new Vector2(-20.3158f, -36.639f);
		propolisCount.rectTransform.offsetMax = new Vector2(22.3158f, 131.251f);
		AddTalentTextOutline(propolisCount);

		Text propolisLabel = CreateProfileText(propolisSlot.rectTransform, "Propolis Label", GameText.Get(GameTextKeys.Talents.PropolisPoints),
			25, TextAnchor.MiddleCenter, Color.white, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.25f));
		propolisLabel.rectTransform.offsetMin = new Vector2(1f, -25f);
		propolisLabel.rectTransform.offsetMax = new Vector2(1f, -25f);
		AddTalentTextOutline(propolisLabel);
	}

	private static void AddTalentTextOutline(Text text)
	{
		if ((Object)(object)text == (Object)null)
			return;
		Outline outline = text.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 1f);
		outline.effectDistance = new Vector2(2f, -2f);
		outline.useGraphicAlpha = false;
	}

	/// <summary>
	/// I quattro emblemi identificano visivamente i rami del favo. Non sono controlli:
	/// le celle restano consultabili direttamente senza che gli emblemi intercettino input.
	/// </summary>
	private void RenderTalentBranchSelector(Transform parent)
	{
		var slots = new (string Branch, string Sprite, Vector2 Minimum, Vector2 Maximum, Color Color)[]
		{
			(TalentBranchPurse, "UI/ProfileTalents/branch_purse",
				new Vector2(0.835f, 0.835f), new Vector2(0.985f, 1.06f), new Color(0.98f, 0.74f, 0.28f)),
			(TalentBranchInitiative, "UI/ProfileTalents/branch_initiative",
				new Vector2(0.015f, 0.835f), new Vector2(0.165f, 1.06f), new Color(0.42f, 0.75f, 1f)),
			(TalentBranchMastery, "UI/ProfileTalents/branch_mastery",
				new Vector2(0.835f, -0.02f), new Vector2(0.985f, 0.205f), new Color(0.73f, 0.55f, 1f)),
			(TalentBranchOccasion, "UI/ProfileTalents/branch_occasions",
				new Vector2(0.015f, -0.02f), new Vector2(0.165f, 0.205f), new Color(0.94f, 0.45f, 0.38f))
		};

		foreach ((string branch, string sprite, Vector2 minimum, Vector2 maximum, Color color) in slots)
		{
			bool selected = string.Equals(branch, selectedTalentBranch, StringComparison.Ordinal);
			TalentBranchData data = FindTalentBranch(branch);

			Image emblem = CreateImage("Talent Branch " + branch, parent, Color.clear);
			emblem.raycastTarget = false;
			SetRect(emblem.rectTransform, minimum, maximum);
			profileDynamicObjects.Add(emblem.gameObject);

			Image branchIcon = CreateImage("Talent Branch Emblem " + branch, emblem.transform, Color.white);
			branchIcon.sprite = LoadSpriteResource(sprite);
			branchIcon.preserveAspect = true;
			branchIcon.raycastTarget = false;
			branchIcon.color = new Color(1f, 1f, 1f, 1f);
			SetRect(branchIcon.rectTransform, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.98f));
			profileDynamicObjects.Add(branchIcon.gameObject);

			Text branchLabel = CreateProfileText(emblem.transform, "Talent Branch Label " + branch,
				TalentUiText.BranchName(data?.id ?? branch).ToUpperInvariant(), 30,
				TextAnchor.MiddleCenter, selected ? color : new Color(color.r, color.g, color.b, 0.62f),
				new Vector2(0.01f, 0.015f), new Vector2(0.99f, 0.27f));
			branchLabel.resizeTextMinSize = 20;
			branchLabel.raycastTarget = false;
		}
	}

	/// <summary>
	/// Le celle del ramo aperto, una colonna per tier. Il tier e' l'asse verticale perche' e'
	/// l'ordine in cui si sbloccano: guardare l'albero deve dire subito quanto manca al
	/// prossimo scalino.
	/// </summary>
	private void RenderTalentNodes(Transform parent)
	{
		Dictionary<string, Vector2[]> branchSlots = TalentHiveBranchSlots();

		foreach (KeyValuePair<string, Vector2[]> group in branchSlots)
		{
			List<TalentEntryData> nodes = OrderedVisibleTalentsOfBranch(group.Key);
			int count = Mathf.Min(nodes.Count, group.Value.Length);
			for (int index = 0; index < count; index++)
			{
				Vector2 center = group.Value[index];
				Vector2 halfSize = new Vector2(0.072f, 0.095f);
				CreateTalentNodeCell(parent, nodes[index], center - halfSize, center + halfSize);
			}
		}
	}

	private void RenderTalentProgressVfx(Transform parent)
	{
		GameObject effectObject = new GameObject(
			"Talent Hive Progress VFX", typeof(RectTransform), typeof(CanvasRenderer),
			typeof(TalentHiveProgressGraphic));
		effectObject.transform.SetParent(parent, false);
		RectTransform effectRect = (RectTransform)effectObject.transform;
		SetRect(effectRect, Vector2.zero, Vector2.one);
		TalentHiveProgressGraphic graphic = effectObject.GetComponent<TalentHiveProgressGraphic>();
		graphic.raycastTarget = false;

		Dictionary<string, Vector2[]> slots = TalentHiveBranchSlots();
		foreach (KeyValuePair<string, Vector2[]> group in slots)
		{
			List<TalentEntryData> nodes = OrderedVisibleTalentsOfBranch(group.Key);
			Color color = TalentBranchColor(group.Key);
			int count = Mathf.Min(nodes.Count, group.Value.Length);
			for (int index = 0; index < count; index++)
			{
				if (nodes[index].rank > 0)
				{
					if (index == 0)
						graphic.AddTube(new Vector2(0.5f, 0.495f), group.Value[0], color);
					else if ((index == 1 || index == 2) && nodes[0].rank > 0)
						graphic.AddTube(group.Value[0], group.Value[index], color);
					else if (index == 3)
					{
						// Ogni flusso parte solo da un nodo realmente acquisito: il nodo finale
						// puo' essere raggiunto da uno dei due percorsi senza attivare anche l'altro.
						if (nodes[1].rank > 0)
							graphic.AddTube(group.Value[1], group.Value[3], color);
						if (nodes[2].rank > 0)
							graphic.AddTube(group.Value[2], group.Value[3], color);
					}
				}

				if (nodes[index].rank >= nodes[index].maxRank)
					graphic.AddCompletedHex(group.Value[index], new Vector2(0.062f, 0.082f), color);
			}
		}
		graphic.Commit();
		profileDynamicObjects.Add(effectObject);
	}

	private void RenderTalentRanks(Transform parent)
	{
		Dictionary<string, Vector2[]> slots = TalentHiveBranchSlots();
		foreach (KeyValuePair<string, Vector2[]> group in slots)
		{
			List<TalentEntryData> nodes = OrderedVisibleTalentsOfBranch(group.Key);
			int count = Mathf.Min(nodes.Count, group.Value.Length);
			for (int index = 0; index < count; index++)
			{
				Vector2 center = group.Value[index];
				Vector2 halfSize = new Vector2(0.072f, 0.095f);
				CreateTalentNodeRank(parent, nodes[index], center - halfSize, center + halfSize);
			}
		}
	}

	private void CreateTalentNodeRank(
		Transform parent, TalentEntryData node, Vector2 minimum, Vector2 maximum)
	{
		bool maxed = node.rank >= node.maxRank;
		Image holder = CreateImage("Talent Rank Holder " + node.id, parent, Color.clear);
		holder.raycastTarget = false;
		SetRect(holder.rectTransform, minimum, maximum);
		profileDynamicObjects.Add(holder.gameObject);

		Image rankBadge = CreateImage("Talent Rank Badge " + node.id, holder.transform, Color.white);
		rankBadge.sprite = LoadSpriteResource("UI/ProfileTalents/talents_points_badge");
		rankBadge.preserveAspect = true;
		rankBadge.raycastTarget = false;
		rankBadge.color = maxed ? Color.white : new Color(0.68f, 0.72f, 0.78f, 0.88f);
		SetRect(rankBadge.rectTransform, new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.30f));
		profileDynamicObjects.Add(rankBadge.gameObject);

		Text rankText = CreateProfileText(holder.transform, "Talent Node Rank " + node.id,
			node.rank + "/" + node.maxRank,
			21, TextAnchor.MiddleCenter, maxed ? ProfileGold : ProfileBody,
			new Vector2(0.35f, 0.075f), new Vector2(0.65f, 0.245f));
		rankText.fontStyle = FontStyle.Bold;
		rankText.rectTransform.offsetMin = new Vector2(rankText.rectTransform.offsetMin.x, -30f);
		rankText.rectTransform.offsetMax = new Vector2(rankText.rectTransform.offsetMax.x, -30f);
		AddTalentTextOutline(rankText);
	}

	private List<TalentEntryData> OrderedVisibleTalentsOfBranch(string branch)
	{
		List<TalentEntryData> nodes = TalentsOfBranch(branch);
		if (branch == TalentBranchPurse)
			nodes.RemoveAll(node => string.Equals(node.id, "purse-first-deal", StringComparison.Ordinal));
		nodes.Sort((left, right) => left.tier != right.tier
			? left.tier.CompareTo(right.tier)
			: string.Compare(left.id, right.id, StringComparison.Ordinal));
		return nodes;
	}

	private static Color TalentBranchColor(string branch) => branch switch
	{
		TalentBranchPurse => new Color(0.98f, 0.65f, 0.12f, 1f),
		TalentBranchInitiative => new Color(0.18f, 0.72f, 1f, 1f),
		TalentBranchMastery => new Color(0.66f, 0.35f, 1f, 1f),
		TalentBranchOccasion => new Color(1f, 0.28f, 0.2f, 1f),
		_ => Color.white
	};

	private static Dictionary<string, Vector2[]> TalentHiveBranchSlots()
	{
		return new Dictionary<string, Vector2[]>
		{
			[TalentBranchInitiative] = new[]
			{
				new Vector2(0.297f, 0.595f), new Vector2(0.183f, 0.738f),
				new Vector2(0.411f, 0.738f), new Vector2(0.297f, 0.885f)
			},
			[TalentBranchPurse] = new[]
			{
				new Vector2(0.701f, 0.595f), new Vector2(0.587f, 0.738f),
				new Vector2(0.815f, 0.738f), new Vector2(0.701f, 0.885f)
			},
			[TalentBranchOccasion] = new[]
			{
				new Vector2(0.297f, 0.397f), new Vector2(0.183f, 0.263f),
				new Vector2(0.411f, 0.263f), new Vector2(0.297f, 0.125f)
			},
			[TalentBranchMastery] = new[]
			{
				new Vector2(0.701f, 0.397f), new Vector2(0.587f, 0.263f),
				new Vector2(0.815f, 0.263f), new Vector2(0.701f, 0.125f)
			}
		};
	}

	private void CreateTalentNodeCell(
		Transform parent, TalentEntryData node, Vector2 minimum, Vector2 maximum)
	{
		bool maxed = node.rank >= node.maxRank;

		Button cell = CreateImageButton(
			"Talent Node " + node.id, parent,
			AccardND.Battlefield.MmoUiTheme.BodyFont, null, null);
		string capturedId = node.id;
		cell.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			SelectTalentNode(capturedId);
		});
		SetRect((RectTransform)cell.transform, minimum, maximum);
		Image frameImage = cell.GetComponent<Image>();
		if ((Object)(object)frameImage != (Object)null)
		{
			// Il bottone e' solo la superficie interattiva. La cornice viene disegnata
			// separatamente sopra l'illustrazione, altrimenti l'icona la copre.
			frameImage.sprite = null;
			frameImage.color = new Color(0f, 0f, 0f, 0.01f);
			frameImage.type = Image.Type.Simple;
		}
		profileDynamicObjects.Add(cell.gameObject);

		Color nameColor = maxed ? ProfileGold : node.tierUnlocked ? ProfileBody : new Color(0.5f, 0.56f, 0.6f);

		// L'icona si cerca dall'id del nodo. Finche' il file non c'e' la cella ripiega sul
		// nome scritto, cosi' l'albero e' leggibile prima che l'arte esista e si illustra da
		// solo quando arriva, senza una riga di codice in piu'.
		Sprite icon = LoadSpriteResource(TalentIconResourcePath(node.id));
		if ((Object)(object)icon != (Object)null)
		{
			GameObject maskObject = new GameObject(
				"Talent Node Hex Mask " + node.id,
				typeof(RectTransform), typeof(CanvasRenderer),
				typeof(TalentHexMaskGraphic), typeof(Mask));
			maskObject.transform.SetParent(cell.transform, false);
			RectTransform maskRect = (RectTransform)maskObject.transform;
			SetRect(maskRect, new Vector2(0.14f, 0.21f), new Vector2(0.86f, 0.88f));
			TalentHexMaskGraphic maskGraphic = maskObject.GetComponent<TalentHexMaskGraphic>();
			maskGraphic.color = Color.white;
			maskGraphic.raycastTarget = false;
			maskObject.GetComponent<Mask>().showMaskGraphic = false;

			Image iconImage = CreateImage("Talent Node Icon " + node.id, maskObject.transform, Color.white);
			iconImage.sprite = icon;
			iconImage.preserveAspect = false;
			iconImage.raycastTarget = false;
			// Il frammento acquistato riempie l'alveolo; quello disponibile resta una
			// presenza spettrale, quello bloccato quasi inciso nel fondo.
			iconImage.color = node.rank > 0
				? Color.white
				: node.tierUnlocked
					? new Color(0.62f, 0.68f, 0.74f, 0.58f)
					: new Color(0.28f, 0.32f, 0.36f, 0.24f);
			SetRect(iconImage.rectTransform, Vector2.zero, Vector2.one);
			profileDynamicObjects.Add(iconImage.gameObject);
		}
		else
		{
			CreateProfileText(cell.transform, "Talent Node Name " + node.id, TalentUiText.Name(node).ToUpperInvariant(),
				17, TextAnchor.MiddleCenter, nameColor,
				new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.82f));
		}

	}

	/// <summary>
	/// La riga in basso col nodo scelto: cosa fa adesso, cosa farebbe al rango dopo e il
	/// bottone per comprarlo. Se non e' comprabile mostra il motivo che ha deciso il server:
	/// il client non riscrive le regole, le riporta.
	/// </summary>
	private void RenderTalentDetail(Transform parent)
	{
		TalentEntryData node = FindTalent(selectedTalentId);
		if (node == null)
			return;

		Image bar = CreateImage("Talent Detail", parent, new Color(0.82745f, 0.82745f, 0.82745f, 1f));
		bar.sprite = LoadSpriteResource("UI/ProfileTalents/talents_title_plaque");
		bar.type = Image.Type.Simple;
		bar.preserveAspect = false;
		SetRect(bar.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.995f));
		bar.rectTransform.offsetMin = new Vector2(-24.8149f, -30.7904f);
		bar.rectTransform.offsetMax = new Vector2(24.8149f, 42f);
		profileDynamicObjects.Add(bar.gameObject);

		Color branchColor = node.branch switch
		{
			TalentBranchPurse => new Color(0.98f, 0.74f, 0.28f, 1f),
			TalentBranchInitiative => new Color(0.42f, 0.75f, 1f, 1f),
			TalentBranchMastery => new Color(0.73f, 0.55f, 1f, 1f),
			TalentBranchOccasion => new Color(0.94f, 0.45f, 0.38f, 1f),
			_ => Color.white
		};
		Text detailName = CreateProfileText(bar.rectTransform, "Talent Detail Name", TalentUiText.Name(node).ToUpperInvariant(),
			28, TextAnchor.MiddleCenter, branchColor,
			new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.96f));
		detailName.rectTransform.offsetMin = new Vector2(0f, -22.2f);
		detailName.rectTransform.offsetMax = new Vector2(0f, -22.2f);
		Outline detailNameOutline = detailName.gameObject.AddComponent<Outline>();
		detailNameOutline.effectColor = new Color(0f, 0f, 0f, 1f);
		detailNameOutline.effectDistance = new Vector2(2f, 2f);
		detailNameOutline.useGraphicAlpha = false;

		Text detailBody = CreateProfileText(bar.rectTransform, "Talent Detail Body", TalentDetailBody(node),
			30, TextAnchor.MiddleCenter, Color.white,
			new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.67f));
		detailBody.rectTransform.offsetMin = new Vector2(0f, 8f);
		detailBody.rectTransform.offsetMax = new Vector2(0f, 8f);

		bool maxed = node.rank >= node.maxRank;
		if (maxed)
		{
			Text maxedText = CreateProfileText(bar.rectTransform, "Talent Detail Maxed", GameText.Get(GameTextKeys.Talents.Maxed),
				20, TextAnchor.MiddleCenter, ProfileGood,
				new Vector2(0.34f, 0.03f), new Vector2(0.66f, 0.3f));
			maxedText.rectTransform.offsetMin = new Vector2(0f, 42.9f);
			maxedText.rectTransform.offsetMax = new Vector2(0f, 42.9f);
			return;
		}

		if (!node.purchasable)
		{
			Text lockedText = CreateProfileText(bar.rectTransform, "Talent Detail Locked",
				 TalentUiText.LockedReason(node, FindTalentBranch(node.branch)?.pointsSpent ?? 0),
				18, TextAnchor.MiddleCenter, new Color(0.85f, 0.62f, 0.42f),
				new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.3f));
			lockedText.rectTransform.offsetMin = new Vector2(-158.4797f, 24f);
			lockedText.rectTransform.offsetMax = new Vector2(158.4803f, 24f);
			return;
		}

		// Il verbo cambia col rango: "SBLOCCA" su un nodo gia' comprato direbbe al giocatore
		// che sta prendendo una cosa nuova invece di alzare di un rango quella che ha. E il
		// prezzo porta con se' la valuta, altrimenti "SBLOCCA 3" si legge come tre di qualcosa.
		string buyLabel = TalentUiText.BuyLabel(node.rank > 0, node.nextCost);
		Button buy = CreateButton("Talent Buy", bar.transform,
			AccardND.Battlefield.MmoUiTheme.BodyFont,
			talentBuying ? "..." : buyLabel);
		ApplyBattleButtonVariant(buy, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Arcane);
		buy.interactable = !talentBuying;
		string capturedId = node.id;
		buy.onClick.AddListener((UnityAction)delegate
		{
			PlayGenericButtonClickSfx();
			BuyTalent(capturedId);
		});
		SetRect((RectTransform)buy.transform, new Vector2(0.35f, 0.035f), new Vector2(0.65f, 0.3f));
		profileDynamicObjects.Add(buy.gameObject);
	}

	/// <summary>
	/// La descrizione, e sotto l'effetto: quello che il talento fa adesso e quello che farebbe
	/// al rango successivo.
	///
	/// Il confronto e' il punto: prima della spesa il giocatore vedeva solo la descrizione, e
	/// il numero compariva soltanto dopo aver comprato - cioe' quando non serviva piu' a
	/// decidere. Le due scritte arrivano gia' composte dal server, unita' di misura compresa.
	/// I nodi a rango unico non ne hanno nessuna: la loro descrizione dice gia' tutto.
	/// </summary>
	private static string TalentDetailBody(TalentEntryData node)
	{
		string body = TalentUiText.Description(node);
		string effect = TalentEffectLine(TalentUiText.Value(node, node.currentValue), TalentUiText.Value(node, node.nextValue));
		return string.IsNullOrEmpty(effect) ? body : body + "\n" + effect;
	}

	/// <summary>
	/// La riga dell'effetto: rango posseduto, freccia, rango successivo.
	///
	/// L'unita' di misura si scrive una volta sola quando e' la stessa da entrambe le parti,
	/// che e' il caso normale: "Ora -10% → -20% alle soglie dei livelli 4 e 5" si legge in un
	/// colpo d'occhio, mentre la stessa frase ripetuta due volte intera riempie la targa e
	/// costringe a confrontare due periodi per trovare l'unico numero che cambia.
	/// </summary>
	private static string TalentEffectLine(string currentText, string nextText)
	{
		return TalentUiText.DetailEffect(currentText, nextText);
	}

	/// <summary>
	/// La parte finale che due scritte hanno in comune, tagliata all'inizio di una parola.
	///
	/// Il taglio sulla parola non e' un dettaglio: "-10%" e "-20%" condividono anche lo zero,
	/// e una coda presa a caratteri comincerebbe in mezzo a un numero.
	/// </summary>
	private static string CommonWordSuffix(string left, string right)
	{
		int length = 0;
		int maximum = Mathf.Min(left.Length, right.Length);
		while (length < maximum &&
			left[left.Length - 1 - length] == right[right.Length - 1 - length])
		{
			length++;
		}

		while (length > 0)
		{
			int start = left.Length - length;
			if (start > 0 && left[start] != ' ' && left[start - 1] == ' ')
				break;
			length--;
		}

		return length > 0 ? left.Substring(left.Length - length) : string.Empty;
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
		Font questFont = tavernTitleFont
			?? Resources.Load<Font>("Fonts/IMFellEnglishSC")
			?? AccardND.Battlefield.MmoUiTheme.BodyFont;

		for (int index = 0; index < achievements.Length; index++)
		{
			AchievementDto achievement = achievements[index];
			Image card = CreateImage("Achievement " + achievement.achievementId, content,
				new Color(0.035f, 0.028f, 0.021f, 0.94f));
			LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
			cardLayout.preferredHeight = 220f;
			cardLayout.flexibleHeight = 0f;
			profileDynamicObjects.Add(card.gameObject);

			Text info = CreateText("Achievement Info", card.transform, questFont, 31,
				FontStyle.Normal, TextAnchor.MiddleLeft);
			info.resizeTextForBestFit = false;
			info.fontSize = 31;
			info.horizontalOverflow = HorizontalWrapMode.Wrap;
			info.verticalOverflow = VerticalWrapMode.Overflow;
			info.lineSpacing = 0.75f;
			string title = (achievement.name ?? string.Empty).ToUpperInvariant();
			int titleSize = title.Length > 21 ? 24 : 31;
			string titleColor = achievement.unlocked ? "#24E914" : "#E97D14";
			info.text = $"<size={titleSize}><color={titleColor}>{title}</color></size>\n" +
				$"<size=27><color=#FFFFFF>{achievement.description}</color></size>\n" +
				$"<size=23><color=#E7C56F>{achievement.progress} / {achievement.threshold}</color></size>";
			SetRect(info.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.735f, 0.965f));

			Button status = CreateButton("Achievement Status", card.transform, questFont,
				achievement.unlocked ? "COMPLETATO" : "IN CORSO");
			if (achievement.unlocked)
				ApplyBattleButtonVariant(status, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Gold);
			SetRect((RectTransform)status.transform, new Vector2(0.735f, 0.08f), new Vector2(0.99f, 0.92f));
			Text statusLabel = status.GetComponentInChildren<Text>();
			if ((Object)(object)statusLabel != (Object)null)
			{
				statusLabel.font = questFont;
				statusLabel.fontStyle = FontStyle.Bold;
				statusLabel.resizeTextForBestFit = false;
				statusLabel.fontSize = 30;
				statusLabel.resizeTextMinSize = 30;
				statusLabel.resizeTextMaxSize = 30;
			}
			SetTavernButtonInteractable(status, false);
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
				Color.white);
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
		RectTransform badgeRect = (RectTransform)profileMessagesBadge.transform;
		badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
		badgeRect.pivot = new Vector2(0.5f, 0.5f);
		badgeRect.sizeDelta = new Vector2(42f, 42f);
		badgeRect.anchoredPosition = new Vector2(-17.8f, -95.9f);
		badgeRect.localScale = new Vector3(0.6f, 0.6f, 0.75f);
		RectTransform innerRect = (RectTransform)badgeRect.GetChild(0);
		innerRect.sizeDelta = new Vector2(34f, 34f);
		profileMessagesBadgeText.fontSize = 22;
	}

	/// <summary>I punti propoli disponibili da assegnare, visibili direttamente sulla navbar.</summary>
	private void CreateProfileTalentsBadge(Button tab, Font font)
	{
		profileTalentsBadge = CreateProfileNotificationBadge(
			tab, font, "Profile Talents Notification", out profileTalentsBadgeText);
		RectTransform badgeRect = (RectTransform)profileTalentsBadge.transform;
		badgeRect.anchorMin = badgeRect.anchorMax = Vector2.one;
		badgeRect.pivot = new Vector2(0.5f, 0.5f);
		badgeRect.sizeDelta = new Vector2(42f, 42f);
		badgeRect.anchoredPosition = new Vector2(-8f, -8f);
		badgeRect.localScale = new Vector3(0.7f, 0.7f, 1f);
		RectTransform innerRect = (RectTransform)badgeRect.GetChild(0);
		innerRect.sizeDelta = new Vector2(34f, 34f);
		profileTalentsBadgeText.fontSize = 22;
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
		SetProfileBadge(profileTalentsBadge, profileTalentsBadgeText, UnspentTalentPoints());
		SetProfileBadge(profileMessagesBadge, profileMessagesBadgeText, pending);
		// Il badge dell'hub somma i punti talento non spesi: da fuori il profilo il giocatore
		// non ha nessun altro modo di sapere che ha qualcosa da spendere nell'albero, e punti
		// che restano in tasca sono progressione che non si vede.
		SetProfileBadge(profileHubBadge, profileHubBadgeText, pending + UnspentTalentPoints());
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

/// <summary>
/// Superficie di pan del favo: il viewport resta fermo e ritaglia il contenuto ingrandito.
/// </summary>
internal sealed class TalentHiveDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler
{
	private const float MinimumZoom = 0.75f;
	private const float MaximumZoom = 2.25f;
	private RectTransform viewport;
	private RectTransform content;
	private Canvas canvas;
	private Vector2 dragStartPointer;
	private Vector2 dragStartPosition;
	private Action<Vector2> positionChanged;
	private Action<float> zoomChanged;
	private float zoom = 1f;
	private float previousPinchDistance;
	private bool pinching;

	public void Initialize(
		RectTransform viewportRect, RectTransform contentRect,
		Vector2 initialPosition, float initialZoom,
		Action<Vector2> onPositionChanged, Action<float> onZoomChanged)
	{
		viewport = viewportRect;
		content = contentRect;
		canvas = GetComponentInParent<Canvas>();
		positionChanged = onPositionChanged;
		zoomChanged = onZoomChanged;
		zoom = Mathf.Clamp(initialZoom, MinimumZoom, MaximumZoom);
		content.localScale = Vector3.one * zoom;
		content.anchoredPosition = initialPosition;
		ClampContent();
	}

	private void Update()
	{
		ApplyMouseWheelZoom();

		Touchscreen touchscreen = Touchscreen.current;
		if (touchscreen == null || !touchscreen.touches[0].press.isPressed ||
			!touchscreen.touches[1].press.isPressed)
		{
			if (pinching)
				RebaseDragAfterPinch(touchscreen);
			pinching = false;
			previousPinchDistance = 0f;
			return;
		}

		Vector2 firstPosition = touchscreen.touches[0].position.ReadValue();
		Vector2 secondPosition = touchscreen.touches[1].position.ReadValue();
		float distance = Vector2.Distance(firstPosition, secondPosition);
		if (!pinching || previousPinchDistance <= 0f)
		{
			pinching = true;
			previousPinchDistance = distance;
			return;
		}

		float reference = Mathf.Max(240f, Screen.dpi > 0f ? Screen.dpi * 2f : 320f);
		ApplyZoom(zoom + (distance - previousPinchDistance) / reference);
		previousPinchDistance = distance;
	}

	private void RebaseDragAfterPinch(Touchscreen touchscreen)
	{
		if (touchscreen == null || (Object)(object)content == (Object)null)
			return;

		// OnBeginDrag appartiene ancora al gesto iniziato prima del pinch. Quando resta un
		// solo dito, il suo vecchio riferimento farebbe saltare il contenuto fino a quel dito.
		// Ripartiamo invece dalla posizione corrente, mantenendo continuo l'eventuale pan.
		for (int i = 0; i < touchscreen.touches.Count; i++)
		{
			if (!touchscreen.touches[i].press.isPressed)
				continue;
			dragStartPointer = touchscreen.touches[i].position.ReadValue();
			dragStartPosition = content.anchoredPosition;
			return;
		}
	}

	private void ApplyMouseWheelZoom()
	{
		Mouse mouse = Mouse.current;
		if (mouse == null || (Object)(object)viewport == (Object)null)
			return;

		Vector2 pointerPosition = mouse.position.ReadValue();
		Camera eventCamera = null;
		if ((Object)(object)canvas != (Object)null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
			eventCamera = canvas.worldCamera;
		if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, pointerPosition, eventCamera))
			return;

		float wheelDelta = mouse.scroll.ReadValue().y;
		if (!Mathf.Approximately(wheelDelta, 0f))
			ApplyZoom(zoom + Mathf.Sign(wheelDelta) * 0.12f);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if ((Object)(object)content == (Object)null)
			return;
		dragStartPointer = eventData.position;
		dragStartPosition = content.anchoredPosition;
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (pinching || HasMultipleTouches() ||
			(Object)(object)viewport == (Object)null || (Object)(object)content == (Object)null)
			return;
		float scale = (Object)(object)canvas != (Object)null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
		content.anchoredPosition = dragStartPosition + (eventData.position - dragStartPointer) / scale;
		ClampContent();
		positionChanged?.Invoke(content.anchoredPosition);
	}

	private static bool HasMultipleTouches()
	{
		Touchscreen touchscreen = Touchscreen.current;
		return touchscreen != null && touchscreen.touches[0].press.isPressed &&
			touchscreen.touches[1].press.isPressed;
	}

	private void ApplyZoom(float value)
	{
		if ((Object)(object)content == (Object)null)
			return;
		float next = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
		if (Mathf.Approximately(next, zoom))
			return;
		zoom = next;
		content.localScale = Vector3.one * zoom;
		ClampContent();
		zoomChanged?.Invoke(zoom);
		positionChanged?.Invoke(content.anchoredPosition);
	}

	private void ClampContent()
	{
		if ((Object)(object)viewport == (Object)null || (Object)(object)content == (Object)null)
			return;
		float horizontal = Mathf.Max(0f, (content.rect.width * zoom - viewport.rect.width) * 0.5f);
		float vertical = Mathf.Max(0f, (content.rect.height * zoom - viewport.rect.height) * 0.5f);
		Vector2 position = content.anchoredPosition;
		position.x = Mathf.Clamp(position.x, -horizontal, horizontal);
		position.y = Mathf.Clamp(position.y, -vertical, vertical);
		content.anchoredPosition = position;
	}
}

/// <summary>
/// Graphic procedurale usato esclusivamente per scrivere nello stencil della UI. In questo
/// modo ogni artwork quadrato viene ritagliato nello stesso esagono pointy-top della cornice.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class TalentHexMaskGraphic : MaskableGraphic
{
	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();
		Rect rect = GetPixelAdjustedRect();
		Vector2 center = rect.center;
		float radiusX = rect.width * 0.5f;
		float radiusY = rect.height * 0.5f;

		vertexHelper.AddVert(center, color, new Vector2(0.5f, 0.5f));
		for (int index = 0; index < 6; index++)
		{
			float angle = Mathf.Deg2Rad * (90f + index * 60f);
			float cosine = Mathf.Cos(angle);
			float sine = Mathf.Sin(angle);
			Vector2 point = center + new Vector2(cosine * radiusX, sine * radiusY);
			Vector2 uv = new Vector2(cosine * 0.5f + 0.5f, sine * 0.5f + 0.5f);
			vertexHelper.AddVert(point, color, uv);
		}

		for (int index = 0; index < 6; index++)
			vertexHelper.AddTriangle(0, index + 1, (index + 1) % 6 + 1);
	}
}

/// <summary>
/// Tubi di progressione e bordi esagonali completati. Il mesh e' procedurale per seguire
/// senza texture aggiuntive il ridimensionamento 4:3, il pan e lo zoom del favo.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class TalentHiveProgressGraphic : MaskableGraphic
{
	private readonly List<Tube> tubes = new();
	private readonly List<CompletedHex> completedHexes = new();

	private readonly struct Tube
	{
		public Tube(Vector2 start, Vector2 end, Color color)
		{
			Start = start;
			End = end;
			Color = color;
		}
		public Vector2 Start { get; }
		public Vector2 End { get; }
		public Color Color { get; }
	}

	private readonly struct CompletedHex
	{
		public CompletedHex(Vector2 center, Vector2 radius, Color color)
		{
			Center = center;
			Radius = radius;
			Color = color;
		}
		public Vector2 Center { get; }
		public Vector2 Radius { get; }
		public Color Color { get; }
	}

	public void AddTube(Vector2 start, Vector2 end, Color tubeColor) =>
		tubes.Add(new Tube(start, end, tubeColor));

	public void AddCompletedHex(Vector2 center, Vector2 radius, Color glowColor) =>
		completedHexes.Add(new CompletedHex(center, radius, glowColor));

	public void Commit() => SetVerticesDirty();

	private void Update()
	{
		if (completedHexes.Count > 0 || tubes.Count > 0)
			SetVerticesDirty();
	}

	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();
		Rect rect = GetPixelAdjustedRect();
		float pulse = 0.82f + Mathf.Sin(Time.unscaledTime * 3.4f) * 0.18f;
		float baseSize = Mathf.Min(rect.width, rect.height);

		for (int tubeIndex = 0; tubeIndex < tubes.Count; tubeIndex++)
		{
			Tube tube = tubes[tubeIndex];
			Vector2 direction = (tube.End - tube.Start).normalized;
			float startGap = Vector2.Distance(tube.Start, new Vector2(0.5f, 0.495f)) < 0.01f
				? 0.105f : 0.064f;
			Vector2 start = NormalizedToPixel(rect, tube.Start + direction * startGap);
			Vector2 end = NormalizedToPixel(rect, tube.End - direction * 0.064f);
			AddRoundedLine(vertexHelper, start, end, baseSize * 0.014f,
				WithAlpha(tube.Color, 0.12f * pulse));
			AddRoundedLine(vertexHelper, start, end, baseSize * 0.006f,
				WithAlpha(tube.Color, 0.68f * pulse));
			AddRoundedLine(vertexHelper, start, end, baseSize * 0.0018f,
				new Color(1f, 1f, 1f, 0.38f * pulse));

			// Una scintilla percorre il condotto, poi resta un breve intervallo buio prima
			// del passaggio successivo. Le copie arretrate formano una coda morbida.
			float travel = Mathf.Repeat(Time.unscaledTime * 0.55f + tubeIndex * 0.19f, 1.35f);
			if (travel <= 1f)
			{
				float eased = travel * travel * (3f - 2f * travel);
				for (int trail = 0; trail < 4; trail++)
				{
					float trailProgress = eased - trail * 0.035f;
					if (trailProgress < 0f)
						continue;
					Vector2 position = Vector2.Lerp(start, end, trailProgress);
					float strength = 1f - trail / 4f;
					AddCircle(vertexHelper, position, baseSize * (0.011f - trail * 0.0015f),
						WithAlpha(tube.Color, 0.18f * strength), 16);
					AddCircle(vertexHelper, position, baseSize * (0.0048f - trail * 0.0005f),
						new Color(1f, 1f, 1f, 0.9f * strength), 14);
				}
			}
		}

		foreach (CompletedHex completed in completedHexes)
		{
			Vector2[] points = new Vector2[6];
			for (int index = 0; index < 6; index++)
			{
				float angle = Mathf.Deg2Rad * (90f + index * 60f);
				Vector2 normalized = completed.Center + new Vector2(
					Mathf.Cos(angle) * completed.Radius.x,
					Mathf.Sin(angle) * completed.Radius.y);
				points[index] = NormalizedToPixel(rect, normalized);
			}
			for (int index = 0; index < 6; index++)
			{
				Vector2 start = points[index];
				Vector2 end = points[(index + 1) % 6];
				AddRoundedLine(vertexHelper, start, end, baseSize * 0.014f,
					WithAlpha(completed.Color, 0.14f * pulse));
				AddRoundedLine(vertexHelper, start, end, baseSize * 0.005f,
					WithAlpha(completed.Color, 0.78f * pulse));
			}
		}
	}

	private static Vector2 NormalizedToPixel(Rect rect, Vector2 normalized) =>
		new(rect.xMin + normalized.x * rect.width, rect.yMin + normalized.y * rect.height);

	private static Color WithAlpha(Color source, float alpha) =>
		new(source.r, source.g, source.b, Mathf.Clamp01(alpha));

	private static void AddLine(
		VertexHelper vertexHelper, Vector2 start, Vector2 end, float width, Color lineColor)
	{
		Vector2 direction = end - start;
		if (direction.sqrMagnitude < 0.01f)
			return;
		Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
		int first = vertexHelper.currentVertCount;
		vertexHelper.AddVert(start - normal, lineColor, Vector2.zero);
		vertexHelper.AddVert(start + normal, lineColor, Vector2.up);
		vertexHelper.AddVert(end + normal, lineColor, Vector2.one);
		vertexHelper.AddVert(end - normal, lineColor, Vector2.right);
		vertexHelper.AddTriangle(first, first + 1, first + 2);
		vertexHelper.AddTriangle(first, first + 2, first + 3);
	}

	private static void AddRoundedLine(
		VertexHelper vertexHelper, Vector2 start, Vector2 end, float width, Color lineColor)
	{
		AddLine(vertexHelper, start, end, width, lineColor);
		AddCircle(vertexHelper, start, width * 0.5f, lineColor, 12);
		AddCircle(vertexHelper, end, width * 0.5f, lineColor, 12);
	}

	private static void AddCircle(
		VertexHelper vertexHelper, Vector2 center, float radius, Color circleColor, int sides)
	{
		if (radius <= 0f || sides < 3)
			return;
		int first = vertexHelper.currentVertCount;
		vertexHelper.AddVert(center, circleColor, new Vector2(0.5f, 0.5f));
		for (int index = 0; index < sides; index++)
		{
			float angle = Mathf.PI * 2f * index / sides;
			Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			vertexHelper.AddVert(center + radial * radius, circleColor, radial * 0.5f + Vector2.one * 0.5f);
		}
		for (int index = 0; index < sides; index++)
			vertexHelper.AddTriangle(first, first + index + 1, first + (index + 1) % sides + 1);
	}
}
}
