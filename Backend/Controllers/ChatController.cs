using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Backend.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly OpenAIClient _openAiClient;
        private readonly PolygonService _polygonService;
        private readonly SentimentAnalysisService _sentimentAnalysisService;

        public ChatController(OpenAIClient openAiClient, PolygonService polygonService, SentimentAnalysisService sentimentAnalysisService)
        {
            _openAiClient = openAiClient;
            _polygonService = polygonService;
            _sentimentAnalysisService = sentimentAnalysisService;
        }

        [HttpPost("message")]
        public async Task<IActionResult> ChatMessage([FromBody] ChatMessageRequest request)
        {
            var threadId = request.ThreadId;
            var message = request.Message;
            var noDataLookup = request.NoDataLookup;
            var userId = request.UserId;

            // Add current date to the request context
            string currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Step 1: Determine assistant IDs
            string firstAssistantId = "TEMP";   // Replace with your first assistant ID
            string secondAssistantId = "TEMP"; // Replace with your second assistant ID

            // Step 2: If threadId is null or empty, create a new thread
            if (string.IsNullOrEmpty(threadId) || threadId == "0")
            {
                threadId = await _openAiClient.CreateThreadAsync();
                if (string.IsNullOrEmpty(threadId))
                {
                    return BadRequest("Failed to create a conversation thread.");
                }
            }

            // Step 3: Add the user's message to the thread
            var messageId = await _openAiClient.AddMessageToThreadAsync(threadId, message);
            if (string.IsNullOrEmpty(messageId))
            {
                return BadRequest("Failed to send the message to the assistant.");
            }

            // Step 4: Create a run in the thread with the first assistant and specific instructions
            string firstAssistantInstructions = $@"
                You are an assistant that helps determine which data sources are needed to answer the user's query.
                The current date is {currentDate}, and you can use this information to provide time-sensitive data.
                Analyze the user's message and decide which data sources (e.g., stock_price, sentiment_analysis, sma, ema, macd, rsi, candlestick_data) and tickers are needed.
                You can also request specific candlestick data for different time frames (e.g., 15 latest daily candles, specific day/range of 15-minute candles, etc.).
                Always respond with a JSON object in the following format without any additional explanations:
                {{
                    'action': 'fetch_data',
                    'dataSources': ['stock_price', 'candlestick_data', 'sma', 'ema', 'macd', 'rsi'],
                    'tickers': ['AAPL', 'SPY'],
                    'timeframes': [
                        {{ 'timespan': 'minute', 'multiplier': 15, 'from': '2023-01-01', 'to': '2023-01-31' }},
                        {{ 'timespan': 'day', 'multiplier': 1, 'from': '2022-01-01', 'to': '2022-02-01' }}
                    ]
                }}
                Only include the necessary data points and make sure to structure the object as requested. Do not provide explanations outside of the JSON object.
                ";

            await _openAiClient.CreateRunAsync(threadId, firstAssistantId, firstAssistantInstructions);

            // Step 5: Wait for the assistant's response
            var assistantResponse = await _openAiClient.WaitForAssistantResponseAsync(threadId, firstAssistantId, messageId);
            if (string.IsNullOrEmpty(assistantResponse))
            {
                return BadRequest("The assistant did not provide a response.");
            }

            var cleanedResponse = assistantResponse.Trim('`');

            if (cleanedResponse.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                cleanedResponse = cleanedResponse.Substring(4).Trim();
            }

            // Step 6: Parse the assistant's response to determine which data sources to call
            JObject responseJson = null;
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    responseJson = JObject.Parse(cleanedResponse);
                    break; // Parsing successful, exit the loop
                }
                catch (JsonReaderException)
                {
                    retryCount++;

                    if (retryCount == maxRetries)
                    {
                        // Reset the thread
                        threadId = await _openAiClient.CreateThreadAsync();
                        await _openAiClient.AddMessageToThreadAsync(threadId, message);
                        retryCount = 0; // Reset retry count for the new thread
                        continue; // Try with the new thread
                    }

                    // Inform the assistant of the parsing error and retry
                    string errorMessage = $"Failed to parse output. Please ensure your response is in the correct JSON format. Nothing more. The response must be able to be parsed with: responseJson = JObject.Parse(YOUR_RESPONSE); Attempt {retryCount}/{maxRetries}.";
                    await _openAiClient.AddMessageToThreadAsync(threadId, errorMessage);
                    await _openAiClient.CreateRunAsync(threadId, firstAssistantId, firstAssistantInstructions);
                    assistantResponse = await _openAiClient.WaitForAssistantResponseAsync(threadId, firstAssistantId, messageId);
                    if (string.IsNullOrEmpty(assistantResponse))
                    {
                        return BadRequest("The assistant did not provide a response after multiple attempts.");
                    }
                    cleanedResponse = assistantResponse.Trim('`').Trim();
                    if (cleanedResponse.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanedResponse = cleanedResponse.Substring(4).Trim();
                    }
                }
            }

            var action = responseJson["action"]?.ToString();

            if (action == "fetch_data")
            {
                // Extract data sources, tickers, and timeframes
                var dataSources = responseJson["dataSources"]?.ToObject<List<string>>();
                var tickers = responseJson["tickers"]?.ToObject<List<string>>();
                var timeframes = responseJson["timeframes"]?.ToObject<List<TimeFrame>>();

                // Step 7: Fetch the data as per the assistant's response
                var data = new Dictionary<string, object>();

                if (dataSources != null && dataSources.Count > 0)
                {
                    if (tickers == null || tickers.Count == 0)
                    {
                        return BadRequest("No tickers specified by the assistant.");
                    }

                    foreach (var ticker in tickers)
                    {
                        var tickerData = new Dictionary<string, object>();

                        if (dataSources.Contains("stock_price"))
                        {
                            // Fetch stock prices
                            var stockData = await _polygonService.GetStocksDataFromPolygon(new List<string> { ticker });
                            tickerData["stockPrice"] = stockData != null && stockData.Count > 0 ? stockData[0] : null;
                        }
                        if (dataSources.Contains("sentiment_analysis"))
                        {
                            // Fetch news and sentiment from Polygon
                            var newsArticles = await _polygonService.GetNewsWithSentimentAsync(ticker);

                            if (newsArticles != null && newsArticles.Any())
                            {
                                var sentimentSummary = newsArticles
                                    .GroupBy(article => article.Sentiment)
                                    .OrderByDescending(group => group.Count())
                                    .First().Key;  // Get the dominant sentiment

                                tickerData["sentimentAnalysis"] = new
                                {
                                    Sentiment = sentimentSummary,
                                    News = newsArticles.Select(article => new
                                    {
                                        article.Title,
                                        article.Author,
                                        article.PublishedUtc,
                                        article.ArticleUrl,
                                        article.Sentiment,
                                        article.SentimentReasoning
                                    }).ToList()
                                };
                            }
                        }

                        if (dataSources.Contains("sma"))
                        {
                            // Fetch SMA data
                            var smaData = await _polygonService.GetSMAAsync(ticker);
                            tickerData["sma"] = smaData;
                        }
                        if (dataSources.Contains("ema"))
                        {
                            // Fetch EMA data
                            var emaData = await _polygonService.GetEMAAsync(ticker);
                            tickerData["ema"] = emaData;
                        }
                        if (dataSources.Contains("macd"))
                        {
                            // Fetch MACD data
                            var macdData = await _polygonService.GetMACDAsync(ticker);
                            tickerData["macd"] = macdData;
                        }
                        if (dataSources.Contains("rsi"))
                        {
                            // Fetch RSI data
                            var rsiData = await _polygonService.GetRSIAsync(ticker);
                            tickerData["rsi"] = rsiData;
                        }
                        if (dataSources.Contains("candlestick_data") && timeframes != null)
                        {
                            // Fetch candlestick data for each timeframe
                            var candlestickData = new Dictionary<string, List<Candlestick>>();

                            foreach (var timeframe in timeframes)
                            {
                                var candlesticks = await _polygonService.GetCandlestickDataAsync(
                                    ticker,
                                    timeframe.Multiplier,
                                    timeframe.Timespan,
                                    timeframe.From,
                                    timeframe.To
                                );

                                // Key is a combination of timespan and multiplier
                                candlestickData[$"{timeframe.Timespan}-{timeframe.Multiplier}"] = candlesticks;
                            }

                            tickerData["candlestickData"] = candlestickData;
                        }
                        // Add the ticker's data to the main data dictionary
                        data[ticker] = tickerData;
                    }
                }

                // Step 8: Compile the data into a message to send to the second assistant
                string dataMessage = $@"
                    Here's the relevant data for the tickers you requested:

                    {JsonConvert.SerializeObject(data, Formatting.Indented)}

Formatting guidelines:
- Use <ul> and <li> tags to make key data easily scannable as a list.
- Use <strong> tags to bold key information such as stock prices, important percentages, or action items.
- Use <p> tags for paragraphs to create clear separation between different pieces of advice.
- Ensure proper line breaks and spacing by using <br> where needed.

Be sure to focus on a specific response to this user input, using the information above: '{message}'
                    ";

                // Step 9: Add the data message to the thread
                var dataMessageId = await _openAiClient.AddMessageToThreadAsync(threadId, dataMessage);
                if (string.IsNullOrEmpty(dataMessageId))
                {
                    return BadRequest("Failed to send the data message to the assistant.");
                }

                // Step 10: Create a run in the thread with the second assistant and specific instructions
                string secondAssistantInstructions = @"
    You are a helpful assistant that provides concise, and friendly responses to inputs tailored around trading.";

                await _openAiClient.CreateRunAsync(threadId, secondAssistantId, secondAssistantInstructions);

                // Step 11: Wait for the assistant's response
                var finalAssistantResponse = await _openAiClient.WaitForAssistantResponseAsync(threadId, secondAssistantId, dataMessageId);
                if (string.IsNullOrEmpty(finalAssistantResponse))
                {
                    return BadRequest("The assistant did not provide a response.");
                }

                // Step 12: Return the assistant's response to the user
                return Ok(new { ThreadId = threadId, Message = finalAssistantResponse });
            }
            else
            {
                string warningMessage = "Coach Z responded with an unexpected output. Here is the response. This thread has been tagged and will be looked into to aid in correct output in the future, sorry for any inconvenience.";
                return Ok(new { ThreadId = threadId, Message = warningMessage + "\n\n" + responseJson.ToString() });

            }
        }
    }
}
