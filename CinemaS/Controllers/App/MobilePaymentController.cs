using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.Services;
using CinemaS.VNPAY;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CinemaS.Controllers
{
    [ApiController]
    [Route("api")]
    public class MobilePaymentController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly CinemaContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IQrTicketService _qrTicketService;

        public MobilePaymentController(
            IConfiguration cfg,
            CinemaContext context,
            IEmailSender emailSender,
            IQrTicketService qrTicketService)
        {
            _cfg = cfg;
            _context = context;
            _emailSender = emailSender;
            _qrTicketService = qrTicketService;
        }

        /* ===================== DTOs ===================== */

        public class SnackLineDto
        {
            [JsonPropertyName("snackId")] public string SnackId { get; set; } = "";
            [JsonPropertyName("quantity")] public int Quantity { get; set; }
        }

        public class CreateVnpayRequest
        {
            [JsonPropertyName("userId")] public JsonElement UserId { get; set; }
            [JsonPropertyName("email")] public string? Email { get; set; }
            [JsonPropertyName("showtimeId")] public string ShowtimeId { get; set; } = "";
            [JsonPropertyName("seatIds")] public List<string> SeatIds { get; set; } = new();
            [JsonPropertyName("snacks")] public List<SnackLineDto> Snacks { get; set; } = new();
            [JsonPropertyName("movieTitle")] public string? MovieTitle { get; set; }
        }

        public class ApplyPromotionRequest
        {
            [JsonPropertyName("invoiceId")] public string InvoiceId { get; set; } = "";
            [JsonPropertyName("code")] public string Code { get; set; } = "";
            // mobile đang truyền userId trong create, nên apply/remove cũng dùng userId để check quyền
            [JsonPropertyName("userId")] public JsonElement UserId { get; set; }
        }

        public class RemovePromotionRequest
        {
            [JsonPropertyName("invoiceId")] public string InvoiceId { get; set; } = "";
            [JsonPropertyName("userId")] public JsonElement UserId { get; set; }
        }

        /* ===================== Promotion (Mobile) ===================== */

        [HttpPost("vnpay/apply-promotion")]
        public async Task<IActionResult> ApplyPromotion([FromBody] ApplyPromotionRequest req)
        {
            if (req == null) return BadRequest();
            if (string.IsNullOrWhiteSpace(req.InvoiceId) || string.IsNullOrWhiteSpace(req.Code))
                return BadRequest(new { ok = false, message = "Missing invoiceId/code" });

            var userId = ReadUserId(req.UserId);
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { ok = false, message = "Missing userId" });

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId);
            if (invoice == null) return NotFound(new { ok = false, message = "Invoice not found" });

            if (invoice.Status == (byte)1)
                return Conflict(new { ok = false, message = "Invoice already paid" });

            if (!string.Equals(invoice.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var nowVn = NowVn();
            var normalized = NormalizePromoCode(req.Code);

            var promo = await _context.Promotion.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code != null && p.Code.ToLower() == normalized);

            if (promo == null)
                return NotFound(new { ok = false, message = "Promotion not found" });

            var validation = ValidatePromotion(promo, nowVn);
            if (!validation.Success)
                return BadRequest(new { ok = false, message = validation.ErrorMessage });

            // Tổng gốc: ưu tiên invoice.OriginalTotal, nếu không có thì sum line-items, fallback invoice.TotalPrice
            var baseTotal = await GetInvoiceBaseTotalForPricingAsync(invoice.InvoiceId, invoice.TotalPrice, invoice.PromotionId);
            if (!baseTotal.ok)
                return BadRequest(new { ok = false, message = baseTotal.error });

            var (okPercent, percent) = NormalizeDiscountPercent(promo.Discount!.Value);
            if (!okPercent)
                return BadRequest(new { ok = false, message = "Invalid discount percent" });

            decimal discountAmount = Math.Round(baseTotal.total * (decimal)percent / 100m, 0, MidpointRounding.AwayFromZero);
            decimal payable = baseTotal.total - discountAmount;
            if (payable < 0) payable = 0;

            // Lưu: set PromotionId + TotalPrice = giá sau giảm
            invoice.PromotionId = promo.PromotionId;

            // Nếu model có OriginalTotal thì set (để remove promo / hiển thị đúng)
            // (Không đủ dữ liệu để xác minh field OriginalTotal có tồn tại, nhưng web controller đang dùng)
            TrySetInvoiceOriginalTotal(invoice, baseTotal.total);

            invoice.TotalPrice = payable;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                success = true,
                message = "Promotion applied",
                invoiceId = invoice.InvoiceId,
                promotionId = promo.PromotionId,
                code = promo.Code,
                discountPercent = percent,
                originalAmount = baseTotal.total,
                discountAmount = discountAmount,
                payableAmount = payable
            });
        }

        [HttpPost("vnpay/remove-promotion")]
        public async Task<IActionResult> RemovePromotion([FromBody] RemovePromotionRequest req)
        {
            if (req == null) return BadRequest();
            if (string.IsNullOrWhiteSpace(req.InvoiceId))
                return BadRequest(new { ok = false, message = "Missing invoiceId" });

            var userId = ReadUserId(req.UserId);
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { ok = false, message = "Missing userId" });

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId);
            if (invoice == null) return NotFound(new { ok = false, message = "Invoice not found" });

            if (invoice.Status == (byte)1)
                return Conflict(new { ok = false, message = "Invoice already paid" });

            if (!string.Equals(invoice.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var baseTotal = await GetInvoiceBaseTotalForPricingAsync(invoice.InvoiceId, invoice.TotalPrice, invoice.PromotionId);
            if (!baseTotal.ok)
                return BadRequest(new { ok = false, message = baseTotal.error });

            invoice.PromotionId = null;

            TrySetInvoiceOriginalTotal(invoice, baseTotal.total);

            invoice.TotalPrice = baseTotal.total;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                success = true,
                message = "Promotion removed",
                invoiceId = invoice.InvoiceId,
                originalAmount = baseTotal.total,
                discountAmount = 0,
                payableAmount = baseTotal.total
            });
        }

        /* ===================== VNPay Create ===================== */

        // POST: /api/vnpay/create
        [HttpPost("vnpay/create")]
        public async Task<IActionResult> CreateVnpay([FromBody] CreateVnpayRequest req)
        {
            if (req == null) return BadRequest();

            var userId = ReadUserId(req.UserId);
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId");
            

            // ✅ NEW: validate user exists to avoid FK crash
            var userExists = await _context.Users.AsNoTracking().AnyAsync(u => u.UserId == userId);
            if (!userExists) return BadRequest(new { ok = false, message = "Invalid userId (not found)" });

            if (string.IsNullOrWhiteSpace(req.ShowtimeId)) return BadRequest("showtimeId");
            if (req.SeatIds == null || req.SeatIds.Count == 0) return BadRequest("seatIds");

            var showtime = await _context.ShowTimes.FirstOrDefaultAsync(x => x.ShowTimeId == req.ShowtimeId);
            if (showtime == null) return NotFound("showtime");

            var seats = await _context.Seats
                .Where(s => req.SeatIds.Contains(s.SeatId))
                .ToListAsync();
            if (seats.Count != req.SeatIds.Count) return BadRequest("seatIds invalid");

            if (seats.Any(s => s.CinemaTheaterId != showtime.CinemaTheaterId))
                return BadRequest("seatIds not in this room");

            var now = DateTime.UtcNow;

            // blocked: pending (1) còn hạn OR booked(2)
            var blocked = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == req.ShowtimeId &&
                            req.SeatIds.Contains(t.SeatId) &&
                            (t.Status == (byte)2 || (t.Status == (byte)1 && t.Expire != null && t.Expire > DateTime.UtcNow)))
                .Select(t => t.SeatId)
                .ToListAsync();

            if (blocked.Count > 0)
                return Conflict(new { ok = false, message = "Seat already booked", seats = blocked });

            var seatTypeIds = seats.Select(s => s.SeatTypeId).Distinct().ToList();

            var seatTypes = await _context.SeatTypes
                .Where(x => seatTypeIds.Contains(x.SeatTypeId))
                .ToDictionaryAsync(x => x.SeatTypeId, x => x.Price);

            var snackIds = req.Snacks?.Select(x => x.SnackId).Distinct().ToList() ?? new();
            var snacksDb = await _context.Snacks
                .Where(x => snackIds.Contains(x.SnackId))
                .ToDictionaryAsync(x => x.SnackId, x => x);

            using var tx = await _context.Database.BeginTransactionAsync();

            var invoiceId = await NextInvoiceIdSafeAsync();
            var invoice = new Invoices
            {
                InvoiceId = invoiceId,
                CustomerId = userId,
                Email = req.Email,
                Status = (byte)0,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _context.Invoices.AddAsync(invoice);

            decimal ticketSum = 0m;

            // giống web: áp dụng PriceAdjustmentPercent từ showtime nếu có
            decimal priceAdjustmentPercent = 0m;
            try
            {
                priceAdjustmentPercent = showtime.PriceAdjustmentPercent ?? 0m;
            }
            catch { priceAdjustmentPercent = 0m; }

            foreach (var seat in seats)
            {
                var basePrice = seatTypes.TryGetValue(seat.SeatTypeId, out var p) ? (p ?? 0m) : 0m;

                var adjustedPrice = basePrice * (1 + priceAdjustmentPercent / 100m);
                adjustedPrice = Math.Round(adjustedPrice, 0, MidpointRounding.AwayFromZero);

                ticketSum += adjustedPrice;

                var ticketId = await NextTicketIdSafeAsync();
                var ticketTypeId = MapTicketTypeId(seat.SeatTypeId);

                var t = new Tickets
                {
                    TicketId = ticketId,
                    InvoiceId = invoiceId,
                    ShowTimeId = req.ShowtimeId,
                    SeatId = seat.SeatId,
                    TicketTypeId = ticketTypeId,
                    Price = adjustedPrice,
                    Status = (byte)1, // pending
                    CreatedBooking = now,
                    Expire = DateTime.UtcNow.AddMinutes(15)
                };
                await _context.Tickets.AddAsync(t);
            }

            decimal snackSum = 0m;
            if (req.Snacks != null && req.Snacks.Count > 0)
            {
                foreach (var s in req.Snacks.Where(x => x.Quantity > 0))
                {
                    if (!snacksDb.ContainsKey(s.SnackId)) continue;

                    var unit = snacksDb[s.SnackId].Price ?? 0m;
                    var total = unit * s.Quantity;
                    snackSum += total;

                    var dbsId = await NextDbSnackIdSafeAsync();

                    var d = new DetailBookingSnacks
                    {
                        DetailBookingSnackId = dbsId,
                        InvoiceId = invoiceId,
                        SnackId = s.SnackId,
                        TotalSnack = s.Quantity,
                        TotalPrice = total
                    };

                    await _context.DetailBookingSnacks.AddAsync(d);
                }
            }

            invoice.TotalTicket = seats.Count;

            var originalTotal = ticketSum + snackSum;

            TrySetInvoiceOriginalTotal(invoice, originalTotal);

            invoice.TotalPrice = originalTotal;
            invoice.PromotionId = null;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            var vnp = new VnPayLibrary();

            var baseUrl = _cfg["VnPay:BaseUrl"];
            var tmnCode = _cfg["VnPay:TmnCode"];
            var hashSecret = _cfg["VnPay:HashSecret"];
            var locale = _cfg["VnPay:Locale"] ?? "vn";
            var curr = _cfg["VnPay:CurrCode"] ?? "VND";
            var orderType = _cfg["VnPay:OrderType"] ?? "other";

            var returnUrl = $"{Request.Scheme}://{Request.Host}/api/vnpay/return";

            var amount = (long)((invoice.TotalPrice ?? 0m) * 100m);

            vnp.AddRequestData("vnp_Version", "2.1.0");
            vnp.AddRequestData("vnp_Command", "pay");
            vnp.AddRequestData("vnp_TmnCode", tmnCode);
            vnp.AddRequestData("vnp_Amount", amount.ToString());
            vnp.AddRequestData("vnp_CurrCode", curr);
            vnp.AddRequestData("vnp_TxnRef", invoiceId);
            vnp.AddRequestData("vnp_OrderInfo", $"Thanh toan hoa don {invoiceId}");
            vnp.AddRequestData("vnp_OrderType", orderType);
            vnp.AddRequestData("vnp_Locale", locale);
            vnp.AddRequestData("vnp_ReturnUrl", returnUrl);
            vnp.AddRequestData("vnp_IpAddr", GetIpAddress());
            vnp.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnp.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"));

            var payUrl = vnp.CreateRequestUrl(baseUrl, hashSecret);

            return Ok(new { ok = true, success = true, orderId = invoiceId, invoiceId = invoiceId, paymentUrl = payUrl });
        }

        /* ===================== VNPay Return ===================== */

        // GET: /api/vnpay/return
        [HttpGet("vnpay/return")]
        public async Task<IActionResult> VnpayReturn()
        {
            var vnp = new VnPayLibrary();

            foreach (var (k, v) in Request.Query)
            {
                if (!string.IsNullOrWhiteSpace(k) && k.StartsWith("vnp_"))
                    vnp.AddResponseData(k, v);
            }

            var secureHash = Request.Query["vnp_SecureHash"].ToString();
            var hashSecret = _cfg["VnPay:HashSecret"];

            var valid = vnp.ValidateSignature(secureHash, hashSecret);

            var invoiceId = vnp.GetResponseData("vnp_TxnRef");
            var respCode = vnp.GetResponseData("vnp_ResponseCode");
            var txnStatus = vnp.GetResponseData("vnp_TransactionStatus");
            var amountStr = vnp.GetResponseData("vnp_Amount");
            var bankCode = vnp.GetResponseData("vnp_BankCode");
            var txnNo = vnp.GetResponseData("vnp_TransactionNo");
            var payDate = vnp.GetResponseData("vnp_PayDate");

            if (string.IsNullOrWhiteSpace(invoiceId))
                return Content("<html><body>Invalid</body></html>", "text/html");

            await SavePaymentTransactionAsync(
                invoiceId: invoiceId,
                signatureValid: valid,
                respCode: respCode,
                txnStatus: txnStatus,
                amountStr: amountStr,
                bankCode: bankCode,
                txnNo: txnNo,
                payDate: payDate
            );

            var success = valid && respCode == "00" && txnStatus == "00";

            if (!success)
            {
                await MarkFailedAndReleaseSeatsAsync(invoiceId);
                return Content(BuildSimpleResultHtml("Payment Failed", "Thanh toán thất bại. Quay lại ứng dụng để xem trạng thái."), "text/html");
            }

            await FinalizePaidAsync(invoiceId);
            return Content(BuildSimpleResultHtml("Payment Success", "Thanh toán thành công. Quay lại ứng dụng để xem vé."), "text/html");
        }

        private static string BuildSimpleResultHtml(string title, string message)
        {
            string escTitle = System.Net.WebUtility.HtmlEncode(title);
            string escMsg = System.Net.WebUtility.HtmlEncode(message);

            return $@"
<!doctype html>
<html lang='vi'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1'>
  <title>{escTitle}</title>
</head>
<body style='font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial; padding:24px;'>
  <div style='max-width:520px;margin:0 auto;border:1px solid rgba(0,0,0,.08);border-radius:16px;padding:18px;'>
    <div style='font-size:18px;font-weight:800;margin-bottom:6px;'>{escTitle}</div>
    <div style='opacity:.85;'>{escMsg}</div>
  </div>
</body>
</html>";
        }


        private static string BuildAutoRedirectHtml(string title, string message, string deepLink, string webFallback)
        {
            // deepLink: cinemas://ticket-success?invoiceId=...
            // webFallback: https://your-domain.com/ticket-success?invoiceId=...
            // Nếu deepLink/webFallback rỗng thì chỉ hiển thị thông báo.
            string escTitle = System.Net.WebUtility.HtmlEncode(title);
            string escMsg = System.Net.WebUtility.HtmlEncode(message);

            string deepLinkHtml = string.IsNullOrWhiteSpace(deepLink)
                ? ""
                : $"<a href=\"{deepLink}\" style=\"display:inline-block;padding:12px 16px;border-radius:10px;background:#6A1B9A;color:#fff;text-decoration:none;font-weight:700;\">Mở trong ứng dụng</a>";

            string webFallbackHtml = string.IsNullOrWhiteSpace(webFallback)
                ? ""
                : $"<a href=\"{webFallback}\" style=\"display:inline-block;margin-left:10px;padding:12px 16px;border-radius:10px;background:#111827;color:#fff;text-decoration:none;font-weight:700;\">Mở trang vé</a>";

            string js = "";
            if (!string.IsNullOrWhiteSpace(deepLink))
            {
                // thử mở app ngay; sau ~1.2s fallback sang web (nếu có)
                js = $@"
<script>
  (function() {{
    var deep = {System.Text.Json.JsonSerializer.Serialize(deepLink)};
    var web = {System.Text.Json.JsonSerializer.Serialize(webFallback ?? "")};
    try {{ window.location.href = deep; }} catch(e) {{}}
    if (web) {{
      setTimeout(function() {{ window.location.href = web; }}, 1200);
    }}
  }})();
</script>";
            }
            else if (!string.IsNullOrWhiteSpace(webFallback))
            {
                js = $@"
<script>
  (function() {{
    var web = {System.Text.Json.JsonSerializer.Serialize(webFallback)};
    setTimeout(function() {{ window.location.href = web; }}, 300);
  }})();
</script>";
            }

            return $@"
<!doctype html>
<html lang=""vi"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>{escTitle}</title>
</head>
<body style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial; background:#fff; color:#111827; padding:24px;"">
  <div style=""max-width:520px;margin:0 auto;border:1px solid rgba(0,0,0,.08);border-radius:16px;padding:18px;"">
    <div style=""font-size:18px;font-weight:800;margin-bottom:6px;"">{escTitle}</div>
    <div style=""opacity:.85;margin-bottom:14px;"">{escMsg}</div>
    <div>{deepLinkHtml}{webFallbackHtml}</div>
  </div>
  {js}
</body>
</html>";
        }


        /* ===================== Status for Flutter Polling ===================== */

        // GET: /api/vnpay/status/{invoiceId}
        [HttpGet("vnpay/status/{invoiceId}")]
        public async Task<IActionResult> GetVnpayStatus(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId)) return BadRequest();

            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null) return NotFound();

            var lastTxn = await _context.PaymentTransactions.AsNoTracking()
                .Where(pt => pt.InvoiceId == invoiceId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Select(pt => new
                {
                    pt.PaymentTransactionId,
                    pt.Status,
                    pt.Amount,
                    pt.Currency,
                    pt.ProviderTxnId,
                    pt.ProviderOrderNo,
                    pt.Description,
                    pt.FailureReason,
                    pt.CreatedAt,
                    pt.PaidAt
                })
                .FirstOrDefaultAsync();

            // Tính info giảm giá nếu có promo (tương đương web)
            var di = await GetDiscountInfoAsync(invoiceId, invoice.TotalPrice);

            return Ok(new
            {
                ok = true,
                success = true,
                invoiceId = invoice.InvoiceId,
                status = invoice.Status,                 // 0: pending, 1: paid, 2: failed
                promotionId = invoice.PromotionId,
                originalAmount = di.original,
                discountAmount = di.discount,
                payableAmount = di.payable,
                discountPercent = di.percent,
                promotionName = di.promoName,
                totalPrice = invoice.TotalPrice,
                lastTransaction = lastTxn
            });
        }

        /* ===================== Order Details ===================== */

        // GET: /api/orders/{invoiceId}/details
        [HttpGet("orders/{invoiceId}/details")]
        public async Task<IActionResult> GetOrderDetails(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId)) return BadRequest();

            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null) return NotFound();
            if (invoice.Status != (byte)1) return NotFound();

            var tickets = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .ToListAsync();

            var first = tickets.FirstOrDefault();
            if (first == null) return NotFound();

            var show = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(st => st.ShowTimeId == first.ShowTimeId);
            if (show == null) return NotFound();

            var movie = await _context.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == show.MoviesId);

            var room = await _context.CinemaTheaters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.CinemaTheaterId == show.CinemaTheaterId);

            MovieTheaters? theater = null;
            if (room != null && !string.IsNullOrWhiteSpace(room.MovieTheaterId))
            {
                theater = await _context.MovieTheaters.AsNoTracking()
                    .FirstOrDefaultAsync(mt => mt.MovieTheaterId == room.MovieTheaterId);
            }

            var seatIds = tickets.Select(x => x.SeatId).ToList();
            var seatLabels = await _context.Seats.AsNoTracking()
                .Where(s => seatIds.Contains(s.SeatId))
                .Select(s => s.Label ?? s.SeatId)
                .OrderBy(x => x)
                .ToListAsync();

            var snackLines = await (from d in _context.DetailBookingSnacks.AsNoTracking()
                                    join sn in _context.Snacks.AsNoTracking()
                                        on d.SnackId equals sn.SnackId
                                    where d.InvoiceId == invoiceId
                                    select new
                                    {
                                        snackId = sn.SnackId,
                                        name = sn.Name ?? "",
                                        quantity = d.TotalSnack ?? 0,
                                        total = (int)(d.TotalPrice ?? 0m)
                                    })
                                    .ToListAsync();

            DateTime? startAt = null;
            try
            {
                var dt = EF.Property<DateTime?>(show, "StartTime");
                if (dt.HasValue) startAt = dt.Value;
            }
            catch { }

            if (!startAt.HasValue)
            {
                try
                {
                    var ts = EF.Property<TimeSpan?>(show, "StartTime");
                    if (ts.HasValue && show.ShowDate.HasValue)
                        startAt = show.ShowDate.Value.Date.Add(ts.Value);
                }
                catch { }
            }

            var qrBase64 = await _qrTicketService.GenerateQrImageBase64Async(invoiceId, 8);
            var qrData = string.IsNullOrWhiteSpace(qrBase64) ? "" : $"data:image/png;base64,{qrBase64}";

            // discount info
            var di = await GetDiscountInfoAsync(invoiceId, invoice.TotalPrice);

            return Ok(new
            {
                invoiceId,
                movieTitle = movie?.Title ?? "",
                cinemaName = theater?.Name ?? "",
                screenName = room?.Name ?? "",
                startTime = startAt.HasValue ? startAt.Value.ToString("o") : "",
                seats = seatLabels,
                total = (int)(di.payable),
                totalAmount = (double)(di.payable),
                originalAmount = (double)di.original,
                discountAmount = (double)di.discount,
                discountPercent = di.percent,
                promotionName = di.promoName,
                snacks = snackLines,
                qrData
            });
        }

        /* ===================== Helpers ===================== */

        private string ReadUserId(JsonElement el)
        {
            try
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString() ?? "",
                    JsonValueKind.Number => el.GetInt32().ToString(),
                    _ => ""
                };
            }
            catch { return ""; }
        }

        private string GetIpAddress()
        {
            return HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }

        private string MapTicketTypeId(string seatTypeId)
        {
            if (string.IsNullOrWhiteSpace(seatTypeId)) return "TT001";
            if (seatTypeId.StartsWith("ST") && seatTypeId.Length >= 4) return "TT" + seatTypeId.Substring(2);
            return "TT001";
        }

        private async Task<string> NextInvoiceIdSafeAsync()
        {
            var last = await _context.Invoices.AsNoTracking()
                .Where(x => x.InvoiceId.StartsWith("INV"))
                .OrderByDescending(x => x.InvoiceId)
                .Select(x => x.InvoiceId)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 7)
            {
                var numPart = last.Substring(3, 4);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            var stamp = DateTime.Now.ToString("HH'h'mm'm'ddMMyyyy");
            return $"INV{next:D4}_{stamp}";
        }

        private async Task<string> NextTicketIdSafeAsync()
        {
            var last = await _context.Tickets.AsNoTracking()
                .Where(x => x.TicketId.StartsWith("T"))
                .OrderByDescending(x => x.TicketId)
                .Select(x => x.TicketId)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 2)
            {
                var numPart = last.Substring(1);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            return $"T{next:D6}";
        }

        private async Task<string> NextDbSnackIdSafeAsync()
        {
            var last = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(x => x.DetailBookingSnackId.StartsWith("DBS"))
                .OrderByDescending(x => x.DetailBookingSnackId)
                .Select(x => x.DetailBookingSnackId)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 6)
            {
                var numPart = last.Substring(3);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            return $"DBS{next:D3}";
        }

        private async Task<string> NextPaymentTransactionIdSafeAsync()
        {
            var last = await _context.PaymentTransactions.AsNoTracking()
                .Where(x => x.PaymentTransactionId.StartsWith("PT"))
                .OrderByDescending(x => x.PaymentTransactionId)
                .Select(x => x.PaymentTransactionId)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 3)
            {
                var numPart = last.Substring(2);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            return $"PT{next:D8}";
        }

        private async Task SavePaymentTransactionAsync(
    string invoiceId,
    bool signatureValid,
    string respCode,
    string txnStatus,
    string amountStr,
    string bankCode,
    string txnNo,
    string payDate)
        {
            decimal amount = 0m;
            if (!string.IsNullOrWhiteSpace(amountStr) && long.TryParse(amountStr, out var a))
                amount = a / 100m;

            // ✅ Prefer Code == "VNPAY"
            var pm = await _context.PaymentMethods.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "VNPAY");

            // ✅ Fallback: lấy 1 payment method bất kỳ để chắc chắn FK hợp lệ
            if (pm == null)
            {
                pm = await _context.PaymentMethods.AsNoTracking()
                    .OrderBy(x => x.PaymentMethodId)
                    .FirstOrDefaultAsync();
            }

            if (pm == null)
            {
                // Không có payment method nào trong DB -> không thể insert PaymentTransactions
                // Trả sớm để tránh crash
                return;
            }

            var id = await NextPaymentTransactionIdSafeAsync();
            var now = DateTime.UtcNow;

            var pt = new PaymentTransactions
            {
                PaymentTransactionId = id,
                InvoiceId = invoiceId,
                PaymentMethodId = pm.PaymentMethodId, // ✅ always existing
                Amount = amount,
                Currency = "VND",
                Status = (byte)((signatureValid && respCode == "00" && txnStatus == "00") ? 1 : 0),
                ProviderTxnId = txnNo,
                ProviderOrderNo = invoiceId, // ✅ provider order no nên là invoiceId (không phải bankCode)
                Description = $"VNPAY MOBILE - Bank: {bankCode}, Response: {respCode}, Status: {txnStatus}",
                FailureReason = signatureValid ? null : "INVALID_SIGNATURE",
                CreatedAt = now,
                UpdatedAt = now,
                PaidAt = (signatureValid && respCode == "00" && txnStatus == "00") ? now : null
            };

            await _context.PaymentTransactions.AddAsync(pt);
            await _context.SaveChangesAsync();
        }


        // Fail: set invoice.Status=2, remove tickets/snacks to release seats, keep invoice
        private async Task MarkFailedAndReleaseSeatsAsync(string invoiceId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null) return;

            if (invoice.Status == (byte)1) return;

            invoice.Status = (byte)2;
            invoice.UpdatedAt = DateTime.UtcNow;

            var tickets = await _context.Tickets.Where(t => t.InvoiceId == invoiceId).ToListAsync();
            var snacks = await _context.DetailBookingSnacks.Where(d => d.InvoiceId == invoiceId).ToListAsync();

            _context.Tickets.RemoveRange(tickets);
            _context.DetailBookingSnacks.RemoveRange(snacks);

            await _context.SaveChangesAsync();
        }

        // 1) SỬA FinalizePaidAsync: thay phần build email body cũ bằng build theo mẫu
        // SỬA FinalizePaidAsync: bỏ nhúng data:image;base64, chuyển sang đính kèm QR png
        private async Task FinalizePaidAsync(string invoiceId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null) return;
            if (invoice.Status == (byte)1) return;

            invoice.Status = (byte)1;
            invoice.UpdatedAt = DateTime.UtcNow;

            var tickets = await _context.Tickets.Where(t => t.InvoiceId == invoiceId).ToListAsync();
            foreach (var t in tickets)
            {
                t.Status = (byte)2;
                t.Expire = null;
            }

            await _context.SaveChangesAsync();

            await AwardPointsForInvoiceAsync(invoice);
            await _context.SaveChangesAsync();

            var toEmail = invoice.Email;
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                var user = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == invoice.CustomerId);
                toEmail = user?.Email;
            }
            if (string.IsNullOrWhiteSpace(toEmail)) return;

            var detail = await BuildEmailTicketDetailAsync(invoice.InvoiceId);
            if (detail == null) return;

            var di = await GetDiscountInfoAsync(invoice.InvoiceId, invoice.TotalPrice);
            detail.OriginalAmount = di.original;
            detail.DiscountAmount = di.discount;
            detail.PayableAmount = di.payable;
            detail.DiscountPercent = di.percent;
            detail.PromotionName = di.promoName;

            var subject = $"Đã đặt vé thành công - {detail.MovieTitle}";

            // ✅ QR bytes để đính kèm
            byte[]? qrBytes = null;
            try
            {
                var qrBase64 = await _qrTicketService.GenerateQrImageBase64Async(invoiceId, 8);
                if (!string.IsNullOrWhiteSpace(qrBase64))
                    qrBytes = Convert.FromBase64String(qrBase64);
            }
            catch { /* bỏ qua */ }

            // ✅ Body: KHÔNG nhúng img data-uri nữa
            var body = BuildTicketEmailBodyLikeWeb_NoInlineQr(detail);

            // ✅ Nếu có EmailSender hỗ trợ attachment -> attach QR
            if (qrBytes != null && qrBytes.Length > 0 && _emailSender is IEmailSenderWithAttachment emailWithAttachment)
            {
                var attachments = new List<(byte[] data, string name, string mimeType)>
        {
            (qrBytes, $"QR_{invoiceId}.png", "image/png")
        };

                await emailWithAttachment.SendEmailWithMultipleAttachmentsAsync(
                    toEmail,
                    subject,
                    body,
                    attachments
                );
            }
            else
            {
                // fallback: gửi thường (QR sẽ không có nếu không attach được)
                await _emailSender.SendEmailAsync(toEmail, subject, body);
            }
        }


        // THÊM hàm body: giống mẫu nhưng bỏ phần QR <img> để tránh broken
        private string BuildTicketEmailBodyLikeWeb_NoInlineQr(EmailTicketDetail d)
        {
            string showDate = d.ShowDate?.ToString("dd/MM/yyyy") ?? "";
            string startTime = d.StartTime?.ToString("HH:mm") ?? "";
            string endTime = d.EndTime?.ToString("HH:mm") ?? "";
            string createdAt = d.CreatedAt.HasValue
                ? DateTime.SpecifyKind(d.CreatedAt.Value, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm")
                : "";

            string seats = (d.SeatLabels != null && d.SeatLabels.Any())
                ? string.Join(" ", d.SeatLabels.Select(s => $"<span class=\"seat-item\">{System.Net.WebUtility.HtmlEncode(s)}</span>"))
                : "";

            bool hasSnacks = d.SnackItems != null && d.SnackItems.Count > 0;
            bool hasDiscount = d.DiscountAmount > 0m && d.OriginalAmount > d.PayableAmount;

            var sb = new System.Text.StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8' /><meta name='viewport' content='width=device-width, initial-scale=1.0' /><title>Hóa đơn</title><style>");
            sb.Append(@"
body { font-family: 'Georgia', 'Times New Roman', serif; margin: 0; padding: 20px; background: #fff; color: #000; }
.container { max-width: 650px; margin: 0 auto; border: 1px solid #000; padding: 40px; }
.header { border-bottom: 2px solid #000; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-content: space-between; align-items: flex-end; }
.brand { font-size: 28px; font-weight: bold; text-transform: uppercase; letter-spacing: 2px; }
.invoice-title { font-size: 16px; font-style: italic; }
.grid { display: flex; gap: 40px; margin-bottom: 30px; }
.col { flex: 1; }
.label { font-size: 10px; text-transform: uppercase; border-bottom: 1px solid #ccc; padding-bottom: 2px; margin-bottom: 5px; display: block; }
.value { font-size: 14px; font-weight: bold; }
table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
th { text-align: left; border-bottom: 1px solid #000; padding: 5px 0; font-size: 11px; text-transform: uppercase; }
td { padding: 10px 0; border-bottom: 1px dotted #ccc; font-size: 13px; }
.total-box { border-top: 2px solid #000; margin-top: 20px; padding-top: 10px; }
.total-row { display: flex; gap: 40px; align-items: baseline; justify-content: flex-end; margin-bottom: 5px; }
.subtotal-row { color: #666; font-size: 13px; }
.discount-row { color: #22c55e; font-size: 13px; }
.grand-total { font-weight: bold; font-size: 16px; }
.note { margin-top: 14px; font-size: 12px; }
.footer { text-align: center; margin-top: 50px; font-size: 11px; font-style: italic; border-top: 1px solid #eee; padding-top: 10px; }
");
            sb.Append("</style></head><body><div class='container'>");

            sb.Append("<div class='header'><div><div class='brand'>CinemaS</div>");
            sb.Append("<div style='font-size:12px; margin-top:5px;'>" + System.Net.WebUtility.HtmlEncode(d.TheaterAddress ?? "N/A") + "</div></div>");
            sb.Append("<div style='text-align:right'><div class='invoice-title'>HÓA ĐƠN</div><div>#"
                + System.Net.WebUtility.HtmlEncode(d.InvoiceId) + "</div></div></div>");

            sb.Append("<div class='grid'><div class='col'><span class='label'>KHÁCH HÀNG</span><div class='value'>"
                + System.Net.WebUtility.HtmlEncode(d.InvoiceEmail ?? "Khách lẻ") + "</div></div>");
            sb.Append("<div class='col'><span class='label'>NGÀY</span><div class='value'>"
                + System.Net.WebUtility.HtmlEncode(createdAt) + "</div></div></div>");

            if (d.TicketCount > 0)
            {
                sb.Append("<table><thead><tr><th width='40%'>MÔ TẢ</th><th width='20%'>PHÒNG</th><th width='20%'>GHẾ</th><th width='20%' style='text-align:right'>THÀNH TIỀN</th></tr></thead><tbody>");
                sb.Append("<tr><td><strong>" + System.Net.WebUtility.HtmlEncode(d.MovieTitle) + "</strong><br><small>"
                    + System.Net.WebUtility.HtmlEncode(startTime + (string.IsNullOrWhiteSpace(endTime) ? "" : " - " + endTime) + " | " + showDate)
                    + "</small></td>");
                sb.Append("<td>" + System.Net.WebUtility.HtmlEncode(d.CinemaTheater ?? "N/A") + "</td>");
                sb.Append("<td>" + seats + "</td>");
                sb.Append("<td style='text-align:right'>" + d.TicketTotal.ToString("N0") + "</td></tr></tbody></table>");
            }

            if (hasSnacks)
            {
                sb.Append("<table><thead><tr><th width='60%'>DỊCH VỤ</th><th width='20%' style='text-align:center'>SL</th><th width='20%' style='text-align:right'>THÀNH TIỀN</th></tr></thead><tbody>");
                foreach (var s in d.SnackItems)
                {
                    sb.Append("<tr><td>" + System.Net.WebUtility.HtmlEncode(s.Name) + "</td>");
                    sb.Append("<td style='text-align:center'>" + s.Quantity + "</td>");
                    sb.Append("<td style='text-align:right'>" + s.LineTotal.ToString("N0") + "</td></tr>");
                }
                sb.Append("</tbody></table>");
            }

            sb.Append("<div class='total-box'>");
            if (hasDiscount)
            {
                sb.Append("<div class='total-row subtotal-row'><span>TỔNG TIỀN (GỐC):</span><span>" + d.OriginalAmount.ToString("N0") + " VND</span></div>");
                sb.Append("<div class='total-row discount-row'><span>GIẢM GIÁ:</span><span>-" + d.DiscountAmount.ToString("N0") + " VND</span></div>");
                sb.Append("<div class='total-row'><span>TỔNG CỘNG (VND):</span><span class='grand-total'>" + d.PayableAmount.ToString("N0") + "</span></div>");
            }
            else
            {
                var grand = d.TicketTotal + d.SnackTotal;
                sb.Append("<div class='total-row'><span>TỔNG CỘNG (VND):</span><span class='grand-total'>" + grand.ToString("N0") + "</span></div>");
            }
            sb.Append("</div>");

            sb.Append("<div class='note'><strong>QR Check-in:</strong> xem trong file đính kèm (QR_"
                + System.Net.WebUtility.HtmlEncode(d.InvoiceId) + ".png)</div>");

            sb.Append("<div class='footer'>Cảm ơn quý khách đã sử dụng dịch vụ.</div></div></body></html>");
            return sb.ToString();
        }



        // 2) THÊM class detail dùng cho email (đặt trong MobilePaymentController)
        private sealed class EmailTicketDetail
        {
            public string InvoiceId { get; set; } = "";
            public string? InvoiceEmail { get; set; }
            public DateTime? CreatedAt { get; set; }

            public string MovieTitle { get; set; } = "N/A";
            public string CinemaTheater { get; set; } = "N/A";   // phòng
            public string TheaterName { get; set; } = "N/A";     // rạp
            public string TheaterAddress { get; set; } = "N/A";

            public DateTime? ShowDate { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }

            public List<string> SeatLabels { get; set; } = new();

            public int TicketCount { get; set; }
            public decimal TicketTotal { get; set; }

            public sealed class SnackLine
            {
                public string Name { get; set; } = "Snack";
                public int Quantity { get; set; }
                public decimal UnitPrice { get; set; }
                public decimal LineTotal { get; set; }
            }
            public List<SnackLine> SnackItems { get; set; } = new();
            public decimal SnackTotal { get; set; }

            // discount fields (giống mẫu)
            public decimal OriginalAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal PayableAmount { get; set; }
            public double? DiscountPercent { get; set; }
            public string? PromotionName { get; set; }
        }


        // 3) THÊM BuildEmailTicketDetailAsync: lấy dữ liệu DB giống web mẫu
        private async Task<EmailTicketDetail?> BuildEmailTicketDetailAsync(string invoiceId)
        {
            var inv = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (inv == null) return null;

            var tickets = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .ToListAsync();

            var detail = new EmailTicketDetail
            {
                InvoiceId = inv.InvoiceId,
                InvoiceEmail = inv.Email,
                CreatedAt = inv.CreatedAt
            };

            // Có vé => lấy showtime/movie/room/theater
            if (tickets.Any())
            {
                var first = tickets.First();

                var show = await _context.ShowTimes.AsNoTracking()
                    .FirstOrDefaultAsync(st => st.ShowTimeId == first.ShowTimeId);
                if (show == null) return null;

                var movie = await _context.Movies.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MoviesId == show.MoviesId);

                var room = await _context.CinemaTheaters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.CinemaTheaterId == show.CinemaTheaterId);

                MovieTheaters? theater = null;
                if (room != null && !string.IsNullOrWhiteSpace(room.MovieTheaterId))
                {
                    theater = await _context.MovieTheaters.AsNoTracking()
                        .FirstOrDefaultAsync(mt => mt.MovieTheaterId == room.MovieTheaterId);
                }

                detail.MovieTitle = movie?.Title ?? "N/A";
                detail.CinemaTheater = room?.Name ?? "N/A";
                detail.TheaterName = theater?.Name ?? "N/A";
                detail.TheaterAddress = theater?.Address ?? "N/A";
                detail.ShowDate = show.ShowDate;

                // StartTime / EndTime có thể DateTime hoặc TimeSpan => lấy bằng EF.Property
                DateTime? startAt = null;
                DateTime? endAt = null;

                try
                {
                    var dt = EF.Property<DateTime?>(show, "StartTime");
                    if (dt.HasValue) startAt = dt.Value;
                }
                catch { }

                if (!startAt.HasValue)
                {
                    try
                    {
                        var ts = EF.Property<TimeSpan?>(show, "StartTime");
                        if (ts.HasValue && show.ShowDate.HasValue)
                            startAt = show.ShowDate.Value.Date.Add(ts.Value);
                    }
                    catch { }
                }

                try
                {
                    var dt = EF.Property<DateTime?>(show, "EndTime");
                    if (dt.HasValue) endAt = dt.Value;
                }
                catch { }

                if (!endAt.HasValue)
                {
                    try
                    {
                        var ts = EF.Property<TimeSpan?>(show, "EndTime");
                        if (ts.HasValue && show.ShowDate.HasValue)
                            endAt = show.ShowDate.Value.Date.Add(ts.Value);
                    }
                    catch { }
                }

                detail.StartTime = startAt;
                detail.EndTime = endAt;

                // ghế
                var seatIds = tickets.Select(t => t.SeatId).ToList();
                detail.SeatLabels = await _context.Seats.AsNoTracking()
                    .Where(s => seatIds.Contains(s.SeatId))
                    .Select(s => s.Label ?? s.SeatId)
                    .OrderBy(x => x)
                    .ToListAsync();

                detail.TicketCount = tickets.Count;
                detail.TicketTotal = tickets.Sum(t => (decimal)(t.Price ?? 0m));
            }
            else
            {
                // snack-only (nếu có)
                detail.MovieTitle = "Đơn đồ ăn";
                detail.CinemaTheater = "N/A";
                detail.TheaterName = "N/A";
                detail.TheaterAddress = "N/A";
                detail.TicketCount = 0;
                detail.TicketTotal = 0m;
            }

            // snacks
            var snackLines = await (from d in _context.DetailBookingSnacks.AsNoTracking()
                                    join s in _context.Snacks.AsNoTracking()
                                        on d.SnackId equals s.SnackId
                                    where d.InvoiceId == invoiceId
                                    select new
                                    {
                                        name = s.Name,
                                        unit = (decimal)(s.Price ?? 0m),
                                        qty = (int)(d.TotalSnack ?? 0),
                                        line = (decimal)(d.TotalPrice ?? ((s.Price ?? 0m) * (d.TotalSnack ?? 0)))
                                    })
                                    .ToListAsync();

            foreach (var l in snackLines)
            {
                detail.SnackItems.Add(new EmailTicketDetail.SnackLine
                {
                    Name = l.name ?? "Snack",
                    Quantity = l.qty,
                    UnitPrice = l.unit,
                    LineTotal = l.line
                });
            }

            detail.SnackTotal = detail.SnackItems.Sum(x => x.LineTotal);

            return detail;
        }


        // 4) THÊM BuildTicketEmailBodyLikeWeb: HTML giống mẫu (tổng/giảm giá/QR)
        private string BuildTicketEmailBodyLikeWeb(EmailTicketDetail d, string? qrBase64)
        {
            string showDate = d.ShowDate?.ToString("dd/MM/yyyy") ?? "";
            string startTime = d.StartTime?.ToString("HH:mm") ?? "";
            string endTime = d.EndTime?.ToString("HH:mm") ?? "";

            // giống mẫu: CreatedAt hiển thị giờ VN (+7)
            string createdAt = d.CreatedAt.HasValue
                ? DateTime.SpecifyKind(d.CreatedAt.Value, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm")
                : "";

            string seats = (d.SeatLabels != null && d.SeatLabels.Any())
                ? string.Join(" ", d.SeatLabels.Select(s => $"<span class=\"seat-item\">{System.Net.WebUtility.HtmlEncode(s)}</span>"))
                : "";

            bool hasSnacks = d.SnackItems != null && d.SnackItems.Count > 0;

            bool hasDiscount = d.DiscountAmount > 0m && d.OriginalAmount > d.PayableAmount;

            string qrHtml = "";
            if (!string.IsNullOrWhiteSpace(qrBase64))
            {
                // nhúng data-uri
                qrHtml = $"<img src=\"data:image/png;base64,{qrBase64}\" alt=\"QR Code\" style=\"width:200px;height:200px;object-fit:contain;\" />";
            }

            string escTitle = System.Net.WebUtility.HtmlEncode(d.MovieTitle ?? "CinemaS");
            string escAddr = System.Net.WebUtility.HtmlEncode(d.TheaterAddress ?? "N/A");
            string escInv = System.Net.WebUtility.HtmlEncode(d.InvoiceId ?? "");

            var sb = new System.Text.StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8' /><meta name='viewport' content='width=device-width, initial-scale=1.0' />");
            sb.Append("<title>Hóa đơn</title><style>");
            sb.Append(@"
body { font-family: 'Georgia', 'Times New Roman', serif; margin: 0; padding: 20px; background: #fff; color: #000; }
.container { max-width: 650px; margin: 0 auto; border: 1px solid #000; padding: 40px; }
.header { border-bottom: 2px solid #000; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-content: space-between; align-items: flex-end; }
.brand { font-size: 28px; font-weight: bold; text-transform: uppercase; letter-spacing: 2px; }
.invoice-title { font-size: 16px; font-style: italic; }

.grid { display: flex; gap: 40px; margin-bottom: 30px; }
.col { flex: 1; }
.label { font-size: 10px; text-transform: uppercase; border-bottom: 1px solid #ccc; padding-bottom: 2px; margin-bottom: 5px; display: block; }
.value { font-size: 14px; font-weight: bold; }

table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
th { text-align: left; border-bottom: 1px solid #000; padding: 5px 0; font-size: 11px; text-transform: uppercase; }
td { padding: 10px 0; border-bottom: 1px dotted #ccc; font-size: 13px; }

.total-box { border-top: 2px solid #000; margin-top: 20px; padding-top: 10px; }
.total-row { display: flex; gap: 40px; align-items: baseline; justify-content: flex-end; margin-bottom: 5px; }
.subtotal-row { color: #666; font-size: 13px; }
.discount-row { color: #22c55e; font-size: 13px; }
.grand-total { font-weight: bold; font-size: 16px; }

.qr-wrap { margin-top: 18px; display: flex; justify-content: center; }
.footer { text-align: center; margin-top: 50px; font-size: 11px; font-style: italic; border-top: 1px solid #eee; padding-top: 10px; }
");
            sb.Append("</style></head><body><div class='container'>");

            // Header
            sb.Append("<div class='header'><div>");
            sb.Append("<div class='brand'>CinemaS</div>");
            sb.Append("<div style='font-size:12px; margin-top:5px;'>" + escAddr + "</div>");
            sb.Append("</div><div style='text-align:right'>");
            sb.Append("<div class='invoice-title'>HÓA ĐƠN</div><div>#" + escInv + "</div>");
            sb.Append("</div></div>");

            // Info
            sb.Append("<div class='grid'>");
            sb.Append("<div class='col'><span class='label'>KHÁCH HÀNG</span><div class='value'>" + System.Net.WebUtility.HtmlEncode(d.InvoiceEmail ?? "Khách lẻ") + "</div></div>");
            sb.Append("<div class='col'><span class='label'>NGÀY</span><div class='value'>" + System.Net.WebUtility.HtmlEncode(createdAt) + "</div></div>");
            sb.Append("</div>");

            // Table tickets
            if (d.TicketCount > 0)
            {
                sb.Append("<table><thead><tr><th width='40%'>MÔ TẢ</th><th width='20%'>PHÒNG</th><th width='20%'>GHẾ</th><th width='20%' style='text-align:right'>THÀNH TIỀN</th></tr></thead><tbody>");
                sb.Append("<tr><td><strong>" + escTitle + "</strong><br><small>" + System.Net.WebUtility.HtmlEncode(startTime + (string.IsNullOrWhiteSpace(endTime) ? "" : " - " + endTime) + " | " + showDate) + "</small></td>");
                sb.Append("<td>" + System.Net.WebUtility.HtmlEncode(d.CinemaTheater ?? "N/A") + "</td>");
                sb.Append("<td>" + seats + "</td>");
                sb.Append("<td style='text-align:right'>" + d.TicketTotal.ToString("N0") + "</td></tr></tbody></table>");
            }

            // Table snacks
            if (hasSnacks)
            {
                sb.Append("<table><thead><tr><th width='60%'>DỊCH VỤ</th><th width='20%' style='text-align:center'>SL</th><th width='20%' style='text-align:right'>THÀNH TIỀN</th></tr></thead><tbody>");
                foreach (var s in d.SnackItems)
                {
                    sb.Append("<tr><td>" + System.Net.WebUtility.HtmlEncode(s.Name) + "</td>");
                    sb.Append("<td style='text-align:center'>" + s.Quantity + "</td>");
                    sb.Append("<td style='text-align:right'>" + s.LineTotal.ToString("N0") + "</td></tr>");
                }
                sb.Append("</tbody></table>");
            }

            // Total (có/không discount)
            sb.Append("<div class='total-box'>");
            if (hasDiscount)
            {
                sb.Append("<div class='total-row subtotal-row'><span>TỔNG TIỀN (GỐC):</span><span>" + d.OriginalAmount.ToString("N0") + " VND</span></div>");
                sb.Append("<div class='total-row discount-row'><span>GIẢM GIÁ:</span><span>-" + d.DiscountAmount.ToString("N0") + " VND</span></div>");
                sb.Append("<div class='total-row'><span>TỔNG CỘNG (VND):</span><span class='grand-total'>" + d.PayableAmount.ToString("N0") + "</span></div>");
            }
            else
            {
                var grand = d.TicketTotal + d.SnackTotal;
                sb.Append("<div class='total-row'><span>TỔNG CỘNG (VND):</span><span class='grand-total'>" + grand.ToString("N0") + "</span></div>");
            }
            sb.Append("</div>");

            // QR
            if (!string.IsNullOrWhiteSpace(qrHtml))
            {
                sb.Append("<div class='qr-wrap'>" + qrHtml + "</div>");
            }

            sb.Append("<div class='footer'>Cảm ơn quý khách đã sử dụng dịch vụ.</div>");
            sb.Append("</div></body></html>");

            return sb.ToString();
        }


        private string BuildEmailBodyWithQr(string invoiceId, string qrDataUrl)
        {
            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head><meta charset=""utf-8"" /></head>
<body style=""font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:14px;line-height:1.6;color:#111827;"">
  <p><strong>Đã đặt vé thành công.</strong></p>
  <p><strong>Mã hóa đơn:</strong> {invoiceId}</p>
  {(string.IsNullOrWhiteSpace(qrDataUrl) ? "" : $@"<p><strong>QR Check-in:</strong></p><p><img src=""{qrDataUrl}"" style=""width:200px;height:200px;"" /></p>")}
</body>
</html>";
        }

        /* ===================== Discount helpers (mobile) ===================== */

        private sealed record PromotionValidationResult(bool Success, string? ErrorMessage);

        private DateTime NowVn()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        private static string NormalizePromoCode(string code) => (code ?? "").Trim().ToLower();

        private static (bool ok, double percent) NormalizeDiscountPercent(double raw)
        {
            double percent = (raw > 0 && raw < 1) ? raw * 100.0 : raw;
            if (percent <= 0 || percent > 100) return (false, 0);
            return (true, percent);
        }

        private PromotionValidationResult ValidatePromotion(Promotion promo, DateTime nowVn)
        {
            if (promo.Status != true)
                return new(false, "Promotion disabled");

            if (promo.StartDay.HasValue && promo.StartDay.Value > nowVn)
                return new(false, "Promotion not started");

            if (promo.EndDay.HasValue && promo.EndDay.Value < nowVn)
                return new(false, "Promotion expired");

            if (!promo.Discount.HasValue)
                return new(false, "Promotion missing discount");

            var (ok, _) = NormalizeDiscountPercent(promo.Discount.Value);
            if (!ok)
                return new(false, "Invalid discount percent");

            return new(true, null);
        }

        private async Task<(bool ok, decimal total, string? error)> GetInvoiceBaseTotalForPricingAsync(
            string invoiceId,
            decimal? fallbackTotalPrice,
            string? promotionId)
        {
            // ưu tiên OriginalTotal nếu có
            var inv = await _context.Invoices.AsNoTracking()
                .Where(i => i.InvoiceId == invoiceId)
                .Select(i => new { i.TotalPrice, i.PromotionId, i.OriginalTotal })
                .FirstOrDefaultAsync();

            if (inv != null && inv.OriginalTotal.HasValue && inv.OriginalTotal.Value > 0m)
                return (true, inv.OriginalTotal.Value, null);

            decimal ticketSum = 0m;
            try
            {
                ticketSum = await _context.Tickets.AsNoTracking()
                    .Where(t => t.InvoiceId == invoiceId)
                    .SumAsync(t => (decimal)(t.Price ?? 0m));
            }
            catch { ticketSum = 0m; }

            decimal snackSum = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(s => s.InvoiceId == invoiceId)
                .SumAsync(s => (decimal)(s.TotalPrice ?? 0m));

            var sum = ticketSum + snackSum;
            if (sum > 0m) return (true, sum, null);

            if (fallbackTotalPrice.HasValue && fallbackTotalPrice.Value > 0m)
                return (true, fallbackTotalPrice.Value, null);

            return (false, 0m, "Cannot compute base total");
        }

        private async Task<(decimal original, decimal discount, decimal payable, double? percent, string? promoName)> GetDiscountInfoAsync(
            string invoiceId,
            decimal? fallbackTotal)
        {
            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return (0m, 0m, 0m, null, null);

            var baseTotal = await GetInvoiceBaseTotalForPricingAsync(invoiceId, fallbackTotal, invoice.PromotionId);
            if (!baseTotal.ok)
                return (0m, 0m, 0m, null, null);

            decimal original = baseTotal.total;

            if (string.IsNullOrWhiteSpace(invoice.PromotionId))
                return (original, 0m, invoice.TotalPrice ?? original, null, null);

            var promo = await _context.Promotion.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PromotionId == invoice.PromotionId);

            if (promo == null)
                return (original, 0m, invoice.TotalPrice ?? 0m, null, null);

            var nowVn = NowVn();
            var valid = ValidatePromotion(promo, nowVn);
            if (!valid.Success)
                return (original, 0m, invoice.TotalPrice ?? 0m, null, null);

            var (_, p) = NormalizeDiscountPercent(promo.Discount!.Value);

            decimal payable = invoice.TotalPrice ?? 0m;
            decimal discount = original - payable;
            if (discount < 0) discount = 0;

            return (original, discount, payable, p, promo.Name);
        }

        // set OriginalTotal nếu model có field (web đang dùng). Nếu không có field thì bỏ qua.
        private void TrySetInvoiceOriginalTotal(Invoices invoice, decimal originalTotal)
        {
            try
            {
                invoice.OriginalTotal = originalTotal;
            }
            catch
            {
                // Không đủ dữ liệu để xác minh model Invoices có OriginalTotal trong mobile project
            }
        }

        /* ===================== Points (copy từ web) ===================== */

        private async Task<string> NextPointHistoryIdSafeAsync()
        {
            string lastId = await _context.PointHistories.AsNoTracking()
                .OrderByDescending(p => p.PointHistoryId)
                .Select(p => p.PointHistoryId)
                .FirstOrDefaultAsync() ?? "PH00000000";

            int nextNum = 0;
            if (lastId.Length >= 10 && lastId.StartsWith("PH"))
                _ = int.TryParse(lastId.Substring(2), out nextNum);

            while (true)
            {
                nextNum++;
                string candidate = $"PH{nextNum:D8}";
                bool exists = await _context.PointHistories.AsNoTracking()
                    .AnyAsync(p => p.PointHistoryId == candidate);
                if (!exists) return candidate;
            }
        }

        private async Task AwardPointsForInvoiceAsync(Invoices invoice)
        {
            if (invoice == null) return;
            if (invoice.Status != (byte)1) return;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == invoice.CustomerId);

            if (user == null) return;

            bool existedHistory = await _context.PointHistories.AsNoTracking()
                .AnyAsync(ph => ph.InvoiceId == invoice.InvoiceId && ph.UserId == user.UserId);

            if (existedHistory) return;

            var rank = await _context.Set<MembershipRank>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.MembershipRankId == user.MembershipRankId);

            int pointPerTicket = rank?.PointReturnTicket ?? 0;
            int pointPerCombo = rank?.PointReturnCombo ?? 0;

            int ticketCount = await _context.Tickets.AsNoTracking()
                .CountAsync(t => t.InvoiceId == invoice.InvoiceId);

            if (ticketCount <= 0)
                ticketCount = invoice.TotalTicket ?? 0;

            int snackQty = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(d => d.InvoiceId == invoice.InvoiceId)
                .SumAsync(d => (int)(d.TotalSnack ?? 0));

            int earned = (ticketCount * pointPerTicket) + (snackQty * pointPerCombo);
            if (earned <= 0) return;

            user.SavePoint = (user.SavePoint ?? 0) + earned;
            user.UpdatedAt = DateTime.UtcNow;

            var newId = await NextPointHistoryIdSafeAsync();

            _context.PointHistories.Add(new PointHistories
            {
                PointHistoryId = newId,
                UserId = user.UserId,
                InvoiceId = invoice.InvoiceId,
                ChangeAmount = earned,
                Reason = $"+{earned} points from invoice {invoice.InvoiceId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        // GET: /api/booking/history?userId=U001&page=1&pageSize=50
        [HttpGet("booking/history")]
        public async Task<IActionResult> BookingHistory(
            [FromQuery] string userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { ok = false, message = "Missing userId" });

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            // Chỉ lấy invoice đã PAID (Status=1) để đúng "lịch sử đặt vé"
            // Nếu bạn muốn thấy cả pending/failed thì bỏ điều kiện Status == 1
            var baseQuery = _context.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == userId && i.Status == (byte)1)
                .OrderByDescending(i => i.CreatedAt);

            var totalItems = await baseQuery.CountAsync();

            var invoices = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    i.InvoiceId,
                    i.CreatedAt,
                    i.TotalPrice,
                    i.TotalTicket,
                    i.Status
                })
                .ToListAsync();

            var results = new List<object>();

            foreach (var inv in invoices)
            {
                // lấy 1 ticket bất kỳ để suy ra showtime/movie/room
                var firstTicket = await _context.Tickets.AsNoTracking()
                    .Where(t => t.InvoiceId == inv.InvoiceId)
                    .OrderBy(t => t.TicketId)
                    .Select(t => new { t.ShowTimeId })
                    .FirstOrDefaultAsync();

                string movieTitle = "";
                string roomName = "";
                string startTimeIso = "";

                if (firstTicket != null)
                {
                    var show = await _context.ShowTimes.AsNoTracking()
                        .Where(st => st.ShowTimeId == firstTicket.ShowTimeId)
                        .Select(st => new
                        {
                            st.ShowTimeId,
                            st.MoviesId,
                            st.CinemaTheaterId,
                            st.ShowDate,
                            // StartTime có thể là DateTime hoặc TimeSpan tùy model -> xử lý sau
                        })
                        .FirstOrDefaultAsync();

                    if (show != null)
                    {
                        var movie = await _context.Movies.AsNoTracking()
                            .Where(m => m.MoviesId == show.MoviesId)
                            .Select(m => new { m.Title })
                            .FirstOrDefaultAsync();

                        var room = await _context.CinemaTheaters.AsNoTracking()
                            .Where(r => r.CinemaTheaterId == show.CinemaTheaterId)
                            .Select(r => new { r.Name })
                            .FirstOrDefaultAsync();

                        movieTitle = movie?.Title ?? "";
                        roomName = room?.Name ?? "";

                        // cố lấy StartTime từ EF.Property để không phụ thuộc kiểu dữ liệu
                        DateTime? startAt = null;

                        try
                        {
                            var dt = EF.Property<DateTime?>(_context.ShowTimes.Local.FirstOrDefault() ?? new ShowTimes(), "StartTime");
                        }
                        catch { }

                        // lấy StartTime trực tiếp từ DB object
                        var showEntity = await _context.ShowTimes.AsNoTracking()
                            .FirstOrDefaultAsync(st => st.ShowTimeId == show.ShowTimeId);

                        if (showEntity != null)
                        {
                            try
                            {
                                var dt = EF.Property<DateTime?>(showEntity, "StartTime");
                                if (dt.HasValue) startAt = dt.Value;
                            }
                            catch { }

                            if (!startAt.HasValue)
                            {
                                try
                                {
                                    var ts = EF.Property<TimeSpan?>(showEntity, "StartTime");
                                    if (ts.HasValue && show.ShowDate.HasValue)
                                        startAt = show.ShowDate.Value.Date.Add(ts.Value);
                                }
                                catch { }
                            }
                        }

                        startTimeIso = startAt.HasValue ? startAt.Value.ToString("o") : "";
                    }
                }

                results.Add(new
                {
                    invoiceId = inv.InvoiceId,
                    createdAt = inv.CreatedAt,
                    status = inv.Status,
                    totalPrice = inv.TotalPrice ?? 0m,
                    totalTicket = inv.TotalTicket ?? 0,
                    movieTitle = movieTitle,
                    room = roomName,
                    startTime = startTimeIso
                });
            }

            return Ok(new
            {
                ok = true,
                success = true,
                page,
                pageSize,
                totalItems,
                items = results
            });
        }

    }
}
