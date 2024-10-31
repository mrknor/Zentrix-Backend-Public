namespace Backend.Controllers
{
    // Controllers/TradesController.cs
    using Microsoft.AspNetCore.Mvc;
    using Backend.Models;
    using Microsoft.AspNetCore.Identity;
    using Backend.Data;
    using System.Security.Claims;
    using Microsoft.EntityFrameworkCore;

    [ApiController]
    [Route("api/[controller]")]
    public class PaperTradesController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StockDbContext _context;

        public PaperTradesController(UserManager<ApplicationUser> userManager, StockDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("buy")]
        public async Task<IActionResult> Buy([FromBody] PaperTradeRequest tradeRequest)
        {
            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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

            var cost = tradeRequest.Amount * tradeRequest.Price;

            // Check if the account has enough balance
            if (account.Balance < cost)
            {
                return BadRequest("Insufficient funds.");
            }

            // Check for an open short trade
            var openShortTrade = await _context.PaperTrades
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Ticker == tradeRequest.Ticker && t.Status == "open" && t.Type == "sell");

            if (openShortTrade != null)
            {
                // If covering a short position (buying back shares)
                if (tradeRequest.Amount < openShortTrade.Amount)
                {
                    // Partially cover the short position
                    var coveredTrade = new PaperTrade
                    {
                        Ticker = tradeRequest.Ticker,
                        Amount = tradeRequest.Amount,
                        OpenPrice = openShortTrade.OpenPrice,
                        ClosePrice = tradeRequest.Price,
                        Type = "sell",
                        UserId = user.Id,
                        Status = "closed",
                        OpenTime = openShortTrade.OpenTime,
                        CloseTime = DateTime.UtcNow,
                        ProfitLoss = (openShortTrade.OpenPrice - tradeRequest.Price) * tradeRequest.Amount // Profit for a short is open price - close price
                    };

                    _context.PaperTrades.Add(coveredTrade);

                    // Update the remaining open short trade
                    openShortTrade.Amount -= tradeRequest.Amount;
                    _context.PaperTrades.Update(openShortTrade);
                }
                else if (tradeRequest.Amount == openShortTrade.Amount)
                {
                    // Fully cover the short position
                    openShortTrade.ClosePrice = tradeRequest.Price;
                    openShortTrade.CloseTime = DateTime.UtcNow;
                    openShortTrade.Status = "closed";
                    openShortTrade.ProfitLoss = (openShortTrade.OpenPrice - tradeRequest.Price) * openShortTrade.Amount;

                    _context.PaperTrades.Update(openShortTrade);
                }
                else
                {
                    return BadRequest("Cannot buy more than the amount shorted.");
                }

                // Update the account to reflect the cost of covering the short
                account.Balance -= cost;
                account.TotalValue = account.Balance + account.PortfolioValue;
                _context.PaperTradingAccounts.Update(account);
            }
            else
            {
                // Open a new long position if no short trade exists
                var newTrade = new PaperTrade
                {
                    Ticker = tradeRequest.Ticker,
                    Amount = tradeRequest.Amount,
                    OpenPrice = tradeRequest.Price,
                    Price = tradeRequest.Price,
                    Type = "buy",
                    UserId = user.Id,
                    Status = "open",
                    OpenTime = DateTime.UtcNow
                };

                _context.PaperTrades.Add(newTrade);

                // Update the account to reflect the cost of the new long position
                account.Balance -= cost;
                account.PortfolioValue += cost;
                account.TotalValue = account.Balance + account.PortfolioValue;
                _context.PaperTradingAccounts.Update(account);
            }

            await _context.SaveChangesAsync();

            return Ok("Buy processed successfully.");
        }



        [HttpPost("sell")]
        public async Task<IActionResult> Sell([FromBody] PaperTradeRequest tradeRequest)
        {
            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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

            var revenue = tradeRequest.Amount * tradeRequest.Price;

            // Check for an open long trade
            var openLongTrade = await _context.PaperTrades
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Ticker == tradeRequest.Ticker && t.Status == "open" && t.Type == "buy");

            if (openLongTrade != null)
            {
                // If selling to close a long position
                if (tradeRequest.Amount < openLongTrade.Amount)
                {
                    // Partially sell the long position
                    var closedTrade = new PaperTrade
                    {
                        Ticker = tradeRequest.Ticker,
                        Amount = tradeRequest.Amount,
                        OpenPrice = openLongTrade.OpenPrice,
                        ClosePrice = tradeRequest.Price,
                        Type = "buy",
                        UserId = user.Id,
                        Status = "closed",
                        OpenTime = openLongTrade.OpenTime,
                        CloseTime = DateTime.UtcNow,
                        ProfitLoss = (tradeRequest.Price - openLongTrade.OpenPrice) * tradeRequest.Amount // Profit for a long is close price - open price
                    };

                    _context.PaperTrades.Add(closedTrade);

                    // Update the remaining open long trade
                    openLongTrade.Amount -= tradeRequest.Amount;
                    _context.PaperTrades.Update(openLongTrade);
                }
                else if (tradeRequest.Amount == openLongTrade.Amount)
                {
                    // Fully sell the long position
                    openLongTrade.ClosePrice = tradeRequest.Price;
                    openLongTrade.CloseTime = DateTime.UtcNow;
                    openLongTrade.Status = "closed";
                    openLongTrade.ProfitLoss = (tradeRequest.Price - openLongTrade.OpenPrice) * openLongTrade.Amount;

                    _context.PaperTrades.Update(openLongTrade);
                }
                else
                {
                    return BadRequest("Cannot sell more than the amount owned.");
                }

                // Update the account to reflect the sale revenue
                account.Balance += revenue;
                account.PortfolioValue -= revenue;
                account.TotalValue = account.Balance + account.PortfolioValue;
                _context.PaperTradingAccounts.Update(account);
            }
            else
            {
                // Open a new short position if no long trade exists
                var newShortTrade = new PaperTrade
                {
                    Ticker = tradeRequest.Ticker,
                    Amount = tradeRequest.Amount,
                    OpenPrice = tradeRequest.Price,
                    Price = tradeRequest.Price,
                    Type = "sell",
                    UserId = user.Id,
                    Status = "open",
                    OpenTime = DateTime.UtcNow
                };

                _context.PaperTrades.Add(newShortTrade);

                // Update the account to reflect the short position's revenue
                account.Balance += revenue;
                account.TotalValue = account.Balance + account.PortfolioValue;
                _context.PaperTradingAccounts.Update(account);
            }

            await _context.SaveChangesAsync();

            return Ok("Sell processed successfully.");
        }



        [HttpGet("get-trades")]
        public async Task<IActionResult> GetTrades()
        {
            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByEmailAsync(email);

            if (user.Id == null)
            {
                return Unauthorized();
            }

            var trades = await _context.PaperTrades
                .Where(t => t.UserId == user.Id)
                .ToListAsync();

            return Ok(trades);
        }
    }

}
