using System.Collections;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const string MusicVolumePlayerPrefsKey = "AccardND.MusicVolume";

	private const string MusicMutedPlayerPrefsKey = "AccardND.MusicMuted";

	private const float DefaultMusicFadeOutDuration = 1.2f;

	private const float MusicSwitchFadeOutDuration = 0.45f;

	private AudioSource musicAudioSource;

	private AudioSource pvpTimerAudioSource;

	private MusicFadeRunner musicFadeRunner;

	private bool musicFadeActive;

	private BattleSfxPlayer battleSfx;

	private float musicVolume = 0.75f;

	private bool musicMuted;

	/// <summary>Rifiuto per mana insufficiente, accompagna il callout NO MANA.</summary>
	private AudioClip noManaSfx;

	private AudioClip pvpTimerSfx;

	private AudioClip actionTargetingSfx;

	private AudioClip openCardInspectionSfx;

	private AudioClip closeCardInspectionSfx;

	private AudioClip buyCardSfx;

	private AudioClip forgeHitSfx;

	private AudioClip arrowChangeSfx;

	private AudioClip coinFlipSfx;

	private AudioClip transitionSfx;

	private AudioClip levelUpSfx;

	private AudioClip talentAcquiredSfx;

	private AudioClip openBagSfx;

	private AudioClip closedBagSfx;

	private AudioClip lootRoomEnterSfx;

	private AudioClip accessibleMonsterRoomEnterSfx;

	private AudioClip normalMonsterRoomEnterSfx;

	private AudioClip apocalypticMonsterRoomEnterSfx;

	private AudioClip hubNightSoundtrack;

	private AudioClip hubDaySoundtrack;

	private AudioClip tutorialSoundtrack;

	private AudioClip[] pvpArenaSoundtracks;

	private AudioClip gameOverSoundtrack;

	private AudioClip victorySfx;

	private AudioClip bossBragusSoundtrack;

	private AudioClip bossJurinashorSoundtrack;

	private AudioClip jurinashorWeaponEvocationSfx;

	private AudioClip bossJurinashorJoinBattlefieldSfx;

	private AudioClip bossMedusaSoundtrack;

	private AudioClip bossPalantirSoundtrack;

	private AudioClip bossMedusaAttackSfx;

	private AudioClip bossMedusaDeathSfx;

	private AudioClip bossTrentorJoinBattlefieldSfx;

	private AudioClip bossTrentorAttackSfx;

	private AudioClip[] bossTrentorTakeDamageSfx;

	private AudioClip[] bossBragusAttackSfx;

	private AudioClip bossBragusAttackHitSfx;

	private AudioClip[] bossBragusTakeDamageSfx;

	private AudioClip bossBragusDeathSfx;

	private AudioClip transformationSeraphelSfx;

	private AudioClip seraphelHealSfx;

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
		musicFadeRunner = musicObject.AddComponent<MusicFadeRunner>();
		UpdateMusicSourceVolume();

		GameObject pvpTimerObject = new GameObject("PvP Timer SFX Audio Source");
		pvpTimerObject.transform.SetParent(transform, false);
		pvpTimerAudioSource = pvpTimerObject.AddComponent<AudioSource>();
		pvpTimerAudioSource.playOnAwake = false;
		pvpTimerAudioSource.loop = true;
		pvpTimerAudioSource.spatialBlend = 0f;

		openCardInspectionSfx = LoadSfx("open_card_inspection");
		closeCardInspectionSfx = LoadSfx("close_card_inspection");
		noManaSfx = LoadSfx("no_mana");
		pvpTimerSfx = LoadSfx("timer_out");
		actionTargetingSfx = LoadSfx("click_on_action");
		buyCardSfx = LoadSfx("buy_card");
		forgeHitSfx = LoadSfx("forge_hit");
		arrowChangeSfx = LoadSfx("arrow_change");
		coinFlipSfx = LoadSfx("coin_flip");
		transitionSfx = LoadSfx("transition");
		levelUpSfx = LoadSfx("level_up");
		talentAcquiredSfx = LoadSfx("talent_acquired");
		openBagSfx = LoadSfx("open_bag");
		closedBagSfx = LoadSfx("closed_bag");
		lootRoomEnterSfx = LoadSfx("loot_room_enter");
		accessibleMonsterRoomEnterSfx = LoadSfx("monster_accessible_room");
		normalMonsterRoomEnterSfx = LoadSfx("monster_normal_room");
		apocalypticMonsterRoomEnterSfx = LoadSfx("monster_apocalyptic_room");
		hubNightSoundtrack = LoadSfx("hub_night");
		hubDaySoundtrack = LoadSfx("hub_day") ?? hubNightSoundtrack;
		tutorialSoundtrack = LoadSfx("tutorial_song");
		pvpArenaSoundtracks = new[]
		{
			LoadSfx("arena_1"),
			LoadSfx("arena_2"),
			LoadSfx("arena_3")
		};
		gameOverSoundtrack = LoadSfx("game_over");
		victorySfx = LoadSfx("victory_pvp");
		bossBragusSoundtrack = LoadSfx("boss_bragus_soundtrack");
		// Jurinashor uses the standard boss music until a dedicated track is provided.
		bossJurinashorSoundtrack = LoadSfx("boss_jurinashor_soundtrack") ?? bossBragusSoundtrack;
		jurinashorWeaponEvocationSfx = LoadSfx("jurinashor_weapon_evocation");
		bossJurinashorJoinBattlefieldSfx = LoadSfx("boss_jurinashor_join_battlefield");
		bossMedusaSoundtrack = LoadSfx("boss_medusa_soundtrack");
		bossPalantirSoundtrack = LoadSfx("boss_palantir_soundtrack");
		bossMedusaAttackSfx = LoadSfx("boss_medusa_attack");
		bossMedusaDeathSfx = LoadSfx("boss_medusa_death");
		bossTrentorJoinBattlefieldSfx = LoadSfx("boss_trentor_join_battlefield");
		bossTrentorAttackSfx = LoadSfx("boss_trentor_attack");
		bossTrentorTakeDamageSfx = LoadSfxSet("boss_trentor_takedamage", 3);
		bossBragusAttackSfx = LoadSfxSet("boss_bragus_attack", 3);
		bossBragusAttackHitSfx = LoadSfx("boss_bragus_attack_hit");
		bossBragusTakeDamageSfx = LoadSfxSet("boss_bragus_takedamage", 3);
		bossBragusDeathSfx = LoadSfx("boss_bragus_death");
		transformationSeraphelSfx = LoadSfx("transformation_seraphel");
		seraphelHealSfx = LoadSfx("seraphel_heal");
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

	/// <summary>
	/// Feedback condiviso quando un'azione entra nella modalita di scelta bersaglio.
	/// Va chiamato sul bottone dell'azione, mai sul click della pedina di destinazione.
	/// </summary>
	private void PlayActionTargetingSfx()
	{
		PlaySfx(actionTargetingSfx);
	}

	private void StartPvpTimerSfx()
	{
		if ((Object)(object)pvpTimerAudioSource == (Object)null
			|| (Object)(object)pvpTimerSfx == (Object)null
			|| pvpTimerAudioSource.isPlaying)
		{
			return;
		}

		pvpTimerAudioSource.clip = pvpTimerSfx;
		pvpTimerAudioSource.volume = battleSfx?.Volume ?? 1f;
		pvpTimerAudioSource.Play();
	}

	private void StopPvpTimerSfx()
	{
		if ((Object)(object)pvpTimerAudioSource == (Object)null)
			return;

		pvpTimerAudioSource.Stop();
		pvpTimerAudioSource.clip = null;
	}

	private void PlayLevelUpSfx()
	{
		PlaySfx(levelUpSfx);
	}

	private void PlayTalentAcquiredSfx()
	{
		PlaySfx(talentAcquiredSfx);
	}

	private void PlaySeraphelTransformationSfx()
	{
		PlaySfx(transformationSeraphelSfx);
	}

	private void PlaySeraphelHealSfx()
	{
		PlaySfx(seraphelHealSfx);
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
			sfxVolumeText.text = muted
				? GameText.Get(GameTextKeys.Common.Muted)
				: GameText.Format(GameTextKeys.Audio.VolumePercent, Mathf.RoundToInt(volume * 100f));
		}
		if ((Object)(object)sfxVolumeSlider != (Object)null)
			sfxVolumeSlider.SetValueWithoutNotify(battleSfx?.Volume ?? 1f);
		if ((Object)(object)sfxMuteButtonText != (Object)null)
		{
			sfxMuteButtonText.text = LocalizedOptionsAudioAction(battleSfx?.Muted == true);
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
			musicVolumeText.text = musicMuted
				? GameText.Get(GameTextKeys.Common.Muted)
				: GameText.Format(GameTextKeys.Audio.VolumePercent, Mathf.RoundToInt(musicVolume * 100f));
		}
		if ((Object)(object)musicVolumeSlider != (Object)null)
			musicVolumeSlider.SetValueWithoutNotify(musicVolume);
		if ((Object)(object)musicMuteButtonText != (Object)null)
		{
			musicMuteButtonText.text = LocalizedOptionsAudioAction(musicMuted);
		}
	}

	private static string LocalizedOptionsAudioAction(bool muted)
	{
		return muted
			? GameText.GetLocalizedFallback(GameTextKeys.Options.Unmute, "ATTIVA AUDIO", "UNMUTE", "TON EINSCHALTEN", "ACTIVAR SONIDO", "ACTIVER LE SON")
			: GameText.GetLocalizedFallback(GameTextKeys.Options.Mute, "MUTE", "MUTE", "STUMMSCHALTEN", "SILENCIAR", "COUPER LE SON");
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
		if (musicAudioSource.isPlaying)
		{
			StopMusicFade();
			StartMusicFade(SwitchMusicRoutine(clip, MusicSwitchFadeOutDuration));
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
		FadeOutMusic(DefaultMusicFadeOutDuration);
	}

	private void FadeOutMusic(float duration)
	{
		if ((Object)(object)musicAudioSource == (Object)null || !musicAudioSource.isPlaying)
		{
			return;
		}
		StopMusicFade();
		StartMusicFade(FadeOutMusicRoutine(Mathf.Max(0.01f, duration)));
	}

	private void StartMusicFade(IEnumerator routine)
	{
		if ((Object)(object)musicFadeRunner == (Object)null)
		{
			StartCoroutine(routine);
			return;
		}
		musicFadeActive = true;
		musicFadeRunner.Play(routine);
	}

	private IEnumerator SwitchMusicRoutine(AudioClip clip, float duration)
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
			musicAudioSource.clip = clip;
			musicAudioSource.loop = true;
			UpdateMusicSourceVolume();
			Debug.Log($"[Music] Riproduco '{clip.name}' (volume {musicAudioSource.volume:0.00})");
			musicAudioSource.Play();
		}
		CompleteMusicFade();
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
		CompleteMusicFade();
	}

	private void CompleteMusicFade()
	{
		musicFadeActive = false;
		if ((Object)(object)musicFadeRunner != (Object)null)
		{
			musicFadeRunner.ClearActive();
		}
	}

	private void StopMusicFade()
	{
		if (!musicFadeActive)
		{
			return;
		}
		if ((Object)(object)musicFadeRunner != (Object)null)
		{
			musicFadeRunner.StopActive();
		}
		musicFadeActive = false;
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

	private void PlayForgeHitSfx()
	{
		PlaySfx(forgeHitSfx);
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
		if ((Object)(object)definition != (Object)null && IsJurinashorBossDefinition(definition))
		{
			PlaySfx(bossJurinashorJoinBattlefieldSfx);
			return;
		}
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

	private void PlayWarriorSupremeSfx()
	{
		battleSfx?.PlayWarriorSupreme();
	}

	private void PlayBarbarianSupremeSfx()
	{
		battleSfx?.PlayBarbarianSupreme();
	}

	private void PlayMageSupremeSfx()
	{
		battleSfx?.PlayMageSupreme();
	}

	private void PlayAssassinSupremeSfx()
	{
		battleSfx?.PlayAssassinSupreme();
	}

	private void PlayPriestSupremeSfx()
	{
		battleSfx?.PlayPriestSupreme();
	}

	private void PlayNecromancerSupremeSfx()
	{
		battleSfx?.PlayNecromancerSupreme();
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

	private void PlayTrentorTakeDamageSfx()
	{
		PlayRandomSfx(bossTrentorTakeDamageSfx);
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
		if (currentRoomType == RoomType.Boss && IsJurinashorMusicRoom())
		{
			PlayMusic(bossJurinashorSoundtrack);
			return;
		}
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
		PlayMusic(pendingRoomDifficulty switch
		{
			RoomDifficulty.Easy => accessibleMonsterRoomEnterSfx,
			RoomDifficulty.Hard => apocalypticMonsterRoomEnterSfx,
			_ => normalMonsterRoomEnterSfx
		});
	}

	private void PlayCurrentHubMusic()
	{
		AudioClip clip = IsCurrentHubNight() ? hubNightSoundtrack : hubDaySoundtrack;
		PlayMusic(clip ?? hubNightSoundtrack);
	}

	private void PlayTutorialMusic()
	{
		if ((Object)(object)tutorialSoundtrack == (Object)null)
		{
			Debug.LogWarning("[Music] Traccia tutorial_song non trovata.");
			return;
		}
		PlayMusic(tutorialSoundtrack);
	}

	private void PlayPvpArenaMusic(int matchRound)
	{
		int index = Mathf.Clamp(matchRound <= 0 ? 1 : matchRound, 1, 3) - 1;
		AudioClip clip = pvpArenaSoundtracks != null && index < pvpArenaSoundtracks.Length
			? pvpArenaSoundtracks[index]
			: null;
		if ((Object)(object)clip == (Object)null)
		{
			Debug.LogWarning($"[Music] Traccia arena_{index + 1} non trovata.");
			return;
		}
		PlayMusic(clip);
	}

	private void StopPvpArenaMusic()
	{
		if ((Object)(object)musicAudioSource == (Object)null
			|| pvpArenaSoundtracks == null
			|| !System.Array.Exists(
				pvpArenaSoundtracks,
				clip => (Object)(object)clip == (Object)(object)musicAudioSource.clip))
		{
			return;
		}

		// L'uscita dal PvP e' un confine netto: nessun fade/coroutine Arena deve
		// poter sopravvivere e riscrivere il volume o ripartire sopra la musica Hub.
		StopMusicFade();
		musicAudioSource.Stop();
		musicAudioSource.clip = null;
		UpdateMusicSourceVolume();
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

	private bool IsJurinashorMusicRoom()
	{
		if (activeJurinashorBoss != null)
		{
			return true;
		}
		return (Object)(object)currentScenario != (Object)null
			&& string.Equals(currentScenario.BossId, JurinashorBossCardId, System.StringComparison.OrdinalIgnoreCase);
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

internal sealed class MusicFadeRunner : MonoBehaviour
{
	private Coroutine activeRoutine;

	public void Play(IEnumerator routine)
	{
		StopActive();
		activeRoutine = StartCoroutine(routine);
	}

	public void StopActive()
	{
		if (activeRoutine == null)
		{
			return;
		}
		StopCoroutine(activeRoutine);
		activeRoutine = null;
	}

	public void ClearActive()
	{
		activeRoutine = null;
	}
}
}
