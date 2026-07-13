using UnityEditor;
using UnityEngine;

namespace TexasHoldem
{
    public static class MonteCarloPerformanceBenchmarkMenu
    {
        private static MonoBehaviour _runner;

        [MenuItem("Texas Hold'em/Run Monte Carlo Performance Benchmark")]
        private static void RunBenchmark()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[MonteCarlo] Enter Play Mode, then run Texas Hold'em/Run Monte Carlo Performance Benchmark.");
                return;
            }

            if (_runner == null)
            {
                var go = new GameObject("MonteCarloPerformanceBenchmarkRunner");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<MonteCarloPerformanceBenchmarkRunner>();
            }

            _runner.StopAllCoroutines();
            _runner.StartCoroutine(MonteCarloSimulator.RunPerformanceBenchmark());
        }

        private sealed class MonteCarloPerformanceBenchmarkRunner : MonoBehaviour { }
    }
}
