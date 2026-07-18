using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Appends a complete betting protocol for every hand to TexasHoldem_AI_Debug.txt
    /// next to the game executable. Observes only — does not affect AI decisions.
    /// </summary>
    public sealed class SuspiciousPreflopDebugLog
    {
        private const string FileName = "TexasHoldem_AI_Debug.txt";

        private const string FileHeader =
            "Texas Hold'em AI Debug Log\n" +
            "==========================\n" +
            "\n" +
            "(Complete hand protocols appended below.)\n";

        /// <summary>
        /// When true, overwrite <c>TexasHoldem_AI_Debug.txt</c> with the header at startup.
        /// When false, keep existing contents and append as usual.
        /// </summary>
        public static bool ClearDebugLogOnStartup = true;

        private static bool _pathLogged;

        private int  _handNumber;
        private bool _handActive;

        private readonly List<PlayerSnapshot> _players = new List<PlayerSnapshot>(8);
        private readonly List<ActionLine>     _actions = new List<ActionLine>(64);
        private readonly List<string>         _postflopDecisions = new List<string>(32);
        private readonly Dictionary<string, int> _startStacks = new Dictionary<string, int>();

        private string _flopBoard  = string.Empty;
        private string _turnBoard  = string.Empty;
        private string _riverBoard = string.Empty;

        private static SuspiciousPreflopDebugLog _activeHand;

        /// <summary>
        /// Records a postflop AI decision block for the active hand.
        /// Observes only — safe to call from <see cref="AIController"/>.
        /// </summary>
        public static void RecordPostflopDecision(string decisionBlock)
        {
            if (_activeHand == null || string.IsNullOrEmpty(decisionBlock))
                return;

            _activeHand._postflopDecisions.Add(decisionBlock);
        }

        /// <summary>Logs the absolute debug file path once; optionally clears the file to header only.</summary>
        public void LogPathOnce()
        {
            if (_pathLogged)
                return;

            _pathLogged = true;
            string path = ResolveLogPath();
            Debug.Log($"[AIDebugLog] Writing to: {path}");

            if (!ClearDebugLogOnStartup)
                return;

            try
            {
                File.WriteAllText(path, FileHeader, Encoding.UTF8);
                Debug.Log("[AIDebugLog] Cleared log file (header only).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIDebugLog] Clear on startup failed: {ex.Message}");
            }
        }

        public void BeginHand(IReadOnlyList<PlayerState> players, Func<PlayerState, PreflopSeatBucket> seatOf)
        {
            _handNumber++;
            _handActive = true;
            _players.Clear();
            _actions.Clear();
            _postflopDecisions.Clear();
            _startStacks.Clear();
            _flopBoard  = string.Empty;
            _turnBoard  = string.Empty;
            _riverBoard = string.Empty;
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

        /// <summary>Records community cards revealed for a street (Flop / Turn / River).</summary>
        public void RecordBoard(GamePhase street, IReadOnlyList<Card> communityCards)
        {
            if (!_handActive)
                return;

            string formatted = FormatBoard(communityCards);
            switch (street)
            {
                case GamePhase.Flop:
                    _flopBoard = formatted;
                    break;
                case GamePhase.Turn:
                    _turnBoard = formatted;
                    break;
                case GamePhase.River:
                    _riverBoard = formatted;
                    break;
            }
        }

        public void RecordAction(
            GamePhase street,
            PlayerState player,
            BettingAction action,
            int amount,
            int potAfter,
            int tableBet,
            int playerStreetBet,
            int streetRaiseCount)
        {
            if (!_handActive || player == null)
                return;

            _actions.Add(new ActionLine(
                street,
                player.Name,
                action,
                amount,
                potAfter,
                tableBet,
                playerStreetBet,
                streetRaiseCount,
                ClassifyTier(player.HoleCards)));
        }

        /// <summary>
        /// Writes the complete hand protocol to disk (every hand, including normal showdowns).
        /// </summary>
        public void FlushIfSuspicious(
            IReadOnlyList<PlayerState> winners,
            IReadOnlyList<Card> finalBoard = null,
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands = null,
            int potAwarded = 0,
            int grossPot = 0)
        {
            if (!_handActive)
                return;

            _handActive = false;
            if (_activeHand == this)
                _activeHand = null;

            try
            {
                string path = ResolveLogPath();
                File.AppendAllText(
                    path,
                    FormatHandBlock(winners, finalBoard, showdownHands, potAwarded, grossPot),
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIDebugLog] Append failed: {ex.Message}");
            }
        }

        private string FormatHandBlock(
            IReadOnlyList<PlayerState> winners,
            IReadOnlyList<Card> finalBoard,
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands,
            int potAwarded,
            int grossPot)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine();
            sb.AppendLine($"=== Hand #{_handNumber} ===");

            sb.AppendLine("Players:");
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerSnapshot p = _players[i];
                sb.AppendLine(
                    $"  {p.Name} | pos={p.Position} | hole={p.HoleCards} | " +
                    $"startStack={p.StartingStack} | tier={p.Tier}");
            }

            sb.AppendLine("Board by street:");
            sb.AppendLine($"  Flop:  {(string.IsNullOrEmpty(_flopBoard) ? "(none)" : _flopBoard)}");
            sb.AppendLine($"  Turn:  {(string.IsNullOrEmpty(_turnBoard) ? "(none)" : _turnBoard)}");
            sb.AppendLine($"  River: {(string.IsNullOrEmpty(_riverBoard) ? "(none)" : _riverBoard)}");

            sb.AppendLine("Betting sequence:");
            if (_actions.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                for (int i = 0; i < _actions.Count; i++)
                {
                    ActionLine a = _actions[i];
                    sb.AppendLine(
                        $"  {a.Street} | {a.PlayerName} | {a.Action} | amount={a.Amount} | " +
                        $"pot={a.PotAfter} | tableBet={a.TableBet} | " +
                        $"playerStreetBet={a.PlayerStreetBet} | streetRaiseCount={a.StreetRaiseCount} | " +
                        $"tier={a.Tier}");
                }
            }

            sb.AppendLine("Postflop AI decisions:");
            if (_postflopDecisions.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                for (int i = 0; i < _postflopDecisions.Count; i++)
                {
                    sb.AppendLine(_postflopDecisions[i]);
                    sb.AppendLine("---");
                }
            }

            string finalBoardText = FormatBoard(finalBoard);
            if (string.IsNullOrEmpty(finalBoardText))
            {
                if (!string.IsNullOrEmpty(_riverBoard))
                    finalBoardText = _riverBoard;
                else if (!string.IsNullOrEmpty(_turnBoard))
                    finalBoardText = _turnBoard;
                else if (!string.IsNullOrEmpty(_flopBoard))
                    finalBoardText = _flopBoard;
                else
                    finalBoardText = "(none)";
            }

            sb.AppendLine($"Final board: {finalBoardText}");

            sb.AppendLine("Showdown hands:");
            if (showdownHands == null || showdownHands.Count == 0)
            {
                sb.AppendLine("  (none — folded out / no showdown)");
            }
            else
            {
                for (int i = 0; i < showdownHands.Count; i++)
                {
                    (PlayerState player, HandResult result) = showdownHands[i];
                    string name = player != null ? player.Name : "?";
                    string hole = player != null ? FormatHoleCards(player.HoleCards) : "??";
                    if (result == null)
                    {
                        sb.AppendLine($"  {name} | hole={hole} | result=(null)");
                        continue;
                    }

                    sb.AppendLine(
                        $"  {name} | hole={hole} | {result.Rank} [{string.Join(",", result.Tiebreakers)}]");
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

            if (grossPot > 0 || potAwarded > 0)
                sb.AppendLine($"Pot: gross={grossPot} awarded={potAwarded}");
            else
                sb.AppendLine("Pot: (n/a)");

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
            return $"{cards[0]} {cards[1]}";
        }

        private static string FormatBoard(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return string.Empty;

            var parts = new string[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                parts[i] = cards[i] != null ? cards[i].ToString() : "?";
            return string.Join(" ", parts);
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
            public int              Amount           { get; }
            public int              PotAfter         { get; }
            public int              TableBet         { get; }
            public int              PlayerStreetBet  { get; }
            public int              StreetRaiseCount { get; }
            public PreflopHandGroup Tier             { get; }

            public ActionLine(
                GamePhase street,
                string playerName,
                BettingAction action,
                int amount,
                int potAfter,
                int tableBet,
                int playerStreetBet,
                int streetRaiseCount,
                PreflopHandGroup tier)
            {
                Street           = street;
                PlayerName       = playerName ?? string.Empty;
                Action           = action;
                Amount           = amount;
                PotAfter         = potAfter;
                TableBet         = tableBet;
                PlayerStreetBet  = playerStreetBet;
                StreetRaiseCount = streetRaiseCount;
                Tier             = tier;
            }
        }
    }
}
