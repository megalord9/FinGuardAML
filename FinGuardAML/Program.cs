using FinGuardAML.Services;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;

// Поддержка кодировки Windows-1251 в официальном XML ЦБ РФ.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FinGuard AML API",
        Version = "v1",
        Description = "Учебный веб-API для оценки риска финансовых операций, получения курсов валют ЦБ РФ и проверки контрагентов. Результаты скоринга не являются юридическими решениями."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<CbrCurrencyService>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<OpenSanctionsScreeningService>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddHttpClient<PartnerAnalyticsService>(client =>
    client.Timeout = TimeSpan.FromSeconds(7));

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
