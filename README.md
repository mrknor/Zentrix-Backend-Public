
# Zentrix Backend

Zentrix is an AI-powered trading signals platform, blending machine learning, real-time data analysis, and sentiment insights to deliver actionable trading signals. The backend integrates with services like OpenAI and Polygon and is built to handle user interactions, sentiment analysis, signal generation, and WebSocket-driven real-time updates.

## Table of Contents

1. [System Overview](#system-overview)
2. [Key Features](#key-features)
3. [Core Controllers](#core-controllers)
   - [AccountController](#accountcontroller)
   - [ChatController](#chatcontroller)
   - [EmailController](#emailcontroller)
   - [PaperTradesController](#papertradescontroller)
   - [PaperTradingAccountController](#papertradingaccountcontroller)
   - [SignalsController](#signalscontroller)
   - [StocksController](#stockscontroller)
4. [Core Services](#core-services)
   - [ChatService](#chatservice)
   - [EmailSender](#emailsender)
   - [OpenAIClient](#openaiclient)
   - [PolygonService](#polygonservice)
   - [SentimentAnalysisService](#sentimentanalysisservice)
   - [WebSocketManager](#websocketmanager)
5. [Development Insights](#development-insights)

---

## System Overview

The Zentrix backend is built to support real-time stock trading signals, combining AI analysis, data streams, and custom algorithms to serve the trading community. The system handles multiple key functions, including:

- **User Account Management**: Registration, authentication, and account profile features.
- **AI-Driven Chatbot**: An interactive chatbot to answer trading-related questions and provide insights using OpenAI.
- **Trading Signal Generation**: Delivers actionable signals based on real-time data and sentiment analysis.
- **Paper Trading**: Allows users to simulate trading in a controlled environment.
- **Real-Time Data Updates**: Utilizes WebSocket for live data streaming.

## Key Features

1. **Real-Time Trading Signals**: Dynamic signal generation powered by ML algorithms.
2. **Paper Trading**: Simulated trading environment to test strategies without financial risk.
3. **Sentiment Analysis**: Integrates with OpenAI for analyzing news and social media sentiment.
4. **WebSocket Updates**: Keeps users updated with real-time trading data.

---

## Core Controllers

### AccountController

The `AccountController` manages all account-related functionality, including registration, login, and profile management.

- **Registration & Login**: Uses secure methods to authenticate users.
- **Profile Management**: Updates user information.

**Snippet: Handling Registration**
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register(UserRegistrationDto userDto)
{
    var result = await _accountService.Register(userDto);
    if (result.Success)
        return Ok(result);
    return BadRequest(result.Message);
}
```

### ChatController

The `ChatController` leverages OpenAI to facilitate a chatbot that responds to user inquiries about trading.

**Snippet: Sending Chat Messages to OpenAI**
```csharp
[HttpPost("send")]
public async Task<IActionResult> SendMessage(ChatMessageDto message)
{
    var response = await _chatService.GenerateResponse(message);
    return Ok(response);
}
```

This integration with OpenAI API provides meaningful answers, creating an interactive experience for users.

### EmailController

The `EmailController` sends notifications and signals through emails, using the SMTP server configuration.

**Snippet: Sending Email Notifications**
```csharp
[HttpPost("send")]
public async Task<IActionResult> SendEmail(EmailRequest request)
{
    var result = await _emailSender.SendEmailAsync(request);
    return result ? Ok("Email sent successfully") : BadRequest("Email failed to send");
}
```

### PaperTradesController

Handles individual simulated trades, allowing users to practice trading strategies in a paper trading environment.

**Snippet: Executing a Paper Trade**
```csharp
[HttpPost("execute")]
public async Task<IActionResult> ExecuteTrade(PaperTradeRequest tradeRequest)
{
    var tradeResult = await _paperTradesService.ExecuteTrade(tradeRequest);
    return Ok(tradeResult);
}
```

### PaperTradingAccountController

This controller manages users’ paper trading accounts, which include simulated balance management and transaction tracking.

### SignalsController

The `SignalsController` is a core part of the backend, handling signal generation and historical data management.

**Snippet: Fetching the Latest Signal**
```csharp
[HttpGet("get")]
public async Task<IActionResult> GetSignal(string symbol)
{
    var signal = await _signalsService.GetLatestSignal(symbol);
    return signal != null ? Ok(signal) : NotFound("No signals available");
}
```

### StocksController

This controller retrieves live stock data from Polygon, providing real-time market insights.

**Snippet: Retrieving Stock Information**
```csharp
[HttpGet("info")]
public async Task<IActionResult> GetStockInfo(string symbol)
{
    var stockInfo = await _polygonService.GetStockInfo(symbol);
    return Ok(stockInfo);
}
```

---

## Core Services

### ChatService

The `ChatService` integrates with OpenAI for NLP-powered responses in the chatbot. It allows users to interact with AI directly through the chatbot.

**Snippet: Generating an OpenAI Response**
```csharp
public async Task<string> GenerateResponse(ChatMessageDto message)
{
    var aiResponse = await _openAIClient.GetResponseAsync(message.Text);
    return aiResponse;
}
```

### EmailSender

This service manages email notifications for users, especially critical updates and signals. Integrates with SMTP for reliable delivery.

**Snippet: Sending Emails via SMTP**
```csharp
public async Task<bool> SendEmailAsync(EmailRequest request)
{
    // SMTP client configuration and sending logic
    return emailSuccess;
}
```

### OpenAIClient

The `OpenAIClient` handles interactions with the OpenAI API, both for chat responses and sentiment analysis.

**Snippet: Requesting an AI-Generated Response**
```csharp
public async Task<string> GetResponseAsync(string prompt)
{
    var response = await openAiApi.GetCompletionAsync(prompt);
    return response;
}
```

### PolygonService

Integrates with the Polygon API to fetch current and historical stock data, which feeds directly into signal generation and user dashboards.

**Snippet: Fetching Stock Data from Polygon**
```csharp
public async Task<StockData> GetStockInfo(string symbol)
{
    return await polygonClient.GetRealTimeStockData(symbol);
}
```

### SentimentAnalysisService

The `SentimentAnalysisService` provides sentiment scoring based on market news, aiding in signal creation and risk assessment.

**Snippet: Performing Sentiment Analysis**
```csharp
public async Task<double> AnalyzeSentiment(string text)
{
    var sentimentScore = await _openAIClient.AnalyzeSentiment(text);
    return sentimentScore;
}
```

### WebSocketManager

`WebSocketManager` enables real-time communication, pushing updates like trade signals and stock prices to connected clients.

**Snippet: Broadcasting Updates via WebSocket**
```csharp
public async Task BroadcastUpdate(string message)
{
    foreach (var socket in _sockets)
    {
        if (socket.State == WebSocketState.Open)
            await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
```

---

## Development Insights

### Challenges and Solutions

1. **Handling OpenAI Response Delays**: Initially, the response time from OpenAI caused delays. Implementing asynchronous calls and caching responses for repeated questions improved response speed.

2. **Integrating Polygon Data Efficiently**: Due to rate limits on the Polygon API, we set up a caching system to store frequently requested data. This reduced the number of API calls, keeping data retrieval fast.

3. **Creating a Reliable WebSocket System**: Managing real-time data with WebSocket required efficient handling of client connections. We implemented `WebSocketManager` to broadcast updates only to active clients, reducing unnecessary data traffic.

4. **Sentiment Analysis Calibration**: The initial sentiment scores from OpenAI were inconsistent, so we adjusted the scoring to better align with market data. This calibration provided a more accurate reflection of market sentiment.

5. **Error Handling in Email Delivery**: Configuring the email service to retry failed deliveries enhanced the reliability of email notifications, ensuring users received critical updates without delay.

---

## Summary

The Zentrix backend is designed to provide traders with AI-driven insights, fast data retrieval, and a smooth user experience. Leveraging AI and real-time data integration, it offers powerful tools to help traders make data-driven decisions, with systems in place to ensure accuracy, reliability, and user-friendly interaction.
