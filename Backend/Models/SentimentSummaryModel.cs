namespace Backend.Models
{
    public class SentimentSummary
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public string FinalSentimentSummary { get; set; }
        public float FinalSentimentScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ArticleSentimentsJson { get; set; } // To store article sentiments as JSON
    }

}
