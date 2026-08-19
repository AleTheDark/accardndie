using AccardND.AudioKit;
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

	private MusicChannel musicChannel;

	private AudioSource pvpTimerAudioSource;

	private BattleSfxPlayer battleSfx;

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
		musicChannel = MusicChannel.Create(transform, MusicVolumePlayerPrefsKey, MusicMutedPlayerPrefsKey);
		musicChannel.Changed += RefreshMusicOptionsUi;

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

	/// <summary>Il canale vive come figlio del controller: qui vale il controllo di vita di Unity.</summary>
	private bool HasMusicChannel => (Object)(object)musicChannel != (Object)null;

	private float MusicVolume => HasMusicChannel ? musicChannel.Volume : 0.75f;

	private bool MusicMuted => HasMusicChannel && musicChannel.Muted;

	private void IncreaseMusicVolume()
	{
		SetMusicVolume(MusicVolume + 0.1f);
	}

	private void DecreaseMusicVolume()
	{
		SetMusicVolume(MusicVolume - 0.1f);
	}

	private void SetMusicVolume(float volume)
	{
		if (HasMusicChannel)
		{
			// SetVolume persiste e notifica Changed, che riaggiorna la UI delle opzioni.
			musicChannel.SetVolume(volume);
		}
	}

	private void ToggleMusicMute()
	{
		if (HasMusicChannel)
		{
			musicChannel.ToggleMute();
		}
	}

	private void RefreshMusicOptionsUi()
	{
		bool muted = MusicMuted;
		float volume = MusicVolume;
		if ((Object)(object)musicVolumeText != (Object)null)
		{
			musicVolumeText.text = muted
				? GameText.Get(GameTextKeys.Common.Muted)
				: GameText.Format(GameTextKeys.Audio.VolumePercent, Mathf.RoundToInt(volume * 100f));
		}
		if ((Object)(object)musicVolumeSlider != (Object)null)
			musicVolumeSlider.SetValueWithoutNotify(volume);
		if ((Object)(object)musicMuteButtonText != (Object)null)
		{
			musicMuteButtonText.text = LocalizedOptionsAudioAction(muted);
		}
	}

	private static string LocalizedOptionsAudioAction(bool muted)
	{
		return muted
			? GameText.Get(GameTextKeys.Options.Unmute)
			: GameText.Get(GameTextKeys.Options.Mute);
	}

	private void PlayMusic(AudioClip clip)
	{
		if (HasMusicChannel)
		{
			musicChannel.Play(clip);
		}
	}

	private void StopMusic()
	{
		if (HasMusicChannel)
		{
			musicChannel.Stop();
		}
	}

	private void FadeOutMusic(float duration)
	{
		if (HasMusicChannel)
		{
			musicChannel.FadeOut(duration);
		}
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
		if (!HasMusicChannel || pvpArenaSoundtracks == null)
		{
			return;
		}

		AudioClip playing = musicChannel.CurrentClip;
		if (!System.Array.Exists(
			pvpArenaSoundtracks,
			clip => (Object)(object)clip == (Object)(object)playing))
		{
			return;
		}

		// L'uscita dal PvP e' un confine netto: nessun fade/coroutine Arena deve
		// poter sopravvivere e riscrivere il volume o ripartire sopra la musica Hub.
		musicChannel.StopImmediate();
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

}
