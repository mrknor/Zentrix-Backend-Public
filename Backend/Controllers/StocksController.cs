using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Services;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StockDbContext _context;
        private readonly PolygonService _polygonService;

        public StocksController(UserManager<ApplicationUser> userManager,StockDbContext context, PolygonService polygonService)
        {
            _userManager = userManager;
            _context = context;
            _polygonService = polygonService;
        }

        [HttpGet("get-stock-data")]
        public async Task<IActionResult> GetStockData([FromQuery] string tickers)
        {
            if (string.IsNullOrEmpty(tickers))
            {
                return BadRequest("Tickers must be provided.");
            }

            // Split the tickers string into a list of tickers
            var tickerList = tickers.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Fetch the stocks from the database
            var stocksInDb = await _context.Stocks
                .Where(s => tickerList.Contains(s.Ticker))
                .ToListAsync();

            // Determine which stocks need updating
            var staleStocks = stocksInDb
                .Where(s => s.LastUpdated < DateTime.UtcNow.AddMinutes(-1))
                .Select(s => s.Ticker)
                .ToList();

            // Add new tickers that aren't in the database yet
            var newTickers = tickerList.Except(stocksInDb.Select(s => s.Ticker)).ToList();
            staleStocks.AddRange(newTickers);

            // If there are stale stocks, fetch new data from Polygon
            if (staleStocks.Count > 0)
            {
                var updatedStocks = await _polygonService.GetStocksDataFromPolygon(staleStocks);

                // Update existing stocks or insert new stocks into the database
                foreach (var updatedStock in updatedStocks)
                {
                    var stockInDb = stocksInDb.FirstOrDefault(s => s.Ticker == updatedStock.Ticker);
                    if (stockInDb != null)
                    {
                        stockInDb.CurrentPrice = updatedStock.CurrentPrice;
                        stockInDb.Volume = updatedStock.Volume;
                        stockInDb.PreviousClose = updatedStock.PreviousClose;
                        stockInDb.ChangePercent = updatedStock.ChangePercent;
                        stockInDb.LastUpdated = DateTime.UtcNow;

                        if(stockInDb.CurrentPrice == 0)
                        {
                            stockInDb.CurrentPrice = updatedStock.PreviousClose;
                        }
                    }
                    else
                    {
                        _context.Stocks.Add(updatedStock);
                    }
                }

                await _context.SaveChangesAsync();
                stocksInDb = await _context.Stocks
                    .Where(s => tickerList.Contains(s.Ticker))
                    .ToListAsync(); // Refetch stocks from DB after update
            }

            // Return the stock data
            return Ok(stocksInDb);
        }

        // Save or update user's watchlist
        [HttpPost("save-watchlist")]
        public async Task<IActionResult> SaveWatchlist([FromBody] string[] tickers)
        {
            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Unauthorized();
            }

            var watchlist = await _context.Watchlists.FirstOrDefaultAsync(w => w.UserId == user.Id);

            if (watchlist == null)
            {
                watchlist = new Watchlist
                {
                    UserId = user.Id,
                    Tickers = string.Join(",", tickers)
                };
                _context.Watchlists.Add(watchlist);
            }
            else
            {
                watchlist.Tickers = string.Join(",", tickers);
                watchlist.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // Get the watchlist
        [HttpGet("get-watchlist")]
        public async Task<IActionResult> GetWatchlist()
        {
            // Get user email from the JWT token
            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            // Find the user
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Unauthorized();
            }

            // Find the watchlist for this user
            var watchlist = await _context.Watchlists
                .FirstOrDefaultAsync(w => w.UserId == user.Id);

            if (watchlist == null)
            {
                return Ok(new { Tickers = "" }); // Return empty watchlist if none exists
            }

            return Ok(watchlist);
        }

        // Add ticker to the watchlist
        [HttpPost("add-to-watchlist")]
        public async Task<IActionResult> AddToWatchlist([FromBody] AddStockRequest request)
        {
            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Unauthorized();
            }

            // Find the user's watchlist
            var watchlist = await _context.Watchlists
                .FirstOrDefaultAsync(w => w.UserId == user.Id);

            if (watchlist == null)
            {
                // If the user doesn't have a watchlist yet, create a new one
                watchlist = new Watchlist
                {
                    UserId = user.Id,
                    Tickers = request.Ticker,
                    LastUpdated = DateTime.UtcNow
                };
                _context.Watchlists.Add(watchlist);
            }
            else
            {
                // Check if the ticker already exists in the watchlist
                var tickers = watchlist.Tickers.Split(',').ToList();
                if (tickers.Contains(request.Ticker))
                {
                    return BadRequest("Ticker already in watchlist.");
                }

                // Append the new ticker to the comma-separated list
                watchlist.Tickers += $",{request.Ticker}";
                watchlist.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(watchlist);
        }

        [HttpPost("remove-from-watchlist")]
        public async Task<IActionResult> RemoveFromWatchlist([FromBody] RemoveFromWatchlistRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Ticker))
            {
                return BadRequest("Ticker is required.");
            }

            var email = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Unauthorized();
            }

            var watchlist = await _context.Watchlists.FirstOrDefaultAsync(w => w.UserId == user.Id);
            if (watchlist == null)
            {
                return NotFound("Watchlist not found.");
            }

            var tickers = watchlist.Tickers.Split(',').ToList();
            if (!tickers.Contains(request.Ticker))
            {
                return NotFound("Ticker not found in watchlist.");
            }

            tickers.Remove(request.Ticker);
            watchlist.Tickers = string.Join(",", tickers);

            _context.Watchlists.Update(watchlist);
            await _context.SaveChangesAsync();

            return Ok("Ticker removed from watchlist.");
        }


    }
}
