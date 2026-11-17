using FinGuardAML.Services;
using System.Text;

// Поддержка кодировки Windows-1251 в официальном XML ЦБ РФ.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<CbrCurrencyService>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<OpenSanctionsScreeningService>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddScoped<RiskScoringService>();
builder.Services.AddSingleton<TransactionStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.Run();
