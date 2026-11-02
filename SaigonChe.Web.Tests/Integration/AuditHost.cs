using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace SaigonChe.Web.Tests.Integration;

public sealed class AuditHost : IAsyncLifetime
{
    public const string Password = "Audit!2026_TestOnly";
    public string Root { get; private set; } = "";
    public string Artifacts { get; private set; } = "";
    public string Connection { get; private set; } = "";
    public string DatabaseName { get; private set; } = "";
    public string WebRoot { get; private set; } = "";
    public string BaseUrl { get; private set; } = "";
    public string ApiUrl { get; private set; } = "";
    private Process? apiProcess;
    private Process? process;
    private ServiceProvider? identityProvider;
    private readonly StringBuilder log = new();
    public ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(Connection).UseApplicationServiceProvider(identityProvider!).Options);

    public async Task InitializeAsync()
    {
        Root = Environment.GetEnvironmentVariable("SAIGONCHE_AUDIT_ROOT") ?? FindRoot();
        Artifacts = Path.Combine(Root, "artifacts", "audit", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(Artifacts);
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(Root,"artifacts","playwright"));
        WebRoot = Path.Combine(Artifacts, "wwwroot");
        CopyDirectory(Path.Combine(Root, "SaigonChe.Web", "wwwroot"), WebRoot);
        DatabaseName = "SaigonChe_Audit_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SAIGONCHE_TEST_SQL_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;");
        connection.InitialCatalog = DatabaseName;
        Connection = connection.ConnectionString;
        var identityServices = new ServiceCollection();
        identityServices.AddLogging();
        identityServices.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Connection));
        identityServices.AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        identityProvider = identityServices.BuildServiceProvider();
        await CreateAuditDatabase(Connection, Path.Combine(Artifacts, "database"));
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        BaseUrl = "http://127.0.0.1:" + port;
        var webDll = Path.Combine(Root, "SaigonChe.Web", "bin", "Debug", "net10.0", "SaigonChe.Web.dll");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(Root, "SaigonChe.Web"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(webDll);
        start.ArgumentList.Add("--urls"); start.ArgumentList.Add(BaseUrl);
        start.ArgumentList.Add("--webroot"); start.ArgumentList.Add(WebRoot);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ConnectionStrings__DefaultConnection"] = Connection;
        start.Environment["Seed__AdminPassword"] = Password;
        start.Environment["Payment__BankId"] = "";
        start.Environment["Payment__AccountNumber"] = "";
        start.Environment["Payment__AccountName"] = "";
        process = Process.Start(start) ?? throw new Exception("Could not start audit host.");
        process.OutputDataReceived += (_, e) => { lock (log) { if (e.Data != null) log.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { lock (log) { if (e.Data != null) log.AppendLine(e.Data); } };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var probe = Client();
        var ready = false;
        for (var attempt = 0; attempt < 120; attempt++)
        {
            if (process.HasExited) throw new Exception("Audit host exited: " + log);
            try { using var response = await probe.GetAsync("/"); if (response.IsSuccessStatusCode) { ready = true; break; } }
            catch (HttpRequestException) { }
            await Task.Delay(500);
        }
        if (!ready) throw new Exception("Audit host did not become ready: " + log);
        await StartApi();
        await WithUsers(async manager =>
        {
            foreach (var email in new[] { "audit.staff@example.test", "audit.customer@example.test", "audit.other@example.test" })
            {
                var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                Check(await manager.CreateAsync(user, Password));
                Check(await manager.AddToRoleAsync(user, email.Contains("staff") ? "Staff" : "Customer"));
            }
        });
        await File.WriteAllTextAsync(Path.Combine(Artifacts, "environment.txt"), $"Database={DatabaseName}\nHost={BaseUrl}\n");
    }

    public async Task WithUsers(Func<UserManager<ApplicationUser>, Task> action)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Connection));
        services.AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }
    public static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new Exception(string.Join("; ", result.Errors.Select(item => item.Description)));
    }
    public HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(40) };
    public async Task<HttpClient> Login(string email, string? password = null)
    {
        var client = Client();
        var response = await Post(client, "/Account/Login", "/Account/Login",
            new() { ["Email"] = email, ["Password"] = password ?? Password, ["RememberMe"] = "false" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }
    public static string Field(string html, string name)
    {
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
            if (Regex.IsMatch(input.Value, "\\bname=[\"']" + Regex.Escape(name) + "[\"']", RegexOptions.IgnoreCase))
            {
                var value = Regex.Match(input.Value, "\\bvalue=[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
                if (value.Success) return WebUtility.HtmlDecode(value.Groups[1].Value);
            }
        throw new Exception("Field missing: " + name);
    }
    public static async Task<HttpResponseMessage> Post(HttpClient client, string form, string target, Dictionary<string,string> values)
    {
        var html = await client.GetStringAsync(form);
        values["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");
        return await client.PostAsync(target, new FormUrlEncodedContent(values));
    }
    public async Task<Product> Product(int stock = 10)
    {
        await using var db = Db();
        var product = new Product { Name = "Audit-" + Guid.NewGuid().ToString("N")[..10], Description = "Audit product",
            CategoryId = await db.Categories.Select(item => item.Id).FirstAsync(), Price = 25000, Stock = stock, IsAvailable = true };
        db.Products.Add(product); await db.SaveChangesAsync(); return product;
    }
    public async Task<int> Pos(HttpClient client, Product product, int? tableId = null)
    {
        var response = await Post(client, "/Pos", "/Pos/Start", tableId.HasValue ? new() { ["tableId"] = tableId.Value.ToString() } : new());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = int.Parse(Regex.Match(response.Headers.Location!.ToString(), "(\\d+)$").Value);
        var html = await client.GetStringAsync("/Pos/Order/" + id);
        var added = await Post(client, "/Pos/Order/" + id, "/Pos/AddItem", new() {
            ["OrderId"] = id.ToString(), ["ProductId"] = product.Id.ToString(), ["Quantity"] = "1",
            ["RowVersion"] = Field(html, "rowVersion"), ["Notes"] = "" });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        return id;
    }
    public async Task DisposeAsync()
    {
        if (apiProcess is { HasExited: false }) { apiProcess.Kill(true); await apiProcess.WaitForExitAsync(); }
        if (process is { HasExited: false }) { process.Kill(true); await process.WaitForExitAsync(); }
        await File.WriteAllTextAsync(Path.Combine(Artifacts, "host.log"), log.ToString());
        SqlConnection.ClearAllPools();
        if (!DatabaseName.StartsWith("SaigonChe_Audit_", StringComparison.Ordinal) || !Regex.IsMatch(DatabaseName, "^SaigonChe_Audit_[a-f0-9]{32}$"))
            throw new Exception("Refusing to remove a non-audit database.");
        await using var db = Db();
        await db.Database.EnsureDeletedAsync();
        identityProvider?.Dispose();
    }
    public async Task WithQrHost(Func<HttpClient,Task> action)
    {
        var listener=new TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var url="http://127.0.0.1:"+port;
        var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(Root,"SaigonChe.Web"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.Combine(Root,"SaigonChe.Web","bin","Debug","net10.0","SaigonChe.Web.dll"));
        start.ArgumentList.Add("--urls");start.ArgumentList.Add(url);
        start.ArgumentList.Add("--webroot");start.ArgumentList.Add(WebRoot);
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["ConnectionStrings__DefaultConnection"]=Connection;start.Environment["Seed__AdminPassword"]=Password;
        start.Environment["Payment__BankId"]="970436";start.Environment["Payment__AccountNumber"]="0000000000";start.Environment["Payment__AccountName"]="AUDIT TEST";
        using var qr=Process.Start(start) ?? throw new Exception("Could not start configured QR audit host.");
        var output=qr.StandardOutput.ReadToEndAsync();var error=qr.StandardError.ReadToEndAsync();
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new CookieContainer()}){BaseAddress=new Uri(url)};
        try
        {
            var ready=false;
            for(var attempt=0;attempt<100;attempt++){if(qr.HasExited)throw new Exception("QR audit host exited.");try{using var response=await client.GetAsync("/");if(response.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(200);}
            Assert.True(ready,"Configured QR host readiness");
            var login=await Post(client,"/Account/Login","/Account/Login",new(){["Email"]="audit.staff@example.test",["Password"]=Password});
            Assert.Equal(HttpStatusCode.Redirect,login.StatusCode);
            await action(client);
        }
        finally {if(!qr.HasExited){qr.Kill(true);await qr.WaitForExitAsync();}await File.WriteAllTextAsync(Path.Combine(Artifacts,"qr-host.log"),await output+await error);}
    }
    public static async Task CreateAuditDatabase(string connection, string directory)
    {
        var builder = new SqlConnectionStringBuilder(connection);
        var name = builder.InitialCatalog;
        if (!Regex.IsMatch(name, "^SaigonChe_Audit_[a-f0-9]{32}$")) throw new Exception("Refusing to create non-audit database.");
        Directory.CreateDirectory(directory);
        var mdf = Path.Combine(directory, name + ".mdf").Replace("'", "''");
        var ldf = Path.Combine(directory, name + ".ldf").Replace("'", "''");
        builder.InitialCatalog = "master";
        await using var sql = new SqlConnection(builder.ConnectionString);
        await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}] ON PRIMARY (NAME=N'{name}',FILENAME=N'{mdf}',SIZE=8MB,FILEGROWTH=8MB) LOG ON (NAME=N'{name}_log',FILENAME=N'{ldf}',SIZE=8MB,FILEGROWTH=8MB)";
        await command.ExecuteNonQueryAsync();
    }
    private async Task StartApi()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        ApiUrl = "http://127.0.0.1:" + port;
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(Root, "SaigonChe.Api"),
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        start.ArgumentList.Add(Path.Combine(Root,"SaigonChe.Api","bin","Debug","net10.0","SaigonChe.Api.dll"));
        start.ArgumentList.Add("--urls"); start.ArgumentList.Add(ApiUrl);
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
        start.Environment["ConnectionStrings__DefaultConnection"]=Connection;
        apiProcess=Process.Start(start) ?? throw new Exception("Could not start API audit host.");
        apiProcess.OutputDataReceived+=(_,e)=>{lock(log){if(e.Data!=null)log.AppendLine("API "+e.Data);}};
        apiProcess.ErrorDataReceived+=(_,e)=>{lock(log){if(e.Data!=null)log.AppendLine("API "+e.Data);}};
        apiProcess.BeginOutputReadLine();apiProcess.BeginErrorReadLine();
        using var client=new HttpClient();
        for(var i=0;i<100;i++)
        {
            if(apiProcess.HasExited)throw new Exception("API audit host exited: "+log);
            try {using var response=await client.GetAsync(ApiUrl+"/api/categories");if(response.IsSuccessStatusCode)return;}catch(HttpRequestException){}
            await Task.Delay(200);
        }
        throw new Exception("API audit host did not become ready.");
    }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SaigonChe.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new Exception("Set SAIGONCHE_AUDIT_ROOT to the repository.");
    }
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
}
