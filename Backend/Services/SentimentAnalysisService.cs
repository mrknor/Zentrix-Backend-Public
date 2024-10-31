using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;
using Microsoft.Extensions.Configuration;
using OpenAI_API;
using OpenAI_API.Chat;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Backend.Data;

namespace Backend.Services
{
    public class SentimentAnalysisService
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAiClient;
        private const int MaxRetries = 10;
        private const int TimeoutSeconds = 20;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // Semaphore for singleton instance
        private bool isRunning = false;
        private readonly StockDbContext _dbContext;

        public SentimentAnalysisService(IConfiguration configuration, OpenAIClient openAiClient, StockDbContext dbContext)
        {
            _configuration = configuration;
            _openAiClient = openAiClient;
            _dbContext = dbContext;
        }

        public async Task<SentimentSummaryResult> SummarizeSentimentAsync(string query, string ticker)
        {
            if (isRunning)
            {
                Console.WriteLine("SummarizeSentimentAsync is already running.");
                return null;
            }

            await _semaphore.WaitAsync();
            try
            {
                isRunning = true;
                Console.WriteLine($"Summarizing sentiment for query: {query}");

                // Check if a recent sentiment summary exists for the ticker
                var existingSentiment = await _dbContext.SentimentSummaries
                    .Where(s => s.Ticker == ticker && s.CreatedAt > DateTime.Now.AddHours(-24))
                    .FirstOrDefaultAsync();

                if (existingSentiment != null)
                {
                    // Return the existing sentiment
                    return new SentimentSummaryResult
                    {
                        FinalSentimentScore = existingSentiment.FinalSentimentScore,
                        FinalSentimentSummary = existingSentiment.FinalSentimentSummary,
                        ArticleSentiments = JsonConvert.DeserializeObject<List<SentimentResult>>(existingSentiment.ArticleSentimentsJson)
                    };
                }

                // Generate new sentiment as per original method
                var threadId = await _openAiClient.CreateThreadAsync();
                var articles = await GetNewsArticlesAsync(query, DateTime.Now.AddDays(-7));
                var relevantIndexes = await FilterRelevantArticlesAsync(articles, ticker, threadId);

                if (!relevantIndexes.Any())
                {
                    Console.WriteLine("No relevant articles found.");
                    return new SentimentSummaryResult();
                }

                var validArticles = await ScrapeRecommendedArticlesAsync(articles, relevantIndexes);
                var sentimentResults = await AnalyzeEachArticleSentimentAsync(validArticles, ticker, threadId);
                var (finalScore, finalSummary) = await GetFinalSentimentAnalysisAsync(sentimentResults, ticker, threadId);

                Console.WriteLine($"Final Sentiment Score: {finalScore}");
                Console.WriteLine($"Final Sentiment Summary: {finalSummary}");

                // Store the new sentiment in the database
                var newSentiment = new SentimentSummary
                {
                    Ticker = ticker,
                    FinalSentimentScore = finalScore,
                    FinalSentimentSummary = finalSummary,
                    CreatedAt = DateTime.Now,
                    ArticleSentimentsJson = JsonConvert.SerializeObject(sentimentResults)
                };

                _dbContext.SentimentSummaries.Add(newSentiment);
                await _dbContext.SaveChangesAsync();

                return new SentimentSummaryResult
                {
                    ArticleSentiments = sentimentResults,
                    FinalSentimentScore = finalScore,
                    FinalSentimentSummary = finalSummary
                };
            }
            finally
            {
                isRunning = false;
                _semaphore.Release();
            }
        }


        // Step 1: Fetch articles using NewsAPI
        public async Task<List<Article>> GetNewsArticlesAsync(string query, DateTime fromDate)
        {
            var apiKey = _configuration["News:ApiKey"];
            var newsApiClient = new NewsApiClient(apiKey);

            var articlesResponse = newsApiClient.GetEverything(new EverythingRequest
            {
                Q = query,
                SortBy = SortBys.Relevancy,
                Language = Languages.EN,
                From = fromDate,
                Page = 1
            });

            if (articlesResponse.Status == Statuses.Ok)
            {
                return articlesResponse.Articles
                    .Select(article => new Article
                    {
                        Title = article.Title,
                        Link = article.Url,
                        Snippet = article.Description,
                        Date = article.PublishedAt ?? DateTime.Now
                    })
                    .ToList();
            }

            return new List<Article>();
        }

