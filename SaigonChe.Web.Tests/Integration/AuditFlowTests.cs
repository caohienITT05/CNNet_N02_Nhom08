using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Playwright;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using Xunit;

namespace SaigonChe.Web.Tests.Integration;

[Trait("Category", "SqlIntegration")]
public sealed class AuditFlowTests(AuditHost host) : IClassFixture<AuditHost>
{
    [Fact]
    public async Task Authorization_Registration_Csrf_And_DefaultIdentityRoutes()
    {
        using var anonymous = host.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Pos")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync("/Cart/AddToCart", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        var alternate = await anonymous.GetAsync("/Identity/Account/Register");
        Assert.Equal("/Account/Register", alternate.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync("/Identity/Account/Register", new StringContent(""))).StatusCode);
        using var customer = await host.Login("audit.customer@example.test");
        using var staff = await host.Login("audit.staff@example.test");
        Assert.Contains("/Account/AccessDenied", (await customer.GetAsync("/Pos")).Headers.Location!.ToString());
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync("/Reports")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/Pos")).StatusCode);
        var email = "register." + Guid.NewGuid().ToString("N") + "@example.test";
        var registration = await AuditHost.Post(anonymous, "/Account/Register", "/Account/Register", new() {
            ["Email"] = email, ["Password"] = AuditHost.Password, ["ConfirmPassword"] = AuditHost.Password });
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        await host.WithUsers(async manager =>
        {
            var user = await manager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.Equal(new[] { "Customer" }, await manager.GetRolesAsync(user));
        });
    }

    [Fact]
    public async Task Catalog_Validation_ImageContent_And_ProtectedDeletion()
    {
        using var admin = await host.Login("admin@gmail.com");
        var name = "Catalog-" + Guid.NewGuid().ToString("N");
        var invalid = await AuditHost.Post(admin, "/Admin/CreateProduct", "/Admin/CreateProduct",
            new() { ["Name"] = name, ["Description"] = "valid", ["Price"] = "25000", ["CategoryId"] = "2147483647" });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        await using var db = host.Db();
        Assert.False(await db.Products.AnyAsync(item => item.Name == name));
        var category = await db.Categories.FirstAsync();
        var html = await admin.GetStringAsync("/Admin/CreateProduct");
        using var multipart = new MultipartFormDataContent();
        foreach (var pair in new Dictionary<string,string> { ["Name"] = name, ["Description"] = "valid",
            ["Price"] = "25000", ["CategoryId"] = category.Id.ToString(),
            ["__RequestVerificationToken"] = AuditHost.Field(html, "__RequestVerificationToken") })
            multipart.Add(new StringContent(pair.Value), pair.Key);
        var fake = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("not an image but a forged extension"));
        fake.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(fake, "ImageFile", "fake.png");
        var forged = await admin.PostAsync("/Admin/CreateProduct", multipart);
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        Assert.False(await db.Products.AnyAsync(item => item.Name == name));
        var created = await AuditHost.Post(admin, "/Admin/CreateProduct", "/Admin/CreateProduct", new() {
            ["Name"] = name, ["Description"] = "valid", ["Price"] = "25000", ["CategoryId"] = category.Id.ToString(), ["IsAvailable"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var product = await db.Products.SingleAsync(item => item.Name == name);
        db.InventoryMovements.Add(new InventoryMovement { ProductId = product.Id, QuantityChange = 1, Reason = "Audit history" });
        await db.SaveChangesAsync();
        var deleted = await AuditHost.Post(admin, "/Admin/Products", "/Admin/DeleteProduct", new() { ["id"] = product.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        await db.Entry(product).ReloadAsync();
        Assert.False(product.IsAvailable);
        var blank = await AuditHost.Post(admin, "/Staff/Create", "/Staff/Create", new() { ["Email"] = "", ["Password"] = "" });
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        var invalidVoucher = await AuditHost.Post(admin, "/OperationsAdmin/CreateVoucher", "/OperationsAdmin/SaveVoucher",
            new() { ["Code"] = "", ["Amount"] = "0", ["Type"] = "999", ["ValidFrom"] = "0001-01-01", ["ValidTo"] = "9999-12-31" });
        Assert.Equal(HttpStatusCode.OK, invalidVoucher.StatusCode);
    }

    [Fact]
    public async Task Checkout_RepricesFromDatabase_And_DuplicatePostsCreateOneOrder()
    {
        var product = await host.Product();
        using var client = await host.Login("audit.customer@example.test");
        await AuditHost.Post(client, "/", "/Cart/AddToCart", new() { ["productId"] = product.Id.ToString(), ["quantity"] = "2" });
        var html = await client.GetStringAsync("/Cart/Checkout");
        var token = AuditHost.Field(html, "CheckoutToken");
        var csrf = AuditHost.Field(html, "__RequestVerificationToken");
        await using (var db = host.Db()) { var row = await db.Products.FindAsync(product.Id); row!.Price = 27000; await db.SaveChangesAsync(); }
        Dictionary<string,string> values = new() { ["CheckoutToken"] = token, ["__RequestVerificationToken"] = csrf,
            ["CustomerName"] = "Audit Checkout", ["Phone"] = "0900000000", ["Address"] = "Audit address" };
        var responses = await Task.WhenAll(client.PostAsync("/Cart/Checkout", new FormUrlEncodedContent(values)),
            client.PostAsync("/Cart/Checkout", new FormUrlEncodedContent(values)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        await using var verify = host.Db();
        var order = Assert.Single(await verify.Orders.Include(item => item.Items).Where(item => item.CheckoutToken == Guid.Parse(token)).ToListAsync());
        Assert.Equal(54000, order.Total); Assert.Equal(27000, Assert.Single(order.Items).Price);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Cart/Checkout")).StatusCode);
    }

    [Fact]
    public async Task Pos_NotesAdvanceVersion_ConcurrentLastStock_And_RetryAreSafe()
    {
        using var staff1 = await host.Login("audit.staff@example.test");
        using var staff2 = await host.Login("audit.staff@example.test");
        var product = await host.Product(1);
        var order1 = await host.Pos(staff1, product);
        var order2 = await host.Pos(staff2, product);
        await using var db = host.Db();
        var row = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == order1);
        var before = row.RowVersion.ToArray();
        var html = await staff1.GetStringAsync("/Pos/Order/" + order1);
        var csrf = AuditHost.Field(html, "__RequestVerificationToken");
        Dictionary<string,string> edit = new() { ["orderId"] = order1.ToString(), ["itemId"] = row.Items.Single().Id.ToString(),
            ["quantity"] = "1", ["notes"] = "Audit note", ["rowVersion"] = Convert.ToBase64String(before), ["__RequestVerificationToken"] = csrf };
        await staff1.PostAsync("/Pos/UpdateItem", new FormUrlEncodedContent(edit));
        await db.Entry(row).ReloadAsync();
        Assert.False(before.SequenceEqual(row.RowVersion));
        edit["notes"] = "stale note";
        await staff1.PostAsync("/Pos/UpdateItem", new FormUrlEncodedContent(edit));
        await db.Entry(row.Items.Single()).ReloadAsync();
        Assert.Equal("Audit note", row.Items.Single().Notes);
        async Task<HttpResponseMessage> Pay(HttpClient client, int id)
        {
            var page = await client.GetStringAsync("/Pos/Order/" + id);
            return await AuditHost.Post(client, "/Pos/Order/" + id, "/Pos/Pay", new() { ["orderId"] = id.ToString(),
                ["rowVersion"] = AuditHost.Field(page, "rowVersion"), ["method"] = ((int)PaymentMethod.Cash).ToString(), ["amountReceived"] = "30000" });
        }
        var results = await Task.WhenAll(Pay(staff1, order1), Pay(staff2, order2));
        foreach (var result in results) Assert.True(result.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Conflict, $"Payment HTTP {(int)result.StatusCode}");
        var orders = await db.Orders.AsNoTracking().Where(item => item.Id == order1 || item.Id == order2).ToListAsync();
        var paid = Assert.Single(orders, item => item.IsPaid);
        Assert.Equal(0, await db.Products.Where(item => item.Id == product.Id).Select(item => item.Stock).SingleAsync());
        Assert.Equal(1, await db.InventoryMovements.CountAsync(item => item.ProductId == product.Id));
        var receipt = await staff1.GetAsync("/Pos/Receipt/" + paid.Id);
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        await AuditHost.Post(staff1, "/Pos", "/Pos/Pay", new() { ["orderId"] = paid.Id.ToString(),
            ["rowVersion"] = Convert.ToBase64String(before), ["method"] = ((int)PaymentMethod.Cash).ToString(), ["amountReceived"] = "30000" });
        Assert.Equal(1, await db.InventoryMovements.CountAsync(item => item.ProductId == product.Id));
    }

    [Fact]
    public async Task Pos_QuickAdd_CreatesDraft_AndPaidOrderRedirectsToReceipt()
    {
        var email = $"audit.staff.{Guid.NewGuid():N}@example.test";
        await host.WithUsers(async manager =>
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            AuditHost.Check(await manager.CreateAsync(user, AuditHost.Password));
            AuditHost.Check(await manager.AddToRoleAsync(user, "Staff"));
        });
        using var staff = await host.Login(email);
        var product = await host.Product(10);

        var quickAdd = await AuditHost.Post(staff, "/Pos", "/Pos/QuickAdd", new() { ["productId"] = product.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, quickAdd.StatusCode);
        var orderId = int.Parse(System.Text.RegularExpressions.Regex.Match(quickAdd.Headers.Location!.ToString(), @"(\d+)$").Value);

        await using var db = host.Db();
        var order = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId);
        Assert.False(order.IsPaid);
        Assert.Contains(order.Items, item => item.ProductId == product.Id);

        var page = await staff.GetStringAsync("/Pos/Order/" + orderId);
        var pay = await AuditHost.Post(staff, "/Pos/Order/" + orderId, "/Pos/Pay", new() {
            ["orderId"] = orderId.ToString(), ["rowVersion"] = AuditHost.Field(page, "rowVersion"),
            ["method"] = ((int)PaymentMethod.Cash).ToString(), ["amountReceived"] = "30000" });
        Assert.Equal(HttpStatusCode.Redirect, pay.StatusCode);

        var closed = await staff.GetAsync("/Pos/Order/" + orderId);
        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);
        Assert.EndsWith("/Pos/Receipt/" + orderId, closed.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Party_OwnerIsolation_StaleUpdate_And_PaymentPersist()
    {
        using var customer = await host.Login("audit.customer@example.test");
        using var other = await host.Login("audit.other@example.test");
        using var staff = await host.Login("audit.staff@example.test");
        var marker = "Party-" + Guid.NewGuid().ToString("N");
        var create = await AuditHost.Post(customer, "/Party/Create", "/Party/Create", new() {
            ["CustomerName"] = marker, ["Phone"] = "0900000000", ["Location"] = "Audit hall",
            ["EventAt"] = VietnamTime.ToLocal(DateTime.UtcNow).AddDays(3).ToString("yyyy-MM-ddTHH:mm"), ["Headcount"] = "10" });
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        await using var db = host.Db();
        var request = await db.PartyRequests.SingleAsync(item => item.CustomerName == marker);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/Party/Details/" + request.Id)).StatusCode);
        var product = await host.Product();
        async Task<HttpResponseMessage> Update(PartyRequestStatus status, string? version = null, PaymentMethod method = PaymentMethod.Cash)
        {
            await db.Entry(request).ReloadAsync();
            return await AuditHost.Post(staff, "/Party/Details/" + request.Id, "/Party/Update", new() {
                ["Id"] = request.Id.ToString(), ["RowVersion"] = version ?? Convert.ToBase64String(request.RowVersion),
                ["Status"] = ((int)status).ToString(), ["QuoteAmount"] = "25000", ["Consultation"] = "Audit consultation",
                ["PaymentMethod"] = ((int)method).ToString(), ["Lines[0].ProductId"] = product.Id.ToString(),
                ["Lines[0].Selected"] = "true", ["Lines[0].Quantity"] = "1" });
        }
        var stale = Convert.ToBase64String(request.RowVersion);
        Assert.Equal(HttpStatusCode.Redirect, (await Update(PartyRequestStatus.InProgress)).StatusCode);
        await Update(PartyRequestStatus.Confirmed, stale);
        await db.Entry(request).ReloadAsync(); Assert.Null(request.OrderId);
        await Update(PartyRequestStatus.Confirmed);
        await db.Entry(request).ReloadAsync(); Assert.NotNull(request.OrderId);
        var id = request.OrderId;
        await Update(PartyRequestStatus.Confirmed);
        await db.Entry(request).ReloadAsync(); Assert.Equal(id, request.OrderId);
        Assert.Equal(HttpStatusCode.OK, (await Update(PartyRequestStatus.Completed, method:PaymentMethod.VietQr)).StatusCode);
        Assert.False(await db.Orders.Where(item=>item.Id==id).Select(item=>item.IsPaid).SingleAsync());
        await Update(PartyRequestStatus.Completed);
        var order = await db.Orders.FindAsync(id);
        Assert.True(order!.IsPaid); Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Single(await db.InventoryMovements.Where(item => item.OrderId == id).ToListAsync());
    }

    [Fact]
    public async Task Voucher_EditingRoundTripsVietnamDates_And_ReportFiltersAreValidated()
    {
        using var admin = await host.Login("admin@gmail.com");
        var code = "AUD" + Guid.NewGuid().ToString("N")[..10];
        Dictionary<string,string> fields = new() { ["Code"] = code, ["Type"] = "0", ["Amount"] = "5000",
            ["MinimumOrderTotal"] = "0", ["ValidFrom"] = "2026-10-31", ["ValidTo"] = "2026-11-02", ["IsActive"] = "true" };
        Assert.Equal(HttpStatusCode.Redirect, (await AuditHost.Post(admin, "/OperationsAdmin/CreateVoucher", "/OperationsAdmin/SaveVoucher", fields)).StatusCode);
        await using var db = host.Db();
        var voucher = await db.Vouchers.SingleAsync(item => item.Code == code);
        Assert.Equal(VietnamTime.ToUtc(new DateTime(2026,10,31)), voucher.ValidFrom);
        var html = await admin.GetStringAsync("/OperationsAdmin/EditVoucher/" + voucher.Id);
        Assert.StartsWith("2026-10-31", AuditHost.Field(html, "ValidFrom"));
        fields["Id"] = voucher.Id.ToString();
        await AuditHost.Post(admin, "/OperationsAdmin/EditVoucher/" + voucher.Id, "/OperationsAdmin/SaveVoucher", fields);
        await db.Entry(voucher).ReloadAsync();
        Assert.Equal(VietnamTime.ToUtc(new DateTime(2026,10,31)), voucher.ValidFrom);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Reports?from=0001-01-01&to=9999-12-31&type=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/Reports/ExportExcel?to=9999-12-31")).StatusCode);
    }

    [Fact]
    public async Task Reports_ExcelMatchesVietnamBoundaryAndExcludesUnpaid()
    {
        await using var db = host.Db();
        db.Orders.AddRange(
            new Order { CustomerName="Audit report included",Phone="0900000000",Address="Audit",Total=12345,DiscountAmount=100,
                Status=OrderStatus.Completed,IsPaid=true,Type=OrderType.Pos,CompletedAt=new DateTime(2024,12,31,16,59,59,DateTimeKind.Utc) },
            new Order { CustomerName="Audit report excluded",Phone="0900000000",Address="Audit",Total=99999,
                Status=OrderStatus.Completed,IsPaid=false,Type=OrderType.Pos,CompletedAt=new DateTime(2024,12,31,16,59,59,DateTimeKind.Utc) },
            new Order { CustomerName="Audit next day",Phone="0900000000",Address="Audit",Total=88888,
                Status=OrderStatus.Completed,IsPaid=true,Type=OrderType.Pos,CompletedAt=new DateTime(2024,12,31,17,0,0,DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        using var admin = await host.Login("admin@gmail.com");
        var filter = "?from=2024-12-31&to=2024-12-31&type=Pos";
        var screen = await admin.GetStringAsync("/Reports" + filter);
        Assert.Contains("Audit report included", screen);
        Assert.DoesNotContain("Audit report excluded", screen); Assert.DoesNotContain("Audit next day", screen);
        var response = await admin.GetAsync("/Reports/ExportExcel" + filter);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(Path.Combine(host.Artifacts, "report.xlsx"), bytes);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var stream = entry.Open();
        var sheet = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = sheet.Descendants(ns+"row").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("12345", rows[1].Value);
        Assert.DoesNotContain("99999", rows[1].Value);
    }

    [Fact]
    public async Task AccountLockAndResetRevokeOldCookies_LastAdminCannotBeLocked()
    {
        using var admin = await host.Login("admin@gmail.com");
        string id="";
        await host.WithUsers(async manager =>
        {
            var user = new ApplicationUser { UserName="lock."+Guid.NewGuid().ToString("N")+"@example.test", EmailConfirmed=true };
            user.Email=user.UserName; AuditHost.Check(await manager.CreateAsync(user, AuditHost.Password));
            AuditHost.Check(await manager.AddToRoleAsync(user,"Staff")); id=user.Id;
        });
        await using var db=host.Db(); var email=await db.Users.Where(item=>item.Id==id).Select(item=>item.Email).SingleAsync();
        using var old = await host.Login(email!);
        await AuditHost.Post(admin,"/Staff","/Staff/SetLocked",new(){["id"]=id,["locked"]="true"});
        Assert.Equal(HttpStatusCode.Redirect,(await old.GetAsync("/Pos")).StatusCode);
        await AuditHost.Post(admin,"/Staff","/Staff/SetLocked",new(){["id"]=id,["locked"]="false"});
        using var beforeReset = await host.Login(email!);
        await AuditHost.Post(admin,"/Staff/ResetPassword/"+id,"/Staff/ResetPassword",new(){["Id"]=id,["Password"]="Changed!2026_Audit",["ConfirmPassword"]="Changed!2026_Audit"});
        Assert.Equal(HttpStatusCode.Redirect,(await beforeReset.GetAsync("/Pos")).StatusCode);
        string adminId="";
        await host.WithUsers(async manager=>{var user=await manager.FindByEmailAsync("admin@gmail.com");adminId=user!.Id;AuditHost.Check(await manager.AddToRoleAsync(user,"Staff"));});
        await AuditHost.Post(admin,"/Staff","/Staff/SetLocked",new(){["id"]=adminId,["locked"]="true"});
        var last = await db.Users.AsNoTracking().SingleAsync(item=>item.Id==adminId);
        Assert.True(last.LockoutEnd is null || last.LockoutEnd <= DateTimeOffset.UtcNow);
        var temporaryEnd=DateTimeOffset.UtcNow.AddMinutes(5);
        try
        {
            await host.WithUsers(async manager=>AuditHost.Check(await manager.SetLockoutEndDateAsync((await manager.FindByIdAsync(adminId))!,temporaryEnd)));
            await AuditHost.Post(admin,"/Staff","/Staff/SetLocked",new(){["id"]=adminId,["locked"]="true"});
            await db.Entry(last).ReloadAsync();
            Assert.True(last.LockoutEnd is null || last.LockoutEnd<=temporaryEnd.AddSeconds(1));
        }
        finally {await host.WithUsers(async manager=>AuditHost.Check(await manager.SetLockoutEndDateAsync((await manager.FindByIdAsync(adminId))!,null)));}
    }

    [Fact]
    public async Task Migration_UpgradesLegacyDatabaseWithoutLosingOrder()
    {
        var builder=new SqlConnectionStringBuilder(host.Connection){InitialCatalog="SaigonChe_Audit_"+Guid.NewGuid().ToString("N")};
        var identityServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.IdentityServiceCollectionUIExtensions.AddDefaultIdentity<ApplicationUser>(identityServices);
        using var legacyIdentity = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(identityServices);
        await using var legacy=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).UseApplicationServiceProvider(legacyIdentity).Options);
        try
        {
            await AuditHost.CreateAuditDatabase(builder.ConnectionString, Path.Combine(host.Artifacts,"legacy-database"));
            await legacy.GetService<IMigrator>().MigrateAsync("20261021100513_AddContactMessages");
            await legacy.Database.ExecuteSqlRawAsync("INSERT INTO Categories(Name,Description) VALUES(N'Legacy category',N'Legacy'); DECLARE @c int=SCOPE_IDENTITY(); INSERT INTO Products(Name,Description,Price,IsAvailable,IsFeatured,CategoryId) VALUES(N'Current name',N'Legacy',25000,1,0,@c); DECLARE @p int=SCOPE_IDENTITY(); INSERT INTO Orders(CustomerName,Phone,Address,Total,CreatedAt,Status) VALUES(N'Legacy audit','0900000000',N'Legacy',12345,'2026-01-01',3); DECLARE @o int=SCOPE_IDENTITY(); INSERT INTO OrderItems(OrderId,ProductId,ProductName,Price,Quantity) VALUES(@o,@p,N'Legacy snapshot name',12345,1)");
            await legacy.Database.MigrateAsync();
            var order=await legacy.Orders.Include(item=>item.Items).SingleAsync();
            Assert.Equal("Legacy snapshot name",Assert.Single(order.Items).ProductName);
            Assert.Equal(12345,Assert.Single(order.Items).Price);
            Assert.Equal("Legacy audit",order.CustomerName); Assert.Equal(12345,order.Total);
            Assert.Equal(OrderStatus.Completed,order.Status); Assert.False(order.IsPaid); Assert.Null(order.CheckoutToken);
            Assert.NotEmpty(order.RowVersion);
            Assert.Empty(await legacy.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            Assert.StartsWith("SaigonChe_Audit_",builder.InitialCatalog);
            await legacy.Database.EnsureDeletedAsync();
        }
    }

    [Fact, Trait("Category","Browser")]
    public async Task Browser_RealForms_ResponsiveThemes_And_Print()
    {
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
        var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=1366,Height=900}});
        await context.Tracing.StartAsync(new(){Screenshots=true,Snapshots=true,Sources=false});
        var page=await context.NewPageAsync();
        var errors=new List<string>(); page.PageError+=(_,error)=>errors.Add(error);
        await page.GotoAsync(host.BaseUrl+"/Account/Login");
        await page.Locator("#Email").FillAsync("admin@gmail.com"); await page.Locator("#Password").FillAsync(AuditHost.Password);
        await page.Locator("form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(host.BaseUrl+"/");
        foreach(var width in new[]{1920,1366,1024,768,390})
        foreach(var theme in new[]{"light","dark"})
        {
            await page.SetViewportSizeAsync(width,900);
            await page.EvaluateAsync("theme=>localStorage.setItem('csg-theme',theme)",theme);
            foreach(var route in new[]{"/Admin/Products","/Admin/Categories","/Admin/Orders","/Admin/ContactMessages","/Pos","/OrderOperations","/Party","/OperationsAdmin/Inventory","/OperationsAdmin/DiningTables","/OperationsAdmin/Vouchers","/Staff","/Reports"})
            {
                var response=await page.GotoAsync(host.BaseUrl+route);
                Assert.Equal(200,response!.Status);
                Assert.False(await page.Locator("body").InnerTextAsync().ContinueWith(t=>t.Result.Contains("An unhandled exception")));
                if(route=="/Pos")
                    Assert.True(await page.Locator(".pos-card").First.EvaluateAsync<bool>("node=>{const p=document.createElement('div');p.style.background='var(--card)';document.body.append(p);const ok=getComputedStyle(p).backgroundColor===getComputedStyle(node).backgroundColor;p.remove();return ok;}"));
                if(route=="/Reports") Assert.True(await page.EvaluateAsync<bool>("()=>Boolean(window.Chart)"));
                if(route=="/OrderOperations")
                {
                    if(width>1024)
                    {
                        var sidebar=await page.Locator(".admin-sidebar").BoundingBoxAsync();
                        var main=await page.Locator(".admin-main").BoundingBoxAsync();
                        Assert.InRange(sidebar!.Width,251,253); Assert.True(main!.X>=251);
                    }
                    else
                    {
                        await page.Locator("[data-admin-toggle]").ClickAsync();
                        Assert.Equal("true",await page.Locator("[data-admin-toggle]").GetAttributeAsync("aria-expanded"));
                        await page.Keyboard.PressAsync("Escape");
                        Assert.Equal("false",await page.Locator("[data-admin-toggle]").GetAttributeAsync("aria-expanded"));
                    }
                }
                await page.ScreenshotAsync(new(){Path=Path.Combine(host.Artifacts,$"{width}-{theme}-{route.Replace('/','_')}.png")});
            }
        }
        await page.GotoAsync(host.BaseUrl+"/OrderOperations");
        await page.SetViewportSizeAsync(390,900);
        await page.Locator("[data-admin-toggle]").ClickAsync();
        await page.SetViewportSizeAsync(1366,900);
        await page.WaitForFunctionAsync("()=>!document.body.classList.contains('nav-open')");
        // Operate a real create form and inspect persistence.
        var name="Browser-"+Guid.NewGuid().ToString("N");
        await page.SetViewportSizeAsync(1366,900);
        await page.GotoAsync(host.BaseUrl+"/Admin/CreateCategory");
        await page.Locator("#Name").FillAsync(name);
        await page.Locator("#Description").FillAsync("Browser audit");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await using(var db=host.Db()) Assert.True(await db.Categories.AnyAsync(item=>item.Name==name));
        // Own fixed data: this print assertion must also work when the browser test runs alone.
        var printProduct1=await host.Product();
        var printProduct2=await host.Product();
        await using(var printDb=host.Db())
        {
            printDb.Orders.AddRange(
                new Order {CustomerName="PDF audit one",Phone="0900000000",Address="Audit",Total=25000,
                    Type=OrderType.Pos,Status=OrderStatus.Completed,IsPaid=true,
                    CompletedAt=new DateTime(2031,12,31,16,59,59,DateTimeKind.Utc),
                    Items=[new OrderItem {ProductId=printProduct1.Id,ProductName="Món in thử A",Price=25000,Quantity=1}]},
                new Order {CustomerName="PDF audit two",Phone="0900000000",Address="Audit",Total=50000,
                    Type=OrderType.Pos,Status=OrderStatus.Completed,IsPaid=true,
                    CompletedAt=new DateTime(2031,12,31,17,0,0,DateTimeKind.Utc),
                    Items=[new OrderItem {ProductId=printProduct2.Id,ProductName="Món in thử B",Price=25000,Quantity=2}]});
            await printDb.SaveChangesAsync();
        }
        await page.GotoAsync(host.BaseUrl+"/Reports?from=2031-12-31&to=2032-01-01");
        Assert.Equal(2,await page.Locator(".report-table-card").Last.Locator("tbody tr").CountAsync());
        Assert.Equal("75000",string.Concat((await page.Locator(".stat-value").First.InnerTextAsync()).Where(char.IsDigit)));
        await page.EmulateMediaAsync(new(){Media=Media.Print});
        Assert.Equal("block",await page.Locator(".admin-shell").EvaluateAsync<string>("node=>getComputedStyle(node).display"));
        Assert.Equal("rgb(17, 17, 17)",await page.Locator(".report-chart-card h2").First.EvaluateAsync<string>("node=>getComputedStyle(node).color"));
        await page.WaitForFunctionAsync("()=>document.querySelectorAll('.print-chart-image').length===2 && [...document.querySelectorAll('.print-chart-image')].every(image=>image.complete&&image.naturalWidth>0)");
        foreach(var image in await page.Locator(".print-chart-image").AllAsync())
        {
            var bounds=await image.BoundingBoxAsync();
            var parent=await image.Locator("..").BoundingBoxAsync();
            Assert.True(bounds!.X+bounds.Width<=parent!.X+parent.Width+1);
        }
        var reportPdf=await page.PdfAsync(new(){Path=Path.Combine(host.Artifacts,"report.pdf"),Format="A4",PrintBackground=true});
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(reportPdf),@"/Type\s*/Page\b"));
        Assert.Empty(errors);
        await page.EmulateMediaAsync(new(){Media=Media.Screen});
        // Customer: click add-to-cart, submit checkout and submit a party form.
        var customerContext=await browser.NewContextAsync();
        var customerPage=await customerContext.NewPageAsync();
        customerPage.PageError+=(_,error)=>errors.Add(error);
        await BrowserLogin(customerPage,"audit.customer@example.test");
        var uiProduct=await host.Product(10);
        await customerPage.GotoAsync(host.BaseUrl+"/");
        await customerPage.Locator("#menuSearch").FillAsync(uiProduct.Name);
        await customerPage.WaitForFunctionAsync("()=>document.querySelectorAll('.menu-card-wrapper:not([hidden])').length===1");
        await customerPage.Locator("#menuSearch").FillAsync("audit-no-match-xxxxxxxxx");
        await customerPage.WaitForFunctionAsync("()=>!document.querySelector('.menu-no-results').hidden");
        await customerPage.Locator("#menuSearch").FillAsync("");
        var categoryFilter = $"category-{uiProduct.CategoryId}";
        await customerPage.Locator($".filter-btn[data-filter='{categoryFilter}']").ClickAsync();
        Assert.True(await customerPage.Locator(".menu-card-wrapper:not([hidden])").EvaluateAllAsync<bool>($"nodes=>nodes.length>0 && nodes.every(node=>node.dataset.category==='{categoryFilter}')"));
        await customerPage.Locator(".filter-btn[data-filter='all']").ClickAsync();
        await customerPage.Locator($"form:has(input[name='productId'][value='{uiProduct.Id}']) button").ClickAsync();
        await customerPage.WaitForURLAsync("**/Cart");
        await customerPage.GotoAsync(host.BaseUrl+"/Cart/Checkout");
        var buyer="Browser buyer "+Guid.NewGuid().ToString("N")[..8];
        await customerPage.Locator("#CustomerName").FillAsync(buyer);
        await customerPage.Locator("#Phone").FillAsync("0900000000");
        await customerPage.Locator("#Address").FillAsync("Browser audit address");
        await customerPage.Locator("form.checkout-layout button[type=submit]").ClickAsync();
        await customerPage.WaitForURLAsync(host.BaseUrl+"/");
        await using(var verify=host.Db()) Assert.True(await verify.Orders.AnyAsync(order=>order.CustomerName==buyer));
        await customerPage.GotoAsync(host.BaseUrl+"/Party/Create");
        await customerPage.Locator("#CustomerName").FillAsync(buyer);
        await customerPage.Locator("#Phone").FillAsync("0900000000");
        await customerPage.Locator("#Location").FillAsync("Browser party hall");
        await customerPage.Locator("#EventAt").FillAsync(VietnamTime.ToLocal(DateTime.UtcNow).AddDays(4).ToString("yyyy-MM-ddTHH:mm"));
        await customerPage.Locator("#Headcount").FillAsync("10");
        await customerPage.Locator("main form button[type=submit]").ClickAsync();
        await customerPage.WaitForURLAsync("**/Party/MyRequests");
        await using(var verify=host.Db()) Assert.True(await verify.PartyRequests.AnyAsync(request=>request.CustomerName==buyer));
        await customerPage.ScreenshotAsync(new(){Path=Path.Combine(host.Artifacts,"customer-party.png")});
        await customerContext.CloseAsync();
        // Staff: start a POS draft, add a product and confirm cash through the UI.
        var staffContext=await browser.NewContextAsync(new(){ViewportSize=new(){Width=1366,Height=900}});
        var staffPage=await staffContext.NewPageAsync();
        staffPage.PageError+=(_,error)=>errors.Add(error);
        staffPage.Dialog+=async (_,dialog)=>await dialog.AcceptAsync();
        await BrowserLogin(staffPage,"audit.staff@example.test");
        await staffPage.Locator("[data-user-toggle]").ClickAsync();
        Assert.True(await staffPage.GetByRole(AriaRole.Menuitem,new(){Name="Khu vực vận hành",Exact=true}).IsVisibleAsync());
        await staffPage.GetByRole(AriaRole.Menuitem,new(){Name="Khu vực vận hành",Exact=true}).ClickAsync();
        await staffPage.Locator("form[action='/Pos/Start'] button").ClickAsync();
        await staffPage.WaitForURLAsync("**/Pos/Order/*");
        var posId=int.Parse(System.Text.RegularExpressions.Regex.Match(staffPage.Url,"(\\d+)$").Value);
        await staffPage.Locator("form[action='/Pos/AddItem'] select[name='ProductId']").SelectOptionAsync(uiProduct.Id.ToString());
        await staffPage.Locator("form[action='/Pos/AddItem'] input[name='Quantity']").FillAsync("1");
        await staffPage.Locator("form[action='/Pos/AddItem'] button[type=submit]").ClickAsync();
        await staffPage.SetViewportSizeAsync(390,900);
        await staffPage.ScreenshotAsync(new(){Path=Path.Combine(host.Artifacts,"staff-pos-order-mobile.png")});
        Assert.True(await staffPage.Locator(".pos-wrap").EvaluateAsync<bool>("node=>node.scrollWidth<=node.clientWidth+1"),
            await staffPage.Locator(".pos-wrap").EvaluateAsync<string>("node=>JSON.stringify([...node.querySelectorAll('*')].filter(e=>e.getBoundingClientRect().right>node.getBoundingClientRect().right+1).map(e=>({tag:e.tagName,class:e.className,width:e.getBoundingClientRect().width})))"));
        await staffPage.SetViewportSizeAsync(1366,900);
        await staffPage.ScreenshotAsync(new(){Path=Path.Combine(host.Artifacts,"staff-pos-order-desktop.png")});
        await staffPage.Locator("#posPayForm input[name='amountReceived']").FillAsync("30000");
        await staffPage.Locator("#posPayForm button[type=submit]").ClickAsync();
        await staffPage.WaitForURLAsync("**/Pos/Receipt/*");
        await using(var verify=host.Db()) Assert.True(await verify.Orders.Where(order=>order.Id==posId).Select(order=>order.IsPaid).SingleAsync());
        await staffPage.ScreenshotAsync(new(){Path=Path.Combine(host.Artifacts,"staff-receipt.png")});
        await staffPage.PdfAsync(new(){Path=Path.Combine(host.Artifacts,"receipt.pdf"),Format="A4",PrintBackground=true});
        await staffContext.CloseAsync();
        Assert.Empty(errors);
        await context.Tracing.StopAsync(new(){Path=Path.Combine(host.Artifacts,"browser-trace.zip")});
        await context.CloseAsync();
    }
    private async Task BrowserLogin(IPage page, string email)
    {
        await page.GotoAsync(host.BaseUrl+"/Account/Login");
        await page.Locator("#Email").FillAsync(email);
        await page.Locator("#Password").FillAsync(AuditHost.Password);
        await page.Locator("form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(host.BaseUrl+"/");
    }

    [Fact]
    public async Task ConcurrentTableAndLastVoucher_KeepUniqueBusinessState()
    {
        using var one=await host.Login("audit.staff@example.test");
        using var two=await host.Login("audit.staff@example.test");
        await using var db=host.Db();
        var table=new DiningTable{Code="T"+Guid.NewGuid().ToString("N")[..8],Name="Audit table",Capacity=4,IsActive=true};
        var voucher=new Voucher{Code="V"+Guid.NewGuid().ToString("N")[..8],Type=VoucherType.FixedAmount,Amount=1000,
            IsActive=true,ValidFrom=DateTime.UtcNow.AddDays(-1),ValidTo=DateTime.UtcNow.AddDays(1),UseLimit=1};
        db.DiningTables.Add(table);db.Vouchers.Add(voucher);await db.SaveChangesAsync();
        async Task<HttpResponseMessage> Start(HttpClient client)=>await AuditHost.Post(client,"/Pos","/Pos/Start",new(){["tableId"]=table.Id.ToString()});
        var opened=await Task.WhenAll(Start(one),Start(two));
        foreach(var response in opened)Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Conflict,$"Table HTTP {(int)response.StatusCode}");
        Assert.Equal(1,await db.Orders.CountAsync(item=>item.TableId==table.Id&&!item.IsPaid&&item.Status!=OrderStatus.Cancelled));
        var product=await host.Product(10);
        var first=await host.Pos(one,product);var second=await host.Pos(two,product);
        async Task SetVoucher(HttpClient client,int id)
        {
            var html=await client.GetStringAsync("/Pos/Order/"+id);
            await AuditHost.Post(client,"/Pos/Order/"+id,"/Pos/Voucher",new(){["orderId"]=id.ToString(),["voucherId"]=voucher.Id.ToString(),["rowVersion"]=AuditHost.Field(html,"rowVersion")});
        }
        await SetVoucher(one,first);await SetVoucher(two,second);
        async Task<HttpResponseMessage> Pay(HttpClient client,int id)
        {
            var html=await client.GetStringAsync("/Pos/Order/"+id);
            return await AuditHost.Post(client,"/Pos/Order/"+id,"/Pos/Pay",new(){["orderId"]=id.ToString(),["rowVersion"]=AuditHost.Field(html,"rowVersion"),
                ["method"]=((int)PaymentMethod.Cash).ToString(),["amountReceived"]="30000"});
        }
        var paid=await Task.WhenAll(Pay(one,first),Pay(two,second));
        foreach(var response in paid)Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Conflict,$"Voucher payment HTTP {(int)response.StatusCode}");
        await db.Entry(voucher).ReloadAsync();Assert.Equal(1,voucher.UseCount);
        Assert.Equal(1,await db.Orders.CountAsync(item=>(item.Id==first||item.Id==second)&&item.IsPaid));
        Assert.Equal(9,await db.Products.Where(item=>item.Id==product.Id).Select(item=>item.Stock).SingleAsync());
    }

    [Fact]
    public async Task OnlineTransitions_Cancellation_And_DisabledVietQrAreEnforced()
    {
        using var staff=await host.Login("audit.staff@example.test");
        using var admin=await host.Login("admin@gmail.com");
        var product=await host.Product(5);
        await using var db=host.Db();
        var order=new Order{CustomerName="Audit online workflow",Phone="0900000000",Address="Audit",Type=OrderType.Online,
            Status=OrderStatus.Pending,PaymentMethod=PaymentMethod.CashOnDelivery,Total=25000,
            Items=[new OrderItem{ProductId=product.Id,ProductName=product.Name,Price=25000,Quantity=1}]};
        db.Orders.Add(order);await db.SaveChangesAsync();
        var stale=Convert.ToBase64String(order.RowVersion);
        foreach(var next in new[]{OrderStatus.Confirmed,OrderStatus.Preparing,OrderStatus.Completed})
        {
            await db.Entry(order).ReloadAsync();
            var response=await AuditHost.Post(staff,"/OrderOperations","/OrderOperations/Update",new(){["OrderId"]=order.Id.ToString(),
                ["RowVersion"]=Convert.ToBase64String(order.RowVersion),["Status"]=((int)next).ToString()});
            Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        }
        await db.Entry(order).ReloadAsync();Assert.True(order.IsPaid);Assert.Equal(OrderStatus.Completed,order.Status);
        await AuditHost.Post(admin,"/Admin/Orders","/Admin/UpdateOrderStatus",new(){["OrderId"]=order.Id.ToString(),["RowVersion"]=stale,["Status"]="4",["Note"]="stale"});
        await db.Entry(order).ReloadAsync();Assert.Equal(OrderStatus.Completed,order.Status);
        Assert.Equal(1,await db.InventoryMovements.CountAsync(item=>item.OrderId==order.Id));
        var pos=await host.Pos(staff,product);
        var html=await staff.GetStringAsync("/Pos/Order/"+pos);
        await AuditHost.Post(staff,"/Pos/Order/"+pos,"/Pos/Pay",new(){["orderId"]=pos.ToString(),["rowVersion"]=AuditHost.Field(html,"rowVersion"),
            ["method"]=((int)PaymentMethod.VietQr).ToString()});
        Assert.False(await db.Orders.Where(item=>item.Id==pos).Select(item=>item.IsPaid).SingleAsync());
        await AuditHost.Post(staff,"/Pos/Order/"+pos,"/Pos/Cancel",new(){["orderId"]=pos.ToString(),["rowVersion"]=AuditHost.Field(html,"rowVersion")});
        Assert.Equal(OrderStatus.Cancelled,await db.Orders.Where(item=>item.Id==pos).Select(item=>item.Status).SingleAsync());
    }

    [Fact]
    public async Task ApiRemainsReadOnly_And_DatabaseOutageReturnsFriendly503()
    {
        using var api=new HttpClient{BaseAddress=new Uri(host.ApiUrl)};
        Assert.Equal(HttpStatusCode.OK,(await api.GetAsync("/api/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await api.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await api.GetAsync("/api/products/2147483647")).StatusCode);
        foreach(var method in new[]{HttpMethod.Post,HttpMethod.Put,HttpMethod.Delete,HttpMethod.Patch})
            Assert.Equal(HttpStatusCode.NotFound,(await api.SendAsync(new HttpRequestMessage(method,"/api/categories/1"))).StatusCode);
        var connection=new SqlConnectionStringBuilder(host.Connection){InitialCatalog="master"};
        Assert.StartsWith("SaigonChe_Audit_",host.DatabaseName);
        await using var sql=new SqlConnection(connection.ConnectionString);await sql.OpenAsync();
        await using var command=sql.CreateCommand();
        try
        {
            command.CommandText=$"ALTER DATABASE [{host.DatabaseName}] SET OFFLINE WITH ROLLBACK IMMEDIATE";
            await command.ExecuteNonQueryAsync();
            using var web=host.Client();var response=await web.GetAsync("/");
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SqlException",body);Assert.DoesNotContain(host.Connection,body);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await api.GetAsync("/api/products")).StatusCode);
        }
        finally
        {
            command.CommandText=$"ALTER DATABASE [{host.DatabaseName}] SET ONLINE";await command.ExecuteNonQueryAsync();
        }
        using var healthy=host.Client();Assert.Equal(HttpStatusCode.OK,(await healthy.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task CustomerCanChangeOwnPassword_And_EmptyCatalogDoesNotInventProducts()
    {
        var email="self."+Guid.NewGuid().ToString("N")+"@example.test";
        await host.WithUsers(async manager=>{var user=new ApplicationUser{UserName=email,Email=email,EmailConfirmed=true};
            AuditHost.Check(await manager.CreateAsync(user,AuditHost.Password));AuditHost.Check(await manager.AddToRoleAsync(user,"Customer"));});
        using var client=await host.Login(email);
        var changed=await AuditHost.Post(client,"/Identity/Account/Manage/ChangePassword","/Identity/Account/Manage/ChangePassword",
            new(){["Input.OldPassword"]=AuditHost.Password,["Input.NewPassword"]="Own!2026_Changed",["Input.ConfirmPassword"]="Own!2026_Changed"});
        Assert.Equal(HttpStatusCode.Redirect,changed.StatusCode);
        using var fresh=await host.Login(email,"Own!2026_Changed");
        await AuditHost.Post(fresh,"/Identity/Account/Manage","/Identity/Account/Manage",new(){["Input.PhoneNumber"]="0900000001"});
        await using var db=host.Db();
        Assert.Equal("0900000001",await db.Users.Where(item=>item.Email==email).Select(item=>item.PhoneNumber).SingleAsync());var active=await db.Products.Where(item=>item.IsAvailable).Select(item=>item.Id).ToListAsync();
        try
        {
            await db.Products.Where(item=>item.IsAvailable).ExecuteUpdateAsync(set=>set.SetProperty(item=>item.IsAvailable,false));
            var home=await fresh.GetStringAsync("/");
            Assert.Contains("Thực đơn đang được cập nhật",home);
            Assert.DoesNotContain("menu-card-wrapper",home);
        }
        finally {await db.Products.Where(item=>active.Contains(item.Id)).ExecuteUpdateAsync(set=>set.SetProperty(item=>item.IsAvailable,true));}
    }

    [Fact]
    public async Task Catalog_ImageReplacement_Delete_And_FileFailureAreConsistent()
    {
        using var admin=await host.Login("admin@gmail.com");
        await using var db=host.Db();
        var name="Image-"+Guid.NewGuid().ToString("N");
        var category=await db.Categories.FirstAsync();
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=");
        async Task<HttpResponseMessage> Upload(string form,string target,int id=0)
        {
            var html=await admin.GetStringAsync(form);
            using var content=new MultipartFormDataContent();
            foreach(var pair in new Dictionary<string,string>{["Id"]=id.ToString(),["Name"]=name,["Description"]="Image audit",
                ["Price"]="25000",["CategoryId"]=category.Id.ToString(),["IsAvailable"]="true",["__RequestVerificationToken"]=AuditHost.Field(html,"__RequestVerificationToken")})
                content.Add(new StringContent(pair.Value),pair.Key);
            var image=new ByteArrayContent(png);image.Headers.ContentType=new MediaTypeHeaderValue("image/png");content.Add(image,"ImageFile","audit.png");
            return await admin.PostAsync(target,content);
        }
        Assert.Equal(HttpStatusCode.Redirect,(await Upload("/Admin/CreateProduct","/Admin/CreateProduct")).StatusCode);
        var product=await db.Products.SingleAsync(item=>item.Name==name);
        string FileFor(string url)=>Path.Combine(host.WebRoot,url.TrimStart('/').Replace('/',Path.DirectorySeparatorChar));
        var old=FileFor(product.ImageUrl!);Assert.True(File.Exists(old));
        Assert.Equal(HttpStatusCode.Redirect,(await Upload("/Admin/EditProduct/"+product.Id,"/Admin/EditProduct",product.Id)).StatusCode);
        await db.Entry(product).ReloadAsync();Assert.False(File.Exists(old));Assert.True(File.Exists(FileFor(product.ImageUrl!)));
        var blockedCategory=await AuditHost.Post(admin,"/Admin/Categories","/Admin/DeleteCategory",new(){["id"]=category.Id.ToString()});
        Assert.Equal(HttpStatusCode.Redirect,blockedCategory.StatusCode);Assert.NotNull(await db.Categories.FindAsync(category.Id));
        var imagePath=FileFor(product.ImageUrl!);
        await AuditHost.Post(admin,"/Admin/Products","/Admin/DeleteProduct",new(){["id"]=product.Id.ToString()});
        Assert.False(await db.Products.AnyAsync(item=>item.Id==product.Id));Assert.False(File.Exists(imagePath));
        var uploadDirectory=Path.Combine(host.WebRoot,"uploads","products");
        // Force a database rejection after the file was saved, only in the audit database.
        Assert.StartsWith("SaigonChe_Audit_",host.DatabaseName);
        var filesBeforeFailure=Directory.GetFiles(uploadDirectory).OrderBy(item=>item).ToArray();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.AuditRejectProduct ON dbo.Products AFTER INSERT AS BEGIN THROW 51000, 'Audit rejection', 1; END");
        try
        {
            Assert.Equal(HttpStatusCode.OK,(await Upload("/Admin/CreateProduct","/Admin/CreateProduct")).StatusCode);
            Assert.False(await db.Products.AnyAsync(item=>item.Name==name));
            Assert.Equal(filesBeforeFailure,Directory.GetFiles(uploadDirectory).OrderBy(item=>item).ToArray());
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.AuditRejectProduct");}
        // Simulate filesystem failure only inside this fixture's upload root.
        Directory.Delete(uploadDirectory,true);
        await File.WriteAllTextAsync(uploadDirectory,"block directory creation");
        try
        {
            var response=await Upload("/Admin/CreateProduct","/Admin/CreateProduct");
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.False(await db.Products.AnyAsync(item=>item.Name==name));
        }
        finally {File.Delete(uploadDirectory);}
    }

    [Fact]
    public async Task InventoryAndCategoryCrud_RejectInvalidUpdates_AndPersistValidOnes()
    {
        using var admin=await host.Login("admin@gmail.com");
        var product=await host.Product(10);
        await AuditHost.Post(admin,"/OperationsAdmin/Inventory","/OperationsAdmin/AdjustStock",new(){["ProductId"]=product.Id.ToString(),["QuantityChange"]="-11",["Reason"]="negative"});
        await using var db=host.Db();Assert.Equal(10,await db.Products.Where(item=>item.Id==product.Id).Select(item=>item.Stock).SingleAsync());
        await AuditHost.Post(admin,"/OperationsAdmin/Inventory","/OperationsAdmin/AdjustStock",new(){["ProductId"]=product.Id.ToString(),["QuantityChange"]="5",["Reason"]="valid audit adjustment"});
        Assert.Equal(15,await db.Products.Where(item=>item.Id==product.Id).Select(item=>item.Stock).SingleAsync());
        Assert.Single(await db.InventoryMovements.Where(item=>item.ProductId==product.Id).ToListAsync());
        var name="Category-"+Guid.NewGuid().ToString("N");
        await AuditHost.Post(admin,"/Admin/CreateCategory","/Admin/CreateCategory",new(){["Name"]=name,["Description"]=""});
        var category=await db.Categories.SingleAsync(item=>item.Name==name);
        Assert.Equal(HttpStatusCode.OK,(await AuditHost.Post(admin,"/Admin/CreateCategory","/Admin/CreateCategory",new(){["Name"]=name})).StatusCode);
        await AuditHost.Post(admin,"/Admin/EditCategory/"+category.Id,"/Admin/EditCategory",new(){["Id"]=category.Id.ToString(),["Name"]=name+" edited",["Description"]="edited"});
        await db.Entry(category).ReloadAsync();Assert.EndsWith(" edited",category.Name);
        await AuditHost.Post(admin,"/Admin/Categories","/Admin/DeleteCategory",new(){["id"]=category.Id.ToString()});
        Assert.False(await db.Categories.AnyAsync(item=>item.Id==category.Id));
        var concurrentName="Concurrent-"+Guid.NewGuid().ToString("N");
        using var second=await host.Login("admin@gmail.com");
        var responses=await Task.WhenAll(
            AuditHost.Post(admin,"/Admin/CreateCategory","/Admin/CreateCategory",new(){["Name"]=concurrentName}),
            AuditHost.Post(second,"/Admin/CreateCategory","/Admin/CreateCategory",new(){["Name"]=concurrentName}));
        foreach(var response in responses) Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Conflict);
        Assert.Equal(1,await db.Categories.CountAsync(item=>item.Name==concurrentName));
    }

    [Fact]
    public async Task ConfiguredVietQr_ManualConfirmationStoresPaymentAndDeductsOnce()
    {
        var product=await host.Product(3);
        await host.WithQrHost(async client=>
        {
            var id=await host.Pos(client,product);
            var html=await client.GetStringAsync("/Pos/Order/"+id);
            Assert.Contains("img.vietqr.io/image/970436-0000000000",html);
            Assert.Contains("amount=25000",html);
            var paid=await AuditHost.Post(client,"/Pos/Order/"+id,"/Pos/Pay",new(){["orderId"]=id.ToString(),
                ["rowVersion"]=AuditHost.Field(html,"rowVersion"),["method"]=((int)PaymentMethod.VietQr).ToString()});
            Assert.Equal(HttpStatusCode.Redirect,paid.StatusCode);
            await using var db=host.Db();var order=await db.Orders.SingleAsync(item=>item.Id==id);
            Assert.True(order.IsPaid);Assert.Equal(PaymentMethod.VietQr,order.PaymentMethod);
            Assert.NotNull(order.PaidAt);Assert.NotNull(order.AssignedStaffId);
            Assert.Equal(2,await db.Products.Where(item=>item.Id==product.Id).Select(item=>item.Stock).SingleAsync());
        });
    }

    [Fact]
    public async Task Seeder_HandlesAnExistingCustomCategoryWithNoProducts()
    {
        var connection=new SqlConnectionStringBuilder(host.Connection){InitialCatalog="SaigonChe_Audit_"+Guid.NewGuid().ToString("N")};
        await AuditHost.CreateAuditDatabase(connection.ConnectionString,Path.Combine(host.Artifacts,"seed-database"));
        var services=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(services);
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(services,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Seed:AdminPassword",AuditHost.Password}}).Build());
        services.AddDbContext<ApplicationDbContext>(options=>options.UseSqlServer(connection.ConnectionString));
        services.AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider=services.BuildServiceProvider();
        using var scope=provider.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            db.Categories.Add(new Category{Name="Custom retained",Description="Audit"});
            await db.SaveChangesAsync();
            await DbSeeder.SeedAsync(scope.ServiceProvider);
            Assert.True(await db.Categories.AnyAsync(item=>item.Name=="Custom retained"));
            Assert.Equal(8,await db.Products.CountAsync());
        }
        finally
        {
            Assert.StartsWith("SaigonChe_Audit_",connection.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task ContactForm_InboxReadAndDelete_PersistThroughHttp()
    {
        using var customer=host.Client();
        var marker="Contact-"+Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Redirect,(await AuditHost.Post(customer,"/","/Home/Contact",new(){["FullName"]=marker,["Contact"]="audit@example.test",["Message"]="Audit message"})).StatusCode);
        await using var db=host.Db();var message=await db.ContactMessages.SingleAsync(item=>item.FullName==marker);
        using var admin=await host.Login("admin@gmail.com");
        await AuditHost.Post(admin,"/Admin/ContactMessages","/Admin/MarkContactMessageRead",new(){["id"]=message.Id.ToString()});
        await db.Entry(message).ReloadAsync();Assert.True(message.IsRead);
        await AuditHost.Post(admin,"/Admin/ContactMessages","/Admin/DeleteContactMessage",new(){["id"]=message.Id.ToString()});
        Assert.False(await db.ContactMessages.AnyAsync(item=>item.Id==message.Id));
        await AuditHost.Post(customer,"/","/Home/Contact",new(){["FullName"]=marker,["Contact"]="",["Message"]=""});
        Assert.False(await db.ContactMessages.AnyAsync(item=>item.FullName==marker));
    }

    [Fact]
    public async Task StaffCreateEdit_AssignsOnlyStaff_AndRevokesOldSession()
    {
        using var admin=await host.Login("admin@gmail.com");
        var email="staff.create."+Guid.NewGuid().ToString("N")+"@example.test";
        var created=await AuditHost.Post(admin,"/Staff/Create","/Staff/Create",new(){["Email"]=email,["Password"]=AuditHost.Password,["ConfirmPassword"]=AuditHost.Password});
        Assert.Equal(HttpStatusCode.Redirect,created.StatusCode);
        await using var db=host.Db();var user=await db.Users.SingleAsync(item=>item.Email==email);
        await host.WithUsers(async manager=>Assert.Equal(new[]{"Staff"},await manager.GetRolesAsync((await manager.FindByIdAsync(user.Id))!)));
        using var old=await host.Login(email);
        var duplicate=await AuditHost.Post(admin,"/Staff/Edit/"+user.Id,"/Staff/Edit",new(){["Id"]=user.Id,["Email"]="audit.customer@example.test"});
        Assert.Equal(HttpStatusCode.OK,duplicate.StatusCode);
        var changed="edited."+email;
        await AuditHost.Post(admin,"/Staff/Edit/"+user.Id,"/Staff/Edit",new(){["Id"]=user.Id,["Email"]=changed});
        await db.Entry(user).ReloadAsync();Assert.Equal(changed,user.Email);
        Assert.Equal(HttpStatusCode.Redirect,(await old.GetAsync("/Pos")).StatusCode);
        using var current=await host.Login(changed);
        Assert.Equal(HttpStatusCode.OK,(await current.GetAsync("/Pos")).StatusCode);
    }

    [Fact]
    public async Task ConfirmedPartyCancellation_RequiresReason_AndDoesNotDeductStock()
    {
        var product=await host.Product(10);await using var db=host.Db();
        var owner=await db.Users.Where(item=>item.Email=="audit.customer@example.test").Select(item=>item.Id).SingleAsync();
        var order=new Order{CustomerName="Cancelled party",Phone="0900000000",Address="Audit hall",Type=OrderType.Party,Total=25000,
            Items=[new OrderItem{ProductId=product.Id,ProductName=product.Name,Price=25000,Quantity=1}]};
        var request=new PartyRequest{CustomerName=order.CustomerName,Phone=order.Phone,Location=order.Address,Headcount=10,
            EventAt=DateTime.UtcNow.AddDays(3),CustomerUserId=owner,Status=PartyRequestStatus.Confirmed,QuoteAmount=25000,Order=order};
        db.PartyRequests.Add(request);await db.SaveChangesAsync();
        using var staff=await host.Login("audit.staff@example.test");
        Dictionary<string,string> values=new(){["Id"]=request.Id.ToString(),["RowVersion"]=Convert.ToBase64String(request.RowVersion),
            ["Status"]="Cancelled",["QuoteAmount"]="25000"};
        Assert.Equal(HttpStatusCode.OK,(await AuditHost.Post(staff,"/Party/Details/"+request.Id,"/Party/Update",values)).StatusCode);
        await db.Entry(request).ReloadAsync();Assert.Equal(PartyRequestStatus.Confirmed,request.Status);
        values["CancellationReason"]="Audit cancellation";
        await AuditHost.Post(staff,"/Party/Details/"+request.Id,"/Party/Update",values);
        await db.Entry(request).ReloadAsync();await db.Entry(order).ReloadAsync();
        Assert.Equal(PartyRequestStatus.Cancelled,request.Status);Assert.Equal(OrderStatus.Cancelled,order.Status);
        Assert.Empty(await db.InventoryMovements.Where(item=>item.OrderId==order.Id).ToListAsync());
        Assert.Equal(10,await db.Products.Where(item=>item.Id==product.Id).Select(item=>item.Stock).SingleAsync());
    }

}
