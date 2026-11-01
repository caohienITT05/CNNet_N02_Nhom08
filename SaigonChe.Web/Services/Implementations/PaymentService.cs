using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Hubs;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<PaymentHub> _hubContext;

        public PaymentService(ApplicationDbContext context, IHubContext<PaymentHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<bool> ProcessVietQrWebhookAsync(WebhookPayloadDto payload, string rawJson)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Content))
                return false;

            // 1. Tìm đơn hàng qua mã đơn TFxxx nằm trong nội dung CK
            var order = await _context.Orders.FirstOrDefaultAsync(o => payload.Content.Contains(o.OrderCode));
            if (order == null) return false;

            // 2. Chống lặp giao dịch (Idempotency)
            var existingTx = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == payload.TransactionNumber);
            if (existingTx != null) return true;

            // 3. Đối soát số tiền thực nhận với đơn hàng
            bool isAmountMatched = payload.Amount >= order.Total;

            var transaction = new PaymentTransaction
            {
                OrderId = order.Id,
                TransactionCode = payload.TransactionNumber ?? Guid.NewGuid().ToString(),
                ReferenceCode = payload.Content,
                Amount = payload.Amount,
                Provider = "VietQR",
                Status = isAmountMatched ? "Success" : "AmountMismatch",
                RawWebhookData = rawJson,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransactions.Add(transaction);

            if (isAmountMatched)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaidAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // 4. Bắn thông báo Realtime SignalR tới trang web đang mở của khách hàng
                await _hubContext.Clients.Group(order.OrderCode).SendAsync("PaymentSuccess", new
                {
                    orderCode = order.OrderCode,
                    amount = payload.Amount,
                    message = "Thanh toán thành công! Đang chuyển hướng..."
                });

                return true;
            }

            await _context.SaveChangesAsync();
            return false;
        }
    }
}