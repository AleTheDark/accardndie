using System;
using AccardND.GameData;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const int TurnCoinLayer = 30;
	private const float TurnCoinFlipDuration = 0.45f;
	private const bool TurnCoinEnabled = true;
	private RawImage turnCoinImage;
	private Transform turnCoinTransform;
	private RenderTexture turnCoinTexture;
	private Quaternion turnCoinTargetRotation = Quaternion.identity;
	private Quaternion turnCoinStartRotation = Quaternion.identity;
	private float turnCoinFlipElapsed = TurnCoinFlipDuration;
	private bool turnCoinPointsToPlayer;
	private bool turnCoinStateAvailable;
	private bool turnCoinShouldBeVisible;
	private bool turnCoinSuppressed;
	private bool turnCoinFlipPending;

	private void SetTurnCoinState(bool playerTurn, bool visible)
	{
		if (!TurnCoinEnabled)
		{
			turnCoinShouldBeVisible = false;
			RefreshTurnCoinVisibility();
			return;
		}

		bool hadState = turnCoinStateAvailable;
		bool directionChanged = hadState && turnCoinPointsToPlayer != playerTurn;
		bool becomingVisible = visible && !turnCoinShouldBeVisible;
		if (directionChanged || becomingVisible)
		{
			turnCoinStartRotation = Quaternion.Euler(
				0f,
				0f,
				playerTurn ? 180f : 0f);
			turnCoinFlipPending = true;
		}
		turnCoinStateAvailable = true;
		turnCoinShouldBeVisible = visible;
		turnCoinPointsToPlayer = playerTurn;
		turnCoinTargetRotation = Quaternion.Euler(0f, 0f, playerTurn ? 0f : 180f);
		if (turnCoinSuppressed)
		{
			RefreshTurnCoinVisibility();
			return;
		}
		EnsureTurnCoinView();
		StartPendingTurnCoinFlip();
		RefreshTurnCoinVisibility();
	}

	private void StartPendingTurnCoinFlip()
	{
		if (!turnCoinFlipPending || (Object)(object)turnCoinTransform == (Object)null)
		{
			return;
		}

		turnCoinFlipPending = false;
		turnCoinTransform.localRotation = turnCoinStartRotation;
		turnCoinTransform.localScale = Vector3.one;
		turnCoinFlipElapsed = 0f;
		if (!adventureScriptedTutorialActive)
		{
			PlaySfx(coinFlipSfx);
		}
	}

	private void UpdateTurnCoinAnimation()
	{
		if (!TurnCoinEnabled || !turnCoinStateAvailable || turnCoinSuppressed)
		{
			return;
		}

		EnsureTurnCoinView();
		PlaceTurnCoinBelowPawns();
		RefreshTurnCoinVisibility();
		if ((Object)(object)turnCoinTransform != (Object)null && turnCoinFlipElapsed < TurnCoinFlipDuration)
		{
			turnCoinFlipElapsed = Mathf.Min(TurnCoinFlipDuration, turnCoinFlipElapsed + Time.unscaledDeltaTime);
			float progress = turnCoinFlipElapsed / TurnCoinFlipDuration;
			// Simula il flip 3D sulla texture UI: la moneta si assottiglia fino
			// al bordo, cambia verso a meta corsa e torna alla larghezza piena.
			float height = Mathf.Abs(Mathf.Cos(progress * Mathf.PI));
			turnCoinTransform.localScale = new Vector3(1f, height, 1f);
			turnCoinTransform.localRotation = progress < 0.5f
				? turnCoinStartRotation
				: turnCoinTargetRotation;
			if (progress >= 1f)
			{
				turnCoinTransform.localScale = Vector3.one;
			}
		}
	}

	private void RefreshTurnCoinVisibility()
	{
		if ((Object)(object)turnCoinImage == (Object)null)
		{
			return;
		}

		bool visible = TurnCoinEnabled
			&& turnCoinShouldBeVisible
			&& !turnCoinSuppressed
			&& !adventureScriptedTutorialActive
			&& (pvpPresentationActive || currentRoomType == RoomType.Monster);
		if (((Component)turnCoinImage).gameObject.activeSelf != visible)
		{
			((Component)turnCoinImage).gameObject.SetActive(visible);
		}
	}

	private bool IsTurnCoinVisiblePhase()
	{
		// La moneta indica di chi e' il turno: serve mentre si schiera e per
		// tutto il combattimento, dove il pannello messaggi sparisce nei turni
		// della CPU e resterebbe altrimenti nessun segnale a schermo.
		if (pvpPresentationActive)
		{
			return pvpState != null
				&& (pvpState.Phase == PvpClientPhase.Deployment
					|| pvpState.Phase == PvpClientPhase.Battle);
		}

		if (IsBackdropBossTurnCoinSuppressed())
		{
			return false;
		}

		if (deploymentDraftActive)
		{
			return !UsesBossStyleDeployment();
		}

		return IsTurnCoinCombatPhase();
	}

	private bool IsTurnCoinCombatPhase()
	{
		return roundNumber > 0
			&& !draftActive
			&& !gameFinished
			&& currentRoomType == RoomType.Monster
			&& (playerCards.Count > 0 || cpuCards.Count > 0);
	}

	private bool IsBackdropBossTurnCoinSuppressed()
	{
		return debugForceFirstRoomBragus
			|| debugForceFirstRoomTrentor
			|| debugForceFirstRoomSeraphel
			|| bragusBossPresentationActive
			|| trentorBossPresentationActive
			|| activeBragusBoss != null
			|| activeTrentorBoss != null
			|| ((Object)(object)currentScenario != (Object)null
				&& (string.Equals(
						currentScenario.BossId,
						BragusBossCardId,
						System.StringComparison.OrdinalIgnoreCase)
					|| string.Equals(
						currentScenario.BossId,
						TrentorBossCardId,
						System.StringComparison.OrdinalIgnoreCase)));
	}

	private void SetTurnCoinSuppressed(bool suppressed)
	{
		turnCoinSuppressed = suppressed;
		if (!suppressed && turnCoinStateAvailable)
		{
			EnsureTurnCoinView();
			PlaceTurnCoinBelowPawns();
			StartPendingTurnCoinFlip();
		}
		RefreshTurnCoinVisibility();
	}

	private void EnsureTurnCoinView()
	{
		if ((Object)(object)turnCoinImage != (Object)null || (Object)(object)safeAreaRoot == (Object)null)
		{
			return;
		}

		GameObject imageObject = new GameObject("3D Turn Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
		imageObject.transform.SetParent((Transform)(object)safeAreaRoot, false);
		turnCoinImage = imageObject.GetComponent<RawImage>();
		turnCoinImage.raycastTarget = false;
		turnCoinImage.color = Color.white;
		RectTransform imageRect = (RectTransform)imageObject.transform;
		imageRect.anchorMin = new Vector2(0.5f, 0.5f);
		imageRect.anchorMax = new Vector2(0.5f, 0.5f);
		imageRect.pivot = new Vector2(0.5f, 0.5f);
		imageRect.anchoredPosition = new Vector2(0f, -55f);
		imageRect.sizeDelta = new Vector2(135.375f, 135.375f);
		imageRect.localRotation = Quaternion.Euler(0f, 0f, turnCoinPointsToPlayer ? 0f : 180f);
		turnCoinTransform = imageRect;
		PlaceTurnCoinBelowPawns();

		turnCoinTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
		{
			name = "Turn Coin Render Texture",
			antiAliasing = 4
		};
		turnCoinTexture.Create();
		turnCoinImage.texture = turnCoinTexture;

		Vector3 stagePosition = new Vector3(10000f, 10000f, 10000f);
		GameObject stage = new GameObject("Turn Coin 3D Stage");
		stage.transform.position = stagePosition;

		GameObject cameraObject = new GameObject("Turn Coin Camera", typeof(Camera));
		cameraObject.transform.SetParent(stage.transform, false);
		cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);
		Camera coinCamera = cameraObject.GetComponent<Camera>();
		coinCamera.clearFlags = CameraClearFlags.SolidColor;
		coinCamera.backgroundColor = Color.clear;
		coinCamera.cullingMask = 1 << TurnCoinLayer;
		coinCamera.orthographic = true;
		coinCamera.orthographicSize = 1.45f;
		coinCamera.nearClipPlane = 0.1f;
		coinCamera.farClipPlane = 10f;
		coinCamera.allowHDR = false;
		coinCamera.targetTexture = turnCoinTexture;

		GameObject lightObject = new GameObject("Turn Coin Light", typeof(Light));
		lightObject.transform.SetParent(stage.transform, false);
		lightObject.transform.localRotation = Quaternion.Euler(35f, -25f, 0f);
		Light coinLight = lightObject.GetComponent<Light>();
		coinLight.type = LightType.Directional;
		coinLight.color = Color.white;
		coinLight.intensity = 1.15f;
		coinLight.cullingMask = 1 << TurnCoinLayer;

		GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		coin.name = "Turn Coin";
		coin.transform.SetParent(stage.transform, false);
		coin.transform.localScale = new Vector3(1f, 0.12f, 1f);
		// Allo spawn scegli subito la faccia già orientata verso chi deve
		// schierare: in basso per il giocatore, in alto per la CPU.
		coin.transform.localRotation = Quaternion.Euler(
			90f,
			0f,
			turnCoinPointsToPlayer ? 180f : 0f);
		SetLayerRecursively(coin, TurnCoinLayer);
		Texture2D coinFace = Resources.Load<Texture2D>("UI/TurnCoin/turn_coin_arrow_aaa");
		ApplyTurnCoinMaterial(coin.GetComponent<Renderer>(), Color.white, 0.35f, coinFace);
		// Usa la texture direttamente nella Canvas: su alcuni device mobili la
		// camera fuori scena può lasciare la RenderTexture vuota.
		turnCoinImage.texture = coinFace;
		stage.SetActive(false);
	}

	private void PlaceTurnCoinBelowPawns()
	{
		if ((Object)(object)turnCoinImage == (Object)null)
		{
			return;
		}

		int siblingIndex = ((Component)turnCoinImage).transform.GetSiblingIndex();
		if ((Object)(object)cpuRow != (Object)null)
		{
			siblingIndex = Mathf.Min(siblingIndex, ((Transform)cpuRow).GetSiblingIndex());
		}
		if ((Object)(object)playerRow != (Object)null)
		{
			siblingIndex = Mathf.Min(siblingIndex, ((Transform)playerRow).GetSiblingIndex());
		}
		((Component)turnCoinImage).transform.SetSiblingIndex(siblingIndex);
	}

	private static void ApplyTurnCoinMaterial(Renderer target, Color color, float metallic, Texture texture)
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
		if ((Object)(object)shader == (Object)null)
		{
			return;
		}
		Material material = new Material(shader);
		material.color = color;
		if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
		if ((Object)(object)texture != (Object)null)
		{
			if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
			if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
		}
		if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
		if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.72f);
		target.material = material;
	}

	private static void SetLayerRecursively(GameObject target, int layer)
	{
		target.layer = layer;
		foreach (Transform child in target.transform)
		{
			SetLayerRecursively(((Component)child).gameObject, layer);
		}
	}
}
}
