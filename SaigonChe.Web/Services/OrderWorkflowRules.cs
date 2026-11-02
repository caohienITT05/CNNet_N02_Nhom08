using SaigonChe.Web.Models;
namespace SaigonChe.Web.Services;
public static class OrderWorkflowRules
{
    public static bool IsAllowed(OrderStatus current, OrderStatus next) => current switch
    {
        OrderStatus.Pending => next is OrderStatus.Confirmed or OrderStatus.Cancelled,
        OrderStatus.Confirmed => next is OrderStatus.Preparing or OrderStatus.Cancelled,
        OrderStatus.Preparing => next is OrderStatus.Completed or OrderStatus.Cancelled,
        _ => false
    };
    public static bool Matches(string? submitted, byte[] current)
    {
        try { return !string.IsNullOrWhiteSpace(submitted) && Convert.FromBase64String(submitted).SequenceEqual(current); }
        catch (FormatException) { return false; }
    }
}
