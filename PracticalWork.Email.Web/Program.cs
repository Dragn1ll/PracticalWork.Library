using Hangfire;
using Hangfire.PostgreSql;
using PracticalWork.Email.Web;
using PracticalWork.Email.Web.Hangfire;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var services = builder.Services;

services.AddEmailWorkerDatabases(configuration);

services.AddEmailWorkerServices(configuration);

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

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() },
    DashboardTitle = "Библиотека - Фоновые задачи"
});

app.Services.AddRecurringJobs();

app.MapGet("/", () => "Library Email Worker Service is running. Dashboard: /hangfire");

await app.RunAsync();