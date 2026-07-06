using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const string HintSeenPrefsPrefix = "AccardHint_";

	// Chiavi degli hint contestuali "prima volta".
	private const string HintKeyCampaignIntro = "campaign_intro";
	private const string HintKeyRoomChoice = "room_choice";
	private const string HintKeyFormationDraft = "formation_draft";
	private const string HintKeyCombat = "combat";
	private const string HintKeyMerchant = "merchant";
	private const string HintKeyFirstAura = "first_aura";
	private const string HintKeyFirstDefeat = "first_defeat";

	private readonly struct HintContent
	{
		public string Title { get; }

		public string Body { get; }

		public HintContent(string title, string body)
		{
			Title = title;
			Body = body;
		}
	}

	private readonly Queue<HintContent> pendingHints = new Queue<HintContent>();

	private GameObject hintPanel;

	private Text hintTitleText;

	private Text hintBodyText;

	private bool hintActive;

	private void CreateHintOverlay(Transform parent, Font font)
	{
		Image overlay = CreateImage("Hint Overlay", parent, new Color(0f, 0f, 0f, 0.74f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		hintPanel = ((Component)overlay).gameObject;
		Canvas canvas = hintPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 970;
		hintPanel.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Hint Dialog", ((Component)overlay).transform, new Color(0.01f, 0.018f, 0.028f, 0.98f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		SetRect(dialog.rectTransform, new Vector2(0.13f, 0.3f), new Vector2(0.87f, 0.7f));

		hintTitleText = CreateText("Hint Title", ((Component)dialog).transform, font, 30, (FontStyle)1, (TextAnchor)4);
		hintTitleText.color = new Color(0.95f, 0.79f, 0.34f);
		hintTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
		hintTitleText.verticalOverflow = VerticalWrapMode.Truncate;
		SetRect(hintTitleText.rectTransform, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.94f));

		hintBodyText = CreateText("Hint Body", ((Component)dialog).transform, font, 21, (FontStyle)0, (TextAnchor)4);
		hintBodyText.color = new Color(0.86f, 0.92f, 0.94f);
		hintBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		hintBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		hintBodyText.resizeTextForBestFit = true;
		hintBodyText.resizeTextMinSize = 14;
		hintBodyText.resizeTextMaxSize = 21;
		SetRect(hintBodyText.rectTransform, new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.76f));

		Button gotItButton = CreateButton("Hint Dismiss", ((Component)dialog).transform, font, "HO CAPITO");
		((UnityEvent)gotItButton.onClick).AddListener(new UnityAction(DismissHint));
		SetRect((RectTransform)((Component)gotItButton).transform, new Vector2(0.34f, 0.07f), new Vector2(0.66f, 0.22f));

		hintPanel.SetActive(false);
	}

	private static bool HasSeenHint(string key)
	{
		return PlayerPrefs.GetInt(HintSeenPrefsPrefix + key, 0) != 0;
	}

	private static void MarkHintSeen(string key)
	{
		PlayerPrefs.SetInt(HintSeenPrefsPrefix + key, 1);
		PlayerPrefs.Save();
	}

	// Ripristina tutti gli hint: la prossima volta che le meccaniche compaiono
	// vengono di nuovo spiegate. Utile per un pulsante "rivedi i suggerimenti".
	private void ResetHints()
	{
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyCampaignIntro);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyRoomChoice);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyFormationDraft);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyCombat);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyMerchant);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyFirstAura);
		PlayerPrefs.DeleteKey(HintSeenPrefsPrefix + HintKeyFirstDefeat);
		PlayerPrefs.Save();
	}

	private void ShowHintOnce(string key, string title, string body)
	{
		if (string.IsNullOrEmpty(key) || HasSeenHint(key))
		{
			return;
		}
		MarkHintSeen(key);
		pendingHints.Enqueue(new HintContent(title, body));
		if (!hintActive)
		{
			ShowNextHint();
		}
	}

	private void ShowNextHint()
	{
		if (pendingHints.Count == 0 || (Object)(object)hintPanel == (Object)null)
		{
			hintActive = false;
			return;
		}
		HintContent content = pendingHints.Dequeue();
		if ((Object)(object)hintTitleText != (Object)null)
		{
			hintTitleText.text = content.Title;
		}
		if ((Object)(object)hintBodyText != (Object)null)
		{
			hintBodyText.text = content.Body;
		}
		hintActive = true;
		hintPanel.SetActive(true);
		hintPanel.transform.SetAsLastSibling();
	}

	private void DismissHint()
	{
		if ((Object)(object)hintPanel != (Object)null)
		{
			hintPanel.SetActive(false);
		}
		hintActive = false;
		if (pendingHints.Count > 0)
		{
			ShowNextHint();
		}
	}

	private void ShowCampaignIntroHint()
	{
		ShowHintOnce(
			HintKeyCampaignIntro,
			"LA GROTTA DEL MASTER",
			"Sei entrato nella grotta del Master. Davanti a te una serie di stanze generate a caso: " +
			"mostri da sconfiggere, mercanti, tesori e imprevisti. Attraversale tutte fino a raggiungere " +
			"il Boss che ti aspetta in fondo.\n\nMa prima devi prepararti: forgia il tuo mazzo spendendo " +
			"l'essenza iniziale, poi premi INIZIA.");
	}

	private void ShowRoomChoiceHint()
	{
		ShowHintOnce(
			HintKeyRoomChoice,
			"SCEGLI LA VIA",
			"Tre porte, difficolta' nascosta. Dietro ognuna puo' esserci un combattimento, un mercante, " +
			"un tesoro o un evento imprevisto. Le vie piu' rischiose portano ricompense migliori. " +
			"Tocca una porta per proseguire nella grotta.");
	}

	private void ShowFormationDraftHint()
	{
		ShowHintOnce(
			HintKeyFormationDraft,
			"SCHIERA LA FORMAZIONE",
			"Scegli dalla mano le carte da mandare in campo. Ogni classe ha vantaggi e svantaggi contro " +
			"le altre, quindi guarda la formazione nemica prima di decidere. Quando sei pronto, conferma " +
			"la formazione per iniziare lo scontro.");
	}

	private void ShowCombatHint()
	{
		ShowHintOnce(
			HintKeyCombat,
			"IL COMBATTIMENTO",
			"Si combatte a turni, in ordine di iniziativa. Nel tuo turno scegli una carta e il bersaglio: " +
			"si tira il dado Vigore e chi ottiene il totale piu' alto (Forza + dado + bonus) vince lo " +
			"scontro. Sfrutta i vantaggi di classe e le abilita' speciali per avere la meglio.");
	}

	private void ShowFirstAuraHint(BattleAuraType aura)
	{
		if (aura == BattleAuraType.None)
		{
			return;
		}
		ShowHintOnce(
			HintKeyFirstAura,
			"AURA ATTIVATA!",
			"La tua formazione ha attivato un'aura: " + AuraDisplayName(aura) + ".\n\n" +
			"Le aure sono bonus di squadra che ottieni schierando 3 carte in sinergia: stessa classe, " +
			"stessa famiglia, oppure una per ogni famiglia.\n\n" +
			"Effetto: " + AuraEffectText(aura) + "\n\n" +
			"Trovi tutte le aure spiegate in Opzioni > AURE.");
	}

	private void ShowFirstDefeatHint()
	{
		ShowHintOnce(
			HintKeyFirstDefeat,
			"SCONFITTA... MA NON E' FINITA",
			"La tua formazione e' stata eliminata, ma puoi riprovare la stessa stanza! Torni in campo " +
			"con le carte ancora disponibili nel mazzo e affronti i mostri rimasti. Premi RIPROVA STANZA " +
			"per continuare.\n\nVuoi vedere quali carte ti restano? Apri la borsa in basso a destra: trovi " +
			"le carte nel mazzo, quelle in cooldown e quelle finite al cimitero.");
	}

	private void ShowMerchantHint()
	{
		ShowHintOnce(
			HintKeyMerchant,
			"IL MERCANTE",
			"Spendi i tuoi punti esperienza per acquistare nuove carte, a caso, per classe o per valore. " +
			"Puoi anche recuperare le carte cadute dal cimitero. Quando hai finito, premi CONTINUA per " +
			"tornare nella grotta.");
	}
}
}
