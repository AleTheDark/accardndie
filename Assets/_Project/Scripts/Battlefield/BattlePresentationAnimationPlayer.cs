using System;
using System.Collections;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    public sealed class BattlePresentationAnimationPlayer : MonoBehaviour
    {
        private readonly Queue<IEnumerator> queue = new();
        private Coroutine routine;
        private static Sprite hunterArrowSprite;
        private static Sprite hunterMarkReticleSprite;
        private static Sprite hunterExplosionCoreSprite;
        private static Sprite hunterExplosionRingSprite;
        private static Sprite hunterExplosionEmberSprite;
        private static Sprite hunterExplosionSmokeSprite;
        private static Sprite assassinSmokeSprite;
        private static Sprite assassinDaggerLeftSprite;
        private static Sprite assassinDaggerRightSprite;
        private static Sprite barbarianDoubleAxeSprite;
        private static Sprite bragusCleaverSprite;
        private static Sprite warriorSwordSprite;
        private static Sprite warriorSlashSprite;
        private static Sprite warriorDashTrailSprite;
        private static Sprite warriorGroundCrackSprite;
        private static Sprite mageProjectileSprite;
        private static Sprite mageParticleSprite;
        private static Sprite mageTrailSprite;
        private static Sprite priestBeamSprite;
        private static Sprite priestSparkSprite;
        private static Sprite priestCrossSprite;
        private static Sprite paladinShieldSprite;
        private static Sprite paladinCrestSprite;
        private static Sprite paladinShardSprite;
        private static Sprite paladinConstellationShieldSprite;
        private static Sprite rogueDaggerSprite;
        private static Sprite rogueHitMarkerSprite;
        private static Sprite rogueDeflectSprite;
        private static Sprite necromancerSkullSprite;
        private static Sprite necromancerTrailSprite;
        private static Sprite necromancerBurstSprite;
        private static Sprite medusaStoneConeSprite;
        private static Sprite medusaStoneShardSprite;
        private static Sprite medusaStoneCrackSprite;
        private static Sprite medusaGhostSnakeSprite;
        private static Sprite trentorVineSprite;
        private static Sprite trentorLeafSprite;
        private static Sprite targetLineSprite;
        private static AudioClip rogueAttackHitSfx;
        private AudioSource rogueSfxSource;
        private static bool TargetLinesEnabled => false;

        /// <summary>True finché la coda di animazioni (duelli) è in esecuzione.</summary>
        public bool IsBusy => routine != null;

        public void PlayDuel(
            RectTransform root,
            GameConfiguration configuration,
            DiceSpriteCatalog diceCatalog,
            PrototypeCardView attacker,
            PrototypeCardView defender,
            VigorRollResult attackerRoll,
            VigorRollResult defenderRoll,
            int attackerDieSides,
            int defenderDieSides,
            string attackerCaption,
            string defenderCaption,
            bool defenderEliminated,
            System.Action onDiceResolved = null,
            System.Action onDuelStarted = null,
            System.Action onDuelFinished = null,
            bool defenderHit = false,
            int attackerTotal = 0,
            int defenderTotal = 0,
            HeroClass? attackerHeroClass = null)
        {
            Vector3 attackerPoint = DuelWorldPoint(root, 0.34f);
            Vector3 defenderPoint = DuelWorldPoint(root, 0.66f);
            PlayDuelAtPoints(
                configuration,
                diceCatalog,
                attacker,
                defender,
                attackerRoll,
                defenderRoll,
                attackerDieSides,
                defenderDieSides,
                attackerCaption,
                defenderCaption,
                defenderEliminated,
                attackerPoint,
                defenderPoint,
                onDiceResolved,
                onDuelStarted,
                onDuelFinished,
                defenderHit,
                attackerTotal,
                defenderTotal,
                attackerHeroClass);
        }

        public void PlayDuelAtPoints(
            GameConfiguration configuration,
            DiceSpriteCatalog diceCatalog,
            PrototypeCardView attacker,
            PrototypeCardView defender,
            VigorRollResult attackerRoll,
            VigorRollResult defenderRoll,
            int attackerDieSides,
            int defenderDieSides,
            string attackerCaption,
            string defenderCaption,
            bool defenderEliminated,
            Vector3 attackerPoint,
            Vector3 defenderPoint,
            System.Action onDiceResolved = null,
            System.Action onDuelStarted = null,
            System.Action onDuelFinished = null,
            bool defenderHit = false,
            int attackerTotal = 0,
            int defenderTotal = 0,
            HeroClass? attackerHeroClass = null)
        {
            queue.Enqueue(PlayDuelRoutine(
                configuration,
                diceCatalog,
                attacker,
                defender,
                attackerRoll,
                defenderRoll,
                attackerDieSides,
                defenderDieSides,
                attackerCaption,
                defenderCaption,
                defenderEliminated,
                attackerPoint,
                defenderPoint,
                onDiceResolved,
                onDuelStarted,
                onDuelFinished,
                defenderHit,
                attackerTotal,
                defenderTotal,
                attackerHeroClass));
            if (routine == null)
                routine = StartCoroutine(RunQueue());
        }

        public static VigorRollResult BuildRoll(int dieSides, int first, int second, bool hasSecond, int selected)
        {
            return BuildRoll(
                dieSides,
                first,
                second,
                hasSecond,
                selected,
                hasSecond ? VigorSelectionMode.Sum : VigorSelectionMode.Single);
        }

        public static VigorRollResult BuildRoll(
            int dieSides,
            int first,
            int second,
            bool hasSecond,
            int selected,
            VigorSelectionMode selectionMode,
            int firstBeforeReroll = 0,
            int secondBeforeReroll = 0)
        {
            VigorSelectionMode normalizedSelectionMode = NormalizeSelectionMode(hasSecond, selectionMode);
            return new VigorRollResult(
                dieSides,
                first,
                second,
                hasSecond,
                selected,
                SelectionModeToMatchup(normalizedSelectionMode),
                normalizedSelectionMode,
                firstBeforeReroll,
                secondBeforeReroll);
        }

        private static VigorSelectionMode NormalizeSelectionMode(bool hasSecond, VigorSelectionMode selectionMode)
        {
            if (!hasSecond)
                return VigorSelectionMode.Single;

            return selectionMode == VigorSelectionMode.Single
                ? VigorSelectionMode.Sum
                : selectionMode;
        }

        private static MatchupResult SelectionModeToMatchup(VigorSelectionMode selectionMode)
        {
            return selectionMode switch
            {
                VigorSelectionMode.Highest => MatchupResult.Advantage,
                VigorSelectionMode.Lowest => MatchupResult.Disadvantage,
                _ => MatchupResult.Neutral
            };
        }

        public IEnumerator MoveToDuelPoints(
            PrototypeCardView attacker,
            PrototypeCardView defender,
            Vector3 attackerPoint,
            Vector3 defenderPoint,
            float duration = 0.34f,
            float scale = 1.16f,
            float wait = 0.37f)
        {
            if (attacker == null || defender == null)
                yield break;

            StartCoroutine(attacker.MoveToDuelPoint(attackerPoint, duration, scale));
            StartCoroutine(defender.MoveToDuelPoint(defenderPoint, duration, scale));
            yield return new WaitForSecondsRealtime(wait);
        }

        public IEnumerator ReturnDuelParticipants(
            PrototypeCardView attacker,
            PrototypeCardView defender,
            bool returnAttacker,
            bool returnDefender,
            float duration = 0.26f,
            float wait = 0.28f)
        {
            if (returnAttacker && attacker != null)
                StartCoroutine(attacker.ReturnFromDuelPoint(duration));
            if (returnDefender && defender != null)
                StartCoroutine(defender.ReturnFromDuelPoint(duration));
            if (returnAttacker || returnDefender)
                yield return new WaitForSecondsRealtime(wait);
        }

        private IEnumerator RunQueue()
        {
            while (queue.Count > 0)
                yield return StartCoroutine(queue.Dequeue());
            routine = null;
        }

        private IEnumerator PlayDuelRoutine(
            GameConfiguration configuration,
            DiceSpriteCatalog diceCatalog,
            PrototypeCardView attacker,
            PrototypeCardView defender,
            VigorRollResult attackerRoll,
            VigorRollResult defenderRoll,
            int attackerDieSides,
            int defenderDieSides,
            string attackerCaption,
            string defenderCaption,
            bool defenderEliminated,
            Vector3 attackerPoint,
            Vector3 defenderPoint,
            System.Action onDiceResolved,
            System.Action onDuelStarted,
            System.Action onDuelFinished,
            bool defenderHit,
            int attackerTotal,
            int defenderTotal,
            HeroClass? attackerHeroClass)
        {
            if (attacker == null || defender == null)
                yield break;

            Canvas.ForceUpdateCanvases();
            onDuelStarted?.Invoke();
            bool isHunterAttack = attacker.HeroClass == HeroClass.Hunter;
            bool isMageAttack = attacker.HeroClass == HeroClass.Mage;
            bool isAssassinAttack = attacker.HeroClass == HeroClass.Assassin;
            bool isBarbarianAttack = attacker.HeroClass == HeroClass.Barbarian;
            bool isWarriorAttack = attacker.HeroClass == HeroClass.Warrior;
            bool isPaladinAttack = attacker.HeroClass == HeroClass.Paladin;
            bool isPriestAttack = attacker.HeroClass == HeroClass.Priest;
            bool isRogueAttack = attacker.HeroClass == HeroClass.Rogue;
            bool isNecromancerAttack = attacker.HeroClass == HeroClass.Necromancer;
            if (!isHunterAttack && !isMageAttack && !isAssassinAttack && !isBarbarianAttack && !isWarriorAttack && !isPaladinAttack && !isPriestAttack && !isRogueAttack && !isNecromancerAttack)
            {
                yield return MoveToDuelPoints(
                    attacker,
                    defender,
                    attackerPoint,
                    defenderPoint,
                    wait: 0.34f);
            }

            bool hasAttackerRoll = attackerDieSides > 0 && attackerRoll.FirstRoll > 0;
            bool hasDefenderRoll = defenderDieSides > 0 && defenderRoll.FirstRoll > 0;
            float attackerDuration = hasAttackerRoll
                ? PrototypeCardView.VigorRollPresentationDuration(
                    attackerRoll,
                    configuration.Animation.DiceRollDuration,
                    configuration.Animation.DiceResultHold)
                : 0f;
            float defenderDuration = hasDefenderRoll
                ? PrototypeCardView.VigorRollPresentationDuration(
                    defenderRoll,
                    configuration.Animation.DiceRollDuration,
                    configuration.Animation.DiceResultHold)
                : 0f;
            float synchronizedDiceDuration = Mathf.Max(attackerDuration, defenderDuration);
            float attackerResultHold = configuration.Animation.DiceResultHold
                + Mathf.Max(0f, synchronizedDiceDuration - attackerDuration);
            float defenderResultHold = configuration.Animation.DiceResultHold
                + Mathf.Max(0f, synchronizedDiceDuration - defenderDuration);
            if (hasAttackerRoll)
            {
                attacker.PlayVigorRoll(
                    diceCatalog,
                    attackerDieSides,
                    attackerRoll,
                    attackerCaption,
                    configuration.Animation.DiceRollDuration,
                    attackerResultHold);
            }
            if (hasDefenderRoll)
            {
                defender.PlayVigorRoll(
                    diceCatalog,
                    defenderDieSides,
                    defenderRoll,
                    defenderCaption,
                    configuration.Animation.DiceRollDuration,
                    defenderResultHold);
            }
            if (hasAttackerRoll || hasDefenderRoll)
                yield return new WaitForSecondsRealtime(synchronizedDiceDuration);
            onDiceResolved?.Invoke();

            if (isHunterAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayHunterArrowAttack(attacker, defender));
                else
                    yield return StartCoroutine(PlayHunterArrowMiss(attacker));
            }
            else if (isMageAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayMageArcaneBoltAttack(attacker, defender));
                else
                    yield return StartCoroutine(PlayMageArcaneBoltBlocked(attacker, defender));
            }
            else if (isAssassinAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayAssassinShadowStrike(attacker, defender));
                else
                    yield return StartCoroutine(attacker.PlayAttackAnimation());
            }
            else if (isBarbarianAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayBarbarianAxeSmash(attacker, defender));
                else
                    yield return StartCoroutine(attacker.PlayAttackAnimation());
            }
            else if (isWarriorAttack)
            {
                bool abilityAttack = attackerRoll.SelectionMode == VigorSelectionMode.Sum;
                if (defenderHit)
                    yield return StartCoroutine(PlayWarriorSwordRush(attacker, defender, abilityAttack));
                else
                    yield return StartCoroutine(PlayWarriorSwordBlocked(attacker, defender));
            }
            else if (isPaladinAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayPaladinDivineShieldBash(attacker, defender));
                else
                    yield return StartCoroutine(PlayPaladinAegisBlocked(attacker, defender));
            }
            else if (isPriestAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayPriestSacredJudgement(attacker, defender));
                else
                    yield return StartCoroutine(PlayPriestJudgementBlocked(attacker, defender));
            }
            else if (isRogueAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayRogueDaggerFlurry(attacker, defender, attackerTotal - defenderTotal));
                else
                    yield return StartCoroutine(PlayRogueDaggerBlocked(attacker, defender));
            }
            else if (isNecromancerAttack)
            {
                if (defenderHit)
                    yield return StartCoroutine(PlayNecromancerSoulSwarm(attacker, defender));
                else
                    yield return StartCoroutine(PlayNecromancerSoulWardBlocked(attacker, defender));
            }
            else
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield return ReturnDuelParticipants(
                    attacker,
                    defender,
                    returnAttacker: true,
                    returnDefender: true,
                    wait: 0.26f);
            }
            onDuelFinished?.Invoke();
            if (defenderEliminated)
            {
                HeroClass resolvedAttackerHeroClass = attackerHeroClass ?? attacker.HeroClass;
                Debug.Log($"[DeathCrack] duel-defender-eliminated: attackerView={attacker.name}, defenderView={defender.name}, attackerHeroClassArg={attackerHeroClass?.ToString() ?? "NULL"}, attackerViewHeroClass={attacker.HeroClass}, resolved={resolvedAttackerHeroClass}");
                yield return StartCoroutine(defender.PlayDefeatAnimation(killerHeroClass: resolvedAttackerHeroClass));
            }
        }

        public IEnumerator PlayAssassinShadowStrike(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            RectTransform attackerRect = attacker.RectTransform;
            Vector3 homePosition = attackerRect.position;
            Vector3 homeScale = attackerRect.localScale;
            Quaternion homeRotation = attackerRect.localRotation;
            int homeSibling = attackerRect.GetSiblingIndex();
            bool restoredLayout = false;

            attacker.SetLayoutIgnored(true);
            attackerRect.SetAsLastSibling();

            yield return StartCoroutine(PlaySmokeWithScale(
                parent,
                attacker,
                homePosition,
                homeScale,
                homeScale * 0.42f,
                1.02f,
                fadeOut: true));

            Vector3 behindPosition = BehindTargetPoint(defender.RectTransform, homePosition);
            attackerRect.position = behindPosition;
            attackerRect.localScale = homeScale * 0.46f;
            attackerRect.localRotation = Quaternion.identity;
            yield return StartCoroutine(PlaySmokeWithScale(
                parent,
                attacker,
                behindPosition,
                homeScale * 0.46f,
                homeScale * 1.12f,
                0.88f,
                fadeOut: false));
            attackerRect.localScale = homeScale * 1.08f;

            yield return StartCoroutine(PlayAssassinDaggers(parent, defender.RectTransform, behindPosition));
            yield return StartCoroutine(PlayImpactPulse(defender.RectTransform));

            yield return StartCoroutine(PlaySmokeWithScale(
                parent,
                attacker,
                behindPosition,
                attackerRect.localScale,
                homeScale * 0.38f,
                0.78f,
                fadeOut: true));
            attackerRect.position = homePosition;
            attackerRect.localScale = homeScale * 0.44f;
            attackerRect.localRotation = homeRotation;
            yield return StartCoroutine(PlaySmokeWithScale(
                parent,
                attacker,
                homePosition,
                homeScale * 0.44f,
                homeScale * 1.08f,
                0.82f,
                fadeOut: false));
            attackerRect.localScale = homeScale;
            attackerRect.SetSiblingIndex(homeSibling);
            attacker.SetLayoutIgnored(false);
            restoredLayout = true;

            if (!restoredLayout)
            {
                attacker.SetAlpha(1f);
                attackerRect.position = homePosition;
                attackerRect.localScale = homeScale;
                attackerRect.localRotation = homeRotation;
                attacker.SetLayoutIgnored(false);
            }
        }

        public IEnumerator PlayBarbarianAxeSmash(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            RectTransform defenderRect = defender.RectTransform;
            RectTransform attackerRect = attacker.RectTransform;
            Vector3 attackerStart = attackerRect.position;
            Vector3 attackerScale = attackerRect.localScale;
            Quaternion attackerRotation = attackerRect.localRotation;
            Vector3 defenderStart = defenderRect.position;
            Vector3 defenderScale = defenderRect.localScale;
            Quaternion defenderRotation = defenderRect.localRotation;
            Vector3 attackDirection = defenderStart - attackerStart;
            float side = Mathf.Abs(attackDirection.x) < 0.001f ? 1f : Mathf.Sign(attackDirection.x);
            Vector3 leapTarget = defenderStart - new Vector3(side * 86f, 0f, 0f);
            Vector3 leapApex = Vector3.Lerp(attackerStart, leapTarget, 0.55f) + new Vector3(0f, 110f, 0f);

            GameObject axe = CreateOverlaySprite(
                parent,
                "Barbarian Double Axe",
                LoadBarbarianDoubleAxeSprite(),
                new Vector2(220f, 220f),
                out RectTransform axeRect,
                out Image axeImage);
            axeImage.color = new Color(1f, 1f, 1f, 0f);

            float leapDuration = 0.34f;
            float elapsed = 0f;
            while (elapsed < leapDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / leapDuration);
                Vector3 a = Vector3.LerpUnclamped(attackerStart, leapApex, progress);
                Vector3 b = Vector3.LerpUnclamped(leapApex, leapTarget, progress);
                attackerRect.position = Vector3.LerpUnclamped(a, b, progress);
                attackerRect.localScale = attackerScale * Mathf.Lerp(1f, 1.16f, Mathf.Sin(progress * Mathf.PI));
                attackerRect.localRotation = attackerRotation * Quaternion.Euler(0f, 0f, -side * Mathf.Lerp(0f, 10f, Mathf.Sin(progress * Mathf.PI)));
                axeRect.position = attackerRect.position + new Vector3(side * 34f, 62f, 0f);
                axeRect.localRotation = Quaternion.Euler(0f, 0f, -side * Mathf.Lerp(58f, 18f, progress));
                axeRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.05f, progress);
                axeImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(progress * 3.2f));
                yield return null;
            }

            Vector3 smashStart = attackerRect.position;
            Vector3 smashEnd = defenderStart - new Vector3(side * 58f, -8f, 0f);
            float smashDuration = 0.28f;
            elapsed = 0f;
            while (elapsed < smashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / smashDuration);
                float fall = progress * progress;
                attackerRect.position = Vector3.LerpUnclamped(smashStart, smashEnd, fall);
                attackerRect.localScale = attackerScale * Mathf.Lerp(1.12f, 1.02f, fall);
                attackerRect.localRotation = attackerRotation * Quaternion.Euler(0f, 0f, -side * Mathf.Lerp(10f, 4f, fall));
                axeRect.position = attackerRect.position + new Vector3(side * Mathf.Lerp(38f, 70f, fall), Mathf.Lerp(64f, 6f, fall), 0f);
                axeRect.localRotation = Quaternion.Euler(0f, 0f, -side * Mathf.Lerp(78f, -34f, fall));
                axeRect.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.45f, Mathf.Sin(progress * Mathf.PI));
                axeImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 10f)));

                defenderRect.position = defenderStart + new Vector3(UnityEngine.Random.Range(-4f, 4f), Mathf.Sin(progress * Mathf.PI) * 18f, 0f);
                defenderRect.localRotation = defenderRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 5f) * 6f);

                if (progress > 0.48f)
                {
                    float crackProgress = Mathf.Clamp01((progress - 0.48f) / 0.22f);
                    float shake = (1f - crackProgress) * 10f;
                    defenderRect.position += new Vector3(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake) * 0.35f, 0f);
                }

                yield return null;
            }

            yield return StartCoroutine(PlayImpactPulse(defenderRect));
            float returnDuration = 0.22f;
            elapsed = 0f;
            Vector3 returnStart = attackerRect.position;
            Quaternion returnRotation = attackerRect.localRotation;
            while (elapsed < returnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
                attackerRect.position = Vector3.LerpUnclamped(returnStart, attackerStart, progress);
                attackerRect.localScale = Vector3.LerpUnclamped(attackerRect.localScale, attackerScale, progress);
                attackerRect.localRotation = Quaternion.SlerpUnclamped(returnRotation, attackerRotation, progress);
                yield return null;
            }

            defenderRect.position = defenderStart;
            defenderRect.localScale = defenderScale;
            defenderRect.localRotation = defenderRotation;
            attackerRect.position = attackerStart;
            attackerRect.localScale = attackerScale;
            attackerRect.localRotation = attackerRotation;
            Destroy(axe);
        }

        public IEnumerator PlayWarriorSwordRush(PrototypeCardView attacker, PrototypeCardView defender, bool abilityAttack)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            RectTransform attackerRect = attacker.RectTransform;
            RectTransform defenderRect = defender.RectTransform;
            Vector3 attackerStart = attackerRect.position;
            Vector3 attackerScale = attackerRect.localScale;
            Quaternion attackerRotation = attackerRect.localRotation;
            int attackerSibling = attackerRect.GetSiblingIndex();
            Vector3 defenderStart = defenderRect.position;
            Vector3 attackDirection = defenderStart - attackerStart;
            Vector3 normalizedDirection = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : Vector3.right;

            attacker.SetLayoutIgnored(true);
            attackerRect.SetAsLastSibling();

            if (abilityAttack)
            {
                yield return StartCoroutine(PlayWarriorJudgementSword(parent, defenderRect));
                attackerRect.position = attackerStart;
                attackerRect.localScale = attackerScale;
                attackerRect.localRotation = attackerRotation;
                attackerRect.SetSiblingIndex(attackerSibling);
                attacker.SetLayoutIgnored(false);
                yield break;
            }

            float windupDuration = 0.16f;
            float elapsed = 0f;
            Vector3 bracePoint = attackerStart - normalizedDirection * 28f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / windupDuration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                attackerRect.position = Vector3.LerpUnclamped(attackerStart, bracePoint, Mathf.SmoothStep(0f, 1f, progress));
                attackerRect.localScale = attackerScale * Mathf.Lerp(1f, 1.08f, pulse);
                attackerRect.localRotation = attackerRotation * Quaternion.Euler(0f, 0f, -Mathf.Sign(normalizedDirection.x == 0f ? 1f : normalizedDirection.x) * pulse * 5f);
                yield return null;
            }

            yield return StartCoroutine(PlayWarriorCleaveStrike(parent, attackerRect, defenderRect, normalizedDirection, blocked: false));
            yield return StartCoroutine(PlayImpactPulse(defenderRect));

            float returnDuration = 0.18f;
            elapsed = 0f;
            Vector3 returnStart = attackerRect.position;
            Quaternion returnRotation = attackerRect.localRotation;
            while (elapsed < returnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
                attackerRect.position = Vector3.LerpUnclamped(returnStart, attackerStart, progress);
                attackerRect.localScale = Vector3.LerpUnclamped(attackerRect.localScale, attackerScale, progress);
                attackerRect.localRotation = Quaternion.SlerpUnclamped(returnRotation, attackerRotation, progress);
                yield return null;
            }

            attackerRect.position = attackerStart;
            attackerRect.localScale = attackerScale;
            attackerRect.localRotation = attackerRotation;
            attackerRect.SetSiblingIndex(attackerSibling);
            attacker.SetLayoutIgnored(false);
        }

        public IEnumerator PlayWarriorSwordBlocked(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            RectTransform attackerRect = attacker.RectTransform;
            RectTransform defenderRect = defender.RectTransform;
            Vector3 attackerStart = attackerRect.position;
            Vector3 attackerScale = attackerRect.localScale;
            Quaternion attackerRotation = attackerRect.localRotation;
            int attackerSibling = attackerRect.GetSiblingIndex();
            Vector3 attackDirection = defenderRect.position - attackerStart;
            Vector3 normalizedDirection = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : Vector3.right;

            attacker.SetLayoutIgnored(true);
            attackerRect.SetAsLastSibling();

            float windupDuration = 0.14f;
            float elapsed = 0f;
            Vector3 bracePoint = attackerStart - normalizedDirection * 22f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / windupDuration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                attackerRect.position = Vector3.LerpUnclamped(attackerStart, bracePoint, Mathf.SmoothStep(0f, 1f, progress));
                attackerRect.localScale = attackerScale * Mathf.Lerp(1f, 1.06f, pulse);
                yield return null;
            }

            yield return StartCoroutine(PlayWarriorCleaveStrike(parent, attackerRect, defenderRect, normalizedDirection, blocked: true));
            attackerRect.position = attackerStart;
            attackerRect.localScale = attackerScale;
            attackerRect.localRotation = attackerRotation;
            attackerRect.SetSiblingIndex(attackerSibling);
            attacker.SetLayoutIgnored(false);
        }

        private static IEnumerator PlayWarriorCleaveStrike(RectTransform parent, RectTransform attacker, RectTransform defender, Vector3 direction, bool blocked)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            Vector3 perpendicular = new Vector3(-normalizedDirection.y, normalizedDirection.x, 0f);
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            Vector3 target = Vector3.LerpUnclamped(EdgePoint(defender, attacker.position), defender.position, blocked ? 0.08f : 0.24f);
            Vector3 attackerStart = attacker.position;
            Vector3 defenderStart = defender.position;
            Vector3 chargeStart = attackerStart - normalizedDirection * 18f;
            Vector3 strikePoint = target - normalizedDirection * 94f;
            Vector3 handOffset = normalizedDirection * 34f + perpendicular * 58f;

            GameObject trail = CreateOverlaySprite(
                parent,
                blocked ? "Warrior Guard Charge Trail" : "Warrior Charge Trail",
                LoadWarriorDashTrailSprite(),
                new Vector2(460f, 126f),
                out RectTransform trailRect,
                out Image trailImage);
            GameObject slash = null;
            RectTransform slashRect = null;
            Image slashImage = null;
            if (blocked)
            {
                slash = CreateOverlaySprite(
                    parent,
                    "Warrior Blocked Finisher Slash",
                    LoadWarriorSlashSprite(),
                    new Vector2(320f, 190f),
                    out slashRect,
                    out slashImage);
            }
            GameObject sword = CreateOverlaySprite(
                parent,
                blocked ? "Warrior Held Sword Deflected" : "Warrior Held Sword",
                LoadWarriorSwordSprite(),
                new Vector2(300f, 300f),
                out RectTransform swordRect,
                out Image swordImage);

            GameObject crack = null;
            RectTransform crackRect = null;
            Image crackImage = null;
            if (!blocked)
            {
                crack = CreateOverlaySprite(
                    parent,
                    "Warrior Cleave Ground Rupture",
                    LoadWarriorGroundCrackSprite(),
                    new Vector2(430f, 430f),
                    out crackRect,
                    out crackImage);
                crackRect.position = target - perpendicular * 26f;
                crackRect.localRotation = Quaternion.Euler(0f, 0f, angle - 8f);
                crackRect.localScale = Vector3.one * 0.18f;
            }

            if (slashRect != null)
            {
                slashRect.position = target;
                slashRect.localRotation = Quaternion.Euler(0f, 0f, angle - 8f);
            }
            trailRect.localRotation = Quaternion.Euler(0f, 0f, angle + 180f);

            float elapsed = 0f;
            float chargeDuration = blocked ? 0.34f : 0.42f;
            while (elapsed < chargeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / chargeDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                float anticipation = Mathf.Sin(progress * Mathf.PI);
                attacker.position = Vector3.LerpUnclamped(chargeStart, strikePoint, eased);

                Vector3 handPosition = attacker.position + handOffset + perpendicular * Mathf.Lerp(10f, -8f, progress);
                swordRect.position = handPosition;
                swordRect.localRotation = Quaternion.Euler(0f, 0f, angle - 124f + Mathf.Lerp(0f, 34f, eased));
                swordRect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, anticipation);
                swordImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(progress * 8f));

                trailRect.position = attacker.position - normalizedDirection * 90f;
                trailRect.localScale = new Vector3(Mathf.Lerp(0.62f, 1.18f, anticipation), Mathf.Lerp(0.72f, 1f, anticipation), 1f);
                trailImage.color = new Color(0.9f, 0.76f, 0.48f, Mathf.Clamp01(Mathf.Min(progress * 5f, (1f - progress) * 3.2f)) * 0.55f);
                if (slashImage != null)
                    slashImage.color = new Color(1f, 0.92f, 0.72f, 0f);
                yield return null;
            }

            Vector3 swingStart = strikePoint + handOffset - perpendicular * 8f;
            Vector3 swingApex = target + perpendicular * 74f - normalizedDirection * 26f;
            Vector3 swingEnd = target + normalizedDirection * (blocked ? 12f : 48f) - perpendicular * 28f;
            elapsed = 0f;
            float swingDuration = blocked ? 0.2f : 0.24f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / swingDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                Vector3 swordPosition = Vector3.LerpUnclamped(
                    Vector3.LerpUnclamped(swingStart, swingApex, eased),
                    Vector3.LerpUnclamped(swingApex, swingEnd, eased),
                    eased);

                attacker.position = Vector3.LerpUnclamped(strikePoint, strikePoint + normalizedDirection * (blocked ? 18f : 38f), pulse);
                swordRect.position = swordPosition;
                swordRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + Mathf.Lerp(0f, blocked ? 92f : 142f, eased));
                swordRect.localScale = Vector3.one * Mathf.Lerp(0.96f, blocked ? 1.02f : 1.16f, pulse);
                swordImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - progress) * 2.6f + 0.2f));

                trailRect.position = swordPosition - normalizedDirection * Mathf.Lerp(82f, 132f, pulse);
                trailRect.localScale = new Vector3(Mathf.Lerp(0.85f, 1.62f, pulse), Mathf.Lerp(0.82f, 1.2f, pulse), 1f);
                trailImage.color = new Color(0.95f, 0.79f, 0.46f, Mathf.Clamp01(Mathf.Min(progress * 7f, (1f - progress) * 4f)) * 0.88f);

                if (slashRect != null)
                {
                    slashRect.localScale = new Vector3(Mathf.Lerp(0.58f, 0.92f, pulse), Mathf.Lerp(0.48f, 0.82f, pulse), 1f);
                    slashRect.position = target + normalizedDirection * Mathf.Lerp(-18f, 10f, progress);
                    slashImage.color = new Color(1f, 0.92f, 0.72f, Mathf.Clamp01(Mathf.Min(progress * 9f, (1f - progress) * 4.2f)));
                }

                if (crackRect != null)
                {
                    float crackProgress = Mathf.Clamp01((progress - 0.34f) / 0.52f);
                    crackRect.localScale = Vector3.one * Mathf.Lerp(0.18f, 1.05f, Mathf.SmoothStep(0f, 1f, crackProgress));
                    crackImage.color = new Color(1f, 0.86f, 0.58f, Mathf.Clamp01(crackProgress * 1.15f));
                }

                if (blocked && progress > 0.46f)
                    defender.position = defenderStart + new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-2f, 2f), 0f);
                yield return null;
            }

            if (blocked)
            {
                Vector3 bounceStart = swordRect.position;
                Vector3 bounceEnd = target - normalizedDirection * 150f + perpendicular * 96f;
                elapsed = 0f;
                float bounceDuration = 0.22f;
                while (elapsed < bounceDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / bounceDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, progress);
                    swordRect.position = Vector3.LerpUnclamped(bounceStart, bounceEnd, eased);
                    swordRect.localRotation = Quaternion.Euler(0f, 0f, angle - 16f + progress * 380f);
                    swordRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 0.52f, eased);
                    swordImage.color = new Color(1f, 1f, 1f, 1f - eased);
                    if (slashImage != null)
                        slashImage.color = new Color(1f, 0.82f, 0.46f, 1f - eased);
                    yield return null;
                }
                defender.position = defenderStart;
            }
            else if (crackImage != null)
            {
                elapsed = 0f;
                float fadeDuration = 0.18f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / fadeDuration);
                    crackImage.color = new Color(1f, 0.82f, 0.48f, 1f - progress);
                    yield return null;
                }
            }

            Destroy(sword);
            if (slash != null)
                Destroy(slash);
            Destroy(trail);
            if (crack != null)
                Destroy(crack);
        }

        private static IEnumerator PlayWarriorJudgementSword(RectTransform parent, RectTransform defender)
        {
            Vector3 target = defender.position;
            GameObject crack = CreateOverlaySprite(
                parent,
                "Warrior Judgement Crack",
                LoadWarriorGroundCrackSprite(),
                new Vector2(520f, 520f),
                out RectTransform crackRect,
                out Image crackImage);
            GameObject sword = CreateOverlaySprite(
                parent,
                "Warrior Falling Judgement Sword",
                LoadWarriorSwordSprite(),
                new Vector2(560f, 560f),
                out RectTransform swordRect,
                out Image swordImage);

            crackRect.position = target + new Vector3(0f, -18f, 0f);
            crackRect.localScale = Vector3.one * 0.24f;
            swordRect.position = target + new Vector3(0f, 360f, 0f);
            swordRect.localRotation = Quaternion.Euler(0f, 0f, 180f);

            float fallDuration = 0.34f;
            float elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fallDuration);
                float eased = progress * progress;
                swordRect.position = Vector3.LerpUnclamped(target + new Vector3(0f, 360f, 0f), target + new Vector3(0f, 12f, 0f), eased);
                swordRect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.35f, Mathf.Sin(progress * Mathf.PI));
                swordImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(progress * 5f));
                crackImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01((progress - 0.58f) * 3.2f));
                crackRect.localScale = Vector3.one * Mathf.Lerp(0.24f, 1.12f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.56f) / 0.34f)));
                yield return null;
            }

            float shockDuration = 0.3f;
            elapsed = 0f;
            while (elapsed < shockDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / shockDuration);
                float shake = (1f - progress) * 7f;
                defender.position = target + new Vector3(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake) * 0.35f, 0f);
                crackRect.localScale = Vector3.one * Mathf.Lerp(1.12f, 1.42f, progress);
                crackImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - progress * 0.3f));
                swordImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - progress * 0.75f));
                yield return null;
            }

            float fadeDuration = 0.22f;
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                crackImage.color = new Color(1f, 1f, 1f, 1f - progress);
                swordImage.color = new Color(1f, 1f, 1f, 0.25f * (1f - progress));
                yield return null;
            }

            defender.position = target;
            Destroy(crack);
            Destroy(sword);
        }

        public IEnumerator PlayHunterArrowAttack(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            GameObject arrowObject = CreateHunterArrowProjectile(parent, out RectTransform arrowRect, out Image arrowImage);
            GameObject trailObject = CreateHunterArrowTrail(parent, out RectTransform trailRect, out Image trailImage);
            GameObject emberObject = CreateHunterArrowTrail(parent, out RectTransform emberRect, out Image emberImage);

            Vector3 start = EdgePoint(attacker.RectTransform, defender.RectTransform.position);
            Vector3 end = EdgePoint(defender.RectTransform, attacker.RectTransform.position);
            Vector3 direction = end - start;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            float drawDuration = 0.2f;
            float flightDuration = 0.34f;
            float elapsed = 0f;
            Vector3 originalScale = attacker.RectTransform.localScale;
            Vector3 originalPosition = attacker.RectTransform.position;
            while (elapsed < drawDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / drawDuration));
                float drawBack = Mathf.Sin(progress * Mathf.PI) * 10f;
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.1f, progress);
                attacker.RectTransform.position = originalPosition - direction.normalized * drawBack;
                arrowRect.position = start - direction.normalized * Mathf.Lerp(26f, 4f, progress);
                arrowRect.localScale = new Vector3(Mathf.Lerp(0.72f, 1.08f, progress), Mathf.Lerp(0.82f, 1f, progress), 1f);
                arrowImage.color = new Color(1f, 0.9f, 0.55f, Mathf.Clamp01(progress * 1.15f));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                arrowRect.position = Vector3.LerpUnclamped(start, end, eased);
                arrowRect.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.32f, Mathf.Sin(progress * Mathf.PI));
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 10f));
                arrowImage.color = new Color(1f, 0.95f, 0.72f, alpha);
                UpdateHunterArrowTrail(trailRect, trailImage, arrowRect.position, direction.normalized, angle, progress, alpha, 1f);
                UpdateHunterArrowTrail(emberRect, emberImage, arrowRect.position, direction.normalized, angle, progress, alpha, 0.42f);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            attacker.RectTransform.position = originalPosition;
            yield return StartCoroutine(PlayHunterHitExplosion(parent, end, defender.RectTransform));
            Destroy(emberObject);
            Destroy(trailObject);
            Destroy(arrowObject);
        }

        public IEnumerator PlayTargetLine(
            PrototypeCardView source,
            PrototypeCardView target,
            Color color,
            float duration = 0.78f)
        {
            if (!TargetLinesEnabled)
                yield break;

            RectTransform parent = ResolveProjectileParent(source);
            if (parent == null || source == null || target == null)
                yield break;

            Vector3 start = source.RectTransform.position;
            Vector3 end = target.RectTransform.position;
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            if (distance <= 0.1f)
                yield break;

            int segmentCount = 18;
            RectTransform[] glowRects = new RectTransform[segmentCount];
            RectTransform[] coreRects = new RectTransform[segmentCount];
            Image[] glowImages = new Image[segmentCount];
            Image[] coreImages = new Image[segmentCount];
            GameObject[] glowObjects = new GameObject[segmentCount];
            GameObject[] coreObjects = new GameObject[segmentCount];
            for (int index = 0; index < segmentCount; index++)
            {
                glowObjects[index] = CreateTargetLineSegment(parent, "Target Arc Glow", out glowRects[index], out glowImages[index]);
                coreObjects[index] = CreateTargetLineSegment(parent, "Target Arc Core", out coreRects[index], out coreImages[index]);
                glowImages[index].color = new Color(color.r, color.g, color.b, 0f);
                coreImages[index].color = new Color(1f, 1f, 1f, 0f);
            }
            GameObject headObject = CreateTargetLineSegment(parent, "Target Line Head", out RectTransform headRect, out Image headImage);

            headImage.color = new Color(color.r, color.g, color.b, 0f);
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f).normalized;
            float side = Mathf.Sign(direction.x == 0f ? 1f : direction.x);
            float arcHeight = Mathf.Clamp(distance * 0.34f, 90f, 260f);
            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * 1.5f)
                arcHeight *= 0.55f;
            Vector3 arcOffset = Vector3.up * arcHeight + perpendicular * (side * Mathf.Clamp(distance * 0.04f, 0f, 34f));

            duration = Mathf.Max(0.08f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float headProgress = Mathf.Clamp01(eased);
                float visibleStart = Mathf.Clamp01(headProgress - 0.88f);
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 8f + 0.25f));
                float pulse = 1f + Mathf.Sin(progress * Mathf.PI * 6f) * 0.08f;

                Vector3 previous = TargetArcPoint(start, end, arcOffset, visibleStart);
                for (int index = 0; index < segmentCount; index++)
                {
                    float segmentT = Mathf.Lerp(visibleStart, headProgress, (index + 1f) / segmentCount);
                    Vector3 next = TargetArcPoint(start, end, arcOffset, segmentT);
                    Vector3 segment = next - previous;
                    float length = segment.magnitude;
                    float angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;
                    Vector3 center = (previous + next) * 0.5f;
                    float segmentAlpha = alpha * Mathf.SmoothStep(0f, 1f, (index + 1f) / segmentCount);

                    glowRects[index].position = center;
                    coreRects[index].position = center;
                    glowRects[index].localRotation = Quaternion.Euler(0f, 0f, angle);
                    coreRects[index].localRotation = glowRects[index].localRotation;
                    glowRects[index].sizeDelta = new Vector2(length + 2f, 8f * pulse);
                    coreRects[index].sizeDelta = new Vector2(length + 1f, 2.2f);
                    glowImages[index].color = new Color(color.r, color.g, color.b, 0.42f * segmentAlpha);
                    coreImages[index].color = new Color(1f, 0.96f, 0.88f, 0.78f * segmentAlpha);
                    previous = next;
                }

                Vector3 head = TargetArcPoint(start, end, arcOffset, headProgress);
                Vector3 headBack = TargetArcPoint(start, end, arcOffset, Mathf.Clamp01(headProgress - 0.02f));
                Vector3 headDirection = head - headBack;
                float headAngle = Mathf.Atan2(headDirection.y, headDirection.x) * Mathf.Rad2Deg;
                headRect.position = head;
                headRect.localRotation = Quaternion.Euler(0f, 0f, headAngle);
                headRect.sizeDelta = new Vector2(18f, 18f);
                headRect.localScale = new Vector3(1.6f, 0.55f, 1f) * Mathf.Lerp(0.8f, 1.08f, Mathf.Sin(progress * Mathf.PI));
                headImage.color = new Color(color.r, color.g, color.b, 0.88f * alpha);
                yield return null;
            }

            Destroy(headObject);
            for (int index = 0; index < segmentCount; index++)
            {
                Destroy(coreObjects[index]);
                Destroy(glowObjects[index]);
            }
        }

        private static Vector3 TargetArcPoint(Vector3 start, Vector3 end, Vector3 arcOffset, float t)
        {
            Vector3 line = Vector3.LerpUnclamped(start, end, t);
            return line + arcOffset * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }

        public IEnumerator PlayHunterMarkReticle(PrototypeCardView target)
        {
            RectTransform parent = ResolveProjectileParent(target);
            if (parent == null || target == null)
                yield break;

            RectTransform targetRect = target.RectTransform;
            Vector2 targetSize = targetRect.rect.size;
            float reticleSize = Mathf.Clamp(Mathf.Max(targetSize.x, targetSize.y) * 0.88f, 142f, 260f);
            Vector2 size = Vector2.one * reticleSize;

            GameObject outer = CreateOverlaySprite(
                parent,
                "Hunter Sniper Mark Reticle",
                LoadHunterMarkReticleSprite(),
                size,
                out RectTransform outerRect,
                out Image outerImage);
            GameObject inner = CreateOverlaySprite(
                parent,
                "Hunter Sniper Mark Focus",
                LoadHunterMarkReticleSprite(),
                size * 0.72f,
                out RectTransform innerRect,
                out Image innerImage);

            Vector3 originalTargetScale = targetRect.localScale;
            outerRect.position = targetRect.position;
            innerRect.position = targetRect.position;
            outerImage.color = new Color(1f, 0.42f, 0.02f, 0f);
            innerImage.color = new Color(1f, 0.76f, 0.28f, 0f);

            float duration = 0.92f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float lockOn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.38f));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.72f) / 0.28f));
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 7.5f, 1f - fadeOut));
                float pulse = Mathf.Sin(progress * Mathf.PI * 5.2f) * 0.055f;

                Vector3 center = targetRect.position;
                outerRect.position = center;
                innerRect.position = center;
                outerRect.localScale = Vector3.one * (Mathf.Lerp(1.85f, 1f, lockOn) + pulse);
                innerRect.localScale = Vector3.one * (Mathf.Lerp(0.38f, 1.16f, lockOn) - pulse * 0.6f);
                outerRect.localRotation = Quaternion.Euler(0f, 0f, -34f * progress);
                innerRect.localRotation = Quaternion.Euler(0f, 0f, 118f * progress);
                outerImage.color = new Color(1f, 0.42f, 0.02f, alpha * 0.96f);
                innerImage.color = new Color(1f, 0.78f, 0.3f, alpha * Mathf.Lerp(0.52f, 1f, lockOn));
                targetRect.localScale = originalTargetScale * Mathf.Lerp(1f, 1.045f, Mathf.Sin(Mathf.Clamp01(progress / 0.55f) * Mathf.PI));
                yield return null;
            }

            targetRect.localScale = originalTargetScale;
            Destroy(inner);
            Destroy(outer);
        }

        public IEnumerator PlayTrentorVineAttack(PrototypeCardView attacker, PrototypeCardView defender, bool hit, bool bind)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null || attacker == null || defender == null)
                yield break;

            RectTransform attackerRect = attacker.RectTransform;
            RectTransform defenderRect = defender.RectTransform;
            Vector3 start = attackerRect.position;
            Vector3 end = defenderRect.position;
            Vector3 direction = (end - start).normalized;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
            int vineCount = 14;
            GameObject[] vines = new GameObject[vineCount];
            RectTransform[] vineRects = new RectTransform[vineCount];
            Image[] vineImages = new Image[vineCount];
            GameObject[] glows = new GameObject[vineCount];
            RectTransform[] glowRects = new RectTransform[vineCount];
            Image[] glowImages = new Image[vineCount];

            for (int index = 0; index < vineCount; index++)
            {
                vines[index] = CreateOverlaySprite(parent, "Trentor Living Vine", LoadTrentorVineSprite(), new Vector2(160f, 20f), out vineRects[index], out vineImages[index]);
                glows[index] = CreateOverlaySprite(parent, "Trentor Vine Glow", LoadTrentorVineSprite(), new Vector2(180f, 34f), out glowRects[index], out glowImages[index]);
                vineImages[index].color = new Color(0.24f, 0.72f, 0.18f, 0f);
                glowImages[index].color = new Color(0.38f, 1f, 0.26f, 0f);
            }

            int leafCount = 34;
            GameObject[] leaves = new GameObject[leafCount];
            RectTransform[] leafRects = new RectTransform[leafCount];
            Image[] leafImages = new Image[leafCount];
            for (int index = 0; index < leafCount; index++)
            {
                leaves[index] = CreateOverlaySprite(parent, "Trentor Thorn Leaf", LoadTrentorLeafSprite(), Vector2.one * UnityEngine.Random.Range(18f, 34f), out leafRects[index], out leafImages[index]);
                leafImages[index].color = new Color(0.68f, 1f, 0.34f, 0f);
            }

            Vector3 originalDefenderScale = defenderRect.localScale;
            Quaternion originalDefenderRotation = defenderRect.localRotation;
            float travelDuration = 1.05f;
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / travelDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 5.5f, (1f - progress) * 3.2f + 0.42f));
                for (int index = 0; index < vineCount; index++)
                {
                    float lane = index - (vineCount - 1) * 0.5f;
                    bool mainVine = index % 3 == 0;
                    float offset = lane * 17f + Mathf.Sin(progress * Mathf.PI * 5f + index) * (mainVine ? 22f : 13f);
                    float vineProgress = Mathf.Clamp01(eased - index * 0.018f);
                    Vector3 head = Vector3.Lerp(start, end, vineProgress) + perpendicular * offset + Vector3.up * Mathf.Sin(vineProgress * Mathf.PI) * (mainVine ? 122f : 76f);
                    Vector3 tail = Vector3.Lerp(start, end, Mathf.Clamp01(vineProgress - (mainVine ? 0.18f : 0.1f))) + perpendicular * (offset * 0.74f);
                    Vector3 segment = head - tail;
                    float length = Mathf.Clamp(segment.magnitude, 40f, 260f);
                    float angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;
                    Vector3 center = (head + tail) * 0.5f;
                    float pulse = 1f + Mathf.Sin(progress * Mathf.PI * 8f + index) * 0.12f;

                    vineRects[index].position = center;
                    glowRects[index].position = center;
                    vineRects[index].localRotation = Quaternion.Euler(0f, 0f, angle);
                    glowRects[index].localRotation = vineRects[index].localRotation;
                    vineRects[index].sizeDelta = new Vector2(length, Mathf.Lerp(mainVine ? 17f : 9f, mainVine ? 30f : 16f, pulse));
                    glowRects[index].sizeDelta = new Vector2(length + (mainVine ? 34f : 18f), Mathf.Lerp(mainVine ? 36f : 18f, mainVine ? 62f : 32f, pulse));
                    vineImages[index].color = new Color(0.13f, mainVine ? 0.5f : 0.62f, 0.1f, (mainVine ? 0.96f : 0.76f) * alpha);
                    glowImages[index].color = new Color(0.32f, 1f, 0.28f, (mainVine ? 0.25f : 0.16f) * alpha);
                }

                for (int index = 0; index < leafCount; index++)
                {
                    float leafProgress = Mathf.Clamp01(eased - (index % 6) * 0.04f);
                    float lane = (index % 7) - 3f;
                    Vector3 pos = Vector3.Lerp(start, end, leafProgress)
                        + perpendicular * (lane * 28f + Mathf.Sin(progress * Mathf.PI * 4f + index) * 14f)
                        + Vector3.up * Mathf.Sin(leafProgress * Mathf.PI) * UnityEngine.Random.Range(48f, 130f);
                    leafRects[index].position = pos;
                    leafRects[index].localRotation = Quaternion.Euler(0f, 0f, progress * 540f + index * 31f);
                    leafImages[index].color = new Color(0.74f, 1f, 0.36f, 0.62f * alpha);
                }
                yield return null;
            }

            float bindDuration = bind ? 0.92f : 0.62f;
            elapsed = 0f;
            while (elapsed < bindDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / bindDuration);
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.58f) / 0.42f));
                float coil = Mathf.Sin(progress * Mathf.PI * 5f);
                for (int index = 0; index < vineCount; index++)
                {
                    bool mainVine = index % 3 == 0;
                    float angle = index * (360f / vineCount) + progress * (mainVine ? 135f : -92f);
                    float radius = Mathf.Lerp(mainVine ? 104f : 132f, bind ? (mainVine ? 48f : 68f) : (mainVine ? 72f : 92f), Mathf.SmoothStep(0f, 1f, progress));
                    Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * radius;
                    vineRects[index].position = end + offset * 0.34f;
                    glowRects[index].position = vineRects[index].position;
                    vineRects[index].localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
                    glowRects[index].localRotation = vineRects[index].localRotation;
                    vineRects[index].sizeDelta = new Vector2(mainVine ? 188f : 138f, bind ? (mainVine ? 28f : 16f) : (mainVine ? 20f : 11f));
                    glowRects[index].sizeDelta = new Vector2(mainVine ? 234f : 172f, bind ? (mainVine ? 58f : 34f) : (mainVine ? 38f : 22f));
                    vineImages[index].color = new Color(0.1f, mainVine ? 0.45f : 0.58f, 0.08f, (mainVine ? 0.96f : 0.74f) * alpha);
                    glowImages[index].color = new Color(hit ? 0.42f : 0.22f, 1f, 0.25f, (hit ? (mainVine ? 0.4f : 0.24f) : 0.18f) * alpha);
                }
                defenderRect.localScale = originalDefenderScale * (hit ? Mathf.Lerp(1.08f, 0.94f, Mathf.Sin(progress * Mathf.PI)) : 1f);
                defenderRect.localRotation = originalDefenderRotation * Quaternion.Euler(0f, 0f, hit ? coil * 1.8f : coil * 0.8f);
                yield return null;
            }

            defenderRect.localScale = originalDefenderScale;
            defenderRect.localRotation = originalDefenderRotation;
            for (int index = 0; index < leafCount; index++)
                Destroy(leaves[index]);
            for (int index = 0; index < vineCount; index++)
            {
                Destroy(vines[index]);
                Destroy(glows[index]);
            }
        }

        public IEnumerator PlayBragusCleaverCounterattack(PrototypeCardView attacker, PrototypeCardView defender, bool hit)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null || attacker == null || defender == null)
                yield break;

            RectTransform attackerRect = attacker.RectTransform;
            RectTransform defenderRect = defender.RectTransform;
            Vector3 start = attackerRect.position;
            Vector3 end = defenderRect.position;
            Vector3 direction = end - start;
            Vector3 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            Vector3 perpendicular = new Vector3(-normalized.y, normalized.x, 0f);
            float angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
            float side = Mathf.Abs(normalized.x) < 0.001f ? 1f : Mathf.Sign(normalized.x);
            Vector3 travelEnd = hit
                ? end
                : end + normalized * 330f + perpendicular * side * 120f + Vector3.up * 36f;

            GameObject cleaver = CreateOverlaySprite(parent, "Bragus Flying Cleaver", LoadBragusCleaverSprite(), new Vector2(260f, 260f), out RectTransform cleaverRect, out Image cleaverImage);
            GameObject aura = CreateOverlaySprite(parent, "Bragus Cleaver Aura", LoadHunterExplosionCoreSprite(), new Vector2(220f, 220f), out RectTransform auraRect, out Image auraImage);
            cleaverImage.color = new Color(1f, 1f, 1f, 0f);
            auraImage.color = new Color(1f, 0.18f, 0.04f, 0f);
            List<GameObject> trailSegments = new List<GameObject>(24);
            List<Image> trailImages = new List<Image>(24);
            List<float> trailBirthProgress = new List<float>(24);
            const int maxTrailSegments = 22;

            Vector3 originalDefenderPosition = defenderRect.position;
            Vector3 originalDefenderScale = defenderRect.localScale;
            Quaternion originalDefenderRotation = defenderRect.localRotation;
            float travelDuration = 0.72f;
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / travelDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float arc = Mathf.Sin(progress * Mathf.PI);
                Vector3 position = Vector3.Lerp(start, travelEnd, eased)
                    + perpendicular * Mathf.Sin(progress * Mathf.PI * 2.2f) * 92f
                    + Vector3.up * arc * 135f;
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 5.5f, (1f - progress) * 4.5f + 0.35f));

                float cleaverAngle = angle + side * (progress * 620f + 32f);
                float cleaverScale = Mathf.Lerp(0.7f, 1.34f, arc);

                cleaverRect.position = position;
                cleaverRect.localRotation = Quaternion.Euler(0f, 0f, cleaverAngle);
                cleaverRect.localScale = Vector3.one * cleaverScale;
                cleaverImage.color = new Color(1f, 1f, 1f, alpha);

                auraRect.position = position - normalized * 18f;
                auraRect.localRotation = Quaternion.Euler(0f, 0f, -progress * 520f);
                auraRect.localScale = Vector3.one * Mathf.Lerp(0.32f, 1.05f, arc);
                auraImage.color = new Color(1f, 0.12f, 0.02f, alpha * 0.42f);

                int desiredTrailSegments = Mathf.Clamp(Mathf.FloorToInt(progress * maxTrailSegments), 0, maxTrailSegments);
                while (trailSegments.Count < desiredTrailSegments)
                {
                    float segmentProgress = trailSegments.Count / (float)Mathf.Max(1, maxTrailSegments - 1);
                    float segmentArc = Mathf.Sin(segmentProgress * Mathf.PI);
                    Vector3 segmentPosition = Vector3.Lerp(start, travelEnd, Mathf.SmoothStep(0f, 1f, segmentProgress))
                        + perpendicular * Mathf.Sin(segmentProgress * Mathf.PI * 2.2f) * 92f
                        + Vector3.up * segmentArc * 135f;
                    float segmentAngle = angle + side * (segmentProgress * 620f + 32f);
                    float segmentScale = Mathf.Lerp(0.34f, cleaverScale * 1.18f, segmentProgress);
                    float spiralOffset = Mathf.Sin(segmentProgress * Mathf.PI * 5.5f) * Mathf.Lerp(12f, 42f, segmentProgress);
                    Vector3 segmentForward = Quaternion.Euler(0f, 0f, segmentAngle) * Vector3.right;
                    Vector3 segmentSide = Quaternion.Euler(0f, 0f, segmentAngle) * Vector3.up;

                    GameObject segment = CreateOverlaySprite(parent, "Bragus Cleaver Spiral Trail", LoadMageTrailSprite(), new Vector2(230f, 74f), out RectTransform segmentRect, out Image segmentImage);
                    segmentRect.position = segmentPosition - segmentForward * Mathf.Lerp(28f, 96f, segmentProgress) + segmentSide * spiralOffset;
                    segmentRect.localRotation = Quaternion.Euler(0f, 0f, segmentAngle + 180f);
                    segmentRect.localScale = new Vector3(segmentScale * 1.55f, segmentScale * 0.52f, 1f);
                    segmentImage.color = new Color(1f, 0.025f, 0.01f, Mathf.Lerp(0.18f, 0.72f, segmentProgress));
                    trailSegments.Add(segment);
                    trailImages.Add(segmentImage);
                    trailBirthProgress.Add(segmentProgress);
                }

                for (int i = 0; i < trailImages.Count; i++)
                {
                    float maturity = Mathf.InverseLerp(trailBirthProgress[i], 1f, progress);
                    float segmentAlpha = Mathf.Lerp(0.28f, 0.78f, trailBirthProgress[i]) * Mathf.Lerp(0.92f, 1.08f, Mathf.Sin(maturity * Mathf.PI));
                    trailImages[i].color = new Color(1f, 0.02f, 0.01f, segmentAlpha * alpha);
                }
                yield return null;
            }

            for (int i = 0; i < trailSegments.Count; i++)
                Destroy(trailSegments[i]);
            Destroy(aura);
            Destroy(cleaver);
            yield return StartCoroutine(PlayBragusCleaverImpact(
                parent,
                hit ? end : travelEnd,
                hit ? defenderRect : null,
                hit,
                originalDefenderPosition,
                originalDefenderScale,
                originalDefenderRotation,
                angle));
        }

        private static IEnumerator PlayBragusCleaverImpact(
            RectTransform parent,
            Vector3 worldPosition,
            RectTransform target,
            bool hit,
            Vector3 originalTargetPosition,
            Vector3 originalTargetScale,
            Quaternion originalTargetRotation,
            float angle)
        {
            GameObject slash = CreateOverlaySprite(parent, "Bragus Cleaver Impact Slash", LoadWarriorSlashSprite(), new Vector2(520f, 320f), out RectTransform slashRect, out Image slashImage);
            GameObject smoke = CreateOverlaySprite(parent, "Bragus Cleaver Impact Smoke", LoadHunterExplosionSmokeSprite(), new Vector2(420f, 420f), out RectTransform smokeRect, out Image smokeImage);
            GameObject core = CreateOverlaySprite(parent, "Bragus Cleaver Impact Core", LoadHunterExplosionCoreSprite(), new Vector2(320f, 320f), out RectTransform coreRect, out Image coreImage);
            GameObject ring = CreateOverlaySprite(parent, "Bragus Cleaver Shock Ring", LoadHunterExplosionRingSprite(), new Vector2(360f, 360f), out RectTransform ringRect, out Image ringImage);
            slashRect.position = worldPosition;
            smokeRect.position = worldPosition;
            coreRect.position = worldPosition;
            ringRect.position = worldPosition;
            slashRect.localRotation = Quaternion.Euler(0f, 0f, angle - 28f);
            slashImage.color = new Color(1f, 0.08f, 0.02f, 0f);
            smokeImage.color = new Color(0.22f, 0.08f, 0.04f, 0f);
            coreImage.color = new Color(1f, 0.24f, 0.04f, 0f);
            ringImage.color = new Color(1f, 0.7f, 0.22f, 0f);

            int shardCount = hit ? 22 : 12;
            var shards = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float rotation, float spin, float scale)>(shardCount);
            for (int index = 0; index < shardCount; index++)
            {
                GameObject shard = CreateOverlaySprite(parent, "Bragus Cleaver Ember Shard", LoadHunterExplosionEmberSprite(), new Vector2(UnityEngine.Random.Range(28f, 54f), UnityEngine.Random.Range(70f, 118f)), out RectTransform shardRect, out Image shardImage);
                float radians = (Mathf.PI * 2f * index / shardCount) + UnityEngine.Random.Range(-0.28f, 0.28f);
                float distance = UnityEngine.Random.Range(hit ? 96f : 54f, hit ? 230f : 132f);
                Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * distance;
                shardRect.position = worldPosition;
                shardImage.color = new Color(1f, 0.16f, 0.02f, 0f);
                shards.Add((shard, shardRect, shardImage, offset, radians * Mathf.Rad2Deg, UnityEngine.Random.Range(-420f, 420f), UnityEngine.Random.Range(0.5f, 1.25f)));
            }

            float duration = hit ? 0.64f : 0.42f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                float flash = Mathf.Sin(progress * Mathf.PI);

                slashRect.localScale = Vector3.one * Mathf.Lerp(0.42f, hit ? 1.38f : 0.92f, flash);
                slashRect.localRotation = Quaternion.Euler(0f, 0f, angle - 28f + progress * 32f);
                slashImage.color = new Color(1f, 0.04f, 0.01f, Mathf.Clamp01(Mathf.Min(progress * 12f, fade * 1.55f)));

                smokeRect.localScale = Vector3.one * Mathf.Lerp(0.25f, hit ? 1.65f : 1.05f, eased);
                smokeRect.localRotation = Quaternion.Euler(0f, 0f, -progress * 26f);
                smokeImage.color = new Color(0.24f, 0.08f, 0.04f, Mathf.Clamp01(Mathf.Min(progress * 5f, fade * 0.64f)));

                coreRect.localScale = Vector3.one * Mathf.Lerp(0.18f, hit ? 1.22f : 0.72f, flash);
                coreRect.localRotation = Quaternion.Euler(0f, 0f, progress * 80f);
                coreImage.color = new Color(1f, 0.2f, 0.02f, Mathf.Clamp01(Mathf.Min(progress * 10f, fade * 1.25f)));

                ringRect.localScale = Vector3.one * Mathf.Lerp(0.18f, hit ? 1.6f : 1.0f, eased);
                ringImage.color = new Color(1f, 0.68f, 0.18f, Mathf.Clamp01(Mathf.Min(progress * 12f, fade * 1.1f)));

                if (target != null)
                {
                    float shake = hit ? (1f - progress) * 18f : (1f - progress) * 7f;
                    target.position = originalTargetPosition + new Vector3(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake) * 0.45f, 0f);
                    target.localScale = originalTargetScale * (1f + flash * (hit ? 0.13f : 0.05f));
                    target.localRotation = originalTargetRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 6f) * (hit ? 5f : 2f));
                }

                foreach (var shard in shards)
                {
                    shard.rect.position = worldPosition + shard.offset * eased + Vector3.down * (progress * progress * 28f);
                    shard.rect.localRotation = Quaternion.Euler(0f, 0f, shard.rotation + shard.spin * progress);
                    shard.rect.localScale = Vector3.one * Mathf.Lerp(0.25f, shard.scale, flash);
                    shard.image.color = new Color(1f, 0.18f, 0.02f, Mathf.Clamp01(Mathf.Min(progress * 11f, fade * 1.18f)));
                }

                yield return null;
            }

            if (target != null)
            {
                target.position = originalTargetPosition;
                target.localScale = originalTargetScale;
                target.localRotation = originalTargetRotation;
            }

            Destroy(slash);
            Destroy(smoke);
            Destroy(core);
            Destroy(ring);
            foreach (var shard in shards)
                Destroy(shard.obj);
        }

        public IEnumerator PlayAssassinInhibitSmoke(PrototypeCardView target)
        {
            RectTransform parent = ResolveProjectileParent(target);
            if (parent == null || target == null)
                yield break;

            RectTransform targetRect = target.RectTransform;
            Vector2 targetSize = targetRect.rect.size;
            float smokeSize = Mathf.Clamp(Mathf.Max(targetSize.x, targetSize.y) * 3.1f, 560f, 1040f);

            GameObject smoke = CreateOverlaySprite(
                parent,
                "Assassin Inhibit Smoke",
                LoadAssassinSmokeSprite(),
                Vector2.one * smokeSize,
                out RectTransform smokeRect,
                out Image smokeImage);
            GameObject veil = CreateOverlaySprite(
                parent,
                "Assassin Inhibit Smoke Veil",
                LoadAssassinSmokeSprite(),
                Vector2.one * smokeSize * 0.78f,
                out RectTransform veilRect,
                out Image veilImage);

            Vector3 originalTargetScale = targetRect.localScale;
            Quaternion originalTargetRotation = targetRect.localRotation;
            float duration = 0.78f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.22f));
                float vanish = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.58f) / 0.42f));
                float alpha = Mathf.Clamp01(appear * (1f - vanish));
                float shiver = Mathf.Sin(progress * Mathf.PI * 8f);
                Vector3 center = targetRect.position;

                smokeRect.position = center + new Vector3(Mathf.Sin(progress * Mathf.PI * 2f) * 8f, 6f, 0f);
                veilRect.position = center + new Vector3(Mathf.Cos(progress * Mathf.PI * 2.4f) * 10f, -8f, 0f);
                smokeRect.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.22f, Mathf.SmoothStep(0f, 1f, progress));
                veilRect.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.36f, Mathf.SmoothStep(0f, 1f, progress));
                smokeRect.localRotation = Quaternion.Euler(0f, 0f, progress * 34f);
                veilRect.localRotation = Quaternion.Euler(0f, 0f, -progress * 47f);
                smokeImage.color = new Color(0.86f, 0.82f, 0.95f, alpha * 0.82f);
                veilImage.color = new Color(0.36f, 0.32f, 0.48f, alpha * 0.58f);

                float pulse = Mathf.Sin(Mathf.Clamp01(progress / 0.48f) * Mathf.PI) * 0.075f;
                targetRect.localScale = originalTargetScale * (1f + pulse);
                targetRect.localRotation = originalTargetRotation * Quaternion.Euler(0f, 0f, shiver * 2.2f * (1f - vanish));
                yield return null;
            }

            targetRect.localScale = originalTargetScale;
            targetRect.localRotation = originalTargetRotation;
            Destroy(veil);
            Destroy(smoke);
        }

        public IEnumerator PlayMedusaPetrifyingGaze(PrototypeCardView medusa, IReadOnlyList<PrototypeCardView> targets, int petrificationDifference)
        {
            if (medusa == null || targets == null || targets.Count == 0)
                yield break;

            RectTransform parent = ResolveProjectileParent(medusa);
            if (parent == null)
                yield break;

            List<PrototypeCardView> validTargets = new();
            Vector3 targetCenter = Vector3.zero;
            for (int i = 0; i < targets.Count; i++)
            {
                PrototypeCardView target = targets[i];
                if (target == null)
                    continue;
                validTargets.Add(target);
                targetCenter += target.RectTransform.position;
            }
            if (validTargets.Count == 0)
                yield break;
            targetCenter /= validTargets.Count;

            RectTransform medusaRect = medusa.RectTransform;
            Vector3 origin = EdgePoint(medusaRect, targetCenter);
            Vector3 direction = targetCenter - origin;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.down;
            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float distance = Vector3.Distance(origin, targetCenter);
            float coneLength = Mathf.Clamp(distance * 1.55f, 420f, 1180f);
            float coneWidth = Mathf.Clamp(coneLength * 0.72f, 360f, 860f);

            GameObject cone = CreateOverlaySprite(
                parent,
                "Medusa Petrifying Cone",
                LoadMedusaStoneConeSprite(),
                new Vector2(coneLength, coneWidth),
                out RectTransform coneRect,
                out Image coneImage);
            coneRect.pivot = new Vector2(0f, 0.5f);
            coneRect.position = origin;
            coneRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            coneImage.preserveAspect = false;

            GameObject flash = CreateOverlaySprite(
                parent,
                "Medusa Eye Flash",
                LoadMedusaStoneShardSprite(),
                Vector2.one * 170f,
                out RectTransform flashRect,
                out Image flashImage);
            flashRect.position = origin;

            List<(GameObject obj, RectTransform rect, Image image, Vector3 start, Vector3 end, float delay, float spin, float scale)> shards = new();
            for (int i = 0; i < validTargets.Count; i++)
            {
                RectTransform targetRect = validTargets[i].RectTransform;
                for (int shardIndex = 0; shardIndex < 5; shardIndex++)
                {
                    GameObject shard = CreateOverlaySprite(
                        parent,
                        "Medusa Stone Shard",
                        LoadMedusaStoneShardSprite(),
                        Vector2.one * UnityEngine.Random.Range(24f, 42f),
                        out RectTransform shardRect,
                        out Image shardImage);
                    Vector3 side = new Vector3(-direction.y, direction.x, 0f) * UnityEngine.Random.Range(-90f, 90f);
                    Vector3 start = origin + direction * UnityEngine.Random.Range(25f, 90f) + side * 0.35f;
                    Vector3 end = targetRect.position
                        + new Vector3(UnityEngine.Random.Range(-42f, 42f), UnityEngine.Random.Range(-54f, 54f), 0f);
                    shards.Add((shard, shardRect, shardImage, start, end, UnityEngine.Random.Range(0.08f, 0.28f), UnityEngine.Random.Range(-240f, 240f), UnityEngine.Random.Range(0.78f, 1.28f)));
                }
            }

            int snakeCount = Mathf.Max(1, petrificationDifference);
            List<(GameObject obj, RectTransform rect, Image image, Vector3 start, Vector3 end, Vector3 normal, float delay, float amplitude, float phase, float scale)> snakes = new();
            for (int i = 0; i < snakeCount; i++)
            {
                PrototypeCardView target = validTargets[i % validTargets.Count];
                RectTransform targetRect = target.RectTransform;
                GameObject snake = CreateOverlaySprite(
                    parent,
                    "Medusa Ghost Snake",
                    LoadMedusaGhostSnakeSprite(),
                    new Vector2(148f, 58f),
                    out RectTransform snakeRect,
                    out Image snakeImage);

                Vector3 end = targetRect.position
                    + new Vector3(UnityEngine.Random.Range(-48f, 48f), UnityEngine.Random.Range(-62f, 62f), 0f);
                Vector3 start = origin
                    + direction * UnityEngine.Random.Range(12f, 58f)
                    + new Vector3(-direction.y, direction.x, 0f) * UnityEngine.Random.Range(-36f, 36f);
                Vector3 path = end - start;
                if (path.sqrMagnitude < 0.01f)
                    path = direction;
                path.Normalize();
                Vector3 normal = new Vector3(-path.y, path.x, 0f);
                snakes.Add((
                    snake,
                    snakeRect,
                    snakeImage,
                    start,
                    end,
                    normal,
                    UnityEngine.Random.Range(0.03f, 0.34f),
                    UnityEngine.Random.Range(26f, 72f),
                    UnityEngine.Random.Range(0f, 1f),
                    UnityEngine.Random.Range(0.74f, 1.18f)));
            }

            Dictionary<PrototypeCardView, (Vector3 scale, Quaternion rotation)> originals = new();
            foreach (PrototypeCardView target in validTargets)
                originals[target] = (target.RectTransform.localScale, target.RectTransform.localRotation);

            float duration = 1.18f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.28f));
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.76f) / 0.24f));
                float pulse = Mathf.Sin(progress * Mathf.PI * 7.5f);

                coneRect.position = origin;
                coneRect.localScale = new Vector3(Mathf.Lerp(0.08f, 1.06f, open), Mathf.Lerp(0.2f, 1f + pulse * 0.035f, open), 1f);
                coneImage.color = new Color(0.66f, 0.72f, 0.76f, Mathf.Clamp01(open * (1f - fade)) * 0.48f);

                flashRect.position = origin + direction * 18f;
                flashRect.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.35f, Mathf.Sin(Mathf.Clamp01(progress / 0.45f) * Mathf.PI));
                flashRect.localRotation = Quaternion.Euler(0f, 0f, progress * 280f);
                flashImage.color = new Color(0.9f, 0.96f, 1f, Mathf.Clamp01(Mathf.Min(progress * 8f, 1f - fade)) * 0.82f);

                for (int i = 0; i < shards.Count; i++)
                {
                    var shard = shards[i];
                    float local = Mathf.Clamp01((progress - shard.delay) / 0.58f);
                    float eased = Mathf.SmoothStep(0f, 1f, local);
                    Vector3 arc = new Vector3(0f, Mathf.Sin(local * Mathf.PI) * UnityEngine.Random.Range(8f, 18f), 0f);
                    shard.rect.position = Vector3.LerpUnclamped(shard.start, shard.end, eased) + arc;
                    shard.rect.localRotation = Quaternion.Euler(0f, 0f, shard.spin * progress);
                    shard.rect.localScale = Vector3.one * shard.scale * Mathf.Lerp(0.55f, 1.18f, Mathf.Sin(local * Mathf.PI));
                    shard.image.color = new Color(0.62f, 0.66f, 0.68f, Mathf.Clamp01(Mathf.Min(local * 6f, (1f - local) * 3f)) * 0.92f);
                }

                for (int i = 0; i < snakes.Count; i++)
                {
                    var snake = snakes[i];
                    float local = Mathf.Clamp01((progress - snake.delay) / 0.74f);
                    float eased = Mathf.SmoothStep(0f, 1f, local);
                    float wave = Mathf.Sin((local * 3.35f + snake.phase) * Mathf.PI * 2f);
                    Vector3 offset = snake.normal * wave * snake.amplitude * Mathf.Sin(local * Mathf.PI);
                    Vector3 position = Vector3.LerpUnclamped(snake.start, snake.end, eased) + offset;
                    Vector3 tangent = (snake.end - snake.start).normalized;
                    float snakeAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                    float wiggle = Mathf.Sin((local * 5.8f + snake.phase) * Mathf.PI * 2f) * 16f;
                    float alpha = Mathf.Clamp01(Mathf.Min(local * 5.5f, (1f - local) * 3.6f));

                    snake.rect.position = position;
                    snake.rect.localRotation = Quaternion.Euler(0f, 0f, snakeAngle + wiggle);
                    snake.rect.localScale = Vector3.one * snake.scale * Mathf.Lerp(0.62f, 1.12f, Mathf.Sin(local * Mathf.PI));
                    snake.image.color = new Color(0.68f, 0.72f, 0.73f, alpha * 0.82f);
                }

                foreach (PrototypeCardView target in validTargets)
                {
                    RectTransform targetRect = target.RectTransform;
                    var original = originals[target];
                    float hit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.22f) / 0.42f));
                    float shake = hit * (1f - fade) * 5f;
                    targetRect.localScale = original.scale * (1f + Mathf.Sin(hit * Mathf.PI) * 0.065f);
                    targetRect.localRotation = original.rotation * Quaternion.Euler(0f, 0f, pulse * shake * 0.42f);
                }

                yield return null;
            }

            Destroy(flash);
            Destroy(cone);
            foreach (var shard in shards)
                Destroy(shard.obj);
            foreach (var snake in snakes)
                Destroy(snake.obj);

            List<Coroutine> petrifyRoutines = new();
            foreach (PrototypeCardView target in validTargets)
                petrifyRoutines.Add(StartCoroutine(PlayTargetStoneSeal(parent, target, originals[target].scale, originals[target].rotation)));
            foreach (Coroutine coroutine in petrifyRoutines)
                yield return coroutine;
        }

        private IEnumerator PlayTargetStoneSeal(RectTransform parent, PrototypeCardView target, Vector3 originalScale, Quaternion originalRotation)
        {
            if (target == null)
                yield break;

            RectTransform targetRect = target.RectTransform;
            Vector2 targetSize = targetRect.rect.size;
            float size = Mathf.Clamp(Mathf.Max(targetSize.x, targetSize.y) * 1.22f, 130f, 280f);
            GameObject crack = CreateOverlaySprite(
                parent,
                "Medusa Stone Crack",
                LoadMedusaStoneCrackSprite(),
                Vector2.one * size,
                out RectTransform crackRect,
                out Image crackImage);

            GameObject dust = CreateOverlaySprite(
                parent,
                "Medusa Stone Dust",
                LoadMedusaStoneConeSprite(),
                new Vector2(size * 1.35f, size * 0.68f),
                out RectTransform dustRect,
                out Image dustImage);
            dustImage.preserveAspect = false;

            float duration = 0.52f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float hit = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 center = targetRect.position;

                crackRect.position = center;
                crackRect.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.12f, hit);
                crackRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 2f) * 4f);
                crackImage.color = new Color(0.9f, 0.93f, 0.94f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 2.4f)));

                dustRect.position = center + new Vector3(0f, -size * 0.06f, 0f);
                dustRect.localScale = new Vector3(Mathf.Lerp(0.55f, 1.25f, hit), Mathf.Lerp(0.55f, 1f, hit), 1f);
                dustRect.localRotation = Quaternion.identity;
                dustImage.color = new Color(0.48f, 0.5f, 0.5f, Mathf.Clamp01(Mathf.Min(progress * 6f, (1f - progress) * 2.2f)) * 0.38f);

                float stone = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.12f) / 0.42f));
                targetRect.localScale = originalScale * Mathf.Lerp(1.08f, 0.96f, stone);
                targetRect.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 9f) * (1f - stone) * 2.5f);
                yield return null;
            }

            targetRect.localScale = originalScale;
            targetRect.localRotation = originalRotation;
            Destroy(dust);
            Destroy(crack);
        }

        public IEnumerator PlayPaladinDivineShieldBash(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            RectTransform attackerRect = attacker.RectTransform;
            RectTransform defenderRect = defender.RectTransform;
            Vector3 originalScale = attackerRect.localScale;
            Vector3 start = EdgePoint(attackerRect, defenderRect.position);
            Vector3 end = EdgePoint(defenderRect, attackerRect.position);
            Vector3 direction = (end - start).sqrMagnitude > 0.01f
                ? (end - start).normalized
                : Vector3.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            GameObject crest = CreateOverlaySprite(
                parent,
                "Paladin Consecrated Crest",
                LoadPaladinCrestSprite(),
                new Vector2(210f, 210f),
                out RectTransform crestRect,
                out Image crestImage);
            GameObject shield = CreateOverlaySprite(
                parent,
                "Paladin Divine Shield Bash",
                LoadPaladinShieldSprite(),
                new Vector2(170f, 190f),
                out RectTransform shieldRect,
                out Image shieldImage);

            float windupDuration = 0.16f;
            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / windupDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                attackerRect.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.045f, eased);
                crestRect.position = attackerRect.position + new Vector3(0f, 8f, 0f);
                crestRect.localRotation = Quaternion.Euler(0f, 0f, -16f + progress * 42f);
                crestRect.localScale = Vector3.one * Mathf.Lerp(0.36f, 0.92f, eased);
                crestImage.color = new Color(1f, 0.78f, 0.3f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress * 0.28f)) * 0.82f));
                shieldRect.position = start - direction * 36f + new Vector3(0f, 12f, 0f);
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(0.74f, 1.04f, eased);
                shieldImage.color = new Color(1f, 0.9f, 0.62f, Mathf.Clamp01(progress * 7f));
                yield return null;
            }

            float flightDuration = 0.26f;
            elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                Vector3 arc = Vector3.up * Mathf.Sin(progress * Mathf.PI) * 18f;
                shieldRect.position = Vector3.LerpUnclamped(start, end, eased) + arc;
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + Mathf.Sin(progress * Mathf.PI) * 7f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(1.04f, 1.22f, Mathf.Sin(progress * Mathf.PI));
                shieldImage.color = new Color(1f, 0.88f, 0.54f, Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 12f)));

                crestRect.position = shieldRect.position - direction * 42f;
                crestRect.localRotation = Quaternion.Euler(0f, 0f, progress * 96f);
                crestRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, Mathf.Sin(progress * Mathf.PI));
                crestImage.color = new Color(1f, 0.7f, 0.24f, Mathf.Clamp01((1f - progress) * 0.58f));
                yield return null;
            }

            attackerRect.localScale = originalScale;
            Destroy(crest);
            Destroy(shield);
            yield return StartCoroutine(PlayPaladinHolyImpact(parent, defenderRect.position, blocked: false));
            yield return StartCoroutine(PlayImpactPulse(defenderRect));
        }

        public IEnumerator PlayPaladinProtectionConstellation(PrototypeCardView target)
        {
            RectTransform parent = ResolveProjectileParent(target);
            if (parent == null || target == null)
                yield break;

            RectTransform targetRect = target.RectTransform;
            GameObject shield = CreateOverlaySprite(
                parent,
                "Paladin Protection Constellation",
                LoadPaladinConstellationShieldSprite(),
                new Vector2(260f, 292f),
                out RectTransform shieldRect,
                out Image shieldImage);
            shieldRect.position = targetRect.position + new Vector3(0f, 8f, 0f);

            float duration = 0.62f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.18f));
                float fade = 1f - Mathf.SmoothStep(0.42f, 1f, progress);
                float pulse = Mathf.Sin(progress * Mathf.PI);

                shieldRect.position = targetRect.position + new Vector3(0f, 8f + pulse * 4f, 0f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.72f, eased);
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 5f, eased));
                shieldImage.color = new Color(0.72f, 0.9f, 1f, Mathf.Clamp01(appear * fade * 0.92f));
                yield return null;
            }

            Destroy(shield);
        }

        public IEnumerator PlayPaladinAegisBlocked(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            RectTransform attackerRect = attacker.RectTransform;
            RectTransform defenderRect = defender.RectTransform;
            Vector3 originalScale = attackerRect.localScale;
            Vector3 guardPoint = EdgePoint(defenderRect, attackerRect.position);
            Vector3 fromAttacker = (guardPoint - attackerRect.position).sqrMagnitude > 0.01f
                ? (guardPoint - attackerRect.position).normalized
                : Vector3.right;
            guardPoint -= fromAttacker * 20f;
            float angle = Mathf.Atan2(fromAttacker.y, fromAttacker.x) * Mathf.Rad2Deg;

            GameObject shield = CreateOverlaySprite(
                parent,
                "Paladin Aegis Block",
                LoadPaladinShieldSprite(),
                new Vector2(220f, 248f),
                out RectTransform shieldRect,
                out Image shieldImage);
            GameObject crest = CreateOverlaySprite(
                parent,
                "Paladin Blocking Crest",
                LoadPaladinCrestSprite(),
                new Vector2(260f, 260f),
                out RectTransform crestRect,
                out Image crestImage);

            float duration = 0.42f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.18f));
                float fade = Mathf.Clamp01(1f - Mathf.SmoothStep(0.62f, 1f, progress));
                float pulse = Mathf.Sin(progress * Mathf.PI);
                attackerRect.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.035f, pulse);
                shieldRect.position = guardPoint + new Vector3(Mathf.Sin(progress * Mathf.PI * 9f) * 2f * fade, 0f, 0f);
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.16f, appear) * (1f + pulse * 0.045f);
                shieldImage.color = new Color(1f, 0.88f, 0.56f, Mathf.Clamp01(appear * fade));

                crestRect.position = guardPoint;
                crestRect.localRotation = Quaternion.Euler(0f, 0f, progress * -92f);
                crestRect.localScale = Vector3.one * Mathf.Lerp(0.74f, 1.28f, appear);
                crestImage.color = new Color(1f, 0.68f, 0.22f, Mathf.Clamp01(appear * fade * 0.55f));
                yield return null;
            }

            attackerRect.localScale = originalScale;
            Destroy(shield);
            Destroy(crest);
            yield return StartCoroutine(PlayPaladinHolyImpact(parent, guardPoint, blocked: true));
        }

        private static IEnumerator PlayPaladinHolyImpact(RectTransform parent, Vector3 worldPosition, bool blocked)
        {
            GameObject burst = CreateOverlaySprite(
                parent,
                blocked ? "Paladin Aegis Shatter" : "Paladin Divine Impact",
                LoadPaladinCrestSprite(),
                new Vector2(blocked ? 300f : 260f, blocked ? 300f : 260f),
                out RectTransform burstRect,
                out Image burstImage);
            burstRect.position = worldPosition;

            int particleCount = blocked ? 18 : 14;
            var particles = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float spin, float scale)>(particleCount);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = CreateOverlaySprite(
                    parent,
                    blocked ? "Paladin Shield Shard" : "Paladin Radiant Spark",
                    blocked ? LoadPaladinShardSprite() : LoadPriestSparkSprite(),
                    new Vector2(blocked ? 58f : 38f, blocked ? 66f : 38f),
                    out RectTransform particleRect,
                    out Image particleImage);
                float angle = blocked
                    ? Mathf.Lerp(Mathf.PI * 0.08f, Mathf.PI * 0.92f, i / Mathf.Max(1f, particleCount - 1f)) + UnityEngine.Random.Range(-0.18f, 0.18f)
                    : (Mathf.PI * 2f * i / particleCount) + UnityEngine.Random.Range(-0.2f, 0.2f);
                float distance = UnityEngine.Random.Range(blocked ? 76f : 46f, blocked ? 176f : 118f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * (blocked ? 0.72f : 0.62f), 0f) * distance;
                if (blocked)
                    offset.y = Mathf.Abs(offset.y) + UnityEngine.Random.Range(-8f, 24f);
                particleRect.position = worldPosition;
                particleRect.localScale = Vector3.one * UnityEngine.Random.Range(0.62f, 1.05f);
                particleImage.color = new Color(1f, 0.8f, 0.36f, 0f);
                particles.Add((particle, particleRect, particleImage, offset, UnityEngine.Random.Range(-220f, 220f), UnityEngine.Random.Range(0.62f, 1.08f)));
            }

            float duration = blocked ? 0.48f : 0.34f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                burstRect.localScale = Vector3.one * Mathf.Lerp(blocked ? 0.56f : 0.44f, blocked ? 1.36f : 1.14f, eased);
                burstRect.localRotation = Quaternion.Euler(0f, 0f, progress * (blocked ? -74f : 48f));
                burstImage.color = new Color(1f, 0.7f, 0.22f, Mathf.Clamp01(Mathf.Min(progress * 11f, fade * (blocked ? 0.72f : 0.58f))));

                for (int i = 0; i < particles.Count; i++)
                {
                    var particle = particles[i];
                    Vector3 drift = blocked
                        ? new Vector3(Mathf.Sin((progress + i * 0.21f) * Mathf.PI) * 18f, -progress * 24f, 0f)
                        : Vector3.zero;
                    particle.rect.position = worldPosition + particle.offset * eased + drift;
                    particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.spin * progress);
                    particle.rect.localScale = Vector3.one * Mathf.Lerp(particle.scale, particle.scale * 0.34f, eased);
                    particle.image.color = new Color(1f, 0.82f, 0.42f, Mathf.Clamp01(Mathf.Min(progress * 12f, fade * 0.95f)));
                }
                yield return null;
            }

            Destroy(burst);
            foreach (var particle in particles)
                Destroy(particle.obj);
        }

        public IEnumerator PlayNecromancerSoulSwarm(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            Vector3 originalScale = attacker.RectTransform.localScale;
            float windupDuration = 0.14f;
            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / windupDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.07f, progress);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            yield return StartCoroutine(PlayNecromancerSkullVolley(parent, attacker.RectTransform, defender.RectTransform, blocked: false));
            yield return StartCoroutine(PlayNecromancerGreenExplosion(parent, defender.RectTransform.position, false));
            yield return StartCoroutine(PlayImpactPulse(defender.RectTransform));
        }

        public IEnumerator PlayNecromancerSoulWardBlocked(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            Vector3 originalScale = attacker.RectTransform.localScale;
            float windupDuration = 0.12f;
            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / windupDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.05f, progress);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            yield return StartCoroutine(PlayNecromancerSkullVolley(parent, attacker.RectTransform, defender.RectTransform, blocked: true));
            yield return StartCoroutine(PlayNecromancerWardShatter(parent, defender.RectTransform.position));
        }

        private static IEnumerator PlayNecromancerSkullVolley(RectTransform parent, RectTransform attacker, RectTransform target, bool blocked)
        {
            const int skullCount = 6;
            Vector3 start = attacker.position;
            Vector3 end = target.position;
            Vector3 travel = end - start;
            Vector3 travelDirection = travel.sqrMagnitude > 0.001f ? travel.normalized : Vector3.right;
            Vector3 perpendicular = new Vector3(-travelDirection.y, travelDirection.x, 0f);
            float travelDistance = Mathf.Max(160f, travel.magnitude);
            var skulls = new List<(GameObject skull, RectTransform skullRect, Image skullImage, GameObject trail, RectTransform trailRect, Image trailImage, float delay, float lane, float wavePhase, float amplitude, float speed, float depthJitter)>(skullCount);

            for (int i = 0; i < skullCount; i++)
            {
                GameObject trail = CreateOverlaySprite(
                    parent,
                    "Necromancer Fired Skull Trail",
                    LoadNecromancerTrailSprite(),
                    new Vector2(172f, 68f),
                    out RectTransform trailRect,
                    out Image trailImage);
                GameObject skull = CreateOverlaySprite(
                    parent,
                    "Necromancer Fired Skull",
                    LoadNecromancerSkullSprite(),
                    new Vector2(68f, 68f),
                    out RectTransform skullRect,
                    out Image skullImage);

                float lane = ((i / (float)(skullCount - 1)) - 0.5f) * 92f + UnityEngine.Random.Range(-10f, 10f);
                float delay = i * 0.035f + UnityEngine.Random.Range(0f, 0.014f);
                float wavePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float amplitude = UnityEngine.Random.Range(10f, 24f);
                float speed = UnityEngine.Random.Range(0.92f, 1.12f);
                float depthJitter = UnityEngine.Random.Range(-8f, 8f);
                Vector3 spawn = start + perpendicular * lane * 0.34f + travelDirection * UnityEngine.Random.Range(-32f, 16f);
                skullRect.position = spawn;
                trailRect.position = spawn - travelDirection * 42f;
                skullImage.color = new Color(0.56f, 0.95f, 0.68f, 0f);
                trailImage.color = new Color(0.16f, 0.78f, 0.5f, 0f);
                skulls.Add((skull, skullRect, skullImage, trail, trailRect, trailImage, delay, lane, wavePhase, amplitude, speed, depthJitter));
            }

            float duration = blocked ? 0.72f : 0.78f;
            float elapsed = 0f;
            while (elapsed < duration + 0.28f)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < skulls.Count; i++)
                {
                    var item = skulls[i];
                    float localProgress = Mathf.Clamp01((elapsed - item.delay) / duration * item.speed);
                    if (localProgress <= 0f)
                        continue;

                    float eased = Mathf.SmoothStep(0f, 1f, localProgress);
                    float fade = Mathf.Clamp01(Mathf.Min(localProgress * 7f, (1f - localProgress) * 4f + 0.18f));
                    float laneWidth = Mathf.Lerp(1f, blocked ? 0.48f : 0.12f, eased);
                    float wave = Mathf.Sin(localProgress * Mathf.PI * 3.2f + item.wavePhase) * item.amplitude * Mathf.Sin(localProgress * Mathf.PI);
                    Vector3 position = Vector3.LerpUnclamped(start, end, eased)
                        + perpendicular * (item.lane * laneWidth + wave)
                        + travelDirection * Mathf.Sin(localProgress * Mathf.PI) * item.depthJitter;
                    float angle = Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;
                    float scale = Mathf.Lerp(0.72f, blocked ? 0.88f : 1.08f, Mathf.Sin(localProgress * Mathf.PI));

                    item.skullRect.position = position;
                    item.skullRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + localProgress * 260f);
                    item.skullRect.localScale = Vector3.one * scale;
                    item.skullImage.color = new Color(0.58f, 0.96f, 0.72f, Mathf.Clamp01(fade * 0.88f));

                    item.trailRect.position = position - travelDirection * Mathf.Lerp(42f, 74f, Mathf.Sin(localProgress * Mathf.PI));
                    item.trailRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                    item.trailRect.localScale = new Vector3(Mathf.Lerp(0.78f, 1.34f, Mathf.Sin(localProgress * Mathf.PI)), 1.12f, 1f);
                    item.trailImage.color = new Color(0.14f, 0.76f, 0.48f, Mathf.Clamp01(fade * 0.55f));
                }
                yield return null;
            }

            foreach (var item in skulls)
            {
                Destroy(item.skull);
                Destroy(item.trail);
            }
        }

        public IEnumerator PlayNecromancerReviveSkullConvergence(PrototypeCardView target)
        {
            RectTransform parent = ResolveProjectileParent(target);
            if (parent == null || target == null)
                yield break;

            yield return PlayNecromancerReviveSkullConvergence(parent, target.RectTransform);
        }

        private static IEnumerator PlayNecromancerReviveSkullConvergence(RectTransform parent, RectTransform target)
        {
            const int skullCount = 7;
            Vector3 center = target.position;
            var skulls = new List<(GameObject skull, RectTransform skullRect, Image skullImage, GameObject trail, RectTransform trailRect, Image trailImage, float angle, float distance, float wavePhase, float amplitude)>(skullCount);

            for (int i = 0; i < skullCount; i++)
            {
                GameObject trail = CreateOverlaySprite(
                    parent,
                    "Necromancer Revive Trail",
                    LoadNecromancerTrailSprite(),
                    new Vector2(178f, 70f),
                    out RectTransform trailRect,
                    out Image trailImage);
                GameObject skull = CreateOverlaySprite(
                    parent,
                    "Necromancer Revive Skull",
                    LoadNecromancerSkullSprite(),
                    new Vector2(74f, 74f),
                    out RectTransform skullRect,
                    out Image skullImage);

                float angle = Mathf.PI * 2f * i / skullCount + UnityEngine.Random.Range(-0.16f, 0.16f);
                float distance = UnityEngine.Random.Range(155f, 245f);
                float wavePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float amplitude = UnityEngine.Random.Range(8f, 22f);
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.72f, 0f).normalized;
                Vector3 start = center + direction * distance;
                skullRect.position = start;
                trailRect.position = start;
                skullImage.color = new Color(0.56f, 0.95f, 0.68f, 0f);
                trailImage.color = new Color(0.16f, 0.78f, 0.5f, 0f);
                skulls.Add((skull, skullRect, skullImage, trail, trailRect, trailImage, angle, distance, wavePhase, amplitude));
            }

            GameObject focalSkull = CreateOverlaySprite(
                parent,
                "Necromancer Revive Focal Skull",
                LoadNecromancerSkullSprite(),
                new Vector2(92f, 92f),
                out RectTransform focalSkullRect,
                out Image focalSkullImage);
            focalSkullRect.position = center;
            focalSkullImage.color = new Color(0.58f, 0.96f, 0.72f, 0f);

            float duration = 0.9f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = Mathf.Clamp01(Mathf.Min(progress * 6f, (1f - progress) * 3.6f + 0.15f));

                for (int i = 0; i < skulls.Count; i++)
                {
                    var item = skulls[i];
                    Vector3 direction = new Vector3(Mathf.Cos(item.angle), Mathf.Sin(item.angle) * 0.72f, 0f).normalized;
                    Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
                    float wave = Mathf.Sin(progress * Mathf.PI * 2.8f + item.wavePhase) * item.amplitude * Mathf.Sin(progress * Mathf.PI);
                    Vector3 position = center + direction * Mathf.Lerp(item.distance, 8f, eased) + perpendicular * wave;
                    float travelAngle = Mathf.Atan2((-direction).y, (-direction).x) * Mathf.Rad2Deg;

                    item.skullRect.position = position;
                    item.skullRect.localRotation = Quaternion.Euler(0f, 0f, travelAngle - 90f + progress * 180f);
                    item.skullRect.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.18f, Mathf.Sin(progress * Mathf.PI));
                    item.skullImage.color = new Color(0.58f, 0.96f, 0.72f, Mathf.Clamp01(fade * 0.88f));

                    item.trailRect.position = position + direction * 58f;
                    item.trailRect.localRotation = Quaternion.Euler(0f, 0f, travelAngle);
                    item.trailRect.localScale = new Vector3(Mathf.Lerp(0.78f, 1.42f, Mathf.Sin(progress * Mathf.PI)), 1.16f, 1f);
                    item.trailImage.color = new Color(0.14f, 0.76f, 0.48f, Mathf.Clamp01(fade * 0.66f));
                }

                focalSkullRect.position = center;
                focalSkullRect.localRotation = Quaternion.Euler(0f, 0f, progress * -96f);
                focalSkullRect.localScale = Vector3.one * Mathf.Lerp(0.34f, 1.28f, Mathf.Sin(progress * Mathf.PI));
                focalSkullImage.color = new Color(0.58f, 0.96f, 0.72f, Mathf.Clamp01(fade * 0.7f));

                yield return null;
            }

            foreach (var item in skulls)
            {
                Destroy(item.skull);
                Destroy(item.trail);
            }
            Destroy(focalSkull);

            yield return PlayNecromancerGreenExplosion(parent, center, blocked: false);
        }

        private static IEnumerator PlayNecromancerSkullOrbit(RectTransform parent, RectTransform target, bool collapse)
        {
            const int skullCount = 4;
            Vector3 center = target.position;
            var skulls = new List<(GameObject skull, RectTransform skullRect, Image skullImage, GameObject trail, RectTransform trailRect, Image trailImage, float phase)>(skullCount);
            for (int i = 0; i < skullCount; i++)
            {
                GameObject trail = CreateOverlaySprite(
                    parent,
                    "Necromancer Skull Trail",
                    LoadNecromancerTrailSprite(),
                    new Vector2(184f, 72f),
                    out RectTransform trailRect,
                    out Image trailImage);
                GameObject skull = CreateOverlaySprite(
                    parent,
                    "Necromancer Orbiting Skull",
                    LoadNecromancerSkullSprite(),
                    new Vector2(68f, 68f),
                    out RectTransform skullRect,
                    out Image skullImage);
                float phase = (Mathf.PI * 2f * i / skullCount) + UnityEngine.Random.Range(-0.15f, 0.15f);
                skullImage.color = new Color(0.56f, 0.95f, 0.68f, 0f);
                trailImage.color = new Color(0.16f, 0.78f, 0.5f, 0f);
                skulls.Add((skull, skullRect, skullImage, trail, trailRect, trailImage, phase));
            }

            float duration = collapse ? 1.02f : 0.86f;
            float startRadius = 136f;
            float endRadius = collapse ? 18f : 92f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fadeOut = collapse ? 1f : Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, progress));
                float radius = Mathf.Lerp(startRadius, endRadius, eased);
                for (int i = 0; i < skulls.Count; i++)
                {
                    var item = skulls[i];
                    float angle = item.phase + progress * Mathf.PI * (collapse ? 3.3f : 2.15f);
                    Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.62f, 0f);
                    Vector3 position = center + direction * radius;
                    Vector3 tangent = new Vector3(-direction.y, direction.x, 0f).normalized;
                    item.skullRect.position = position;
                    item.skullRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f);
                    item.skullRect.localScale = Vector3.one * Mathf.Lerp(0.86f, collapse ? 1.34f : 0.74f, collapse ? eased : progress);
                    item.skullImage.color = new Color(0.58f, 0.96f, 0.72f, Mathf.Clamp01(Mathf.Min(progress * 7f, fadeOut) * 0.84f));

                    item.trailRect.position = position - tangent * 46f;
                    item.trailRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                    item.trailRect.localScale = new Vector3(Mathf.Lerp(0.78f, 1.34f, Mathf.Sin(progress * Mathf.PI)), 1.14f, 1f);
                    item.trailImage.color = new Color(0.14f, 0.76f, 0.48f, Mathf.Clamp01(Mathf.Min(progress * 6f, fadeOut * 0.55f)));
                }
                yield return null;
            }

            foreach (var item in skulls)
            {
                Destroy(item.skull);
                Destroy(item.trail);
            }
        }

        private static IEnumerator PlayNecromancerGreenExplosion(RectTransform parent, Vector3 worldPosition, bool blocked)
        {
            GameObject burst = CreateOverlaySprite(
                parent,
                blocked ? "Necromancer Failed Soul Burst" : "Necromancer Soul Explosion",
                LoadNecromancerBurstSprite(),
                new Vector2(blocked ? 230f : 300f, blocked ? 230f : 300f),
                out RectTransform burstRect,
                out Image burstImage);
            burstRect.position = worldPosition;

            int particleCount = blocked ? 8 : 12;
            var particles = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float spin)>(particleCount);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = CreateOverlaySprite(
                    parent,
                    "Necromancer Soul Fragment",
                    LoadNecromancerTrailSprite(),
                    new Vector2(96f, 38f),
                    out RectTransform particleRect,
                    out Image particleImage);
                float angle = Mathf.PI * 2f * i / particleCount + UnityEngine.Random.Range(-0.18f, 0.18f);
                float distance = UnityEngine.Random.Range(blocked ? 66f : 82f, blocked ? 132f : 178f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.78f, 0f) * distance;
                particleRect.position = worldPosition;
                particleRect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                particleImage.color = new Color(0.18f, 0.84f, 0.5f, 0f);
                particles.Add((particle, particleRect, particleImage, offset, UnityEngine.Random.Range(-220f, 220f)));
            }

            float duration = blocked ? 0.48f : 0.56f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                burstRect.localScale = Vector3.one * Mathf.Lerp(0.34f, blocked ? 1.28f : 1.62f, eased);
                burstRect.localRotation = Quaternion.Euler(0f, 0f, progress * (blocked ? -42f : 58f));
                burstImage.color = new Color(0.28f, 0.9f, 0.58f, Mathf.Clamp01(Mathf.Min(progress * 8f, fade * 0.82f)));

                for (int i = 0; i < particles.Count; i++)
                {
                    var particle = particles[i];
                    particle.rect.position = worldPosition + particle.offset * eased;
                    particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.spin * progress);
                    particle.rect.localScale = Vector3.one * Mathf.Lerp(0.96f, 0.32f, eased);
                    particle.image.color = new Color(0.16f, 0.78f, 0.48f, Mathf.Clamp01(Mathf.Min(progress * 8f, fade * 0.68f)));
                }
                yield return null;
            }

            Destroy(burst);
            foreach (var particle in particles)
                Destroy(particle.obj);
        }

        private static IEnumerator PlayNecromancerWardShatter(RectTransform parent, Vector3 worldPosition)
        {
            GameObject ward = CreateOverlaySprite(
                parent,
                "Necromancer Soul Ward",
                LoadNecromancerBurstSprite(),
                new Vector2(260f, 260f),
                out RectTransform wardRect,
                out Image wardImage);
            wardRect.position = worldPosition;

            float wardDuration = 0.28f;
            float elapsed = 0f;
            while (elapsed < wardDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / wardDuration);
                wardRect.localScale = Vector3.one * Mathf.Lerp(0.48f, 1.08f, Mathf.Sin(progress * Mathf.PI));
                wardRect.localRotation = Quaternion.Euler(0f, 0f, progress * -48f);
                wardImage.color = new Color(0.28f, 0.9f, 0.58f, Mathf.Clamp01(Mathf.Min(progress * 7f, (1f - progress) * 0.95f)));
                yield return null;
            }

            Destroy(ward);
            yield return PlayNecromancerGreenExplosion(parent, worldPosition, true);
        }

        public IEnumerator PlayRogueDaggerFlurry(PrototypeCardView attacker, PrototypeCardView defender, int attackMargin, Action onHit = null)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            int daggerCount = Mathf.Clamp(Mathf.Max(1, attackMargin), 1, 9);
            Vector3 originalScale = attacker.RectTransform.localScale;
            float windupDuration = 0.14f;
            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / windupDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.07f, progress);
                yield return null;
            }

            for (int i = 0; i < daggerCount; i++)
            {
                StartCoroutine(PlayRogueDagger(parent, attacker.RectTransform, defender.RectTransform, i, blocked: false, onHit));
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.045f, 0.115f));
            }

            attacker.RectTransform.localScale = originalScale;
            yield return new WaitForSecondsRealtime(0.46f);
            yield return StartCoroutine(PlayImpactPulse(defender.RectTransform));
        }

        public IEnumerator PlayRogueDaggerBlocked(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            Vector3 originalScale = attacker.RectTransform.localScale;
            float windupDuration = 0.12f;
            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / windupDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.05f, progress);
                yield return null;
            }

            int daggerCount = UnityEngine.Random.Range(3, 6);
            for (int i = 0; i < daggerCount; i++)
            {
                StartCoroutine(PlayRogueDagger(parent, attacker.RectTransform, defender.RectTransform, i, blocked: true));
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.055f, 0.13f));
            }

            attacker.RectTransform.localScale = originalScale;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private IEnumerator PlayRogueDagger(RectTransform parent, RectTransform attacker, RectTransform defender, int index, bool blocked, Action onHit = null)
        {
            Vector3 targetCenter = defender.position;
            Vector3 seedOffset = new Vector3(
                UnityEngine.Random.Range(-34f, 34f),
                UnityEngine.Random.Range(-46f, 46f),
                0f);
            Vector3 end = blocked
                ? Vector3.LerpUnclamped(EdgePoint(defender, attacker.position), targetCenter, 0.12f) + seedOffset * 0.36f
                : targetCenter + seedOffset;
            Vector3 start = EdgePoint(attacker, end)
                + new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-18f, 18f), 0f);
            Vector3 direction = end - start;
            Vector3 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            GameObject dagger = CreateOverlaySprite(
                parent,
                blocked ? "Rogue Deflected Dagger" : "Rogue Flying Dagger",
                LoadRogueDaggerSprite(),
                new Vector2(124f, 124f),
                out RectTransform daggerRect,
                out Image daggerImage);
            daggerRect.position = start;
            daggerRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            daggerRect.localScale = Vector3.one * UnityEngine.Random.Range(0.72f, 0.96f);

            float flightDuration = UnityEngine.Random.Range(0.24f, 0.38f);
            float elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 arc = new Vector3(0f, Mathf.Sin(progress * Mathf.PI) * UnityEngine.Random.Range(10f, 24f), 0f);
                daggerRect.position = Vector3.LerpUnclamped(start, end, eased) + arc;
                daggerRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + Mathf.Sin(progress * Mathf.PI) * UnityEngine.Random.Range(-8f, 8f));
                daggerImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 12f)));
                yield return null;
            }

            Destroy(dagger);
            if (blocked)
            {
                yield return StartCoroutine(PlayRogueDeflect(parent, end, normalized, index));
            }
            else
            {
                PlayRogueDaggerHitSfx();
                onHit?.Invoke();
                yield return StartCoroutine(PlayRogueHitMarker(parent, end, index));
            }
        }

        private void PlayRogueDaggerHitSfx()
        {
            if (rogueSfxSource == null)
            {
                rogueSfxSource = gameObject.AddComponent<AudioSource>();
                rogueSfxSource.playOnAwake = false;
                rogueSfxSource.loop = false;
                rogueSfxSource.spatialBlend = 0f;
            }

            if (rogueAttackHitSfx == null)
                rogueAttackHitSfx = Resources.Load<AudioClip>("SFX/rogue_attack_hit");

            if (rogueAttackHitSfx == null)
                return;

            bool muted = PlayerPrefs.GetInt(BattleSfxPlayer.MutedPlayerPrefsKey, 0) != 0;
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(BattleSfxPlayer.VolumePlayerPrefsKey, 1f));
            if (muted || volume <= 0f)
                return;

            rogueSfxSource.PlayOneShot(rogueAttackHitSfx, volume);
        }

        private static IEnumerator PlayRogueHitMarker(RectTransform parent, Vector3 worldPosition, int index)
        {
            GameObject marker = CreateOverlaySprite(
                parent,
                "Rogue Dagger Hit Marker",
                LoadRogueHitMarkerSprite(),
                new Vector2(86f, 86f),
                out RectTransform markerRect,
                out Image markerImage);
            markerRect.position = worldPosition;
            markerRect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-38f, 38f));

            float duration = 0.24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);
                markerRect.localScale = Vector3.one * Mathf.Lerp(0.46f, 1.18f, Mathf.Sin(progress * Mathf.PI));
                markerRect.localRotation = Quaternion.Euler(0f, 0f, markerRect.localRotation.eulerAngles.z + (index % 2 == 0 ? 90f : -90f) * Time.unscaledDeltaTime);
                markerImage.color = new Color(0.9f, 1f, 0.72f, Mathf.Clamp01(Mathf.Min(progress * 12f, fade * 1.1f)));
                yield return null;
            }

            Destroy(marker);
        }

        private static IEnumerator PlayRogueDeflect(RectTransform parent, Vector3 worldPosition, Vector3 incomingDirection, int index)
        {
            GameObject deflect = CreateOverlaySprite(
                parent,
                "Rogue Dagger Deflect",
                LoadRogueDeflectSprite(),
                new Vector2(118f, 118f),
                out RectTransform deflectRect,
                out Image deflectImage);
            deflectRect.position = worldPosition;
            deflectRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(incomingDirection.y, incomingDirection.x) * Mathf.Rad2Deg + 90f);

            float duration = 0.28f;
            float elapsed = 0f;
            Vector3 drift = new Vector3(-incomingDirection.y, incomingDirection.x, 0f) * UnityEngine.Random.Range(index % 2 == 0 ? 42f : -68f, index % 2 == 0 ? 68f : -42f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                deflectRect.position = worldPosition + drift * eased + new Vector3(0f, -28f * progress, 0f);
                deflectRect.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.34f, Mathf.Sin(progress * Mathf.PI));
                deflectRect.localRotation = Quaternion.Euler(0f, 0f, deflectRect.localRotation.eulerAngles.z + 560f * Time.unscaledDeltaTime);
                deflectImage.color = new Color(0.72f, 1f, 0.86f, Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 1.4f)));
                yield return null;
            }

            Destroy(deflect);
        }

        public IEnumerator PlayMageArcaneBoltAttack(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            GameObject trailObject = CreateMageArcaneTrail(parent, out RectTransform trailRect, out Image trailImage);
            GameObject boltObject = CreateMageArcaneProjectile(parent, out RectTransform boltRect, out Image boltImage);

            Vector3 start = EdgePoint(attacker.RectTransform, defender.RectTransform.position);
            Vector3 end = EdgePoint(defender.RectTransform, attacker.RectTransform.position);
            Vector3 direction = end - start;
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float castDuration = 0.18f;
            float flightDuration = 0.48f;
            float elapsed = 0f;
            Vector3 originalScale = attacker.RectTransform.localScale;
            while (elapsed < castDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / castDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.08f, progress);
                boltRect.position = start;
                boltRect.localRotation = Quaternion.Euler(0f, 0f, progress * 150f);
                boltRect.localScale = Vector3.one * Mathf.Lerp(0.36f, 1.08f, progress);
                boltImage.color = new Color(0.76f, 0.58f, 1f, Mathf.Clamp01(progress * 1.15f));
                UpdateArcaneTrail(trailRect, trailImage, start, normalizedDirection, angle, 0f, 0f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                boltRect.position = Vector3.LerpUnclamped(start, end, eased);
                boltRect.localRotation = Quaternion.Euler(0f, 0f, elapsed * 720f);
                boltRect.localScale = Vector3.one * Mathf.Lerp(1.08f, 1.62f, pulse);
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 9f));
                boltImage.color = new Color(0.76f, 0.58f, 1f, alpha);
                UpdateArcaneTrail(trailRect, trailImage, boltRect.position, normalizedDirection, angle, progress, alpha);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            yield return StartCoroutine(PlayArcaneImpact(parent, end, blocked: false));
            yield return StartCoroutine(PlayImpactPulse(defender.RectTransform));
            Destroy(trailObject);
            Destroy(boltObject);
        }

        public IEnumerator PlayMageArcaneBoltBlocked(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            GameObject trailObject = CreateMageArcaneTrail(parent, out RectTransform trailRect, out Image trailImage);
            GameObject boltObject = CreateMageArcaneProjectile(parent, out RectTransform boltRect, out Image boltImage);

            Vector3 start = EdgePoint(attacker.RectTransform, defender.RectTransform.position);
            Vector3 targetEdge = EdgePoint(defender.RectTransform, attacker.RectTransform.position);
            Vector3 blockPoint = Vector3.LerpUnclamped(targetEdge, defender.RectTransform.position, 0.28f);
            Vector3 direction = blockPoint - start;
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float castDuration = 0.16f;
            float flightDuration = 0.42f;
            float elapsed = 0f;
            Vector3 originalScale = attacker.RectTransform.localScale;
            while (elapsed < castDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / castDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.06f, progress);
                boltRect.position = start;
                boltRect.localRotation = Quaternion.Euler(0f, 0f, progress * 130f);
                boltRect.localScale = Vector3.one * Mathf.Lerp(0.34f, 1.02f, progress);
                boltImage.color = new Color(0.76f, 0.58f, 1f, Mathf.Clamp01(progress * 1.18f));
                UpdateArcaneTrail(trailRect, trailImage, start, normalizedDirection, angle, 0f, 0f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                boltRect.position = Vector3.LerpUnclamped(start, blockPoint, eased);
                boltRect.localRotation = Quaternion.Euler(0f, 0f, elapsed * 760f);
                boltRect.localScale = Vector3.one * Mathf.Lerp(1.02f, 1.48f, Mathf.Sin(progress * Mathf.PI));
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 9f, (1f - progress) * 11f));
                boltImage.color = new Color(0.76f, 0.58f, 1f, alpha);
                UpdateArcaneTrail(trailRect, trailImage, boltRect.position, normalizedDirection, angle, progress, alpha);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            Destroy(trailObject);
            Destroy(boltObject);
            yield return StartCoroutine(PlayArcaneImpact(parent, blockPoint, blocked: true));
        }

        public IEnumerator PlayPriestSacredJudgement(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            Vector3 start = EdgePoint(attacker.RectTransform, defender.RectTransform.position);
            Vector3 target = defender.RectTransform.position;
            Vector3 direction = target - start;
            Vector3 targetDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            Vector3 beamEnd = target + targetDirection * 180f;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 originalScale = attacker.RectTransform.localScale;
            GameObject beamObject = CreatePriestBeam(parent, "Priest Sacred Judgement", out RectTransform beamRect, out Image beamImage);
            GameObject coreObject = CreateOverlaySprite(
                parent,
                "Priest Judgement Core",
                LoadPriestSparkSprite(),
                new Vector2(130f, 130f),
                out RectTransform coreRect,
                out Image coreImage);

            beamRect.pivot = new Vector2(0.5f, 0f);
            beamRect.position = start;
            beamRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            beamRect.sizeDelta = new Vector2(150f, 1f);
            coreRect.position = target;
            coreImage.color = new Color(1f, 0.92f, 0.42f, 0f);

            float castDuration = 0.18f;
            float strikeDuration = 0.42f;
            float elapsed = 0f;
            while (elapsed < castDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / castDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.07f, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < strikeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / strikeDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                float flash = Mathf.Clamp01(Mathf.Min(progress * 7f, fade * 2.7f));
                beamRect.position = start;
                beamRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                float beamLength = Vector3.Distance(start, beamEnd);
                beamRect.sizeDelta = new Vector2(Mathf.Lerp(96f, 178f, Mathf.Sin(progress * Mathf.PI)), beamLength * Mathf.Lerp(0.08f, 1.08f, eased));
                beamRect.localScale = Vector3.one;
                beamImage.color = new Color(1f, 0.93f, 0.48f, flash * 0.88f);
                coreRect.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.36f, Mathf.Sin(progress * Mathf.PI));
                coreRect.localRotation = Quaternion.Euler(0f, 0f, progress * 52f);
                coreImage.color = new Color(1f, 0.96f, 0.62f, flash);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            Destroy(beamObject);
            Destroy(coreObject);
            yield return StartCoroutine(PlayPriestHitMarker(parent, target, defender.RectTransform));
        }

        public IEnumerator PlayPriestJudgementBlocked(PrototypeCardView attacker, PrototypeCardView defender)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            Vector3 start = EdgePoint(attacker.RectTransform, defender.RectTransform.position);
            Vector3 targetEdge = EdgePoint(defender.RectTransform, attacker.RectTransform.position);
            Vector3 blockPoint = Vector3.LerpUnclamped(targetEdge, defender.RectTransform.position, 0.56f);
            Vector3 direction = blockPoint - start;
            float length = Mathf.Max(1f, direction.magnitude);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 originalScale = attacker.RectTransform.localScale;
            GameObject beamObject = CreatePriestBeam(parent, "Priest Blocked Judgement", out RectTransform beamRect, out Image beamImage);
            beamRect.pivot = new Vector2(0.5f, 0f);
            beamRect.position = start;
            beamRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            beamRect.sizeDelta = new Vector2(138f, 1f);

            float castDuration = 0.16f;
            float strikeDuration = 0.34f;
            float elapsed = 0f;
            while (elapsed < castDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / castDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.05f, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < strikeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / strikeDuration);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                beamRect.position = start;
                beamRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                beamRect.sizeDelta = new Vector2(Mathf.Lerp(90f, 160f, Mathf.Sin(progress * Mathf.PI)), length * Mathf.Lerp(0.08f, 1.04f, eased));
                beamRect.localScale = Vector3.one;
                beamImage.color = new Color(1f, 0.96f, 0.64f, Mathf.Clamp01(Mathf.Min(progress * 8f, fade * 1.3f)));
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            Destroy(beamObject);
            yield return StartCoroutine(PlayHolySparkScatter(parent, blockPoint, blocked: true));
        }

        public IEnumerator PlayPriestBlessing(PrototypeCardView caster, PrototypeCardView target, int magnitude = 0)
        {
            if (caster == null || target == null)
                yield break;

            RectTransform parent = ResolveProjectileParent(caster);
            if (parent == null)
                parent = ResolveProjectileParent(target);
            if (parent == null)
                yield break;

            Vector3 casterCenter = caster.RectTransform.position;
            Vector3 targetCenter = target.RectTransform.position;
            bool selfCast = Vector3.SqrMagnitude(targetCenter - casterCenter) < 4f;
            Vector3 start = selfCast
                ? targetCenter + new Vector3(0f, 132f, 0f)
                : EdgePoint(caster.RectTransform, targetCenter);
            Vector3 end = targetCenter;
            Vector3 direction = end - start;
            float length = Mathf.Max(1f, direction.magnitude);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            Vector3 casterScale = caster.RectTransform.localScale;
            Vector3 targetScale = target.RectTransform.localScale;
            GameObject beamObject = CreatePriestBeam(parent, "Priest Blessing Beam", out RectTransform beamRect, out Image beamImage);
            GameObject haloObject = CreateOverlaySprite(
                parent,
                "Priest Blessing Halo",
                LoadPriestCrossSprite(),
                new Vector2(190f, 190f),
                out RectTransform haloRect,
                out Image haloImage);
            GameObject coreObject = CreateOverlaySprite(
                parent,
                "Priest Blessing Core",
                LoadPriestSparkSprite(),
                new Vector2(112f, 112f),
                out RectTransform coreRect,
                out Image coreImage);

            beamRect.pivot = new Vector2(0.5f, 0f);
            beamRect.position = start;
            beamRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            beamRect.sizeDelta = new Vector2(86f, 1f);
            haloRect.position = end;
            coreRect.position = end;

            float duration = 0.68f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                float fade = 1f - Mathf.SmoothStep(0.62f, 1f, progress);
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 7f, fade * 1.2f));
                float bonusPulse = magnitude > 0 ? Mathf.Clamp01(magnitude / 4f) * 0.08f : 0f;

                caster.RectTransform.localScale = casterScale * (1f + pulse * 0.04f);
                target.RectTransform.localScale = targetScale * (1f + pulse * (0.08f + bonusPulse));

                beamRect.position = start;
                beamRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                beamRect.sizeDelta = new Vector2(Mathf.Lerp(58f, 122f, pulse), length * Mathf.Lerp(0.05f, 1.02f, eased));
                beamImage.color = new Color(1f, 0.96f, 0.58f, alpha * 0.82f);

                haloRect.position = end;
                haloRect.localScale = Vector3.one * Mathf.Lerp(0.48f, 1.46f, eased);
                haloRect.localRotation = Quaternion.Euler(0f, 0f, progress * 38f);
                haloImage.color = new Color(1f, 0.96f, 0.64f, alpha * 0.9f);

                coreRect.position = end + new Vector3(0f, Mathf.Sin(progress * Mathf.PI * 2f) * 8f, 0f);
                coreRect.localScale = Vector3.one * Mathf.Lerp(0.42f, 1.2f, pulse);
                coreRect.localRotation = Quaternion.Euler(0f, 0f, -progress * 90f);
                coreImage.color = new Color(1f, 1f, 0.8f, alpha);
                yield return null;
            }

            caster.RectTransform.localScale = casterScale;
            target.RectTransform.localScale = targetScale;
            Destroy(beamObject);
            Destroy(haloObject);
            Destroy(coreObject);
            yield return StartCoroutine(PlayHolySparkScatter(parent, end, blocked: false));
        }

        public IEnumerator PlayHunterArrowMiss(PrototypeCardView attacker)
        {
            RectTransform parent = ResolveProjectileParent(attacker);
            if (parent == null)
            {
                yield return StartCoroutine(attacker.PlayAttackAnimation());
                yield break;
            }

            GameObject arrowObject = CreateHunterArrowProjectile(parent, out RectTransform arrowRect, out Image arrowImage);

            parent.GetWorldCorners(worldCorners);
            float centerX = (worldCorners[0].x + worldCorners[3].x) * 0.5f;
            float centerY = (worldCorners[0].y + worldCorners[1].y) * 0.5f;
            float width = Mathf.Max(1f, Vector3.Distance(worldCorners[0], worldCorners[3]));
            float height = Mathf.Max(1f, Vector3.Distance(worldCorners[0], worldCorners[1]));
            float horizontalOffset = attacker.RectTransform.position.x - centerX;
            float side = Mathf.Abs(horizontalOffset) < width * 0.08f ? 0f : Mathf.Sign(horizontalOffset);
            float verticalOffset = attacker.RectTransform.position.y - centerY;
            float verticalDirection = verticalOffset > 0f ? -1f : 1f;
            Vector3 direction = new Vector3(-side * 0.62f, verticalDirection, 0f).normalized;
            Vector3 start = EdgePoint(attacker.RectTransform, attacker.RectTransform.position + direction);
            Vector3 end = start + direction * Mathf.Max(width, height) * 1.35f;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            float drawDuration = 0.14f;
            float flightDuration = 0.48f;
            float elapsed = 0f;
            Vector3 originalScale = attacker.RectTransform.localScale;
            while (elapsed < drawDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / drawDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.06f, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = progress * progress;
                arrowRect.position = Vector3.LerpUnclamped(start, end, eased);
                arrowRect.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.22f, Mathf.Sin(progress * Mathf.PI));
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 4.5f));
                arrowImage.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            Destroy(arrowObject);
        }

        private static IEnumerator PlaySmokePuff(RectTransform parent, Vector3 worldPosition, float maxScale)
        {
            GameObject smokeObject = CreateOverlaySprite(
                parent,
                "Assassin Smoke Puff",
                LoadAssassinSmokeSprite(),
                new Vector2(560f, 560f),
                out RectTransform smokeRect,
                out Image smokeImage);
            smokeRect.position = worldPosition;

            float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Sin(progress * Mathf.PI);
                smokeRect.localScale = Vector3.one * Mathf.Lerp(0.25f, maxScale, Mathf.SmoothStep(0f, 1f, progress));
                smokeRect.localRotation = Quaternion.Euler(0f, 0f, progress * 28f);
                smokeImage.color = new Color(1f, 1f, 1f, alpha * 0.95f);
                yield return null;
            }

            Destroy(smokeObject);
        }

        private IEnumerator PlaySmokeWithScale(
            RectTransform parent,
            PrototypeCardView view,
            Vector3 smokePosition,
            Vector3 fromScale,
            Vector3 toScale,
            float smokeScale,
            bool fadeOut)
        {
            Coroutine smoke = StartCoroutine(PlaySmokePuff(parent, smokePosition, smokeScale));
            RectTransform rect = view.RectTransform;
            float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float pulse = Mathf.Sin(progress * Mathf.PI) * 0.12f;
                rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased) * (1f + pulse);
                view.SetAlpha(fadeOut ? 1f - eased : eased);
                yield return null;
            }

            rect.localScale = toScale;
            view.SetAlpha(fadeOut ? 0f : 1f);
            yield return smoke;
        }

        private static IEnumerator PlayAssassinDaggers(RectTransform parent, RectTransform target, Vector3 approachPosition)
        {
            Vector3 center = target.position;
            float approachSide = Mathf.Abs(approachPosition.x - center.x) < 0.001f
                ? 1f
                : Mathf.Sign(approachPosition.x - center.x);
            GameObject left = CreateOverlaySprite(
                parent,
                "Assassin Left Dagger",
                LoadAssassinDaggerLeftSprite(),
                new Vector2(168f, 168f),
                out RectTransform leftRect,
                out Image leftImage);
            GameObject right = CreateOverlaySprite(
                parent,
                "Assassin Right Dagger",
                LoadAssassinDaggerRightSprite(),
                new Vector2(168f, 168f),
                out RectTransform rightRect,
                out Image rightImage);

            Vector3 leftStart = center + new Vector3(approachSide * 104f, 86f, 0f);
            Vector3 rightStart = center + new Vector3(approachSide * 104f, -64f, 0f);
            Vector3 leftEnd = center + new Vector3(-approachSide * 22f, -24f, 0f);
            Vector3 rightEnd = center + new Vector3(-approachSide * 22f, 28f, 0f);
            float attackRotation = -90f * approachSide;
            float duration = 0.34f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float alpha = Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 6f));
                leftRect.position = Vector3.LerpUnclamped(leftStart, leftEnd, eased);
                rightRect.position = Vector3.LerpUnclamped(rightStart, rightEnd, eased);
                leftRect.localRotation = Quaternion.Euler(0f, 0f, attackRotation);
                rightRect.localRotation = Quaternion.Euler(0f, 0f, attackRotation);
                float scale = Mathf.Lerp(0.96f, 1.36f, Mathf.Sin(progress * Mathf.PI));
                leftRect.localScale = Vector3.one * scale;
                rightRect.localScale = Vector3.one * scale;
                leftImage.color = new Color(1f, 1f, 1f, alpha);
                rightImage.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }

            Destroy(left);
            Destroy(right);
        }

        private static GameObject CreateOverlaySprite(
            RectTransform parent,
            string objectName,
            Sprite sprite,
            Vector2 size,
            out RectTransform rect,
            out Image image)
        {
            GameObject obj = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasRenderer),
                typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.transform.SetAsLastSibling();

            Canvas canvas = obj.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;

            rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            return obj;
        }

        private static GameObject CreateTargetLineSegment(
            RectTransform parent,
            string objectName,
            out RectTransform rect,
            out Image image)
        {
            GameObject obj = CreateOverlaySprite(
                parent,
                objectName,
                LoadTargetLineSprite(),
                new Vector2(1f, 1f),
                out rect,
                out image);
            image.preserveAspect = false;
            return obj;
        }

        private static Sprite LoadTargetLineSprite()
        {
            if (targetLineSprite != null)
                return targetLineSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Target Line Pixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            targetLineSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            targetLineSprite.name = "Runtime Target Line Sprite";
            return targetLineSprite;
        }

        private static Vector3 BehindTargetPoint(RectTransform target, Vector3 attackerHomePosition)
        {
            Vector3 center = target.position;
            Vector3 directionFromAttacker = center - attackerHomePosition;
            float side = Mathf.Abs(directionFromAttacker.x) < 0.001f ? 1f : Mathf.Sign(directionFromAttacker.x);
            target.GetWorldCorners(worldCorners);
            float halfWidth = Vector3.Distance(worldCorners[0], worldCorners[3]) * 0.58f;
            return center + new Vector3(side * halfWidth, 18f, 0f);
        }

        private static RectTransform ResolveProjectileParent(PrototypeCardView attacker)
        {
            Canvas canvas = attacker != null ? attacker.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.transform is RectTransform canvasRect)
                return canvasRect;

            RectTransform root = attacker != null ? attacker.RectTransform.root as RectTransform : null;
            if (root != null)
                return root;

            return attacker != null ? attacker.RectTransform.parent as RectTransform : null;
        }

        private static GameObject CreateHunterArrowProjectile(
            RectTransform parent,
            out RectTransform arrowRect,
            out Image arrowImage)
        {
            GameObject arrowObject = new GameObject(
                "Hunter Arrow Projectile",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasRenderer),
                typeof(Image));
            arrowObject.transform.SetParent(parent, false);
            arrowObject.transform.SetAsLastSibling();

            Canvas arrowCanvas = arrowObject.GetComponent<Canvas>();
            arrowCanvas.overrideSorting = true;
            arrowCanvas.sortingOrder = 32000;

            arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowImage = arrowObject.GetComponent<Image>();
            arrowImage.sprite = LoadHunterArrowSprite();
            arrowImage.preserveAspect = true;
            arrowImage.raycastTarget = false;
            arrowImage.color = new Color(1f, 1f, 1f, 0f);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(240f, 60f);
            return arrowObject;
        }

        private static GameObject CreateHunterArrowTrail(
            RectTransform parent,
            out RectTransform trailRect,
            out Image trailImage)
        {
            GameObject trailObject = CreateOverlaySprite(
                parent,
                "Hunter Arrow Trail",
                LoadTargetLineSprite(),
                new Vector2(1f, 1f),
                out trailRect,
                out trailImage);
            trailImage.preserveAspect = false;
            trailImage.color = new Color(1f, 0.62f, 0.16f, 0f);
            return trailObject;
        }

        private static void UpdateHunterArrowTrail(
            RectTransform trailRect,
            Image trailImage,
            Vector3 headPosition,
            Vector3 direction,
            float angle,
            float progress,
            float headAlpha,
            float scale)
        {
            if (trailRect == null || trailImage == null)
                return;

            float pulse = Mathf.Lerp(0.72f, 1.12f, Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI));
            trailRect.position = headPosition - direction * Mathf.Lerp(70f, 124f, pulse);
            trailRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            trailRect.sizeDelta = new Vector2(Mathf.Lerp(116f, 210f, pulse) * scale, Mathf.Lerp(8f, 18f, pulse) * scale);
            trailRect.localScale = Vector3.one;
            trailImage.color = new Color(1f, 0.56f, 0.1f, headAlpha * 0.54f * scale);
        }

        private static GameObject CreateMageArcaneProjectile(
            RectTransform parent,
            out RectTransform boltRect,
            out Image boltImage)
        {
            GameObject boltObject = CreateOverlaySprite(
                parent,
                "Mage Arcane Bolt",
                LoadMageProjectileSprite(),
                new Vector2(220f, 220f),
                out boltRect,
                out boltImage);
            boltImage.color = new Color(0.76f, 0.58f, 1f, 0f);
            return boltObject;
        }

        private static GameObject CreateMageArcaneTrail(
            RectTransform parent,
            out RectTransform trailRect,
            out Image trailImage)
        {
            GameObject trailObject = CreateOverlaySprite(
                parent,
                "Mage Arcane Meteor Trail",
                LoadMageTrailSprite(),
                new Vector2(300f, 92f),
                out trailRect,
                out trailImage);
            trailImage.color = new Color(0.55f, 0.42f, 0.8f, 0f);
            return trailObject;
        }

        private static void UpdateArcaneTrail(
            RectTransform trailRect,
            Image trailImage,
            Vector3 headPosition,
            Vector3 direction,
            float angle,
            float progress,
            float headAlpha)
        {
            if (trailRect == null || trailImage == null)
                return;

            float length = Mathf.Lerp(0.72f, 1.24f, Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI));
            trailRect.position = headPosition - direction * Mathf.Lerp(68f, 118f, length);
            trailRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            trailRect.localScale = new Vector3(length, Mathf.Lerp(0.78f, 1.08f, length), 1f);
            trailImage.color = new Color(0.62f, 0.48f, 0.86f, headAlpha * 0.8f);
        }

        private static GameObject CreatePriestBeam(
            RectTransform parent,
            string objectName,
            out RectTransform beamRect,
            out Image beamImage)
        {
            GameObject beamObject = CreateOverlaySprite(
                parent,
                objectName,
                LoadPriestBeamSprite(),
                new Vector2(92f, 430f),
                out beamRect,
                out beamImage);
            beamImage.preserveAspect = false;
            beamImage.color = new Color(1f, 0.94f, 0.55f, 0f);
            return beamObject;
        }

        private static IEnumerator PlayPriestHitCross(RectTransform parent, Vector3 worldPosition)
        {
            GameObject cross = CreateOverlaySprite(
                parent,
                "Priest Sacred Hit Cross",
                LoadPriestCrossSprite(),
                new Vector2(176f, 176f),
                out RectTransform crossRect,
                out Image crossImage);
            crossRect.position = worldPosition;
            crossRect.localScale = Vector3.one * 0.18f;
            crossImage.color = new Color(1f, 0.95f, 0.58f, 0f);

            float duration = 0.34f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                crossRect.localScale = Vector3.one * Mathf.Lerp(0.28f, 1.55f, eased);
                crossRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI) * 4f);
                crossImage.color = new Color(1f, 0.96f, 0.62f, Mathf.Clamp01(Mathf.Min(progress * 10f, fade * 1.35f)));
                yield return null;
            }

            Destroy(cross);
        }

        private static IEnumerator PlayPriestHitMarker(RectTransform parent, Vector3 worldPosition, RectTransform target)
        {
            GameObject halo = CreateOverlaySprite(
                parent,
                "Priest Hit Marker Halo",
                LoadPriestCrossSprite(),
                new Vector2(230f, 230f),
                out RectTransform haloRect,
                out Image haloImage);
            GameObject core = CreateOverlaySprite(
                parent,
                "Priest Hit Marker Core",
                LoadPriestSparkSprite(),
                new Vector2(190f, 190f),
                out RectTransform coreRect,
                out Image coreImage);
            GameObject ring = CreateOverlaySprite(
                parent,
                "Priest Hit Marker Ring",
                LoadHunterExplosionRingSprite(),
                new Vector2(252f, 252f),
                out RectTransform ringRect,
                out Image ringImage);

            haloRect.position = worldPosition;
            coreRect.position = worldPosition;
            ringRect.position = worldPosition;
            haloRect.localScale = Vector3.one * 0.16f;
            coreRect.localScale = Vector3.one * 0.2f;
            ringRect.localScale = Vector3.one * 0.14f;
            haloImage.color = new Color(0.78f, 0.92f, 1f, 0f);
            coreImage.color = new Color(1f, 0.97f, 0.68f, 0f);
            ringImage.color = new Color(0.72f, 0.9f, 1f, 0f);

            int shardCount = 14;
            var shards = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float angle, float scale, float spin)>(shardCount);
            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = CreateOverlaySprite(
                    parent,
                    "Priest Hit Marker Shard",
                    LoadPriestSparkSprite(),
                    new Vector2(UnityEngine.Random.Range(28f, 46f), UnityEngine.Random.Range(52f, 78f)),
                    out RectTransform shardRect,
                    out Image shardImage);
                float angle = (Mathf.PI * 2f * i / shardCount) + UnityEngine.Random.Range(-0.18f, 0.18f);
                float distance = UnityEngine.Random.Range(58f, 132f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                shardRect.position = worldPosition;
                shardRect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                shardImage.color = new Color(0.86f, 0.94f, 1f, 0f);
                shards.Add((shard, shardRect, shardImage, offset, angle * Mathf.Rad2Deg - 90f, UnityEngine.Random.Range(0.62f, 1.08f), UnityEngine.Random.Range(-150f, 150f)));
            }

            Vector3 originalScale = target != null ? target.localScale : Vector3.one;
            Quaternion originalRotation = target != null ? target.localRotation : Quaternion.identity;
            float duration = 0.36f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                float flash = Mathf.Sin(progress * Mathf.PI);

                haloRect.position = worldPosition;
                haloRect.localScale = Vector3.one * Mathf.Lerp(0.22f, 1.34f, eased);
                haloRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI) * 5f);
                haloImage.color = new Color(0.78f, 0.92f, 1f, Mathf.Clamp01(Mathf.Min(progress * 12f, fade * 1.2f)));

                coreRect.position = worldPosition;
                coreRect.localScale = Vector3.one * Mathf.Lerp(0.24f, 1.12f, flash);
                coreRect.localRotation = Quaternion.Euler(0f, 0f, progress * 72f);
                coreImage.color = new Color(1f, 0.97f, 0.68f, Mathf.Clamp01(Mathf.Min(progress * 14f, fade * 1.45f)));

                ringRect.position = worldPosition;
                ringRect.localScale = Vector3.one * Mathf.Lerp(0.18f, 1.28f, eased);
                ringRect.localRotation = Quaternion.Euler(0f, 0f, progress * -28f);
                ringImage.color = new Color(0.72f, 0.9f, 1f, Mathf.Clamp01(Mathf.Min(progress * 14f, fade * 1.32f)));

                if (target != null)
                {
                    target.localScale = originalScale * (1f + flash * 0.11f);
                    target.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 2f) * 3.5f);
                }

                foreach (var shard in shards)
                {
                    shard.rect.position = worldPosition + shard.offset * eased;
                    shard.rect.localRotation = Quaternion.Euler(0f, 0f, shard.angle + shard.spin * progress);
                    shard.rect.localScale = Vector3.one * Mathf.Lerp(0.24f, shard.scale, flash);
                    shard.image.color = new Color(0.86f, 0.94f, 1f, Mathf.Clamp01(Mathf.Min(progress * 13f, fade * 1.2f)));
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = originalScale;
                target.localRotation = originalRotation;
            }

            Destroy(halo);
            Destroy(core);
            Destroy(ring);
            foreach (var shard in shards)
                Destroy(shard.obj);
        }

        private static IEnumerator PlayHolySparkScatter(RectTransform parent, Vector3 worldPosition, bool blocked)
        {
            int particleCount = blocked ? 18 : 10;
            var particles = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float spin, float scale)>(particleCount);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = CreateOverlaySprite(
                    parent,
                    blocked ? "Priest Dispersed Feather" : "Priest Sacred Spark",
                    LoadPriestSparkSprite(),
                    new Vector2(blocked ? 54f : 38f, blocked ? 72f : 38f),
                    out RectTransform particleRect,
                    out Image particleImage);
                float angle = blocked
                    ? Mathf.Lerp(Mathf.PI * 0.1f, Mathf.PI * 0.9f, i / Mathf.Max(1f, particleCount - 1f)) + UnityEngine.Random.Range(-0.18f, 0.18f)
                    : (Mathf.PI * 2f * i / particleCount) + UnityEngine.Random.Range(-0.2f, 0.2f);
                float distance = UnityEngine.Random.Range(blocked ? 74f : 34f, blocked ? 170f : 92f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                if (blocked)
                    offset.y = Mathf.Abs(offset.y) * 0.72f + UnityEngine.Random.Range(-8f, 22f);
                particleRect.position = worldPosition;
                particleRect.localScale = Vector3.one * UnityEngine.Random.Range(0.58f, 1.08f);
                particleImage.color = new Color(1f, 0.95f, 0.72f, 0f);
                particles.Add((particle, particleRect, particleImage, offset, UnityEngine.Random.Range(-180f, 180f), UnityEngine.Random.Range(0.76f, 1.18f)));
            }

            float duration = blocked ? 0.54f : 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                for (int i = 0; i < particles.Count; i++)
                {
                    var particle = particles[i];
                    Vector3 drift = blocked
                        ? new Vector3(Mathf.Sin((progress + i) * Mathf.PI) * 18f, -progress * 22f, 0f)
                        : Vector3.zero;
                    particle.rect.position = worldPosition + particle.offset * eased + drift;
                    particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.spin * progress);
                    particle.rect.localScale = Vector3.one * Mathf.Lerp(particle.scale, particle.scale * 0.42f, eased);
                    particle.image.color = new Color(1f, 0.97f, 0.78f, Mathf.Clamp01(Mathf.Min(progress * 9f, fade * 1.25f)));
                }
                yield return null;
            }

            foreach (var particle in particles)
                Destroy(particle.obj);
        }

        private static IEnumerator PlayArcaneImpact(RectTransform parent, Vector3 worldPosition, bool blocked)
        {
            GameObject burstObject = CreateOverlaySprite(
                parent,
                blocked ? "Mage Arcane Barrier Break" : "Mage Arcane Impact",
                LoadMageProjectileSprite(),
                new Vector2(blocked ? 260f : 210f, blocked ? 260f : 210f),
                out RectTransform burstRect,
                out Image burstImage);
            burstRect.position = worldPosition;

            int particleCount = blocked ? 22 : 11;
            var particles = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float spin)>(particleCount);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = CreateOverlaySprite(
                    parent,
                    blocked ? "Mage Arcane Barrier Shard" : "Mage Arcane Particle",
                    LoadMageParticleSprite(),
                    new Vector2(blocked ? 62f : 46f, blocked ? 62f : 46f),
                    out RectTransform particleRect,
                    out Image particleImage);
                float angle = blocked
                    ? Mathf.Lerp(Mathf.PI * 0.08f, Mathf.PI * 0.92f, i / Mathf.Max(1f, particleCount - 1f)) + UnityEngine.Random.Range(-0.22f, 0.22f)
                    : (Mathf.PI * 2f * i / particleCount) + UnityEngine.Random.Range(-0.18f, 0.18f);
                float distance = UnityEngine.Random.Range(blocked ? 98f : 46f, blocked ? 210f : 104f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                if (blocked)
                    offset.y = Mathf.Abs(offset.y) * 0.9f + UnityEngine.Random.Range(-18f, 34f);
                particleRect.position = worldPosition;
                particleRect.localScale = Vector3.one * UnityEngine.Random.Range(blocked ? 0.74f : 0.62f, blocked ? 1.34f : 1.08f);
                particleImage.color = new Color(0.72f, 0.56f, 1f, 0f);
                particles.Add((particle, particleRect, particleImage, offset, UnityEngine.Random.Range(-360f, 360f)));
            }

            float duration = blocked ? 0.58f : 0.34f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                burstRect.localScale = Vector3.one * Mathf.Lerp(blocked ? 0.5f : 0.34f, blocked ? 1.8f : 1.24f, eased);
                burstRect.localRotation = Quaternion.Euler(0f, 0f, progress * (blocked ? 128f : 42f));
                burstImage.color = new Color(0.68f, 0.5f, 1f, Mathf.Clamp01(Mathf.Min(progress * 9f, fade * (blocked ? 1.1f : 0.92f))));

                for (int i = 0; i < particles.Count; i++)
                {
                    var particle = particles[i];
                    Vector3 drift = blocked
                        ? new Vector3(Mathf.Sin((progress + i * 0.17f) * Mathf.PI) * 16f, -progress * 28f, 0f)
                        : Vector3.zero;
                    particle.rect.position = worldPosition + particle.offset * eased + drift;
                    particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.spin * progress);
                    particle.rect.localScale *= blocked ? 0.988f : 0.992f;
                    particle.image.color = new Color(0.74f, 0.58f, 1f, Mathf.Clamp01(Mathf.Min(progress * 11f, fade * (blocked ? 1.35f : 1.18f))));
                }
                yield return null;
            }

            Destroy(burstObject);
            foreach (var particle in particles)
                Destroy(particle.obj);
        }

        private static IEnumerator PlayImpactPulse(RectTransform target)
        {
            if (target == null)
                yield break;

            Vector3 originalScale = target.localScale;
            Quaternion originalRotation = target.localRotation;
            float duration = 0.18f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float punch = Mathf.Sin(progress * Mathf.PI);
                target.localScale = originalScale * (1f + punch * 0.08f);
                target.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 2f) * 2.5f);
                yield return null;
            }

            target.localScale = originalScale;
            target.localRotation = originalRotation;
        }

        private static IEnumerator PlayHunterHitExplosion(RectTransform parent, Vector3 worldPosition, RectTransform target)
        {
            GameObject smoke = CreateOverlaySprite(
                parent,
                "Hunter Impact Smoke Bloom",
                LoadHunterExplosionSmokeSprite(),
                new Vector2(300f, 300f),
                out RectTransform smokeRect,
                out Image smokeImage);
            GameObject core = CreateOverlaySprite(
                parent,
                "Hunter Impact Fire Burst",
                LoadHunterExplosionCoreSprite(),
                new Vector2(230f, 230f),
                out RectTransform coreRect,
                out Image coreImage);
            GameObject ring = CreateOverlaySprite(
                parent,
                "Hunter Impact Explosion Ring",
                LoadHunterExplosionRingSprite(),
                new Vector2(250f, 250f),
                out RectTransform ringRect,
                out Image ringImage);
            smokeRect.position = worldPosition;
            coreRect.position = worldPosition;
            ringRect.position = worldPosition;
            smokeRect.localScale = Vector3.one * 0.18f;
            coreRect.localScale = Vector3.one * 0.22f;
            ringRect.localScale = Vector3.one * 0.16f;
            smokeImage.color = new Color(0.38f, 0.28f, 0.18f, 0f);
            coreImage.color = new Color(1f, 0.72f, 0.18f, 0f);
            ringImage.color = new Color(1f, 0.82f, 0.36f, 0f);

            int shardCount = 18;
            var shards = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float angle, float scale, float spin)>(shardCount);
            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = CreateOverlaySprite(
                    parent,
                    "Hunter Impact Ember",
                    LoadHunterExplosionEmberSprite(),
                    new Vector2(UnityEngine.Random.Range(34f, 58f), UnityEngine.Random.Range(58f, 92f)),
                    out RectTransform shardRect,
                    out Image shardImage);
                float angle = (Mathf.PI * 2f * i / shardCount) + UnityEngine.Random.Range(-0.22f, 0.22f);
                float distance = UnityEngine.Random.Range(72f, 158f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                shardRect.position = worldPosition;
                shardRect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                shardImage.color = new Color(1f, 0.48f, 0.06f, 0f);
                shards.Add((shard, shardRect, shardImage, offset, angle * Mathf.Rad2Deg - 90f, UnityEngine.Random.Range(0.55f, 1.15f), UnityEngine.Random.Range(-180f, 180f)));
            }

            Vector3 originalScale = target != null ? target.localScale : Vector3.one;
            Quaternion originalRotation = target != null ? target.localRotation : Quaternion.identity;
            float duration = 0.46f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                float flash = Mathf.Sin(progress * Mathf.PI);

                smokeRect.position = worldPosition + Vector3.up * Mathf.Lerp(0f, 18f, eased);
                smokeRect.localScale = Vector3.one * Mathf.Lerp(0.26f, 1.55f, eased);
                smokeRect.localRotation = Quaternion.Euler(0f, 0f, progress * -18f);
                smokeImage.color = new Color(0.45f, 0.33f, 0.22f, Mathf.Clamp01(Mathf.Min(progress * 5f, fade * 0.52f)));

                coreRect.position = worldPosition;
                coreRect.localScale = Vector3.one * Mathf.Lerp(0.28f, 1.18f, Mathf.Sin(Mathf.Clamp01(progress * 1.25f) * Mathf.PI));
                coreRect.localRotation = Quaternion.Euler(0f, 0f, progress * 46f);
                coreImage.color = new Color(1f, 0.66f, 0.12f, Mathf.Clamp01(Mathf.Min(progress * 9f, fade * 1.35f)));

                ringRect.position = worldPosition;
                ringRect.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.42f, eased);
                ringRect.localRotation = Quaternion.Euler(0f, 0f, progress * 34f);
                ringImage.color = new Color(1f, 0.72f, 0.18f, Mathf.Clamp01(Mathf.Min(progress * 12f, fade * 1.45f)));

                if (target != null)
                {
                    target.localScale = originalScale * (1f + flash * 0.1f);
                    target.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 2f) * 3.2f);
                }

                foreach (var shard in shards)
                {
                    Vector3 gravity = Vector3.down * (progress * progress * 32f);
                    shard.rect.position = worldPosition + shard.offset * eased + gravity;
                    shard.rect.localRotation = Quaternion.Euler(0f, 0f, shard.angle + shard.spin * progress);
                    shard.rect.localScale = Vector3.one * Mathf.Lerp(0.28f, shard.scale, flash);
                    shard.image.color = new Color(1f, 0.5f, 0.06f, Mathf.Clamp01(Mathf.Min(progress * 11f, fade * 1.28f)));
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = originalScale;
                target.localRotation = originalRotation;
            }

            Destroy(smoke);
            Destroy(core);
            Destroy(ring);
            foreach (var shard in shards)
                Destroy(shard.obj);
        }

        private static Vector3 EdgePoint(RectTransform source, Vector3 toward)
        {
            Vector3 center = source.position;
            Vector3 direction = toward - center;
            if (direction.sqrMagnitude < 0.001f)
                return center;

            source.GetWorldCorners(worldCorners);
            float halfWidth = Vector3.Distance(worldCorners[0], worldCorners[3]) * 0.42f;
            float halfHeight = Vector3.Distance(worldCorners[0], worldCorners[1]) * 0.42f;
            Vector3 normalized = direction.normalized;
            return center + new Vector3(normalized.x * halfWidth, normalized.y * halfHeight, 0f);
        }

        private static readonly Vector3[] worldCorners = new Vector3[4];

        private static Sprite LoadMedusaStoneConeSprite()
        {
            if (medusaStoneConeSprite != null)
                return medusaStoneConeSprite;

            const int width = 256;
            const int height = 192;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = Mathf.Abs((y - (height - 1) * 0.5f) / ((height - 1) * 0.5f));
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float allowed = Mathf.Clamp01(1f - ny / Mathf.Lerp(0.12f, 1.02f, nx));
                    float front = Mathf.SmoothStep(0f, 1f, nx);
                    float grain = Mathf.PerlinNoise(x * 0.055f, y * 0.055f);
                    float vein = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin((x * 0.035f) + (y * 0.08f))) * 7f) * 0.35f;
                    float alpha = allowed * front * (0.34f + grain * 0.42f + vein);
                    byte shade = (byte)Mathf.Lerp(122f, 220f, Mathf.Clamp01(grain + vein));
                    pixels[y * width + x] = new Color32(shade, shade, (byte)Mathf.Clamp(shade + 8, 0, 255), (byte)Mathf.Clamp(alpha * 230f, 0f, 210f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            medusaStoneConeSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0f, 0.5f), 100f);
            medusaStoneConeSprite.name = "Medusa Stone Cone";
            medusaStoneConeSprite.hideFlags = HideFlags.HideAndDontSave;
            return medusaStoneConeSprite;
        }

        private static Sprite LoadMedusaStoneShardSprite()
        {
            if (medusaStoneShardSprite != null)
                return medusaStoneShardSprite;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - 31.5f) / 31.5f;
                    float ny = (y - 31.5f) / 31.5f;
                    float diamond = Mathf.Clamp01(1f - (Mathf.Abs(nx * 0.82f + ny * 0.22f) + Mathf.Abs(ny * 1.12f - nx * 0.18f)));
                    float cut = Mathf.Clamp01(1f - Mathf.Abs(nx - ny * 0.45f) * 5.8f) * diamond;
                    float highlight = Mathf.Clamp01(1f - Mathf.Sqrt((nx + 0.22f) * (nx + 0.22f) * 5f + (ny + 0.28f) * (ny + 0.28f) * 7f));
                    float alpha = Mathf.Clamp01(diamond * 1.2f);
                    byte shade = (byte)Mathf.Lerp(112f, 238f, Mathf.Clamp01(cut + highlight * 0.65f));
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 245f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            medusaStoneShardSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            medusaStoneShardSprite.name = "Medusa Stone Shard";
            medusaStoneShardSprite.hideFlags = HideFlags.HideAndDontSave;
            return medusaStoneShardSprite;
        }

        private static Sprite LoadMedusaStoneCrackSprite()
        {
            if (medusaStoneCrackSprite != null)
                return medusaStoneCrackSprite;

            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - 63.5f) / 63.5f;
                    float ny = (y - 63.5f) / 63.5f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float main = Mathf.Clamp01(1f - Mathf.Abs(nx + Mathf.Sin(ny * 12f) * 0.08f) * 22f)
                        * Mathf.Clamp01(1f - Mathf.Abs(ny) * 0.85f);
                    float branchA = Mathf.Clamp01(1f - Mathf.Abs(nx + ny * 0.78f + 0.16f) * 25f)
                        * Mathf.Clamp01(1f - radius * 1.15f);
                    float branchB = Mathf.Clamp01(1f - Mathf.Abs(nx - ny * 0.62f - 0.18f) * 25f)
                        * Mathf.Clamp01(1f - radius * 1.12f);
                    float dust = Mathf.Clamp01(1f - radius) * 0.18f;
                    float alpha = Mathf.Clamp01(main + branchA * 0.78f + branchB * 0.78f + dust);
                    pixels[y * size + x] = new Color32(226, 230, 228, (byte)(alpha * 230f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            medusaStoneCrackSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            medusaStoneCrackSprite.name = "Medusa Stone Crack";
            medusaStoneCrackSprite.hideFlags = HideFlags.HideAndDontSave;
            return medusaStoneCrackSprite;
        }

        private static Sprite LoadMedusaGhostSnakeSprite()
        {
            if (medusaGhostSnakeSprite != null)
                return medusaGhostSnakeSprite;

            const int width = 192;
            const int height = 72;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = x / (float)(width - 1);
                    float centerY = height * (0.48f + Mathf.Sin(t * Mathf.PI * 4.15f) * 0.16f);
                    float dy = Mathf.Abs(y - centerY);
                    float bodyRadius = Mathf.Lerp(9.5f, 6.2f, t);
                    float head = Mathf.SmoothStep(0.78f, 0.98f, t);
                    float radius = Mathf.Lerp(bodyRadius, 17.5f, head);
                    float body = Mathf.Clamp01(1f - dy / radius);
                    float taper = Mathf.SmoothStep(0.02f, 0.13f, t) * Mathf.SmoothStep(1.02f, 0.82f, t);
                    float ghost = body * taper;
                    float stripe = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(t * Mathf.PI * 18f + dy * 0.13f)) * 4.2f) * ghost * 0.34f;
                    float jaw = head * Mathf.Clamp01(1f - Mathf.Abs((y - centerY) - 8f) / 3.2f) * Mathf.SmoothStep(0.88f, 1f, t);
                    float eye = head * Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(width * 0.9f, centerY + 6f)) / 4.2f);
                    float alpha = Mathf.Clamp01(ghost * 0.82f + stripe * 0.5f + jaw * 0.55f + eye);
                    float shadeValue = Mathf.Clamp01(0.46f + ghost * 0.38f + stripe * 0.22f + eye * 0.45f);
                    byte shade = (byte)Mathf.Lerp(86f, 226f, shadeValue);
                    pixels[y * width + x] = new Color32(shade, shade, (byte)Mathf.Clamp(shade + 8, 0, 255), (byte)(alpha * 230f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            medusaGhostSnakeSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.1f, 0.5f), 100f);
            medusaGhostSnakeSprite.name = "Medusa Ghost Snake";
            medusaGhostSnakeSprite.hideFlags = HideFlags.HideAndDontSave;
            return medusaGhostSnakeSprite;
        }

        private static Sprite LoadTrentorVineSprite()
        {
            if (trentorVineSprite != null)
                return trentorVineSprite;

            trentorVineSprite = Resources.Load<Sprite>("UI/trentor_living_vine");
            if (trentorVineSprite != null)
                return trentorVineSprite;

            const int width = 256;
            const int height = 42;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float wave = Mathf.Sin(x * 0.11f) * 5.5f + Mathf.Sin(x * 0.031f) * 4f;
                    float distance = Mathf.Abs(y - height * 0.5f - wave);
                    float core = Mathf.Clamp01(1f - distance / 8.5f);
                    float bark = Mathf.Clamp01(1f - distance / 14f);
                    float vein = Mathf.Abs(Mathf.Sin(x * 0.22f + y * 0.4f));
                    if (bark <= 0f)
                    {
                        pixels[y * width + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    Color color = Color.Lerp(new Color(0.08f, 0.28f, 0.07f, bark * 0.82f), new Color(0.38f, 0.9f, 0.18f, 0.95f), core);
                    color = Color.Lerp(color, new Color(0.72f, 0.48f, 0.18f, color.a), vein * core * 0.22f);
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            trentorVineSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            trentorVineSprite.name = "Trentor Living Vine";
            trentorVineSprite.hideFlags = HideFlags.HideAndDontSave;
            return trentorVineSprite;
        }

        private static Sprite LoadTrentorLeafSprite()
        {
            if (trentorLeafSprite != null)
                return trentorLeafSprite;

            trentorLeafSprite = Resources.Load<Sprite>("UI/trentor_thorn_leaf");
            if (trentorLeafSprite != null)
                return trentorLeafSprite;

            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float nx = p.x / 18f;
                    float ny = p.y / 38f;
                    float leaf = Mathf.Clamp01(1f - (nx * nx + Mathf.Abs(ny) * 0.9f));
                    float point = Mathf.Clamp01((p.y + 38f) / 76f);
                    float alpha = leaf * point;
                    Color color = alpha > 0f
                        ? Color.Lerp(new Color(0.2f, 0.62f, 0.12f, alpha), new Color(0.78f, 1f, 0.34f, alpha), Mathf.Clamp01(p.y / 42f + 0.45f))
                        : Color.clear;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            trentorLeafSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            trentorLeafSprite.name = "Trentor Thorn Leaf";
            trentorLeafSprite.hideFlags = HideFlags.HideAndDontSave;
            return trentorLeafSprite;
        }

        private static Sprite LoadHunterArrowSprite()
        {
            if (hunterArrowSprite != null)
                return hunterArrowSprite;

            hunterArrowSprite = Resources.Load<Sprite>("UI/hunter_arrow_mmorpg");
            if (hunterArrowSprite != null)
                return hunterArrowSprite;

            Texture2D texture = new Texture2D(64, 16, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[64 * 16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            for (int y = 6; y <= 9; y++)
            {
                for (int x = 6; x < 52; x++)
                    pixels[y * 64 + x] = new Color32(220, 180, 80, 255);
            }
            for (int y = 2; y <= 13; y++)
            {
                int span = Mathf.Abs(y - 8);
                for (int x = 48; x < 62 - span; x++)
                    pixels[y * 64 + x] = new Color32(180, 230, 210, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hunterArrowSprite = Sprite.Create(texture, new Rect(0, 0, 64, 16), new Vector2(0.5f, 0.5f), 100f);
            hunterArrowSprite.hideFlags = HideFlags.HideAndDontSave;
            return hunterArrowSprite;
        }

        private static Sprite LoadHunterMarkReticleSprite()
        {
            if (hunterMarkReticleSprite != null)
                return hunterMarkReticleSprite;

            hunterMarkReticleSprite = Resources.Load<Sprite>("UI/hunter_sniper_reticle");
            if (hunterMarkReticleSprite != null)
                return hunterMarkReticleSprite;

            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float distance = Vector2.Distance(point, center);
                    bool ring = Mathf.Abs(distance - 24f) < 1.4f || Mathf.Abs(distance - 12f) < 1.1f;
                    bool cross = (Mathf.Abs(x - 32f) < 1.4f && (y < 16 || y > 48 || (y > 25 && y < 39)))
                        || (Mathf.Abs(y - 32f) < 1.4f && (x < 16 || x > 48 || (x > 25 && x < 39)));
                    if (ring || cross)
                        pixels[y * 64 + x] = new Color32(255, 126, 24, 235);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hunterMarkReticleSprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            hunterMarkReticleSprite.hideFlags = HideFlags.HideAndDontSave;
            return hunterMarkReticleSprite;
        }

        private static Sprite LoadHunterExplosionCoreSprite()
        {
            if (hunterExplosionCoreSprite != null)
                return hunterExplosionCoreSprite;

            hunterExplosionCoreSprite = Resources.Load<Sprite>("UI/hunter_fire_burst");
            if (hunterExplosionCoreSprite != null)
                return hunterExplosionCoreSprite;

            const int size = 128;
            Texture2D texture = NewRuntimeTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    Vector2 delta = (point - center) / (size * 0.5f);
                    float radius = delta.magnitude;
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float flame = Mathf.Sin(angle * 8f + radius * 10f) * 0.08f
                        + Mathf.Sin(angle * 15f - radius * 7f) * 0.045f;
                    float edge = Mathf.Clamp01(1f - (radius - flame) / 0.96f);
                    float hotCore = Mathf.Clamp01(1f - radius * 2.2f);
                    float alpha = Mathf.SmoothStep(0f, 1f, edge) * Mathf.Clamp01(1.05f - radius);
                    byte r = 255;
                    byte g = (byte)Mathf.Lerp(72f, 238f, hotCore);
                    byte b = (byte)Mathf.Lerp(8f, 96f, hotCore);
                    pixels[y * size + x] = new Color32(r, g, b, (byte)Mathf.Clamp(alpha * 245f, 0f, 245f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hunterExplosionCoreSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            hunterExplosionCoreSprite.name = "Hunter Fire Burst Fallback";
            hunterExplosionCoreSprite.hideFlags = HideFlags.HideAndDontSave;
            return hunterExplosionCoreSprite;
        }

        private static Sprite LoadHunterExplosionRingSprite()
        {
            if (hunterExplosionRingSprite != null)
                return hunterExplosionRingSprite;

            hunterExplosionRingSprite = Resources.Load<Sprite>("UI/hunter_shockwave_ring");
            if (hunterExplosionRingSprite != null)
                return hunterExplosionRingSprite;

            const int size = 128;
            Texture2D texture = NewRuntimeTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = (new Vector2(x, y) - center) / (size * 0.5f);
                    float radius = delta.magnitude;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.62f) / 0.09f);
                    float innerGlow = Mathf.Clamp01(1f - radius / 0.82f) * 0.18f;
                    float alpha = Mathf.Clamp01(ring + innerGlow) * Mathf.Clamp01(1f - Mathf.Max(0f, radius - 0.98f) * 10f);
                    pixels[y * size + x] = new Color32(255, 204, 74, (byte)Mathf.Clamp(alpha * 230f, 0f, 230f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hunterExplosionRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            hunterExplosionRingSprite.name = "Hunter Shockwave Ring Fallback";
            hunterExplosionRingSprite.hideFlags = HideFlags.HideAndDontSave;
            return hunterExplosionRingSprite;
        }

        private static Sprite LoadHunterExplosionEmberSprite()
        {
            if (hunterExplosionEmberSprite != null)
                return hunterExplosionEmberSprite;

            hunterExplosionEmberSprite = Resources.Load<Sprite>("UI/hunter_impact_ember");
            if (hunterExplosionEmberSprite != null)
                return hunterExplosionEmberSprite;

            const int width = 48;
            const int height = 96;
            Texture2D texture = NewRuntimeTexture(width, height);
            Color32[] pixels = new Color32[width * height];
            Vector2 center = new Vector2((width - 1) * 0.5f, height * 0.62f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - center.x) / (width * 0.5f);
                    float ny = (y - center.y) / (height * 0.5f);
                    float taper = Mathf.Lerp(0.28f, 1f, Mathf.Clamp01((height - y) / (float)height));
                    float body = Mathf.Clamp01(1f - Mathf.Sqrt((nx * nx) / Mathf.Max(0.12f, taper) + ny * ny * 1.9f));
                    float tip = Mathf.Clamp01(1f - Mathf.Abs(nx) * 5f) * Mathf.SmoothStep(0.44f, 1f, y / (float)(height - 1));
                    float alpha = Mathf.Clamp01(body * 1.25f + tip * 0.48f);
                    float heat = Mathf.Clamp01(body * 1.8f + tip);
                    pixels[y * width + x] = new Color32(255, (byte)Mathf.Lerp(80f, 224f, heat), 18, (byte)(alpha * 235f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hunterExplosionEmberSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.35f), 100f);
            hunterExplosionEmberSprite.name = "Hunter Impact Ember Fallback";
            hunterExplosionEmberSprite.hideFlags = HideFlags.HideAndDontSave;
            return hunterExplosionEmberSprite;
        }

        private static Sprite LoadHunterExplosionSmokeSprite()
        {
            if (hunterExplosionSmokeSprite != null)
                return hunterExplosionSmokeSprite;

            hunterExplosionSmokeSprite = Resources.Load<Sprite>("UI/hunter_impact_smoke");
            if (hunterExplosionSmokeSprite != null)
                return hunterExplosionSmokeSprite;

            const int size = 128;
            Texture2D texture = NewRuntimeTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.52f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = (new Vector2(x, y) - center) / (size * 0.5f);
                    float radius = delta.magnitude;
                    float noise = Mathf.PerlinNoise(x * 0.055f, y * 0.055f);
                    float cloud = Mathf.Clamp01(1f - radius / Mathf.Lerp(0.74f, 1.06f, noise));
                    float alpha = Mathf.SmoothStep(0f, 1f, cloud) * 0.72f;
                    byte shade = (byte)Mathf.Lerp(72f, 142f, noise);
                    pixels[y * size + x] = new Color32(shade, (byte)Mathf.Lerp(56f, 104f, noise), (byte)Mathf.Lerp(42f, 74f, noise), (byte)(alpha * 190f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hunterExplosionSmokeSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            hunterExplosionSmokeSprite.name = "Hunter Impact Smoke Fallback";
            hunterExplosionSmokeSprite.hideFlags = HideFlags.HideAndDontSave;
            return hunterExplosionSmokeSprite;
        }

        private static Texture2D NewRuntimeTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private static Sprite LoadMageProjectileSprite()
        {
            if (mageProjectileSprite != null)
                return mageProjectileSprite;

            mageProjectileSprite = Resources.Load<Sprite>("UI/mage_arcane_bolt");
            if (mageProjectileSprite != null)
                return mageProjectileSprite;

            return mageProjectileSprite = CreateFallbackArcaneGlyphSprite("Mage Arcane Bolt Fallback");
        }

        private static Sprite LoadMageParticleSprite()
        {
            if (mageParticleSprite != null)
                return mageParticleSprite;

            mageParticleSprite = Resources.Load<Sprite>("UI/mage_arcane_particle");
            if (mageParticleSprite != null)
                return mageParticleSprite;

            return mageParticleSprite = CreateFallbackArcaneParticleSprite("Mage Arcane Particle Fallback");
        }

        private static Sprite LoadMageTrailSprite()
        {
            if (mageTrailSprite != null)
                return mageTrailSprite;

            mageTrailSprite = Resources.Load<Sprite>("UI/mage_arcane_trail");
            if (mageTrailSprite != null)
                return mageTrailSprite;

            return mageTrailSprite = CreateFallbackArcaneTrailSprite("Mage Arcane Trail Fallback");
        }

        private static Sprite LoadPriestBeamSprite()
        {
            if (priestBeamSprite != null)
                return priestBeamSprite;

            priestBeamSprite = Resources.Load<Sprite>("UI/priest_sacred_beam");
            if (priestBeamSprite != null)
                return priestBeamSprite;

            return priestBeamSprite = CreateFallbackPriestBeamSprite("Priest Sacred Beam Fallback");
        }

        private static Sprite LoadPriestSparkSprite()
        {
            if (priestSparkSprite != null)
                return priestSparkSprite;

            priestSparkSprite = Resources.Load<Sprite>("UI/priest_sacred_spark");
            if (priestSparkSprite != null)
                return priestSparkSprite;

            return priestSparkSprite = CreateFallbackPriestSparkSprite("Priest Sacred Spark Fallback");
        }

        private static Sprite LoadPriestCrossSprite()
        {
            if (priestCrossSprite != null)
                return priestCrossSprite;

            priestCrossSprite = Resources.Load<Sprite>("UI/priest_sacred_cross");
            if (priestCrossSprite != null)
                return priestCrossSprite;

            return priestCrossSprite = CreateFallbackPriestCrossSprite("Priest Sacred Cross Fallback");
        }

        private static Sprite LoadPaladinShieldSprite()
        {
            if (paladinShieldSprite != null)
                return paladinShieldSprite;

            paladinShieldSprite = Resources.Load<Sprite>("UI/paladin_divine_shield");
            if (paladinShieldSprite != null)
                return paladinShieldSprite;

            return paladinShieldSprite = CreateFallbackPaladinShieldSprite("Paladin Divine Shield Fallback");
        }

        private static Sprite LoadPaladinCrestSprite()
        {
            if (paladinCrestSprite != null)
                return paladinCrestSprite;

            paladinCrestSprite = Resources.Load<Sprite>("UI/paladin_holy_crest");
            if (paladinCrestSprite != null)
                return paladinCrestSprite;

            return paladinCrestSprite = CreateFallbackPaladinCrestSprite("Paladin Holy Crest Fallback");
        }

        private static Sprite LoadPaladinShardSprite()
        {
            if (paladinShardSprite != null)
                return paladinShardSprite;

            paladinShardSprite = Resources.Load<Sprite>("UI/paladin_shield_shard");
            if (paladinShardSprite != null)
                return paladinShardSprite;

            return paladinShardSprite = CreateFallbackPaladinShardSprite("Paladin Shield Shard Fallback");
        }

        private static Sprite LoadPaladinConstellationShieldSprite()
        {
            if (paladinConstellationShieldSprite != null)
                return paladinConstellationShieldSprite;

            return paladinConstellationShieldSprite = CreatePaladinConstellationShieldSprite("Paladin Protection Constellation");
        }

        private static Sprite LoadRogueDaggerSprite()
        {
            if (rogueDaggerSprite != null)
                return rogueDaggerSprite;

            rogueDaggerSprite = Resources.Load<Sprite>("UI/rogue_dagger");
            if (rogueDaggerSprite != null)
                return rogueDaggerSprite;

            return rogueDaggerSprite = CreateFallbackRogueDaggerSprite("Rogue Dagger Fallback");
        }

        private static Sprite LoadRogueHitMarkerSprite()
        {
            if (rogueHitMarkerSprite != null)
                return rogueHitMarkerSprite;

            rogueHitMarkerSprite = Resources.Load<Sprite>("UI/rogue_hit_marker");
            if (rogueHitMarkerSprite != null)
                return rogueHitMarkerSprite;

            return rogueHitMarkerSprite = CreateFallbackRogueHitMarkerSprite("Rogue Hit Marker Fallback");
        }

        private static Sprite LoadRogueDeflectSprite()
        {
            if (rogueDeflectSprite != null)
                return rogueDeflectSprite;

            rogueDeflectSprite = Resources.Load<Sprite>("UI/rogue_deflect");
            if (rogueDeflectSprite != null)
                return rogueDeflectSprite;

            return rogueDeflectSprite = CreateFallbackRogueDeflectSprite("Rogue Deflect Fallback");
        }

        private static Sprite LoadNecromancerSkullSprite()
        {
            if (necromancerSkullSprite != null)
                return necromancerSkullSprite;

            necromancerSkullSprite = Resources.Load<Sprite>("UI/necromancer_skull");
            if (necromancerSkullSprite != null)
                return necromancerSkullSprite;

            return necromancerSkullSprite = CreateFallbackNecromancerSkullSprite("Necromancer Skull Fallback");
        }

        private static Sprite LoadNecromancerTrailSprite()
        {
            if (necromancerTrailSprite != null)
                return necromancerTrailSprite;

            necromancerTrailSprite = Resources.Load<Sprite>("UI/necromancer_soul_trail");
            if (necromancerTrailSprite != null)
                return necromancerTrailSprite;

            return necromancerTrailSprite = CreateFallbackNecromancerTrailSprite("Necromancer Soul Trail Fallback");
        }

        private static Sprite LoadNecromancerBurstSprite()
        {
            if (necromancerBurstSprite != null)
                return necromancerBurstSprite;

            necromancerBurstSprite = Resources.Load<Sprite>("UI/necromancer_green_burst");
            if (necromancerBurstSprite != null)
                return necromancerBurstSprite;

            return necromancerBurstSprite = CreateFallbackNecromancerBurstSprite("Necromancer Green Burst Fallback");
        }

        private static Sprite LoadAssassinSmokeSprite()
        {
            if (assassinSmokeSprite != null)
                return assassinSmokeSprite;

            assassinSmokeSprite = Resources.Load<Sprite>("UI/assassin_smoke");
            if (assassinSmokeSprite != null)
                return assassinSmokeSprite;

            return assassinSmokeSprite = CreateFallbackBlobSprite("Assassin Smoke Fallback", new Color32(54, 48, 66, 190));
        }

        private static Sprite LoadAssassinDaggerLeftSprite()
        {
            if (assassinDaggerLeftSprite != null)
                return assassinDaggerLeftSprite;

            assassinDaggerLeftSprite = Resources.Load<Sprite>("UI/assassin_dagger_left");
            if (assassinDaggerLeftSprite != null)
                return assassinDaggerLeftSprite;

            return assassinDaggerLeftSprite = CreateFallbackBladeSprite("Assassin Dagger Left Fallback");
        }

        private static Sprite LoadAssassinDaggerRightSprite()
        {
            if (assassinDaggerRightSprite != null)
                return assassinDaggerRightSprite;

            assassinDaggerRightSprite = Resources.Load<Sprite>("UI/assassin_dagger_right");
            if (assassinDaggerRightSprite != null)
                return assassinDaggerRightSprite;

            return assassinDaggerRightSprite = CreateFallbackBladeSprite("Assassin Dagger Right Fallback");
        }

        private static Sprite LoadBarbarianDoubleAxeSprite()
        {
            if (barbarianDoubleAxeSprite != null)
                return barbarianDoubleAxeSprite;

            barbarianDoubleAxeSprite = Resources.Load<Sprite>("UI/barbarian_double_axe");
            if (barbarianDoubleAxeSprite != null)
                return barbarianDoubleAxeSprite;

            return barbarianDoubleAxeSprite = CreateFallbackBladeSprite("Barbarian Double Axe Fallback");
        }

        private static Sprite LoadBragusCleaverSprite()
        {
            if (bragusCleaverSprite != null)
                return bragusCleaverSprite;

            bragusCleaverSprite = Resources.Load<Sprite>("UI/bragus_cleaver");
            if (bragusCleaverSprite != null)
                return bragusCleaverSprite;

            return bragusCleaverSprite = CreateFallbackBladeSprite("Bragus Cleaver Fallback");
        }

        private static Sprite LoadWarriorSwordSprite()
        {
            if (warriorSwordSprite != null)
                return warriorSwordSprite;

            warriorSwordSprite = Resources.Load<Sprite>("UI/warrior_sword");
            if (warriorSwordSprite != null)
                return warriorSwordSprite;

            return warriorSwordSprite = CreateFallbackWarriorSwordSprite("Warrior Sword Fallback");
        }

        private static Sprite LoadWarriorSlashSprite()
        {
            if (warriorSlashSprite != null)
                return warriorSlashSprite;

            warriorSlashSprite = Resources.Load<Sprite>("UI/warrior_slash");
            if (warriorSlashSprite != null)
                return warriorSlashSprite;

            return warriorSlashSprite = CreateFallbackWarriorSlashSprite("Warrior Slash Fallback");
        }

        private static Sprite LoadWarriorDashTrailSprite()
        {
            if (warriorDashTrailSprite != null)
                return warriorDashTrailSprite;

            warriorDashTrailSprite = Resources.Load<Sprite>("UI/warrior_dash_trail");
            if (warriorDashTrailSprite != null)
                return warriorDashTrailSprite;

            return warriorDashTrailSprite = CreateFallbackWarriorTrailSprite("Warrior Dash Trail Fallback");
        }

        private static Sprite LoadWarriorGroundCrackSprite()
        {
            if (warriorGroundCrackSprite != null)
                return warriorGroundCrackSprite;

            warriorGroundCrackSprite = Resources.Load<Sprite>("UI/warrior_ground_crack");
            if (warriorGroundCrackSprite != null)
                return warriorGroundCrackSprite;

            return warriorGroundCrackSprite = CreateFallbackWarriorCrackSprite("Warrior Ground Crack Fallback");
        }

        private static Sprite CreateFallbackBlobSprite(string name, Color32 color)
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * 64 + x] = new Color32(color.r, color.g, color.b, (byte)(color.a * alpha));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackArcaneGlyphSprite(string name)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[96 * 96];
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    float dx = (x - 47.5f) / 47.5f;
                    float dy = (y - 47.5f) / 47.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.56f) * 18f);
                    float core = Mathf.Clamp01(1f - radius * 1.8f);
                    float rays = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(angle * 4f)) * 7f) * Mathf.Clamp01(1f - Mathf.Abs(radius - 0.35f) * 5f);
                    float alpha = Mathf.Clamp01(core * 0.72f + ring + rays * 0.75f);
                    byte a = (byte)(alpha * 235f);
                    pixels[y * 96 + x] = new Color32(80, 180, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackArcaneParticleSprite(string name)
        {
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Abs(x - 15.5f) / 15.5f;
                    float dy = Mathf.Abs(y - 15.5f) / 15.5f;
                    float diamond = Mathf.Clamp01(1f - (dx + dy));
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    byte a = (byte)Mathf.Clamp(diamond * 255f + glow * 80f, 0f, 255f);
                    pixels[y * 32 + x] = new Color32(110, 215, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackPriestBeamSprite(string name)
        {
            Texture2D texture = new Texture2D(64, 192, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[64 * 192];
            for (int y = 0; y < 192; y++)
            {
                float vertical = 1f - Mathf.Abs((y / 191f) - 0.54f) * 0.9f;
                for (int x = 0; x < 64; x++)
                {
                    float dx = Mathf.Abs((x - 31.5f) / 31.5f);
                    float core = Mathf.Clamp01(1f - dx * 2.8f);
                    float glow = Mathf.Clamp01(1f - dx * 1.15f) * 0.48f;
                    float ray = Mathf.Clamp01(core + glow) * Mathf.Clamp01(vertical);
                    byte a = (byte)(ray * 230f);
                    byte r = 255;
                    byte g = (byte)Mathf.Lerp(244f, 214f, dx);
                    byte b = (byte)Mathf.Lerp(248f, 124f, dx);
                    pixels[y * 64 + x] = new Color32(r, g, b, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 192), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackPriestSparkSprite(string name)
        {
            Texture2D texture = new Texture2D(48, 48, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[48 * 48];
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 48; x++)
                {
                    float dx = Mathf.Abs((x - 23.5f) / 23.5f);
                    float dy = Mathf.Abs((y - 23.5f) / 23.5f);
                    float diamond = Mathf.Clamp01(1f - (dx * 0.82f + dy * 1.18f));
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.42f;
                    byte a = (byte)Mathf.Clamp((diamond + glow) * 255f, 0f, 255f);
                    pixels[y * 48 + x] = new Color32(255, 246, 188, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackPriestCrossSprite(string name)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[96 * 96];
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    float nx = Mathf.Abs((x - 47.5f) / 47.5f);
                    float ny = Mathf.Abs((y - 47.5f) / 47.5f);
                    float vertical = Mathf.Clamp01(1f - nx * 7.2f) * Mathf.Clamp01(1f - ny * 1.45f);
                    float horizontal = Mathf.Clamp01(1f - ny * 8.8f) * Mathf.Clamp01(1f - nx * 1.15f);
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) * 1.22f) * 0.34f;
                    float alpha = Mathf.Clamp01(Mathf.Max(vertical, horizontal) + glow);
                    if (alpha < 0.08f)
                        alpha = 0f;
                    byte a = (byte)(alpha * 245f);
                    byte g = (byte)Mathf.Lerp(244f, 218f, Mathf.Clamp01(nx + ny));
                    byte b = (byte)Mathf.Lerp(255f, 128f, Mathf.Clamp01(nx + ny));
                    pixels[y * 96 + x] = new Color32(255, g, b, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackPaladinShieldSprite(string name)
        {
            Texture2D texture = new Texture2D(128, 144, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[128 * 144];
            for (int y = 0; y < 144; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float nx = (x - 63.5f) / 63.5f;
                    float ny = (y - 71.5f) / 71.5f;
                    float upper = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx * 0.82f + (ny + 0.18f) * (ny + 0.18f) * 1.2f));
                    float taper = Mathf.Clamp01(1f - Mathf.Abs(nx) * Mathf.Lerp(0.82f, 1.55f, Mathf.Clamp01((ny + 0.1f) * 0.86f)));
                    float bottomPoint = Mathf.Clamp01(1f - Mathf.Abs(ny - 0.72f) * 2.45f) * Mathf.Clamp01(1f - Mathf.Abs(nx) * 2.8f);
                    float silhouette = Mathf.Clamp01((upper * taper) + bottomPoint);
                    float border = Mathf.Clamp01(1f - Mathf.Abs(silhouette - 0.52f) * 12f) * silhouette;
                    float ridge = Mathf.Clamp01(1f - Mathf.Abs(nx) * 18f) * Mathf.Clamp01(1f - Mathf.Abs(ny) * 1.35f);
                    float crossH = Mathf.Clamp01(1f - Mathf.Abs(ny + 0.08f) * 24f) * Mathf.Clamp01(1f - Mathf.Abs(nx) * 2.4f);
                    float crossV = Mathf.Clamp01(1f - Mathf.Abs(nx) * 22f) * Mathf.Clamp01(1f - Mathf.Abs(ny + 0.02f) * 3.7f);
                    float inset = Mathf.Clamp01(silhouette - border * 0.5f);
                    float shine = Mathf.Clamp01(1f - Mathf.Sqrt((nx + 0.28f) * (nx + 0.28f) * 7f + (ny + 0.42f) * (ny + 0.42f) * 9f));
                    float alpha = Mathf.Clamp01(silhouette * 1.28f + border * 0.8f);
                    byte a = (byte)(alpha * 245f);
                    byte r = (byte)Mathf.Lerp(128f, 255f, Mathf.Clamp01(border + crossH + crossV + shine * 0.65f));
                    byte g = (byte)Mathf.Lerp(150f, 238f, Mathf.Clamp01(inset + border + ridge));
                    byte b = (byte)Mathf.Lerp(174f, 252f, Mathf.Clamp01(shine + crossH * 0.55f));
                    pixels[y * 128 + x] = new Color32(r, g, b, a);

                    if (crossH > 0.1f || crossV > 0.1f || ridge > 0.18f)
                        pixels[y * 128 + x] = new Color32(255, 232, 132, (byte)Mathf.Clamp(a + 28, 0, 255));
                    if (border > 0.18f)
                        pixels[y * 128 + x] = new Color32(255, 208, 78, (byte)Mathf.Clamp(a + 10, 0, 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 128, 144), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackPaladinCrestSprite(string name)
        {
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float nx = (x - 63.5f) / 63.5f;
                    float ny = (y - 63.5f) / 63.5f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);
                    float ringOuter = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.72f) * 20f);
                    float ringInner = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.43f) * 16f);
                    float rays = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(angle * 8f)) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(radius - 0.58f) * 4.2f);
                    float crossH = Mathf.Clamp01(1f - Mathf.Abs(ny) * 18f) * Mathf.Clamp01(1f - Mathf.Abs(nx) * 2.6f);
                    float crossV = Mathf.Clamp01(1f - Mathf.Abs(nx) * 20f) * Mathf.Clamp01(1f - Mathf.Abs(ny) * 2.2f);
                    float glow = Mathf.Clamp01(1f - radius) * 0.36f;
                    float alpha = Mathf.Clamp01(ringOuter + ringInner * 0.85f + rays * 0.72f + crossH + crossV + glow);
                    byte a = (byte)(alpha * 230f);
                    byte r = 255;
                    byte g = (byte)Mathf.Lerp(190f, 245f, Mathf.Clamp01(ringOuter + crossH + crossV));
                    byte b = (byte)Mathf.Lerp(70f, 188f, Mathf.Clamp01(glow + ringInner));
                    pixels[y * 128 + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackPaladinShardSprite(string name)
        {
            Texture2D texture = new Texture2D(48, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[48 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 48; x++)
                {
                    float nx = (x - 23.5f) / 23.5f;
                    float ny = (y - 31.5f) / 31.5f;
                    float leftEdge = nx + ny * 0.34f + 0.42f;
                    float rightEdge = -nx + ny * 0.2f + 0.46f;
                    float bottomEdge = 0.84f - ny;
                    float topEdge = ny + 0.86f;
                    float shard = Mathf.Clamp01(leftEdge * 5f) * Mathf.Clamp01(rightEdge * 5f) * Mathf.Clamp01(bottomEdge * 4f) * Mathf.Clamp01(topEdge * 4f);
                    float highlight = Mathf.Clamp01(1f - Mathf.Abs(nx + ny * 0.18f) * 7f) * shard;
                    byte a = (byte)(Mathf.Clamp01(shard) * 235f);
                    byte r = (byte)Mathf.Lerp(170f, 255f, highlight);
                    byte g = (byte)Mathf.Lerp(190f, 236f, highlight);
                    byte b = (byte)Mathf.Lerp(218f, 255f, highlight);
                    pixels[y * 48 + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 48, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreatePaladinConstellationShieldSprite(string name)
        {
            const int width = 128;
            const int height = 144;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            Vector2[] points =
            {
                new(22f, 28f),
                new(64f, 12f),
                new(106f, 28f),
                new(100f, 84f),
                new(64f, 132f),
                new(28f, 84f),
                new(38f, 46f),
                new(64f, 34f),
                new(90f, 46f),
                new(82f, 78f),
                new(64f, 104f),
                new(46f, 78f)
            };

            int[] outline = { 0, 1, 2, 3, 4, 5, 0 };
            int[] inner = { 6, 7, 8, 9, 10, 11, 6 };
            int[] cross = { 1, 4, 0, 2, 5, 3, 7, 10, 11, 9 };
            Color32 lineColor = new Color32(168, 222, 255, 205);
            Color32 brightLineColor = new Color32(230, 244, 255, 225);
            Color32 starColor = new Color32(242, 248, 255, 245);

            DrawConstellationPath(pixels, width, height, points, outline, lineColor, 1);
            DrawConstellationPath(pixels, width, height, points, inner, lineColor, 1);
            DrawConstellationPath(pixels, width, height, points, cross, new Color32(118, 196, 255, 128), 1);

            foreach (Vector2 point in points)
                DrawConstellationStar(pixels, width, height, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), starColor);
            DrawConstellationStar(pixels, width, height, 64, 64, brightLineColor);

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DrawConstellationPath(Color32[] pixels, int width, int height, Vector2[] points, int[] indices, Color32 color, int radius)
        {
            for (int i = 0; i < indices.Length - 1; i++)
                DrawConstellationLine(pixels, width, height, points[indices[i]], points[indices[i + 1]], color, radius);
        }

        private static void DrawConstellationLine(Color32[] pixels, int width, int height, Vector2 from, Vector2 to, Color32 color, int radius)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 1.35f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / Mathf.Max(1f, steps);
                Vector2 point = Vector2.Lerp(from, to, t);
                byte alpha = (byte)Mathf.Clamp(color.a * Mathf.Lerp(0.54f, 1f, Mathf.Sin(t * Mathf.PI)), 0f, 255f);
                DrawConstellationDot(pixels, width, height, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), radius, new Color32(color.r, color.g, color.b, alpha));
            }
        }

        private static void DrawConstellationStar(Color32[] pixels, int width, int height, int x, int y, Color32 color)
        {
            DrawConstellationDot(pixels, width, height, x, y, 2, color);
            DrawConstellationDot(pixels, width, height, x - 4, y, 1, new Color32(color.r, color.g, color.b, 120));
            DrawConstellationDot(pixels, width, height, x + 4, y, 1, new Color32(color.r, color.g, color.b, 120));
            DrawConstellationDot(pixels, width, height, x, y - 4, 1, new Color32(color.r, color.g, color.b, 120));
            DrawConstellationDot(pixels, width, height, x, y + 4, 1, new Color32(color.r, color.g, color.b, 120));
        }

        private static void DrawConstellationDot(Color32[] pixels, int width, int height, int centerX, int centerY, int radius, Color32 color)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= height)
                    continue;
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= width)
                        continue;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance > radius + 0.45f)
                        continue;

                    float coverage = Mathf.Clamp01(1f - (distance / (radius + 0.45f)));
                    int index = y * width + x;
                    Color32 existing = pixels[index];
                    byte alpha = (byte)Mathf.Clamp(existing.a + color.a * coverage, 0f, 255f);
                    pixels[index] = new Color32(
                        (byte)Mathf.Max(existing.r, color.r),
                        (byte)Mathf.Max(existing.g, color.g),
                        (byte)Mathf.Max(existing.b, color.b),
                        alpha);
                }
            }
        }

        private static Sprite CreateFallbackRogueDaggerSprite(string name)
        {
            Texture2D texture = new Texture2D(48, 96, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[48 * 96];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            for (int y = 8; y < 74; y++)
            {
                float t = (y - 8f) / 66f;
                int half = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, Mathf.Sin(t * Mathf.PI))));
                int center = 24 + Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 2f);
                for (int x = center - half; x <= center + half; x++)
                {
                    if (x < 0 || x >= 48)
                        continue;
                    bool edge = Mathf.Abs(x - center) >= half - 1;
                    pixels[y * 48 + x] = edge
                        ? new Color32(102, 255, 170, 230)
                        : new Color32(220, 232, 238, 255);
                }
            }

            for (int y = 70; y < 88; y++)
            {
                for (int x = 18; x <= 30; x++)
                    pixels[y * 48 + x] = new Color32(62, 42, 72, 255);
            }
            for (int x = 13; x <= 35; x++)
                for (int y = 68; y <= 73; y++)
                    pixels[y * 48 + x] = new Color32(122, 255, 178, 235);

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 48, 96), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackRogueHitMarkerSprite(string name)
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float slashA = Mathf.Clamp01(1f - Mathf.Abs(dx + dy * 0.46f) * 12f) * Mathf.Clamp01(1f - Mathf.Abs(dx - dy) * 2.6f);
                    float slashB = Mathf.Clamp01(1f - Mathf.Abs(dx - dy * 0.58f) * 13f) * Mathf.Clamp01(1f - Mathf.Abs(dx + dy) * 2.4f);
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.34f;
                    byte a = (byte)Mathf.Clamp((slashA + slashB + glow) * 255f, 0f, 255f);
                    pixels[y * 64 + x] = new Color32(170, 255, 160, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackRogueDeflectSprite(string name)
        {
            Texture2D texture = new Texture2D(72, 72, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[72 * 72];
            for (int y = 0; y < 72; y++)
            {
                for (int x = 0; x < 72; x++)
                {
                    float dx = (x - 35.5f) / 35.5f;
                    float dy = (y - 35.5f) / 35.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float arc = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.52f) * 16f)
                        * Mathf.Clamp01(Mathf.Sin(angle + 0.4f) * 2.8f);
                    float spark = Mathf.Clamp01(1f - Mathf.Abs(dx) * 5.5f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.4f);
                    byte a = (byte)Mathf.Clamp((arc + spark * 0.55f) * 235f, 0f, 255f);
                    pixels[y * 72 + x] = new Color32(154, 255, 206, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 72, 72), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackNecromancerSkullSprite(string name)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[96 * 96];
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    float dx = (x - 47.5f) / 47.5f;
                    float dy = (y - 47.5f) / 47.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float head = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx * 1.02f + (dy + 0.16f) * (dy + 0.16f) * 1.42f));
                    float cheek = Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.65f) * Mathf.Clamp01(1f - Mathf.Abs(dy - 0.22f) * 2.6f);
                    float jaw = Mathf.Clamp01(1f - Mathf.Abs(dx) * 2.08f) * Mathf.Clamp01(1f - Mathf.Abs(dy - 0.54f) * 3.35f);
                    float leftEye = Mathf.Clamp01(1f - Mathf.Sqrt((dx + 0.32f) * (dx + 0.32f) * 34f + (dy + 0.08f) * (dy + 0.08f) * 46f));
                    float rightEye = Mathf.Clamp01(1f - Mathf.Sqrt((dx - 0.32f) * (dx - 0.32f) * 34f + (dy + 0.08f) * (dy + 0.08f) * 46f));
                    float nose = Mathf.Clamp01(1f - (Mathf.Abs(dx) * 6.4f + Mathf.Abs(dy - 0.18f) * 4.7f));
                    float teeth = Mathf.Clamp01(1f - Mathf.Abs(dy - 0.58f) * 20f) * Mathf.Clamp01(Mathf.Abs(Mathf.Sin((dx + 0.5f) * 34f)) * 1.55f);
                    float cracks = Mathf.Clamp01(1f - Mathf.Abs(dx + dy * 0.35f + Mathf.Sin(dy * 17f) * 0.04f) * 28f)
                        * Mathf.Clamp01(1f - Mathf.Abs(radius - 0.42f) * 2.8f);
                    float glow = Mathf.Clamp01(1f - radius) * 0.36f;
                    float silhouette = Mathf.Clamp01(head * 1.12f + cheek * 0.42f + jaw * 0.92f);
                    float holes = Mathf.Clamp01(leftEye + rightEye + nose);
                    byte a = (byte)Mathf.Clamp((silhouette - holes * 0.82f + teeth * 0.34f + cracks * 0.35f + glow) * 255f, 0f, 255f);
                    byte r = (byte)Mathf.Lerp(112f, 205f, Mathf.Clamp01(head + jaw));
                    byte g = (byte)Mathf.Lerp(164f, 255f, Mathf.Clamp01(head + jaw + glow));
                    byte b = (byte)Mathf.Lerp(82f, 138f, Mathf.Clamp01(head));
                    pixels[y * 96 + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackNecromancerTrailSprite(string name)
        {
            Texture2D texture = new Texture2D(180, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[180 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 180; x++)
                {
                    float normalizedX = x / 179f;
                    float dy = (y - 31.5f) / 31.5f;
                    float waveCenter = Mathf.Sin(normalizedX * Mathf.PI * 2.8f) * 0.24f;
                    float body = Mathf.Clamp01(1f - Mathf.Abs(dy - waveCenter) * Mathf.Lerp(5.8f, 1.1f, normalizedX));
                    float smoke = Mathf.Clamp01(1f - Mathf.Abs(dy + Mathf.Sin(normalizedX * Mathf.PI * 5.4f) * 0.18f) * 2.1f);
                    float alpha = (body * 0.82f + smoke * 0.34f) * Mathf.Pow(normalizedX, 1.52f);
                    byte r = (byte)Mathf.Lerp(32f, 104f, body);
                    byte g = (byte)Mathf.Lerp(118f, 255f, body);
                    byte b = (byte)Mathf.Lerp(44f, 62f, body);
                    pixels[y * 180 + x] = new Color32(r, g, b, (byte)(alpha * 230f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 180, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackNecromancerBurstSprite(string name)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[96 * 96];
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    float dx = (x - 47.5f) / 47.5f;
                    float dy = (y - 47.5f) / 47.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.44f) * 10.5f);
                    float innerRing = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.24f) * 13.5f);
                    float core = Mathf.Clamp01(1f - radius * 2.2f);
                    float rays = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(angle * 8f + radius * 5.5f)) * 4.8f) * Mathf.Clamp01(1f - Mathf.Abs(radius - 0.64f) * 3.4f);
                    float rot = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(angle * 3f - radius * 7f)) * 5.2f) * Mathf.Clamp01(1f - radius * 1.4f);
                    float alpha = Mathf.Clamp01(core * 0.72f + ring * 0.9f + innerRing * 0.46f + rays * 0.86f + rot * 0.42f);
                    byte r = (byte)Mathf.Lerp(36f, 98f, Mathf.Clamp01(core + rays));
                    byte g = (byte)Mathf.Lerp(120f, 255f, Mathf.Clamp01(core + ring + rays));
                    byte b = (byte)Mathf.Lerp(32f, 70f, Mathf.Clamp01(core));
                    pixels[y * 96 + x] = new Color32(r, g, b, (byte)(alpha * 238f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackArcaneTrailSprite(string name)
        {
            Texture2D texture = new Texture2D(128, 40, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[128 * 40];
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float normalizedX = x / 127f;
                    float centerY = (y - 19.5f) / 19.5f;
                    float tail = Mathf.Pow(normalizedX, 1.35f);
                    float width = Mathf.Lerp(0.1f, 0.9f, normalizedX);
                    float body = Mathf.Clamp01(1f - Mathf.Abs(centerY) / width);
                    float alpha = body * tail;
                    pixels[y * 128 + x] = new Color32(112, 86, 170, (byte)(alpha * 220f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 128, 40), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackWarriorSwordSprite(string name)
        {
            Texture2D texture = new Texture2D(64, 128, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[64 * 128];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            for (int y = 10; y < 88; y++)
            {
                float t = (y - 10f) / 78f;
                int half = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(3f, 10f, Mathf.Sin(t * Mathf.PI))));
                int center = 32;
                for (int x = center - half; x <= center + half; x++)
                {
                    if (x < 0 || x >= 64)
                        continue;
                    bool edge = Mathf.Abs(x - center) >= half - 1;
                    pixels[y * 64 + x] = edge
                        ? new Color32(255, 223, 128, 245)
                        : new Color32(230, 236, 244, 255);
                }
            }

            for (int y = 84; y < 100; y++)
                for (int x = 24; x <= 40; x++)
                    pixels[y * 64 + x] = new Color32(96, 72, 56, 255);
            for (int y = 98; y < 118; y++)
                for (int x = 29; x <= 35; x++)
                    pixels[y * 64 + x] = new Color32(68, 54, 50, 255);
            for (int y = 90; y <= 96; y++)
                for (int x = 14; x <= 50; x++)
                    pixels[y * 64 + x] = new Color32(245, 190, 70, 255);

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 128), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackWarriorSlashSprite(string name)
        {
            Texture2D texture = new Texture2D(160, 96, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[160 * 96];
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 160; x++)
                {
                    float nx = (x - 79.5f) / 79.5f;
                    float ny = (y - 47.5f) / 47.5f;
                    float arc = Mathf.Clamp01(1f - Mathf.Abs(ny - Mathf.Sin((nx + 0.12f) * Mathf.PI) * 0.34f) * 11f)
                        * Mathf.Clamp01(1f - Mathf.Abs(nx) * 0.88f);
                    float core = Mathf.Clamp01(1f - Mathf.Abs(ny - Mathf.Sin((nx + 0.18f) * Mathf.PI) * 0.28f) * 22f)
                        * Mathf.Clamp01(1f - Mathf.Abs(nx) * 1.05f);
                    float spark = Mathf.Clamp01(1f - Mathf.Abs(nx + ny * 0.42f) * 12f) * Mathf.Clamp01(1f - Mathf.Abs(nx) * 2.4f);
                    float alpha = Mathf.Clamp01(arc * 0.72f + core + spark * 0.22f);
                    byte r = (byte)Mathf.Lerp(255f, 255f, core);
                    byte g = (byte)Mathf.Lerp(184f, 246f, core);
                    byte b = (byte)Mathf.Lerp(72f, 215f, core);
                    pixels[y * 160 + x] = new Color32(r, g, b, (byte)(alpha * 238f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 160, 96), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackWarriorTrailSprite(string name)
        {
            Texture2D texture = new Texture2D(180, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[180 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 180; x++)
                {
                    float normalizedX = x / 179f;
                    float dy = Mathf.Abs((y - 31.5f) / 31.5f);
                    float width = Mathf.Lerp(0.08f, 0.78f, normalizedX);
                    float body = Mathf.Clamp01(1f - dy / width);
                    float taper = Mathf.Pow(normalizedX, 1.55f);
                    byte a = (byte)(body * taper * 205f);
                    pixels[y * 180 + x] = new Color32(255, 222, 132, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 180, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackWarriorCrackSprite(string name)
        {
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float nx = (x - 63.5f) / 63.5f;
                    float ny = (y - 63.5f) / 63.5f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float main = Mathf.Clamp01(1f - Mathf.Abs(nx + Mathf.Sin(ny * 9f) * 0.08f) * 20f)
                        * Mathf.Clamp01(1f - Mathf.Abs(ny) * 0.9f);
                    float left = Mathf.Clamp01(1f - Mathf.Abs(nx + ny * 0.72f + 0.16f) * 24f)
                        * Mathf.Clamp01(1f - radius * 1.15f);
                    float right = Mathf.Clamp01(1f - Mathf.Abs(nx - ny * 0.64f - 0.18f) * 24f)
                        * Mathf.Clamp01(1f - radius * 1.1f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.46f) * 18f);
                    float alpha = Mathf.Clamp01(main + left * 0.72f + right * 0.72f + ring * 0.38f);
                    byte r = (byte)Mathf.Lerp(80f, 255f, alpha);
                    byte g = (byte)Mathf.Lerp(72f, 212f, alpha);
                    byte b = (byte)Mathf.Lerp(62f, 92f, alpha);
                    pixels[y * 128 + x] = new Color32(r, g, b, (byte)(alpha * 232f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackBladeSprite(string name)
        {
            Texture2D texture = new Texture2D(32, 64, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[32 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            for (int y = 4; y < 58; y++)
            {
                int half = Mathf.Max(1, y < 42 ? (42 - y) / 5 + 1 : (y - 42) / 3 + 1);
                for (int x = 16 - half; x <= 16 + half; x++)
                    if (x >= 0 && x < 32)
                        pixels[y * 32 + x] = new Color32(210, 215, 230, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Vector3 DuelWorldPoint(RectTransform root, float xAnchor)
        {
            Rect rect = root.rect;
            return root.TransformPoint(new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, xAnchor),
                Mathf.Lerp(rect.yMin, rect.yMax, 0.5f),
                0f));
        }
    }
}
