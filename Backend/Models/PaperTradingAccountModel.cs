namespace Backend.Models
{
    public class PaperTradingAccount
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public decimal Balance { get; set; } // The cash balance of the account
        public decimal PortfolioValue { get; set; } // The value of open positions
        public decimal TotalValue { get; set; } // Balance + PortfolioValue
        public DateTime LastUpdated { get; set; } // Track the last time the values were updated
    }
}
