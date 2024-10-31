using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SignalsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Services.WebSocketManager _webSocketManager;
        private readonly SentimentAnalysisService _sentimentAnalysisService;
        private readonly ILogger<SignalsController> _logger;

        public SignalsController(IConfiguration configuration, Services.WebSocketManager webSocketManager, SentimentAnalysisService sentimentAnalysisService, ILogger<SignalsController> logger)
        {
            _configuration = configuration;
            _webSocketManager = webSocketManager;
            _sentimentAnalysisService = sentimentAnalysisService;
            _logger = logger;
        }

        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages()
        {
            var connectionString = _configuration.GetConnectionString("IntradaySpySignalsConnection");
            var messages = new List<Message>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("SELECT TOP 10 * FROM signal_messages ORDER BY created_at DESC", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            messages.Add(new Message
                            {
                                Id = reader.GetInt32(0),
                                Content = reader.GetString(1),
                                CreatedAt = reader.GetDateTime(2)
                            });
                        }
                    }
                }
            }

            return Ok(messages);
        }

        [AllowAnonymous]
        [HttpPost("messages")]
        public async Task<IActionResult> AddMessage([FromBody] Message message)
        {
            var connectionString = _configuration.GetConnectionString("IntradaySpySignalsConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("INSERT INTO signal_messages (message, created_at) VALUES (@message, @created_at)", connection))
                {
                    command.Parameters.AddWithValue("@message", message.Content);
                    command.Parameters.AddWithValue("@created_at", message.CreatedAt);

                    //await command.ExecuteNonQueryAsync();
                }
            }

            // Broadcast the new message to all WebSocket clients
            string jsonMessage = JsonConvert.SerializeObject(message);
            await _webSocketManager.BroadcastMessage(jsonMessage);

            return Ok();
        }

        [HttpGet("sentiment")]
        public async Task<IActionResult> GetSentiment([FromQuery] string ticker)
        {

            var results = await _sentimentAnalysisService.SummarizeSentimentAsync($"{ticker} stock news", ticker);

            return Ok(results);
        }

        [HttpGet("trades")]
        public async Task<IActionResult> GetTrades()
        {
            var connectionString = _configuration.GetConnectionString("IntradaySpySignalsConnection");
            var trades = new List<Trade>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(@"
                    SELECT TOP (1000) [id]
                          ,[symbol]
                          ,[signal_type]
                          ,[entry_point]
                          ,[stop_loss]
                          ,[invalidated_price]
                          ,[take_profit]
                          ,[sentiment]
                          ,[is_open]
                          ,[invalidated]
                          ,[volume_confirmed]
                          ,[total_profit]
                          ,[created_at]
                          ,[updated_at]
                          ,[confidence]
                    FROM [dbo].[trade_signals]
                    WHERE volume_confirmed = 0 AND confidence = 1", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            trades.Add(new Trade
                            {
                                Id = reader.GetInt32(0),
                                Symbol = reader.GetString(1),
                                SignalType = reader.GetString(2),
                                EntryPoint = (decimal)reader.GetDouble(3),
                                StopLoss = (decimal)reader.GetDouble(4),
                                InvalidatedPrice = reader.IsDBNull(5) ? (decimal?)null : (decimal)reader.GetDouble(5),
                                TakeProfit = reader.IsDBNull(6) ? (decimal?)null : (decimal)reader.GetDouble(6),
                                Sentiment = reader.IsDBNull(7) ? (decimal?)null : (decimal)reader.GetDouble(7),
                                IsOpen = reader.GetBoolean(8),
                                Invalidated = reader.GetBoolean(9),
                                VolumeConfirmed = reader.GetBoolean(10),
                                TotalProfit = reader.IsDBNull(11) ? (decimal?)null : (decimal)reader.GetDouble(11),
                                CreatedAt = reader.GetDateTime(12),
                                UpdatedAt = reader.GetDateTime(13),
                                Confidence = reader.IsDBNull(14) ? null : reader.GetString(14)
                            });
                        }
                    }
                }
            }

            return Ok(trades);
        }

        [HttpGet("daily_swing_signals")]
        public async Task<IActionResult> GetDailySwingSignals()
        {
            var connectionString = _configuration.GetConnectionString("IntradaySpySignalsConnection");
            var signals = new List<DailySwingSignal>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(@"
            SELECT TOP (1000) 
                  [id]
                  ,[symbol]
                  ,[signal_type]
                  ,[entry_point]
                  ,[stop_loss]
                  ,[take_profit]
                  ,[is_open]
                  ,[total_profit]
                  ,[created_at]
                  ,[updated_at]
                  ,[pass_by_counter]
            FROM [dbo].[daily_swing_signals]", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            signals.Add(new DailySwingSignal
                            {
                                Id = reader.GetInt32(0),
                                Symbol = reader.GetString(1),
                                SignalType = reader.GetString(2),
                                EntryPoint = (decimal)reader.GetDouble(3),
                                StopLoss = (decimal)reader.GetDouble(4),
                                TakeProfit = reader.IsDBNull(5) ? (decimal?)null : (decimal)reader.GetDouble(5),
                                IsOpen = reader.GetBoolean(6),
                                TotalProfit = reader.IsDBNull(7) ? (decimal?)null : (decimal)reader.GetDouble(7),
                                CreatedAt = reader.GetDateTime(8),
                                UpdatedAt = reader.GetDateTime(9),
                                PassByCounter = reader.GetInt32(10)
                            });
                        }
                    }
                }
            }

            return Ok(signals);
        }

    }


}
