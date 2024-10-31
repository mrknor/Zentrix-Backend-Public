namespace Backend.Models
{
    public class ChatMessageRequest
    {
        public string ThreadId { get; set; }
        public string UserId { get; set; }
        public string Message { get; set; }
        public bool NoDataLookup { get; set; } = false;
    }

    public class TimeFrame
    {
        public string Timespan { get; set; }
        public int Multiplier { get; set; }
        public string From { get; set; }
        public string To { get; set; }
    }
}
