using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using TiemChe.Api.Authorization;
using TiemChe.Application.Services;
using TiemChe.Contracts.Auth;

namespace TiemChe.Api.Endpoints;

internal static class AccountEndpoints
{
    internal static void MapAccounts(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").RequireRateLimiting("account");
        auth.MapPost("/register", async (RegisterRequest body, AccountService service, CancellationToken ct) =>
            Results.Ok(await service.RegisterAsync(body, ct)));
        auth.MapPost("/login", async (LoginRequest body, AccountService service, CancellationToken ct) =>
        {
            var session = await service.LoginAsync(body, ct);
            return Results.SignIn(SessionClaims.Create(session.Profile, session.Stamp),
                authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
        });
        auth.MapPost("/forgot-password", async (ForgotPasswordRequest body, AccountService service, CancellationToken ct) =>
        {
            await service.RequestResetAsync(body, ct);
            return Results.NoContent();
        });
        auth.MapPost("/reset-password", async (ResetPasswordRequest body, AccountService service, CancellationToken ct) =>
        {
            await service.ResetAsync(body, ct);
            return Results.NoContent();
        });

        var profile = api.MapGroup("/profile").RequireAuthorization();
        profile.MapGet("", (ClaimsPrincipal user, AccountService service, CancellationToken ct) =>
            service.GetAsync(user.UserId(), ct));
        profile.MapPut("", (ProfileRequest body, ClaimsPrincipal user, AccountService service, CancellationToken ct) =>
            service.UpdateAsync(user.UserId(), body, ct));
        profile.MapPost("/password", async (ChangePasswordRequest body, ClaimsPrincipal user, AccountService service, CancellationToken ct) =>
        {
            await service.ChangePasswordAsync(user.UserId(), body, ct);
            return Results.NoContent();
        }).RequireRateLimiting("account");
    }
}
