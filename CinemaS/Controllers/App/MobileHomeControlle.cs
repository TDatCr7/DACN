// Controllers/Api/MobileHomeController.cs
// FIX TRIỆT ĐỂ:
// - DB + Web đã dùng Model.DetailDescription => Movies entity CÓ property DetailDescription
// - API movie detail PHẢI TRẢ detailDescription = movie.DetailDescription (KHÔNG fallback summary)
// - Nếu null/empty => trả "" để app hiện "Đang cập nhật…"
// - Giữ các field khác như hiện tại

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers.Api
{
    [ApiController]
    [Route("api/mobile/home")]
    public class MobileHomeController : ControllerBase
    {
        private readonly CinemaContext _db;
        private readonly IWebHostEnvironment _env;

        public MobileHomeController(CinemaContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private string GetHomeConfigPath()
        {
            var folder = Path.Combine(_env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "home-config.json");
        }

        private HomeCfg LoadHomeConfig()
        {
            var path = GetHomeConfigPath();
            if (!System.IO.File.Exists(path)) return new HomeCfg();
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<HomeCfg>(json);
                return cfg ?? new HomeCfg();
            }
            catch { return new HomeCfg(); }
        }

        private sealed class HomeCfg
        {
            public List<string> HiddenBannerMovieIds { get; set; } = new();
            public List<ExtraBannerItem> ExtraBanners { get; set; } = new();
            public sealed class ExtraBannerItem
            {
                public int Id { get; set; }
                public string? ImagePath { get; set; }
            }
        }

        private static string NormalizePath(string p)
        {
            p = (p ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) return "";
            if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return p;
            return p.StartsWith("/") ? p : "/" + p;
        }

        [HttpGet("banners")]
        public async Task<IActionResult> Banners()
        {
            var cfg = LoadHomeConfig();
            var hidden = new HashSet<string>(cfg.HiddenBannerMovieIds ?? new List<string>());

            var released = await _db.Movies.AsNoTracking()
                .Where(m => m.StatusId == "RELEASED")
                .OrderByDescending(m => m.UpdatedAt ?? m.ReleaseDate)
                .Select(m => new
                {
                    movieId = m.MoviesId,
                    title = m.Title ?? "",
                    bannerUrl = NormalizePath(!string.IsNullOrWhiteSpace(m.BannerImage) ? m.BannerImage! : (m.PosterImage ?? "")),
                })
                .ToListAsync();

            var movieBanners = released
                .Where(x => !hidden.Contains(x.movieId))
                .Where(x => !string.IsNullOrWhiteSpace(x.bannerUrl))
                .Take(6)
                .Select(x => new
                {
                    type = "movie",
                    movieId = x.movieId,
                    title = x.title,
                    imageUrl = x.bannerUrl
                })
                .ToList();

            var extra = (cfg.ExtraBanners ?? new List<HomeCfg.ExtraBannerItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.ImagePath))
                .Select(x => new
                {
                    type = "extra",
                    movieId = "",
                    title = "",
                    imageUrl = NormalizePath(x.ImagePath!)
                })
                .ToList();

            return Ok(movieBanners.Concat(extra).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Home([FromQuery] string? tab = "all")
        {
            tab = (tab ?? "all").Trim().ToLowerInvariant();

            var q = _db.Movies.AsNoTracking();

            if (tab == "now")
                q = q.Where(m => m.StatusId == "RELEASED");
            else if (tab == "upcoming")
                q = q.Where(m => m.StatusId == "COMING");

            var list = await q
                .OrderByDescending(m => m.UpdatedAt ?? m.ReleaseDate)
                .Select(m => new
                {
                    movieId = m.MoviesId,
                    title = m.Title ?? "",
                    posterUrl = NormalizePath(m.PosterImage ?? ""),
                    trailerUrl = m.TrailerLink ?? "",
                    durationMin = m.Duration ?? 0,
                    releaseDate = m.ReleaseDate.HasValue ? m.ReleaseDate.Value.ToString("yyyy-MM-dd") : "",
                    status = (m.StatusId == "RELEASED") ? 1 : 0
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("movie/{id}")]
        public async Task<IActionResult> MovieDetail(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var movie = await _db.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == id);

            if (movie == null) return NotFound();

            var genres = await (from mg in _db.MoviesGenres
                                join g in _db.Genres on mg.GenresId equals g.GenresId
                                where mg.MoviesId == id
                                select g.Name ?? "")
                               .Where(x => x != "")
                               .Distinct()
                               .ToListAsync();

            var genreText = genres.Any() ? string.Join(", ", genres) : "";

            var roles = await _db.MovieRoles.AsNoTracking().ToListAsync();

            string? dirRoleId = roles
                .FirstOrDefault(r => ((r.Name ?? "").ToLower().Contains("đạo")) || ((r.Name ?? "").ToLower().Contains("director")))
                ?.MovieRoleId;

            string? actRoleId = roles
                .FirstOrDefault(r => ((r.Name ?? "").ToLower().Contains("diễn")) || ((r.Name ?? "").ToLower().Contains("actor")))
                ?.MovieRoleId;

            var directorNames = new List<string>();
            var actorNames = new List<string>();

            if (!string.IsNullOrWhiteSpace(dirRoleId))
            {
                var directorIds = await _db.MoviesParticipants.AsNoTracking()
                    .Where(mp => mp.MoviesId == id && mp.MovieRoleId == dirRoleId)
                    .Select(mp => mp.ParticipantsId)
                    .ToListAsync();

                if (directorIds.Any())
                {
                    directorNames = await _db.Participants.AsNoTracking()
                        .Where(p => directorIds.Contains(p.ParticipantsId))
                        .OrderBy(p => p.NickName ?? p.BirthName)
                        .Select(p => (p.NickName ?? p.BirthName ?? p.ParticipantsId) ?? "")
                        .Where(x => x != "")
                        .ToListAsync();
                }
            }

            if (!string.IsNullOrWhiteSpace(actRoleId))
            {
                var actorIds = await _db.MoviesParticipants.AsNoTracking()
                    .Where(mp => mp.MoviesId == id && mp.MovieRoleId == actRoleId)
                    .Select(mp => mp.ParticipantsId)
                    .ToListAsync();

                if (actorIds.Any())
                {
                    actorNames = await _db.Participants.AsNoTracking()
                        .Where(p => actorIds.Contains(p.ParticipantsId))
                        .OrderBy(p => p.NickName ?? p.BirthName)
                        .Select(p => (p.NickName ?? p.BirthName ?? p.ParticipantsId) ?? "")
                        .Where(x => x != "")
                        .ToListAsync();
                }
            }

            var directorText = directorNames.Any() ? string.Join(", ", directorNames) : "";
            var castText = actorNames.Any()
                ? (actorNames.Count > 6 ? string.Join(", ", actorNames.Take(6)) + ", ..." : string.Join(", ", actorNames))
                : "";

            var bannerUrl = NormalizePath(!string.IsNullOrWhiteSpace(movie.BannerImage) ? movie.BannerImage! : (movie.PosterImage ?? ""));

            // ✅ ĐÚNG THEO WEB: DetailDescription (KHÔNG fallback summary)
            // Nếu DB record có nội dung => trả ra ngay.
            // Nếu null/empty => trả "".
            var detailDescription = (movie.DetailDescription ?? "").Trim();

            var result = new
            {
                movieId = movie.MoviesId,
                title = movie.Title ?? "",
                posterUrl = NormalizePath(movie.PosterImage ?? ""),
                bannerUrl = bannerUrl,
                trailerUrl = movie.TrailerLink ?? "",
                durationMin = movie.Duration ?? 0,
                releaseDate = movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.ToString("yyyy-MM-dd") : "",
                status = (movie.StatusId == "RELEASED") ? 1 : 0,

                detailDescription = detailDescription,

                ageRating = "",
                genres = genreText,
                director = directorText,
                cast = castText,
                languages = ""
            };

            return Ok(result);
        }
    }
}
