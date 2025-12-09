using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // ============== CẤU HÌNH HOME (JSON) ==============

        private string GetHomeConfigPath()
        {
            var folder = Path.Combine(_env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "home-config.json");
        }

        private HomeDisplayConfigVM LoadHomeConfig()
        {
            var path = GetHomeConfigPath();
            if (!System.IO.File.Exists(path))
                return new HomeDisplayConfigVM();

            try
            {
                var json = System.IO.File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<HomeDisplayConfigVM>(json);
                return cfg ?? new HomeDisplayConfigVM();
            }
            catch
            {
                return new HomeDisplayConfigVM();
            }
        }

        private void SaveHomeConfig(HomeDisplayConfigVM cfg)
        {
            var path = GetHomeConfigPath();
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            System.IO.File.WriteAllText(path, json);
        }

        // ============== TRANG CHỦ ==============

        public async Task<IActionResult> Index()
        {
            var homeCfg = LoadHomeConfig();
            var moviesQ = _db.Movies.AsNoTracking();

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
                    Duration = m.Duration,
                    Country = m.Country,
                    AudioOption = m.AudioOption
                });

            // HERO: chỉ phim ĐANG CHIẾU (RELEASED) để đồng bộ với trang EditBanner
            var heroRaw = await Project(
                                moviesQ.Where(m => m.StatusId == "RELEASED")
                                       .OrderByDescending(m => m.UpdatedAt ?? m.ReleaseDate)
                           ).ToListAsync();

            if (homeCfg.HiddenBannerMovieIds != null && homeCfg.HiddenBannerMovieIds.Any())
            {
                heroRaw = heroRaw
                    .Where(x => !homeCfg.HiddenBannerMovieIds.Contains(x.MoviesId))
                    .ToList();
            }

            var carousel = heroRaw
                .DistinctBy(x => x.MoviesId)
                .Take(6)
                .ToList();

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

            // TRAILERS cho Home (lọc theo config và cắt 8)
            var trailersRaw = await moviesQ
                                .Where(m => m.TrailerLink != null)
                                .OrderByDescending(m => m.ReleaseDate)
                                .Select(m => new { m.MoviesId, m.Title, m.TrailerLink, m.PosterImage })
                                .ToListAsync();

            if (homeCfg.HomeTrailerMovieIds != null && homeCfg.HomeTrailerMovieIds.Any())
            {
                trailersRaw = trailersRaw
                    .Where(t => homeCfg.HomeTrailerMovieIds.Contains(t.MoviesId))
                    .ToList();
            }

            trailersRaw = trailersRaw.Take(8).ToList();

            var vm = new HomeVM
            {
                Carousel = carousel,
                NowShowing = nowShowing.DistinctBy(x => x.MoviesId).ToList(),
                ComingSoon = comingSoon.DistinctBy(x => x.MoviesId).ToList(),
                Trailers = trailersRaw
                            .Select(t => (t.MoviesId, t.Title ?? string.Empty, t.TrailerLink, t.PosterImage))
                            .ToList(),
                ExtraBanners = homeCfg.ExtraBanners ?? new List<HomeDisplayConfigVM.ExtraBannerItem>()
            };

            return View(vm);
        }

        // ============== DANH SÁCH PHIM THEO STATUS (XEM THÊM) ==============

        [HttpGet]
        public async Task<IActionResult> ListByStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return NotFound();

            var moviesQ = _db.Movies.AsNoTracking()
                                    .Where(m => m.StatusId == status);

            var cards = await moviesQ
                .Select(m => new MovieCardVM
                {
                    MoviesId = m.MoviesId,
                    Title = m.Title ?? string.Empty,
                    PosterImage = m.PosterImage,
                    Summary = m.Summary,
                    GenreName = (from mg in _db.MoviesGenres
                                 join g in _db.Genres on mg.GenresId equals g.GenresId
                                 where mg.MoviesId == m.MoviesId
                                 select g.Name).FirstOrDefault() ?? "Khác",
                    ReleaseDate = m.ReleaseDate,
                    StatusId = m.StatusId,
                    StatusName = _db.Statuses
                                    .Where(s => s.StatusId == m.StatusId)
                                    .Select(s => s.Name)
                                    .FirstOrDefault(),
                    Duration = m.Duration,
                    Country = m.Country,
                    AudioOption = m.AudioOption
                })
                .OrderByDescending(x => x.ReleaseDate)
                .ToListAsync();

            var genres = cards.Select(c => c.GenreName ?? "Khác")
                              .Distinct()
                              .OrderBy(x => x)
                              .ToList();

            ViewData["Title"] = status switch
            {
                "RELEASED" => "Phim đang chiếu",
                "COMING" => "Phim sắp chiếu",
                _ => "Danh sách phim"
            };

            var vm = new MovieListVM
            {
                Movies = cards,
                Genres = genres
            };

            return View("~/Views/Home/ListByStatus.cshtml", vm);
        }

        // ============== QUẢN LÝ BANNER HOME (PHÂN TRANG) ==============

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> EditBanner(int page = 1)
        {
            const int pageSize = 8;
            if (page < 1) page = 1;

            var cfg = LoadHomeConfig();

            var movies = await _db.Movies
                .AsNoTracking()
                .Where(m => m.StatusId == "RELEASED")
                .OrderByDescending(m => m.ReleaseDate)
                .ToListAsync();

            var allBannerItems = movies.Select(m =>
            {
                var img = !string.IsNullOrWhiteSpace(m.BannerImage)
                    ? m.BannerImage
                    : (string.IsNullOrWhiteSpace(m.PosterImage) ? "/images/no-poster.png" : m.PosterImage);

                return new BannerMovieItemVM
                {
                    MoviesId = m.MoviesId,
                    Title = m.Title ?? string.Empty,
                    ImageUrl = img,
                    IsHidden = cfg.HiddenBannerMovieIds.Contains(m.MoviesId)
                };
            }).ToList();

            var totalCount = allBannerItems.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var pageItems = allBannerItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new BannerManageVM
            {
                MovieBanners = pageItems,
                ExtraBanners = cfg.ExtraBanners ?? new List<HomeDisplayConfigVM.ExtraBannerItem>(),
                PageIndex = page,
                TotalPages = totalPages
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> EditBanner(
            List<BannerMovieItemVM> MovieBanners,
            IFormFile? extraBannerFile,
            string? actionType,
            int page = 1)
        {
            const int pageSize = 8;
            var cfg = LoadHomeConfig();

            if (string.Equals(actionType, "SaveMovieFlags", StringComparison.OrdinalIgnoreCase))
            {
                var set = new HashSet<string>(cfg.HiddenBannerMovieIds ?? new List<string>());

                foreach (var mv in MovieBanners)
                {
                    if (mv.IsHidden) set.Add(mv.MoviesId);
                    else set.Remove(mv.MoviesId);
                }

                cfg.HiddenBannerMovieIds = set.ToList();
                SaveHomeConfig(cfg);
            }
            else if (string.Equals(actionType, "AddExtra", StringComparison.OrdinalIgnoreCase))
            {
                if (extraBannerFile != null && extraBannerFile.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "images", "extra-banners");
                    Directory.CreateDirectory(folder);

                    var fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(extraBannerFile.FileName)}";
                    var fullPath = Path.Combine(folder, fileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                        await extraBannerFile.CopyToAsync(stream);

                    var list = cfg.ExtraBanners ?? new List<HomeDisplayConfigVM.ExtraBannerItem>();
                    var nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;

                    list.Add(new HomeDisplayConfigVM.ExtraBannerItem
                    {
                        Id = nextId,
                        ImagePath = $"/images/extra-banners/{fileName}"
                    });

                    cfg.ExtraBanners = list;
                    SaveHomeConfig(cfg);
                }
            }

            return RedirectToAction(nameof(EditBanner), new { page });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult DeleteExtraBanner(int deleteExtraId)
        {
            var cfg = LoadHomeConfig();
            if (cfg.ExtraBanners != null && cfg.ExtraBanners.Any())
            {
                var item = cfg.ExtraBanners.FirstOrDefault(x => x.Id == deleteExtraId);
                if (item != null)
                {
                    cfg.ExtraBanners.Remove(item);
                }
                SaveHomeConfig(cfg);
            }
            return RedirectToAction(nameof(EditBanner));
        }

        // ============== QUẢN LÝ TRAILER HOT (PHÂN TRANG) ==============

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageTrailers(int page = 1)
        {
            const int pageSize = 8;
            if (page < 1) page = 1;

            var cfg = LoadHomeConfig();

            var movies = await _db.Movies
                .AsNoTracking()
                .Where(m => m.TrailerLink != null)
                .OrderByDescending(m => m.ReleaseDate)
                .ToListAsync();

            var allItems = movies.Select(m => new TrailerManageItemVM
            {
                MoviesId = m.MoviesId,
                Title = m.Title ?? string.Empty,
                TrailerLink = m.TrailerLink,
                ShowOnHome = cfg.HomeTrailerMovieIds.Contains(m.MoviesId)
            }).ToList();

            var totalCount = allItems.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var pageItems = allItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new TrailerManageVM
            {
                Items = pageItems,
                PageIndex = page,
                TotalPages = totalPages
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult ManageTrailers(TrailerManageVM model, int page = 1)
        {
            var cfg = LoadHomeConfig();
            var set = new HashSet<string>(cfg.HomeTrailerMovieIds ?? new List<string>());

            foreach (var it in model.Items)
            {
                if (it.ShowOnHome) set.Add(it.MoviesId);
                else set.Remove(it.MoviesId);
            }

            cfg.HomeTrailerMovieIds = set.ToList();
            SaveHomeConfig(cfg);

            return RedirectToAction(nameof(ManageTrailers), new { page });
        }

        // ============== ERROR & DETAIL ==============

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

            // Thể loại
            var genres = await (from mg in _db.MoviesGenres
                                join g in _db.Genres on mg.GenresId equals g.GenresId
                                where mg.MoviesId == id
                                select g.Name).ToListAsync();
            ViewBag.GenreText = genres.Any() ? string.Join(", ", genres) : "Khác";

            // ===== Lấy role id cho ĐẠO DIỄN / DIỄN VIÊN =====
            var roles = await _db.MovieRoles.ToListAsync();

            string? dirRoleId = roles
                .FirstOrDefault(r => (r.Name ?? "").ToLower().Contains("đạo")
                                     || (r.Name ?? "").ToLower().Contains("director"))
                ?.MovieRoleId;

            string? actRoleId = roles
                .FirstOrDefault(r => (r.Name ?? "").ToLower().Contains("diễn")
                                     || (r.Name ?? "").ToLower().Contains("actor"))
                ?.MovieRoleId;

            var directorNames = new List<string>();
            var actorNames = new List<string>();

            // Đạo diễn
            if (dirRoleId != null)
            {
                var directorIds = await _db.MoviesParticipants
                    .Where(mp => mp.MoviesId == id && mp.MovieRoleId == dirRoleId)
                    .Select(mp => mp.ParticipantsId)
                    .ToListAsync();

                if (directorIds.Any())
                {
                    directorNames = await _db.Participants
                        .Where(p => directorIds.Contains(p.ParticipantsId))
                        .OrderBy(p => p.NickName ?? p.BirthName)
                        .Select(p => p.NickName ?? p.BirthName ?? p.ParticipantsId)
                        .ToListAsync();
                }
            }

            // Diễn viên
            if (actRoleId != null)
            {
                var actorIds = await _db.MoviesParticipants
                    .Where(mp => mp.MoviesId == id && mp.MovieRoleId == actRoleId)
                    .Select(mp => mp.ParticipantsId)
                    .ToListAsync();

                if (actorIds.Any())
                {
                    actorNames = await _db.Participants
                        .Where(p => actorIds.Contains(p.ParticipantsId))
                        .OrderBy(p => p.NickName ?? p.BirthName)
                        .Select(p => p.NickName ?? p.BirthName ?? p.ParticipantsId)
                        .ToListAsync();
                }
            }

            // Chuẩn bị text hiển thị
            var directorText = directorNames.Any()
                ? string.Join(", ", directorNames)
                : "Chưa có thông tin";

            string castText;
            if (!actorNames.Any())
            {
                castText = "Chưa có thông tin";
            }
            else
            {
                var first4 = actorNames.Take(4).ToList();
                castText = first4.Count < actorNames.Count
                    ? string.Join(", ", first4) + ", ..."
                    : string.Join(", ", first4);
            }

            ViewBag.DirectorText = directorText;
            ViewBag.CastText = castText;

            return View("~/Views/Home/Detail.cshtml", movie);
        }

        // ============== ADMIN BOOKING MOVIES - DANH SÁCH PHIM CHO ADMIN ==============

        [Authorize(Roles = "Admin")]
        [HttpGet("AdminBooking")]
        public async Task<IActionResult> AdminBookingMovies()
        {
            var homeCfg = LoadHomeConfig();
            var moviesQ = _db.Movies.AsNoTracking();

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
                    Duration = m.Duration,
                    Country = m.Country,
                    AudioOption = m.AudioOption
                });

            // HERO: chỉ phim ĐANG CHIẾU (RELEASED) để đồng bộ với trang EditBanner
            var heroRaw = await Project(
                                moviesQ.Where(m => m.StatusId == "RELEASED")
                                       .OrderByDescending(m => m.UpdatedAt ?? m.ReleaseDate)
                           ).ToListAsync();

            if (homeCfg.HiddenBannerMovieIds != null && homeCfg.HiddenBannerMovieIds.Any())
            {
                heroRaw = heroRaw
                    .Where(x => !homeCfg.HiddenBannerMovieIds.Contains(x.MoviesId))
                    .ToList();
            }

            var carousel = heroRaw
                .DistinctBy(x => x.MoviesId)
                .Take(6)
                .ToList();

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

            // TRAILERS cho Home (lọc theo config và cắt 8)
            var trailersRaw = await moviesQ
                                .Where(m => m.TrailerLink != null)
                                .OrderByDescending(m => m.ReleaseDate)
                                .Select(m => new { m.MoviesId, m.Title, m.TrailerLink, m.PosterImage })
                                .ToListAsync();

            if (homeCfg.HomeTrailerMovieIds != null && homeCfg.HomeTrailerMovieIds.Any())
            {
                trailersRaw = trailersRaw
                    .Where(t => homeCfg.HomeTrailerMovieIds.Contains(t.MoviesId))
                    .ToList();
            }

            trailersRaw = trailersRaw.Take(8).ToList();

            var vm = new HomeVM
            {
                Carousel = carousel,
                NowShowing = nowShowing.DistinctBy(x => x.MoviesId).ToList(),
                ComingSoon = comingSoon.DistinctBy(x => x.MoviesId).ToList(),
                Trailers = trailersRaw
                            .Select(t => (t.MoviesId, t.Title ?? string.Empty, t.TrailerLink, t.PosterImage))
                            .ToList(),
                ExtraBanners = homeCfg.ExtraBanners ?? new List<HomeDisplayConfigVM.ExtraBannerItem>()
            };

            return View("~/Views/Home/AdminBookingMovies.cshtml", vm);
        }

    }
}
