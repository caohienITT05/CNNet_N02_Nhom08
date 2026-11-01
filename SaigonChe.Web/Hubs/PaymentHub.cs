using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SaigonChe.Web.Hubs
{
    public class PaymentHub : Hub
    {
        public async Task JoinOrderGroup(string orderCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, orderCode);
        }
    }
}