using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using TiemChe.Contracts.Auth;

namespace TiemChe.Api.Authorization;

internal static class SessionClaims
{
    internal const string Stamp = "tiemche:stamp";
    internal const string Operators = "StaffOrAdmin";

    internal static ClaimsPrincipal Create(UserProfile user, Guid stamp) => new(new ClaimsIdentity(
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(Stamp, stamp.ToString("D"))
        ], BearerTokenDefaults.AuthenticationScheme));

    internal static int UserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
}
