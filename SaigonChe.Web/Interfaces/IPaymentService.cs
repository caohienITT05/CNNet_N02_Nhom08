using System.Threading.Tasks;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Interfaces
{
    public interface IPaymentService
    {
        Task<bool> ProcessVietQrWebhookAsync(WebhookPayloadDto payload, string rawJson);
    }
}