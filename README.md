# CNNet_N02_Nhom08

XÂY DỰNG HỆ THỐNG ĐẶT VÀ QUẢN LÝ QUÁN CHÈ SÀI GÒN

Dự án môn học Công nghệ .NET, sử dụng ASP.NET Core 10 và C#.

## Cấu trúc hiện tại

- `SaigonChe.Api`: ASP.NET Core Web API.
- `SaigonChe.Web`: ASP.NET Core MVC.
- `SaigonChe.slnx`: solution chung của hai project.

## Yêu cầu

- .NET 10 SDK
- SQL Server Express
cài các package vào SaigonChe.Api
```
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.12
```
```
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.12
```
```
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 10.0.12
```

## Khôi phục và build

```powershell
dotnet restore .\SaigonChe.slnx
dotnet build .\SaigonChe.slnx
```

## Chạy API

```powershell
dotnet run --project .\SaigonChe.Api\SaigonChe.Api.csproj
```
hoặc 
```
dotnet run --project SaigonChe.Api\SaigonChe.Api.csproj --launch-profile https
```

API mặc định: `https://localhost:7286` hoặc `http://localhost:5267`.
Endpoint mẫu: `/weatherforecast`.

## Chạy Web

```powershell
dotnet run --project .\SaigonChe.Web\SaigonChe.Web.csproj
```

Web mặc định: `https://localhost:7120` hoặc `http://localhost:5140`.



ToListAsync()       → SELECT
FindAsync(id)       → SELECT ... WHERE Id = ...
Add() + SaveChanges → INSERT
SaveChangesAsync()  → UPDATE
Remove()            → DELETE