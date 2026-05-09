using QuestPDF.Infrastructure;
using Synapse.KSeF.Endpoints;
using Synapse.KSeF.Services;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("ksef", client =>
{
    var baseUrl = builder.Configuration["KSeF:ApiBase"]
        ?? Environment.GetEnvironmentVariable("KSEF_API_BASE")
        ?? "https://ksef-test.mf.gov.pl";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IKsefAuthService, KsefAuthService>();
builder.Services.AddScoped<IInvoiceGeneratorService, InvoiceGeneratorService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IPdfArchiveService, PdfArchiveService>();
builder.Services.AddSingleton<UpoPollerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<UpoPollerService>());

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    app = "Synapse.KSeF",
    timestamp = DateTime.UtcNow
}));

app.MapInvoiceEndpoints();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8002";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine("=== SYNAPSE KSEF SERVICE READY ===");
app.Run();
