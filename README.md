# CNNet_N02_Nhom08

XÂY DỰNG HỆ THỐNG ĐẶT VÀ QUẢN LÝ QUÁN CHÈ SÀI GÒN

Dự án môn học Công nghệ .NET, sử dụng ASP.NET Core 10 và C#.

## Cấu trúc hiện tại

- `SaigonChe.Api`: ASP.NET Core Web API.
- `SaigonChe.Web`: ASP.NET Core MVC.
- `SaigonChe.slnx`: solution chung của hai project.

## Yêu cầu

- .NET 10 SDK

## Khôi phục và build

```powershell
dotnet restore .\SaigonChe.slnx
dotnet build .\SaigonChe.slnx
```

## Chạy API

```powershell
dotnet run --project .\SaigonChe.Api\SaigonChe.Api.csproj
```

API mặc định: `https://localhost:7286` hoặc `http://localhost:5267`.
Endpoint mẫu: `/weatherforecast`.

## Chạy Web

```powershell
dotnet run --project .\SaigonChe.Web\SaigonChe.Web.csproj
```

Web mặc định: `https://localhost:7120` hoặc `http://localhost:5140`.

Hai project hiện là khung mặc định và chưa kết nối với nhau.
