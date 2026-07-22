using System.Collections;
using UnityEngine;

namespace AccardND.Presentation
{
    public sealed class HintDebugScene : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;

            BattleBoardController controller = FindFirstObjectByType<BattleBoardController>();
            if (controller == null)
            {
                GameObject board = new GameObject("Accard N' Die - Battle Board");
                controller = board.AddComponent<BattleBoardController>();
                yield return null;
            }

            controller.ShowAllHintsForDebug();
        }
    }
}
