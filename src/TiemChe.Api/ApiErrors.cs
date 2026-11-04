using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Validation;

namespace TiemChe.Api;

internal sealed class ApiErrors(ILogger<ApiErrors> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, message) = exception switch
        {
            BusinessException error => (error.Status, error.Message),
            BadHttpRequestException error => (error.StatusCode, "Dữ liệu gửi lên không hợp lệ."),
            DbUpdateConcurrencyException => (409, "Dữ liệu vừa thay đổi. Vui lòng tải lại."),
            DbUpdateException { InnerException: SqliteException { SqliteExtendedErrorCode: 2067 or 1555 } }
                => (409, "Dữ liệu này đã tồn tại hoặc vừa được cập nhật."),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 5 or 6 } }
                or SqliteException { SqliteErrorCode: 5 or 6 } => (503, "Hệ thống đang bận. Vui lòng thử lại."),
            _ => (500, "Có lỗi xảy ra. Vui lòng thử lại.")
        };
        if (status == 500)
            logger.LogError(exception, "Lỗi khi xử lý {Method} {Path}.", context.Request.Method, context.Request.Path);
        await Results.Problem(statusCode: status, detail: message).ExecuteAsync(context);
        return true;
    }
}
