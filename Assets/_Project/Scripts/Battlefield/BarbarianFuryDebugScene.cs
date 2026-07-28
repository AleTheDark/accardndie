using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    public sealed class BarbarianFuryDebugScene : MonoBehaviour
    {
        [SerializeField] private RectTransform pawn;
        [SerializeField] private float replayDelay = 0.6f;

        private IEnumerator Start()
        {
            yield return null;
            while (enabled)
            {
                yield return BarbarianFuryVfx.Play(pawn);
                yield return new WaitForSecondsRealtime(replayDelay);
            }
        }

    }
}
