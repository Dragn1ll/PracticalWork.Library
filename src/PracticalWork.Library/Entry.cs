﻿using Microsoft.Extensions.DependencyInjection;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Services;

namespace PracticalWork.Library;

public static class Entry
{
    /// <summary>
    /// Регистрация зависимостей уровня бизнес-логики
    /// </summary>
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IReaderService, ReaderService>();

        return services;
    }
}