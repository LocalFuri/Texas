namespace TexasHoldem
{
    public enum Suit { Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3 }

    public enum Rank
    {
        Two = 2, Three = 3, Four = 4, Five = 5, Six = 6,
        Seven = 7, Eight = 8, Nine = 9, Ten = 10,
        Jack = 11, Queen = 12, King = 13, Ace = 14
    }

    public class Card
    {
        public Suit Suit { get; }
        public Rank Rank { get; }
        public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public string RankSymbol() => Rank switch
        {
            Rank.Jack  => "J",
            Rank.Queen => "Q",
            Rank.King  => "K",
            Rank.Ace   => "A",
            Rank.Ten   => "10",
            _          => ((int)Rank).ToString()
        };

        public string SuitSymbol() => Suit switch
        {
            Suit.Clubs    => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts   => "♥",
            Suit.Spades   => "♠",
            _             => "?"
        };

        public override string ToString() => $"{RankSymbol()}{SuitSymbol()}";
    }
}
