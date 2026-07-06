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
        private static Sprite assassinSmokeSprite;
        private static Sprite assassinDaggerLeftSprite;
        private static Sprite assassinDaggerRightSprite;
        private static Sprite barbarianDoubleAxeSprite;
        private static Sprite barbarianGroundCrackSprite;
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
        private static Sprite rogueDaggerSprite;
        private static Sprite rogueHitMarkerSprite;
        private static Sprite rogueDeflectSprite;
        private static Sprite necromancerSkullSprite;
        private static Sprite necromancerTrailSprite;
        private static Sprite necromancerBurstSprite;
        private static AudioClip rogueAttackHitSfx;
        private AudioSource rogueSfxSource;

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
            int defenderTotal = 0)
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
                defenderTotal);
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
            int defenderTotal = 0)
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
                defenderTotal));
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
            VigorSelectionMode selectionMode)
        {
            VigorSelectionMode normalizedSelectionMode = NormalizeSelectionMode(hasSecond, selectionMode);
            return new VigorRollResult(
                dieSides,
                first,
                second,
                hasSecond,
                selected,
                SelectionModeToMatchup(normalizedSelectionMode),
                normalizedSelectionMode);
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
            int defenderTotal)
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

            bool hasAttackerRoll = diceCatalog != null && attackerDieSides > 0 && attackerRoll.FirstRoll > 0;
            bool hasDefenderRoll = diceCatalog != null && defenderDieSides > 0 && defenderRoll.FirstRoll > 0;
            if (hasAttackerRoll)
            {
                attacker.PlayVigorRoll(
                    diceCatalog,
                    attackerDieSides,
                    attackerRoll,
                    attackerCaption,
                    configuration.Animation.DiceRollDuration,
                    configuration.Animation.DiceResultHold);
            }
            if (hasDefenderRoll)
            {
                defender.PlayVigorRoll(
                    diceCatalog,
                    defenderDieSides,
                    defenderRoll,
                    defenderCaption,
                    configuration.Animation.DiceRollDuration,
                    configuration.Animation.DiceResultHold);
            }
            if (hasAttackerRoll || hasDefenderRoll)
                yield return new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
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
                yield return StartCoroutine(defender.PlayDefeatAnimation());
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

            yield return StartCoroutine(PlayAssassinDaggers(parent, defender.RectTransform));
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

            GameObject crack = CreateOverlaySprite(
                parent,
                "Barbarian Ground Crack",
                LoadBarbarianGroundCrackSprite(),
                new Vector2(260f, 260f),
                out RectTransform crackRect,
                out Image crackImage);
            crackRect.position = defenderStart + new Vector3(0f, -12f, 0f);
            crackRect.localScale = Vector3.one * 0.2f;
            crackImage.color = new Color(1f, 1f, 1f, 0f);

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
                    crackRect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.2f, Mathf.SmoothStep(0f, 1f, crackProgress));
                    crackImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.92f, crackProgress));
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

            float fadeDuration = 0.2f;
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                crackImage.color = new Color(1f, 1f, 1f, 1f - progress);
                yield return null;
            }

            defenderRect.position = defenderStart;
            defenderRect.localScale = defenderScale;
            defenderRect.localRotation = defenderRotation;
            attackerRect.position = attackerStart;
            attackerRect.localScale = attackerScale;
            attackerRect.localRotation = attackerRotation;
            Destroy(axe);
            Destroy(crack);
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
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            Vector3 lungePoint = attackerStart + normalizedDirection * Mathf.Clamp(attackDirection.magnitude * 0.42f, 150f, 280f);

            attacker.SetLayoutIgnored(true);
            attackerRect.SetAsLastSibling();

            GameObject trail = CreateOverlaySprite(
                parent,
                "Warrior Dash Trail",
                LoadWarriorDashTrailSprite(),
                new Vector2(430f, 136f),
                out RectTransform trailRect,
                out Image trailImage);
            trailRect.localRotation = Quaternion.Euler(0f, 0f, angle + 180f);

            float dashDuration = 0.24f;
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / dashDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                Vector3 overshoot = normalizedDirection * Mathf.Sin(progress * Mathf.PI) * 28f;
                attackerRect.position = Vector3.LerpUnclamped(attackerStart, lungePoint, eased) + overshoot;
                attackerRect.localScale = attackerScale * Mathf.Lerp(1f, 1.16f, Mathf.Sin(progress * Mathf.PI));
                attackerRect.localRotation = attackerRotation * Quaternion.Euler(0f, 0f, -Mathf.Sign(normalizedDirection.x == 0f ? 1f : normalizedDirection.x) * Mathf.Sin(progress * Mathf.PI) * 8f);
                trailRect.position = attackerRect.position - normalizedDirection * 112f;
                trailRect.localScale = new Vector3(Mathf.Lerp(0.85f, 1.42f, progress), Mathf.Lerp(0.9f, 1.18f, progress), 1f);
                trailImage.color = new Color(0.95f, 0.9f, 0.78f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 5f)));
                yield return null;
            }

            Destroy(trail);

            yield return StartCoroutine(PlayWarriorSlashCombo(parent, attackerRect, defenderRect, normalizedDirection));
            yield return StartCoroutine(PlayImpactPulse(defenderRect));

            if (abilityAttack)
                yield return StartCoroutine(PlayWarriorJudgementSword(parent, defenderRect));

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
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            Vector3 lungePoint = attackerStart + normalizedDirection * Mathf.Clamp(attackDirection.magnitude * 0.38f, 140f, 250f);

            attacker.SetLayoutIgnored(true);
            attackerRect.SetAsLastSibling();

            GameObject trail = CreateOverlaySprite(
                parent,
                "Warrior Blocked Dash Trail",
                LoadWarriorDashTrailSprite(),
                new Vector2(390f, 124f),
                out RectTransform trailRect,
                out Image trailImage);
            trailRect.localRotation = Quaternion.Euler(0f, 0f, angle + 180f);

            GameObject sword = CreateOverlaySprite(
                parent,
                "Warrior Bounced Sword",
                LoadWarriorSwordSprite(),
                new Vector2(300f, 300f),
                out RectTransform swordRect,
                out Image swordImage);
            swordImage.color = new Color(1f, 1f, 1f, 0f);

            float dashDuration = 0.22f;
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / dashDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                attackerRect.position = Vector3.LerpUnclamped(attackerStart, lungePoint, eased) + normalizedDirection * Mathf.Sin(progress * Mathf.PI) * 22f;
                attackerRect.localScale = attackerScale * Mathf.Lerp(1f, 1.14f, Mathf.Sin(progress * Mathf.PI));
                trailRect.position = attackerRect.position - normalizedDirection * 98f;
                trailRect.localScale = new Vector3(Mathf.Lerp(0.82f, 1.32f, progress), 1.05f, 1f);
                trailImage.color = new Color(0.95f, 0.9f, 0.78f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 5f)));
                yield return null;
            }

            Destroy(trail);
            Vector3 blockPoint = Vector3.LerpUnclamped(EdgePoint(defenderRect, attackerStart), defenderRect.position, 0.16f);
            yield return StartCoroutine(PlayWarriorSingleSlash(parent, blockPoint, normalizedDirection, 0f, 0.22f));

            Vector3 bounceDirection = (-normalizedDirection + new Vector3(-normalizedDirection.y, normalizedDirection.x, 0f) * 0.72f).normalized;
            Vector3 bounceStart = blockPoint;
            Vector3 bounceEnd = blockPoint + bounceDirection * 220f + new Vector3(0f, 70f, 0f);
            float bounceDuration = 0.44f;
            elapsed = 0f;
            while (elapsed < bounceDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / bounceDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                swordRect.position = Vector3.LerpUnclamped(bounceStart, bounceEnd, eased) + new Vector3(0f, Mathf.Sin(progress * Mathf.PI) * 44f, 0f);
                swordRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + progress * 780f);
                swordRect.localScale = Vector3.one * Mathf.Lerp(0.95f, 0.56f, eased);
                swordImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(progress * 10f, (1f - progress) * 2.3f)));
                attackerRect.position = Vector3.LerpUnclamped(lungePoint, attackerStart, Mathf.Clamp01(eased * 1.15f));
                yield return null;
            }

            Destroy(sword);
            attackerRect.position = attackerStart;
            attackerRect.localScale = attackerScale;
            attackerRect.localRotation = attackerRotation;
            attackerRect.SetSiblingIndex(attackerSibling);
            attacker.SetLayoutIgnored(false);
        }

        private IEnumerator PlayWarriorSlashCombo(RectTransform parent, RectTransform attacker, RectTransform defender, Vector3 direction)
        {
            Vector3 target = Vector3.LerpUnclamped(EdgePoint(defender, attacker.position), defender.position, 0.34f);
            yield return StartCoroutine(PlayWarriorSingleSlash(parent, target + new Vector3(-18f, 18f, 0f), direction, -22f, 0.18f));
            yield return new WaitForSecondsRealtime(0.04f);
            yield return StartCoroutine(PlayWarriorSingleSlash(parent, target + new Vector3(22f, -10f, 0f), direction, 24f, 0.18f));
            yield return new WaitForSecondsRealtime(0.03f);
            yield return StartCoroutine(PlayWarriorSingleSlash(parent, target, direction, 0f, 0.2f));
        }

        private static IEnumerator PlayWarriorSingleSlash(RectTransform parent, Vector3 worldPosition, Vector3 direction, float offsetAngle, float duration)
        {
            GameObject slash = CreateOverlaySprite(
                parent,
                "Warrior Sword Slash",
                LoadWarriorSlashSprite(),
                new Vector2(310f, 184f),
                out RectTransform slashRect,
                out Image slashImage);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + offsetAngle;
            slashRect.position = worldPosition;
            slashRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                slashRect.localScale = new Vector3(Mathf.Lerp(0.72f, 1.26f, pulse), Mathf.Lerp(0.64f, 1.08f, pulse), 1f);
                slashRect.position = worldPosition + direction * Mathf.Lerp(-18f, 30f, progress);
                slashImage.color = new Color(1f, 0.96f, 0.82f, Mathf.Clamp01(Mathf.Min(progress * 11f, (1f - progress) * 7f)));
                yield return null;
            }

            Destroy(slash);
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

            Vector3 start = EdgePoint(attacker.RectTransform, defender.RectTransform.position);
            Vector3 end = EdgePoint(defender.RectTransform, attacker.RectTransform.position);
            Vector3 direction = end - start;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            float drawDuration = 0.16f;
            float flightDuration = 0.36f;
            float elapsed = 0f;
            Vector3 originalScale = attacker.RectTransform.localScale;
            while (elapsed < drawDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / drawDuration));
                attacker.RectTransform.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.08f, progress);
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
                arrowImage.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }

            attacker.RectTransform.localScale = originalScale;
            yield return StartCoroutine(PlayImpactPulse(defender.RectTransform));
            Destroy(arrowObject);
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

            float windupDuration = 0.2f;
            float elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / windupDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                attackerRect.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.08f, eased);
                crestRect.position = attackerRect.position + new Vector3(0f, 10f, 0f);
                crestRect.localRotation = Quaternion.Euler(0f, 0f, -28f + progress * 72f);
                crestRect.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.05f, eased);
                crestImage.color = new Color(1f, 0.91f, 0.42f, Mathf.Clamp01(Mathf.Min(progress * 7f, (1f - progress * 0.18f))));
                shieldRect.position = start - direction * 48f + new Vector3(0f, 18f, 0f);
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(0.68f, 1.02f, eased);
                shieldImage.color = new Color(1f, 0.96f, 0.72f, Mathf.Clamp01(progress * 6f));
                yield return null;
            }

            float flightDuration = 0.32f;
            elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / flightDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 arc = Vector3.up * Mathf.Sin(progress * Mathf.PI) * 36f;
                shieldRect.position = Vector3.LerpUnclamped(start, end, eased) + arc;
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + Mathf.Sin(progress * Mathf.PI) * 16f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.34f, Mathf.Sin(progress * Mathf.PI));
                shieldImage.color = new Color(1f, 0.96f, 0.72f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 10f)));

                crestRect.position = shieldRect.position - direction * 58f;
                crestRect.localRotation = Quaternion.Euler(0f, 0f, progress * 180f);
                crestRect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.28f, Mathf.Sin(progress * Mathf.PI));
                crestImage.color = new Color(1f, 0.88f, 0.34f, Mathf.Clamp01((1f - progress) * 0.85f));
                yield return null;
            }

            attackerRect.localScale = originalScale;
            Destroy(crest);
            Destroy(shield);
            yield return StartCoroutine(PlayPaladinHolyImpact(parent, defenderRect.position, blocked: false));
            yield return StartCoroutine(PlayImpactPulse(defenderRect));
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

            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.24f));
                float fade = Mathf.Clamp01(1f - Mathf.SmoothStep(0.68f, 1f, progress));
                float pulse = Mathf.Sin(progress * Mathf.PI);
                attackerRect.localScale = Vector3.LerpUnclamped(originalScale, originalScale * 1.05f, pulse);
                shieldRect.position = guardPoint + new Vector3(Mathf.Sin(progress * Mathf.PI * 7f) * 3f * fade, 0f, 0f);
                shieldRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                shieldRect.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.22f, appear) * (1f + pulse * 0.08f);
                shieldImage.color = new Color(1f, 0.96f, 0.7f, Mathf.Clamp01(appear * fade));

                crestRect.position = guardPoint;
                crestRect.localRotation = Quaternion.Euler(0f, 0f, progress * -160f);
                crestRect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.48f, appear);
                crestImage.color = new Color(1f, 0.82f, 0.26f, Mathf.Clamp01(appear * fade * 0.78f));
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

            int particleCount = blocked ? 24 : 18;
            var particles = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float spin, float scale)>(particleCount);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = CreateOverlaySprite(
                    parent,
                    blocked ? "Paladin Shield Shard" : "Paladin Radiant Spark",
                    blocked ? LoadPaladinShardSprite() : LoadPriestSparkSprite(),
                    new Vector2(blocked ? 72f : 44f, blocked ? 78f : 44f),
                    out RectTransform particleRect,
                    out Image particleImage);
                float angle = blocked
                    ? Mathf.Lerp(Mathf.PI * 0.08f, Mathf.PI * 0.92f, i / Mathf.Max(1f, particleCount - 1f)) + UnityEngine.Random.Range(-0.18f, 0.18f)
                    : (Mathf.PI * 2f * i / particleCount) + UnityEngine.Random.Range(-0.2f, 0.2f);
                float distance = UnityEngine.Random.Range(blocked ? 96f : 54f, blocked ? 218f : 142f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * (blocked ? 0.82f : 0.72f), 0f) * distance;
                if (blocked)
                    offset.y = Mathf.Abs(offset.y) + UnityEngine.Random.Range(-12f, 34f);
                particleRect.position = worldPosition;
                particleRect.localScale = Vector3.one * UnityEngine.Random.Range(0.72f, 1.2f);
                particleImage.color = new Color(1f, 0.9f, 0.48f, 0f);
                particles.Add((particle, particleRect, particleImage, offset, UnityEngine.Random.Range(-280f, 280f), UnityEngine.Random.Range(0.72f, 1.24f)));
            }

            float duration = blocked ? 0.56f : 0.42f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = 1f - eased;
                burstRect.localScale = Vector3.one * Mathf.Lerp(blocked ? 0.48f : 0.36f, blocked ? 1.62f : 1.34f, eased);
                burstRect.localRotation = Quaternion.Euler(0f, 0f, progress * (blocked ? -128f : 92f));
                burstImage.color = new Color(1f, 0.84f, 0.28f, Mathf.Clamp01(Mathf.Min(progress * 9f, fade * (blocked ? 1.05f : 0.88f))));

                for (int i = 0; i < particles.Count; i++)
                {
                    var particle = particles[i];
                    Vector3 drift = blocked
                        ? new Vector3(Mathf.Sin((progress + i * 0.21f) * Mathf.PI) * 18f, -progress * 24f, 0f)
                        : Vector3.zero;
                    particle.rect.position = worldPosition + particle.offset * eased + drift;
                    particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.spin * progress);
                    particle.rect.localScale = Vector3.one * Mathf.Lerp(particle.scale, particle.scale * 0.42f, eased);
                    particle.image.color = new Color(1f, 0.92f, 0.54f, Mathf.Clamp01(Mathf.Min(progress * 10f, fade * 1.22f)));
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
            yield return StartCoroutine(PlayNecromancerSkullOrbit(parent, defender.RectTransform, collapse: true));
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
            yield return StartCoroutine(PlayNecromancerSkullOrbit(parent, defender.RectTransform, collapse: false));
            yield return StartCoroutine(PlayNecromancerWardShatter(parent, defender.RectTransform.position));
        }

        private static IEnumerator PlayNecromancerSkullOrbit(RectTransform parent, RectTransform target, bool collapse)
        {
            const int skullCount = 8;
            Vector3 center = target.position;
            var skulls = new List<(GameObject skull, RectTransform skullRect, Image skullImage, GameObject trail, RectTransform trailRect, Image trailImage, float phase)>(skullCount);
            for (int i = 0; i < skullCount; i++)
            {
                GameObject trail = CreateOverlaySprite(
                    parent,
                    "Necromancer Skull Trail",
                    LoadNecromancerTrailSprite(),
                    new Vector2(220f, 86f),
                    out RectTransform trailRect,
                    out Image trailImage);
                GameObject skull = CreateOverlaySprite(
                    parent,
                    "Necromancer Orbiting Skull",
                    LoadNecromancerSkullSprite(),
                    new Vector2(96f, 96f),
                    out RectTransform skullRect,
                    out Image skullImage);
                float phase = (Mathf.PI * 2f * i / skullCount) + UnityEngine.Random.Range(-0.15f, 0.15f);
                skullImage.color = new Color(0.62f, 1f, 0.34f, 0f);
                trailImage.color = new Color(0.22f, 0.95f, 0.18f, 0f);
                skulls.Add((skull, skullRect, skullImage, trail, trailRect, trailImage, phase));
            }

            float duration = collapse ? 1.02f : 0.86f;
            float startRadius = 178f;
            float endRadius = collapse ? 22f : 122f;
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
                    item.skullImage.color = new Color(0.7f, 1f, 0.42f, Mathf.Clamp01(Mathf.Min(progress * 8f, fadeOut)));

                    item.trailRect.position = position - tangent * 64f;
                    item.trailRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                    item.trailRect.localScale = new Vector3(Mathf.Lerp(0.75f, 1.34f, Mathf.Sin(progress * Mathf.PI)), 1.12f, 1f);
                    item.trailImage.color = new Color(0.18f, 0.9f, 0.14f, Mathf.Clamp01(Mathf.Min(progress * 7f, fadeOut * 0.86f)));
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
                new Vector2(blocked ? 300f : 390f, blocked ? 300f : 390f),
                out RectTransform burstRect,
                out Image burstImage);
            burstRect.position = worldPosition;

            int particleCount = blocked ? 22 : 34;
            var particles = new List<(GameObject obj, RectTransform rect, Image image, Vector3 offset, float spin)>(particleCount);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = CreateOverlaySprite(
                    parent,
                    "Necromancer Soul Fragment",
                    LoadNecromancerTrailSprite(),
                    new Vector2(108f, 42f),
                    out RectTransform particleRect,
                    out Image particleImage);
                float angle = Mathf.PI * 2f * i / particleCount + UnityEngine.Random.Range(-0.18f, 0.18f);
                float distance = UnityEngine.Random.Range(blocked ? 98f : 126f, blocked ? 198f : 272f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.78f, 0f) * distance;
                particleRect.position = worldPosition;
                particleRect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                particleImage.color = new Color(0.3f, 1f, 0.18f, 0f);
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
                burstRect.localScale = Vector3.one * Mathf.Lerp(0.38f, blocked ? 1.62f : 2.08f, eased);
                burstRect.localRotation = Quaternion.Euler(0f, 0f, progress * (blocked ? -70f : 96f));
                burstImage.color = new Color(0.34f, 1f, 0.12f, Mathf.Clamp01(Mathf.Min(progress * 9f, fade * 1.18f)));

                for (int i = 0; i < particles.Count; i++)
                {
                    var particle = particles[i];
                    particle.rect.position = worldPosition + particle.offset * eased;
                    particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.spin * progress);
                    particle.rect.localScale = Vector3.one * Mathf.Lerp(0.96f, 0.32f, eased);
                    particle.image.color = new Color(0.26f, 0.95f, 0.13f, Mathf.Clamp01(Mathf.Min(progress * 10f, fade * 1.24f)));
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
                new Vector2(340f, 340f),
                out RectTransform wardRect,
                out Image wardImage);
            wardRect.position = worldPosition;

            float wardDuration = 0.28f;
            float elapsed = 0f;
            while (elapsed < wardDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / wardDuration);
                wardRect.localScale = Vector3.one * Mathf.Lerp(0.56f, 1.36f, Mathf.Sin(progress * Mathf.PI));
                wardRect.localRotation = Quaternion.Euler(0f, 0f, progress * -84f);
                wardImage.color = new Color(0.38f, 1f, 0.18f, Mathf.Clamp01(Mathf.Min(progress * 8f, (1f - progress) * 1.45f)));
                yield return null;
            }

            Destroy(ward);
            yield return PlayNecromancerGreenExplosion(parent, worldPosition, true);
        }

        public IEnumerator PlayRogueDaggerFlurry(PrototypeCardView attacker, PrototypeCardView defender, int attackMargin)
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
                StartCoroutine(PlayRogueDagger(parent, attacker.RectTransform, defender.RectTransform, i, blocked: false));
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

        private IEnumerator PlayRogueDagger(RectTransform parent, RectTransform attacker, RectTransform defender, int index, bool blocked)
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
            yield return StartCoroutine(PlayPriestHitCross(parent, target));
            yield return StartCoroutine(PlayHolySparkScatter(parent, target, blocked: false));
            yield return StartCoroutine(PlayImpactPulse(defender.RectTransform));
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
                new Vector2(280f, 280f),
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

        private static IEnumerator PlayAssassinDaggers(RectTransform parent, RectTransform target)
        {
            Vector3 center = target.position;
            GameObject left = CreateOverlaySprite(
                parent,
                "Assassin Left Dagger",
                LoadAssassinDaggerLeftSprite(),
                new Vector2(118f, 118f),
                out RectTransform leftRect,
                out Image leftImage);
            GameObject right = CreateOverlaySprite(
                parent,
                "Assassin Right Dagger",
                LoadAssassinDaggerRightSprite(),
                new Vector2(118f, 118f),
                out RectTransform rightRect,
                out Image rightImage);

            Vector3 leftStart = center + new Vector3(-88f, 94f, 0f);
            Vector3 rightStart = center + new Vector3(88f, 94f, 0f);
            Vector3 leftEnd = center + new Vector3(18f, -12f, 0f);
            Vector3 rightEnd = center + new Vector3(-18f, -12f, 0f);
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
                leftRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-38f, 18f, eased));
                rightRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(38f, -18f, eased));
                float scale = Mathf.Lerp(0.82f, 1.2f, Mathf.Sin(progress * Mathf.PI));
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

        private static Sprite LoadBarbarianGroundCrackSprite()
        {
            if (barbarianGroundCrackSprite != null)
                return barbarianGroundCrackSprite;

            barbarianGroundCrackSprite = Resources.Load<Sprite>("UI/glowing_runic_cracks_old");
            if (barbarianGroundCrackSprite != null)
                return barbarianGroundCrackSprite;

            barbarianGroundCrackSprite = Resources.Load<Sprite>("UI/glowing_runic_cracks");
            if (barbarianGroundCrackSprite != null)
                return barbarianGroundCrackSprite;

            return barbarianGroundCrackSprite = CreateFallbackBlobSprite("Barbarian Ground Crack Fallback", new Color32(255, 110, 18, 210));
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
