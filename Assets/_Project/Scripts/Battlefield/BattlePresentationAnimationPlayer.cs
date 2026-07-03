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
            bool defenderEliminated)
        {
            Vector3 attackerPoint = DuelWorldPoint(root, 0.43f);
            Vector3 defenderPoint = DuelWorldPoint(root, 0.57f);
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
                defenderPoint);
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
            Vector3 defenderPoint)
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
                defenderPoint));
            if (routine == null)
                routine = StartCoroutine(RunQueue());
        }

        public static VigorRollResult BuildRoll(int dieSides, int first, int second, bool hasSecond, int selected)
        {
            return new VigorRollResult(
                dieSides,
                first,
                second,
                hasSecond,
                selected,
                MatchupResult.Neutral,
                hasSecond ? VigorSelectionMode.Sum : VigorSelectionMode.Single);
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
            Vector3 defenderPoint)
        {
            if (attacker == null || defender == null)
                yield break;

            Canvas.ForceUpdateCanvases();
            yield return MoveToDuelPoints(
                attacker,
                defender,
                attackerPoint,
                defenderPoint,
                wait: 0.34f);

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

            yield return StartCoroutine(attacker.PlayAttackAnimation());
            yield return ReturnDuelParticipants(
                attacker,
                defender,
                returnAttacker: true,
                returnDefender: true,
                wait: 0.26f);
            if (defenderEliminated)
                yield return StartCoroutine(defender.PlayDefeatAnimation());
        }

        private static Vector3 DuelWorldPoint(RectTransform root, float xAnchor)
        {
            Rect rect = root.rect;
            return root.TransformPoint(new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, xAnchor),
                Mathf.Lerp(rect.yMin, rect.yMax, 0.58f),
                0f));
        }
    }
}
