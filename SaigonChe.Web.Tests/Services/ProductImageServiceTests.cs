using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SaigonChe.Web.Services;
using Xunit;
namespace SaigonChe.Web.Tests.Services;

public sealed class ProductImageServiceTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=");
    [Fact]
    public async Task ValidImage_SaveDeleteAndTraversalAreContained()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"image-audit",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var service=new ProductImageService(new EnvironmentStub{WebRootPath=path},NullLogger<ProductImageService>.Instance);
        try
        {
            var url=await service.SaveAsync(File(Png,"ok.png","image/png"));
            var saved=Path.Combine(path,url.TrimStart('/').Replace('/',Path.DirectorySeparatorChar));
            Assert.True(System.IO.File.Exists(saved));
            var outside=Path.Combine(path,"outside.txt");await System.IO.File.WriteAllTextAsync(outside,"keep");
            service.Delete("/uploads/products/../../outside.txt");Assert.True(System.IO.File.Exists(outside));
            service.Delete(url);Assert.False(System.IO.File.Exists(saved));
        }
        finally {Directory.Delete(path,true);}
    }
    [Theory]
    [InlineData("fake.png","image/png")]
    [InlineData("fake.exe","image/png")]
    [InlineData("fake.png","text/plain")]
    public async Task RejectsForgedContentOrMime(string name,string mime)
    {
        var service=new ProductImageService(new EnvironmentStub{WebRootPath=AppContext.BaseDirectory},NullLogger<ProductImageService>.Instance);
        await Assert.ThrowsAsync<ProductImageException>(()=>service.SaveAsync(File(new byte[20],name,mime)));
    }
    [Fact]
    public async Task RejectsOversizedImageBeforeWriting()
    {
        var service=new ProductImageService(new EnvironmentStub{WebRootPath=AppContext.BaseDirectory},NullLogger<ProductImageService>.Instance);
        await Assert.ThrowsAsync<ProductImageException>(()=>service.SaveAsync(File(new byte[5*1024*1024+1],"large.png","image/png")));
    }
    private static IFormFile File(byte[] bytes,string name,string mime)=>new FormFile(new MemoryStream(bytes),0,bytes.Length,"ImageFile",name){Headers=new HeaderDictionary(),ContentType=mime};
    private sealed class EnvironmentStub:IWebHostEnvironment
    {
        public string WebRootPath{get;set;}="";
        public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
        public string ApplicationName{get;set;}="Audit";
        public string EnvironmentName{get;set;}="Testing";
        public string ContentRootPath{get;set;}="";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