        // Step 2: Use AI Assistant to filter relevant articles (in the same thread)
        private async Task<List<int>> FilterRelevantArticlesAsync(List<Article> articles, string ticker, string threadId)
        {
            // Create a list of articles in the format: "1. Article Title"
            var articlesList = string.Join("\n", articles.Select((article, index) => $"{index + 1}. {article.Title}"));

            // Define the strict prompt for the AI
            var prompt = (
                $"Here is a list of news articles about {ticker}. " +
                $"Please return a comma-separated list of indexes for the articles most relevant to understanding the stock's market sentiment. " +
                $"Do not include irrelevant articles. Ensure the output follows this exact format:\n\n" +
                $"Relevant Articles: X, Y, Z\n\n" +
                $"Here is the list of articles:\n\n{articlesList}\n\n" +
                $"Return only the indexes of the relevant articles in the format 'Relevant Articles: X, Y, Z' without deviation."
            );

            // Send the prompt to the AI assistant
            var messageId = await _openAiClient.AddMessageToThreadAsync(threadId, prompt);

            var assistanId = "asst_r9g2QFg5TkyFV"; //fake XD

            // Create a run to filter relevant articles
            await _openAiClient.CreateRunAsync(threadId, assistanId, "Filter relevant articles");

            // Wait for the assistant's response
            var response = await _openAiClient.WaitForAssistantResponseAsync(threadId, assistanId, messageId);

            // Parse the response to extract relevant article indexes
            List<int> relevantIndexes = new List<int>();

            foreach (var line in response.Split('\n'))
            {
                var trimmedLine = line.Trim();

                // Extract the relevant article indexes if the line starts with "Relevant Articles:"
                if (trimmedLine.StartsWith("Relevant Articles:"))
                {
                    var indexesPart = trimmedLine.Split("Relevant Articles:")[1].Trim();
                    relevantIndexes = indexesPart.Split(',')
                                                 .Select(index => int.Parse(index.Trim()))
                                                 .ToList();
                }
            }

            return relevantIndexes;
        }


        // Step 3: Scrape the recommended articles
        private async Task<List<Article>> ScrapeRecommendedArticlesAsync(List<Article> articles, List<int> relevantIndexes)
        {
            var validArticles = new List<Article>();
            foreach (var index in relevantIndexes)
            {
                var article = articles[index - 1]; // Convert 1-based index to 0-based
                var content = await ScrapeArticleContentAsync(article.Link);

                if (!string.IsNullOrEmpty(content) && content.Split(' ').Length > 100)
                {
                    article.Snippet = content;
                    validArticles.Add(article);
                }
            }

            return validArticles;
        }

        // Step 4: Analyze sentiment for each article (same thread)
        private async Task<List<SentimentResult>> AnalyzeEachArticleSentimentAsync(List<Article> articles, string ticker, string threadId)
        {
            var articleContents = articles.Select(a => a.Snippet).ToList();
            var sentimentResults = await AnalyzeSentimentAsync(articleContents, ticker, threadId);

            return articles.Select((article, index) => new SentimentResult
            {
                Title = article.Title,
                Link = article.Link,
                Snippet = article.Snippet,
                Date = article.Date,
                SentimentScore = sentimentResults[index].SentimentScore,
                Summary = sentimentResults[index].Summary
            }).ToList();
        }

        // Step 5: Final sentiment analysis (same thread)
        public async Task<(float FinalSentimentScore, string FinalSentimentSummary)> GetFinalSentimentAnalysisAsync(List<SentimentResult> sentimentResults, string ticker, string threadId)
        {
            if (!sentimentResults.Any())
            {
                return (0, "No sentiment data available.");
            }

            // Build a strict prompt with clear formatting rules, without any special characters like **
            var sentimentSummary = string.Join("\n", sentimentResults.Select((result, index) =>
                $"{index + 1}. Sentiment Score: {result.SentimentScore}\nSummary: {result.Summary}"));

            // Define the prompt with strict formatting for the final sentiment score and detailed reasoning
            var prompt = (
                $"Based on the sentiment scores and summaries provided for the ticker {ticker}, " +
                $"please provide a Final Sentiment Score between 0 and 100, along with a detailed reasoning for this score. " +
                $"Do not reference individual articles or their numbers in your response. " +
                $"Focus on the overall trends, specific data points, and key concerns noted in the summaries to explain the final score. " +
                $"Ensure the output strictly follows this format:\n\n" +
                $"Final Sentiment Score: X\nDetailed Reasoning: Y\n\n" +
                $"{sentimentSummary}\n\n" +
                $"The response should strictly follow the format 'Final Sentiment Score: X' and 'Detailed Reasoning: Y' without deviation. " +
                $"The detailed reasoning should provide specific points that led to the final sentiment score, avoiding references to individual articles."
            );


            // Send the prompt to the AI assistant
            var messageId = await _openAiClient.AddMessageToThreadAsync(threadId, prompt);

            var assistanId = "TEMP";

            // Create a run to generate the final sentiment analysis
            await _openAiClient.CreateRunAsync(threadId, assistanId, "Generate final sentiment score and reasoning");

            // Wait for the assistant's response
            var response = await _openAiClient.WaitForAssistantResponseAsync(threadId, assistanId, messageId);

            // Initialize variables to store the parsed result
            float finalSentimentScore = 0;
            string finalSentimentSummary = "";

            // Parse the response to extract the final sentiment score and detailed reasoning
            foreach (var line in response.Split('\n'))
            {
                var trimmedLine = line.Trim();

                // Extract the final sentiment score
                if (trimmedLine.StartsWith("Final Sentiment Score:"))
                {
                    var scorePart = trimmedLine.Split("Final Sentiment Score:")[1].Trim();
                    if (float.TryParse(scorePart, out var score))
                    {
                        finalSentimentScore = score;
                    }
                }
                // Extract the detailed reasoning
                else if (trimmedLine.StartsWith("Detailed Reasoning:"))
                {
                    finalSentimentSummary = trimmedLine.Split("Detailed Reasoning:")[1].Trim();
                }
            }

            // Return the parsed final sentiment score and detailed reasoning
            return (finalSentimentScore, finalSentimentSummary.Trim());
        }


