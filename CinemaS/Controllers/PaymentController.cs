using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using CinemaS.Services;
using CinemaS.VNPAY;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CinemaS.Controllers
{
    [Route("[controller]")]
    public class PaymentController : Controller
    {
        private readonly IConfiguration _cfg;
        private readonly CinemaContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IQrTicketService _qrService;

        public PaymentController(
            IConfiguration cfg,
            CinemaContext context,
            IEmailSender emailSender,
            IQrTicketService qrService)
        {
            _cfg = cfg;
            _context = context;
            _emailSender = emailSender;
            _qrService = qrService;
        }

        /* ===================== Promotion ===================== */

        [HttpPost("ApplyPromotion")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ApplyPromotion([FromBody] ApplyPromotionRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.InvoiceId) || string.IsNullOrWhiteSpace(req.Code))
                return Json(new { success = false, message = "Thiếu mã hóa đơn hoặc mã khuyến mãi." });

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId);
            if (invoice == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });

            if (invoice.Status == (byte)1)
                return Json(new { success = false, message = "Hóa đơn đã thanh toán." });

            if (!User.IsInRole("Admin"))
            {
                var currentUser = await GetCurrentUserEntityAsync();
                if (currentUser == null || invoice.CustomerId != currentUser.UserId)
                    return Json(new { success = false, message = "Không có quyền áp dụng mã cho hóa đơn này." });
            }

            var nowVn = NowVn();
            var normalized = NormalizePromoCode(req.Code);

            var promo = await _context.Promotion
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code != null && p.Code.ToLower() == normalized);

            if (promo == null)
                return Json(new { success = false, message = "Mã khuyến mãi không tồn tại." });

            var validation = ValidatePromotion(promo, nowVn);
            if (!validation.Success)
                return Json(new { success = false, message = validation.ErrorMessage });

            // 1) Tính tổng gốc từ line items (hoặc dùng TotalPrice hiện tại nếu chưa có line items và CHƯA áp promo)
            var baseTotal = await GetInvoiceBaseTotalForPricingAsync(invoice.InvoiceId, invoice.TotalPrice, invoice.PromotionId);
            if (!baseTotal.ok)
                return Json(new { success = false, message = baseTotal.error });

            // 2) Tính giá sau giảm
            var (_, percent) = NormalizeDiscountPercent(promo.Discount!.Value);
            decimal discountAmount = Math.Round(baseTotal.total * (decimal)percent / 100m, 0, MidpointRounding.AwayFromZero);
            decimal payable = baseTotal.total - discountAmount;
            if (payable < 0) payable = 0;

            // 3) Ghi DB: có PromotionId thì TotalPrice = giá sau giảm
            invoice.PromotionId = promo.PromotionId;
            invoice.TotalPrice = payable;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Áp dụng mã khuyến mãi thành công.",
                promotionId = promo.PromotionId,
                code = promo.Code,
                discountPercent = percent,
                originalAmount = baseTotal.total,
                discountAmount = discountAmount,
                payableAmount = ToVndLong(payable)
            });
        }

        [HttpPost("RemovePromotion")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RemovePromotion([FromBody] RemovePromotionRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.InvoiceId))
                return Json(new { success = false, message = "Thiếu mã hóa đơn." });

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId);
            if (invoice == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });

            if (invoice.Status == (byte)1)
                return Json(new { success = false, message = "Hóa đơn đã thanh toán." });

            if (!User.IsInRole("Admin"))
            {
                var currentUser = await GetCurrentUserEntityAsync();
                if (currentUser == null || invoice.CustomerId != currentUser.UserId)
                    return Json(new { success = false, message = "Không có quyền thao tác hóa đơn này." });
            }

            // Tính lại tổng gốc để phục hồi TotalPrice về giá chưa giảm
            var baseTotal = await GetInvoiceBaseTotalForPricingAsync(invoice.InvoiceId, invoice.TotalPrice, invoice.PromotionId);
            if (!baseTotal.ok)
                return Json(new { success = false, message = baseTotal.error });

            invoice.PromotionId = null;
            invoice.TotalPrice = baseTotal.total; // trả về giá gốc
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã xóa mã khuyến mãi.",
                originalAmount = baseTotal.total,
                discountAmount = 0,
                payableAmount = ToVndLong(baseTotal.total)
            });
        }


        /* ===================== 1) Tạo URL VNPay ===================== */

        [HttpPost("Create")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Create([FromBody] PayRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.OrderId))
                return Json(new { success = false, message = "Dữ liệu tạo thanh toán không hợp lệ." });

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == req.OrderId);
            if (invoice == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });

            if (!User.IsInRole("Admin"))
            {
                var currentUser = await GetCurrentUserEntityAsync();
                if (currentUser == null || invoice.CustomerId != currentUser.UserId)
                    return Json(new { success = false, message = "Không có quyền thanh toán hóa đơn này." });
            }

            var calc = await CalcPayableAsync(invoice);
            if (!calc.Success)
                return Json(new { success = false, message = calc.ErrorMessage });

            var amountToPay = calc.PayableAmountVnd;
            if (amountToPay <= 0)
                return Json(new { success = false, message = "Số tiền thanh toán không hợp lệ." });

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

            vnp.AddRequestData("vnp_Amount", (amountToPay * 100).ToString());
            vnp.AddRequestData("vnp_CurrCode", currCode);
            vnp.AddRequestData("vnp_TxnRef", invoice.InvoiceId);
            vnp.AddRequestData("vnp_OrderInfo", string.IsNullOrWhiteSpace(req.OrderInfo) ? $"Thanh toan {invoice.InvoiceId}" : req.OrderInfo);
            vnp.AddRequestData("vnp_OrderType", orderType);
            vnp.AddRequestData("vnp_Locale", locale);
            vnp.AddRequestData("vnp_ReturnUrl", returnUrl);
            vnp.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnp.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
            vnp.AddRequestData("vnp_ExpireDate", expire.ToString("yyyyMMddHHmmss"));

            var url = vnp.CreateRequestUrl(baseUrl, hashSecret);
            return Json(new { success = true, paymentUrl = url });
        }

        /* ===================== 2) VNPay redirect về ===================== */

        [HttpGet("ReturnVnPay")]
        [AllowAnonymous]
        public async Task<IActionResult> ReturnVnPay()
        {
            var vnpSection = _cfg.GetSection("VnPay");
            string secret = vnpSection["HashSecret"] ?? "";

            var vnp = new VnPayLibrary();
            foreach (var (k, v) in Request.Query)
                vnp.AddResponseData(k, v!);

            string invoiceId = vnp.GetResponseData("vnp_TxnRef") ?? "";
            string respCode = vnp.GetResponseData("vnp_ResponseCode");
            string txnStatus = vnp.GetResponseData("vnp_TransactionStatus");
            string secureHash = vnp.GetResponseData("vnp_SecureHash");
            string providerTxnId = vnp.GetResponseData("vnp_TransactionNo") ?? "";
            string bankCode = vnp.GetResponseData("vnp_BankCode") ?? "";
            string payDate = vnp.GetResponseData("vnp_PayDate") ?? "";
            long vnpAmountRaw = long.TryParse(vnp.GetResponseData("vnp_Amount"), out var amt) ? amt : 0;

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

            var paymentMethod = await _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "VNPAY");

            await SavePaymentTransactionAsync(
                invoiceId: invoiceId,
                paymentMethodId: paymentMethod?.PaymentMethodId ?? "PM001",
                amount: vnpAmountRaw / 100,
                responseCode: respCode,
                transactionStatus: txnStatus,
                providerTxnId: providerTxnId,
                providerOrderNo: invoiceId,
                bankCode: bankCode,
                payDate: payDate,
                signatureValid: signatureOk,
                status: (signatureOk && respCode == "00" && txnStatus == "00") ? (byte)1 : (byte)2,
                failureReason: (signatureOk && respCode == "00" && txnStatus == "00") ? null : $"Response: {respCode}, Status: {txnStatus}"
            );

            if (signatureOk && respCode == "00" && txnStatus == "00" && invoice != null)
            {
                using var tx = await _context.Database.BeginTransactionAsync();

                invoice.Status = (byte)1;
                invoice.UpdatedAt = DateTime.UtcNow;
                if (paymentMethod != null)
                    invoice.PaymentMethodId = paymentMethod.PaymentMethodId;

                bool alreadyHasTickets = await _context.Tickets.AsNoTracking()
                    .AnyAsync(t => t.InvoiceId == invoice.InvoiceId);

                decimal ticketSum = 0m;
                decimal snackSum = 0m;

                /* ==== ƯU TIÊN HÓA ĐƠN ĐỒ ĂN RIÊNG ==== */
                var snacksRawOnly = HttpContext.Session.GetString($"pending_snacks:{invoice.InvoiceId}");
                if (!string.IsNullOrWhiteSpace(snacksRawOnly))
                {
                    var snacksPayload = JsonSerializer.Deserialize<PendingSnacksSelection>(snacksRawOnly) ?? new PendingSnacksSelection();
                    if (snacksPayload.Snacks?.Any() == true)
                    {
                        var sids = snacksPayload.Snacks.Select(x => x.SnackId).Distinct().ToList();
                        var snackMap = await _context.Snacks.AsNoTracking()
                            .Where(s => sids.Contains(s.SnackId))
                            .ToDictionaryAsync(s => s.SnackId, s => new { Unit = (s.Price ?? 0m) });

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

                        foreach (var line in snacksPayload.Snacks)
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

                        invoice.TotalTicket = 0;
                    }
                }
                /* ==== NGƯỢC LẠI: ĐƠN VÉ (CÓ THỂ KÈM ĐỒ ĂN) ==== */
                else if (!alreadyHasTickets)
                {
                    var raw = HttpContext.Session.GetString($"pending:{invoice.InvoiceId}");
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var payload = JsonSerializer.Deserialize<PendingSelection>(raw) ?? new PendingSelection();

                        var st = await _context.ShowTimes.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.ShowTimeId == payload.ShowTimeId);

                        if (st != null)
                        {
                            var requestSeatIds = payload.SeatIds?
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim())
                                .Distinct()
                                .ToList() ?? new List<string>();

                            if (requestSeatIds.Count > 0)
                            {
                                var occupied = await _context.Tickets.AsNoTracking()
                                    .Where(t => t.ShowTimeId == st.ShowTimeId && requestSeatIds.Contains(t.SeatId))
                                    .Select(t => t.SeatId)
                                    .ToListAsync();

                                var occupiedSet = new HashSet<string>(occupied);
                                var finalSeats = requestSeatIds.Where(id => !occupiedSet.Contains(id)).ToList();

                                var seats = await _context.Seats.AsNoTracking()
                                    .Where(s => finalSeats.Contains(s.SeatId))
                                    .ToListAsync();

                                var seatTypes = await _context.SeatTypes.AsNoTracking().ToListAsync();

                                // ✅ Get price adjustment percentage from showtime
                                decimal priceAdjustmentPercent = st.PriceAdjustmentPercent ?? 0m;

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

                                foreach (var sid in finalSeats)
                                {
                                    var seat = seats.FirstOrDefault(x => x.SeatId == sid);
                                    var stype = seatTypes.FirstOrDefault(x => x.SeatTypeId == seat?.SeatTypeId);

                                    string ticketTypeId = "TT001";
                                    if (stype != null && !string.IsNullOrEmpty(stype.SeatTypeId) && stype.SeatTypeId.Length >= 5)
                                        ticketTypeId = "TT" + stype.SeatTypeId.Substring(2);

                                    // ✅ Apply price adjustment to ticket price
                                    decimal basePrice = stype?.Price ?? 0m;
                                    decimal adjustedPrice = basePrice * (1 + priceAdjustmentPercent / 100m);
                                    adjustedPrice = Math.Round(adjustedPrice, 0, MidpointRounding.AwayFromZero);
                                    
                                    ticketSum += adjustedPrice;

                                    var newTicketId = await NextTicketIdSafeAsync();

                                    _context.Tickets.Add(new Tickets
                                    {
                                        TicketId = newTicketId,
                                        InvoiceId = invoice.InvoiceId,
                                        TicketTypeId = ticketTypeId,
                                        ShowTimeId = st.ShowTimeId,
                                        SeatId = sid,
                                        Status = (byte)2,
                                        CreatedBooking = DateTime.UtcNow,
                                        Expire = null,
                                        Price = (int?)Convert.ToInt32(adjustedPrice) // ✅ Save adjusted price
                                    });
                                }

                                if (payload.Snacks?.Any() == true)
                                {
                                    var sids = payload.Snacks.Select(x => x.SnackId).Distinct().ToList();
                                    var snackMap = await _context.Snacks.AsNoTracking()
                                        .Where(s => sids.Contains(s.SnackId))
                                        .ToDictionaryAsync(s => s.SnackId, s => new { Unit = (s.Price ?? 0m) });

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
                                }

                                invoice.TotalTicket = finalSeats.Count;
                            }
                        }
                    }
                }

                try
                {
                    // Set base total then apply promotion consistently
                    var baseTotal = ticketSum + snackSum;
                    if (baseTotal <= 0m && invoice.TotalPrice.HasValue && invoice.TotalPrice.Value > 0m)
                        baseTotal = invoice.TotalPrice.Value;

                    var calcAfter = await CalcPayableAsync(invoice);
                    if (!calcAfter.Success)
                    {
                        await tx.RollbackAsync();
                        vm.IsSuccess = false;
                        vm.Message = calcAfter.ErrorMessage ?? "Lỗi tính tiền.";
                        return View("Result", vm);
                    }

                    // ✅ DB lưu giá sau giảm nếu có promo
                    invoice.TotalPrice = (decimal)calcAfter.PayableAmountVnd;

                    // ✅ FIX: ĐỔ tickets/snacks/invoice trước để AwardPoints query DB ra đúng
                    await _context.SaveChangesAsync();

                    // ✅ Sau khi DB đã có line-items mới tính/ghi PointHistories
                    await AwardPointsForInvoiceAsync(invoice);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    vm.IsSuccess = true;
                    vm.Message = "Thanh toán thành công.";
                    vm.Detail = await BuildTicketDetailAsync(invoice.InvoiceId);

                    HttpContext.Session.Remove($"pending:{invoice.InvoiceId}");
                    HttpContext.Session.Remove($"pending_snacks:{invoice.InvoiceId}");

                    if (vm.Detail != null)
                        await SendTicketEmailAsync(invoice, vm.Detail);
                }
                catch (DbUpdateException)
                {
                    await tx.RollbackAsync();

                    using var tx2 = await _context.Database.BeginTransactionAsync();

                    var tickets = await _context.Tickets
                        .Where(t => t.InvoiceId == invoice.InvoiceId)
                        .ToListAsync();

                    foreach (var t in tickets.ToList())
                    {
                        bool duplicateSeat = await _context.Tickets.AsNoTracking()
                            .AnyAsync(x => x.ShowTimeId == t.ShowTimeId && x.SeatId == t.SeatId && x.InvoiceId != t.InvoiceId);
                        if (duplicateSeat)
                            _context.Tickets.Remove(t);
                    }

                    await _context.SaveChangesAsync();

                    var recomputedBase = await GetInvoiceBaseTotalAsync(invoice.InvoiceId, invoice.TotalPrice);
                    invoice.TotalPrice = recomputedBase;

                    // cập nhật lại thời gian
                    invoice.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    // ✅ FIX: vẫn phải cộng điểm + tạo PointHistories
                    await AwardPointsForInvoiceAsync(invoice);

                    await _context.SaveChangesAsync();
                    await tx2.CommitAsync();

                    vm.IsSuccess = true;
                    vm.Message = "Thanh toán thành công (một số ghế đã được đặt trước đó và đã được loại bỏ).";
                    vm.Detail = await BuildTicketDetailAsync(invoice.InvoiceId);

                    HttpContext.Session.Remove($"pending:{invoice.InvoiceId}");
                    HttpContext.Session.Remove($"pending_snacks:{invoice.InvoiceId}");

                    if (vm.Detail != null)
                        await SendTicketEmailAsync(invoice, vm.Detail);
                }
            }
            else
            {
                if (invoice != null)
                    vm.Detail = await BuildTicketDetailAsync(invoice.InvoiceId);
            }

            if (invoice != null)
            {
                bool hasTickets = await _context.Tickets.AsNoTracking()
                    .AnyAsync(t => t.InvoiceId == invoice.InvoiceId);
                bool hasSnacks = await _context.DetailBookingSnacks.AsNoTracking()
                    .AnyAsync(d => d.InvoiceId == invoice.InvoiceId);

                if (!hasTickets && hasSnacks)
                    return RedirectToAction(nameof(SnackResult), new { invoiceId = invoice.InvoiceId });
            }
            if (vm.Detail != null)
            {
                var di = await GetDiscountInfoAsync(invoiceId, invoice?.TotalPrice);
                vm.Detail.OriginalAmount = di.original;
                vm.Detail.DiscountAmount = di.discount;
                vm.Detail.PayableAmount = di.payable;
                vm.Detail.DiscountPercent = di.percent;
                vm.Detail.PromotionName = di.promoName;
            }

            return View("Result", vm);
        }

        /* ===================== 3) Xem lại kết quả theo InvoiceId ===================== */

        [HttpGet("Result/{invoiceId}")]
        [Authorize]
        public async Task<IActionResult> ResultById(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return NotFound();

            var invoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return NotFound();

            bool hasTickets = await _context.Tickets.AsNoTracking()
                .AnyAsync(t => t.InvoiceId == invoice.InvoiceId);
            bool hasSnacks = await _context.DetailBookingSnacks.AsNoTracking()
                .AnyAsync(d => d.InvoiceId == invoice.InvoiceId);

            if (!hasTickets && hasSnacks)
                return RedirectToAction(nameof(SnackResult), new { invoiceId = invoice.InvoiceId });

            var paymentTxn = await _context.PaymentTransactions.AsNoTracking()
                .Where(pt => pt.InvoiceId == invoice.InvoiceId)
                .OrderByDescending(pt => pt.CreatedAt)
                .FirstOrDefaultAsync();

            var vm = new PaymentResultVM
            {
                OrderId = invoice.InvoiceId,
                IsSuccess = invoice.Status == (byte)1,
                Message = invoice.Status == (byte)1 ? "Thanh toán thành công." : "Đơn hàng chưa thanh toán.",
                Detail = await BuildTicketDetailAsync(invoice.InvoiceId)
            };

            if (paymentTxn != null)
            {
                vm.IsValidSignature = paymentTxn.Status == 1;

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

                // ✅ FIX: PaidAt (UTC) -> giờ VN rồi format yyyyMMddHHmmss
                if (paymentTxn.PaidAt.HasValue)
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    var paidAtVn = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(paymentTxn.PaidAt.Value, DateTimeKind.Utc),
                        tz
                    );
                    vm.PayDateRaw = paidAtVn.ToString("yyyyMMddHHmmss");
                }
                else
                {
                    vm.PayDateRaw = null;
                }
            }

            if (vm.Detail != null)
            {
                var di = await GetDiscountInfoAsync(invoice.InvoiceId, invoice.TotalPrice);
                vm.Detail.OriginalAmount = di.original;
                vm.Detail.DiscountAmount = di.discount;
                vm.Detail.PayableAmount = di.payable;
                vm.Detail.DiscountPercent = di.percent;
                vm.Detail.PromotionName = di.promoName;
            }

            return View("Result", vm);
        }

        private string ExtractBankCodeFromDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return "";
            var parts = description.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains("Bank:"))
                {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex + 1 < trimmed.Length)
                        return trimmed[(colonIndex + 1)..].Trim();
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
                        return part[(colonIndex + 1)..].Trim();
                }
            }
            return "";
        }

        /* ===================== Delete history (user) ===================== */

        [HttpPost("DeleteHistory")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHistory(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
            {
                TempData["Error"] = "Mã hóa đơn không hợp lệ.";
                return RedirectToAction(nameof(History));
            }

            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Không xác định được tài khoản.";
                return RedirectToAction(nameof(History));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(History));
            }

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.CustomerId == user.UserId);

            if (invoice == null)
            {
                TempData["Error"] = "Không tìm thấy hóa đơn.";
                return RedirectToAction(nameof(History));
            }

            if (invoice.Status == 1)
            {
                TempData["Error"] = "Không thể xóa hóa đơn đã thanh toán thành công.";
                return RedirectToAction(nameof(History));
            }

            bool hasValidMovie =
                await (from t in _context.Tickets
                       join st in _context.ShowTimes on t.ShowTimeId equals st.ShowTimeId
                       join m in _context.Movies on st.MoviesId equals m.MoviesId
                       where t.InvoiceId == invoice.InvoiceId
                       select m.MoviesId).AnyAsync();

            if (hasValidMovie)
            {
                TempData["Error"] = "Chỉ có thể xóa những hóa đơn không có thông tin phim (N/A).";
                return RedirectToAction(nameof(History));
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var tickets = await _context.Tickets.Where(t => t.InvoiceId == invoice.InvoiceId).ToListAsync();
                if (tickets.Any()) _context.Tickets.RemoveRange(tickets);

                var snackDetails = await _context.DetailBookingSnacks.Where(d => d.InvoiceId == invoice.InvoiceId).ToListAsync();
                if (snackDetails.Any()) _context.DetailBookingSnacks.RemoveRange(snackDetails);

                var paymentTxns = await _context.PaymentTransactions.Where(p => p.InvoiceId == invoice.InvoiceId).ToListAsync();
                if (paymentTxns.Any()) _context.PaymentTransactions.RemoveRange(paymentTxns);

                _context.Invoices.Remove(invoice);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Message"] = "Đã xóa hóa đơn khỏi lịch sử.";
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Xóa hóa đơn không thành công.";
            }

            return RedirectToAction(nameof(History));
        }

        /* ===================== 4) Lịch sử hoá đơn ===================== */

        [HttpGet("History")]
        [Authorize]
        public async Task<IActionResult> History(int page = 1)
        {
            const int PageSize = 8;
            if (page < 1) page = 1;

            var email = User.Identity?.Name;
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ViewBag.Page = 1;
                ViewBag.TotalPages = 1;
                ViewBag.TotalItems = 0;
                ViewBag.PageSize = PageSize;
                return View("History", new List<InvoiceHistoryVM>());
            }

            // Base query (đúng logic cũ: bỏ snack-only)
            var baseQuery = _context.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == user.UserId)
                .OrderByDescending(i => i.CreatedAt);

            var totalItems = await baseQuery.CountAsync();

            var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var invoicesPage = await baseQuery
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var vm = new List<InvoiceHistoryVM>();

            foreach (var inv in invoicesPage)
            {
                var anyTicket = await _context.Tickets.AsNoTracking()
                    .Where(t => t.InvoiceId == inv.InvoiceId)
                    .OrderBy(t => t.TicketId)
                    .FirstOrDefaultAsync();

                var hasSnacks = await _context.DetailBookingSnacks.AsNoTracking()
                    .AnyAsync(d => d.InvoiceId == inv.InvoiceId);

                // giữ logic cũ: bỏ qua hoá đơn snack-only ở trang History vé
                if (anyTicket == null && hasSnacks && (inv.TotalTicket ?? 0) == 0)
                    continue;

                string? movie = null, room = null;
                DateTime? showDate = null, start = null;

                if (anyTicket != null)
                {
                    var st = await _context.ShowTimes.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.ShowTimeId == anyTicket.ShowTimeId);

                    if (st != null)
                    {
                        showDate = st.ShowDate;
                        start = st.StartTime;

                        var mv = await _context.Movies.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);

                        var ct = await _context.CinemaTheaters.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CinemaTheaterId == st.CinemaTheaterId);

                        movie = mv?.Title;
                        room = ct?.Name;
                    }
                }

                var di = await GetDiscountInfoAsync(inv.InvoiceId, inv.TotalPrice);
                var payable = di.payable;

                vm.Add(new InvoiceHistoryVM
                {
                    InvoiceId = inv.InvoiceId,
                    CreatedAt = inv.CreatedAt,
                    Status = inv.Status.GetValueOrDefault(0),
                    TotalPrice = payable,
                    MovieTitle = movie,
                    Room = room,
                    ShowDate = showDate,
                    StartTime = start
                });
            }

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = PageSize;

            return View("History", vm);
        }



        /* ===================== 5) Thanh toán tiền mặt (Admin) ===================== */

        [HttpPost("PayByCash")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PayByCash([FromBody] CashPayRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.InvoiceId))
                return Json(new { success = false, message = "Thiếu mã hóa đơn." });

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId);
            if (invoice == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });

            if (invoice.Status == (byte)1)
            {
                var existedUrl = Url.Action(nameof(ResultById), "Payment", new { invoiceId = invoice.InvoiceId })!;
                return Json(new { success = true, alreadyPaid = true, redirectUrl = existedUrl });
            }

            var cashMethod = await _context.PaymentMethods.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "CASH");

            if (cashMethod == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Chưa có phương thức thanh toán, vui lòng cấu hình trong bảng PaymentMethods."
                });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                invoice.Status = (byte)1;
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.PaymentMethodId = cashMethod.PaymentMethodId;

                bool alreadyHasTickets = await _context.Tickets.AsNoTracking()
                    .AnyAsync(t => t.InvoiceId == invoice.InvoiceId);

                decimal ticketSum = 0m;
                decimal snackSum = 0m;

                var snacksRawOnly = HttpContext.Session.GetString($"pending_snacks:{invoice.InvoiceId}");
                if (!string.IsNullOrWhiteSpace(snacksRawOnly))
                {
                    var snacksPayload = JsonSerializer.Deserialize<PendingSnacksSelection>(snacksRawOnly) ?? new PendingSnacksSelection();
                    if (snacksPayload.Snacks?.Any() == true)
                    {
                        var sids = snacksPayload.Snacks.Select(x => x.SnackId).Distinct().ToList();
                        var snackMap = await _context.Snacks.AsNoTracking()
                            .Where(s => sids.Contains(s.SnackId))
                            .ToDictionaryAsync(s => s.SnackId, s => new { Unit = (s.Price ?? 0m) });

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

                        foreach (var line in snacksPayload.Snacks)
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

                        invoice.TotalTicket = 0;
                    }
                }
                else if (!alreadyHasTickets)
                {
                    var raw = HttpContext.Session.GetString($"pending:{invoice.InvoiceId}");
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var payload = JsonSerializer.Deserialize<PendingSelection>(raw) ?? new PendingSelection();

                        var st = await _context.ShowTimes.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.ShowTimeId == payload.ShowTimeId);

                        if (st != null)
                        {
                            var requestSeatIds = payload.SeatIds?
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim())
                                .Distinct()
                                .ToList() ?? new List<string>();

                            if (requestSeatIds.Count > 0)
                            {
                                var occupied = await _context.Tickets.AsNoTracking()
                                    .Where(t => t.ShowTimeId == st.ShowTimeId && requestSeatIds.Contains(t.SeatId))
                                    .Select(t => t.SeatId)
                                    .ToListAsync();

                                var occupiedSet = new HashSet<string>(occupied);
                                var finalSeats = requestSeatIds.Where(id => !occupiedSet.Contains(id)).ToList();

                                var seats = await _context.Seats.AsNoTracking()
                                    .Where(s => finalSeats.Contains(s.SeatId))
                                    .ToListAsync();

                                var seatTypes = await _context.SeatTypes.AsNoTracking().ToListAsync();

                                // ✅ Get price adjustment percentage from showtime
                                decimal priceAdjustmentPercent = st.PriceAdjustmentPercent ?? 0m;

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

                                foreach (var sid in finalSeats)
                                {
                                    var seat = seats.FirstOrDefault(x => x.SeatId == sid);
                                    var stype = seatTypes.FirstOrDefault(x => x.SeatTypeId == seat?.SeatTypeId);

                                    string ticketTypeId = "TT001";
                                    if (stype != null && !string.IsNullOrEmpty(stype.SeatTypeId) && stype.SeatTypeId.Length >= 5)
                                        ticketTypeId = "TT" + stype.SeatTypeId.Substring(2);

                                    // ✅ Apply price adjustment to ticket price
                                    decimal basePrice = stype?.Price ?? 0m;
                                    decimal adjustedPrice = basePrice * (1 + priceAdjustmentPercent / 100m);
                                    adjustedPrice = Math.Round(adjustedPrice, 0, MidpointRounding.AwayFromZero);
                                    
                                    ticketSum += adjustedPrice;

                                    var newTicketId = await NextTicketIdSafeAsync();

                                    _context.Tickets.Add(new Tickets
                                    {
                                        TicketId = newTicketId,
                                        InvoiceId = invoice.InvoiceId,
                                        TicketTypeId = ticketTypeId,
                                        ShowTimeId = st.ShowTimeId,
                                        SeatId = sid,
                                        Status = (byte)2,
                                        CreatedBooking = DateTime.UtcNow,
                                        Expire = null,
                                        Price = (int?)Convert.ToInt32(adjustedPrice) // ✅ Save adjusted price
                                    });
                                }

                                if (payload.Snacks?.Any() == true)
                                {
                                    var sids = payload.Snacks.Select(x => x.SnackId).Distinct().ToList();
                                    var snackMap = await _context.Snacks.AsNoTracking()
                                        .Where(s => sids.Contains(s.SnackId))
                                        .ToDictionaryAsync(s => s.SnackId, s => new { Unit = (s.Price ?? 0m) });

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
                                }

                                invoice.TotalTicket = finalSeats.Count;
                            }
                        }
                    }
                }

                var baseTotal = ticketSum + snackSum;
                if (baseTotal <= 0m && invoice.TotalPrice.HasValue && invoice.TotalPrice.Value > 0m)
                    baseTotal = invoice.TotalPrice.Value;


                var calcPay = await CalcPayableAsync(invoice);
                if (!calcPay.Success)
                {
                    await tx.RollbackAsync();
                    return Json(new { success = false, message = calcPay.ErrorMessage });
                }

                // ✅ DB lưu giá sau giảm nếu có promo
                invoice.TotalPrice = (decimal)calcPay.PayableAmountVnd;



                long amountLong = calcPay.PayableAmountVnd;

                var lastTxn = await _context.PaymentTransactions.AsNoTracking()
                    .OrderByDescending(pt => pt.PaymentTransactionId)
                    .FirstOrDefaultAsync();

                int nextTxnNum = 1;
                if (lastTxn != null && !string.IsNullOrEmpty(lastTxn.PaymentTransactionId))
                {
                    var numPart = lastTxn.PaymentTransactionId.Substring(2);
                    if (int.TryParse(numPart, out int num))
                        nextTxnNum = num + 1;
                }

                string txnId = $"PT{nextTxnNum:D8}";

                var cashTxn = new PaymentTransactions
                {
                    PaymentTransactionId = txnId,
                    InvoiceId = invoice.InvoiceId,
                    PaymentMethodId = cashMethod.PaymentMethodId,
                    Amount = amountLong,
                    Currency = "VND",
                    Status = 1,
                    ProviderTxnId = $"CASH_{invoice.InvoiceId}",
                    ProviderOrderNo = invoice.InvoiceId,
                    Description = "CASH - Offline payment",
                    FailureReason = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PaidAt = DateTime.UtcNow,
                    RefundedAt = null
                };

                _context.PaymentTransactions.Add(cashTxn);
                await _context.SaveChangesAsync();
                await AwardPointsForInvoiceAsync(invoice);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                HttpContext.Session.Remove($"pending:{invoice.InvoiceId}");
                HttpContext.Session.Remove($"pending_snacks:{invoice.InvoiceId}");

                bool hasTickets = await _context.Tickets.AsNoTracking()
                    .AnyAsync(t => t.InvoiceId == invoice.InvoiceId);
                bool hasSnacks = await _context.DetailBookingSnacks.AsNoTracking()
                    .AnyAsync(d => d.InvoiceId == invoice.InvoiceId);

                var url = (!hasTickets && hasSnacks)
                    ? Url.Action(nameof(SnackResult), "Payment", new { invoiceId = invoice.InvoiceId })!
                    : Url.Action(nameof(ResultById), "Payment", new { invoiceId = invoice.InvoiceId })!;

                return Json(new { success = true, redirectUrl = url });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var inner = ex.InnerException?.Message;
                return Json(new { success = false, message = "Thanh toán tiền mặt lỗi: " + (inner ?? ex.Message) });
            }
        }

        /* ===================== 6) Snack result / history ===================== */

        [HttpGet("SnackResult")]
        [Authorize]
        public async Task<IActionResult> SnackResult(string invoiceId)
        {

            if (string.IsNullOrWhiteSpace(invoiceId))
                return RedirectToAction("SnackHistory");

            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return RedirectToAction("SnackHistory");

            var details = await _context.DetailBookingSnacks.AsNoTracking()
    .Where(d => d.InvoiceId == invoiceId)
    .Join(_context.Snacks.AsNoTracking(),
        d => d.SnackId,
        s => s.SnackId,
        (d, s) => new
        {
            s.SnackId,
            s.Name,
            s.Image, // ✅ lấy ảnh từ bảng Snacks
            Quantity = d.TotalSnack ?? 0,
            LineTotal = d.TotalPrice ?? ((s.Price ?? 0m) * (d.TotalSnack ?? 0)) // ✅ ưu tiên TotalPrice đã lưu
        })
    .ToListAsync();

            decimal baseTotal = details.Sum(x => x.LineTotal);

            var di = await GetDiscountInfoAsync(invoice.InvoiceId, baseTotal);

            var vm = new SnackPaymentResultVM
            {
                OrderId = invoice.InvoiceId,
                IsSuccess = invoice.Status == 1,

                Detail = new SnackInvoiceDetailVM
                {
                    PaymentMethod = await GetPaymentMethodTextAsync(invoice),
                    InvoiceId = invoice.InvoiceId,
                    CreatedAt = invoice.CreatedAt,
                    InvoiceEmail = invoice.Email,
                    InvoicePhone = invoice.PhoneNumber,

                    SnackItems = details.Select(x => new SnackItemVM
                    {
                        SnackId = x.SnackId,     // nếu VM có
                        Name = x.Name,
                        Quantity = x.Quantity,
                        LineTotal = x.LineTotal,
                        Image = x.Image          // ✅ đẩy ảnh xuống VM
                    }).ToList(),

                    OriginalAmount = di.original,
                    DiscountAmount = di.discount,
                    PayableAmount = di.payable,
                    PromotionName = di.promoName,
                    DiscountPercent = di.percent,
                }
            };

            return View(vm);
        }

        [HttpGet("SnackHistory")]
        [Authorize]
        public async Task<IActionResult> SnackHistory(int page = 1)
        {
            const int PageSize = 8;
            if (page < 1) page = 1;

            var email = User.Identity?.Name;
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ViewBag.Page = 1;
                ViewBag.TotalPages = 1;
                ViewBag.TotalItems = 0;
                ViewBag.PageSize = PageSize;
                return View(new List<SnackInvoiceHistoryVM>());
            }

            var baseQuery = _context.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == user.UserId && (i.TotalTicket ?? 0) == 0)
                .OrderByDescending(i => i.CreatedAt);

            var totalItems = await baseQuery.CountAsync();

            var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var invoicesPage = await baseQuery
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var vm = new List<SnackInvoiceHistoryVM>();

            foreach (var inv in invoicesPage)
            {
                var hasSnack = await _context.DetailBookingSnacks.AsNoTracking()
                    .AnyAsync(d => d.InvoiceId == inv.InvoiceId);

                if (!hasSnack) continue;

                var di = await GetDiscountInfoAsync(inv.InvoiceId, inv.TotalPrice);
                var payable = di.payable;

                vm.Add(new SnackInvoiceHistoryVM
                {
                    InvoiceId = inv.InvoiceId,
                    CreatedAt = inv.CreatedAt,
                    Status = inv.Status.GetValueOrDefault(0),
                    TotalPrice = payable
                });
            }

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = PageSize;

            return View(vm);
        }



        /* ===================== Helpers ===================== */
        private async Task<(bool ok, decimal total, string? error)> GetInvoiceBaseTotalForPricingAsync(
    string invoiceId,
    decimal? fallbackTotalPrice,
    string? promotionId
)
        {
            // 1) Nếu đã lưu OriginalTotal thì dùng luôn
            var inv = await _context.Invoices.AsNoTracking()
                .Where(i => i.InvoiceId == invoiceId)
                .Select(i => new { i.OriginalTotal, i.TotalPrice, i.PromotionId })
                .FirstOrDefaultAsync();

            if (inv != null && inv.OriginalTotal.HasValue && inv.OriginalTotal.Value > 0)
                return (true, inv.OriginalTotal.Value, null);

            // 2) Sum line-items
            decimal ticketSum = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .SumAsync(t => (decimal)(t.Price ?? 0));

            decimal snackSum = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(s => s.InvoiceId == invoiceId)
                .SumAsync(s => (decimal)(s.TotalPrice ?? 0));

            var sum = ticketSum + snackSum;
            if (sum > 0) return (true, sum, null);

            // 3) Fallback
            if (fallbackTotalPrice.HasValue && fallbackTotalPrice.Value > 0)
                return (true, fallbackTotalPrice.Value, null);

            return (false, 0m, "Không tính được tổng gốc của hóa đơn.");
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


        private sealed record PromotionValidationResult(bool Success, string? ErrorMessage);

        private sealed record PayableCalcResult(
            bool Success,
            string? ErrorMessage,
            decimal OriginalAmount,
            decimal DiscountAmount,
            double? DiscountPercent,
            long PayableAmountVnd
        );

        private DateTime NowVn()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        private static string NormalizePromoCode(string code)
            => (code ?? "").Trim().ToLower();

        private static (bool ok, double percent) NormalizeDiscountPercent(double raw)
        {
            double percent = (raw > 0 && raw < 1) ? raw * 100.0 : raw;
            if (percent <= 0 || percent > 100) return (false, 0);
            return (true, percent);
        }

        private PromotionValidationResult ValidatePromotion(Promotion promo, DateTime nowVn)
        {
            if (promo.Status != true)
                return new(false, "Mã khuyến mãi đang tắt.");

            if (promo.StartDay.HasValue && promo.StartDay.Value > nowVn)
                return new(false, "Mã khuyến mãi chưa bắt đầu.");

            if (promo.EndDay.HasValue && promo.EndDay.Value < nowVn)
                return new(false, "Mã khuyến mãi đã hết hạn.");

            if (!promo.Discount.HasValue)
                return new(false, "Mã khuyến mãi thiếu giá trị giảm.");

            var (ok, _) = NormalizeDiscountPercent(promo.Discount.Value);
            if (!ok)
                return new(false, "Giá trị giảm không hợp lệ (phải trong (0..100]).");

            return new(true, null);
        }

        private static long ToVndLong(decimal money)
            => (long)Math.Round(money, 0, MidpointRounding.AwayFromZero);

        private async Task<PayableCalcResult> CalcPayableAsync(Invoices invoice)
        {
            // Lấy tổng gốc ưu tiên từ line items (Tickets + DetailBookingSnacks)
            var baseTotal = await GetInvoiceBaseTotalForPricingAsync(
                invoice.InvoiceId,
                invoice.TotalPrice,
                invoice.PromotionId
            );

            if (!baseTotal.ok)
                return new(false, baseTotal.error, 0m, 0m, null, 0);

            decimal original = baseTotal.total;

            // Không có promo => payable = original
            if (string.IsNullOrWhiteSpace(invoice.PromotionId))
                return new(true, null, original, 0m, null, ToVndLong(original));

            var promo = await _context.Promotion.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PromotionId == invoice.PromotionId);

            if (promo == null)
                return new(false, "Không tìm thấy khuyến mãi của hóa đơn.", original, 0m, null, ToVndLong(original));

            var nowVn = NowVn();
            var v = ValidatePromotion(promo, nowVn);
            if (!v.Success)
                return new(false, v.ErrorMessage, original, 0m, null, ToVndLong(original));

            var (_, percent) = NormalizeDiscountPercent(promo.Discount!.Value);

            // ✅ Tự tính payable từ original (không lấy invoice.TotalPrice làm payable nữa)
            decimal discountAmount = Math.Round(original * (decimal)percent / 100m, 0, MidpointRounding.AwayFromZero);
            decimal payable = original - discountAmount;
            if (payable < 0) payable = 0;

            return new(true, null, original, discountAmount, percent, ToVndLong(payable));
        }


        private async Task<decimal> GetInvoiceBaseTotalAsync(string invoiceId, decimal? fallbackTotalPrice)
        {
            decimal ticketSum = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .SumAsync(t => (decimal)(t.Price ?? 0));

            decimal snackSum = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(s => s.InvoiceId == invoiceId)
                .SumAsync(s => (decimal)(s.TotalPrice ?? 0));

            var total = ticketSum + snackSum;
            if (total <= 0m && fallbackTotalPrice.HasValue && fallbackTotalPrice.Value > 0m)
                total = fallbackTotalPrice.Value;

            return total;
        }

        private async Task<CinemaS.Models.Users?> GetCurrentUserEntityAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return null;

            return await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        private static int ParseVnpAmount(string? vnpAmount)
            => (long.TryParse(vnpAmount, out var raw) ? (int)(raw / 100) : 0);

        private async Task<TicketDetailVM?> BuildTicketDetailAsync(string invoiceId)
        {
            var inv = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (inv == null) return null;

            var tickets = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .ToListAsync();

            TicketDetailVM detail;

            if (tickets.Any())
            {
                var first = tickets.First();
                var st = await _context.ShowTimes.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ShowTimeId == first.ShowTimeId);
                if (st == null) return null;

                var movie = await _context.Movies.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);
                var room = await _context.CinemaTheaters.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CinemaTheaterId == st.CinemaTheaterId);

                var seatIds = tickets.Select(t => t.SeatId).ToList();
                var seatLabels = await _context.Seats.AsNoTracking()
                    .Where(s => seatIds.Contains(s.SeatId))
                    .Select(s => s.Label ?? s.SeatId)
                    .OrderBy(x => x)
                    .ToListAsync();

                decimal ticketTotal = tickets.Sum(t => Convert.ToDecimal(t.Price.GetValueOrDefault(0)));

                detail = new TicketDetailVM
                {
                    InvoiceId = inv.InvoiceId,
                    InvoiceEmail = inv.Email,
                    InvoicePhone = inv.PhoneNumber,
                    CreatedAt = inv.CreatedAt,
                    PaymentMethod = await GetPaymentMethodTextAsync(inv),
                    MovieTitle = movie?.Title ?? "N/A",
                    MoviePoster = movie?.PosterImage,
                    CinemaTheater = room?.Name ?? "N/A",
                    ShowDate = st.ShowDate,
                    StartTime = st.StartTime,
                    EndTime = st.EndTime,
                    SeatLabels = seatLabels,
                    TicketCount = tickets.Count,
                    TicketTotal = ticketTotal,
                    SnackItems = new List<TicketSnackLineVM>()
                };
            }
            else
            {
                detail = new TicketDetailVM
                {
                    InvoiceId = inv.InvoiceId,
                    InvoiceEmail = inv.Email,
                    InvoicePhone = inv.PhoneNumber,
                    CreatedAt = inv.CreatedAt,
                    PaymentMethod = await GetPaymentMethodTextAsync(inv),
                    MovieTitle = "Đơn đồ ăn",
                    MoviePoster = null,
                    CinemaTheater = "N/A",
                    ShowDate = null,
                    StartTime = null,
                    EndTime = null,
                    SeatLabels = new List<string>(),
                    TicketCount = 0,
                    TicketTotal = 0m,
                    SnackItems = new List<TicketSnackLineVM>()
                };
            }

            var snackLines = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(d => d.InvoiceId == invoiceId)
                .ToListAsync();

            var sids = snackLines.Select(x => x.SnackId).ToList();
            var snacks = await _context.Snacks.AsNoTracking()
                .Where(s => sids.Contains(s.SnackId))
                .ToDictionaryAsync(
                    s => s.SnackId,
                    s => new { s.Name, Price = (s.Price ?? 0m), s.Image });

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
                    LineTotal = line,
                    Image = s?.Image
                });
            }

            detail.SnackTotal = detail.SnackItems.Sum(x => x.LineTotal);
            detail.GrandTotal = detail.TicketTotal + detail.SnackTotal;

            // Generate QR code for the ticket
            try
            {
                detail.QrImageBase64 = await _qrService.GenerateQrImageBase64Async(invoiceId, 10);
            }
            catch
            {
                // QR generation failed - continue without QR
                detail.QrImageBase64 = null;
            }

            return detail;
        }

        private async Task<string> GetPaymentMethodTextAsync(Invoices inv)
        {
            if (!string.IsNullOrEmpty(inv.PaymentMethodId))
            {
                var method = await _context.PaymentMethods.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.PaymentMethodId == inv.PaymentMethodId);

                if (method != null)
                {
                    if (!string.IsNullOrWhiteSpace(method.Name)) return method.Name;
                    if (!string.IsNullOrWhiteSpace(method.Code)) return method.Code;
                }
            }

            return "Thanh toán";
        }

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
    byte status = 1,
    string? failureReason = null)
        {
            var lastTxn = await _context.PaymentTransactions.AsNoTracking()
                .OrderByDescending(pt => pt.PaymentTransactionId)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (lastTxn != null && !string.IsNullOrEmpty(lastTxn.PaymentTransactionId))
            {
                var numPart = lastTxn.PaymentTransactionId.Substring(2);
                if (int.TryParse(numPart, out int num))
                    nextNum = num + 1;
            }

            string txnId = $"PT{nextNum:D8}";

            // ✅ NEW: PaidAt lưu UTC nhưng lấy theo payDate (giờ VN) nếu có
            DateTime? paidAtUtc = null;
            if (status == 1 && !string.IsNullOrWhiteSpace(payDate))
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                if (DateTime.TryParseExact(
                        payDate.Trim(),
                        "yyyyMMddHHmmss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var vnPayLocal))
                {
                    paidAtUtc = TimeZoneInfo.ConvertTimeToUtc(vnPayLocal, tz);
                }
            }

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

                // ✅ FIX: dùng paidAtUtc nếu parse được, fallback UtcNow
                PaidAt = status == 1 ? (paidAtUtc ?? DateTime.UtcNow) : null,

                RefundedAt = null
            };

            _context.PaymentTransactions.Add(transaction);
        }


        /* ===================== Email helpers ===================== */

        private async Task SendTicketEmailAsync(Invoices invoice, TicketDetailVM detail)
        {
            if (detail == null) return;

            string? toEmail = detail.InvoiceEmail;

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                if (!string.IsNullOrWhiteSpace(invoice.Email))
                    toEmail = invoice.Email;
                else if (!string.IsNullOrWhiteSpace(invoice.CustomerId))
                {
                    var user = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UserId == invoice.CustomerId);

                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                        toEmail = user.Email;
                }
            }

            if (string.IsNullOrWhiteSpace(toEmail))
                return;

            var subject = $"Đã đặt vé thành công - {detail.MovieTitle}";

            string? qrBase64 = null;
            byte[]? attachmentBytes = null;
            string? attachmentName = null;

            try
            {
                qrBase64 = await _qrService.GenerateQrImageBase64Async(invoice.InvoiceId, 8);
            }
            catch
            {
                qrBase64 = null;
            }

            if (!string.IsNullOrWhiteSpace(qrBase64))
            {
                try
                {
                    attachmentBytes = Convert.FromBase64String(qrBase64);
                    attachmentName = $"QR_{invoice.InvoiceId}.png";
                }
                catch
                {
                    attachmentBytes = null;
                    attachmentName = null;
                }
            }

            string? cid = null;
            if (attachmentBytes != null && attachmentBytes.Length > 0 && !string.IsNullOrWhiteSpace(attachmentName))
            {
                cid = attachmentName.Replace(" ", "_");
            }

            var body = BuildTicketEmailBody(detail, cid);

            try
            {
                if (attachmentBytes != null && attachmentBytes.Length > 0 && _emailSender is IEmailSenderWithAttachment emailWithAttachment && !string.IsNullOrWhiteSpace(attachmentName))
                {
                    await emailWithAttachment.SendEmailWithAttachmentAsync(
                        toEmail,
                        subject,
                        body,
                        attachmentBytes,
                        attachmentName,
                        "image/png");
                }
                else if (!string.IsNullOrWhiteSpace(qrBase64))
                {
                    var bodyWithDataUri = BuildTicketEmailBody(detail, null, qrBase64);
                    await _emailSender.SendEmailAsync(toEmail, subject, bodyWithDataUri);
                }
                else
                {
                    await _emailSender.SendEmailAsync(toEmail, subject, body);
                }
            }
            catch
            {
                await _emailSender.SendEmailAsync(toEmail, subject, body);
            }
        }

        private string BuildTicketEmailBody(TicketDetailVM d, string? qrCid = null, string? qrDataUri = null)
        {
            string showDate = d.ShowDate?.ToString("dd/MM/yyyy") ?? string.Empty;
            string startTime = d.StartTime?.ToString("HH:mm") ?? string.Empty;
            string endTime = d.EndTime?.ToString("HH:mm") ?? string.Empty;

            string createdAt = d.CreatedAt.HasValue
                ? d.CreatedAt.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                : string.Empty;

            string seats = (d.SeatLabels != null && d.SeatLabels.Any())
                ? string.Join(", ", d.SeatLabels)
                : string.Empty;

            bool hasEmail = !string.IsNullOrWhiteSpace(d.InvoiceEmail);
            bool hasPhone = !string.IsNullOrWhiteSpace(d.InvoicePhone);
            bool hasPaymentMethod = !string.IsNullOrWhiteSpace(d.PaymentMethod);
            bool hasSnacks = d.SnackItems != null && d.SnackItems.Count > 0;

            var sb = new System.Text.StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8' /><title>Đã đặt vé thành công</title></head>");
            sb.Append("<body style='font-family:-apple-system,BlinkMacSystemFont,\'Segoe UI\',Roboto,Helvetica,Arial,sans-serif;font-size:14px;line-height:1.6;color:#111827;'>");
            sb.Append("<p><strong>Đã đặt vé thành công.</strong></p>");

            sb.Append("<p>Thông tin vé:</p><ul>");
            sb.Append($"<li><strong>Mã hóa đơn:</strong> {d.InvoiceId}</li>");
            sb.Append($"<li><strong>Phim:</strong> {d.MovieTitle}</li>");
            sb.Append($"<li><strong>Rạp/phòng:</strong> {d.CinemaTheater}</li>");
            sb.Append($"<li><strong>Ngày chiếu:</strong> {showDate}</li>");
            sb.Append($"<li><strong>Suất chiếu:</strong> {startTime} - {endTime}</li>");
            sb.Append($"<li><strong>Ghế:</strong> {seats}</li>");
            sb.Append($"<li><strong>Ngày đặt:</strong> {createdAt}</li>");
            if (hasEmail) sb.Append($"<li><strong>Email:</strong> {d.InvoiceEmail}</li>");
            if (hasPhone) sb.Append($"<li><strong>Số điện thoại:</strong> {d.InvoicePhone}</li>");
            if (hasPaymentMethod) sb.Append($"<li><strong>Phương thức thanh toán:</strong> {d.PaymentMethod}</li>");
            sb.Append("</ul>");

            if (hasSnacks)
            {
                sb.Append("<p><strong>Bắp nước:</strong></p><ul>");
                foreach (var s in d.SnackItems)
                    sb.Append($"<li>{s.Name} × {s.Quantity}</li>");
                sb.Append("</ul>");
            }

            sb.Append("<p>Vui lòng mang theo mã hóa đơn hoặc email này khi đến rạp để được hỗ trợ nếu cần.</p>");
            sb.Append("</body></html>");

            return sb.ToString();
        }

        // ===================== Point / Point_Histories helpers =====================

        private async Task<string> NextPointHistoryIdSafeAsync()
        {
            // ID dạng: PH00000001 (10 ký tự)
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

            // Chỉ tích điểm khi đã paid
            if (invoice.Status != (byte)1) return;

            // Load user (tracked) để cộng SavePoint
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == invoice.CustomerId);

            if (user == null) return;

            // Chặn tạo trùng lịch sử cho cùng invoice + user
            bool existedHistory = await _context.PointHistories.AsNoTracking()
                .AnyAsync(ph => ph.InvoiceId == invoice.InvoiceId && ph.UserId == user.UserId);

            if (existedHistory) return;

            var rank = await _context.Set<MembershipRank>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.MembershipRankId == user.MembershipRankId);

            int pointPerTicket = rank?.PointReturnTicket ?? 0;
            int pointPerCombo = rank?.PointReturnCombo ?? 0;

            // Số vé: ưu tiên đếm Tickets theo InvoiceId (chuẩn nhất)
            int ticketCount = await _context.Tickets.AsNoTracking()
                .CountAsync(t => t.InvoiceId == invoice.InvoiceId);

            if (ticketCount <= 0)
                ticketCount = invoice.TotalTicket ?? 0;

            // Số combo/snack: tổng quantity từ DetailBookingSnacks
            int snackQty = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(d => d.InvoiceId == invoice.InvoiceId)
                .SumAsync(d => (int)(d.TotalSnack ?? 0));

            int earned = (ticketCount * pointPerTicket) + (snackQty * pointPerCombo);
            if (earned <= 0) return;

            // Cộng điểm vào Users.SavePoint
            user.SavePoint = (user.SavePoint ?? 0) + earned;
            user.UpdatedAt = DateTime.UtcNow;

            // Tạo Point_Histories
            var newId = await NextPointHistoryIdSafeAsync();

            _context.PointHistories.Add(new PointHistories
            {
                PointHistoryId = newId,
                UserId = user.UserId,
                InvoiceId = invoice.InvoiceId,
                ChangeAmount = earned,
                Reason = $"+{earned} điểm từ hóa đơn {invoice.InvoiceId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }


    /* ===================== DTOs ===================== */

    public class CashPayRequest
    {
        public string InvoiceId { get; set; } = default!;
    }

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

    public class PendingSnacksSelection
    {
        public List<SnackRequest> Snacks { get; set; } = new();
    }

    public class SnackRequest
    {
        public string SnackId { get; set; } = default!;
        public int Quantity { get; set; }
    }

    public class ApplyPromotionRequest
    {
        public string InvoiceId { get; set; } = default!;
        public string Code { get; set; } = default!;
    }

    public class RemovePromotionRequest
    {
        public string InvoiceId { get; set; } = default!;
    }
}
