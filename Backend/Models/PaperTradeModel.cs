namespace Backend.Models
{
   
    public class PaperTrade
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public int Amount { get; set; }
        public decimal Price { get; set; }
        public string Type { get; set; } // "buy" or "sell"
        public string UserId { get; set; }
        public string Status { get; set; } // "open" or "closed"
        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal? ClosePrice { get; set; }
        public decimal? ProfitLoss { get; set; }
    }
    

    public class PaperTradeRequest
    {
        public string Ticker { get; set; }
        public int Amount { get; set; }
        public decimal Price { get; set; }
        public string Type { get; set; } // "buy" or "sell"
    }
}
