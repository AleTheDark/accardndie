using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccardND.Iap;
using AccardND.Localization;
using AccardND.NetProtocol;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private bool premiumPurchaseInFlight;
	private bool entitlementsSyncing;

	/// <summary>
	/// Le voci premium visibili adesso. L'upgrade "solo supreme" compare soltanto a chi ha
	/// gia' comprato le classi: a tutti gli altri il negozio mostra i due pacchetti interi,
	/// altrimenti la sezione diventa un listino da leggere invece che tre scelte.
	/// </summary>
	private IReadOnlyList<IapProduct> VisiblePremiumProducts()
	{
		// Durante l'onboarding la sezione non esiste proprio. Non basta renderla non
		// cliccabile: un tour guidato che finisse per sbaglio su un acquisto reale e'
		// l'unico errore di questo sistema che costerebbe soldi a qualcuno.
		if (IsTutorialOnboardingActive())
		{
			return Array.Empty<IapProduct>();
		}

		List<IapProduct> visible = new()
		{
			IapProduct.NoAds,
			IapProduct.Classes,
			IapProduct.ClassesSupreme
		};
		if (IapService.Entitlements.ShowsSupremeUpgrade)
		{
			visible.Remove(IapProduct.ClassesSupreme);
			visible.Add(IapProduct.SupremeUpgrade);
		}
		return visible;
	}

	private string PremiumTitle(IapProduct product)
	{
		switch (product)
		{
			case IapProduct.NoAds:
				return GameText.GetOrFallbackSilent(GameTextKeys.Merchant.ShopPremiumNoAds, "NO ADS");
			case IapProduct.Classes:
				return GameText.GetOrFallbackSilent(GameTextKeys.Merchant.ShopPremiumClasses, "CLASSI");
			case IapProduct.ClassesSupreme:
				return GameText.GetOrFallbackSilent(
					GameTextKeys.Merchant.ShopPremiumClassesSupreme, "CLASSI + SUPREME");
			default:
				return GameText.GetOrFallbackSilent(
					GameTextKeys.Merchant.ShopPremiumSupremeUpgrade, "SUPREME");
		}
	}

	private string PremiumIconResource(IapProduct product)
	{
		switch (product)
		{
		case IapProduct.NoAds: return "UI/no_ads_item";
			case IapProduct.Classes: return "UI/Sanctuary/sanctuary_classes_emblem_aaa";
			default: return "UI/Sanctuary/sanctuary_techniques_emblem_aaa";
		}
	}

	/// <summary>
	/// La riga sotto il nome: prezzo dello store, "posseduto", oppure il motivo per cui da
	/// qui non si compra. Su web e in editor non c'e' nessuno store, e dirlo e' piu' onesto
	/// che mostrare un pulsante che non fa niente.
	/// </summary>
	private string PremiumInfoLine(IapProduct product, out Color color, out bool interactable)
	{
		if (IapService.Entitlements.Owns(product))
		{
			color = new Color(0.55f, 0.9f, 0.62f);
			interactable = false;
			return GameText.GetOrFallbackSilent(GameTextKeys.Merchant.ShopPremiumOwned, "POSSEDUTO");
		}

		IapOffer offer = IapService.Offers.Find(product);
		string price = offer?.Price ?? IapProducts.FallbackPrice(product);
		if (!IapService.IsStoreAvailable)
		{
			color = ShopBody;
			interactable = false;
			return price + "\n" + GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopPremiumAndroidOnly, "SOLO NELL'APP ANDROID");
		}

		color = ShopGold;
		interactable = !premiumPurchaseInFlight && offer != null && offer.Purchasable;
		return price;
	}

	/// <summary>
	/// L'acquisto vero. Il percorso e' lungo di proposito: lo store incassa, il server
	/// verifica la ricevuta e concede, e solo alla fine si conferma l'ordine a Google. Se
	/// il gioco muore nel mezzo, l'ordine resta pendente e riparte al prossimo avvio.
	/// </summary>
	private async void BuyPremium(IapProduct product)
	{
		if (premiumPurchaseInFlight)
			return;
		premiumPurchaseInFlight = true;
		RefreshShop();
		SetShopStatus(GameText.GetOrFallbackSilent(
			GameTextKeys.Merchant.ShopPremiumOpening, "Apro il pagamento..."));
		try
		{
			IapPurchaseResult result = await IapService.PurchaseAsync(product);
			switch (result.Outcome)
			{
				case IapOutcome.Purchased:
					await RedeemPremiumAsync(product, result.Receipt);
					break;
				case IapOutcome.Cancelled:
					SetShopStatus(string.Empty);
					break;
				case IapOutcome.Deferred:
					SetShopStatus(GameText.GetOrFallbackSilent(
						GameTextKeys.Merchant.ShopPremiumDeferred,
						"Pagamento in attesa di approvazione: lo sblocco arrivera' da solo."));
					break;
				case IapOutcome.Unavailable:
					SetShopStatus(GameText.GetOrFallbackSilent(
						GameTextKeys.Merchant.ShopPremiumAndroidOnly, "SOLO NELL'APP ANDROID"));
					break;
				default:
					// "already_owned" non e' un errore: e' un acquisto che c'e' gia' e che
					// il ripristino sa recuperare senza far pagare di nuovo.
					if (result.Message == "already_owned")
						await SyncEntitlementsAsync(restore: true);
					else
						SetShopStatus(GameText.GetOrFallbackSilent(
							GameTextKeys.Merchant.ShopPremiumFailed, "Acquisto non riuscito: {0}", result.Message));
					break;
			}
		}
		catch (Exception exception)
		{
			AppendLog("NEGOZIO - acquisto premium fallito: " + exception.Message);
			SetShopStatus(GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopPremiumFailed, "Acquisto non riuscito: {0}", exception.Message));
		}
		finally
		{
			premiumPurchaseInFlight = false;
		}
		RefreshShop();
	}

	private async Task RedeemPremiumAsync(IapProduct product, IapReceipt receipt)
	{
		if (receipt == null || !receipt.IsUsable)
			return;
		if (!await EnsureServerProgressAsync())
		{
			// Pagato ma offline: non si conferma l'ordine, cosi' resta pendente e il
			// ripristino lo ripresentera' appena il server torna raggiungibile.
			SetShopStatus(GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopPremiumOffline,
				"Acquisto ricevuto: lo sblocco arrivera' appena torni online."));
			return;
		}

		IapRedeemResult result = await serverProgress.RedeemPurchaseAsync(
			IapProducts.IdOf(receipt.Product), receipt.Receipt);
		ApplyEntitlements(result?.entitlements);
		if (result != null && result.granted)
		{
			IapService.Confirm(receipt.Product);
			await RefreshAfterPremiumGrantAsync();
			SetShopStatus(GameText.GetOrFallbackSilent(
				GameTextKeys.Merchant.ShopPremiumGranted, "Affare fatto: {0} e' tuo.", PremiumTitle(product)));
			return;
		}

		string reason = result?.messageKey ?? "shop.premium.bad_receipt";
		AppendLog("NEGOZIO - riscatto rifiutato dal server: " + reason);
		SetShopStatus(GameText.GetOrFallbackSilent(reason, "Ricevuta non valida: acquisto non applicato."));
	}

	/// <summary>
	/// Chiede al server cosa possiede l'account e, se richiesto, ripresenta le ricevute che
	/// lo store conosce ma il server no: e' il cambio di dispositivo e l'acquisto rimasto a
	/// meta'. Girare a vuoto costa poco, quindi si fa a ogni aggancio al server.
	/// </summary>
	private async Task SyncEntitlementsAsync(bool restore = true)
	{
		if (entitlementsSyncing || serverProgress == null)
			return;
		entitlementsSyncing = true;
		try
		{
			ApplyEntitlements(await serverProgress.GetEntitlementsAsync());
			if (!restore)
				return;
			IReadOnlyList<IapReceipt> owned = await IapService.FetchOwnedAsync();
			bool granted = false;
			for (int index = 0; index < owned.Count; index++)
			{
				IapReceipt receipt = owned[index];
				if (IapService.Entitlements.Owns(receipt.Product))
				{
					// Gia' concesso in una sessione precedente ma mai confermato allo store:
					// senza questa conferma Google rimborsa un acquisto che il giocatore ha.
					IapService.Confirm(receipt.Product);
					continue;
				}
				IapRedeemResult result = await serverProgress.RedeemPurchaseAsync(
					IapProducts.IdOf(receipt.Product), receipt.Receipt);
				ApplyEntitlements(result?.entitlements);
				if (result == null || !result.granted)
					continue;
				IapService.Confirm(receipt.Product);
				granted = true;
			}
			if (granted)
				await RefreshAfterPremiumGrantAsync();
		}
		catch (Exception exception)
		{
			AppendLog("NEGOZIO - sincronizzazione acquisti fallita: " + exception.Message);
		}
		finally
		{
			entitlementsSyncing = false;
		}
	}

	/// <summary>Il server ha concesso: la progressione locale non e' piu' quella giusta.</summary>
	private async Task RefreshAfterPremiumGrantAsync()
	{
		try
		{
			await serverProgress.RefreshAsync();
			MirrorServerProgress();
			RefreshSinglePlayerProgressView();
			if ((Object)(object)shopPanel != (Object)null && shopPanel.activeSelf)
			{
				sanctuaryData = await serverProgress.GetSanctuaryAsync();
				RefreshShop();
			}
		}
		catch (Exception exception)
		{
			AppendLog("NEGOZIO - aggiornamento dopo acquisto fallito: " + exception.Message);
		}
	}

	private void ApplyEntitlements(IapEntitlementsData data)
	{
		if (data == null)
			return;
		IapService.ApplyEntitlements(new IapEntitlements(data.noAds, data.allClasses, data.allSupreme));
	}

	/// <summary>
	/// Aggancia il layer acquisti al gioco: diario condiviso e recupero automatico degli
	/// ordini che lo store ripresenta da solo all'avvio.
	/// </summary>
	private void InitializeIapBridge()
	{
		IapService.Log = AppendLog;
		IapService.PurchaseRecovered -= HandleRecoveredPurchase;
		IapService.PurchaseRecovered += HandleRecoveredPurchase;
		IapService.EntitlementsChanged -= HandleEntitlementsChanged;
		IapService.EntitlementsChanged += HandleEntitlementsChanged;
		_ = IapService.InitializeAsync();
	}

	private async void HandleRecoveredPurchase(IapReceipt receipt)
	{
		if (receipt == null)
			return;
		if (!await EnsureServerProgressAsync())
			return;
		await RedeemPremiumAsync(receipt.Product, receipt);
	}

	private void HandleEntitlementsChanged()
	{
		AccardND.Ads.AdService.AdsRemoved = IapService.Entitlements.NoAds;
		if ((Object)(object)shopPanel != (Object)null && shopPanel.activeSelf)
			RefreshShop();
	}
}
}
