using Hangfire;
using Hangfire.PostgreSql;
using PracticalWork.Email.Web;
using PracticalWork.Email.Web.Hangfire;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);

var configuration = builder.Configuration;
var services = builder.Services;

// Базы данных и внешние сервисы
services.AddEmailWorkerDatabases(configuration);

// Email worker сервисы (settings, jobs, etc.)
services.AddEmailWorkerServices(configuration);

// Hangfire с PostgreSQL хранилищем
services.AddHangfire(config => config
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(
            configuration["App:DbConnectionString"])));

services.AddHangfireServer(options =>
{
    options.WorkerCount = 1;
    options.ServerName = "Library.Email.Server";
});

var app = builder.Build();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() },
    DashboardTitle = "Библиотека - Фоновые задачи"
});

// Регистрация recurring jobs
app.Services.AddRecurringJobs();

app.MapGet("/", () => "Library Email Worker Service is running. Dashboard: /hangfire");

await app.RunAsync();