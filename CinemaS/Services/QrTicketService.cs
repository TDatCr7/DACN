using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.Models.Settings;
using CinemaS.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.IO.Font;

namespace CinemaS.Services
{
    public interface IQrTicketService
    {
        /// <summary>
        /// Generate QR content for an invoice (containing all tickets)
        /// </summary>
        Task<QrTicketContent?> GenerateQrContentAsync(string invoiceId);

        /// <summary>
        /// Generate QR code image as PNG bytes
        /// </summary>
        byte[] GenerateQrImage(string qrContent, int pixelsPerModule = 10);

        /// <summary>
        /// Generate QR code image as Base64 string
        /// </summary>
        Task<string?> GenerateQrImageBase64Async(string invoiceId, int pixelsPerModule = 10);

        /// <summary>
        /// Validate QR content and return ticket information
        /// </summary>
        Task<QrValidationResult> ValidateQrAsync(string qrContent);

        /// <summary>
        /// Generate PDF ticket with QR code
        /// </summary>
        Task<byte[]?> GenerateTicketPdfAsync(string invoiceId);

        /// <summary>
        /// Generate PDF ticket using provided QR image bytes
        /// </summary>
        Task<byte[]?> GenerateTicketPdfWithQrAsync(string invoiceId, byte[] qrImageBytes);
    }

    public class QrTicketService : IQrTicketService
    {
        private readonly CinemaContext _context;
        private readonly string _secretKey;

        public QrTicketService(CinemaContext context, IOptions<QrSettings> qrSettings)
        {
            _context = context;
            _secretKey = qrSettings.Value.SecretKey;

            if (string.IsNullOrWhiteSpace(_secretKey))
                throw new InvalidOperationException("QrSettings:SecretKey is not configured in appsettings.json");
        }

        #region QR Generation

        public async Task<QrTicketContent?> GenerateQrContentAsync(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return null;

            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return null;

            // Get first ticket to get movie info
            var firstTicket = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .FirstOrDefaultAsync();

            if (firstTicket == null)
                return null;

            // Get showtime and movie
            var showTime = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(st => st.ShowTimeId == firstTicket.ShowTimeId);

            if (showTime == null)
                return null;

            var movie = await _context.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);

            if (movie == null)
                return null;

            // Get all seat labels
            var tickets = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .ToListAsync();

            var seatIds = tickets.Select(t => t.SeatId).ToList();
            var seatLabels = await _context.Seats.AsNoTracking()
                .Where(s => seatIds.Contains(s.SeatId))
                .Select(s => s.Label ?? s.SeatId)
                .OrderBy(x => x)
                .ToListAsync();

            // Create payload for encryption
            var payload = new QrTicketPayload
            {
                TicketId = firstTicket.TicketId,
                InvoiceId = invoiceId,
                MovieId = movie.MoviesId,
                ShowTimeId = showTime.ShowTimeId
            };

            // Encrypt payload
            var cipherText = Encrypt(payload);

            return new QrTicketContent
            {
                InvoiceId = invoiceId,
                MovieName = movie.Title ?? "N/A",
                Seats = string.Join(",", seatLabels),
                CipherText = cipherText
            };
        }

