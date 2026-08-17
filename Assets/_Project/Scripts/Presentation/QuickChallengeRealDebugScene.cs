using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;

namespace AccardND.Presentation
{
    /// <summary>
    /// Avvia la stanza Quick Challenge con la stessa entrata usata in campagna:
    /// il giocatore può soltanto iniziare una sfida estratta casualmente o rinunciare.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuickChallengeRealDebugScene : MonoBehaviour
    {
        private int rewardSeed = 7319;

        private void Awake()
        {
            if (GetComponent<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            StartRoom();
        }

        private void StartRoom()
        {
            QuickChallengeRoomDebugScene room = gameObject.AddComponent<QuickChallengeRoomDebugScene>();
            room.ConfigureForCampaign(
                RollDebugReward,
                (result, completedLevels) => StartCoroutine(RestartRoom()));
        }

        private FlashTrialSlotOutcome? RollDebugReward(FlashTrialResult result, int completedLevels)
        {
            if (result == FlashTrialResult.Forfeited)
                return null;

            rewardSeed++;
            var machine = new FlashTrialSlotMachine(rewardSeed);
            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            List<FlashTrialCardCandidate> candidates = database == null
                ? new List<FlashTrialCardCandidate>()
                : database.Cards
                    .Where(card => card != null && card.Category == CardCategory.Monster && card.CanEnterCombat)
                    .Select(card => new FlashTrialCardCandidate(card.Id, card.HeroClass, card.Strength))
                    .ToList();
            return candidates.Count > 0
                ? machine.Roll(result, Mathf.Max(0, completedLevels), candidates)
                : machine.Roll(result, Mathf.Max(0, completedLevels));
        }

        private IEnumerator RestartRoom()
        {
            QuickChallengeRoomDebugScene room = GetComponent<QuickChallengeRoomDebugScene>();
            if (room != null)
                Destroy(room);

            for (int index = transform.childCount - 1; index >= 0; index--)
                Destroy(transform.GetChild(index).gameObject);

            yield return null;
            StartRoom();
        }
    }
}
