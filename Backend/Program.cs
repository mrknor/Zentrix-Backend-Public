using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Backend.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();

builder.Services.AddTransient<IEmailSender, EmailSender>();

// Register IHttpClientFactory
builder.Services.AddHttpClient();

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
})
.AddCookie()
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"];
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"];
    options.CallbackPath = new PathString("/signin-discord");
    options.Scope.Add("identify");
    options.Scope.Add("email");
    options.SaveTokens = true;
    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

    options.Events = new OAuthEvents
    {
        OnRedirectToAuthorizationEndpoint = context =>
        {
            var state = Guid.NewGuid().ToString("N");
            context.Properties.Items["state"] = state;

            // Logging for debugging
            Console.WriteLine($"Setting OAuthState cookie: {state}");

            context.Response.Cookies.Append("OAuthState", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Use true if you are running on HTTPS
                SameSite = SameSiteMode.None, // Adjust this based on your needs
                Path = "/" // Ensure the path is correct
            });

            var authorizationEndpoint = $"{context.RedirectUri}&state={state}";
            Console.WriteLine($"Redirecting to authorization endpoint: {authorizationEndpoint}");
            Console.WriteLine($"State set to: {state}");

            context.Response.Redirect(authorizationEndpoint);
            return Task.CompletedTask;
        },
        OnCreatingTicket = async context =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

            var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();

            var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            context.RunClaimActions(user.RootElement);
        },
        OnRemoteFailure = context =>
        {
            var errorMessage = $"Error: {context.Failure.Message}";
            var state = context.Request.Query["state"].ToString();
            var storedState = context.HttpContext.Request.Cookies["OAuthState"];

            errorMessage += $"\nState: {state}";
            errorMessage += $"\nStored State: {storedState}";
            Console.WriteLine(errorMessage);

            context.Response.Redirect($"/signin?error={Uri.EscapeDataString(errorMessage)}");
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnTicketReceived = context =>
        {
            var state = context.Request.Cookies["OAuthState"];
            if (context.Properties.Items.TryGetValue("state", out var propertiesState) && state == propertiesState)
            {
                Console.WriteLine("State matches. Proceeding with authentication.");
                context.Properties.Items.Remove("state");
                context.Response.Cookies.Delete("OAuthState");
            }
            else
            {
                context.Fail("The oauth state was missing or invalid.");
                Console.WriteLine("The oauth state was missing or invalid.");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddControllers();
builder.Services.AddSingleton<Backend.Services.WebSocketManager>();
builder.Services.AddScoped<SentimentAnalysisService>();

builder.Services.AddSingleton<OpenAIClient>(provider =>
{
    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
    var openAIApiKey = apiSettings.ApiKey;
    var assistantId = apiSettings.AssistantId;
    return new OpenAIClient(openAIApiKey, assistantId);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<Backend.Data.StockDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<PolygonService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

var app = builder.Build();

var webSocketManager = app.Services.GetRequiredService<Backend.Services.WebSocketManager>();

app.UseWebSockets();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var socketId = webSocketManager.AddSocket(webSocket);
            await webSocketManager.ReceiveMessages(socketId, webSocket);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
