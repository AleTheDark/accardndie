using System.Collections;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const string MusicVolumePlayerPrefsKey = "AccardND.MusicVolume";

	private const string MusicMutedPlayerPrefsKey = "AccardND.MusicMuted";

	private AudioSource musicAudioSource;

	private Coroutine musicFadeRoutine;

	private BattleSfxPlayer battleSfx;

	private float musicVolume = 0.75f;

	private bool musicMuted;

	private AudioClip openCardInspectionSfx;

	private AudioClip closeCardInspectionSfx;

	private AudioClip buyCardSfx;

	private AudioClip arrowChangeSfx;

	private AudioClip transitionSfx;

	private AudioClip openBagSfx;

	private AudioClip closedBagSfx;

	private AudioClip lootRoomEnterSfx;

	private AudioClip monster1RoomEnterSfx;

	private AudioClip monster2RoomEnterSfx;

	private AudioClip monster3RoomEnterSfx;

	private AudioClip monster4RoomEnterSfx;

	private void InitializeAudio()
	{
		battleSfx = new BattleSfxPlayer();
		battleSfx.Initialize(transform);
		musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePlayerPrefsKey, 0.75f));
		musicMuted = PlayerPrefs.GetInt(MusicMutedPlayerPrefsKey, 0) != 0;

		GameObject musicObject = new GameObject("Music Audio Source");
		musicObject.transform.SetParent(transform, false);
		musicAudioSource = musicObject.AddComponent<AudioSource>();
		musicAudioSource.playOnAwake = false;
		musicAudioSource.loop = true;
		musicAudioSource.spatialBlend = 0f;
		UpdateMusicSourceVolume();

		openCardInspectionSfx = LoadSfx("open_card_inspection");
		closeCardInspectionSfx = LoadSfx("close_card_inspection");
		buyCardSfx = LoadSfx("buy_card");
		arrowChangeSfx = LoadSfx("arrow_change");
		transitionSfx = LoadSfx("transition");
		openBagSfx = LoadSfx("open_bag");
		closedBagSfx = LoadSfx("closed_bag");
		lootRoomEnterSfx = LoadSfx("loot_room_enter");
		monster1RoomEnterSfx = LoadSfx("monster_1_room");
		monster2RoomEnterSfx = LoadSfx("monster_2_room");
		monster3RoomEnterSfx = LoadSfx("monster_3_room_enter");
		monster4RoomEnterSfx = LoadSfx("monster_4_room");
	}

	private static AudioClip LoadSfx(string clipName)
	{
		return Resources.Load<AudioClip>("SFX/" + clipName);
	}

	private void PlaySfx(AudioClip clip, float volume = 1f)
	{
		battleSfx?.PlayClip(clip, volume);
	}

	private void IncreaseSfxVolume()
	{
		SetSfxVolume((battleSfx?.Volume ?? 1f) + 0.1f);
	}

	private void DecreaseSfxVolume()
	{
		SetSfxVolume((battleSfx?.Volume ?? 1f) - 0.1f);
	}

	private void SetSfxVolume(float volume)
	{
		battleSfx?.SetVolume(volume);
		RefreshSfxOptionsUi();
	}

	private void ToggleSfxMute()
	{
		battleSfx?.ToggleMute();
		RefreshSfxOptionsUi();
	}

	private void RefreshSfxOptionsUi()
	{
		if ((Object)(object)sfxVolumeText != (Object)null)
		{
			bool muted = battleSfx?.Muted ?? false;
			float volume = battleSfx?.Volume ?? 1f;
			sfxVolumeText.text = muted ? "MUTO" : Mathf.RoundToInt(volume * 100f) + "%";
		}
		if ((Object)(object)sfxMuteButtonText != (Object)null)
		{
			sfxMuteButtonText.text = battleSfx?.Muted == true ? "ATTIVA" : "MUTE";
		}
	}

	private void IncreaseMusicVolume()
	{
		SetMusicVolume(musicVolume + 0.1f);
	}

	private void DecreaseMusicVolume()
	{
		SetMusicVolume(musicVolume - 0.1f);
	}

	private void SetMusicVolume(float volume)
	{
		musicVolume = Mathf.Clamp01(volume);
		if (musicVolume > 0f)
		{
			musicMuted = false;
		}
		PlayerPrefs.SetFloat(MusicVolumePlayerPrefsKey, musicVolume);
		PlayerPrefs.SetInt(MusicMutedPlayerPrefsKey, musicMuted ? 1 : 0);
		PlayerPrefs.Save();
		UpdateMusicSourceVolume();
		RefreshMusicOptionsUi();
	}

	private void ToggleMusicMute()
	{
		musicMuted = !musicMuted;
		PlayerPrefs.SetInt(MusicMutedPlayerPrefsKey, musicMuted ? 1 : 0);
		PlayerPrefs.Save();
		UpdateMusicSourceVolume();
		RefreshMusicOptionsUi();
	}

	private void RefreshMusicOptionsUi()
	{
		if ((Object)(object)musicVolumeText != (Object)null)
		{
			musicVolumeText.text = musicMuted ? "MUTO" : Mathf.RoundToInt(musicVolume * 100f) + "%";
		}
		if ((Object)(object)musicMuteButtonText != (Object)null)
		{
			musicMuteButtonText.text = musicMuted ? "ATTIVA" : "MUTE";
		}
	}

	private void UpdateMusicSourceVolume()
	{
		if ((Object)(object)musicAudioSource == (Object)null)
		{
			return;
		}
		musicAudioSource.volume = musicMuted ? 0f : musicVolume;
	}

	private void PlayMusic(AudioClip clip)
	{
		if ((Object)(object)musicAudioSource == (Object)null || (Object)(object)clip == (Object)null)
		{
			return;
		}
		StopMusicFade();
		if ((Object)(object)musicAudioSource.clip == (Object)(object)clip && musicAudioSource.isPlaying)
		{
			UpdateMusicSourceVolume();
			return;
		}
		musicAudioSource.clip = clip;
		musicAudioSource.loop = true;
		UpdateMusicSourceVolume();
		Debug.Log($"[Music] Riproduco '{clip.name}' (volume {musicAudioSource.volume:0.00})");
		musicAudioSource.Play();
	}

	private void StopMusic()
	{
		if ((Object)(object)musicAudioSource == (Object)null)
		{
			return;
		}
		StopMusicFade();
		musicAudioSource.Stop();
		musicAudioSource.clip = null;
	}

	private void FadeOutMusic(float duration)
	{
		if ((Object)(object)musicAudioSource == (Object)null || !musicAudioSource.isPlaying)
		{
			return;
		}
		StopMusicFade();
		musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine(Mathf.Max(0.01f, duration)));
	}

	private IEnumerator FadeOutMusicRoutine(float duration)
	{
		float startVolume = musicAudioSource.volume;
		float elapsed = 0f;
		while (elapsed < duration && (Object)(object)musicAudioSource != (Object)null)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / duration);
			musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, progress);
			yield return null;
		}
		if ((Object)(object)musicAudioSource != (Object)null)
		{
			musicAudioSource.Stop();
			musicAudioSource.clip = null;
			UpdateMusicSourceVolume();
		}
		musicFadeRoutine = null;
	}

	private void StopMusicFade()
	{
		if (musicFadeRoutine == null)
		{
			return;
		}
		StopCoroutine(musicFadeRoutine);
		musicFadeRoutine = null;
		UpdateMusicSourceVolume();
	}

	private void PlayCardInspectionOpenSfx()
	{
		PlaySfx(openCardInspectionSfx);
	}

	private void PlayCardInspectionCloseSfx()
	{
		PlaySfx(closeCardInspectionSfx);
	}

	private void PlayGenericButtonClickSfx()
	{
		battleSfx?.PlayButtonClick();
	}

	private void PlayBuyCardSfx()
	{
		PlaySfx(buyCardSfx);
	}

	private void PlayArrowChangeSfx()
	{
		PlaySfx(arrowChangeSfx);
	}

	private void PlayTransitionSfx()
	{
		PlaySfx(transitionSfx);
	}

	private void PlayRollingDiceSfx()
	{
		battleSfx?.PlayRollingDice();
	}

	private void PlayDrawCardSfx()
	{
		battleSfx?.PlayDrawCard();
	}

	private void PlayPawnEnteringBattlefieldSfx()
	{
		battleSfx?.PlayJoinBattlefield();
	}

	private void PlayPawnEnteringBattlefieldSfx(CardDefinition definition)
	{
		battleSfx?.PlayJoinBattlefield(definition);
	}

	private void PlayPawnEnteringBattlefieldSfx(BattleCardState card)
	{
		PlayPawnEnteringBattlefieldSfx(card?.Definition);
	}

	private void PlayOpenBagSfx()
	{
		PlaySfx(openBagSfx);
	}

	private void PlayClosedBagSfx()
	{
		PlaySfx(closedBagSfx);
	}

	private void PlayDeathCardSfx()
	{
		battleSfx?.PlayDeath();
	}

	private void PlayAttachmentSfx()
	{
		battleSfx?.PlayAttachment();
	}

	private void PlayBarbarianFurySfx()
	{
		battleSfx?.PlayBarbarianFury();
	}

	private void PlayClassAbilitySfx(HeroClass heroClass)
	{
		battleSfx?.PlayClassAbility(heroClass);
	}

	private void PlayLootRoomEnterSfx()
	{
		PlaySfx(lootRoomEnterSfx);
	}

	private void PlayCurrentRoomEnterSfx()
	{
		if (currentRoomType != RoomType.Monster)
		{
			StopMusic();
			return;
		}
		PlayMusic(currentMonsterTier switch
		{
			1 => monster1RoomEnterSfx,
			2 => monster2RoomEnterSfx,
			3 => monster3RoomEnterSfx,
			4 => monster4RoomEnterSfx,
			_ => null
		});
	}

	private void PlayAttackResultSfx(BattleCardState attacker, bool hit)
	{
		if (attacker == null)
		{
			return;
		}
		if (hit && attacker.Card.HeroClass == HeroClass.Rogue)
		{
			return;
		}
		battleSfx?.PlayAttackResult(attacker.Card.HeroClass, hit);
	}
}
}
