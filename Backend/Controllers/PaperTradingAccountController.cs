// Controllers/PaperTradingAccountController.cs
using Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using Backend.Data;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaperTradingAccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StockDbContext _context;

        public PaperTradingAccountController(UserManager<ApplicationUser> userManager, StockDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("get-account")]
        public async Task<IActionResult> GetAccount()
        {
            var email = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Unauthorized();
            }

            // Check if the account exists
            var account = await _context.PaperTradingAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (account == null)
            {
                // Create a new account if it does not exist
                account = new PaperTradingAccount
                {
                    UserId = user.Id,
                    Balance = 10000, // Initial balance
                    PortfolioValue = 0,
                    TotalValue = 10000,
                    LastUpdated = DateTime.UtcNow
                };

                _context.PaperTradingAccounts.Add(account);
                await _context.SaveChangesAsync();
            }

            return Ok(account);
        }


        [HttpPost("create-account")]
        public async Task<IActionResult> CreateAccount()
        {
            var email = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Unauthorized();
            }

            var existingAccount = await _context.PaperTradingAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (existingAccount != null)
            {
                return BadRequest("Account already exists.");
            }

            var account = new PaperTradingAccount
            {
                UserId = user.Id,
                Balance = 10000, // Initial balance
                PortfolioValue = 0,
                TotalValue = 10000,
                LastUpdated = DateTime.UtcNow
            };

            _context.PaperTradingAccounts.Add(account);
            await _context.SaveChangesAsync();

            return Ok(account);
        }

        [HttpPost("update-account")]
        public async Task<IActionResult> UpdateAccount([FromBody] PaperTradingAccount updatedAccount)
        {
            var email = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Unauthorized();
            }

            var account = await _context.PaperTradingAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (account == null)
            {
                return NotFound("Account not found.");
            }

            account.Balance = updatedAccount.Balance;
            account.PortfolioValue = updatedAccount.PortfolioValue;
            account.TotalValue = updatedAccount.Balance + updatedAccount.PortfolioValue;
            account.LastUpdated = DateTime.UtcNow;

            _context.PaperTradingAccounts.Update(account);
            await _context.SaveChangesAsync();

            return Ok(account);
        }
    }
}
