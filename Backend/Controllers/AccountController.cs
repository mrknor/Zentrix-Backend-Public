using Backend.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, IEmailSender emailSender, IHttpClientFactory httpClientFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _emailSender = emailSender;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, false, lockoutOnFailure: false);
            if (!result.Succeeded)
            {
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("FirstName", user.FirstName) // Add FirstName to the claims
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    return Ok(new { Message = "User registered successfully" });
                }

                // Return a list of errors
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(new { Errors = errors });
            }

            // Return model state errors
            var modelErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { Errors = modelErrors });
        }

        // Password reset request
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestModel request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist or is not confirmed
                return Ok(new { Message = "If your email exists in our system, you will receive a password reset email shortly." });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var encodedToken = HttpUtility.UrlEncode(token); // URL encode the token

            var frontendBaseUrl = _configuration["Frontend:BaseUrl"];

            var resetLink = $"{frontendBaseUrl}/ResetPassword?token={encodedToken}&email={user.Email}";

            //await _emailSender.SendEmailAsync(user.Email, "Reset Password", $"Please reset your password by clicking here: <a href='{resetLink}'>link</a>");

            return Ok(new { Message = "If your email exists in our system, you will receive a password reset email shortly." });
        }

        // Password reset confirmation
        [HttpPost("confirm-reset-password")]
        public async Task<IActionResult> ConfirmResetPassword([FromBody] ConfirmResetPasswordRequestModel request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return BadRequest(new { Message = "Invalid request." });
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (result.Succeeded)
            {
                return Ok(new { Message = "Password has been reset successfully." });
            }

            return BadRequest(result.Errors);
        }


        [HttpGet("discord-login")]
        public IActionResult DiscordLogin()
        {
            var clientId = _configuration["Discord:ClientId"];
            var redirectUri = Url.Action("DiscordCallback", "Account", null, Request.Scheme);
            var state = Guid.NewGuid().ToString("N");

            var authUrl = $"https://discord.com/oauth2/authorize?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=identify email&state={state}";
            Response.Cookies.Append("OAuthState", state, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax });

            return Redirect(authUrl);
        }

        [HttpGet("signin-discord")]
        public async Task<IActionResult> DiscordCallback(string code, string state)
        {
            Console.WriteLine($"Callback received. Code: {code}, State: {state}");

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return BadRequest(new { error = "Code and state are required" });
            }

            var tokenResponse = await ExchangeCodeForTokenAsync(code);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var responseBody = await tokenResponse.Content.ReadAsStringAsync();
                Console.WriteLine("Token exchange failed with response:");
                Console.WriteLine(responseBody);
                return BadRequest(new { error = "Failed to exchange code for token", details = responseBody });
            }

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonDocument.Parse(tokenContent);

            var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();

            var userResponse = await GetUserInformationAsync(accessToken);
            if (!userResponse.IsSuccessStatusCode)
            {
                return BadRequest(new { error = "Failed to retrieve user information" });
            }

            var userContent = await userResponse.Content.ReadAsStringAsync();
            var userData = JsonDocument.Parse(userContent);

            var email = userData.RootElement.GetProperty("email").GetString();
            var userName = userData.RootElement.GetProperty("username").GetString();

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = userName,
                    LastName = string.Empty
                };

                var identityResult = await _userManager.CreateAsync(user);
                if (!identityResult.Succeeded)
                {
                    return BadRequest(new { error = "Failed to create user" });
                }
            }

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("FirstName", user.FirstName)
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);
            Console.WriteLine($"Token generated: {jwtToken}");

            // Create a script to send the token back and close the popup
            var script = $@"
        <script>
            window.opener.postMessage({{ token: '{jwtToken}' }}, '*');
            window.close();
        </script>";

            return Content(script, "text/html");
        }


        private async Task<HttpResponseMessage> ExchangeCodeForTokenAsync(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/oauth2/token");

            var backendBaseUrl = _configuration["Backend:BaseUrl"];

            var redirectUri = $"{backendBaseUrl}/api/account/signin-discord"; // Update this to match exactly

            var parameters = new Dictionary<string, string>
    {
        { "client_id", _configuration["Discord:ClientId"] },
        { "client_secret", _configuration["Discord:ClientSecret"] },
        { "grant_type", "authorization_code" },
        { "code", code },
        { "redirect_uri", redirectUri }
    };

            var tokenRequestContent = new FormUrlEncodedContent(parameters);

            tokenRequest.Content = tokenRequestContent;
            tokenRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Log the content being sent
            var contentString = await tokenRequest.Content.ReadAsStringAsync();
            Console.WriteLine("Token request content:");
            Console.WriteLine(contentString);

            // Log the complete request URI
            Console.WriteLine("Token request URI:");
            Console.WriteLine(tokenRequest.RequestUri);

            return await client.SendAsync(tokenRequest);
        }

        private async Task<HttpResponseMessage> GetUserInformationAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");

            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await client.SendAsync(userRequest);
        }

    }

}
