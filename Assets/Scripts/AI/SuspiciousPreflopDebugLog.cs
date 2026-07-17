using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Appends all-in hands (any street) and selected preflop spots to TexasHoldem_AI_Debug.txt
    /// next to the game executable. Observes only — does not affect AI decisions.
    /// </summary>
    public sealed class SuspiciousPreflopDebugLog
    {
        private const string FileName = "TexasHoldem_AI_Debug.txt";
        private const float CommitFractionThreshold = 0.5f;

        private static bool _pathLogged;

        private int  _handNumber;
        private bool _handActive;
        private bool _suspicious;

        private readonly List<PlayerSnapshot> _players = new List<PlayerSnapshot>(8);
        private readonly List<ActionLine>     _actions = new List<ActionLine>(64);
        private readonly List<string>         _postflopDecisions = new List<string>(32);
        private readonly Dictionary<string, int> _startStacks = new Dictionary<string, int>();

        private static SuspiciousPreflopDebugLog _activeHand;

        /// <summary>
        /// Records a postflop AI decision block for the active hand (file flush only if hand is logged).
        /// Observes only — safe to call from <see cref="AIController"/>.
        /// </summary>
        public static void RecordPostflopDecision(string decisionBlock)
        {
            if (_activeHand == null || string.IsNullOrEmpty(decisionBlock))
                return;

            _activeHand._postflopDecisions.Add(decisionBlock);
        }

        /// <summary>Logs the absolute debug file path once (Player.log / console).</summary>
        public void LogPathOnce()
        {
            if (_pathLogged)
                return;

            _pathLogged = true;
            Debug.Log($"[AIDebugLog] Writing to: {ResolveLogPath()}");
        }

        public void BeginHand(IReadOnlyList<PlayerState> players, Func<PlayerState, PreflopSeatBucket> seatOf)
        {
            _handNumber++;
            _handActive  = true;
            _suspicious  = false;
            _players.Clear();
            _actions.Clear();
            _postflopDecisions.Clear();
            _startStacks.Clear();
            _activeHand = this;

            if (players == null)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState p = players[i];
                if (p == null)
                    continue;

                _startStacks[p.Name] = p.Chips;
                _players.Add(new PlayerSnapshot(
                    p.Name,
                    seatOf != null ? seatOf(p) : PreflopSeatBucket.Early,
                    p.Chips,
                    FormatHoleCards(p.HoleCards),
                    ClassifyTier(p.HoleCards)));
            }
        }

        /// <summary>Refresh hole cards / tiers after the deal (BeginHand may run before cards are dealt).</summary>
        public void RefreshHoleCards(IReadOnlyList<PlayerState> players)
        {
            if (!_handActive || players == null)
                return;

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerSnapshot snap = _players[i];
                for (int j = 0; j < players.Count; j++)
                {
                    PlayerState p = players[j];
                    if (p == null || p.Name != snap.Name)
                        continue;

                    _players[i] = new PlayerSnapshot(
                        snap.Name,
                        snap.Position,
                        snap.StartingStack,
                        FormatHoleCards(p.HoleCards),
                        ClassifyTier(p.HoleCards));
                    break;
                }
            }
        }

        public void RecordAction(
            GamePhase street,
            PlayerState player,
            BettingAction action,
            int raiseAmount,
            int potAfter,
            int streetRaiseCount)
        {
            if (!_handActive || player == null)
                return;

            _actions.Add(new ActionLine(
                street,
                player.Name,
                action,
                raiseAmount,
                potAfter,
                streetRaiseCount,
                ClassifyTier(player.HoleCards)));

            // Any street: log when the player is all-in after the action (Raise/Call that empties stack).
            if (player.IsAllIn)
                _suspicious = true;

            if (street != GamePhase.PreFlop)
                return;

            if (streetRaiseCount >= 3)
                _suspicious = true;

            if (_startStacks.TryGetValue(player.Name, out int start)
                && start > 0
                && (start - player.Chips) > start * CommitFractionThreshold)
            {
                _suspicious = true;
            }
        }

        public void FlushIfSuspicious(IReadOnlyList<PlayerState> winners)
        {
            if (!_handActive)
                return;

            bool shouldWrite = _suspicious;
            _handActive = false;
            if (_activeHand == this)
                _activeHand = null;

            if (!shouldWrite)
                return;

            try
            {
                string path = ResolveLogPath();
                File.AppendAllText(path, FormatHandBlock(winners), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIDebugLog] Append failed: {ex.Message}");
            }
        }

        private string FormatHandBlock(IReadOnlyList<PlayerState> winners)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine();
            sb.AppendLine($"=== Hand #{_handNumber} (all-in / suspicious) ===");

            sb.Append("Players: ");
            for (int i = 0; i < _players.Count; i++)
            {
                if (i > 0)
                    sb.Append(" | ");

                PlayerSnapshot p = _players[i];
                sb.Append(
                    $"{p.Name} ({p.Position}, {p.HoleCards}, stack={p.StartingStack}, {p.Tier})");
            }

            sb.AppendLine();
            sb.AppendLine("Betting sequence:");

            for (int i = 0; i < _actions.Count; i++)
            {
                ActionLine a = _actions[i];
                sb.AppendLine(
                    $"  {a.Street} | {a.PlayerName} | {a.Action} | raise={a.RaiseAmount} | " +
                    $"pot={a.PotAfter} | streetRaiseCount={a.StreetRaiseCount} | tier={a.Tier}");
            }

            if (_postflopDecisions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Postflop decisions:");
                for (int i = 0; i < _postflopDecisions.Count; i++)
                {
                    sb.AppendLine(_postflopDecisions[i]);
                    sb.AppendLine("---");
                }
            }

            sb.Append("Winner: ");
            if (winners == null || winners.Count == 0)
            {
                sb.AppendLine("(none)");
            }
            else
            {
                for (int i = 0; i < winners.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(winners[i] != null ? winners[i].Name : "?");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Same folder as the Windows executable (parent of *_Data).
        /// In Editor, project root (parent of Assets).
        /// </summary>
        public static string ResolveLogPath()
        {
            string dir = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(dir))
                dir = ".";
            return Path.GetFullPath(Path.Combine(dir, FileName));
        }

        private static string FormatHoleCards(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count < 2)
                return "??";
            return $"{cards[0]}{cards[1]}";
        }

        private static PreflopHandGroup ClassifyTier(IReadOnlyList<Card> cards) =>
            PreflopStrategy.ClassifyHand(cards);

        private readonly struct PlayerSnapshot
        {
            public string            Name          { get; }
            public PreflopSeatBucket Position      { get; }
            public int               StartingStack { get; }
            public string            HoleCards     { get; }
            public PreflopHandGroup  Tier          { get; }

            public PlayerSnapshot(
                string name,
                PreflopSeatBucket position,
                int startingStack,
                string holeCards,
                PreflopHandGroup tier)
            {
                Name          = name;
                Position      = position;
                StartingStack = startingStack;
                HoleCards     = holeCards;
                Tier          = tier;
            }
        }

        private readonly struct ActionLine
        {
            public GamePhase        Street           { get; }
            public string           PlayerName       { get; }
            public BettingAction    Action           { get; }
            public int              RaiseAmount      { get; }
            public int              PotAfter         { get; }
            public int              StreetRaiseCount { get; }
            public PreflopHandGroup Tier             { get; }

            public ActionLine(
                GamePhase street,
                string playerName,
                BettingAction action,
                int raiseAmount,
                int potAfter,
                int streetRaiseCount,
                PreflopHandGroup tier)
            {
                Street           = street;
                PlayerName       = playerName ?? string.Empty;
                Action           = action;
                RaiseAmount      = raiseAmount;
                PotAfter         = potAfter;
                StreetRaiseCount = streetRaiseCount;
                Tier             = tier;
            }
        }
    }
}
