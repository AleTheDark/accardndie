using System;
using System.Collections;
using System.Collections.Generic;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using AccardND.Ads;
using AccardND.NetProtocol;
using AccardND.Network;

namespace AccardND.Presentation
{
	partial class BattleBoardController
	{
		private GameObject tavernPanel;
		private RectTransform tavernQuestContent;
		private Text tavernRefreshText;
		private Text tavernNoticeText;

		/// <summary>
		/// Cosa c'e' scritto al posto delle quest quando la bacheca e' vuota: sta caricando,
		/// il server non risponde, oppure oggi non c'e' davvero niente. Senza, un'attesa di
		/// rete e un fallimento erano la stessa identica schermata vuota.
		/// </summary>
		private Text tavernBoardMessageText;
		private Text tavernCompletionText;
		private AccardND.Battlefield.ArcaneExperienceFillGraphic tavernCompletionFill;
		private Coroutine tavernCompletionFillRoutine;
		private Button tavernBonusButton;
		private GameObject tavernNotificationBadge;
		private Text tavernNotificationBadgeText;
		private Font tavernTitleFont;
		private Coroutine tavernNoticeClearRoutine;
		private readonly List<GameObject> tavernQuestRows = new List<GameObject>();

		/// <summary>Ultima bacheca ricevuta dal server: e' l'unica verita' che la schermata disegna.</summary>
		private TavernData tavernData;
		private bool tavernLoading;
		private bool tavernClaiming;

		/// <summary>
		/// La quest la cui riscossione e' in volo. Serve a tenere spenta quella riga anche
		/// quando la bacheca viene ridisegnata nel frattempo, e a dire cosa sta succedendo:
		/// un bottone che sparisce e basta sembra un tocco andato perso.
		/// </summary>
		private string tavernClaimingQuestId;
		private bool tavernBadgeLoading;
		private float nextTavernBadgeRefreshAt;
		private const float TavernBadgeRefreshIntervalSeconds = 60f;

		/// <summary>
		/// Quando si puo' ritentare la bacheca restando dentro la taverna. Prima di questo
		/// una richiesta andata a vuoto restava a vuoto: l'unico modo di uscirne era fare
		/// un giro dall'arena, che e' l'unica schermata capace di rimettere in piedi la
		/// sessione da sola.
		/// </summary>
		private float nextTavernRetryAt;
		private const float TavernRetryIntervalSeconds = 6f;

		/// <summary>
		/// L'ultima richiesta non e' arrivata: quello a schermo e' l'ultimo stato buono, non
		/// quello di adesso. Serve perche' anche una bacheca vecchia rimasta a video vada
		/// riprovata, non solo lo schermo vuoto.
		/// </summary>
		private bool tavernBoardStale;

