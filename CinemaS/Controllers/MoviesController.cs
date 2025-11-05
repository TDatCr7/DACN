using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using CinemaS.Models;
using CinemaS.Models.ViewModels;

namespace CinemaS.Controllers
{
    public class MoviesController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IWebHostEnvironment _env;

        public MoviesController(CinemaContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /* ===================== Admin: Cập nhật banner ===================== */
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateBanner(string id, IFormFile file)
        {
            if (string.IsNullOrWhiteSpace(id) || file == null || file.Length == 0)
                return BadRequest("Thiếu dữ liệu!");

            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            var folder = Path.Combine(_env.WebRootPath, "images", "banners");
            Directory.CreateDirectory(folder);

            var fileName = $"{id}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
            var savePath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(savePath, FileMode.Create))
                await file.CopyToAsync(stream);

            movie.BannerImage = $"/images/banners/{fileName}";
            movie.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Message"] = "Đã cập nhật ảnh banner!";
            return RedirectToAction("Index", "Home");
        }

        /* ===================== Helpers ===================== */
        private void LoadStatuses(object? selected = null)
        {
            var statuses = _context.Statuses
                .Select(s => new { s.StatusId, s.Name })
                .OrderBy(s => s.Name)
                .ToList();
            ViewBag.StatusId = new SelectList(statuses, "StatusId", "Name", selected);
        }

        private async Task<string?> SaveImageAsync(IFormFile file, string subFolder)
        {
            if (file == null || file.Length == 0) return null;

            var valid = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!valid.Contains(ext)) throw new InvalidOperationException("Định dạng ảnh không hợp lệ (JPG/PNG/GIF/WEBP).");
            if (file.Length > 10 * 1024 * 1024) throw new InvalidOperationException("Ảnh quá lớn (>10MB).");

            var imagesRoot = Path.Combine(_env.WebRootPath, "images", subFolder);
            Directory.CreateDirectory(imagesRoot);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(imagesRoot, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return $"/images/{subFolder}/{fileName}";
        }

