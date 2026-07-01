using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Updates a TextMeshProUGUI label with the current local time on every minute boundary.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class ClockLabel : MonoBehaviour
    {
        private const string TimeFormat = "HH:mm"; //HH military time

        private TextMeshProUGUI _label;

        private void Awake()
        {
            _label = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            StartCoroutine(ClockRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        /// <summary>
        /// Displays the current time immediately, then waits until each successive
        /// minute boundary before updating again. Recalculates the delay from
        /// DateTime.Now each iteration so drift never accumulates.
        /// </summary>
        private IEnumerator ClockRoutine()
        {
            while (true)
            {
                UpdateLabel();
                yield return new WaitForSecondsRealtime(SecondsUntilNextMinute());
            }
        }

        /// <summary>
        /// Writes the current local time into the label.
        /// </summary>
        private void UpdateLabel()
        {
            _label.text = DateTime.Now.ToString(TimeFormat);
        }

        /// <summary>
        /// Returns the number of seconds remaining until the next full minute.
        /// </summary>
        private static float SecondsUntilNextMinute()
        {
            DateTime now = DateTime.Now;
            return 60f - now.Second - now.Millisecond / 1000f;
        }
    }
}
