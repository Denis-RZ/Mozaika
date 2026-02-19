using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Providers;
using Mozaika.Api.Options;
using Mozaika.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MozaikaOptions>(builder.Configuration.GetSection("Mozaika"));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Mozaika:Database"));

builder.Services.AddSingleton<IDatabaseProviderRegistry, DatabaseProviderRegistry>();
builder.Services.AddScoped<MosaicService>();

builder.Services.AddDbContext<MozaikaDbContext>((serviceProvider, optionsBuilder) =>
{
    var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var dbProviderRegistry = serviceProvider.GetRequiredService<IDatabaseProviderRegistry>();
    dbProviderRegistry.Configure(optionsBuilder, dbOptions);

    if (dbOptions.Echo)
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var firstError = context.ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        return new BadRequestObjectResult(new ApiError(firstError ?? "Некорректные данные запроса."));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var corsOrigins = builder.Configuration.GetSection("Mozaika:CorsOrigins").Get<string[]>() ?? ["*"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Any(origin => origin == "*"))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MozaikaDbContext>();
    await DbInitializer.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.Run();
