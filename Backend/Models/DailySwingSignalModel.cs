namespace Backend.Models
{
    public class DailySwingSignal
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public string SignalType { get; set; }
        public decimal EntryPoint { get; set; }
        public decimal StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public bool IsOpen { get; set; }
        public decimal? TotalProfit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PassByCounter { get; set; }
    }

}
