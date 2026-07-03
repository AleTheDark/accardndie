using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;

namespace AccardND.Battlefield
{
    public sealed class BattleSfxPlayer
    {
        public const string VolumePlayerPrefsKey = "AccardND.SfxVolume";
        public const string MutedPlayerPrefsKey = "AccardND.SfxMuted";

        private AudioSource source;
        private float volume = 1f;
        private bool muted;
        private AudioClip genericButtonClickSfx;
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

        public float Volume => volume;
        public bool Muted => muted;

        public void Initialize(Transform parent, string sourceName = "Battle SFX Audio Source")
        {
            if (source != null)
                return;

            RefreshSettings();
            var audioObject = new GameObject(sourceName);
            audioObject.transform.SetParent(parent, false);
            source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            LoadClips();
        }

        public void RefreshSettings()
        {
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePlayerPrefsKey, 1f));
            muted = PlayerPrefs.GetInt(MutedPlayerPrefsKey, 0) != 0;
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (volume > 0f)
                muted = false;
            SaveSettings();
        }

        public void ToggleMute()
        {
            muted = !muted;
            SaveSettings();
        }

        public void PlayClip(AudioClip clip, float clipVolume = 1f)
        {
            if (source == null || clip == null)
                return;

            float effectiveVolume = muted ? 0f : Mathf.Clamp01(clipVolume * volume);
            if (effectiveVolume <= 0f)
                return;

            source.PlayOneShot(clip, effectiveVolume);
        }

        public void PlayButtonClick() => PlayClip(genericButtonClickSfx);

        public void PlayRollingDice() => PlayClip(rollingDiceSfx);

        public void PlayJoinBattlefield() => PlayClip(pawnEnteringBattlefieldSfx);

        public void PlayJoinBattlefield(CardDefinition definition)
        {
            if (definition == null || !definition.HasHeroClass)
            {
                PlayJoinBattlefield();
                return;
            }
            PlayJoinBattlefield(definition.HeroClass);
        }

        public void PlayJoinBattlefield(HeroClass heroClass)
        {
            AudioClip classJoinSfx = heroClass switch
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
            PlayClip(classJoinSfx);
        }

        public void PlayDeath() => PlayClip(deathCardSfx);

        public void PlayAttachment() => PlayClip(attachmentSfx);

        public void PlayBarbarianFury() => PlayClip(barbarianFurySfx);

        public void PlayHunterAbility() => PlayClip(hunterAbilitySfx);

        public void PlayClassAbility(HeroClass heroClass)
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
            PlayClip(abilitySfx);
        }

        public void PlayAttackResult(HeroClass heroClass, bool hit)
        {
            AudioClip attackSfx = heroClass switch
            {
                HeroClass.Assassin => hit ? assassinAttackHitSfx : assassinAttackBlockedSfx,
                HeroClass.Warrior => hit ? warriorAttackHitSfx : warriorAttackBlockedSfx,
                HeroClass.Rogue => hit ? rogueAttackHitSfx : rogueAttackBlockedSfx,
                HeroClass.Mage => hit ? mageAttackHitSfx : mageAttackBlockedSfx,
                HeroClass.Paladin => hit ? paladinAttackHitSfx : paladinAttackBlockedSfx,
                HeroClass.Priest => hit ? priestAttackHitSfx : priestAttackBlockedSfx,
                HeroClass.Necromancer => hit ? necromancerAttackHitSfx : necromancerAttackBlockedSfx,
                HeroClass.Barbarian => hit ? barbarianAttackHitSfx : barbarianAttackBlockedSfx,
                HeroClass.Hunter => hit ? hunterAttackHitSfx : hunterAttackBlockedSfx,
                _ => null
            };
            PlayClip(attackSfx);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(VolumePlayerPrefsKey, volume);
            PlayerPrefs.SetInt(MutedPlayerPrefsKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void LoadClips()
        {
            genericButtonClickSfx = LoadSfx("generic_button_click");
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
        }

        private static AudioClip LoadSfx(string clipName) =>
            Resources.Load<AudioClip>("SFX/" + clipName);
    }
}
