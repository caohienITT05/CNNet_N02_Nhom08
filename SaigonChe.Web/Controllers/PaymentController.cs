using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IVietQrService _vietQrService;
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _config;

        public PaymentController(
            IOrderService orderService,
            IVietQrService vietQrService,
            IPaymentService paymentService,
            IConfiguration config)
        {
            _orderService = orderService;
            _vietQrService = vietQrService;
            _paymentService = paymentService;
            _config = config;
        }

        // GET: /Payment/VietQrPay?orderCode=TFxxxx
        [HttpGet]
        public async Task<IActionResult> VietQrPay(string orderCode)
        {
            if (string.IsNullOrEmpty(orderCode)) return RedirectToAction("Index", "Cart");

            var order = await _orderService.GetOrderByCodeAsync(orderCode);
            if (order == null) return NotFound("Không tìm thấy đơn hàng.");

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                return RedirectToAction("Success", "Order", new { orderCode = order.OrderCode });
            }

            string bankId = _config["VietQR:BankId"] ?? "MB";
            string accountNo = _config["VietQR:AccountNo"] ?? "0355960249";
            string accountName = _config["VietQR:AccountName"] ?? "CAO THI HIEN";

            string qrUrl = _vietQrService.GenerateQuickLink(bankId, accountNo, accountName, order.Total, order.OrderCode);

            var vm = new VietQrPaymentViewModel
            {
                OrderCode = order.OrderCode,
                FinalAmount = order.Total,
                BankId = bankId,
                AccountNo = accountNo,
                AccountName = accountName,
                QrImageUrl = qrUrl
            };

            return View(vm);
        }

        // POST: /api/payment/vietqr-webhook (Endpoint nhận tín hiệu tự động từ VietQR/Ngân hàng)
        [HttpPost]
        [Route("api/payment/vietqr-webhook")]
        public async Task<IActionResult> VietQrWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            string rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(rawBody)) return BadRequest();

            WebhookPayloadDto? payload;
            try
            {
                payload = JsonSerializer.Deserialize<WebhookPayloadDto>(rawBody);
            }
            catch
            {
                return BadRequest("Dữ liệu JSON không hợp lệ.");
            }

            if (payload == null) return BadRequest();

            // Xác thực Secret Key
            string expectedKey = _config["VietQR:WebhookSecretKey"] ?? "";
            if (!string.IsNullOrEmpty(expectedKey) && payload.SecretKey != expectedKey)
            {
                return Unauthorized("Khóa bí mật Webhook không đúng.");
            }

            bool result = await _paymentService.ProcessVietQrWebhookAsync(payload, rawBody);
            if (result)
            {
                return Ok(new { success = true, message = "Xác nhận thanh toán thành công." });
            }

            return BadRequest(new { success = false, message = "Không tìm thấy đơn hoặc sai số tiền." });
        }
    }
}