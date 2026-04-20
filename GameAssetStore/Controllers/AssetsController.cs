using GameAssetStore.Data;
using GameAssetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Evaluation;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.V2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Price")] Asset asset, IFormFile file, IFormFile? imageFile)
        {
            // Postavljanje OwnerId-a i čišćenje modela od polja koja se ručno popunjavaju
            asset.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ModelState.Remove("Url");
            ModelState.Remove("ImageUrl");
            ModelState.Remove("OwnerId");

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select a 3D model file to upload.");
                return View(asset);
            }

            if (ModelState.IsValid)
            {
                var owner = _config["Github:Owner"];
                var repo = _config["Github:Repo"];
                var token = _config["Github:Token"];

                var client = _clientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "GameAssetApp");

                // --- 1. UPLOAD MODELA ---
                var extension = Path.GetExtension(file.FileName).ToLower();
                string folder = extension switch
                {
                    ".obj" => "assets/obj",
                    ".fbx" => "assets/fbx",
                    _ => "assets/other"
                };

                var filePath = $"{folder}/{Guid.NewGuid()}_{file.FileName}";
                var modelUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}";

                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    var modelBody = new
                    {
                        message = $"Upload model {file.FileName}",
                        content = Convert.ToBase64String(ms.ToArray())
                    };

                    var response = await client.PutAsJsonAsync(modelUrl, modelBody);
                    if (!response.IsSuccessStatusCode)
                        return BadRequest("GitHub Model Upload Failed.");

                    var resJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(resJson);
                    asset.Url = doc.RootElement.GetProperty("content").GetProperty("html_url").GetString();
                }

                // --- 2. UPLOAD SLIKE (Opcionalno) ---
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imgExt = Path.GetExtension(imageFile.FileName).ToLower();
                    var imgPath = $"thumbnails/{Guid.NewGuid()}{imgExt}";
                    var imgApiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{imgPath}";

                    using (var ms = new MemoryStream())
                    {
                        await imageFile.CopyToAsync(ms);
                        var imgBody = new
                        {
                            message = $"Upload thumb for {asset.Name}",
                            content = Convert.ToBase64String(ms.ToArray())
                        };

                        var imgResponse = await client.PutAsJsonAsync(imgApiUrl, imgBody);
                        if (imgResponse.IsSuccessStatusCode)
                        {
                            // Koristimo raw.githubusercontent.com da bi slika bila direktno vidljiva u browseru
                            asset.ImageUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/main/{imgPath}";
                        }
                    }
                }

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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Url,ImageUrl,Price,OwnerId")] Asset asset, IFormFile? file, IFormFile? imageFile)
        {
            if (id != asset.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var owner = _config["Github:Owner"];
                    var repo = _config["Github:Repo"];
                    var token = _config["Github:Token"];

                    var client = _clientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    client.DefaultRequestHeaders.Add("User-Agent", "GameAssetApp");

                    // --- 1. UPDATE 3D MODELA ---
                    if (file != null && file.Length > 0)
                    {
                        // Izvuci SHA i putanju starog fajla iz trenutnog URL-a
                        var oldRelativePath = asset.Url.Split("blob/main/").Last();
                        var oldApiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{oldRelativePath}";

                        // Nova putanja (bazirana na novom fajlu)
                        var extension = Path.GetExtension(file.FileName).ToLower();
                        string folder = extension == ".obj" ? "assets/obj" : (extension == ".fbx" ? "assets/fbx" : "assets/other");
                        var newRelativePath = $"{folder}/{Guid.NewGuid()}_{file.FileName}";
                        var newApiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{newRelativePath}";

                        // Dohvati SHA starog fajla da ga možemo obrisati/zamijeniti
                        var getResponse = await client.GetAsync(oldApiUrl);
                        if (getResponse.IsSuccessStatusCode)
                        {
                            var json = await getResponse.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            var sha = doc.RootElement.GetProperty("sha").GetString();

                            using var ms = new MemoryStream();
                            await file.CopyToAsync(ms);
                            var base64Content = Convert.ToBase64String(ms.ToArray());

                            // Ako se putanja promijenila (npr. drugi format ili ime), obriši stari, stavi novi
                            if (oldRelativePath != newRelativePath)
                            {
                                // Brisanje starog
                                var deleteBody = new { message = $"Delete old file {oldRelativePath}", sha = sha };
                                await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, oldApiUrl)
                                {
                                    Content = new StringContent(JsonSerializer.Serialize(deleteBody), Encoding.UTF8, "application/json")
                                });

                                // Kreiranje novog
                                var createBody = new { message = $"Upload new version {file.FileName}", content = base64Content };
                                var createRes = await client.PutAsJsonAsync(newApiUrl, createBody);
                                if (createRes.IsSuccessStatusCode)
                                {
                                    var resJson = await createRes.Content.ReadAsStringAsync();
                                    using var resDoc = JsonDocument.Parse(resJson);
                                    asset.Url = resDoc.RootElement.GetProperty("content").GetProperty("html_url").GetString();
                                }
                            }
                            else
                            {
                                // Ako je putanja ista, samo klasični update sa SHA
                                var updateBody = new { message = $"Update {file.FileName}", content = base64Content, sha = sha };
                                var updateRes = await client.PutAsJsonAsync(oldApiUrl, updateBody);
                                if (updateRes.IsSuccessStatusCode)
                                {
                                    var resJson = await updateRes.Content.ReadAsStringAsync();
                                    using var resDoc = JsonDocument.Parse(resJson);
                                    asset.Url = resDoc.RootElement.GetProperty("content").GetProperty("html_url").GetString();
                                }
                            }
                        }
                    }

                    // --- 2. UPDATE SLIKE (THUMBNAILA) ---
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var imgExt = Path.GetExtension(imageFile.FileName).ToLower();
                        var imgPath = $"thumbnails/{Guid.NewGuid()}{imgExt}";
                        var imgApiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{imgPath}";

                        using (var ms = new MemoryStream())
                        {
                            await imageFile.CopyToAsync(ms);
                            var imgBody = new
                            {
                                message = $"Update thumbnail for {asset.Name}",
                                content = Convert.ToBase64String(ms.ToArray())
                            };

                            var imgResponse = await client.PutAsJsonAsync(imgApiUrl, imgBody);
                            if (imgResponse.IsSuccessStatusCode)
                            {
                                // Update-aj ImageUrl u asset objektu (obavezno raw link)
                                asset.ImageUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/main/{imgPath}";
                            }
                        }
                        // Opcionalno: Ovdje bi mogao dodati i kod da obriše staru sliku sa GitHub-a, 
                        // ali pošto su slike male, nije kritično kao za modele.
                    }

                    // 3. SPASI SVE U BAZU
                    _context.Update(asset);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssetExists(asset.Id)) return NotFound();
                    else throw;
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
                Mode = "payment",

                Metadata = new Dictionary<string, string>
        {
            { "AssetId", asset.Id.ToString() }
        }
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

            // Izvlačimo AssetId iz Stripe metapodataka
            if (session.Metadata.TryGetValue("AssetId", out var assetIdStr) && int.TryParse(assetIdStr, out var assetId))
            {
                var asset = await _context.Asset.FindAsync(assetId);
                return View(asset); // Šaljemo asset u View
            }

            return View();
        }
    }
}
