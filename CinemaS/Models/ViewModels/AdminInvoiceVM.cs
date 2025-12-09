// Models/ViewModels/AdminInvoiceVM.cs
using System;
using CinemaS.Models;

namespace CinemaS.Models.ViewModels
{
    public class AdminInvoiceVM
    {
        public Invoices Invoice { get; set; } = new Invoices();
        public PaymentTransactions? LastTransaction { get; set; }

        // Dùng cho tạo/sửa nhanh
        public decimal? Amount { get; set; }
        public byte? PaymentStatus { get; set; }  // 1 = thành công, 2 = lỗi/huỷ
        public string? PaymentMethodId { get; set; }
    }
}
