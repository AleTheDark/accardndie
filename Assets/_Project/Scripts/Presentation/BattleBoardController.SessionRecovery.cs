using System.Collections.Generic;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private readonly List<Canvas> authenticationSuspendedCanvases = new();
    private GameObject campaignRecoveryPopup;
    private bool recoveryResumeInputLocked;

    // Il popup è uno solo per due situazioni diverse - la sessione rifatta a gioco
    // acceso e la run ritrovata su disco all'ingresso in campagna - quindi testo e
    // bottoni si ricablano a ogni apertura invece di essere fissati alla costruzione.
    private Text campaignRecoveryBodyText;
    private Button campaignRecoveryResumeButton;
    private Text campaignRecoveryResumeLabel;
    private Button campaignRecoveryDiscardButton;
    private Text campaignRecoveryDiscardLabel;

    /// <summary>
    /// Distingue il rientro imposto da una sessione scaduta dal logout volontario. In quel
    /// solo caso il controller DontDestroyOnLoad viene conservato: e' uno snapshot caldo
    /// dell'intero object graph, quindi non perde stato quando vengono aggiunte nuove carte,
    /// stanze, boss o componenti runtime.
    /// </summary>
    internal static void PrepareCampaignSessionRecovery()
    {
        BattleBoardController controller = Object.FindAnyObjectByType<BattleBoardController>();
        if ((Object)(object)controller == (Object)null || controller.campaignDeck == null
            || controller.pvpPresentationActive || controller.gameFinished)
        {
            pendingCampaignSessionRecovery = null;
            return;
        }

        // Conserva anche un checkpoint persistente quando lo stato e' in un punto in cui
        // il mapper attuale e' autorevole. Durante lo scontro non sovrascrive mai il save
        // precedente con uno snapshot parziale e quindi non crea un falso resume.
        if (!controller.IsCampaignBattleActive())
            controller.SaveCurrentRun();
        pendingCampaignSessionRecovery = controller;
    }

    private void SuspendCampaignForAuthentication()
    {
        authenticationSuspendedCanvases.Clear();
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(includeInactive: true))
        {
            if (canvas != null && canvas.enabled)
            {
                authenticationSuspendedCanvases.Add(canvas);
                canvas.enabled = false;
            }
        }
    }

    private void RestoreCampaignAfterAuthentication()
    {
        foreach (Canvas canvas in authenticationSuspendedCanvases)
            if (canvas != null)
                canvas.enabled = true;
        authenticationSuspendedCanvases.Clear();

        // Non si riavvia LoadBattle e non si richiama BeginRoomChoice: entrambi
        // rigenererebbero la stanza. Lo stato runtime rimane quello gia' consolidato.
        recoveryResumeInputLocked = inputLocked;
        inputLocked = true;
        ShowCampaignRecoveryPopup(
            GameText.GetOrFallbackSilent(
                GameTextKeys.Campaign.RecoverySessionBody,
                "Hai una campagna in corso. Vuoi riprenderla nello stesso punto o abbandonarla?"),
            GameText.GetOrFallbackSilent(GameTextKeys.Campaign.RecoveryAbandon, "ABBANDONA"),
            ResumeRecoveredCampaign,
            AbandonRecoveredCampaign);
    }

    /// <summary>
    /// Apre il popup con il testo e le due azioni di questa volta. Il bottone di
    /// sinistra è sempre "riprendi": cambia solo quello di destra, che a gioco acceso
    /// abbandona una run viva e all'ingresso in campagna annulla un salvataggio.
    /// </summary>
    private void ShowCampaignRecoveryPopup(
        string body, string discardLabel, UnityAction onResume, UnityAction onDiscard)
    {
        BuildCampaignRecoveryPopup();
        campaignRecoveryBodyText.text = body;
        campaignRecoveryResumeLabel.text =
            GameText.GetOrFallbackSilent(GameTextKeys.Campaign.RecoveryResume, "RIPRENDI");
        campaignRecoveryDiscardLabel.text = discardLabel;
        campaignRecoveryResumeButton.onClick.RemoveAllListeners();
        campaignRecoveryResumeButton.onClick.AddListener(onResume);
        campaignRecoveryDiscardButton.onClick.RemoveAllListeners();
        campaignRecoveryDiscardButton.onClick.AddListener(onDiscard);
        campaignRecoveryPopup.SetActive(true);
        campaignRecoveryPopup.transform.SetAsLastSibling();
    }

    private void BuildCampaignRecoveryPopup()
    {
        if ((Object)(object)campaignRecoveryPopup != (Object)null)
            return;

        var root = new GameObject("Campaign Recovery", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        campaignRecoveryPopup = root;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Image scrim = CreateRecoveryImage("Scrim", root.transform, new Color(0.01f, 0.015f, 0.025f, 0.94f));
        StretchRecovery(scrim.rectTransform);
        Image panel = CreateRecoveryImage("Panel", root.transform, new Color(0.055f, 0.075f, 0.105f, 1f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 350f);

        Text title = CreateRecoveryText("Title", panel.transform, 40, FontStyle.Bold);
        title.text = GameText.GetOrFallbackSilent(GameTextKeys.Campaign.RecoveryTitle, "CAMPAGNA IN CORSO");
        SetRecoveryRect(title.rectTransform, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.9f));
        campaignRecoveryBodyText = CreateRecoveryText("Body", panel.transform, 25, FontStyle.Normal);
        SetRecoveryRect(campaignRecoveryBodyText.rectTransform, new Vector2(0.08f, 0.4f), new Vector2(0.92f, 0.68f));

        campaignRecoveryResumeButton = CreateRecoveryButton(
            "Resume", panel.transform, out campaignRecoveryResumeLabel, new Color(0.12f, 0.48f, 0.28f, 1f));
        SetRecoveryRect(
            (RectTransform)campaignRecoveryResumeButton.transform,
            new Vector2(0.08f, 0.1f), new Vector2(0.47f, 0.32f));

        campaignRecoveryDiscardButton = CreateRecoveryButton(
            "Discard", panel.transform, out campaignRecoveryDiscardLabel, new Color(0.55f, 0.12f, 0.12f, 1f));
        SetRecoveryRect(
            (RectTransform)campaignRecoveryDiscardButton.transform,
            new Vector2(0.53f, 0.1f), new Vector2(0.92f, 0.32f));
    }

    private void ResumeRecoveredCampaign()
    {
        campaignRecoveryPopup.SetActive(false);
        inputLocked = recoveryResumeInputLocked;
        ApplyResponsiveLayout();
        AppendLog("CAMPAGNA RIPRESA - sessione autenticata, stato della stanza conservato.");
    }

    private void AbandonRecoveredCampaign()
    {
        campaignRecoveryPopup.SetActive(false);
        // ReturnToStart e' il solo percorso autorevole di abbandono: pulisce sia lo stato
        // runtime sia il checkpoint persistente, evitando salvataggi orfani.
        ReturnToStart(showModeSelection: true);
    }

    private static Image CreateRecoveryImage(string name, Transform parent, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateRecoveryText(string name, Transform parent, int size, FontStyle style)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        Text text = gameObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    private static Button CreateRecoveryButton(string name, Transform parent, out Text label, Color color)
    {
        Image image = CreateRecoveryImage(name, parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        label = CreateRecoveryText("Label", image.transform, 27, FontStyle.Bold);
        label.raycastTarget = false;
        StretchRecovery(label.rectTransform);
        return button;
    }

    private static void StretchRecovery(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRecoveryRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
}
