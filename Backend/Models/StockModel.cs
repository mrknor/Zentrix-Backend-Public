namespace Backend.Models
{
    public class Stock
    {
        public string Ticker { get; set; }
        public decimal CurrentPrice { get; set; }
        public long Volume { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal ChangePercent { get; set; }
        public DateTime LastUpdated { get; set; }
    }

}
