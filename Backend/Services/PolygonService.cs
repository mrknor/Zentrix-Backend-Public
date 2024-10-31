using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Backend.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;

namespace Backend.Services
{
    public class PolygonService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public PolygonService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // 1. Get Stocks Data
        public async Task<List<Stock>> GetStocksDataFromPolygon(List<string> tickers)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var tickerList = string.Join(",", tickers);
            var url = $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/tickers?tickers={tickerList}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var updatedStocks = new List<Stock>();

            foreach (var stockData in json["tickers"])
            {
                updatedStocks.Add(new Stock
                {
                    Ticker = stockData["ticker"].ToString(),
                    CurrentPrice = (decimal)stockData["day"]["c"],
                    Volume = (long)stockData["day"]["v"],
                    PreviousClose = (decimal)stockData["prevDay"]["c"],
                    ChangePercent = (decimal)stockData["todaysChangePerc"],
                    LastUpdated = DateTime.UtcNow
                });
            }

            return updatedStocks;
        }

        // 2. Get Daily Open/Close
        public async Task<DailyOpenCloseResponse> GetDailyOpenCloseAsync(string ticker, string date)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v1/open-close/{ticker}/{date}?apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<DailyOpenCloseResponse>();
        }

        // 3. Get Aggregates for Stock
        public async Task<AggregatesResponse> GetAggregatesAsync(string ticker, int multiplier, string timespan, string from, string to, bool adjusted = true)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{from}/{to}?adjusted={adjusted}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<AggregatesResponse>();
        }

        // 4. Get Previous Close
        public async Task<PreviousCloseResponse> GetPreviousCloseAsync(string ticker, bool adjusted = true)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v2/aggs/ticker/{ticker}/prev?adjusted={adjusted}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<PreviousCloseResponse>();
        }

        // 5. Get Gainers or Losers
        public async Task<GainerLoserResponse> GetGainersOrLosersAsync(string direction = "gainers")
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/{direction}?apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<GainerLoserResponse>();
        }

        // 6. Get Quotes for a Ticker
        public async Task<QuoteResponse> GetQuotesAsync(string ticker, int limit = 1000, string sort = "asc")
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v3/quotes/{ticker}?limit={limit}&sort={sort}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<QuoteResponse>();
        }

        // 7. Get Trades for a Ticker
        public async Task<TradeResponse> GetTradesAsync(string ticker, int limit = 1000, string sort = "asc")
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v3/trades/{ticker}?limit={limit}&sort={sort}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<TradeResponse>();
        }

        // 8. Get Simple Moving Average (SMA)
        public async Task<IndicatorResponse> GetSMAAsync(string ticker, int window = 50, string timespan = "day", int limit = 10)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v1/indicators/sma/{ticker}?timespan={timespan}&window={window}&order=desc&limit={limit}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<IndicatorResponse>();
        }

        // 9. Get Exponential Moving Average (EMA)
        public async Task<IndicatorResponse> GetEMAAsync(string ticker, int window = 50, string timespan = "day", int limit = 10)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v1/indicators/ema/{ticker}?timespan={timespan}&window={window}&order=desc&limit={limit}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<IndicatorResponse>();
        }

        // 10. Get Moving Average Convergence/Divergence (MACD)
        public async Task<MACDResponse> GetMACDAsync(string ticker, int shortWindow = 12, int longWindow = 26, int signalWindow = 9, string timespan = "day", int limit = 10)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v1/indicators/macd/{ticker}?timespan={timespan}&short_window={shortWindow}&long_window={longWindow}&signal_window={signalWindow}&order=desc&limit={limit}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<MACDResponse>();
        }

        // 11. Get Relative Strength Index (RSI)
        public async Task<IndicatorResponse> GetRSIAsync(string ticker, int window = 14, string timespan = "day", int limit = 10)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v1/indicators/rsi/{ticker}?timespan={timespan}&window={window}&order=desc&limit={limit}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content).ToObject<IndicatorResponse>();
        }

        public async Task<List<Candlestick>> GetCandlestickDataAsync(string ticker, int multiplier, string timespan, string from, string to, int limit = 1000, bool adjusted = true)
        {
            // Parse the from and to dates to ensure they are in "YYYY-MM-DD" format
            var parsedFromDate = DateTime.Parse(from).ToString("yyyy-MM-dd");
            var parsedToDate = DateTime.Parse(to).ToString("yyyy-MM-dd");

            // Construct the URL using the formatted from and to dates
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{parsedFromDate}/{parsedToDate}?adjusted={adjusted}&sort=asc&limit={limit}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var candlesticks = new List<Candlestick>();

            foreach (var result in json["results"])
            {

                var utcTimestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)result["t"]).UtcDateTime;

                // Convert UTC time to New York time (Eastern Time Zone)
                TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var newYorkTime = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, easternZone);

                candlesticks.Add(new Candlestick
                {
                    Ticker = ticker,
                    Open = (decimal)result["o"],
                    Close = (decimal)result["c"],
                    High = (decimal)result["h"],
                    Low = (decimal)result["l"],
                    Volume = (long)result["v"],
                    Timestamp = newYorkTime
                });
            }

            return candlesticks;
        }


        // 12. Get News and Sentiment for a Ticker
        public async Task<List<NewsArticle>> GetNewsWithSentimentAsync(string ticker, int limit = 10)
        {
            string _apiKey = _configuration["Polygon:ApiKey"];
            var url = $"https://api.polygon.io/v2/reference/news?ticker={ticker}&limit={limit}&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var newsArticles = new List<NewsArticle>();

            foreach (var result in json["results"])
            {
                var insights = result["insights"]?.FirstOrDefault();
                newsArticles.Add(new NewsArticle
                {
                    Title = result["title"]?.ToString(),
                    Author = result["author"]?.ToString(),
                    PublishedUtc = DateTime.Parse(result["published_utc"]?.ToString()),
                    ArticleUrl = result["article_url"]?.ToString(),
                    Sentiment = insights?["sentiment"]?.ToString(),
                    SentimentReasoning = insights?["sentiment_reasoning"]?.ToString(),
                });
            }

            return newsArticles;
        }

    }
}
