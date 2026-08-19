using System;
using System.Collections;
using UnityEngine;

namespace AccardND.AudioKit
{
    /// <summary>
    /// Canale musicale autonomo: una sola AudioSource in loop, cambio traccia con
    /// dissolvenza, volume e mute persistiti su PlayerPrefs.
    ///
    /// Non dipende da alcun tipo di gioco: l'assembly AccardND.AudioKit non ha
    /// riferimenti, quindi questo componente e' riutilizzabile cosi' com'e' in un
    /// altro progetto Unity.
    /// </summary>
    public sealed class MusicChannel : MonoBehaviour
    {
        private const float DefaultFadeOutDuration = 1.2f;
        private const float SwitchFadeOutDuration = 0.45f;

        private AudioSource source;
        private Coroutine activeFade;
        private string volumeKey;
        private string mutedKey;
        private float volume = 0.75f;
        private bool muted;

        /// <summary>Scrive in console ogni cambio traccia. Attivo per default.</summary>
        public bool LogPlayback = true;

        /// <summary>Notificato a ogni variazione di volume o mute, per aggiornare la UI.</summary>
        public event Action Changed;

        public float Volume => volume;

        public bool Muted => muted;

        public AudioClip CurrentClip => source != null ? source.clip : null;

        public bool IsPlaying => source != null && source.isPlaying;

        /// <summary>
        /// Crea il canale come figlio di <paramref name="parent"/> e ne ripristina
        /// volume e mute dalle PlayerPrefs.
        /// </summary>
        public static MusicChannel Create(
            Transform parent,
            string volumePlayerPrefsKey,
            string mutedPlayerPrefsKey,
            float defaultVolume = 0.75f,
            string objectName = "Music Audio Source")
        {
            GameObject host = new GameObject(objectName);
            host.transform.SetParent(parent, false);

            MusicChannel channel = host.AddComponent<MusicChannel>();
            channel.volumeKey = volumePlayerPrefsKey;
            channel.mutedKey = mutedPlayerPrefsKey;
            channel.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(volumePlayerPrefsKey, defaultVolume));
            channel.muted = PlayerPrefs.GetInt(mutedPlayerPrefsKey, 0) != 0;

            AudioSource audioSource = host.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            channel.source = audioSource;
            channel.ApplyVolumeToSource();

            return channel;
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (volume > 0f)
            {
                muted = false;
            }
            Persist();
            ApplyVolumeToSource();
            Changed?.Invoke();
        }

        public void ToggleMute()
        {
            muted = !muted;
            Persist();
            ApplyVolumeToSource();
            Changed?.Invoke();
        }

        /// <summary>
        /// Avvia <paramref name="clip"/>. Se c'e' gia' una traccia in riproduzione
        /// sfuma quella corrente prima di sostituirla; se e' la stessa, non fa nulla.
        /// </summary>
        public void Play(AudioClip clip)
        {
            if (source == null || clip == null)
            {
                return;
            }

            StopFade();

            if (source.clip == clip && source.isPlaying)
            {
                ApplyVolumeToSource();
                return;
            }

            if (source.isPlaying)
            {
                activeFade = StartCoroutine(SwitchRoutine(clip, SwitchFadeOutDuration));
                return;
            }

            StartClip(clip);
        }

        /// <summary>Sfuma e ferma la traccia corrente con la durata di default.</summary>
        public void Stop()
        {
            FadeOut(DefaultFadeOutDuration);
        }

        /// <summary>
        /// Ferma subito la traccia e annulla ogni dissolvenza in corso, senza sfumare.
        /// Serve quando si esce da un contesto in modo netto e nessuna coroutine
        /// residua deve poter riscrivere il volume o ripartire sopra la traccia nuova.
        /// </summary>
        public void StopImmediate()
        {
            StopFade();
            if (source == null)
            {
                return;
            }
            source.Stop();
            source.clip = null;
            ApplyVolumeToSource();
        }

        public void FadeOut(float duration)
        {
            if (source == null || !source.isPlaying)
            {
                return;
            }
            StopFade();
            activeFade = StartCoroutine(FadeOutRoutine(Mathf.Max(0.01f, duration)));
        }

        private void StartClip(AudioClip clip)
        {
            source.clip = clip;
            source.loop = true;
            ApplyVolumeToSource();
            if (LogPlayback)
            {
                Debug.Log($"[Music] Riproduco '{clip.name}' (volume {source.volume:0.00})");
            }
            source.Play();
        }

        private IEnumerator SwitchRoutine(AudioClip clip, float duration)
        {
            yield return FadeVolumeToZero(duration);
            if (source != null)
            {
                source.Stop();
                StartClip(clip);
            }
            activeFade = null;
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            yield return FadeVolumeToZero(duration);
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                ApplyVolumeToSource();
            }
            activeFade = null;
        }

        private IEnumerator FadeVolumeToZero(float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration && source != null)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        private void StopFade()
        {
            if (activeFade == null)
            {
                return;
            }
            StopCoroutine(activeFade);
            activeFade = null;
            ApplyVolumeToSource();
        }

        private void ApplyVolumeToSource()
        {
            if (source == null)
            {
                return;
            }
            source.volume = muted ? 0f : volume;
        }

        private void Persist()
        {
            if (string.IsNullOrEmpty(volumeKey) || string.IsNullOrEmpty(mutedKey))
            {
                return;
            }
            PlayerPrefs.SetFloat(volumeKey, volume);
            PlayerPrefs.SetInt(mutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
