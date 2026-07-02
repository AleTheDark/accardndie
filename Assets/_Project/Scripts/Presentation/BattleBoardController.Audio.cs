using System.Collections;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const string SfxVolumePlayerPrefsKey = "AccardND.SfxVolume";

	private const string SfxMutedPlayerPrefsKey = "AccardND.SfxMuted";

	private const string MusicVolumePlayerPrefsKey = "AccardND.MusicVolume";

	private const string MusicMutedPlayerPrefsKey = "AccardND.MusicMuted";

	private AudioSource sfxAudioSource;

	private AudioSource musicAudioSource;

	private Coroutine musicFadeRoutine;

	private float sfxVolume = 1f;

	private bool sfxMuted;

	private float musicVolume = 0.75f;

	private bool musicMuted;

	private AudioClip openCardInspectionSfx;

	private AudioClip closeCardInspectionSfx;

	private AudioClip genericButtonClickSfx;

	private AudioClip buyCardSfx;

	private AudioClip arrowChangeSfx;

	private AudioClip transitionSfx;

	private AudioClip rollingDiceSfx;

	private AudioClip pawnEnteringBattlefieldSfx;

	private AudioClip warriorJoinBattlefieldSfx;

	private AudioClip assassinJoinBattlefieldSfx;

	private AudioClip barbarianJoinBattlefieldSfx;

	private AudioClip mageJoinBattlefieldSfx;

	private AudioClip paladinJoinBattlefieldSfx;

	private AudioClip hunterJoinBattlefieldSfx;

	private AudioClip rogueJoinBattlefieldSfx;

	private AudioClip necromancerJoinBattlefieldSfx;

	private AudioClip priestJoinBattlefieldSfx;

	private AudioClip openBagSfx;

	private AudioClip closedBagSfx;

	private AudioClip deathCardSfx;

	private AudioClip attachmentSfx;

	private AudioClip assassinAbilitySfx;

	private AudioClip mageAbilitySfx;

	private AudioClip paladinAbilitySfx;

	private AudioClip hunterAbilitySfx;

	private AudioClip necromancerAbilitySfx;

	private AudioClip priestAbilitySfx;

	private AudioClip assassinAttackHitSfx;

	private AudioClip assassinAttackBlockedSfx;

	private AudioClip warriorAttackHitSfx;

	private AudioClip warriorAttackBlockedSfx;

	private AudioClip rogueAttackHitSfx;

	private AudioClip rogueAttackBlockedSfx;

	private AudioClip mageAttackHitSfx;

	private AudioClip mageAttackBlockedSfx;

	private AudioClip paladinAttackHitSfx;

	private AudioClip paladinAttackBlockedSfx;

	private AudioClip priestAttackHitSfx;

	private AudioClip priestAttackBlockedSfx;

	private AudioClip necromancerAttackHitSfx;

	private AudioClip necromancerAttackBlockedSfx;

	private AudioClip barbarianAttackHitSfx;

	private AudioClip barbarianAttackBlockedSfx;

	private AudioClip barbarianFurySfx;

	private AudioClip hunterAttackHitSfx;

	private AudioClip hunterAttackBlockedSfx;

	private AudioClip lootRoomEnterSfx;

	private AudioClip monster1RoomEnterSfx;

	private AudioClip monster2RoomEnterSfx;

	private AudioClip monster3RoomEnterSfx;

	private AudioClip monster4RoomEnterSfx;

	private void InitializeAudio()
	{
		sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePlayerPrefsKey, 1f));
		sfxMuted = PlayerPrefs.GetInt(SfxMutedPlayerPrefsKey, 0) != 0;
		musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePlayerPrefsKey, 0.75f));
		musicMuted = PlayerPrefs.GetInt(MusicMutedPlayerPrefsKey, 0) != 0;
		GameObject audioObject = new GameObject("SFX Audio Source");
		audioObject.transform.SetParent(transform, false);
		sfxAudioSource = audioObject.AddComponent<AudioSource>();
		sfxAudioSource.playOnAwake = false;
		sfxAudioSource.loop = false;
		sfxAudioSource.spatialBlend = 0f;

		GameObject musicObject = new GameObject("Music Audio Source");
		musicObject.transform.SetParent(transform, false);
		musicAudioSource = musicObject.AddComponent<AudioSource>();
		musicAudioSource.playOnAwake = false;
		musicAudioSource.loop = true;
		musicAudioSource.spatialBlend = 0f;
		UpdateMusicSourceVolume();

		openCardInspectionSfx = LoadSfx("open_card_inspection");
		closeCardInspectionSfx = LoadSfx("close_card_inspection");
		genericButtonClickSfx = LoadSfx("generic_button_click");
		buyCardSfx = LoadSfx("buy_card");
		arrowChangeSfx = LoadSfx("arrow_change");
		transitionSfx = LoadSfx("transition");
		rollingDiceSfx = LoadSfx("rolling_dice");
		pawnEnteringBattlefieldSfx = LoadSfx("pawn_entering_battlefield");
		warriorJoinBattlefieldSfx = LoadSfx("warrior_join_battlefield");
		assassinJoinBattlefieldSfx = LoadSfx("assassin_join_battlefield");
		barbarianJoinBattlefieldSfx = LoadSfx("barbarian_join_battlefield");
		mageJoinBattlefieldSfx = LoadSfx("mage_join_battlefield");
		paladinJoinBattlefieldSfx = LoadSfx("paladin_join_battlefield");
		hunterJoinBattlefieldSfx = LoadSfx("hunter_join_battlefield");
		rogueJoinBattlefieldSfx = LoadSfx("rogue_join_battlefield");
		necromancerJoinBattlefieldSfx = LoadSfx("necromancer_hjoin_battlefield");
		priestJoinBattlefieldSfx = LoadSfx("priest_join_battlefield");
		openBagSfx = LoadSfx("open_bag");
		closedBagSfx = LoadSfx("closed_bag");
		deathCardSfx = LoadSfx("death_card");
		attachmentSfx = LoadSfx("attachment");
		assassinAbilitySfx = LoadSfx("assassin_ability");
		mageAbilitySfx = LoadSfx("mage_ability");
		paladinAbilitySfx = LoadSfx("paladin_ability");
		hunterAbilitySfx = LoadSfx("hunter_ability");
		necromancerAbilitySfx = LoadSfx("necromancer_ability");
		priestAbilitySfx = LoadSfx("priest_ability");
		assassinAttackHitSfx = LoadSfx("assassin_attack_hit");
		assassinAttackBlockedSfx = LoadSfx("assassin_attack_blocked");
		warriorAttackHitSfx = LoadSfx("warrior_attack_hit");
		warriorAttackBlockedSfx = LoadSfx("warrior_attack_blocked");
		rogueAttackHitSfx = LoadSfx("rogue_attack_hit");
		rogueAttackBlockedSfx = LoadSfx("rogue_attack_blocked");
		mageAttackHitSfx = LoadSfx("mage_attack_hit");
		mageAttackBlockedSfx = LoadSfx("mage_attack_blocked");
		paladinAttackHitSfx = LoadSfx("paladin_attack_hit");
		paladinAttackBlockedSfx = LoadSfx("paladin_attack_blocked");
		priestAttackHitSfx = LoadSfx("priest_attack_hit");
		priestAttackBlockedSfx = LoadSfx("priest_attack_blocked");
		necromancerAttackHitSfx = LoadSfx("necromancer_attack_hit");
		necromancerAttackBlockedSfx = LoadSfx("necromancer_attack_blocked");
		barbarianAttackHitSfx = LoadSfx("barbarian_attack_hit");
		barbarianAttackBlockedSfx = LoadSfx("barbarian_attack_blocked");
		barbarianFurySfx = LoadSfx("barbarian_fury");
		hunterAttackHitSfx = LoadSfx("hunter_attack_hit");
		hunterAttackBlockedSfx = LoadSfx("hunter_attack_blocked");
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
		if ((Object)(object)sfxAudioSource == (Object)null || (Object)(object)clip == (Object)null)
		{
			return;
		}
		float effectiveVolume = sfxMuted ? 0f : Mathf.Clamp01(volume * sfxVolume);
		if (effectiveVolume <= 0f)
		{
			return;
		}
		sfxAudioSource.PlayOneShot(clip, effectiveVolume);
	}

	private void IncreaseSfxVolume()
	{
		SetSfxVolume(sfxVolume + 0.1f);
	}

	private void DecreaseSfxVolume()
	{
		SetSfxVolume(sfxVolume - 0.1f);
	}

	private void SetSfxVolume(float volume)
	{
		sfxVolume = Mathf.Clamp01(volume);
		if (sfxVolume > 0f)
		{
			sfxMuted = false;
		}
		PlayerPrefs.SetFloat(SfxVolumePlayerPrefsKey, sfxVolume);
		PlayerPrefs.SetInt(SfxMutedPlayerPrefsKey, sfxMuted ? 1 : 0);
		PlayerPrefs.Save();
		RefreshSfxOptionsUi();
	}

	private void ToggleSfxMute()
	{
		sfxMuted = !sfxMuted;
		PlayerPrefs.SetInt(SfxMutedPlayerPrefsKey, sfxMuted ? 1 : 0);
		PlayerPrefs.Save();
		RefreshSfxOptionsUi();
	}

	private void RefreshSfxOptionsUi()
	{
		if ((Object)(object)sfxVolumeText != (Object)null)
		{
			sfxVolumeText.text = sfxMuted ? "MUTO" : Mathf.RoundToInt(sfxVolume * 100f) + "%";
		}
		if ((Object)(object)sfxMuteButtonText != (Object)null)
		{
			sfxMuteButtonText.text = sfxMuted ? "ATTIVA" : "MUTE";
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
		PlaySfx(genericButtonClickSfx);
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
		PlaySfx(rollingDiceSfx);
	}

	private void PlayPawnEnteringBattlefieldSfx()
	{
		PlaySfx(pawnEnteringBattlefieldSfx);
	}

	private void PlayPawnEnteringBattlefieldSfx(CardDefinition definition)
	{
		if ((Object)(object)definition == (Object)null || !definition.HasHeroClass)
		{
			PlayPawnEnteringBattlefieldSfx();
			return;
		}
		AudioClip classJoinSfx = definition.HeroClass switch
		{
			HeroClass.Warrior => warriorJoinBattlefieldSfx,
			HeroClass.Assassin => assassinJoinBattlefieldSfx,
			HeroClass.Barbarian => barbarianJoinBattlefieldSfx,
			HeroClass.Mage => mageJoinBattlefieldSfx,
			HeroClass.Paladin => paladinJoinBattlefieldSfx,
			HeroClass.Hunter => hunterJoinBattlefieldSfx,
			HeroClass.Rogue => rogueJoinBattlefieldSfx,
			HeroClass.Necromancer => necromancerJoinBattlefieldSfx,
			HeroClass.Priest => priestJoinBattlefieldSfx,
			_ => pawnEnteringBattlefieldSfx
		};
		PlaySfx(classJoinSfx);
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
		PlaySfx(deathCardSfx);
	}

	private void PlayAttachmentSfx()
	{
		PlaySfx(attachmentSfx);
	}

	private void PlayBarbarianFurySfx()
	{
		PlaySfx(barbarianFurySfx);
	}

	private void PlayHunterAbilitySfx()
	{
		PlaySfx(hunterAbilitySfx);
	}

	private void PlayClassAbilitySfx(HeroClass heroClass)
	{
		AudioClip abilitySfx = heroClass switch
		{
			HeroClass.Assassin => assassinAbilitySfx,
			HeroClass.Mage => mageAbilitySfx,
			HeroClass.Paladin => paladinAbilitySfx,
			HeroClass.Hunter => hunterAbilitySfx,
			HeroClass.Necromancer => necromancerAbilitySfx,
			HeroClass.Priest => priestAbilitySfx,
			_ => null
		};
		PlaySfx(abilitySfx);
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
		switch (attacker.Card.HeroClass)
		{
			case HeroClass.Assassin:
				PlaySfx(hit ? assassinAttackHitSfx : assassinAttackBlockedSfx);
				break;
			case HeroClass.Warrior:
				PlaySfx(hit ? warriorAttackHitSfx : warriorAttackBlockedSfx);
				break;
			case HeroClass.Rogue:
				PlaySfx(hit ? rogueAttackHitSfx : rogueAttackBlockedSfx);
				break;
			case HeroClass.Mage:
				PlaySfx(hit ? mageAttackHitSfx : mageAttackBlockedSfx);
				break;
			case HeroClass.Paladin:
				PlaySfx(hit ? paladinAttackHitSfx : paladinAttackBlockedSfx);
				break;
			case HeroClass.Priest:
				PlaySfx(hit ? priestAttackHitSfx : priestAttackBlockedSfx);
				break;
			case HeroClass.Necromancer:
				PlaySfx(hit ? necromancerAttackHitSfx : necromancerAttackBlockedSfx);
				break;
			case HeroClass.Barbarian:
				PlaySfx(hit ? barbarianAttackHitSfx : barbarianAttackBlockedSfx);
				break;
			case HeroClass.Hunter:
				PlaySfx(hit ? hunterAttackHitSfx : hunterAttackBlockedSfx);
				break;
		}
	}
}
}
