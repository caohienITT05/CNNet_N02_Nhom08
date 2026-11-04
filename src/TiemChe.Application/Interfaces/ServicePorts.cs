using TiemChe.Contracts.Management;

namespace TiemChe.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IResetEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string code, CancellationToken cancellationToken);
}

public interface IImageStore
{
    Task<string> SaveAsync(Stream source, long length, CancellationToken cancellationToken);
    bool Exists(string path);
}

public interface IRevenueWorkbook
{
    byte[] Create(DashboardDto dashboard);
}
