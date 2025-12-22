// Controllers/App/MobileHomeController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers.App
{
    [ApiController]
    [Route("api/mobile/home")]
    public class MobileHomeController : ControllerBase
    {
        private readonly CinemaContext _db;

        public MobileHomeController(CinemaContext db)
        {
            _db = db;
        }

        // GET: /api/mobile/home?tab=now|upcoming|all
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
                    bannerUrl = NormalizePath((m.BannerImage ?? m.PosterImage) ?? ""),
                    summary = m.Summary ?? "",
                    trailerUrl = m.TrailerLink ?? "",
                    durationMin = m.Duration ?? 0,
                    releaseDate = m.ReleaseDate.HasValue ? m.ReleaseDate.Value.ToString("yyyy-MM-dd") : "",
                    status = (m.StatusId == "RELEASED") ? 1 : 0,

                    // thêm dữ liệu để Home/Detail mobile hiển thị giống ảnh
                    age = (m.Age.HasValue ? m.Age.Value : 0),
                    ageRating = (m.Age.HasValue ? ("T" + m.Age.Value) : ""),
                    country = m.Country ?? "",
                    languages = m.AudioOption ?? "",

                    genre = (from mg in _db.MoviesGenres
                             join g in _db.Genres on mg.GenresId equals g.GenresId
                             where mg.MoviesId == m.MoviesId
                             select (g.Name ?? "")).FirstOrDefault() ?? ""
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/mobile/home/movie/{id}
        [HttpGet("movie/{id}")]
        public async Task<IActionResult> MovieDetail(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var movie = await _db.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == id);

            if (movie == null) return NotFound();

            // Genres
            var genres = await (from mg in _db.MoviesGenres
                                join g in _db.Genres on mg.GenresId equals g.GenresId
                                where mg.MoviesId == id
                                select g.Name ?? "")
                               .Where(x => x != "")
                               .Distinct()
                               .ToListAsync();

            var genreText = genres.Any() ? string.Join(", ", genres) : "";

            // RoleId: Director / Actor
            var roles = await _db.MovieRoles.AsNoTracking().ToListAsync();

            string? dirRoleId = roles
                .FirstOrDefault(r =>
                    ((r.Name ?? "").ToLower().Contains("đạo")) ||
                    ((r.Name ?? "").ToLower().Contains("director")))
                ?.MovieRoleId;

            string? actRoleId = roles
                .FirstOrDefault(r =>
                    ((r.Name ?? "").ToLower().Contains("diễn")) ||
                    ((r.Name ?? "").ToLower().Contains("actor")))
                ?.MovieRoleId;

            var directorNames = new System.Collections.Generic.List<string>();
            var actorNames = new System.Collections.Generic.List<string>();

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
                ? (actorNames.Count > 8 ? string.Join(", ", actorNames.Take(8)) + ", ..." : string.Join(", ", actorNames))
                : "";

            var ageRating = movie.Age.HasValue ? ("T" + movie.Age.Value) : "";
            var languages = movie.AudioOption ?? "";

            var result = new
            {
                movieId = movie.MoviesId,
                title = movie.Title ?? "",
                posterUrl = NormalizePath(movie.PosterImage ?? ""),
                bannerUrl = NormalizePath((movie.BannerImage ?? movie.PosterImage) ?? ""),
                summary = movie.Summary ?? "",
                detail = movie.DetailDescription ?? "",
                trailerUrl = movie.TrailerLink ?? "",
                durationMin = movie.Duration ?? 0,
                releaseDate = movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.ToString("yyyy-MM-dd") : "",
                status = (movie.StatusId == "RELEASED") ? 1 : 0,

                age = (movie.Age.HasValue ? movie.Age.Value : 0),
                ageRating,
                genres = genreText,
                director = directorText,
                cast = castText,
                languages,
                country = movie.Country ?? ""
            };

            return Ok(result);
        }

        private static string NormalizePath(string p)
        {
            p = (p ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) return "";
            if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return p;
            return p.StartsWith("/") ? p : "/" + p;
        }
    }
}
