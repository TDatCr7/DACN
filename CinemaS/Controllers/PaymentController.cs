using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaS.VNPAY;

namespace CinemaS.Controllers
{
    [Route("[controller]")]
    public class PaymentController : Controller
    {
        private readonly IConfiguration _cfg;
        private readonly CinemaContext _context;

        public PaymentController(IConfiguration cfg, CinemaContext context)
        {
            _cfg = cfg;
            _context = context;
        }

        /* ===================== 1) Tạo URL VNPay ===================== */
        [HttpPost("Create")]
        [IgnoreAntiforgeryToken]
        public IActionResult Create([FromBody] PayRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.OrderId) || req.Amount <= 0)
                return Json(new { success = false, message = "Dữ liệu tạo thanh toán không hợp lệ." });

            var returnUrl = Url.Action(nameof(ReturnVnPay), "Payment", values: null, protocol: Request.Scheme)!;

            var vnpSection = _cfg.GetSection("VnPay");
            string tmnCode = vnpSection["TmnCode"] ?? "";
            string hashSecret = vnpSection["HashSecret"] ?? "";
            string baseUrl = vnpSection["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            string locale = vnpSection["Locale"] ?? "vn";
            string currCode = vnpSection["CurrCode"] ?? "VND";
            string orderType = vnpSection["OrderType"] ?? "other";

            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var expire = now.AddMinutes(10);

            var vnp = new VnPayLibrary();
            vnp.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
            vnp.AddRequestData("vnp_Command", "pay");
            vnp.AddRequestData("vnp_TmnCode", tmnCode);
            vnp.AddRequestData("vnp_Amount", ((long)req.Amount * 100).ToString()); // *100 theo chuẩn VNPay
            vnp.AddRequestData("vnp_CurrCode", currCode);
            vnp.AddRequestData("vnp_TxnRef", req.OrderId);
            vnp.AddRequestData("vnp_OrderInfo", string.IsNullOrWhiteSpace(req.OrderInfo) ? $"Thanh toan {req.OrderId}" : req.OrderInfo);
            vnp.AddRequestData("vnp_OrderType", orderType);
            vnp.AddRequestData("vnp_Locale", locale);
            vnp.AddRequestData("vnp_ReturnUrl", returnUrl);
            vnp.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnp.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
            vnp.AddRequestData("vnp_ExpireDate", expire.ToString("yyyyMMddHHmmss"));

            var url = vnp.CreateRequestUrl(baseUrl, hashSecret);
            return Json(new { success = true, paymentUrl = url });
        }

        /* ============ 2) VNPay redirect về: xác thực + lập vé + cập nhật DB ============ */
        [HttpGet("ReturnVnPay")]
        [AllowAnonymous]
        public async Task<IActionResult> ReturnVnPay()
        {
            var vnpSection = _cfg.GetSection("VnPay");
            string secret = vnpSection["HashSecret"] ?? "";

            var vnp = new VnPayLibrary();
            foreach (var (k, v) in Request.Query) vnp.AddResponseData(k, v!);

            string invoiceId = vnp.GetResponseData("vnp_TxnRef") ?? "";
            string respCode = vnp.GetResponseData("vnp_ResponseCode");        // 00
            string txnStatus = vnp.GetResponseData("vnp_TransactionStatus");   // 00
            string secureHash = vnp.GetResponseData("vnp_SecureHash");
            string providerTxnId = vnp.GetResponseData("vnp_TransactionNo") ?? "";
            string bankCode = vnp.GetResponseData("vnp_BankCode") ?? "";
            string payDate = vnp.GetResponseData("vnp_PayDate") ?? "";
            long vnpAmount = long.TryParse(vnp.GetResponseData("vnp_Amount"), out var amt) ? amt : 0;

            bool signatureOk = vnp.ValidateSignature(secureHash, secret);

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            var vm = new PaymentResultVM
            {
                OrderId = invoiceId,
                IsValidSignature = signatureOk,
                VnpResponseCode = respCode,
                VnpTransactionStatus = txnStatus,
                BankCode = bankCode,
                Amount = ParseVnpAmount(vnp.GetResponseData("vnp_Amount")),
                PayDateRaw = payDate,
                TransactionNo = providerTxnId,
                IsSuccess = false,
                Message = signatureOk ? "Thanh toán không thành công." : "Chữ ký VNPay không hợp lệ."
            };

            // Always save payment transaction for history
            var paymentMethod = await _context.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "VNPAY");
            await SavePaymentTransactionAsync(
                invoiceId,
                paymentMethod?.PaymentMethodId ?? "PM001",
                vnpAmount / 100,
                respCode,
                txnStatus,
                providerTxnId,
                invoiceId,
                bankCode,
                payDate,
                signatureOk,
                status: (signatureOk && respCode == "00" && txnStatus == "00") ? (byte)1 : (byte)2,
                failureReason: (signatureOk && respCode == "00" && txnStatus == "00") ? null : $"Response: {respCode}, Status: {txnStatus}"
            );

