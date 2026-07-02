using System;
using System.Collections;
using UnityEngine;

namespace AccardND.Presentation
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFadeTransition : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private Coroutine transitionCoroutine;

        public bool IsPlaying => transitionCoroutine != null;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public void Play(
            Action changeSceneContent,
            float fadeOutDuration,
            float blackHoldDuration,
            float fadeInDuration)
        {
            if (transitionCoroutine != null)
                return;
            transitionCoroutine = StartCoroutine(TransitionRoutine(
                changeSceneContent,
                fadeOutDuration,
                blackHoldDuration,
                fadeInDuration));
        }

        private IEnumerator TransitionRoutine(
            Action changeSceneContent,
            float fadeOutDuration,
            float blackHoldDuration,
            float fadeInDuration)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            yield return Fade(0f, 1f, fadeOutDuration);

            try
            {
                changeSceneContent?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);
            yield return Fade(1f, 0f, fadeInDuration);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            transitionCoroutine = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                canvasGroup.alpha = Mathf.LerpUnclamped(from, to, progress);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
