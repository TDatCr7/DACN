using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Reports")]
    public class ReportsController : Controller
    {
        private readonly CinemaContext _db;
        private readonly TimeZoneInfo _vnTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public ReportsController(CinemaContext db)
        {
            _db = db;
        }

        // ===================== DASHBOARD: THỐNG KÊ + BÁO CÁO =====================
        // URL: /Reports
        [HttpGet("")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string groupBy = "week", int topN = 10)
        {
            var range = NormalizeRange(fromDate, toDate);

            var vm = new StatisticsDashboardVM
            {
                FromDate = range.FromLocal,
                ToDate = range.ToLocal,
                GroupBy = groupBy,
                TopN = Math.Max(1, topN),
                Kpi = new KpiVM(),
                RevenueByPeriod = new List<RevenueBucketVM>(),
                TopMoviesByTickets = new List<TopMovieVM>(),
                MovieRevenue = new List<MovieRevenueVM>()
            };

            // Paid invoices trong khoảng
            var invoices = await _db.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue
                            && i.CreatedAt.Value >= range.FromUtc
                            && i.CreatedAt.Value < range.ToUtcExclusive
                            && i.Status == (byte)1)
                .Select(i => new InvoiceRow
                {
                    InvoiceId = i.InvoiceId,
                    CreatedAtUtc = i.CreatedAt!.Value,
                    TotalPaid = i.TotalPrice ?? 0m,
                    OriginalTotal = i.OriginalTotal ?? 0m
                })
                .ToListAsync();

            if (invoices.Count == 0)
                return View("~/Views/Reports/Index.cshtml", vm);

            var invoiceIds = invoices.Select(x => x.InvoiceId).Distinct().ToList();

            // gross ticket per invoice + count ticket
            var ticketAggByInv = await _db.Tickets.AsNoTracking()
                .Where(t => invoiceIds.Contains(t.InvoiceId) && t.Status == (byte)2)
                .GroupBy(t => t.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    Gross = g.Sum(x => (decimal?)(x.Price ?? 0m)) ?? 0m,
                    Tickets = g.Count()
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => new TicketAgg { Gross = x.Gross, Tickets = x.Tickets });

            // gross snack per invoice
            var snackGrossByInv = await _db.DetailBookingSnacks.AsNoTracking()
                .Where(d => invoiceIds.Contains(d.InvoiceId))
                .GroupBy(d => d.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    Gross = g.Sum(x => (decimal?)(x.TotalPrice ?? 0m)) ?? 0m
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.Gross);

            var invMap = invoices.ToDictionary(x => x.InvoiceId, x => x);

            decimal TicketNetOfInvoice(string invId)
            {
                var inv = invMap[invId];
                var ticketGross = ticketAggByInv.TryGetValue(invId, out var t) ? t.Gross : 0m;
                var snackGross = snackGrossByInv.TryGetValue(invId, out var s) ? s : 0m;

                var original = inv.OriginalTotal > 0m ? inv.OriginalTotal : (ticketGross + snackGross);
                var paid = inv.TotalPaid > 0m ? inv.TotalPaid : original;

                if (original <= 0m || ticketGross <= 0m) return 0m;
                return paid * (ticketGross / original);
            }

            decimal SnackNetOfInvoice(string invId)
            {
                var inv = invMap[invId];
                var ticketGross = ticketAggByInv.TryGetValue(invId, out var t) ? t.Gross : 0m;
                var snackGross = snackGrossByInv.TryGetValue(invId, out var s) ? s : 0m;

                var original = inv.OriginalTotal > 0m ? inv.OriginalTotal : (ticketGross + snackGross);
                var paid = inv.TotalPaid > 0m ? inv.TotalPaid : original;

                if (original <= 0m || snackGross <= 0m) return 0m;
                return paid * (snackGross / original);
            }

            // Revenue by period
            var buckets = new SortedDictionary<DateTime, RevenueBucketVM>();
            foreach (var inv in invoices)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(inv.CreatedAtUtc, _vnTz);
                var periodStart = GetPeriodStart(local, groupBy);

                if (!buckets.TryGetValue(periodStart, out var b))
                {
                    b = new RevenueBucketVM
                    {
                        PeriodStartLocal = periodStart,
                        Label = MakeLabel(periodStart, groupBy)
                    };
                    buckets[periodStart] = b;
                }

                b.TotalPaid += inv.TotalPaid;
                b.TicketRevenue += TicketNetOfInvoice(inv.InvoiceId);
                b.SnackRevenue += SnackNetOfInvoice(inv.InvoiceId);
            }
            vm.RevenueByPeriod = buckets.Values.ToList();

            // Top movies by tickets (rank)
            var ticketInvMovie = await (from t in _db.Tickets.AsNoTracking()
                                        where invoiceIds.Contains(t.InvoiceId) && t.Status == (byte)2
                                        join st in _db.ShowTimes.AsNoTracking() on t.ShowTimeId equals st.ShowTimeId
                                        join m in _db.Movies.AsNoTracking() on st.MoviesId equals m.MoviesId
                                        group t by new { t.InvoiceId, m.MoviesId, m.Title } into g
                                        select new
                                        {
                                            g.Key.InvoiceId,
                                            MovieId = g.Key.MoviesId,
                                            Title = g.Key.Title ?? g.Key.MoviesId,
                                            TicketsSold = g.Count(),
                                            TicketGross = g.Sum(x => (decimal?)(x.Price ?? 0m)) ?? 0m
                                        })
                .ToListAsync();

            var byMovie = ticketInvMovie
                .GroupBy(x => new { x.MovieId, x.Title })
                .Select(g => new
                {
                    g.Key.MovieId,
                    g.Key.Title,
                    TicketsSold = g.Sum(x => x.TicketsSold),
                    TicketRevenue = g.Sum(x => TicketNetOfInvoice(x.InvoiceId))
                })
                .OrderByDescending(x => x.TicketsSold)
                .ThenByDescending(x => x.TicketRevenue)
                .Take(vm.TopN)
                .ToList();

            int rank = 1;
            vm.TopMoviesByTickets = byMovie.Select(x => new TopMovieVM
            {
                Rank = rank++,
                MovieId = x.MovieId,
                Title = x.Title,
                TicketsSold = x.TicketsSold,
                TicketRevenue = decimal.Round(x.TicketRevenue, 0, MidpointRounding.AwayFromZero)
            }).ToList();

            vm.MovieRevenue = vm.TopMoviesByTickets
                .Select(x => new MovieRevenueVM { MovieId = x.MovieId, Title = x.Title, TicketRevenue = x.TicketRevenue })
                .ToList();

            // KPI
            var totalTicketRevenue = vm.RevenueByPeriod.Sum(x => x.TicketRevenue);
            var totalSnackRevenue = vm.RevenueByPeriod.Sum(x => x.SnackRevenue);
            var totalPaid = vm.RevenueByPeriod.Sum(x => x.TotalPaid);

            vm.Kpi = new KpiVM
            {
                TotalPaid = decimal.Round(totalPaid, 0, MidpointRounding.AwayFromZero),
                TicketRevenue = decimal.Round(totalTicketRevenue, 0, MidpointRounding.AwayFromZero),
                SnackRevenue = decimal.Round(totalSnackRevenue, 0, MidpointRounding.AwayFromZero),
                TotalInvoices = invoices.Count,
                TotalTickets = ticketAggByInv.Values.Sum(x => x.Tickets)
            };

            return View("~/Views/Reports/Index.cshtml", vm);
        }

        // ===================== EXPORT: DOANH THU (ALL/MOVIE/SNACK) =====================
        // URL: /Reports/ExportRevenueExcel
        [HttpGet("ExportRevenueExcel")]
        public async Task<IActionResult> ExportRevenueExcel(
            DateTime? fromDate,
            DateTime? toDate,
            string groupBy = "day",
            string mode = "all",
            string? movieId = null,
            string? snackId = null)
        {
            var range = NormalizeRange(fromDate, toDate);

            // 1) Paid invoices trong khoảng
            var invoices = await _db.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue
                            && i.CreatedAt.Value >= range.FromUtc
                            && i.CreatedAt.Value < range.ToUtcExclusive
                            && i.Status == (byte)1)
                .Select(i => new InvoiceRow
                {
                    InvoiceId = i.InvoiceId,
                    CreatedAtUtc = i.CreatedAt!.Value,
                    TotalPaid = i.TotalPrice ?? 0m,
                    OriginalTotal = i.OriginalTotal ?? 0m
                })
                .ToListAsync();

            if (invoices.Count == 0)
                return BuildEmptyExcel("Revenue", "Không có dữ liệu trong khoảng lọc.");

            var invoiceIds = invoices.Select(x => x.InvoiceId).Distinct().ToList();

            // 2) Lọc invoiceIds theo movie/snack (nếu cần)
            if (mode == "movie" && !string.IsNullOrWhiteSpace(movieId))
            {
                var movieInvoiceIds = await (from t in _db.Tickets.AsNoTracking()
                                             where invoiceIds.Contains(t.InvoiceId) && t.Status == (byte)2
                                             join st in _db.ShowTimes.AsNoTracking() on t.ShowTimeId equals st.ShowTimeId
                                             where st.MoviesId == movieId
                                             select t.InvoiceId)
                    .Distinct()
                    .ToListAsync();

                invoiceIds = movieInvoiceIds;
                invoices = invoices.Where(i => invoiceIds.Contains(i.InvoiceId)).ToList();
            }

            if (mode == "snack")
            {
                var snackInvoiceIdsQ = _db.DetailBookingSnacks.AsNoTracking()
                    .Where(d => invoiceIds.Contains(d.InvoiceId));

                if (!string.IsNullOrWhiteSpace(snackId))
                    snackInvoiceIdsQ = snackInvoiceIdsQ.Where(d => d.SnackId == snackId);

                var snackInvoiceIds = await snackInvoiceIdsQ
                    .Select(d => d.InvoiceId)
                    .Distinct()
                    .ToListAsync();

                invoiceIds = snackInvoiceIds;
                invoices = invoices.Where(i => invoiceIds.Contains(i.InvoiceId)).ToList();
            }

            if (invoices.Count == 0)
                return BuildEmptyExcel("Revenue", "Không có dữ liệu theo điều kiện lọc (movie/snack).");

            // 3) Gross theo invoice
            var ticketGrossByInv = new Dictionary<string, decimal>();
            if (mode != "snack")
            {
                ticketGrossByInv = await _db.Tickets.AsNoTracking()
                    .Where(t => invoiceIds.Contains(t.InvoiceId) && t.Status == (byte)2)
                    .GroupBy(t => t.InvoiceId)
                    .Select(g => new
                    {
                        InvoiceId = g.Key,
                        Gross = g.Sum(x => (decimal?)(x.Price ?? 0m)) ?? 0m
                    })
                    .ToDictionaryAsync(x => x.InvoiceId, x => x.Gross);
            }

            var snackGrossByInv = await _db.DetailBookingSnacks.AsNoTracking()
                .Where(d => invoiceIds.Contains(d.InvoiceId))
                .GroupBy(d => d.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    Gross = g.Sum(x => (decimal?)(x.TotalPrice ?? 0m)) ?? 0m
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.Gross);

            // 4) Nếu mode=snack + snackId => cần gross theo snack line để phân bổ net cho đúng snack
            Dictionary<(string invoiceId, string snackId), decimal> snackLineGross = new();

            if (mode == "snack")
            {
                var snackLinesQ = from d in _db.DetailBookingSnacks.AsNoTracking()
                                  where invoiceIds.Contains(d.InvoiceId)
                                  select new
                                  {
                                      d.InvoiceId,
                                      d.SnackId,
                                      Gross = d.TotalPrice ?? 0m
                                  };

                if (!string.IsNullOrWhiteSpace(snackId))
                    snackLinesQ = snackLinesQ.Where(x => x.SnackId == snackId);

                var snackLines = await snackLinesQ.ToListAsync();

                foreach (var x in snackLines)
                {
                    var key = (x.InvoiceId, x.SnackId);
                    if (!snackLineGross.ContainsKey(key)) snackLineGross[key] = 0m;
                    snackLineGross[key] += x.Gross;
                }
            }

            // 5) Tính net ticket/snack theo invoice (phân bổ theo TotalPaid / Original)
            var invMap = invoices.ToDictionary(x => x.InvoiceId, x => x);

            decimal TicketNetOfInvoice(string invId)
            {
                var inv = invMap[invId];
                var ticketGross = ticketGrossByInv.TryGetValue(invId, out var tg) ? tg : 0m;
                var snackGross = snackGrossByInv.TryGetValue(invId, out var sg) ? sg : 0m;

                var original = inv.OriginalTotal > 0m ? inv.OriginalTotal : (ticketGross + snackGross);
                var paid = inv.TotalPaid > 0m ? inv.TotalPaid : original;

                if (original <= 0m) return 0m;
                if (ticketGross <= 0m) return 0m;

                return paid * (ticketGross / original);
            }

            decimal SnackNetOfInvoice(string invId)
            {
                var inv = invMap[invId];
                var ticketGross = ticketGrossByInv.TryGetValue(invId, out var tg) ? tg : 0m;
                var snackGross = snackGrossByInv.TryGetValue(invId, out var sg) ? sg : 0m;

                var original = inv.OriginalTotal > 0m ? inv.OriginalTotal : (ticketGross + snackGross);
                var paid = inv.TotalPaid > 0m ? inv.TotalPaid : original;

                if (original <= 0m) return 0m;
                if (snackGross <= 0m) return 0m;

                return paid * (snackGross / original);
            }

            // 6) Gom nhóm theo kỳ
            var buckets = new SortedDictionary<DateTime, BucketRow>();

            foreach (var inv in invoices)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(inv.CreatedAtUtc, _vnTz);
                var periodStart = GetPeriodStart(local, groupBy);
                if (!buckets.TryGetValue(periodStart, out var b))
                {
                    b = new BucketRow { PeriodStartLocal = periodStart, Label = MakeLabel(periodStart, groupBy) };
                    buckets[periodStart] = b;
                }

                b.NetPaid += inv.TotalPaid;
                b.OriginalTotal += (inv.OriginalTotal > 0m ? inv.OriginalTotal : 0m);

                if (mode == "movie")
                {
                    b.TicketRevenue += TicketNetOfInvoice(inv.InvoiceId);
                }
                else if (mode == "snack")
                {
                    if (string.IsNullOrWhiteSpace(snackId))
                    {
                        b.SnackRevenue += SnackNetOfInvoice(inv.InvoiceId);
                    }
                    else
                    {
                        var snackGrossInv = snackGrossByInv.TryGetValue(inv.InvoiceId, out var sg) ? sg : 0m;
                        if (snackGrossInv <= 0m) continue;

                        var snackNetInv = SnackNetOfInvoice(inv.InvoiceId);

                        var key = (inv.InvoiceId, snackId);
                        if (!snackLineGross.TryGetValue(key, out var lineGross) || lineGross <= 0m) continue;

                        var lineNet = snackNetInv * (lineGross / snackGrossInv);
                        b.SnackRevenue += lineNet;
                    }
                }
                else // all
                {
                    b.TicketRevenue += TicketNetOfInvoice(inv.InvoiceId);
                    b.SnackRevenue += SnackNetOfInvoice(inv.InvoiceId);
                }
            }

            // 7) Excel
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Revenue");

            ws.Cell(1, 1).Value = "Kỳ";
            ws.Cell(1, 2).Value = "Doanh thu vé";
            ws.Cell(1, 3).Value = "Doanh thu snack";
            ws.Cell(1, 4).Value = "Tổng (vé+snack)";
            ws.Cell(1, 5).Value = "Tổng thanh toán (TotalPrice)";
            ws.Cell(1, 6).Value = "Tổng trước giảm (OriginalTotal)";
            ws.Row(1).Style.Font.Bold = true;

            int r = 2;
            foreach (var kv in buckets)
            {
                var b = kv.Value;
                ws.Cell(r, 1).Value = b.Label;
                ws.Cell(r, 2).Value = b.TicketRevenue;
                ws.Cell(r, 3).Value = b.SnackRevenue;
                ws.Cell(r, 4).FormulaA1 = $"=B{r}+C{r}";
                ws.Cell(r, 5).Value = b.NetPaid;
                ws.Cell(r, 6).Value = b.OriginalTotal;
                r++;
            }

            ws.Column(2).Style.NumberFormat.Format = "#,##0";
            ws.Column(3).Style.NumberFormat.Format = "#,##0";
            ws.Column(4).Style.NumberFormat.Format = "#,##0";
            ws.Column(5).Style.NumberFormat.Format = "#,##0";
            ws.Column(6).Style.NumberFormat.Format = "#,##0";

            ws.Columns().AdjustToContents();

            var fileName = $"revenue_{mode}_{range.FromLocal:yyyyMMdd}_{range.ToLocal:yyyyMMdd}_{groupBy}.xlsx";
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ===================== EXPORT: TOP MOVIES (RANK 1..N) =====================
        // URL: /Reports/ExportTopMoviesExcel
        [HttpGet("ExportTopMoviesExcel")]
        public async Task<IActionResult> ExportTopMoviesExcel(
            DateTime? fromDate,
            DateTime? toDate,
            int topN = 10,
            bool onlyReleasedMovies = true)
        {
            var range = NormalizeRange(fromDate, toDate);

            var invoices = await _db.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue
                            && i.CreatedAt.Value >= range.FromUtc
                            && i.CreatedAt.Value < range.ToUtcExclusive
                            && i.Status == (byte)1)
                .Select(i => new InvoiceRow
                {
                    InvoiceId = i.InvoiceId,
                    CreatedAtUtc = i.CreatedAt!.Value,
                    TotalPaid = i.TotalPrice ?? 0m,
                    OriginalTotal = i.OriginalTotal ?? 0m
                })
                .ToListAsync();

            if (invoices.Count == 0)
                return BuildEmptyExcel("TopMovies", "Không có dữ liệu trong khoảng lọc.");

            var invoiceIds = invoices.Select(x => x.InvoiceId).Distinct().ToList();

            var ticketInvMovie = await (from t in _db.Tickets.AsNoTracking()
                                        where invoiceIds.Contains(t.InvoiceId) && t.Status == (byte)2
                                        join st in _db.ShowTimes.AsNoTracking() on t.ShowTimeId equals st.ShowTimeId
                                        join m in _db.Movies.AsNoTracking() on st.MoviesId equals m.MoviesId
                                        where !onlyReleasedMovies || m.StatusId == "RELEASED"
                                        group t by new { t.InvoiceId, m.MoviesId, m.Title, m.PosterImage } into g
                                        select new
                                        {
                                            g.Key.InvoiceId,
                                            MovieId = g.Key.MoviesId,
                                            Title = g.Key.Title ?? "",
                                            Poster = g.Key.PosterImage,
                                            TicketsSold = g.Count(),
                                            TicketGross = g.Sum(x => (decimal?)(x.Price ?? 0m)) ?? 0m
                                        })
                .ToListAsync();

            if (ticketInvMovie.Count == 0)
                return BuildEmptyExcel("TopMovies", "Không có vé theo điều kiện lọc (phim đang chiếu / khoảng ngày).");

            var snackGrossByInv = await _db.DetailBookingSnacks.AsNoTracking()
                .Where(d => invoiceIds.Contains(d.InvoiceId))
                .GroupBy(d => d.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    Gross = g.Sum(x => (decimal?)(x.TotalPrice ?? 0m)) ?? 0m
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.Gross);

            var invMap = invoices.ToDictionary(x => x.InvoiceId, x => x);

            decimal TicketNetOfInvoice(string invId, decimal ticketGross)
            {
                var inv = invMap[invId];
                var snackGross = snackGrossByInv.TryGetValue(invId, out var sg) ? sg : 0m;

                var original = inv.OriginalTotal > 0m ? inv.OriginalTotal : (ticketGross + snackGross);
                var paid = inv.TotalPaid > 0m ? inv.TotalPaid : original;

                if (original <= 0m) return 0m;
                if (ticketGross <= 0m) return 0m;

                return paid * (ticketGross / original);
            }

            var query = ticketInvMovie
                .GroupBy(x => new { x.MovieId, x.Title, x.Poster })
                .Select(g => new
                {
                    g.Key.MovieId,
                    g.Key.Title,
                    g.Key.Poster,
                    TicketsSold = g.Sum(x => x.TicketsSold),
                    TicketRevenue = g.Sum(x => TicketNetOfInvoice(x.InvoiceId, x.TicketGross))
                })
                .OrderByDescending(x => x.TicketsSold)
                .ThenByDescending(x => x.TicketRevenue);

            var rows = (topN > 0 ? query.Take(topN) : query).ToList();


            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("TopMovies");

            ws.Cell(1, 1).Value = "Hạng";
            ws.Cell(1, 2).Value = "MovieId";
            ws.Cell(1, 3).Value = "Tên phim";
            ws.Cell(1, 4).Value = "Số vé";
            ws.Cell(1, 5).Value = "Doanh thu vé (không snack)";
            ws.Row(1).Style.Font.Bold = true;

            int r = 2;
            int rank = 1;
            foreach (var x in rows)
            {
                ws.Cell(r, 1).Value = rank++;
                ws.Cell(r, 2).Value = x.MovieId;
                ws.Cell(r, 3).Value = x.Title;
                ws.Cell(r, 4).Value = x.TicketsSold;
                ws.Cell(r, 5).Value = x.TicketRevenue;
                r++;
            }

            ws.Column(5).Style.NumberFormat.Format = "#,##0";
            ws.Columns().AdjustToContents();

            var fileName = $"top_movies_{range.FromLocal:yyyyMMdd}_{range.ToLocal:yyyyMMdd}.xlsx";
            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ===================== Helpers =====================
        private IActionResult BuildEmptyExcel(string sheetName, string message)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);
            ws.Cell(1, 1).Value = message;
            ws.Cell(1, 1).Style.Font.Bold = true;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "empty_report.xlsx");
        }

        private Range NormalizeRange(DateTime? fromDate, DateTime? toDate)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vnTz);

            var fromLocal = (fromDate?.Date) ?? new DateTime(nowLocal.Year, nowLocal.Month, 1);
            var toLocal = (toDate?.Date) ?? nowLocal.Date;

            if (toLocal < fromLocal)
            {
                var tmp = fromLocal;
                fromLocal = toLocal;
                toLocal = tmp;
            }

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, _vnTz);
            var toUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(toLocal.AddDays(1), _vnTz);

            return new Range
            {
                FromLocal = fromLocal,
                ToLocal = toLocal,
                FromUtc = fromUtc,
                ToUtcExclusive = toUtcExclusive
            };
        }

        private static DateTime GetPeriodStart(DateTime local, string groupBy)
        {
            groupBy = (groupBy ?? "day").Trim().ToLowerInvariant();
            if (groupBy == "year")
                return new DateTime(local.Year, 1, 1);
            if (groupBy == "month")
                return new DateTime(local.Year, local.Month, 1);
            if (groupBy == "week")
            {
                int diff = (7 + (local.DayOfWeek - DayOfWeek.Monday)) % 7;
                return local.Date.AddDays(-diff);
            }
            return local.Date;
        }

        private static string MakeLabel(DateTime periodStartLocal, string groupBy)
        {
            groupBy = (groupBy ?? "day").Trim().ToLowerInvariant();
            if (groupBy == "year") return periodStartLocal.ToString("yyyy");
            if (groupBy == "month") return periodStartLocal.ToString("MM/yyyy");
            if (groupBy == "week") return $"Tuần từ {periodStartLocal:dd/MM/yyyy}";
            return periodStartLocal.ToString("dd/MM/yyyy");
        }

        private sealed class TicketAgg
        {
            public decimal Gross { get; set; }
            public int Tickets { get; set; }
        }

        private sealed class InvoiceRow
        {
            public string InvoiceId { get; set; } = "";
            public DateTime CreatedAtUtc { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal OriginalTotal { get; set; }
        }

        private sealed class BucketRow
        {
            public DateTime PeriodStartLocal { get; set; }
            public string Label { get; set; } = "";
            public decimal TicketRevenue { get; set; }
            public decimal SnackRevenue { get; set; }
            public decimal NetPaid { get; set; }
            public decimal OriginalTotal { get; set; }
        }

        private sealed class Range
        {
            public DateTime FromLocal { get; set; }
            public DateTime ToLocal { get; set; }
            public DateTime FromUtc { get; set; }
            public DateTime ToUtcExclusive { get; set; }
        }
    }
}
