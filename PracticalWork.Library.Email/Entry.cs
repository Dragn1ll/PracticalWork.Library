using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PracticalWork.Library.Abstractions.Services;

namespace PracticalWork.Library.Email;

public static class Entry
{
    public static IServiceCollection AddEmail(
        this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.Configure<EmailOptions>(
            configuration.GetSection("EmailSettings"));

        serviceCollection.AddSingleton<ISmtpClient, SmtpClient>();
        serviceCollection.AddScoped<IEmailService, EmailService>();

        return serviceCollection;
    }
}