#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace AccardND.Presentation
{
    /// <summary>Avvia automaticamente il tour del primo level-up nelle scene debug.</summary>
    public sealed class LevelUpTalentTourDebugLauncher : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // La UI principale viene costruita a runtime: attendiamo che il pulsante
            // Profilo esista prima di chiedere al controller di avviare il tour.
            for (int frame = 0; frame < 300; frame++)
            {
                BattleBoardController controller = FindFirstObjectByType<BattleBoardController>();
                if (controller != null && controller.DebugStartFirstLevelTalentTour())
                    yield break;
                yield return null;
            }

            Debug.LogError("[LEVEL-UP TOUR DEBUG] UI non pronta: impossibile avviare il tour.");
        }
    }
}
#endif
