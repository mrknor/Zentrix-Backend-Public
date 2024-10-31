namespace Backend.Models
{
    // Core reusable models

    public class PriceAggregate
    {
        public decimal O { get; set; }  // Open price
        public decimal C { get; set; }  // Close price
        public decimal H { get; set; }  // High price
        public decimal L { get; set; }  // Low price
        public decimal V { get; set; }  // Volume
        public decimal Vw { get; set; } // Volume Weighted Average Price
        public long T { get; set; }     // Unix timestamp
        public int N { get; set; }      // Number of transactions
    }

    public class BaseResponse<T>
    {
        public string Status { get; set; }
        public string RequestId { get; set; }
        public int ResultsCount { get; set; }
        public List<T> Results { get; set; }
    }

    // Specialized Models

    // Aggregates Response
    public class AggregatesResponse : BaseResponse<PriceAggregate>
    {
        public string Ticker { get; set; }
        public bool Adjusted { get; set; }
        public int QueryCount { get; set; }
    }

    // Daily Open/Close Response
    public class DailyOpenCloseResponse
    {
        public string Symbol { get; set; }
        public string From { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal AfterHours { get; set; }
        public decimal PreMarket { get; set; }
        public long Volume { get; set; }
        public string Status { get; set; }
    }

    // Trade Response
    public class TradePoly
    {
        public int Exchange { get; set; }
        public string Id { get; set; }
        public long ParticipantTimestamp { get; set; }
        public decimal Price { get; set; }
        public long SipTimestamp { get; set; }
        public int Size { get; set; }
        public int SequenceNumber { get; set; }
        public List<int> Conditions { get; set; }
        public int Tape { get; set; }
    }

    public class TradeResponse : BaseResponse<TradePoly>
    {
    }

    // Quote Response
    public class Quote
    {
        public decimal AskPrice { get; set; }
        public int AskExchange { get; set; }
        public int AskSize { get; set; }
        public decimal BidPrice { get; set; }
        public int BidExchange { get; set; }
        public int BidSize { get; set; }
        public long ParticipantTimestamp { get; set; }
        public long SipTimestamp { get; set; }
        public int Tape { get; set; }
    }

    public class QuoteResponse : BaseResponse<Quote>
    {
    }

    // Snapshot Response
    public class SnapshotDay
    {
        public decimal C { get; set; }  // Close price
        public decimal H { get; set; }  // High price
        public decimal L { get; set; }  // Low price
        public decimal O { get; set; }  // Open price
        public decimal V { get; set; }  // Volume
        public decimal Vw { get; set; } // Volume Weighted Average Price
    }

    public class SnapshotTicker
    {
        public string Ticker { get; set; }
        public SnapshotDay Day { get; set; }
        public decimal TodaysChange { get; set; }
        public decimal TodaysChangePerc { get; set; }
        public long Updated { get; set; }
    }

    public class SnapshotResponse : BaseResponse<SnapshotTicker>
    {
    }

    // Previous Close Response
    public class PreviousCloseResponse : BaseResponse<PriceAggregate>
    {
        public string Ticker { get; set; }
        public bool Adjusted { get; set; }
        public int QueryCount { get; set; }
    }

    // Gainers/Losers Response
    public class GainerLoserResponse : BaseResponse<SnapshotTicker>
    {
    }

    // Models for Indicator Responses
    public class IndicatorResponse
    {
        public string NextUrl { get; set; }
        public string RequestId { get; set; }
        public IndicatorResults Results { get; set; }
        public string Status { get; set; }
    }

    public class IndicatorResults
    {
        public UnderlyingData Underlying { get; set; }
        public List<IndicatorValue> Values { get; set; }
    }

    public class UnderlyingData
    {
        public string Url { get; set; }
        public List<PriceAggregate> Aggregates { get; set; }
    }

    public class IndicatorValue
    {
        public long Timestamp { get; set; }
        public decimal Value { get; set; }
    }

    public class MACDResponse
    {
        public string NextUrl { get; set; }
        public string RequestId { get; set; }
        public MACDResults Results { get; set; }
        public string Status { get; set; }
    }

    public class MACDResults
    {
        public UnderlyingData Underlying { get; set; }
        public List<MACDValue> Values { get; set; }
    }

    public class MACDValue
    {
        public long Timestamp { get; set; }
        public decimal Histogram { get; set; }
        public decimal Signal { get; set; }
        public decimal Value { get; set; }
    }

    public class NewsArticle
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime PublishedUtc { get; set; }
        public string ArticleUrl { get; set; }
        public string Sentiment { get; set; }
        public string SentimentReasoning { get; set; }
    }

    public class Candlestick
    {
        public string Ticker { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public long Volume { get; set; }
        public DateTime Timestamp { get; set; }
    }


}