            if (signatureOk && respCode == "00" && txnStatus == "00" && invoice != null)
            {
                using var tx = await _context.Database.BeginTransactionAsync();

                // 1) Update invoice
                invoice.Status = (byte)1;       // 1 = Paid
                invoice.UpdatedAt = DateTime.UtcNow;

                if (paymentMethod != null) invoice.PaymentMethodId = paymentMethod.PaymentMethodId;
                else invoice.PaymentMethod = "VNPAY";

                // 2) Nếu chưa có vé cho invoice này -> dựng từ Session
                bool alreadyHasTickets = await _context.Tickets.AsNoTracking().AnyAsync(t => t.InvoiceId == invoice.InvoiceId);
                if (!alreadyHasTickets)
                {
                    var raw = HttpContext.Session.GetString($"pending:{invoice.InvoiceId}");
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var payload = JsonSerializer.Deserialize<PendingSelection>(raw) ?? new PendingSelection();

                        var st = await _context.ShowTimes.AsNoTracking()
                                   .FirstOrDefaultAsync(x => x.ShowTimeId == payload.ShowTimeId);
                        if (st != null)
                        {
                            // Tập ghế yêu cầu (distinct)
                            var requestSeatIds = payload.SeatIds?.Where(s => !string.IsNullOrWhiteSpace(s))
                                                 .Select(s => s.Trim()).Distinct().ToList() ?? new List<string>();

                            if (requestSeatIds.Count > 0)
                            {
                                // GHẾ đã có vé trong DB cho suất này (bất kể status)
                                var occupied = await _context.Tickets.AsNoTracking()
                                    .Where(t => t.ShowTimeId == st.ShowTimeId && requestSeatIds.Contains(t.SeatId))
                                    .Select(t => t.SeatId)
                                    .ToListAsync();
                                var occupiedSet = new HashSet<string>(occupied);

                                // Chỉ giữ ghế còn trống thật sự
                                var finalSeats = requestSeatIds.Where(id => !occupiedSet.Contains(id)).ToList();

                                var seats = await _context.Seats.AsNoTracking()
                                                    .Where(s => finalSeats.Contains(s.SeatId))
                                                    .ToListAsync();
                                var seatTypes = await _context.SeatTypes.AsNoTracking().ToListAsync();

                                // ======= LẤY SẴN ticket id bắt đầu – chỉ 1 lần và SAFE =======
                                string lastId = await _context.Tickets.AsNoTracking()
                                                    .OrderByDescending(t => t.TicketId)
                                                    .Select(t => t.TicketId)
                                                    .FirstOrDefaultAsync() ?? "T000000";
                                int nextNum = 0;
                                _ = int.TryParse(lastId.Length > 1 ? lastId.Substring(1) : "0", out nextNum);

                                async Task<string> NextTicketIdSafeAsync()
                                {
                                    while (true)
                                    {
                                        nextNum++;
                                        string candidate = $"T{nextNum:D6}";
                                        bool exists = await _context.Tickets.AsNoTracking()
                                                            .AnyAsync(t => t.TicketId == candidate);
                                        if (!exists) return candidate;
                                    }
                                }

                                decimal ticketSum = 0m;

                                foreach (var sid in finalSeats)
                                {
                                    var seat = seats.FirstOrDefault(x => x.SeatId == sid);
                                    var stype = seatTypes.FirstOrDefault(x => x.SeatTypeId == seat?.SeatTypeId);

                                    // Map TicketType based on SeatType: ST001 -> TT001, ST002 -> TT002
                                    string ticketTypeId = "TT001"; // default
                                    if (stype != null && !string.IsNullOrEmpty(stype.SeatTypeId) && stype.SeatTypeId.Length >= 5)
                                    {
                                        ticketTypeId = "TT" + stype.SeatTypeId.Substring(2); // ST001 -> TT001
                                    }

                                    decimal price = stype?.Price ?? 0m;
                                    ticketSum += price;

                                    var newTicketId = await NextTicketIdSafeAsync();

                                    _context.Tickets.Add(new Tickets
                                    {
                                        TicketId = newTicketId,
                                        InvoiceId = invoice.InvoiceId,
                                        TicketTypeId = ticketTypeId, // Map theo SeatType
                                        ShowTimeId = st.ShowTimeId,
                                        SeatId = sid,
                                        Status = (byte)2, // Paid
                                        CreatedBooking = DateTime.UtcNow,
                                        Expire = null,
                                        Price = (int?)Convert.ToInt32(price)
                                    });
                                }

                                // ========= Snack lines =========
                                decimal snackSum = 0m;
                                if (payload.Snacks?.Any() == true)
                                {
                                    var sids = payload.Snacks.Select(x => x.SnackId).Distinct().ToList();
                                    var snackMap = await _context.Snacks.AsNoTracking()
                                        .Where(s => sids.Contains(s.SnackId))
                                        .ToDictionaryAsync(s => s.SnackId, s => new { s.Name, Unit = (s.Price ?? 0m) });

                                    // Lấy DBS id bắt đầu – SAFE
                                    string lastDbId = await _context.DetailBookingSnacks.AsNoTracking()
                                                        .OrderByDescending(d => d.DetailBookingSnackId)
                                                        .Select(d => d.DetailBookingSnackId)
                                                        .FirstOrDefaultAsync() ?? "DBS000";
                                    int nextDb = 0;
                                    _ = int.TryParse(lastDbId.Length > 3 ? lastDbId.Substring(3) : "0", out nextDb);

                                    async Task<string> NextDbSnackIdSafeAsync()
                                    {
                                        while (true)
                                        {
                                            nextDb++;
                                            string cand = $"DBS{nextDb:D3}";
                                            bool exists = await _context.DetailBookingSnacks.AsNoTracking()
                                                             .AnyAsync(d => d.DetailBookingSnackId == cand);
                                            if (!exists) return cand;
                                        }
                                    }

                                    foreach (var line in payload.Snacks)
                                    {
                                        if (!snackMap.TryGetValue(line.SnackId, out var s)) continue;
                                        int qty = Math.Max(1, line.Quantity);
                                        decimal lineTotal = s.Unit * qty;

                                        var newDbId = await NextDbSnackIdSafeAsync();

                                        _context.DetailBookingSnacks.Add(new DetailBookingSnacks
                                        {
                                            DetailBookingSnackId = newDbId,
                                            InvoiceId = invoice.InvoiceId,
                                            SnackId = line.SnackId,
                                            TotalSnack = qty,
                                            TotalPrice = lineTotal
                                        });

                                        snackSum += lineTotal;
                                    }

                                    invoice.TotalPrice = ticketSum + snackSum;
                                }
                                else
                                {
                                    invoice.TotalPrice = ticketSum;
                                }

                                invoice.TotalTicket = finalSeats.Count;
                            }
                        }
                    }
                }

