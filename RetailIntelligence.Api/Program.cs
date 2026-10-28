using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Services;
using RetailIntelligence.Api.Integrations.Eleventa;
using Npgsql;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configuración de Controllers y JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

// La contraseña puede inyectarse fuera del repositorio. Una cadena completa de
// ConnectionStrings__DefaultConnection también puede reemplazar el valor base.
var postgresConnection = new NpgsqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection"));
var postgresPassword = builder.Configuration["Postgres:Password"];
if (!string.IsNullOrWhiteSpace(postgresPassword))
    postgresConnection.Password = postgresPassword;
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnection.ConnectionString));

builder.Services.AddSwaggerGen();


builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Registro de servicios
builder.Services.AddScoped<IDepartamentoService, DepartamentoService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();


builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();
builder.Services.AddScoped<EleventaApiKeyFilter>();
builder.Services.AddScoped<EleventaSyncService>();
builder.Services.AddScoped<EleventaFinanceService>();

var app = builder.Build();

Console.WriteLine("========== CORS VERSION 3 ==========");
Console.WriteLine(app.Environment.EnvironmentName);

app.UseCors("ReactPolicy");


// Swagger en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
