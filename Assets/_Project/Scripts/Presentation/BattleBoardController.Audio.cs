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

	private AudioClip bossBragusSoundtrack;

	private AudioClip bossMedusaSoundtrack;

	private AudioClip bossPalantirSoundtrack;

	private AudioClip bossMedusaAttackSfx;

	private AudioClip bossMedusaDeathSfx;

	private AudioClip bossTrentorJoinBattlefieldSfx;

	private AudioClip bossTrentorAttackSfx;

	private AudioClip[] bossBragusAttackSfx;

	private AudioClip bossBragusAttackHitSfx;

	private AudioClip[] bossBragusTakeDamageSfx;

	private AudioClip bossBragusDeathSfx;

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
		bossBragusSoundtrack = LoadSfx("boss_bragus_soundtrack");
		bossMedusaSoundtrack = LoadSfx("boss_medusa_soundtrack");
		bossPalantirSoundtrack = LoadSfx("boss_palantir_soundtrack");
		bossMedusaAttackSfx = LoadSfx("boss_medusa_attack");
		bossMedusaDeathSfx = LoadSfx("boss_medusa_death");
		bossTrentorJoinBattlefieldSfx = LoadSfx("boss_trentor_join_battlefield");
		bossTrentorAttackSfx = LoadSfx("boss_trentor_attack");
		bossBragusAttackSfx = LoadSfxSet("boss_bragus_attack", 3);
		bossBragusAttackHitSfx = LoadSfx("boss_bragus_attack_hit");
		bossBragusTakeDamageSfx = LoadSfxSet("boss_bragus_takedamage", 3);
		bossBragusDeathSfx = LoadSfx("boss_bragus_death");
	}

	private static AudioClip LoadSfx(string clipName)
	{
		return Resources.Load<AudioClip>("SFX/" + clipName);
	}

	private static AudioClip[] LoadSfxSet(string clipNamePrefix, int count)
	{
		AudioClip[] clips = new AudioClip[Mathf.Max(0, count)];
		for (int index = 0; index < clips.Length; index++)
		{
			clips[index] = LoadSfx($"{clipNamePrefix}_{index + 1}");
		}
		return clips;
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

	private void PlayFootstepSfx()
	{
		battleSfx?.PlayFootstep();
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

	private void PlayDetectorItemUseSfx()
	{
		battleSfx?.PlayDetectorItemUse();
	}

	private void PlayEmpowerItemUseSfx()
	{
		battleSfx?.PlayEmpowerItemUse();
	}

	private void PlayClassAbilitySfx(HeroClass heroClass)
	{
		battleSfx?.PlayClassAbility(heroClass);
	}

	private void PlayComposableGolemAttackSfx(ComposableGolemForm form)
	{
		battleSfx?.PlayComposableGolemAttack(form);
	}

	private void PlayMedusaPetrifyingGazeSfx()
	{
		PlaySfx(bossMedusaAttackSfx);
	}

	private void PlayMedusaDeathSfx()
	{
		PlaySfx(bossMedusaDeathSfx);
	}

	private void PlayTrentorJoinBattlefieldSfx()
	{
		PlaySfx(bossTrentorJoinBattlefieldSfx);
	}

	private void PlayTrentorAttackSfx()
	{
		PlaySfx(bossTrentorAttackSfx);
	}

	private void PlayBragusAttackSfx()
	{
		PlayRandomSfx(bossBragusAttackSfx);
	}

	private void PlayBragusAttackHitSfx()
	{
		PlaySfx(bossBragusAttackHitSfx);
	}

	private void PlayBragusTakeDamageSfx()
	{
		PlayRandomSfx(bossBragusTakeDamageSfx);
	}

	private void PlayBragusDeathSfx()
	{
		PlaySfx(bossBragusDeathSfx);
	}

	private void PlayPalatirCosmicAttackSfx()
	{
		battleSfx?.PlayClassAbility(HeroClass.Mage);
	}

	private void PlayRandomSfx(AudioClip[] clips)
	{
		if (clips == null || clips.Length == 0)
		{
			return;
		}

		int startIndex = random != null ? random.NextInclusive(0, clips.Length - 1) : Random.Range(0, clips.Length);
		for (int offset = 0; offset < clips.Length; offset++)
		{
			AudioClip clip = clips[(startIndex + offset) % clips.Length];
			if ((Object)(object)clip != (Object)null)
			{
				PlaySfx(clip);
				return;
			}
		}
	}

	private void PlayLootRoomEnterSfx()
	{
		PlaySfx(lootRoomEnterSfx);
	}

	private void PlayCurrentRoomEnterSfx()
	{
		if (currentRoomType == RoomType.Boss && IsMedusaMusicRoom())
		{
			PlayMusic(bossMedusaSoundtrack);
			return;
		}
		if (currentRoomType == RoomType.Boss && IsPalatirMusicRoom())
		{
			PlayMusic(bossPalantirSoundtrack);
			return;
		}
		if (currentRoomType == RoomType.Boss && IsBragusMusicRoom())
		{
			PlayMusic(bossBragusSoundtrack);
			return;
		}
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

	private bool IsMedusaMusicRoom()
	{
		if (activeMedusaBoss != null)
		{
			return true;
		}
		if (IsFinalBossRoom())
		{
			return true;
		}
		return (Object)(object)currentScenario != (Object)null
			&& string.Equals(currentScenario.BossId, MedusaBossCardId, System.StringComparison.OrdinalIgnoreCase);
	}

	private bool IsPalatirMusicRoom()
	{
		if (activePalatirBoss != null)
		{
			return true;
		}
		return (Object)(object)currentScenario != (Object)null
			&& string.Equals(currentScenario.BossId, PalatirBossCardId, System.StringComparison.OrdinalIgnoreCase);
	}

	private bool IsBragusMusicRoom()
	{
		if (activeBragusBoss != null)
		{
			return true;
		}
		return (Object)(object)currentScenario != (Object)null
			&& string.Equals(currentScenario.BossId, BragusBossCardId, System.StringComparison.OrdinalIgnoreCase);
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

	private void PlayResolvedAttackSfx(BattleCardState attacker, bool hit, bool abilityAttack)
	{
		if (abilityAttack && hit && attacker?.Card.HeroClass == HeroClass.Warrior)
		{
			PlayClassAbilitySfx(HeroClass.Warrior);
			return;
		}
		PlayAttackResultSfx(attacker, hit);
	}
}
}
