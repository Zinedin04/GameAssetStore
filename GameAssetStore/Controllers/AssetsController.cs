using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GameAssetStore.Data;
using GameAssetStore.Models;
using System.Text.Json;
using System.Text;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Stripe;
using Stripe.V2;

namespace GameAssetStore.Controllers
{
    public class AssetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _config;
        public AssetsController(ApplicationDbContext context, IHttpClientFactory clientFactory, IConfiguration config)
        {
            _context = context;
            _clientFactory = clientFactory;
            _config = config;
        }

        // GET: Assets
        public async Task<IActionResult> Index()
        {
            return View(await _context.Asset.ToListAsync());
        }

        // GET: Assets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset
                .FirstOrDefaultAsync(m => m.Id == id);
            if (asset == null)
            {
                return NotFound();
            }

            return View(asset);
        }

        // GET: Assets/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Assets/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Price")] Asset asset, IFormFile file)
        {

            if (file == null || file.Length == 0)
                {
                ModelState.AddModelError(string.Empty, "Please select a file to upload.");
                    return View(asset);
                }
            
            if (ModelState.IsValid)
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();
                var base64Content = Convert.ToBase64String(fileBytes);

                string folder;
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension == ".obj") folder = "assets/obj";
                else if (extension == ".fbx") folder = "assets/fbx";
                else folder = "assets/other"; // fallback za druge tipove

                var filePath = $"{folder}/{file.FileName}";
                var owner = _config["Github:Owner"];
                var repo = _config["Github:Repo"];
                var token = _config["Github:Token"];

                var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}";

                var body = new
                {
                    message = $"Upload{file.FileName} from web app",
                    content = base64Content
                };

                var client = _clientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "GameAssetApp");

                var json = JsonSerializer.Serialize(body);
                var response = await client.PutAsync(apiUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                var resultJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest($"Github upload failed: {resultJson}");  

                using var doc = JsonDocument.Parse(resultJson);
                asset.Url = doc.RootElement.GetProperty("content").TryGetProperty("html_url", out var html)
    ? html.GetString()
    : doc.RootElement.GetProperty("html_url").GetString();

                _context.Add(asset);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(asset);
        }

        // GET: Assets/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset.FindAsync(id);
            if (asset == null)
            {
                return NotFound();
            }
            return View(asset);
        }

        // POST: Assets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Url,Price")] Asset asset)
        {
            if (id != asset.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var newFile = Request.Form.Files.FirstOrDefault();
                    if (newFile == null || newFile.Length == 0)
                    {
                        return BadRequest("No file selected");
                    }
                    var owner = _config["Github:Owner"];
                    var repo = _config["Github:Repo"];
                    var token = _config["Github:Token"];

                    var relativePath = asset.Url.Split("blob/main").Last().TrimStart('/');

                    var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{relativePath}";

                    var client = _clientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    client.DefaultRequestHeaders.Add("User-Agent", "GameAssetApp");

                    var getResponse = await client.GetAsync(apiUrl);
                    if (!getResponse.IsSuccessStatusCode)
                    {
                        return BadRequest($"Cannot get file info: {await getResponse.Content.ReadAsStringAsync()}");
                    }

                    var json = await getResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var sha = doc.RootElement.GetProperty("sha").GetString();

                    using var memoryStream = new MemoryStream();
                    await newFile.CopyToAsync(memoryStream);
                    var fileBytes = memoryStream.ToArray();
                    var base64Content = Convert.ToBase64String(fileBytes);

                    var updateBody = new
                    {
                        message = $"Update {relativePath} via web app",
                        content = base64Content,
                        sha = sha
                    };
                    var updateJson = JsonSerializer.Serialize(updateBody);
                    var updateRequest = new HttpRequestMessage
                    {
                        Method = HttpMethod.Put,
                        RequestUri = new Uri(apiUrl),
                        Content = new StringContent(updateJson, Encoding.UTF8, "application/json")
                    };

                    var updateResponse = await client.SendAsync(updateRequest);
                    var updateResult = await updateResponse.Content.ReadAsStringAsync();

                    if (!updateResponse.IsSuccessStatusCode)
                        return BadRequest($"GitHub update failed: {updateResult}");

                    _context.Update(asset);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssetExists(asset.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(asset);
        }

        // GET: Assets/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var asset = await _context.Asset
                .FirstOrDefaultAsync(m => m.Id == id);
            if (asset == null)
            {
                return NotFound();
            }

            return View(asset);
        }

        // POST: Assets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var asset = await _context.Asset.FindAsync(id);
            if (asset == null)
               return NotFound();

            var owner = _config["Github:Owner"];
            var repo = _config["Github:Repo"];
            var token = _config["Github:Token"];

            var relativePath = asset.Url.Split("blob/main").Last().TrimStart('/');

            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{relativePath}";

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("User-Agent", "GameAssetApp");

            var getResponse = await client.GetAsync(apiUrl);
            if (!getResponse.IsSuccessStatusCode)
            {
                return BadRequest($"Cannot get file info: {await getResponse.Content.ReadAsStringAsync()}");
            }

            var json = await getResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var sha = doc.RootElement.GetProperty("sha").GetString();

            var deleteBody = new
            {
                message = $"Delete {relativePath} via web app",
                sha = sha
            };

            var deleteJson = JsonSerializer.Serialize(deleteBody);
            var deleteResponse = await client.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(apiUrl),
                Content = new StringContent(deleteJson, Encoding.UTF8, "application/json")
            });
            var deleteResult = await deleteResponse.Content.ReadAsStringAsync();
            if (!deleteResponse.IsSuccessStatusCode)
            {
                return BadRequest($"GitHub delete failed: {deleteResult}");
            }
            _context.Asset.Remove(asset);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AssetExists(int id)
        {
            return _context.Asset.Any(e => e.Id == id);
        }

        public async Task<IActionResult> Buy(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var asset = await _context.Asset
                .FirstOrDefaultAsync(m => m.Id == id);
            if (asset == null)
            {
                return NotFound();
            }
            StripeConfiguration.ApiKey = "sk_test_51SK1yGLQpNBsAdrs19iQ3BnB4yPYDxDqJ1LraisMOT50yUSODhjTAcOD49a7A5b7IPhUUv79zdLk7g6lWWay9ab700fbCy7WAN";

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                SuccessUrl = "https://localhost:7169/payments/success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = "https://localhost:7169/Assets",
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
        {
            new Stripe.Checkout.SessionLineItemOptions
            {
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)(asset.Price * 100),
                    Currency = "usd",
                    ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                    {
                        Name = asset.Name
                    },
                },
                Quantity = 1,
            }
        },
                Mode = "payment"
            };

            var service = new Stripe.Checkout.SessionService();
            var session = service.Create(options);

            // Redirect korisnika na Stripe Checkout
            return Redirect(session.Url);
        }
        [HttpGet("payments/success")]
        public async Task<IActionResult> Success(string session_id)
        {
            var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(session_id);

            // Ovdje provjeriš bazu ili session
            return View();
        }
    }
}
