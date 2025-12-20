using System.Threading.Tasks;
using CinemaS.Models.ViewModels;
using CinemaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaS.Controllers
{
    [Route("[controller]")]
    public class QrController : Controller
    {
        private readonly IQrTicketService _qrService;

        public QrController(IQrTicketService qrService)
        {
            _qrService = qrService;
        }

        /// <summary>
        /// QR Scanner Page (for staff to scan and validate tickets)
        /// </summary>
        [HttpGet("Scanner")]
        [Authorize(Roles = "Admin")]
        public IActionResult Scanner()
        {
            return View();
        }

        /// <summary>
        /// Validate QR content and return ticket information
        /// </summary>
        [HttpPost("Validate")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Validate([FromBody] QrValidateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.QrContent))
                return Json(QrValidationResult.Invalid("Nội dung QR trống"));

            var result = await _qrService.ValidateQrAsync(request.QrContent);
            return Json(result);
        }

        /// <summary>
        /// Get QR image for an invoice (PNG)
        /// </summary>
        [HttpGet("Image/{invoiceId}")]
        [Authorize]
        public async Task<IActionResult> GetQrImage(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return NotFound();

            var qrContent = await _qrService.GenerateQrContentAsync(invoiceId);
            if (qrContent == null)
                return NotFound("Không tìm thấy hóa đơn hoặc vé");

            var qrBytes = _qrService.GenerateQrImage(qrContent.ToQrString(), 10);
            return File(qrBytes, "image/png", $"QR_{invoiceId}.png");
        }

        /// <summary>
        /// Get QR image as Base64 string
        /// </summary>
        [HttpGet("ImageBase64/{invoiceId}")]
        [Authorize]
        public async Task<IActionResult> GetQrImageBase64(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return Json(new { success = false, message = "Thiếu mã hóa đơn" });

            var base64 = await _qrService.GenerateQrImageBase64Async(invoiceId, 10);
            if (base64 == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn hoặc vé" });

            return Json(new { success = true, imageBase64 = base64 });
        }

        /// <summary>
        /// Download PDF ticket with QR code
        /// </summary>
        [HttpGet("Pdf/{invoiceId}")]
        [Authorize]
        public async Task<IActionResult> GetPdf(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return NotFound();

            var pdfBytes = await _qrService.GenerateTicketPdfAsync(invoiceId);
            if (pdfBytes == null)
                return NotFound("Không tìm thấy hóa đơn hoặc vé");

            return File(pdfBytes, "application/pdf", $"Ticket_{invoiceId}.pdf");
        }

        /// <summary>
        /// Get QR content JSON for debugging (Admin only)
        /// </summary>
        [HttpGet("Content/{invoiceId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetQrContent(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return Json(new { success = false, message = "Thiếu mã hóa đơn" });

            var qrContent = await _qrService.GenerateQrContentAsync(invoiceId);
            if (qrContent == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn hoặc vé" });

            return Json(new
            {
                success = true,
                invoiceId = qrContent.InvoiceId,
                movieName = qrContent.MovieName,
                seats = qrContent.Seats,
                qrString = qrContent.ToQrString()
            });
        }
    }

    public class QrValidateRequest
    {
        public string QrContent { get; set; } = string.Empty;
    }
}