                // Cố gắng lưu; nếu vẫn dính race (seat vừa bị chốt), ta loại bỏ ghế gây lỗi và lưu lại
                try
                {
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    vm.IsSuccess = true;
                    vm.Message = "Thanh toán thành công.";
                    vm.Detail = await BuildTicketDetailAsync(invoice.InvoiceId);

                    HttpContext.Session.Remove($"pending:{invoice.InvoiceId}");
                }
                catch (DbUpdateException)
                {
                    // Xử lý mềm: loại tất cả vé vi phạm (theo (ShowTimeId, SeatId)) rồi lưu lại
                    await tx.RollbackAsync();

                    using var tx2 = await _context.Database.BeginTransactionAsync();

                    var tickets = await _context.Tickets
                        .Where(t => t.InvoiceId == invoice.InvoiceId)
                        .ToListAsync();

                    // Ghế đã có sẵn vé (bởi invoice khác) => remove khỏi invoice hiện tại
                    foreach (var t in tickets.ToList())
                    {
                        bool duplicateSeat = await _context.Tickets.AsNoTracking()
                            .AnyAsync(x => x.ShowTimeId == t.ShowTimeId && x.SeatId == t.SeatId && x.InvoiceId != t.InvoiceId);
                        if (duplicateSeat)
                            _context.Tickets.Remove(t);
                    }

                    await _context.SaveChangesAsync();
                    await tx2.CommitAsync();

                    vm.IsSuccess = true; // vẫn coi là thành công với số ghế còn lại
                    vm.Message = "Thanh toán thành công (một số ghế đã được đặt trước đó và đã được loại bỏ).";
                    vm.Detail = await BuildTicketDetailAsync(invoice.InvoiceId);

                    HttpContext.Session.Remove($"pending:{invoice.InvoiceId}");
                }
            }
            else
            {
                if (invoice != null)
                    vm.Detail = await BuildTicketDetailAsync(invoice.InvoiceId);
            }

            return View("Result", vm);
        }

        /* ===================== 3) Xem lại kết quả theo InvoiceId ===================== */
        [HttpGet("Result/{invoiceId}")]
        [Authorize]
        public async Task<IActionResult> ResultById(string invoiceId)
        {
            var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null) return NotFound();

            // Get payment transaction for this invoice
            var paymentTxn = await _context.PaymentTransactions.AsNoTracking()
                .Where(pt => pt.InvoiceId == invoiceId)
                .OrderByDescending(pt => pt.CreatedAt)
                .FirstOrDefaultAsync();

            var vm = new PaymentResultVM
            {
                OrderId = invoice.InvoiceId,
                IsSuccess = invoice.Status == (byte)1,
                Message = invoice.Status == (byte)1 ? "Thanh toán thành công." : "Đơn hàng chưa thanh toán.",
                Detail = await BuildTicketDetailAsync(invoice.InvoiceId)
            };

            // Populate transaction info if available
            if (paymentTxn != null)
            {
                vm.IsValidSignature = paymentTxn.Status == 1;

                // Extract bank code from description (format: "VNPay - Bank: NCB, Response: 00, Status: 00")
                var description = paymentTxn.Description ?? "";
                vm.BankCode = ExtractBankCodeFromDescription(description);

                vm.VnpResponseCode = description.Contains("Response:")
                    ? ExtractValueFromDescription(description, "Response:")
                    : (paymentTxn.Status == 1 ? "00" : "");
                vm.VnpTransactionStatus = description.Contains("Status:")
                    ? ExtractValueFromDescription(description, "Status:")
                    : (paymentTxn.Status == 1 ? "00" : "");

                vm.Amount = (int)paymentTxn.Amount.GetValueOrDefault(0);
                vm.TransactionNo = paymentTxn.ProviderTxnId;
                vm.PayDateRaw = paymentTxn.PaidAt?.ToString("yyyyMMddHHmmss");
            }

            return View("Result", vm);
        }

        private string ExtractBankCodeFromDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return "";

            // Format: "VNPay - Bank: NCB, Response: 00, Status: 00"
            var parts = description.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains("Bank:"))
                {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex + 1 < trimmed.Length)
                    {
                        return trimmed.Substring(colonIndex + 1).Trim();
                    }
                }
            }
            return "";
        }

        private string ExtractValueFromDescription(string description, string key)
        {
            var parts = description.Split(',');
            foreach (var part in parts)
            {
                if (part.Trim().Contains(key))
                {
                    var colonIndex = part.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex + 1 < part.Length)
                    {
                        return part.Substring(colonIndex + 1).Trim();
                    }
                }
            }
            return "";
        }

        /* ===================== 4) Lịch sử hoá đơn của user ===================== */
        [HttpGet("History")]
        [Authorize]
        public async Task<IActionResult> History()
        {
            var email = User.Identity?.Name;
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return View("History", new List<InvoiceHistoryVM>());

            var invoices = await _context.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == user.UserId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var vm = new List<InvoiceHistoryVM>();
            foreach (var inv in invoices)
            {
                var anyTicket = await _context.Tickets.AsNoTracking()
                    .Where(t => t.InvoiceId == inv.InvoiceId)
                    .OrderBy(t => t.TicketId)
                    .FirstOrDefaultAsync();

                string? movie = null, room = null;
                DateTime? showDate = null, start = null;

                if (anyTicket != null)
                {
                    var st = await _context.ShowTimes.AsNoTracking().FirstOrDefaultAsync(x => x.ShowTimeId == anyTicket.ShowTimeId);
                    if (st != null)
                    {
                        showDate = st.ShowDate;
                        start = st.StartTime;

                        var mv = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);
                        var ct = await _context.CinemaTheaters.AsNoTracking().FirstOrDefaultAsync(c => c.CinemaTheaterId == st.CinemaTheaterId);
                        movie = mv?.Title;
                        room = ct?.Name;
                    }
                }

                vm.Add(new InvoiceHistoryVM
                {
                    InvoiceId = inv.InvoiceId,
                    CreatedAt = inv.CreatedAt,
                    Status = inv.Status.GetValueOrDefault(0),
                    TotalPrice = inv.TotalPrice.GetValueOrDefault(0m), // DECIMAL!
                    MovieTitle = movie,
                    Room = room,
                    ShowDate = showDate,
                    StartTime = start
                });
            }

            return View("History", vm);
        }

        /* ===================== Helpers ===================== */

        private static int ParseVnpAmount(string? vnpAmount)
            => (long.TryParse(vnpAmount, out var raw) ? (int)(raw / 100) : 0);

        private async Task<TicketDetailVM?> BuildTicketDetailAsync(string invoiceId)
        {
            var inv = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (inv == null) return null;

            var tickets = await _context.Tickets.AsNoTracking().Where(t => t.InvoiceId == invoiceId).ToListAsync();
            if (!tickets.Any()) return null;

            var first = tickets.First();
            var st = await _context.ShowTimes.AsNoTracking().FirstOrDefaultAsync(x => x.ShowTimeId == first.ShowTimeId);
            var movie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.MoviesId == st!.MoviesId);
            var room = await _context.CinemaTheaters.AsNoTracking().FirstOrDefaultAsync(c => c.CinemaTheaterId == st!.CinemaTheaterId);

            var seatIds = tickets.Select(t => t.SeatId).ToList();
            var seatLabels = await _context.Seats.AsNoTracking()
                                .Where(s => seatIds.Contains(s.SeatId))
                                .Select(s => s.Label ?? s.SeatId)
                                .OrderBy(x => x)
                                .ToListAsync();

            decimal ticketTotal = tickets.Sum(t => Convert.ToDecimal(t.Price.GetValueOrDefault(0)));

            var snackLines = await _context.DetailBookingSnacks.AsNoTracking().Where(d => d.InvoiceId == invoiceId).ToListAsync();
            var sids = snackLines.Select(x => x.SnackId).ToList();
            var snacks = await _context.Snacks.AsNoTracking()
                .Where(s => sids.Contains(s.SnackId))
                .ToDictionaryAsync(s => s.SnackId, s => new { s.Name, Price = (s.Price ?? 0m) });

            var detail = new TicketDetailVM
            {
                InvoiceId = inv.InvoiceId,
                InvoiceEmail = inv.Email,
                InvoicePhone = inv.PhoneNumber,
                CreatedAt = inv.CreatedAt,
                PaymentMethod = !string.IsNullOrEmpty(inv.PaymentMethodId) ? "VNPAY" : (inv.PaymentMethod ?? "N/A"),
                MovieTitle = movie?.Title ?? "N/A",
                MoviePoster = movie?.PosterImage,
                CinemaTheater = room?.Name ?? "N/A",
                ShowDate = st?.ShowDate,
                StartTime = st?.StartTime,
                EndTime = st?.EndTime,
                SeatLabels = seatLabels,
                TicketCount = tickets.Count,
                TicketTotal = ticketTotal,
                SnackItems = new List<TicketSnackLineVM>()
            };

            foreach (var l in snackLines)
            {
                snacks.TryGetValue(l.SnackId, out var s);
                int qty = l.TotalSnack.GetValueOrDefault(0);
                decimal unit = s?.Price ?? 0m;
                decimal line = l.TotalPrice ?? unit * qty;

                detail.SnackItems.Add(new TicketSnackLineVM
                {
                    Name = s?.Name ?? "Snack",
                    Quantity = qty,
                    UnitPrice = unit,
                    LineTotal = line
                });
            }

            detail.SnackTotal = detail.SnackItems.Sum(x => x.LineTotal);
            detail.GrandTotal = detail.TicketTotal + detail.SnackTotal;

            return detail;
        }

        // Save PaymentTransaction for history
        private async Task SavePaymentTransactionAsync(
            string invoiceId,
            string paymentMethodId,
            long amount,
            string responseCode,
            string transactionStatus,
            string providerTxnId,
            string providerOrderNo,
            string bankCode,
            string payDate,
            bool signatureValid,
            byte status = 1, // 1=Success, 2=Failed
            string? failureReason = null)
        {
            // Auto-generate PaymentTransactionId: PT00000001, PT00000002...
            var lastTxn = await _context.PaymentTransactions.AsNoTracking()
                .OrderByDescending(pt => pt.PaymentTransactionId)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (lastTxn != null && !string.IsNullOrEmpty(lastTxn.PaymentTransactionId))
            {
                var numPart = lastTxn.PaymentTransactionId.Substring(2); // Remove "PT"
                if (int.TryParse(numPart, out int num))
                {
                    nextNum = num + 1;
                }
            }

            string txnId = $"PT{nextNum:D8}";

            var transaction = new PaymentTransactions
            {
                PaymentTransactionId = txnId,
                InvoiceId = invoiceId,
                PaymentMethodId = paymentMethodId,
                Amount = amount,
                Currency = "VND",
                Status = status,
                ProviderTxnId = providerTxnId,
                ProviderOrderNo = providerOrderNo,
                Description = $"VNPay - Bank: {bankCode}, Response: {responseCode}, Status: {transactionStatus}",
                FailureReason = failureReason,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PaidAt = status == 1 ? DateTime.UtcNow : null,
                RefundedAt = null
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();
        }
    }

    /* ===================== DTOs & ViewModels ===================== */

    public class PayRequest
    {
        public string OrderId { get; set; } = default!;
        public decimal Amount { get; set; }
        public string? OrderInfo { get; set; }
    }

    public class PendingSelection
    {
        public string ShowTimeId { get; set; } = default!;
        public List<string> SeatIds { get; set; } = new();
        public List<SnackRequest> Snacks { get; set; } = new();
    }

    public class SnackRequest
    {
        public string SnackId { get; set; } = default!;
        public int Quantity { get; set; }
    }

    public class PaymentResultVM
    {
        public string? OrderId { get; set; }
        public bool IsValidSignature { get; set; }
        public string? VnpResponseCode { get; set; }
        public string? VnpTransactionStatus { get; set; }
        public string? BankCode { get; set; }
        public int Amount { get; set; }
        public string? PayDateRaw { get; set; }
        public string? TransactionNo { get; set; }
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }

        public TicketDetailVM? Detail { get; set; }
    }

    public class TicketDetailVM
    {
        public string InvoiceId { get; set; } = default!;
        public DateTime? CreatedAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? InvoiceEmail { get; set; }
        public string? InvoicePhone { get; set; }

        public string MovieTitle { get; set; } = default!;
        public string? MoviePoster { get; set; }
        public string CinemaTheater { get; set; } = default!;
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public List<string> SeatLabels { get; set; } = new();
        public int TicketCount { get; set; }
        public decimal TicketTotal { get; set; }
        public List<TicketSnackLineVM> SnackItems { get; set; } = new();
        public decimal SnackTotal { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class TicketSnackLineVM
    {
        public string Name { get; set; } = default!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class InvoiceHistoryVM
    {
        public string InvoiceId { get; set; } = default!;
        public DateTime? CreatedAt { get; set; }
        public int Status { get; set; }
        public decimal TotalPrice { get; set; }
        public string? MovieTitle { get; set; }
        public string? Room { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
    }
}
