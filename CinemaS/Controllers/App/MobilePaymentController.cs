// Controllers/MobilePaymentController.cs
// FIX TRIỆT ĐỂ 3 LỖI Ở ẢNH:
// 1) CS0266 decimal? -> decimal  (ticketSum += price; price đang là decimal?)
//    => ép về decimal: (seatTypes.TryGetValue(...) ? p : 0m)
// 2) CS0117 DetailBookingSnacks không có CreatedAt/UpdatedAt
//    => xóa 2 field khi tạo DetailBookingSnacks
// 3) CS0266 int -> byte? (PaymentTransactions.Status, Invoices.Status, Tickets.Status)
//    => set (byte)0/(byte)1/(byte)2 ở mọi chỗ

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

        public class SnackLineDto
        {
            [JsonPropertyName("snackId")] public string SnackId { get; set; } = "";
            [JsonPropertyName("qty")] public int Qty { get; set; }
            [JsonPropertyName("price")] public int Price { get; set; }
            [JsonPropertyName("size")] public string? Size { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
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

        // POST: /api/vnpay/create
        [HttpPost("vnpay/create")]
        public async Task<IActionResult> CreateVnpay([FromBody] CreateVnpayRequest req)
        {
            if (req == null) return BadRequest();

            var userId = ReadUserId(req.UserId);
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId");

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

            var now = DateTime.Now;

            // blocked: pending (1) còn hạn OR booked(2)
            var blocked = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == req.ShowtimeId &&
                            req.SeatIds.Contains(t.SeatId) &&
                            (t.Status == (byte)2 || (t.Status == (byte)1 && t.Expire != null && t.Expire > now)))
                .Select(t => t.SeatId)
                .ToListAsync();

            if (blocked.Count > 0)
                return Conflict(new { ok = false, message = "Seat already booked", seats = blocked });

            var seatTypeIds = seats.Select(s => s.SeatTypeId).Distinct().ToList();

            // NOTE: SeatTypes.Price trong DB có thể là decimal? (không đủ dữ liệu để xác minh),
            // nên ép về decimal tại lúc cộng dồn để hết CS0266.
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

            foreach (var seat in seats)
            {
                // ✅ FIX CS0266: luôn về decimal
                var price = seatTypes.TryGetValue(seat.SeatTypeId, out var p) ? (p ?? 0m) : 0m;
                ticketSum += price;

                var ticketId = await NextTicketIdSafeAsync();
                var ticketTypeId = MapTicketTypeId(seat.SeatTypeId);

                var t = new Tickets
                {
                    TicketId = ticketId,
                    InvoiceId = invoiceId,
                    ShowTimeId = req.ShowtimeId,
                    SeatId = seat.SeatId,
                    TicketTypeId = ticketTypeId,
                    Price = price,
                    Status = (byte)1, // pending
                    CreatedBooking = now,
                    Expire = now.AddMinutes(15)
                };
                await _context.Tickets.AddAsync(t);
            }

            decimal snackSum = 0m;
            if (req.Snacks != null && req.Snacks.Count > 0)
            {
                foreach (var s in req.Snacks.Where(x => x.Qty > 0))
                {
                    if (!snacksDb.ContainsKey(s.SnackId)) continue;

                    // Snacks.Price có thể là decimal? -> ép về decimal
                    var unit = snacksDb[s.SnackId].Price ?? 0m;
                    var total = unit * s.Qty;
                    snackSum += total;

                    var dbsId = await NextDbSnackIdSafeAsync();

                    // ✅ FIX CS0117: model DetailBookingSnacks không có CreatedAt/UpdatedAt
                    var d = new DetailBookingSnacks
                    {
                        DetailBookingSnackId = dbsId,
                        InvoiceId = invoiceId,
                        SnackId = s.SnackId,
                        TotalSnack = s.Qty,
                        TotalPrice = total
                    };

                    await _context.DetailBookingSnacks.AddAsync(d);
                }
            }

            invoice.TotalTicket = seats.Count;
            invoice.TotalPrice = ticketSum + snackSum;

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

            // invoice.TotalPrice là decimal?
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

            return Ok(new { ok = true, success = true, orderId = invoiceId, paymentUrl = payUrl });
        }

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

            await SavePaymentTransactionAsync(invoiceId, valid, respCode, txnStatus, amountStr, bankCode, txnNo, payDate);

            if (!valid || respCode != "00" || txnStatus != "00")
            {
                await CleanupPendingAsync(invoiceId);
                return Content("<html><body>Payment Failed</body></html>", "text/html");
            }

            await FinalizePaidAsync(invoiceId);

            return Content("<html><body>Payment Success</body></html>", "text/html");
        }

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

            return Ok(new
            {
                invoiceId,
                movieTitle = movie?.Title ?? "",
                cinemaName = theater?.Name ?? "",
                screenName = room?.Name ?? "",
                startTime = startAt.HasValue ? startAt.Value.ToString("o") : "",
                seats = seatLabels,
                total = (int)(invoice.TotalPrice ?? 0m),
                totalAmount = (double)(invoice.TotalPrice ?? 0m),
                snacks = snackLines,
                qrData
            });
        }

        // ---------------- helpers ----------------
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
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 4)
            {
                var numPart = last.Substring(3);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            return $"DBS{next:D8}";
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

            var pm = await _context.PaymentMethods.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "VNPAY" || x.Provider == "VNPAY");

            var id = await NextPaymentTransactionIdSafeAsync();
            var now = DateTime.Now;

            var pt = new PaymentTransactions
            {
                PaymentTransactionId = id,
                InvoiceId = invoiceId,
                PaymentMethodId = pm != null ? pm.PaymentMethodId : "PM_VNPAY",
                Amount = amount,
                Currency = "VND",
                // ✅ FIX int -> byte?
                Status = (byte)((signatureValid && respCode == "00" && txnStatus == "00") ? 1 : 0),
                ProviderTxnId = txnNo,
                ProviderOrderNo = bankCode,
                Description = "VNPAY MOBILE",
                FailureReason = signatureValid ? respCode : "INVALID_SIGNATURE",
                CreatedAt = now,
                UpdatedAt = now,
                PaidAt = (signatureValid && respCode == "00" && txnStatus == "00") ? now : null
            };

            await _context.PaymentTransactions.AddAsync(pt);
            await _context.SaveChangesAsync();
        }

        private async Task CleanupPendingAsync(string invoiceId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null) return;

            var tickets = await _context.Tickets.Where(t => t.InvoiceId == invoiceId).ToListAsync();
            var snacks = await _context.DetailBookingSnacks.Where(d => d.InvoiceId == invoiceId).ToListAsync();

            _context.Tickets.RemoveRange(tickets);
            _context.DetailBookingSnacks.RemoveRange(snacks);
            _context.Invoices.Remove(invoice);

            await _context.SaveChangesAsync();
        }

        private async Task FinalizePaidAsync(string invoiceId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null) return;

            if (invoice.Status == (byte)1) return;

            // ✅ FIX int -> byte?
            invoice.Status = (byte)1;
            invoice.UpdatedAt = DateTime.Now;

            var tickets = await _context.Tickets.Where(t => t.InvoiceId == invoiceId).ToListAsync();
            foreach (var t in tickets)
            {
                // ✅ FIX int -> byte?
                t.Status = (byte)2;
                t.Expire = null;
            }

            await _context.SaveChangesAsync();

            var qrBase64 = await _qrTicketService.GenerateQrImageBase64Async(invoiceId, 8);
            var qrDataUrl = string.IsNullOrWhiteSpace(qrBase64) ? "" : $"data:image/png;base64,{qrBase64}";

            var toEmail = invoice.Email;
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                var user = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == invoice.CustomerId);
                toEmail = user?.Email;
            }
            if (string.IsNullOrWhiteSpace(toEmail)) return;

            var subject = $"Đã đặt vé thành công - {invoiceId}";
            var body = BuildEmailBodyWithQr(invoiceId, qrDataUrl);

            await _emailSender.SendEmailAsync(toEmail, subject, body);
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
    }
}
