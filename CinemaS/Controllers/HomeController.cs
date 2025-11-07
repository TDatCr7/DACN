using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace CinemaS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CinemaContext _db;

        private readonly IWebHostEnvironment _env;
        public HomeController(ILogger<HomeController> logger, CinemaContext db, IWebHostEnvironment env)
        {
            _logger = logger;
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            // Base query
            var moviesQ = _db.Movies.AsNoTracking();

            // Project mạnh về MovieCardVM (Poster luôn là Poster; không trộn với Banner)
            IQueryable<MovieCardVM> Project(IQueryable<Movies> src) =>
                src.Select(m => new MovieCardVM
                {
                    MoviesId = m.MoviesId,
                    Title = m.Title ?? string.Empty,
                    PosterImage = m.PosterImage,
                    BannerImage = m.BannerImage,
                    Summary = m.Summary,
                    GenreName = (from mg in _db.MoviesGenres
                                 join g in _db.Genres on mg.GenresId equals g.GenresId
                                 where mg.MoviesId == m.MoviesId
                                 select g.Name).FirstOrDefault(),
                    ReleaseDate = m.ReleaseDate,
                    StatusId = m.StatusId,
                    StatusName = _db.Statuses
                                    .Where(s => s.StatusId == m.StatusId)
                                    .Select(s => s.Name)
                                    .FirstOrDefault(),
                    // NEW
                    Duration = m.Duration,
                    Country = m.Country,
                    AudioOption = m.AudioOption
                });

            // HERO (đang/sắp chiếu)
            var carousel = await Project(
                                moviesQ.Where(m => m.StatusId == "RELEASED" || m.StatusId == "COMING")
                                       .OrderByDescending(m => m.UpdatedAt ?? m.ReleaseDate)
                                       .Take(6)
                           ).ToListAsync();

            // ĐANG CHIẾU
            var nowShowing = await Project(
                                moviesQ.Where(m => m.StatusId == "RELEASED")
                                       .OrderByDescending(m => m.ReleaseDate)
                                       .Take(18)
                             ).ToListAsync();

            // SẮP CHIẾU
            var comingSoon = await Project(
                                moviesQ.Where(m => m.StatusId == "COMING")
                                       .OrderBy(m => m.ReleaseDate)
                                       .Take(18)
                             ).ToListAsync();

            // TRAILERS
            var trailersRaw = await moviesQ
                                .Where(m => m.TrailerLink != null)
                                .OrderByDescending(m => m.ReleaseDate)
                                .Select(m => new { m.MoviesId, m.Title, m.TrailerLink, m.PosterImage })
                                .Take(8)
                                .ToListAsync();

            var vm = new HomeVM
            {
                // Nếu project của bạn là .NET < 6 (không có DistinctBy), đổi 3 dòng dưới thành:
                // Carousel   = carousel.GroupBy(x => x.MoviesId).Select(g => g.First()).ToList();
                // NowShowing = nowShowing.GroupBy(x => x.MoviesId).Select(g => g.First()).ToList();
                // ComingSoon = comingSoon.GroupBy(x => x.MoviesId).Select(g => g.First()).ToList();
                Carousel = carousel.DistinctBy(x => x.MoviesId).ToList(),
                NowShowing = nowShowing.DistinctBy(x => x.MoviesId).ToList(),
                ComingSoon = comingSoon.DistinctBy(x => x.MoviesId).ToList(),
                Trailers = trailersRaw.Select(t => (t.MoviesId, t.Title ?? string.Empty, t.TrailerLink, t.PosterImage)).ToList()
            };

            return View(vm);
        }
        // ========== QUẢN LÝ ẢNH BANNER ==========
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditBanner(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var movie = await _db.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> EditBanner(string id, IFormFile? file)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var movie = await _db.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            if (file != null && file.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "images", "banners");
                Directory.CreateDirectory(folder);

                var fileName = $"{id}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
                var savePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                movie.BannerImage = $"/images/banners/{fileName}";
                movie.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                TempData["Message"] = "Cập nhật banner thành công!";
            }

            return RedirectToAction("EditBanner", new { id });
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> Detail(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var movie = await _db.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == id);
            if (movie == null) return NotFound();

            // Lấy danh sách thể loại của phim
            var genres = await (from mg in _db.MoviesGenres
                                join g in _db.Genres on mg.GenresId equals g.GenresId
                                where mg.MoviesId == id
                                select g.Name).ToListAsync();
            ViewBag.GenreText = genres.Any() ? string.Join(", ", genres) : "Khác";

            return View("~/Views/Home/Detail.cshtml", movie);
        }


    }
}
