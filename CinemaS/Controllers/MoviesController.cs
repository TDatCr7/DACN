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

        private async Task<(string? DirectorRoleId, string? ActorRoleId)> GetRoleIdsAsync()
        {
            // MovieRoles (DbSet tên đúng là số nhiều)
            var roles = await _context.MovieRoles.ToListAsync();

            string? dirId = roles
                .FirstOrDefault(r => (r.Name ?? "").ToLower().Contains("đạo") || (r.Name ?? "").ToLower().Contains("director"))
                ?.MovieRoleId;

            string? actId = roles
                .FirstOrDefault(r => (r.Name ?? "").ToLower().Contains("diễn") || (r.Name ?? "").ToLower().Contains("actor"))
                ?.MovieRoleId;

            return (dirId, actId);
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
                                   StatusName = first.StatusName,
                                   Duration = first.m.Duration,
                                   Language = first.m.Language,
                                   Rating = first.m.Rating?.ToString(),
                                   Age = first.m.Age?.ToString(),
                                   // bổ sung cho hover card
                                   // 2 field sau để hiển thị như bạn yêu cầu
                                   // (không bắt buộc nếu view không dùng)
                                   // Country & AudioOption đã có trong Movies
                               };
                           })
                           .ToList();
            return cards;
        }

        /* ===================== Index (card + filter) ===================== */
        public async Task<IActionResult> Index(
            string? genre = null,
            string? q = null,
            string? status = null,
            string? message = null,
            string? error = null,
            int page = 1)
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

            // ===== PHÂN TRANG (8 phim / trang) =====
            const int pageSize = 8;
            if (page < 1) page = 1;

            var totalCount = cards.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var pagedCards = cards
                .OrderByDescending(c => c.ReleaseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new MovieListVM
            {
                Movies = pagedCards,
                Genres = genres,
                Message = message,
                Error = error,
                PageIndex = page,
                TotalPages = totalPages
            };
            return View(vm);
        }


        /* ===================== Details (kèm đạo diễn & diễn viên) ===================== */
        // using System.Linq;
        // using Microsoft.EntityFrameworkCore;

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var movie = await _context.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == id);
            if (movie == null) return NotFound();

            // Thể loại (lấy 1 tên chính, nếu bạn muốn nhiều thì join và join bằng ", ")
            var genreText = await (from mg in _context.MoviesGenres
                                   join g in _context.Genres on mg.GenresId equals g.GenresId
                                   where mg.MoviesId == id
                                   select g.Name).FirstOrDefaultAsync();
            ViewBag.GenreText = string.IsNullOrWhiteSpace(genreText) ? "Khác" : genreText;

            // Lấy IDs vai trò
            var directorIds = await _context.MoviesParticipants
                .Where(x => x.MoviesId == id && x.MovieRoleId == "DIRECTOR")
                .Select(x => x.ParticipantsId)
                .ToListAsync();

            var actorIds = await _context.MoviesParticipants
                .Where(x => x.MoviesId == id && x.MovieRoleId == "ACTOR")
                .Select(x => x.ParticipantsId)
                .ToListAsync();

            // Lấy tên hiển thị
            async Task<List<string>> GetNamesAsync(List<string> ids) =>
                await _context.Participants
                    .Where(p => ids.Contains(p.ParticipantsId))
                    .Select(p => p.NickName ?? p.BirthName ?? p.ParticipantsId)
                    .ToListAsync();

            var directors = await GetNamesAsync(directorIds);
            var actors = await GetNamesAsync(actorIds);

            ViewBag.Directors = directors.Count == 0 ? new List<string> { "Chưa có thông tin" } : directors;
            ViewBag.Actors = actors.Count == 0 ? new List<string> { "Chưa có thông tin" } : actors;

            return View(movie);
        }


        /* ===================== “Xem thêm” views ===================== */
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
            ViewBag.Participants = _context.Participants
                .OrderBy(p => p.NickName ?? p.BirthName).ToList();
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("MoviesId,StatusId,Title,Summary,DetailDescription,ReleaseDate,Language,Duration,Rating,Age,TrailerLink,Country,AudioOption")]
            Movies movies,
            IFormFile? posterFile, IFormFile? bannerFile,
            string[] selectedDirectors, string[] selectedActors)
        {
            if (!ModelState.IsValid)
            {
                LoadStatuses(movies.StatusId);
                ViewBag.Participants = _context.Participants.OrderBy(p => p.NickName ?? p.BirthName).ToList();
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

                // map đạo diễn / diễn viên
                var (dirRoleId, actRoleId) = await GetRoleIdsAsync();
                var links = new List<MoviesParticipants>();

                if (dirRoleId != null && selectedDirectors != null)
                    links.AddRange(selectedDirectors.Select(pid => new MoviesParticipants
                    {
                        MovieParticipantId = Guid.NewGuid().ToString("N")[..10],
                        MoviesId = movies.MoviesId,
                        ParticipantsId = pid,
                        MovieRoleId = dirRoleId
                    }));

                if (actRoleId != null && selectedActors != null)
                    links.AddRange(selectedActors.Select(pid => new MoviesParticipants
                    {
                        MovieParticipantId = Guid.NewGuid().ToString("N")[..10],
                        MoviesId = movies.MoviesId,
                        ParticipantsId = pid,
                        MovieRoleId = actRoleId
                    }));

                if (links.Count > 0)
                {
                    _context.MoviesParticipants.AddRange(links);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index), new { message = "Tạo phim thành công." });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                LoadStatuses(movies.StatusId);
                ViewBag.Participants = _context.Participants.OrderBy(p => p.NickName ?? p.BirthName).ToList();
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

            var (dirRoleId, actRoleId) = await GetRoleIdsAsync();

            var currentDirectors = await _context.MoviesParticipants
                .Where(mp => mp.MoviesId == id && mp.MovieRoleId == dirRoleId)
                .Select(mp => mp.ParticipantsId).ToListAsync();

            var currentActors = await _context.MoviesParticipants
                .Where(mp => mp.MoviesId == id && mp.MovieRoleId == actRoleId)
                .Select(mp => mp.ParticipantsId).ToListAsync();

            ViewBag.Participants = _context.Participants.OrderBy(p => p.NickName ?? p.BirthName).ToList();
            ViewBag.CurrentDirectors = currentDirectors;
            ViewBag.CurrentActors = currentActors;

            return View(movie);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id,
            [Bind("MoviesId,StatusId,Title,Summary,DetailDescription,ReleaseDate,Language,Duration,Rating,Age,TrailerLink,PosterImage,BannerImage,CreatedAt,UpdatedAt,Country,AudioOption")]
            Movies formModel,
            IFormFile? posterFile, IFormFile? bannerFile,
            string[] selectedDirectors, string[] selectedActors)
        {
            if (id != formModel.MoviesId) return NotFound();

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
                movie.Country = formModel.Country;
                movie.AudioOption = formModel.AudioOption;
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

                // cập nhật mapping participants
                var (dirRoleId, actRoleId) = await GetRoleIdsAsync();

                var oldLinks = _context.MoviesParticipants.Where(mp => mp.MoviesId == id);
                _context.MoviesParticipants.RemoveRange(oldLinks);

                var links = new List<MoviesParticipants>();
                if (dirRoleId != null && selectedDirectors != null)
                    links.AddRange(selectedDirectors.Select(pid => new MoviesParticipants
                    {
                        MovieParticipantId = Guid.NewGuid().ToString("N")[..10],
                        MoviesId = id,
                        ParticipantsId = pid,
                        MovieRoleId = dirRoleId
                    }));
                if (actRoleId != null && selectedActors != null)
                    links.AddRange(selectedActors.Select(pid => new MoviesParticipants
                    {
                        MovieParticipantId = Guid.NewGuid().ToString("N")[..10],
                        MoviesId = id,
                        ParticipantsId = pid,
                        MovieRoleId = actRoleId
                    }));

                if (links.Count > 0)
                    _context.MoviesParticipants.AddRange(links);

                _context.Update(movie);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { message = "Cập nhật phim thành công." });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                LoadStatuses(formModel.StatusId);
                ViewBag.Participants = _context.Participants.OrderBy(p => p.NickName ?? p.BirthName).ToList();
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

                // xóa links participants
                var links = _context.MoviesParticipants.Where(mp => mp.MoviesId == id);
                _context.MoviesParticipants.RemoveRange(links);

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
                var links = _context.MoviesParticipants.Where(mp => mp.MoviesId == mv.MoviesId);
                _context.MoviesParticipants.RemoveRange(links);
            }

            _context.Movies.RemoveRange(items);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { message = $"Đã xoá {items.Count} phim." });
        }
    }
}
