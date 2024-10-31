namespace Backend.Models
{
    public class Trade
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public string SignalType { get; set; }
        public decimal EntryPoint { get; set; }
        public decimal StopLoss { get; set; }
        public decimal? InvalidatedPrice { get; set; }
        public decimal? TakeProfit { get; set; }
        public decimal? Sentiment { get; set; }
        public bool IsOpen { get; set; }
        public bool Invalidated { get; set; }
        public bool VolumeConfirmed { get; set; }
        public decimal? TotalProfit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Confidence { get; set; }
    }
}