        private void TryDeletePhysicalFile(string? webPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webPath)) return;
                var trimmed = webPath.StartsWith("/") ? webPath[1..] : webPath;
                var full = Path.Combine(_env.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { }
        }

        private static string RemoveVietnameseTones(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var normalized = input.Normalize(System.Text.NormalizationForm.FormD);
            var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }

        private async Task<List<MovieCardVM>> GetMovieCardsAsync()
        {
            var raw = await (from m in _context.Movies
                             join st in _context.Statuses on m.StatusId equals st.StatusId
                             join mg in _context.MoviesGenres on m.MoviesId equals mg.MoviesId into gj
                             from mg in gj.DefaultIfEmpty()
                             join g in _context.Genres on mg.GenresId equals g.GenresId into gj2
                             from g in gj2.DefaultIfEmpty()
                             select new { m, GenreName = g != null ? g.Name : null, st.StatusId, StatusName = st.Name })
                            .ToListAsync();

            var cards = raw.GroupBy(x => x.m.MoviesId)
                           .Select(gr => {
                               var first = gr.First();
                               return new MovieCardVM
                               {
                                   MoviesId = first.m.MoviesId,
                                   Title = first.m.Title ?? "(No Title)",
                                   PosterImage = first.m.PosterImage,
                                   Summary = first.m.Summary,
                                   GenreName = gr.Select(x => x.GenreName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "Khác",
                                   ReleaseDate = first.m.ReleaseDate,
                                   StatusId = first.StatusId,
                                   StatusName = first.StatusName
                               };
                           })
                           .ToList();
            return cards;
        }

        /* ===================== Index (card + filter) ===================== */
        public async Task<IActionResult> Index(string? genre = null, string? q = null, string? status = null, string? message = null, string? error = null)
        {
            var cards = await GetMovieCardsAsync();
            var genres = cards.Select(c => c.GenreName ?? "Khác").Distinct().OrderBy(s => s).ToList();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                cards = cards.Where(c => string.Equals(c.StatusId, status, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(genre) && genre != "all")
                cards = cards.Where(c => string.Equals(c.GenreName, genre, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = RemoveVietnameseTones(q).ToLowerInvariant();
                cards = cards.Where(c => RemoveVietnameseTones(c.Title).ToLowerInvariant().Contains(qLower)).ToList();
            }

            var vm = new MovieListVM { Movies = cards, Genres = genres, Message = message, Error = error };
            return View(vm);
        }

        /* ===================== Details ===================== */
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        /* ===================== “XEM THÊM” views ===================== */

        [HttpGet]
        public async Task<IActionResult> Showing()
        {
            var cards = await GetMovieCardsAsync();
            var filtered = cards.Where(c => string.Equals(c.StatusId, "RELEASED", StringComparison.OrdinalIgnoreCase)).ToList();
            ViewData["Title"] = "Phim đang chiếu";
            var vm = new MovieListVM { Movies = filtered, Genres = filtered.Select(x => x.GenreName ?? "Khác").Distinct().OrderBy(x => x).ToList() };

            return View("~/Views/Home/ListByStatus.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Upcoming()
        {
            var cards = await GetMovieCardsAsync();
            var filtered = cards.Where(c => string.Equals(c.StatusId, "COMING", StringComparison.OrdinalIgnoreCase)).ToList();
            ViewData["Title"] = "Phim sắp chiếu";
            var vm = new MovieListVM { Movies = filtered, Genres = filtered.Select(x => x.GenreName ?? "Khác").Distinct().OrderBy(x => x).ToList() };

            return View("~/Views/Home/ListByStatus.cshtml", vm);
        }


        /* ===================== Create ===================== */
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            LoadStatuses();
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MoviesId,StatusId,Title,Summary,DetailDescription,ReleaseDate,Language,Duration,Rating,Age,TrailerLink")] Movies movies,
                                                IFormFile? posterFile, IFormFile? bannerFile)
        {
            if (!ModelState.IsValid)
            {
                LoadStatuses(movies.StatusId);
                return View(movies);
            }

            try
            {
                if (posterFile != null)
                    movies.PosterImage = await SaveImageAsync(posterFile, "movies");

                if (bannerFile != null)
                    movies.BannerImage = await SaveImageAsync(bannerFile, "movies");

                movies.CreatedAt = DateTime.UtcNow;
                movies.UpdatedAt = DateTime.UtcNow;

                _context.Add(movies);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { message = "Tạo phim thành công." });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                LoadStatuses(movies.StatusId);
                return View(movies);
            }
        }

        /* ===================== Edit ===================== */
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            LoadStatuses(movie.StatusId);
            return View(movie);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id,
            [Bind("MoviesId,StatusId,Title,Summary,DetailDescription,ReleaseDate,Language,Duration,Rating,Age,TrailerLink,PosterImage,BannerImage,CreatedAt,UpdatedAt")]
            Movies formModel,
            IFormFile? posterFile,
            IFormFile? bannerFile)
        {
            if (id != formModel.MoviesId) return NotFound();
            if (!ModelState.IsValid)
            {
                LoadStatuses(formModel.StatusId);
                return View(formModel);
            }

            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == id);
            if (movie == null) return NotFound();

            try
            {
                movie.StatusId = formModel.StatusId;
                movie.Title = formModel.Title;
                movie.Summary = formModel.Summary;
                movie.DetailDescription = formModel.DetailDescription;
                movie.ReleaseDate = formModel.ReleaseDate;
                movie.Language = formModel.Language;
                movie.Duration = formModel.Duration;
                movie.Rating = formModel.Rating;
                movie.Age = formModel.Age;
                movie.TrailerLink = formModel.TrailerLink;
                movie.UpdatedAt = DateTime.UtcNow;

                if (posterFile != null)
                {
                    var newPath = await SaveImageAsync(posterFile, "movies");
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        TryDeletePhysicalFile(movie.PosterImage);
                        movie.PosterImage = newPath;
                    }
                }
                if (bannerFile != null)
                {
                    var newPath = await SaveImageAsync(bannerFile, "movies");
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        TryDeletePhysicalFile(movie.BannerImage);
                        movie.BannerImage = newPath;
                    }
                }

                _context.Update(movie);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { message = "Cập nhật phim thành công." });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                LoadStatuses(formModel.StatusId);
                return View(formModel);
            }
        }

        /* ===================== Delete ===================== */
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();
            var movies = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == id);
            if (movies == null) return NotFound();
            return View(movies);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var movies = await _context.Movies.FindAsync(id);
            if (movies != null)
            {
                TryDeletePhysicalFile(movies.PosterImage);
                TryDeletePhysicalFile(movies.BannerImage);
                _context.Movies.Remove(movies);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { message = "Đã xoá phim." });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMultiple([FromForm] List<string> movie_ids)
        {
            if (movie_ids == null || movie_ids.Count == 0)
                return RedirectToAction(nameof(Index), new { error = "Vui lòng chọn ít nhất một phim để xoá." });

            var items = await _context.Movies.Where(m => movie_ids.Contains(m.MoviesId)).ToListAsync();
            if (items.Count == 0)
                return RedirectToAction(nameof(Index), new { error = "Không tìm thấy phim để xoá." });

            foreach (var mv in items)
            {
                TryDeletePhysicalFile(mv.PosterImage);
                TryDeletePhysicalFile(mv.BannerImage);
            }

            _context.Movies.RemoveRange(items);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { message = $"Đã xoá {items.Count} phim." });
        }
    }
}
