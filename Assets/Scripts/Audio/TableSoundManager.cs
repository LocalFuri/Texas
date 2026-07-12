using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Plays table SFX with per-clip volume sliders (Inspector on GameManager).</summary>
    [RequireComponent(typeof(AudioSource))]
    public class TableSoundManager : MonoBehaviour
    {
      [Header("Blinds")]
      [SerializeField] private AudioClip _blindClip;
      [SerializeField][Range(0f, 1f)] private float _blindVolume = 1f;

     [Header("Fold")]
      [SerializeField] private AudioClip _foldClip;
      [SerializeField][Range(0f, 1f)] private float _foldVolume = 1f;
      
      [Header("Knock Knock")]
      [SerializeField] private AudioClip _knockKnockClip;
      [SerializeField][Range(0f, 1f)] private float _knockKnockVolume = 1f;

      [Header("Large Bet")]
      [SerializeField] private AudioClip _largeBetClip;
      [SerializeField][Range(0f, 1f)] private float _largeBetVolume = 1f;

     [Header("Loop")]
     [SerializeField] private AudioClip _loopClip;
     [SerializeField][Range(0f, 1f)] private float _loopVolume = 1f;

    [Header("Small Bet")]
      [SerializeField] private AudioClip _smallBetClip;
      [SerializeField] [Range(0f, 1f)] private float _smallBetVolume = 1f;

      [Header("Winner Chips")]
      [SerializeField] private AudioClip _winnerChipClip;
      [SerializeField] [Range(0f, 1f)] private float _winnerChipVolume = 1f;

        private AudioSource  _audioSource;
        private GameManager  _gameManager;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _gameManager ??= GetComponent<GameManager>();
        }

        private void OnEnable()
        {
            _gameManager ??= GetComponent<GameManager>();
        }

        private void Start()
        {
            _gameManager ??= GetComponent<GameManager>();
            if (_gameManager != null)
                _gameManager.OnPlayerAction.AddListener(OnPlayerAction);
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
                _gameManager.OnPlayerAction.RemoveListener(OnPlayerAction);
        }

    
        public void PlayLoop() => PlayClip(_loopClip, _loopVolume);
        public void PlayBlind() => PlayClip(_blindClip, _blindVolume);
        public void PlayKnockKnock() => PlayClip(_knockKnockClip, _knockKnockVolume);

        /// <summary>Length of the check SFX in seconds (0 if unassigned).</summary>
        public float KnockKnockDuration => _knockKnockClip != null ? _knockKnockClip.length : 0f;

        public void PlaySmallBet() => PlayClip(_smallBetClip, _smallBetVolume);

        public void PlayLargeBet() => PlayClip(_largeBetClip, _largeBetVolume);

        public void PlayFold() => PlayClip(_foldClip, _foldVolume);

        public void PlayWinnerChip() => PlayClip(_winnerChipClip, _winnerChipVolume);

        private void OnPlayerAction(PlayerState player, BettingAction action, int amount)
        {
            if (_gameManager == null)
                return;

            switch (action)
            {
                case BettingAction.Check:
                    PlayKnockKnock();
                    break;

                case BettingAction.Fold:
                    PlayFold();
                    break;

                case BettingAction.Raise:
                    PlayActionBetSound(action, amount);
                    break;

                case BettingAction.Call:
                case BettingAction.AllIn:
                    if (amount <= 0)
                        break;

                    PlayActionBetSound(action, amount);
                    break;
            }
        }

        /// <summary>Preflop limp / complete-blind call (table still at BB) uses chip sound; raises use small bet.</summary>
        private bool IsPreflopCallMatchingBigBlind()
        {
            return _gameManager.CurrentPhase == GamePhase.PreFlop
                && _gameManager.CurrentBet <= _gameManager.BigBlindAmount;
        }

        private void PlayActionBetSound(BettingAction action, int amount)
        {
            if (_gameManager.CurrentPhase == GamePhase.PreFlop)
            {
                if ((action == BettingAction.Call || action == BettingAction.AllIn)
                    && IsPreflopCallMatchingBigBlind())
                {
                    PlayBlind();
                }
                else
                {
                    PlaySmallBet();
                }

                return;
            }

            switch (action)
            {
                case BettingAction.Raise:
                    PlayLargeBet();
                    break;

                default:
                    if (amount >= _gameManager.BigBlindAmount)
                        PlayLargeBet();
                    else
                        PlaySmallBet();
                    break;
            }
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null || _audioSource == null)
                return;

            _audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
