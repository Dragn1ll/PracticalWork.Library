using Microsoft.EntityFrameworkCore;
using Npgsql;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Cache.Redis;
using PracticalWork.Library.Data.Minio;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.MessageBroker.Events.Reader;
using PracticalWork.Library.MessageBroker.Events.Report;
using PracticalWork.Library.Services;
using PracticalWork.Reports.Data.PostgreSql;
using PracticalWork.Reports.Worker.Abstractions;
using PracticalWork.Reports.Worker.Consumers;
using PracticalWork.Reports.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

var services = builder.Services;

var queueNames = builder.Configuration.GetSection("QueueNames");
services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

services.AddScoped<IReportGenService, ReportGenService>();

services.AddKeyedSingleton<IRabbitMqConsumer, ActivityLogConsumer<BookCreatedEvent>>(queueNames["BookCreated"]);
services.AddKeyedSingleton<IRabbitMqConsumer, ActivityLogConsumer<BookArchivedEvent>>(queueNames["BookArchived"]);
services.AddKeyedSingleton<IRabbitMqConsumer, ActivityLogConsumer<BookBorrowedEvent>>(queueNames["BookBorrowed"]);
services.AddKeyedSingleton<IRabbitMqConsumer, ActivityLogConsumer<BookReturnedEvent>>(queueNames["BookReturned"]);
services.AddKeyedSingleton<IRabbitMqConsumer, ActivityLogConsumer<ReaderCreatedEvent>>(queueNames["ReaderCreated"]);
services.AddKeyedSingleton<IRabbitMqConsumer, ActivityLogConsumer<ReaderClosedEvent>>(queueNames["ReaderClosed"]);
services.AddKeyedSingleton<IRabbitMqConsumer, ReportGenerateConsumer>(queueNames["CreateReport"]);

services.AddHostedService<ConsumersBackgroundService>();

services.AddPostgreSqlStorage(cfg =>
{
    var npgsqlDataSource = new NpgsqlDataSourceBuilder(builder.Configuration["App:DbConnectionString"])
        .EnableDynamicJson()
        .Build();

    cfg.UseNpgsql(npgsqlDataSource);
});

services.AddMinioFileStorage(builder.Configuration);
services.AddCache(builder.Configuration);

var host = builder.Build();

using var scope = host.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

if (dbContext.Database.GetPendingMigrations().Any())
{
    dbContext.Database.Migrate();
}

await host.RunAsync();