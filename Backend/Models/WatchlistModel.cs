namespace Backend.Models
{
    public class Watchlist
    {
        public int Id { get; set; }
        public string UserId { get; set; } // Foreign key to the user
        public string Tickers { get; set; } // Comma-separated list of tickers
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow; // Track last update time
    }

    public class AddStockRequest
    {
        public string Ticker { get; set; }
    }

    public class RemoveFromWatchlistRequest
    {
        public string Ticker { get; set; }
    }



}