		private void CreateTavernNotificationBadge(Button tavernButton, Font font)
		{
			Sprite circleSprite = AccardND.Battlefield.MmoUiTheme.GetSolidCircleSprite();
			Image badgeFrame = CreateImage("Tavern Quest Notification", tavernButton.transform, Color.black);
			badgeFrame.sprite = circleSprite;
			badgeFrame.type = Image.Type.Simple;
			badgeFrame.raycastTarget = false;

			RectTransform badgeRect = badgeFrame.rectTransform;
			// Il banner usa preserveAspect: il suo bordo visibile e il bordo del RectTransform
			// non coincidono. Queste ancore tengono il badge dentro la parte disegnata.
			badgeRect.anchorMin = new Vector2(0.86f, 0.78f);
			badgeRect.anchorMax = new Vector2(0.86f, 0.78f);
			badgeRect.pivot = new Vector2(0.5f, 0.5f);
			badgeRect.localScale = new Vector3(0.8f, 0.8f, 1f);
			badgeRect.sizeDelta = new Vector2(58f, 58f);
			badgeRect.anchoredPosition = new Vector2(-26.7f, -11.9f);

			Outline frameOutline = badgeFrame.gameObject.AddComponent<Outline>();
			frameOutline.effectColor = Color.black;
			frameOutline.effectDistance = new Vector2(1f, -1f);

			Shadow frameShadow = badgeFrame.gameObject.AddComponent<Shadow>();
			frameShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
			frameShadow.effectDistance = new Vector2(2f, -2f);

			Image badge = CreateImage(
				"Tavern Quest Notification Inner", badgeFrame.transform,
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

			tavernNotificationBadgeText = CreateText(
				"Tavern Quest Notification Count", badge.transform, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
			tavernNotificationBadgeText.color = Color.white;
			tavernNotificationBadgeText.raycastTarget = false;
			Stretch(tavernNotificationBadgeText.rectTransform);
			tavernNotificationBadge = badgeFrame.gameObject;
			tavernNotificationBadge.SetActive(false);
		}

		private void UpdateTavernNotificationBadge(TavernData data)
		{
			if (tavernNotificationBadge == null || tavernNotificationBadgeText == null)
				return;

			int claimable = 0;
			TavernQuestData[] quests = data?.quests ?? Array.Empty<TavernQuestData>();
			for (int i = 0; i < quests.Length; i++)
			{
				if (quests[i] != null && quests[i].completed && !quests[i].claimed)
					claimable++;
			}
			if (data != null && data.bonusAvailable && !data.bonusClaimed)
				claimable++;

			tavernNotificationBadgeText.text = claimable > 9 ? "9+" : claimable.ToString();
			tavernNotificationBadge.SetActive(claimable > 0);
			tavernNotificationBadge.transform.SetAsLastSibling();
		}

		private async System.Threading.Tasks.Task RefreshTavernNotificationBadgeAsync()
		{
			if (tavernBadgeLoading || tavernClaiming)
				return;

			tavernBadgeLoading = true;
			try
			{
				if (await EnsureServerProgressAsync())
				{
					tavernData = await serverProgress.GetTavernAsync();
					// La bacheca arrivata col badge e' la stessa che la taverna disegnera'
					// all'apertura: da qui in poi non e' piu' roba vecchia.
					tavernBoardStale = false;
					UpdateTavernNotificationBadge(tavernData);
				}
				else
				{
					UpdateTavernNotificationBadge(null);
				}
			}
			catch (Exception exception)
			{
				UpdateTavernNotificationBadge(null);
                AppendLog(GameText.Format(GameTextKeys.Tavern.BadgeRefreshFailedLog, exception.Message));
			}
			finally
			{
				tavernBadgeLoading = false;
				nextTavernBadgeRefreshAt = Time.unscaledTime + TavernBadgeRefreshIntervalSeconds;
			}
		}

		/// <summary>
		/// Tiene la taverna allineata al server senza che il giocatore debba uscire e
		/// rientrare. Due lavori distinti: dentro la taverna, se la bacheca e' ancora vuota,
		/// si riprova la richiesta finche' non arriva; nell'Hub si aggiorna il badge, che
		/// altrimenti resterebbe indietro se la sessione e' diventata pronta - o una quest
		/// e' stata completata altrove - mentre il giocatore era gia' li' fermo.
		/// </summary>
		private void UpdateTavernServerRefresh()
		{
			if (tavernPanel != null && tavernPanel.activeInHierarchy)
			{
				if (!tavernLoading && !tavernClaiming && (tavernData == null || tavernBoardStale)
					&& Time.unscaledTime >= nextTavernRetryAt)
					_ = RefreshTavernFromServerAsync();
				return;
			}

			if (!IsAccountHubVisible())
			{
				nextTavernBadgeRefreshAt = 0f;
				return;
			}

			if (!tavernBadgeLoading && Time.unscaledTime >= nextTavernBadgeRefreshAt)
				_ = RefreshTavernNotificationBadgeAsync();
		}

		private void CreateTavernView(Transform canvasTransform, Font fallbackFont)
		{
			Image root = CreateImage("Tavern", canvasTransform, Color.white);
			Stretch(root.rectTransform);
			root.sprite = LoadSpriteResource("UI/CampaignRestyle/tavern_background");
			root.type = Image.Type.Simple;
			root.preserveAspect = false;
			tavernPanel = root.gameObject;

			Canvas canvas = root.gameObject.AddComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = 905;
			root.gameObject.AddComponent<GraphicRaycaster>();

			Image shade = CreateImage("Tavern Veil", root.transform, new Color(0f, 1f / 255f, 4f / 255f, 0.6f));
			SetRect(shade.rectTransform, Vector2.zero, Vector2.one);
			shade.raycastTarget = false;

			Image outerFrame = CreateImage("Tavern Outer Frame", root.transform, Color.white);
			AccardND.Battlefield.MmoUiTheme.ApplyScreenOuterFrame(outerFrame);
			SetRect(outerFrame.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.775f));

			tavernTitleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
			if (tavernTitleFont == null)
				tavernTitleFont = fallbackFont;

			Image titlePanel = CreateImage("Tavern Title Panel", root.transform, Color.white);
			titlePanel.sprite = AccardND.Battlefield.MmoUiTheme.GetScreenTitlePlaqueSprite();
			titlePanel.type = Image.Type.Simple;
			titlePanel.preserveAspect = false;
			titlePanel.raycastTarget = false;
			SetRect(titlePanel.rectTransform, new Vector2(0.08f, 0.775f), new Vector2(0.92f, 0.895f));

			Text title = CreateText("Tavern Title", titlePanel.transform, tavernTitleFont, 50, FontStyle.Normal, TextAnchor.MiddleCenter);
			AccardND.Battlefield.MmoUiTheme.StyleAsScreenTitle(title);
			title.font = tavernTitleFont;
			SetLocalizedText(title, GameTextKeys.Tavern.Title, "TAVERNA");
			title.color = new Color(0.96f, 0.72f, 0.24f, 1f);
			title.supportRichText = false;
			SetRect(title.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.82f));
			title.rectTransform.offsetMin = new Vector2(0f, -27f);
			title.rectTransform.offsetMax = new Vector2(0f, -27f);

			Button back = CreateTransparentButton("Tavern Back", root.transform);
			SetRect((RectTransform)back.transform, new Vector2(0.015f, 0.91f), new Vector2(0.12f, 0.985f));
			Text backLabel = CreateText("Tavern Back Label", back.transform, tavernTitleFont, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
			backLabel.text = "‹";
			backLabel.color = new Color(0.95f, 0.72f, 0.25f, 1f);
			Stretch(backLabel.rectTransform);
			back.onClick.AddListener((UnityAction)ReturnFromTavern);

			Text section = CreateText("Daily Quests Header", root.transform, tavernTitleFont, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetLocalizedText(section, GameTextKeys.Tavern.DailyQuests, "QUEST GIORNALIERE");
			section.color = new Color(0.92f, 0.68f, 0.22f, 1f);
			Outline sectionOutline = section.gameObject.AddComponent<Outline>();
			sectionOutline.effectColor = new Color(0f, 0f, 0f, 1f);
			sectionOutline.effectDistance = new Vector2(2f, -2f);
			SetRect(section.rectTransform, new Vector2(0.065f, 0.69f), new Vector2(0.61f, 0.745f));
			section.rectTransform.offsetMin = new Vector2(0f, -28f);
			section.rectTransform.offsetMax = new Vector2(0f, -28f);

			tavernRefreshText = CreateText("Daily Quests Refresh", root.transform, tavernTitleFont, 20, FontStyle.Normal, TextAnchor.MiddleRight);
			tavernRefreshText.color = new Color(0.72f, 0.62f, 0.42f, 1f);
			SetRect(tavernRefreshText.rectTransform, new Vector2(0.58f, 0.69f), new Vector2(0.935f, 0.745f));
			tavernRefreshText.rectTransform.offsetMin = new Vector2(0f, -27f);
			tavernRefreshText.rectTransform.offsetMax = new Vector2(0f, -27f);

			// L'oste parla qui: il pannello messaggi della battaglia non e' visibile in taverna.
			tavernNoticeText = CreateText("Tavern Notice", root.transform, tavernTitleFont, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
			tavernNoticeText.color = new Color(0.86f, 0.74f, 0.52f, 1f);
			tavernNoticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
			SetRect(tavernNoticeText.rectTransform, new Vector2(0.08f, 0.025f), new Vector2(0.92f, 0.105f));

			Image progressTrack = CreateImage("Tavern Daily Progress", root.transform, new Color(0.12f, 0.07f, 0.02f, 0.96f));
			StylePanel(progressTrack);
			SetRect(progressTrack.rectTransform, new Vector2(0.075f, 0.637f), new Vector2(0.62f, 0.674f));

			Image progressMaskImage = CreateImage("Tavern Daily Progress Mask", progressTrack.transform, Color.white);
			Stretch(progressMaskImage.rectTransform, 4f);
			progressMaskImage.raycastTarget = false;
			Mask progressMask = progressMaskImage.gameObject.AddComponent<Mask>();
			progressMask.showMaskGraphic = false;

			GameObject progressFillObject = new GameObject(
				"Tavern Daily Progress Fill",
				typeof(RectTransform),
				typeof(CanvasRenderer),
				typeof(AccardND.Battlefield.ArcaneExperienceFillGraphic));
			progressFillObject.transform.SetParent(progressMaskImage.transform, false);
			tavernCompletionFill = progressFillObject.GetComponent<AccardND.Battlefield.ArcaneExperienceFillGraphic>();
			tavernCompletionFill.SetPalette(
				new Color(0.28f, 0.055f, 0.005f, 1f),
				new Color(0.94f, 0.28f, 0.015f, 1f),
				new Color(1f, 0.72f, 0.12f, 1f));
			SetRect(tavernCompletionFill.rectTransform, Vector2.zero, new Vector2(0f, 1f));
			tavernCompletionFill.raycastTarget = false;

			Font tavernProgressFont = Resources.Load<Font>("Fonts/AlegreyaSansBold") ?? tavernTitleFont;
			tavernCompletionText = CreateText("Tavern Daily Progress Text", root.transform, tavernProgressFont, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
			tavernCompletionText.color = new Color(0.92f, 0.75f, 0.38f, 1f);
			SetRect(tavernCompletionText.rectTransform, new Vector2(0.62f, 0.63f), new Vector2(0.72f, 0.68f));
			tavernBonusButton = CreateButton("Tavern Daily Bonus", root.transform, tavernTitleFont, "Bonus\n+50");
			Text tavernBonusLabel = tavernBonusButton.GetComponentInChildren<Text>();
			if (tavernBonusLabel != null)
			{
				tavernBonusLabel.font = tavernTitleFont;
				tavernBonusLabel.fontStyle = FontStyle.Bold;
			}
			SetTavernButtonInteractable(tavernBonusButton, false);
			SetRect((RectTransform)tavernBonusButton.transform, new Vector2(0.73f, 0.625f), new Vector2(0.93f, 0.685f));
			tavernBonusButton.onClick.AddListener((UnityAction)ClaimTavernBonus);

			GameObject scrollObject = new GameObject("Tavern Quest Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
			scrollObject.transform.SetParent(root.transform, false);
			RectTransform scrollRect = (RectTransform)scrollObject.transform;
			SetRect(scrollRect, new Vector2(0.055f, 0.12f), new Vector2(0.945f, 0.61f));
			scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.16f);
			scrollObject.GetComponent<Mask>().showMaskGraphic = true;

			GameObject contentObject = new GameObject("Tavern Quest Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			contentObject.transform.SetParent(scrollObject.transform, false);
			tavernQuestContent = (RectTransform)contentObject.transform;
			tavernQuestContent.anchorMin = new Vector2(0f, 1f);
			tavernQuestContent.anchorMax = new Vector2(1f, 1f);
			tavernQuestContent.pivot = new Vector2(0.5f, 1f);
			tavernQuestContent.offsetMin = new Vector2(8f, 0f);
			tavernQuestContent.offsetMax = new Vector2(-8f, 0f);
			VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
			layout.spacing = 12f;
			layout.padding = new RectOffset(5, 5, 10, 10);
			layout.childControlHeight = false;
			layout.childForceExpandHeight = false;
			contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			ScrollRect scrolling = scrollObject.GetComponent<ScrollRect>();
			scrolling.content = tavernQuestContent;
			scrolling.viewport = scrollRect;
			scrolling.horizontal = false;
			scrolling.movementType = ScrollRect.MovementType.Clamped;

			// Vive sopra la lista, non dentro: deve restare leggibile anche quando il
			// contenuto scorrevole e' vuoto, che e' esattamente il caso in cui serve.
			tavernBoardMessageText = CreateText(
				"Tavern Board Message", root.transform, tavernTitleFont, 24, FontStyle.Normal, TextAnchor.MiddleCenter);
			tavernBoardMessageText.color = new Color(0.86f, 0.74f, 0.52f, 1f);
			tavernBoardMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
			tavernBoardMessageText.raycastTarget = false;
			SetRect(tavernBoardMessageText.rectTransform, new Vector2(0.09f, 0.12f), new Vector2(0.91f, 0.61f));
			tavernBoardMessageText.gameObject.SetActive(false);

			tavernPanel.SetActive(false);
		}

		private async void ShowTavern()
		{
			modeSelectionPanel.SetActive(false);
			SetAccountHubHudActive(true);
			tavernPanel.SetActive(true);
			// Gli annunci della taverna si chiedono qui e non all'avvio del gioco: chi apre la
			// taverna riscuote quasi sempre qualcosa, chi non la apre non deve costare una
			// richiesta. I secondi che passano fra l'apertura e il primo tocco su RISCUOTI
			// sono anche il tempo che serve alla rete per rispondere.
			AdService.Warm(AdPlacement.TavernQuestClaim);
			AdService.Warm(AdPlacement.TavernBonusClaim);
			// La bacheca che abbiamo gia' in mano si disegna subito: il badge dell'Hub la
			// scarica intera ogni minuto, ed e' sempre roba del server. Tenerla nascosta
			// dietro una schermata vuota mentre si aspetta la rete era buona parte di quel
			// "clicco taverna e non ho i dati".
			if (tavernData != null)
				ApplyTavernData(tavernData);
			else
				SetTavernBoardMessage(TavernLoadingMessage());
			await RefreshTavernFromServerAsync();
		}

		private static string TavernLoadingMessage() => GameText.Get(GameTextKeys.Tavern.Loading);

		/// <summary>
		/// Scrive al posto delle quest, o toglie il messaggio passando null. E' l'unica cosa
		/// che distingue "sto aspettando" da "non e' arrivato niente".
		/// </summary>
		private void SetTavernBoardMessage(string message)
		{
			if (tavernBoardMessageText == null)
				return;

			tavernBoardMessageText.text = message ?? string.Empty;
			tavernBoardMessageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
		}

		private void ReturnFromTavern()
		{
			tavernPanel.SetActive(false);
			AdService.Cool(AdPlacement.TavernQuestClaim);
			AdService.Cool(AdPlacement.TavernBonusClaim);
			ShowHubFromSinglePlayer();
		}

		private void SetTavernNotice(string notice)
		{
			if (tavernNoticeText == null)
				return;

			if (tavernNoticeClearRoutine != null)
			{
				StopCoroutine(tavernNoticeClearRoutine);
				tavernNoticeClearRoutine = null;
			}

			tavernNoticeText.text = notice ?? string.Empty;
			if (!string.IsNullOrEmpty(tavernNoticeText.text))
				tavernNoticeClearRoutine = StartCoroutine(ClearTavernNoticeAfterDelay());
		}

		private IEnumerator ClearTavernNoticeAfterDelay()
		{
			yield return new WaitForSecondsRealtime(15f);
			if (tavernNoticeText != null)
				tavernNoticeText.text = string.Empty;
			tavernNoticeClearRoutine = null;
		}

		/// <summary>
		/// Disegna la bacheca ricevuta dal server. Il client non decide niente: quali quest
		/// sono di oggi, quanto manca alla soglia e se il premio spetta lo dice il server.
		/// </summary>
		private void ApplyTavernData(TavernData data)
		{
			tavernData = data;
			UpdateTavernNotificationBadge(data);

			for (int i = 0; i < tavernQuestRows.Count; i++)
				Destroy(tavernQuestRows[i]);
			tavernQuestRows.Clear();

			TavernQuestData[] quests = data?.quests ?? Array.Empty<TavernQuestData>();
			// Le quest riscosse finiscono in fondo alla bacheca e spente: non c'e' piu' niente
			// da fare su di loro, e in cima devono restare quelle che chiedono un tocco. Due
			// passate invece di un ordinamento per tenere l'ordine deciso dal server dentro
			// ciascuno dei due gruppi.
			for (int i = 0; i < quests.Length; i++)
			{
				if (quests[i] != null && !quests[i].claimed)
					CreateTavernQuestRow(quests[i]);
			}
			for (int i = 0; i < quests.Length; i++)
			{
				if (quests[i] != null && quests[i].claimed)
					CreateTavernQuestRow(quests[i]);
			}

			SetTavernBoardMessage(quests.Length > 0
				? null
				: data != null
					? GameText.Get(GameTextKeys.Tavern.NoQuests)
					: tavernLoading
						? TavernLoadingMessage()
						: GameText.Get(GameTextKeys.Tavern.Unavailable));

			// Il conto alla rovescia arriva dal server: l'orologio del dispositivo puo' essere
			// sfasato e mostrerebbe un rinnovo che non corrisponde a quello vero.
			int seconds = Mathf.Max(0, data?.secondsToRefresh ?? 0);
            tavernRefreshText.text = GameText.Format(GameTextKeys.Tavern.RefreshCountdown, seconds / 3600, seconds % 3600 / 60);

			// Il bonus usa punti pesati per difficolta': facile 1, intermedia 2, avanzata 3.
			// I campi legacy permettono di mostrare correttamente anche la risposta di un
			// server precedente durante un aggiornamento client/server non simultaneo.
			int required = data != null && data.bonusPointsRequired > 0
				? data.bonusPointsRequired
				: Mathf.Max(1, data?.questsRequiredForBonus ?? quests.Length);
			int completed = data != null && data.bonusPointsRequired > 0
				? data.completedBonusPoints
				: data?.completedCount ?? 0;
			tavernCompletionText.text = $"{Mathf.Min(completed, required)}/{required}";
			AnimateTavernCompletionFill(Mathf.Clamp01((float)completed / required));

			bool bonusClaimable = data != null && data.bonusAvailable && !data.bonusClaimed;
			SetTavernButtonInteractable(tavernBonusButton, bonusClaimable && !tavernClaiming);
			Text bonusLabel = tavernBonusButton.GetComponentInChildren<Text>();
			if (bonusLabel != null)
				bonusLabel.text = "Bonus\n+50";

			RefreshAccountBannerView();
		}

		private void AnimateTavernCompletionFill(float targetFill)
		{
			if (tavernCompletionFill == null)
				return;

			targetFill = Mathf.Clamp01(targetFill);
			if (tavernCompletionFillRoutine != null)
				StopCoroutine(tavernCompletionFillRoutine);
			tavernCompletionFillRoutine = StartCoroutine(AnimateTavernCompletionFillRoutine(targetFill));
		}

		private IEnumerator AnimateTavernCompletionFillRoutine(float targetFill)
		{
			RectTransform fillRect = tavernCompletionFill.rectTransform;
			float startFill = fillRect.anchorMax.x;
			float distance = Mathf.Abs(targetFill - startFill);
			float duration = Mathf.Lerp(0.28f, 1.05f, Mathf.SmoothStep(0f, 1f, distance));
			float elapsed = 0f;

			while (elapsed < duration && tavernCompletionFill != null)
			{
				elapsed += Time.unscaledDeltaTime;
				float normalized = Mathf.Clamp01(elapsed / duration);
				float eased = normalized * normalized * (3f - 2f * normalized);
				fillRect.anchorMax = new Vector2(Mathf.LerpUnclamped(startFill, targetFill, eased), 1f);
				yield return null;
			}

			if (tavernCompletionFill != null)
				fillRect.anchorMax = new Vector2(targetFill, 1f);
			tavernCompletionFillRoutine = null;
		}

		private void CreateTavernQuestRow(TavernQuestData quest)
		{
			Image row = CreateImage(
				"Quest " + quest.questId,
				tavernQuestContent,
				quest.claimed
					? new Color(0.02f, 0.016f, 0.012f, 1f)
					: new Color(0.035f, 0.028f, 0.021f, 0.94f));
			LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
			element.preferredHeight = 220f;
			tavernQuestRows.Add(row.gameObject);

			// Il CanvasGroup spegne la riga intera - testi con i loro tag colore compresi, che
			// ignorerebbero Text.color - senza dover ricalcolare ogni colore per il caso riscosso.
			if (quest.claimed)
			{
				CanvasGroup dimmed = row.gameObject.AddComponent<CanvasGroup>();
				dimmed.alpha = 0.45f;
				dimmed.interactable = false;
			}

			Text info = CreateText("Quest Info", row.transform, tavernTitleFont, 31, FontStyle.Normal, TextAnchor.MiddleLeft);
			info.resizeTextForBestFit = false;
			info.fontSize = 31;
			info.horizontalOverflow = HorizontalWrapMode.Wrap;
			info.verticalOverflow = VerticalWrapMode.Overflow;
			info.lineSpacing = 0.75f;
			string titleKey = string.IsNullOrWhiteSpace(quest.titleKey)
				? GameTextKeys.Tavern.QuestTitle(quest.questId)
				: quest.titleKey;
			string descriptionKey = string.IsNullOrWhiteSpace(quest.descriptionKey)
				? GameTextKeys.Tavern.QuestDescription(quest.questId)
				: quest.descriptionKey;
			string localizedQuestTitle = GameText.GetOrFallbackSilent(titleKey, quest.title);
			string localizedQuestDescription = GameText.GetOrFallbackSilent(descriptionKey, quest.description);
			string questTitle = localizedQuestTitle.ToUpperInvariant();
			string questTitleColor = quest.difficulty switch
			{
				TavernQuestDifficulty.Advanced => "#E31B1B",
				TavernQuestDifficulty.Intermediate => "#E97D14",
				_ => "#24E914"
			};
			int questTitleSize = questTitle.Length > 21 ? 24 : 31;
			info.text = $"<size={questTitleSize}><color={questTitleColor}>{questTitle}</color></size>\n<size=27><color=#FFFFFF>{localizedQuestDescription}</color></size>\n<size=23><color=#E7C56F>{quest.current} / {quest.threshold}  •  +{Mathf.Max(1, quest.bonusPoints)} PT</color></size>";
			SetRect(info.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.59f, 0.965f));

			Text reward = CreateText("Honey Reward", row.transform, tavernTitleFont, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            reward.text = GameText.Format(GameTextKeys.Tavern.HoneyReward, quest.honeyReward);
			SetRect(reward.rectTransform, new Vector2(0.575f, 0.1f), new Vector2(0.755f, 0.9f));

            string label = quest.claimed
                ? GameText.Get(GameTextKeys.Tavern.QuestClaimed)
				: GameText.Get(GameTextKeys.Tavern.QuestInProgress);
			bool claimable = quest.completed && !quest.claimed;
			// Riscossione in volo: al posto di x1/x5 c'e' una sola targa spenta. Finche' il
			// server non ha risposto non c'e' niente da premere, e il giocatore deve vedere
			// perche': la pubblicita' l'ha gia' guardata, e ripremere gliene costerebbe
			// un'altra per sentirsi poi dire che la ricompensa era gia' sua.
			if (!string.IsNullOrEmpty(tavernClaimingQuestId) && tavernClaimingQuestId == quest.questId)
			{
				Button pending = CreateButton(
					"Quest Claiming", row.transform, tavernTitleFont,
					GameText.Get(GameTextKeys.Tavern.QuestClaiming));
				SetRect((RectTransform)pending.transform, new Vector2(0.735f, 0.08f), new Vector2(0.99f, 0.92f));
				ConfigureTavernClaimButton(pending);
				SetTavernButtonInteractable(pending, false);
				return;
			}
			if (claimable)
			{
				string questId = quest.questId;
				Button normalClaim = CreateButton("Quest Claim x1", row.transform, tavernTitleFont, "x1");
				Button videoClaim = CreateButton("Quest Claim x5 Video", row.transform, tavernTitleFont, "x5");
				ApplyBattleButtonVariant(normalClaim, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Gold);
				ApplyBattleButtonVariant(videoClaim, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Gold);
				SetRect((RectTransform)normalClaim.transform, new Vector2(0.735f, 0.08f), new Vector2(0.855f, 0.92f));
				SetRect((RectTransform)videoClaim.transform, new Vector2(0.865f, 0.08f), new Vector2(0.99f, 0.92f));
				ConfigureTavernClaimButton(normalClaim);
				ConfigureTavernClaimButton(videoClaim);
				SetTavernButtonInteractable(normalClaim, !tavernClaiming);
				SetTavernButtonInteractable(videoClaim, !tavernClaiming);
				normalClaim.onClick.AddListener((UnityAction)(() => ClaimTavernQuest(questId, false)));
				videoClaim.onClick.AddListener((UnityAction)(() => ClaimTavernQuest(questId, true)));
				return;
			}

			Button action = CreateButton("Quest Action", row.transform, tavernTitleFont, label);
			SetRect((RectTransform)action.transform, new Vector2(0.735f, 0.08f), new Vector2(0.99f, 0.92f));
			ConfigureTavernClaimButton(action);
			SetTavernButtonInteractable(action, false);
		}

		private void ConfigureTavernClaimButton(Button action)
		{
			Text actionLabel = action.GetComponentInChildren<Text>();
			if (actionLabel != null)
			{
				actionLabel.font = tavernTitleFont;
				actionLabel.fontStyle = FontStyle.Bold;
				actionLabel.resizeTextForBestFit = false;
				actionLabel.fontSize = 30;
				actionLabel.resizeTextMinSize = 30;
				actionLabel.resizeTextMaxSize = 30;
			}
		}

		/// <summary>
		/// I Button usano una transizione colore di 0,11 secondi. Quando una riga nasce gia'
		/// disabilitata, Unity sfumerebbe comunque dal colore acceso a quello disabilitato,
		/// producendo un flash visibile. La seconda transizione a durata zero ferma quel tween.
		/// </summary>
		private static void SetTavernButtonInteractable(Button button, bool interactable)
		{
			button.interactable = interactable;
			if (interactable || button.targetGraphic == null)
				return;

			ColorBlock colors = button.colors;
			button.targetGraphic.CrossFadeColor(
				colors.disabledColor * colors.colorMultiplier,
				0f,
				true,
				true);
		}

		/// <summary>
		/// Scarica la bacheca dal server. Senza connessione la taverna resta visitabile ma
		/// senza quest nuove: mostrare quest da una cache locale darebbe progressi che il
		/// server puo' poi smentire, e qui si paga miele.
		///
		/// Non e' piu' un colpo solo: se va a vuoto, il ciclo di
		/// <see cref="UpdateTavernServerRefresh"/> ritenta finche' il giocatore resta qui.
		/// </summary>
		private async System.Threading.Tasks.Task RefreshTavernFromServerAsync()
		{
			if (tavernLoading)
				return;

			tavernLoading = true;
			if (tavernData == null)
				SetTavernBoardMessage(TavernLoadingMessage());
			try
			{
				// Come al Santuario: chi arriva in taverna direttamente dall'hub non ha ancora
				// il link di progressione, quindi va stabilito qui.
				if (await EnsureServerProgressAsync())
				{
					ApplyTavernData(await serverProgress.GetTavernAsync());
					tavernBoardStale = false;
					SetTavernNotice(string.Empty);
					AppendLog($"TAVERNA - quest ricevute: {tavernData?.quests?.Length ?? 0}.");
				}
				else
				{
					ReportTavernUnavailable(
						AccardND.Network.AccountServerSession.IsReconnecting
							? GameText.Get(GameTextKeys.Tavern.Reconnecting)
							: GameText.Get(GameTextKeys.Tavern.Offline),
						"TAVERNA - nessuna connessione al server.");
				}
			}
			catch (Exception exception)
			{
				ReportTavernUnavailable(
					"La taverna non risponde: " + exception.Message,
					$"TAVERNA - bacheca non ricevuta: {exception.Message}");
			}
			finally
			{
				tavernLoading = false;
				nextTavernRetryAt = Time.unscaledTime + TavernRetryIntervalSeconds;
			}
		}

		/// <summary>
		/// La bacheca non e' arrivata. Quella gia' ricevuta resta a schermo: e' comunque
		/// roba del server, solo di qualche minuto fa, e cancellarla per una caduta di rete
		/// vorrebbe dire svuotare la taverna proprio mentre la riconnessione sta gia'
		/// rimediando. Le riscossioni non rischiano niente: passano sempre dal server, che
		/// rifiuta quello che nel frattempo non e' piu' vero.
		/// </summary>
		private void ReportTavernUnavailable(string notice, string log)
		{
			tavernBoardStale = true;
			if (tavernData == null)
				SetTavernBoardMessage(GameText.Get(GameTextKeys.Tavern.Unavailable));
			AppendLog(log);
			SetTavernNotice(notice);
		}

		/// <summary>
		/// La pubblicita' viene prima della riscossione ed e' obbligatoria: niente annuncio
		/// andato a buon fine, niente miele. Prima era il contrario - si accreditava e poi
		/// partiva un interstitial di cui non si guardava nemmeno l'esito - e voleva dire
		/// pagare la ricompensa anche quando la pubblicita' non era mai partita.
		///
		/// La quest non viene consumata: se l'annuncio non arriva o viene chiuso a meta', il
		/// server non viene nemmeno chiamato e il bottone RISCUOTI resta li' per dopo.
		/// </summary>
		private async void ClaimTavernQuest(string questId, bool useRewardedVideo)
		{
			if (tavernClaiming || !ServerProgressReady)
				return;

			bool claimed = false;
			BeginTavernClaim(questId);
			try
			{
				if (useRewardedVideo && !await ShowTavernAdGateAsync(AdPlacement.TavernQuestClaim))
					return;

				int multiplier = useRewardedVideo ? 5 : 1;
				claimed = await ClaimTavernAsync(
					() => serverProgress.ClaimTavernQuestAsync(questId, multiplier),
					$"quest {questId} x{multiplier}");
			}
			finally
			{
				EndTavernClaim();
			}

			// Fuori dal cancello: e' solo il badge dei messaggi, e tenere spenta la bacheca
			// per un altro giro di rete che non la riguarda sarebbe attesa regalata.
			if (claimed && !useRewardedVideo)
			{
				await LoadPendingAdRewardsAsync();
				for (int index = 0; index < profilePendingRewards.Length; index++)
				{
					SinglePlayerPendingAdRewardData pending = profilePendingRewards[index];
					if (pending == null || !string.Equals(pending.rewardType, "tavern", StringComparison.Ordinal))
						continue;

					SetTavernNotice(
						$"Riscosso x1. Hai ricevuto una comunicazione: recupera gli altri "
						+ $"{pending.extraHoney} vasetti di miele guardando il video.");
					break;
				}
			}
		}

		/// <summary>
		/// Prende il cancello delle riscossioni e spegne la bacheca. Il cancello si tiene per
		/// tutta l'operazione - dal tocco alla risposta del server, pubblicita' in mezzo -
		/// perche' fra l'annuncio chiuso e la conferma passa una richiesta di rete che puo'
		/// prendersi i suoi secondi. Prima il cancello si apriva e richiudeva due volte, e in
		/// quel buco un ridisegno rimetteva i bottoni accesi: un secondo tocco costava
		/// un'altra pubblicita' per poi sentirsi dire che la ricompensa era gia' riscossa.
		/// </summary>
		private void BeginTavernClaim(string questId)
		{
			tavernClaiming = true;
			tavernClaimingQuestId = questId;
			ApplyTavernData(tavernData);
		}

		/// <summary>
		/// Rilascia il cancello e ridisegna: le righe sono state create nello stato "in
		/// riscossione" e vanno rifatte perche' tornino cliccabili.
		/// </summary>
		private void EndTavernClaim()
		{
			tavernClaiming = false;
			tavernClaimingQuestId = null;
			ApplyTavernData(tavernData);
		}

		/// <summary>
		/// Il premio di giornata passa dallo stesso cancello, e nemmeno lui si apre a vuoto:
		/// i 50 vasetti sono gia' stati guadagnati completando le quest, ma restano da
		/// riscuotere finche' una pubblicita' non e' andata fino in fondo.
		/// </summary>
		private async void ClaimTavernBonus()
		{
			if (tavernClaiming || !ServerProgressReady)
				return;

			BeginTavernClaim(null);
			try
			{
				if (!await ShowTavernAdGateAsync(AdPlacement.TavernBonusClaim))
					return;

				await ClaimTavernAsync(() => serverProgress.ClaimTavernBonusAsync(), "premio di giornata");
			}
			finally
			{
				EndTavernClaim();
			}
		}

		/// <summary>
		/// Il cancello pubblicitario delle riscossioni. Tiene fermo il pannello mentre
		/// l'annuncio arriva - il caricamento puo' prendere qualche secondo, e senza una riga
		/// che lo dica un bottone premuto che non fa niente sembra rotto - e spiega il no,
		/// distinguendo "la rete non ha annunci" da "l'hai chiuso a meta'": il primo e' un
		/// riprova piu' tardi, il secondo una scelta del giocatore.
		///
		/// Il cancello delle riscossioni e' gia' preso dal chiamante e resta preso anche dopo
		/// che l'annuncio si e' chiuso: qui non si tocca, altrimenti la bacheca tornerebbe
		/// viva proprio mentre parte la chiamata al server.
		/// </summary>
		private async System.Threading.Tasks.Task<bool> ShowTavernAdGateAsync(AdPlacement placement)
		{
			// Dove la ricompensa e' condonata non c'e' nessuna pubblicita' in arrivo:
			// annunciarla sarebbe una promessa che non manteniamo, e per un attimo farebbe
			// anche sembrare lento un incasso immediato.
			SetTavernNotice(AdService.RewardsWaivedWithoutAds
				? GameText.Get(GameTextKeys.Tavern.Claiming)
				: GameText.Get(GameTextKeys.Tavern.LoadingAd));
			AdResult ad = await AdService.ShowAsync(placement, asGate: true);

			if (ad.Grants)
			{
				SetTavernNotice(GameText.Get(GameTextKeys.Tavern.Claiming));
				return true;
			}

			SetTavernNotice(ad.Unavailable
				? GameText.Get(GameTextKeys.Tavern.AdUnavailable)
				: GameText.Get(GameTextKeys.Tavern.AdIncomplete));
			AppendLog(GameText.Format(GameTextKeys.Tavern.ClaimNotUnlockedLog, ad.Outcome));
			return false;
		}

		/// <summary>
		/// Riscossione: il server risponde con la bacheca aggiornata, poi si riallinea la
		/// cache di progressione perche' il miele e' cambiato e il resto della UI (banner
		/// account, Santuario) legge da li'. Restituisce se la riscossione e' andata a buon
		/// fine, perche' la pubblicita' che segue non deve partire dopo un errore.
		///
		/// Il cancello e' del chiamante: qui si presume gia' preso e non si rilascia, cosi'
		/// che fra l'annuncio e la risposta del server non esista un istante in cui la
		/// bacheca sia di nuovo premibile.
		/// </summary>
		private async System.Threading.Tasks.Task<bool> ClaimTavernAsync(
			Func<System.Threading.Tasks.Task<TavernData>> claim, string description)
		{
			if (!ServerProgressReady)
				return false;

			int honeyBefore = tavernData?.honey ?? 0;
			try
			{
				ApplyTavernData(await claim());
				await SyncProgressAfterTavernClaimAsync();
				int gained = Mathf.Max(0, (tavernData?.honey ?? 0) - honeyBefore);
				SetTavernNotice($"Riscosso: +{gained} vasetti di miele.");
				AppendLog($"TAVERNA - riscossa {description}: +{gained} miele.");
				return true;
			}
			catch (Exception exception)
			{
				AppendLog($"TAVERNA - riscossione rifiutata ({description}): {exception.Message}");
				int recovered = await RecoverTavernClaimAsync(honeyBefore);
				if (recovered > 0)
				{
					SetTavernNotice($"Riscosso: +{recovered} vasetti di miele.");
					AppendLog($"TAVERNA - riscossa {description}: +{recovered} miele (conferma arrivata tardi).");
					return true;
				}

				SetTavernNotice(exception.Message);
				return false;
			}
		}

		/// <summary>
		/// La riscossione cambia il miele lato server: la cache di progressione va riallineata,
		/// altrimenti banner account e Santuario mostrano il saldo vecchio.
		/// </summary>
		private async System.Threading.Tasks.Task SyncProgressAfterTavernClaimAsync()
		{
			await serverProgress.RefreshAsync();
			MirrorServerProgress();
			RefreshSinglePlayerProgressView();
		}

		/// <summary>
		/// Cosa fare quando la riscossione non ha ricevuto risposta. Un timeout dice che la
		/// risposta non e' arrivata, non che il server non abbia accreditato: rimettere in
		/// bacheca la copia vecchia - com'era prima - faceva ricomparire il RISCUOTI su una
		/// quest gia' pagata. Il miele non era a rischio, il server non paga due volte, ma il
		/// giocatore si guardava una seconda pubblicita' per intero per poi sentirsi
		/// rispondere "gia' riscossa": una pubblicita' rubata.
		///
		/// Quindi si richiede la bacheca al server prima di riaccendere qualsiasi bottone. Se
		/// nel frattempo il miele e' cresciuto la riscossione era passata davvero, e va
		/// raccontata come tale invece che come errore. Ritorna il miele recuperato.
		/// </summary>
		private async System.Threading.Tasks.Task<int> RecoverTavernClaimAsync(int honeyBefore)
		{
			try
			{
				tavernBoardStale = true;
				await RefreshTavernFromServerAsync();
				int gained = Mathf.Max(0, (tavernData?.honey ?? 0) - honeyBefore);
				if (gained > 0)
					await SyncProgressAfterTavernClaimAsync();
				return gained;
			}
			catch (Exception exception)
			{
				// La bacheca resta marcata vecchia: ci riprova il ciclo di
				// UpdateTavernServerRefresh finche' il giocatore e' qui.
				AppendLog($"TAVERNA - bacheca non riletta dopo la riscossione: {exception.Message}");
				return 0;
			}
		}
	}
}
