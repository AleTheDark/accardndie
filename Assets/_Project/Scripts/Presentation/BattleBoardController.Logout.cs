using AccardND.Network;
using AccardND.PvpUi;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
/// <summary>
/// Uscita dall'account dalle opzioni. È diverso dal "torna al menu": lì si
/// abbandona la partita restando collegati, qui si chiude proprio la sessione
/// Google e si torna alla schermata di accesso, dove si può entrare con un
/// altro account.
/// </summary>
public sealed partial class BattleBoardController
{
	private const string LoginSceneName = "LoginScreenPrototype";
	private const string LoginProviderPrefsKey = "AccardND.LoginProvider";

	private void LogoutFromOptions()
	{
		ShowLogoutConfirmation();
	}

	private void CreateLogoutConfirmation(Transform parent, Font font)
	{
		Image overlay = CreateImage("Logout Confirmation", parent, new Color(0f, 0f, 0f, 0.72f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		logoutConfirmPanel = ((Component)overlay).gameObject;
		Canvas canvas = logoutConfirmPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 941;
		logoutConfirmPanel.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Logout Dialog", ((Component)overlay).transform, new Color(0.01f, 0.018f, 0.028f, 0.98f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		SetRect(dialog.rectTransform, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.66f));

		Text title = CreateText("Logout Title", ((Component)dialog).transform, font, 50, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		Font logoutTitleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
		if (logoutTitleFont != null)
			title.font = logoutTitleFont;
		title.fontSize = 50;
		title.resizeTextForBestFit = false;
		title.text = "USCIRE DALL'ACCOUNT?";
		title.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(title.rectTransform, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.9f));

		Text body = CreateText("Logout Body", ((Component)dialog).transform, font, 30, (FontStyle)0, (TextAnchor)4);
		body.text = "Tornerai alla schermata di accesso. La campagna in corso verra' abbandonata.";
		body.color = new Color(0.86f, 0.92f, 0.94f);
		body.horizontalOverflow = HorizontalWrapMode.Wrap;
		body.verticalOverflow = VerticalWrapMode.Truncate;
		body.resizeTextForBestFit = false;
		body.fontSize = 30;
		SetRect(body.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.66f));

		Button cancelButton = CreateButton("Cancel Logout", ((Component)dialog).transform, font, "ANNULLA");
		((UnityEvent)cancelButton.onClick).AddListener(new UnityAction(HideLogoutConfirmation));
		SetRect((RectTransform)((Component)cancelButton).transform, new Vector2(0.08f, 0.09f), new Vector2(0.46f, 0.28f));

		Button confirmButton = CreateButton("Confirm Logout", ((Component)dialog).transform, font, "ESCI");
		((UnityEvent)confirmButton.onClick).AddListener(new UnityAction(ConfirmLogout));
		AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(confirmButton);
		SetRect((RectTransform)((Component)confirmButton).transform, new Vector2(0.54f, 0.09f), new Vector2(0.92f, 0.28f));

		logoutConfirmPanel.SetActive(false);
	}

	private void ShowLogoutConfirmation()
	{
		if ((Object)(object)logoutConfirmPanel == (Object)null)
		{
			ConfirmLogout();
			return;
		}
		if ((Object)(object)optionsPanel != (Object)null)
		{
			CloseOptionsPanel();
		}
		logoutConfirmPanel.SetActive(true);
		logoutConfirmPanel.transform.SetAsLastSibling();
	}

	private void HideLogoutConfirmation()
	{
		if ((Object)(object)logoutConfirmPanel != (Object)null)
		{
			logoutConfirmPanel.SetActive(false);
		}
	}

	private void ConfirmLogout()
	{
		HideLogoutConfirmation();

		// Ordine voluto: prima si chiude la sessione col server (che spegne anche la
		// riconnessione automatica, altrimenti riaprirebbe il socket appena chiuso),
		// poi si dimentica l'accesso Google.
		AccountServerSession.SignOut();
		PvpUgsAuth.ForgetSession();

		// Senza questo la schermata di login ripristinerebbe da sola la sessione
		// appena chiusa, e il logout durerebbe il tempo di un cambio scena.
		PlayerPrefs.DeleteKey(LoginProviderPrefsKey);
		PlayerPrefs.Save();

		SceneManager.LoadScene(LoginSceneName);
	}
}
}