        public byte[] GenerateQrImage(string qrContent, int pixelsPerModule = 10)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(pixelsPerModule);
        }

        public async Task<string?> GenerateQrImageBase64Async(string invoiceId, int pixelsPerModule = 10)
        {
            var qrContent = await GenerateQrContentAsync(invoiceId);
            if (qrContent == null)
                return null;

            var qrBytes = GenerateQrImage(qrContent.ToQrString(), pixelsPerModule);
            return Convert.ToBase64String(qrBytes);
        }

        #endregion

        #region QR Validation

        public async Task<QrValidationResult> ValidateQrAsync(string qrContent)
        {
            if (string.IsNullOrWhiteSpace(qrContent))
                return QrValidationResult.Invalid("Nội dung QR trống");

            // Parse QR content
            var parsed = QrTicketContent.FromQrString(qrContent);
            if (parsed == null)
                return QrValidationResult.Invalid("Định dạng QR không hợp lệ");

            // Decrypt and validate
            var payload = Decrypt(parsed.CipherText);
            if (payload == null)
                return QrValidationResult.Invalid("Vé không hợp lệ - Không thể giải mã");

            // Validate invoice
            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == payload.InvoiceId);

            if (invoice == null)
                return QrValidationResult.Invalid("Vé không hợp lệ - Không tìm thấy hóa đơn");

            if (payload.InvoiceId != parsed.InvoiceId)
                return QrValidationResult.Invalid("Vé không hợp lệ - Mã hóa đơn không khớp");

            // Get ticket info
            var ticket = await _context.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TicketId == payload.TicketId && t.InvoiceId == payload.InvoiceId);

            if (ticket == null)
                return QrValidationResult.Invalid("Vé không hợp lệ - Không tìm thấy vé");

            // Validate showtime
            var showTime = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(st => st.ShowTimeId == payload.ShowTimeId);

            if (showTime == null || showTime.ShowTimeId != ticket.ShowTimeId)
                return QrValidationResult.Invalid("Vé không hợp lệ - Suất chiếu không khớp");

            // Validate movie
            var movie = await _context.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == payload.MovieId);

            if (movie == null || movie.MoviesId != showTime.MoviesId)
                return QrValidationResult.Invalid("Vé không hợp lệ - Phim không khớp");

            // Ensure movie title in QR string also matches to detect tampering
            if (!string.Equals(parsed.MovieName?.Trim(), movie.Title?.Trim(), StringComparison.OrdinalIgnoreCase))
                return QrValidationResult.Invalid("Vé không hợp lệ - Tên phim không khớp");

            // Get all tickets for this invoice
            var allTickets = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoice.InvoiceId)
                .ToListAsync();

            // Get seat labels
            var seatIds = allTickets.Select(t => t.SeatId).ToList();
            var seatLabels = await _context.Seats.AsNoTracking()
                .Where(s => seatIds.Contains(s.SeatId))
                .Select(s => s.Label ?? s.SeatId)
                .OrderBy(x => x)
                .ToListAsync();

            // Get customer info
            var customer = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == invoice.CustomerId);

            // Get theater and room
            var room = await _context.CinemaTheaters.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);

            MovieTheaters? theater = null;
            if (room != null && !string.IsNullOrWhiteSpace(room.MovieTheaterId))
            {
                theater = await _context.MovieTheaters.AsNoTracking()
                    .FirstOrDefaultAsync(mt => mt.MovieTheaterId == room.MovieTheaterId);
            }

            return new QrValidationResult
            {
                IsValid = true,
                InvoiceId = invoice.InvoiceId,
                TicketId = ticket.TicketId,
                MovieTitle = movie.Title,
                MoviePoster = movie.PosterImage,
                SeatLabels = seatLabels,
                CustomerName = customer?.FullName ?? "Khách hàng",
                CustomerEmail = invoice.Email ?? customer?.Email,
                CustomerPhone = invoice.PhoneNumber ?? customer?.PhoneNumber,
                BookingDate = invoice.CreatedAt,
                ShowDate = showTime.ShowDate,
                ShowTime = showTime.StartTime,
                TheaterName = theater?.Name ?? "N/A",
                RoomName = room?.Name ?? "N/A",
                TotalPrice = invoice.TotalPrice ?? 0,
                TicketCount = allTickets.Count,
                TicketStatus = ticket.Status
            };
        }

        #endregion

        #region PDF Generation

        public async Task<byte[]?> GenerateTicketPdfAsync(string invoiceId)
        {
            var qrContent = await GenerateQrContentAsync(invoiceId);
            if (qrContent == null)
                return null;

            var validation = await ValidateQrAsync(qrContent.ToQrString());
            if (!validation.IsValid)
                return null;

            var qrImageBytes = GenerateQrImage(qrContent.ToQrString(), 8);

            // ✅ Get invoice to check for promotion/discount
            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);

            // Set page size
            pdf.SetDefaultPageSize(iText.Kernel.Geom.PageSize.A5);

            // Load Vietnamese-compatible font
            var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            PdfFont font = null;
            PdfFont boldFont = null;
            
            try
            {
                font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                boldFont = PdfFontFactory.CreateFont(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"), PdfEncodings.IDENTITY_H);
            }
            catch
            {
                // Fallback to Helvetica if Arial not found
                font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            }

            // Title
            var title = new Paragraph("VÉ XEM PHIM - CINEMAS")
                .SetFont(boldFont)
                .SetFontSize(16)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(10);

            // reduce overall margins so content fits on a single page
            document.SetMargins(20, 20, 20, 20);

            // Use a container to keep the main block together on one page
            var mainDiv = new iText.Layout.Element.Div().SetKeepTogether(true);

            mainDiv.Add(title);

            // QR Code
            var qrImage = new Image(ImageDataFactory.Create(qrImageBytes))
                .SetWidth(100)
                .SetHeight(100)
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginBottom(8);
            mainDiv.Add(qrImage);

            mainDiv.Add(new Paragraph().SetMarginBottom(6));

            // Movie info
            mainDiv.Add(CreateInfoParagraphWithFont("PHIM:", validation.MovieTitle ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("MÃ HÓA ĐƠN:", validation.InvoiceId ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("GHẾ:", validation.SeatLabels != null ? string.Join(", ", validation.SeatLabels) : "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("NGÀY CHIẾU:", validation.ShowDate?.ToString("dd/MM/yyyy") ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("GIỜ CHIẾU:", validation.ShowTime?.ToString("HH:mm") ?? "N/A", boldFont, font));

            // FIX: Show Room and Theater on same line
            string theaterRoom = $"{validation.RoomName ?? "N/A"} - {validation.TheaterName ?? "N/A"}";
            mainDiv.Add(CreateInfoParagraphWithFont("PHÒNG - RẠP:", theaterRoom, boldFont, font));

            mainDiv.Add(CreateInfoParagraphWithFont("KHÁCH HÀNG:", validation.CustomerName ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("EMAIL:", validation.CustomerEmail ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("NGÀY ĐẶT:", validation.BookingDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A", boldFont, font));

            // Add discount information if promotion exists
            bool hasPromotion = invoice != null && !string.IsNullOrWhiteSpace(invoice.PromotionId);
            if (hasPromotion && invoice.OriginalTotal.HasValue && invoice.OriginalTotal.Value > 0)
            {
                decimal originalTotal = invoice.OriginalTotal.Value;
                decimal payableAmount = invoice.TotalPrice ?? 0m;
                decimal discountAmount = originalTotal - payableAmount;

                if (discountAmount > 0)
                {
                    mainDiv.Add(CreateInfoParagraphWithFont("TỔNG TIỀN (GỐC):", $"{originalTotal:N0} VNĐ", boldFont, font));
                    mainDiv.Add(CreateInfoParagraphWithFont("GIẢM GIÁ:", $"{discountAmount:N0} VNĐ", boldFont, font));
                    mainDiv.Add(CreateInfoParagraphWithFont("TỔNG CỘNG:", $"{payableAmount:N0} VNĐ", boldFont, font));
                }
                else
                {
                    mainDiv.Add(CreateInfoParagraphWithFont("TỔNG TIỀN:", $"{payableAmount:N0} VNĐ", boldFont, font));
                }
            }
            else
            {
                mainDiv.Add(CreateInfoParagraphWithFont("TỔNG TIỀN:", $"{(invoice?.TotalPrice ?? validation.TotalPrice ?? 0):N0} VNĐ", boldFont, font));
            }

            document.Add(mainDiv);

            // Footer note (keep small and after main block)
            var footer = new Paragraph("Vui lòng mang theo vé điện tử này khi đến rạp. Xuất trình mã QR để được kiểm tra vé.")
                .SetFont(font)
                .SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(12)
                .SetFontColor(ColorConstants.GRAY);
            document.Add(footer);

            document.Close();
            return ms.ToArray();
        }

        public async Task<byte[]?> GenerateTicketPdfWithQrAsync(string invoiceId, byte[] qrImageBytes)
        {
            var qrContent = await GenerateQrContentAsync(invoiceId);
            if (qrContent == null)
                return null;

            var validation = await ValidateQrAsync(qrContent.ToQrString());
            if (!validation.IsValid)
                return null;

            // ✅ Get invoice to check for promotion/discount
            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);

            // Set page size
            pdf.SetDefaultPageSize(iText.Kernel.Geom.PageSize.A5);

            // Load Vietnamese-compatible font
            var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            PdfFont font = null;
            PdfFont boldFont = null;
            
            try
            {
                font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                boldFont = PdfFontFactory.CreateFont(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"), PdfEncodings.IDENTITY_H);
            }
            catch
            {
                // Fallback to Helvetica if Arial not found
                font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            }

            // Title
            var title = new Paragraph("VÉ XEM PHIM - CINEMAS")
                .SetFont(boldFont)
                .SetFontSize(16)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(10);

            // reduce overall margins so content fits on a single page
            document.SetMargins(20, 20, 20, 20);

            // Use a container to keep the main block together on one page
            var mainDiv = new iText.Layout.Element.Div().SetKeepTogether(true);

            mainDiv.Add(title);

            // QR Code (reuse provided bytes)
            var qrImage = new Image(ImageDataFactory.Create(qrImageBytes))
                .SetWidth(100)
                .SetHeight(100)
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetMarginBottom(8);
            mainDiv.Add(qrImage);

            mainDiv.Add(new Paragraph().SetMarginBottom(6));

            // Movie info - use helper method with font
            mainDiv.Add(CreateInfoParagraphWithFont("PHIM:", validation.MovieTitle ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("MÃ HÓA ĐƠN:", validation.InvoiceId ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("GHẾ:", validation.SeatLabels != null ? string.Join(", ", validation.SeatLabels) : "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("NGÀY CHIẾU:", validation.ShowDate?.ToString("dd/MM/yyyy") ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("GIỜ CHIẾU:", validation.ShowTime?.ToString("HH:mm") ?? "N/A", boldFont, font));

            // FIX: Show Room and Theater on same line
            string theaterRoom = $"{validation.RoomName ?? "N/A"} - {validation.TheaterName ?? "N/A"}";
            mainDiv.Add(CreateInfoParagraphWithFont("PHÒNG - RẠP:", theaterRoom, boldFont, font));

            mainDiv.Add(CreateInfoParagraphWithFont("KHÁCH HÀNG:", validation.CustomerName ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("EMAIL:", validation.CustomerEmail ?? "N/A", boldFont, font));
            mainDiv.Add(CreateInfoParagraphWithFont("NGÀY ĐẶT:", validation.BookingDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A", boldFont, font));

            // Add discount information if promotion exists
            bool hasPromotion = invoice != null && !string.IsNullOrWhiteSpace(invoice.PromotionId);
            if (hasPromotion && invoice.OriginalTotal.HasValue && invoice.OriginalTotal.Value > 0)
            {
                decimal originalTotal = invoice.OriginalTotal.Value;
                decimal payableAmount = invoice.TotalPrice ?? 0m;
                decimal discountAmount = originalTotal - payableAmount;

                if (discountAmount > 0)
                {
                    mainDiv.Add(CreateInfoParagraphWithFont("TỔNG TIỀN (GỐC):", $"{originalTotal:N0} VNĐ", boldFont, font));
                    mainDiv.Add(CreateInfoParagraphWithFont("GIẢM GIÁ:", $"{discountAmount:N0} VNĐ", boldFont, font));
                    mainDiv.Add(CreateInfoParagraphWithFont("TỔNG CỘNG:", $"{payableAmount:N0} VNĐ", boldFont, font));
                }
                else
                {
                    mainDiv.Add(CreateInfoParagraphWithFont("TỔNG TIỀN:", $"{payableAmount:N0} VNĐ", boldFont, font));
                }
            }
            else
            {
                mainDiv.Add(CreateInfoParagraphWithFont("TỔNG TIỀN:", $"{(invoice?.TotalPrice ?? validation.TotalPrice ?? 0):N0} VNĐ", boldFont, font));
            }

            document.Add(mainDiv);

            // Footer note (keep small and after main block)
            var footer = new Paragraph("Vui lòng mang theo vé điện tử này khi đến rạp. Xuất trình mã QR để được kiểm tra vé.")
                .SetFont(font)
                .SetFontSize(9)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(12)
                .SetFontColor(ColorConstants.GRAY);
            document.Add(footer);

            document.Close();
            return ms.ToArray();
        }

        private Paragraph CreateInfoParagraph(string label, string value)
        {
            var p = new Paragraph()
                .SetFontSize(11)
                .SetMarginBottom(5);

            var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var boldText = new Text(label + " ").SetFont(font);
            
            p.Add(boldText);
            p.Add(new Text(value));

            return p;
        }

        private Paragraph CreateInfoParagraphWithFont(string label, string value, PdfFont boldFont, PdfFont regularFont)
        {
            var p = new Paragraph()
                .SetFontSize(11)
                .SetMarginBottom(5);

            var boldText = new Text(label + " ").SetFont(boldFont);
            var valueText = new Text(value).SetFont(regularFont);
            
            p.Add(boldText);
            p.Add(valueText);

            return p;
        }

        #endregion

        #region Encryption/Decryption

        private string Encrypt(QrTicketPayload payload)
        {
            var plainText = $"{payload.TicketId}|{payload.InvoiceId}|{payload.MovieId}|{payload.ShowTimeId}|{_secretKey}";

            using var aes = Aes.Create();
            aes.Key = DeriveKey(_secretKey);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var msEncrypt = new MemoryStream();

            // Write IV first
            msEncrypt.Write(aes.IV, 0, aes.IV.Length);

            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        private QrTicketPayload? Decrypt(string cipherText)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = DeriveKey(_secretKey);

                // Extract IV from the beginning
                var iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var msDecrypt = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                var plainText = srDecrypt.ReadToEnd();
                var parts = plainText.Split('|');

                if (parts.Length < 5)
                    return null;

                // Validate secret key
                if (parts[4] != _secretKey)
                    return null;

                return new QrTicketPayload
                {
                    TicketId = parts[0],
                    InvoiceId = parts[1],
                    MovieId = parts[2],
                    ShowTimeId = parts[3]
                };
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DeriveKey(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        #endregion
    }
}
