using System.Collections;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Central handler for all keyboard input actions.
    /// Add a paired [Range(0f, 1f)] volume field for every new AudioClip field.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class KeyboardHandler : MonoBehaviour
    {
        [Header("Escape Key")]
        [SerializeField] private AudioClip _escapeClip;
        [SerializeField] [Range(0f, 1f)] private float _escapeVolume = 1f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        private void Update()
        {
            HandleEscape();
        }

        private void HandleEscape()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            PlayClip(_escapeClip, _escapeVolume);
            StartCoroutine(QuitAfterClip(_escapeClip));
        }

        private IEnumerator QuitAfterClip(AudioClip clip)
        {
            float delay = clip != null ? clip.length : 0f;
            yield return new WaitForSeconds(delay);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Plays a one-shot clip at the given volume through the shared AudioSource.</summary>
        private void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                Debug.LogWarning("KeyboardHandler: AudioClip is not assigned.");
                return;
            }

            _audioSource.PlayOneShot(clip, volume);
        }
    }
}
