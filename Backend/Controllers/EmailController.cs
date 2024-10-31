using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailController> _logger;

        public EmailController(IConfiguration configuration, ILogger<EmailController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] EmailRequest emailRequest)
        {
            if (emailRequest == null || string.IsNullOrEmpty(emailRequest.Email))
            {
                return BadRequest("Email is required.");
            }

            try
            {
                var apiKey = _configuration["SendGrid:ApiKey"];
                var client = new SendGridClient(apiKey);

                // Add email to SendGrid Contacts
                var listId = _configuration["SendGrid:ListId"];
                var addToListResponse = await client.RequestAsync(method: SendGridClient.Method.PUT, urlPath: "marketing/contacts", requestBody: $"{{\"list_ids\": [\"{listId}\"], \"contacts\": [{{\"email\": \"{emailRequest.Email}\"}}]}}");

                if (addToListResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.LogError($"Failed to add contact to list: {addToListResponse.Body.ReadAsStringAsync().Result}");
                    return StatusCode((int)addToListResponse.StatusCode, addToListResponse.Body.ReadAsStringAsync().Result);
                }

                return Ok("Email subscribed successfully.");
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Exception caught while subscribing email: {ex.Message}");
                return StatusCode(500, "Internal server error.");
            }
        }
    }

    public class EmailRequest
    {
        public string Email { get; set; }
    }
}
