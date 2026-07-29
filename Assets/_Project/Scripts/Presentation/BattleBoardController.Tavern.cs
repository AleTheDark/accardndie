using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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
		private Text tavernCompletionText;
		private Image tavernCompletionFill;
		private Button tavernBonusButton;
		private Font tavernTitleFont;
		private Coroutine tavernNoticeClearRoutine;
		private readonly List<GameObject> tavernQuestRows = new List<GameObject>();

		/// <summary>Ultima bacheca ricevuta dal server: e' l'unica verita' che la schermata disegna.</summary>
		private TavernData tavernData;
		private bool tavernLoading;
		private bool tavernClaiming;

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

			Image shade = CreateImage("Tavern Lower Shade", root.transform, new Color(0.01f, 0.008f, 0.006f, 0.78f));
			SetRect(shade.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.775f));
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
			title.text = "TAVERNA";
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

			Text section = CreateText("Daily Quests Header", root.transform, tavernTitleFont, 28, FontStyle.Normal, TextAnchor.MiddleLeft);
			section.text = "— QUEST GIORNALIERE —";
			section.color = new Color(0.92f, 0.68f, 0.22f, 1f);
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

			Image progressTrack = CreateImage("Tavern Daily Progress", root.transform, new Color(0.12f, 0.1f, 0.08f, 0.96f));
			SetRect(progressTrack.rectTransform, new Vector2(0.075f, 0.637f), new Vector2(0.62f, 0.674f));
			Outline progressBorder = progressTrack.gameObject.AddComponent<Outline>();
			progressBorder.effectColor = new Color(0.82f, 0.61f, 0.2f, 0.9f);
			progressBorder.effectDistance = new Vector2(2f, -2f);

			Image progressInset = CreateImage("Tavern Daily Progress Inset", progressTrack.transform, new Color(0.015f, 0.008f, 0.025f, 1f));
			SetRect(progressInset.rectTransform, new Vector2(0.008f, 0.16f), new Vector2(0.992f, 0.84f));

			tavernCompletionFill = CreateImage("Tavern Daily Progress Fill", progressInset.transform, new Color(0.47f, 0.08f, 0.72f, 1f));
			tavernCompletionFill.type = Image.Type.Filled;
			tavernCompletionFill.fillMethod = Image.FillMethod.Horizontal;
			Stretch(tavernCompletionFill.rectTransform);

			Image fillHighlight = CreateImage("Tavern Daily Progress Highlight", tavernCompletionFill.transform, new Color(0.86f, 0.48f, 1f, 0.38f));
			SetRect(fillHighlight.rectTransform, new Vector2(0f, 0.56f), new Vector2(1f, 0.9f));
			fillHighlight.raycastTarget = false;

			Image movingShine = CreateImage("Tavern Daily Progress Shine", progressInset.transform, new Color(1f, 0.88f, 1f, 0.48f));
			SetRect(movingShine.rectTransform, new Vector2(0f, 0.12f), new Vector2(0.055f, 0.88f));
			movingShine.raycastTarget = false;

			Image fillTipGlow = CreateImage("Tavern Daily Progress Tip Glow", progressInset.transform, new Color(0.88f, 0.42f, 1f, 0.72f));
			SetRect(fillTipGlow.rectTransform, new Vector2(0f, -0.18f), new Vector2(0.018f, 1.18f));
			fillTipGlow.raycastTarget = false;

			for (int i = 1; i < 5; i++)
			{
				Image tick = CreateImage("Tavern Daily Progress Tick " + i, progressInset.transform, new Color(0.92f, 0.73f, 0.35f, 0.36f));
				float x = i / 5f;
				SetRect(tick.rectTransform, new Vector2(x - 0.0015f, 0.08f), new Vector2(x + 0.0015f, 0.92f));
				tick.raycastTarget = false;
			}

			TavernProgressVfx progressVfx = progressTrack.gameObject.AddComponent<TavernProgressVfx>();
			progressVfx.Initialize(tavernCompletionFill, movingShine.rectTransform, fillTipGlow.rectTransform);

			tavernCompletionText = CreateText("Tavern Daily Progress Text", root.transform, tavernTitleFont, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
			tavernCompletionText.color = new Color(0.92f, 0.75f, 0.38f, 1f);
			SetRect(tavernCompletionText.rectTransform, new Vector2(0.62f, 0.63f), new Vector2(0.72f, 0.68f));
			tavernBonusButton = CreateButton("Tavern Daily Bonus", root.transform, tavernTitleFont, "PREMIO 5/5");
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

			tavernPanel.SetActive(false);
		}

		private async void ShowTavern()
		{
			modeSelectionPanel.SetActive(false);
			SetAccountHubHudActive(true);
			tavernPanel.SetActive(true);
			await RefreshTavernFromServerAsync();
		}

		private void ReturnFromTavern()
		{
			tavernPanel.SetActive(false);
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

		private sealed class TavernProgressVfx : MonoBehaviour
		{
			private Image fill;
			private RectTransform shine;
			private RectTransform tipGlow;

			public void Initialize(Image progressFill, RectTransform movingShine, RectTransform progressTipGlow)
			{
				fill = progressFill;
				shine = movingShine;
				tipGlow = progressTipGlow;
			}

			private void Update()
			{
				if (fill == null || shine == null || tipGlow == null)
					return;

				float amount = fill.fillAmount;
				float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f);
				fill.color = Color.Lerp(
					new Color(0.42f, 0.055f, 0.66f, 1f),
					new Color(0.66f, 0.16f, 0.91f, 1f),
					pulse);

				float tipX = Mathf.Clamp01(amount);
				tipGlow.anchorMin = new Vector2(Mathf.Max(0f, tipX - 0.012f), -0.18f);
				tipGlow.anchorMax = new Vector2(Mathf.Min(1f, tipX + 0.012f), 1.18f);
				tipGlow.gameObject.SetActive(amount > 0.01f);
				float tipScale = 0.9f + pulse * 0.28f;
				tipGlow.localScale = new Vector3(tipScale, tipScale, 1f);

				float sweep = Mathf.Repeat(Time.unscaledTime * 0.24f, 1.2f) - 0.1f;
				bool shineVisible = amount > 0.04f && sweep < amount;
				shine.gameObject.SetActive(shineVisible);
				if (shineVisible)
				{
					shine.anchorMin = new Vector2(sweep, 0.12f);
					shine.anchorMax = new Vector2(Mathf.Min(sweep + 0.055f, amount), 0.88f);
				}
			}
		}

		/// <summary>
		/// Disegna la bacheca ricevuta dal server. Il client non decide niente: quali quest
		/// sono di oggi, quanto manca alla soglia e se il premio spetta lo dice il server.
		/// </summary>
		private void ApplyTavernData(TavernData data)
		{
			tavernData = data;

			for (int i = 0; i < tavernQuestRows.Count; i++)
				Destroy(tavernQuestRows[i]);
			tavernQuestRows.Clear();

			TavernQuestData[] quests = data?.quests ?? Array.Empty<TavernQuestData>();
			for (int i = 0; i < quests.Length; i++)
				CreateTavernQuestRow(quests[i]);

			// Il conto alla rovescia arriva dal server: l'orologio del dispositivo puo' essere
			// sfasato e mostrerebbe un rinnovo che non corrisponde a quello vero.
			int seconds = Mathf.Max(0, data?.secondsToRefresh ?? 0);
			tavernRefreshText.text = $"RINNOVO TRA: {seconds / 3600:00}H {seconds % 3600 / 60:00}M";

			// La barra misura la strada verso il premio, non verso l'en plein: le quest in
			// bacheca sono piu' di quelle che servono, e riempirla solo a 8 su 8 farebbe
			// sembrare mancante un premio gia' guadagnato.
			int completed = data?.completedCount ?? 0;
			int required = Mathf.Max(1, data?.questsRequiredForBonus ?? quests.Length);
			tavernCompletionText.text = $"{Mathf.Min(completed, required)}/{required}";
			tavernCompletionFill.fillAmount = Mathf.Clamp01((float)completed / required);

			bool bonusClaimable = data != null && data.bonusAvailable && !data.bonusClaimed;
			tavernBonusButton.interactable = bonusClaimable && !tavernClaiming;
			Text bonusLabel = tavernBonusButton.GetComponentInChildren<Text>();
			if (bonusLabel != null)
			{
				bonusLabel.text = data != null && data.bonusClaimed
					? "PREMIO\nRISCOSSO"
					: $"PREMIO {required}/{quests.Length}\n+{data?.bonusHoneyReward ?? 0}";
			}

			RefreshAccountBannerView();
		}

		private void CreateTavernQuestRow(TavernQuestData quest)
		{
			Image row = CreateImage("Quest " + quest.questId, tavernQuestContent, new Color(0.035f, 0.028f, 0.021f, 0.94f));
			LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
			element.preferredHeight = 220f;
			tavernQuestRows.Add(row.gameObject);

			Text info = CreateText("Quest Info", row.transform, tavernTitleFont, 31, FontStyle.Normal, TextAnchor.MiddleLeft);
			info.resizeTextForBestFit = false;
			info.fontSize = 31;
			info.horizontalOverflow = HorizontalWrapMode.Wrap;
			info.verticalOverflow = VerticalWrapMode.Overflow;
			info.lineSpacing = 0.75f;
			string questTitle = quest.title.ToUpperInvariant();
			int questTitleSize = questTitle.Length > 21 ? 24 : 31;
			info.text = $"<size={questTitleSize}><color=#E7C681>{questTitle}</color></size>\n<size=27><color=#FFFFFF>{quest.description}</color></size>\n<size=23><color=#E7C56F>{quest.current} / {quest.threshold}</color></size>";
			SetRect(info.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.59f, 0.965f));

			Text reward = CreateText("Honey Reward", row.transform, tavernTitleFont, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
			reward.text = $"MIELE\n<size=30><color=#F4B52D>● {quest.honeyReward}</color></size>";
			SetRect(reward.rectTransform, new Vector2(0.575f, 0.1f), new Vector2(0.755f, 0.9f));

			string label = quest.claimed ? "RISCOSSA" : quest.completed ? "RISCUOTI" : "IN CORSO";
			Button action = CreateButton("Quest Action", row.transform, tavernTitleFont, label);
			SetRect((RectTransform)action.transform, new Vector2(0.735f, 0.08f), new Vector2(0.99f, 0.92f));
			Text actionLabel = action.GetComponentInChildren<Text>();
			if (actionLabel != null)
			{
				actionLabel.resizeTextForBestFit = false;
				actionLabel.fontSize = 30;
				actionLabel.resizeTextMinSize = 30;
				actionLabel.resizeTextMaxSize = 30;
			}
			action.interactable = quest.completed && !quest.claimed && !tavernClaiming;
			string questId = quest.questId;
			action.onClick.AddListener((UnityAction)(() => ClaimTavernQuest(questId)));
		}

		/// <summary>
		/// Scarica la bacheca dal server. Senza connessione la taverna resta visitabile ma
		/// vuota: mostrare quest da una cache locale darebbe progressi che il server puo' poi
		/// smentire, e qui si paga miele.
		/// </summary>
		private async System.Threading.Tasks.Task RefreshTavernFromServerAsync()
		{
			if (tavernLoading)
				return;

			tavernLoading = true;
			try
			{
				// Come al Santuario: chi arriva in taverna direttamente dall'hub non ha ancora
				// il link di progressione, quindi va stabilito qui.
				if (await EnsureServerProgressAsync())
				{
					ApplyTavernData(await serverProgress.GetTavernAsync());
					SetTavernNotice(string.Empty);
					AppendLog($"TAVERNA - quest ricevute: {tavernData?.quests?.Length ?? 0}.");
				}
				else
				{
					ApplyTavernData(null);
					AppendLog("TAVERNA - nessuna connessione al server.");
					SetTavernNotice(AccardND.Network.AccountServerSession.IsReconnecting
						? "Riconnessione in corso: la taverna si aggiornerà automaticamente."
						: "Taverna non disponibile offline: serve la connessione al server.");
				}
			}
			catch (Exception exception)
			{
				ApplyTavernData(null);
				AppendLog($"TAVERNA - bacheca non ricevuta: {exception.Message}");
				SetTavernNotice("La taverna non risponde: " + exception.Message);
			}
			finally
			{
				tavernLoading = false;
			}
		}

		private void ClaimTavernQuest(string questId) =>
			ClaimTavern(() => serverProgress.ClaimTavernQuestAsync(questId), "quest " + questId);

		private void ClaimTavernBonus() =>
			ClaimTavern(() => serverProgress.ClaimTavernBonusAsync(), "premio di giornata");

		/// <summary>
		/// Riscossione: il server risponde con la bacheca aggiornata, poi si riallinea la
		/// cache di progressione perche' il miele e' cambiato e il resto della UI (banner
		/// account, Santuario) legge da li'.
		/// </summary>
		private async void ClaimTavern(Func<System.Threading.Tasks.Task<TavernData>> claim, string description)
		{
			if (tavernClaiming || !ServerProgressReady)
				return;

			tavernClaiming = true;
			try
			{
				int honeyBefore = tavernData?.honey ?? 0;
				ApplyTavernData(await claim());
				// La riscossione cambia il miele lato server: la cache di progressione va
				// riallineata, altrimenti banner account e Santuario mostrano il saldo vecchio.
				await serverProgress.RefreshAsync();
				MirrorServerProgress();
				RefreshSinglePlayerProgressView();
				int gained = Mathf.Max(0, (tavernData?.honey ?? 0) - honeyBefore);
				SetTavernNotice($"Riscosso: +{gained} vasetti di miele.");
				AppendLog($"TAVERNA - riscossa {description}: +{gained} miele.");
			}
			catch (Exception exception)
			{
				AppendLog($"TAVERNA - riscossione rifiutata ({description}): {exception.Message}");
				SetTavernNotice(exception.Message);
			}
			finally
			{
				tavernClaiming = false;
			}
			// Le righe sono state create con lo stato "in riscossione": vanno ridisegnate
			// perche' tornino cliccabili.
			ApplyTavernData(tavernData);
		}
	}
}