        // Method to scrape the article content
        public async Task<string> ScrapeArticleContentAsync(string url)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) })
            {
                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();

                    var doc = new HtmlDocument();
                    doc.LoadHtml(responseBody);
                    var paragraphs = doc.DocumentNode.SelectNodes("//p");

                    return paragraphs == null ? string.Empty : string.Join(" ", paragraphs.Select(p => p.InnerText));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scraping {url}: {ex.Message}");
                    return string.Empty;
                }
            }
        }

        public async Task<List<(float? SentimentScore, string Summary)>> AnalyzeSentimentAsync(List<string> contents, string ticker, string threadId)
        {
            Console.WriteLine("Starting sentiment analysis for each article individually.");

            List<(float? SentimentScore, string Summary)> results = new List<(float? SentimentScore, string Summary)>();

            foreach (var content in contents)
            {
                // Build a strict prompt for each article
                string prompt = (
                    $"Analyze the following article content for sentiment related to {ticker} " +
                    $"on a scale of 1 to 100, where 1 is very negative and 100 is very positive. " +
                    $"Focus on real-time, daily updates about the business and specifically its stock value, " +
                    $"such as new announcements, major stock movements, analyst ratings, etc. " +
                    $"Provide the sentiment score in the format: 'Sentiment Score: X' and a summary in the format: 'Summary: ...'.\n\n" +
                    $"Content: {content}\n\n" +
                    "Provide the sentiment score and summary with reasons for sentiment score, strictly in the format 'Sentiment Score: X' and 'Summary: ...'."
                );

                var assistanId = "TEMP";

                // Send the prompt to the AI assistant
                var messageId = await _openAiClient.AddMessageToThreadAsync(threadId, prompt);
                await _openAiClient.CreateRunAsync(threadId, assistanId, $"Analyze Sentiment for {ticker} Article");

                // Wait for the assistant's response
                var responseMessage = await _openAiClient.WaitForAssistantResponseAsync(threadId, assistanId, messageId);
                Console.WriteLine("Sentiment analysis for one article completed.");

                // Parse the response for sentiment score and summary
                float? sentimentScore = null;
                string summary = string.Empty;

                foreach (var line in responseMessage.Split('\n'))
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("Sentiment Score:"))
                    {
                        // Extract the sentiment score
                        var scorePart = trimmedLine.Split("Sentiment Score:")[1].Trim();
                        if (float.TryParse(scorePart, out var score))
                        {
                            sentimentScore = score;
                        }
                    }
                    else if (trimmedLine.StartsWith("Summary:"))
                    {
                        // Extract the summary
                        summary = trimmedLine.Split("Summary:")[1].Trim();
                    }
                }

                // Add the result to the list if we have both sentiment score and summary
                if (sentimentScore.HasValue && !string.IsNullOrEmpty(summary))
                {
                    results.Add((sentimentScore, summary));
                }
                else
                {
                    Console.WriteLine("Failed to parse sentiment score or summary.");
                }
            }

            return results;
        }


    }

    public class Article
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Snippet { get; set; }
        public DateTime Date { get; set; }
    }

    public class SentimentResult : Article
    {
        public float? SentimentScore { get; set; }
        public string Summary { get; set; }
    }
    public class SentimentSummaryResult
    {
        public List<SentimentResult> ArticleSentiments { get; set; }
        public float FinalSentimentScore { get; set; }
        public string FinalSentimentSummary { get; set; }
    }
}
