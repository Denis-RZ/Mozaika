using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Providers;
using Mozaika.Api.Options;
using Mozaika.Api.Security;
using Mozaika.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MozaikaOptions>(builder.Configuration.GetSection("Mozaika"));
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection("Mozaika:Pricing"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Mozaika:Auth"));
var initialDatabaseOptions = builder.Configuration.GetSection("Mozaika:Database").Get<DatabaseOptions>() ?? new DatabaseOptions();

builder.Services.AddSingleton<IDatabaseProviderRegistry, DatabaseProviderRegistry>();
builder.Services.AddSingleton(new RuntimeDatabaseSettingsStore(initialDatabaseOptions));
builder.Services.AddScoped<MosaicService>();
builder.Services.AddScoped<MosaicExportService>();
builder.Services.AddScoped<ProjectStorageService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProjectWorkflowService>();

builder.Services.AddDbContext<MozaikaDbContext>((serviceProvider, optionsBuilder) =>
{
    var dbOptions = serviceProvider.GetRequiredService<RuntimeDatabaseSettingsStore>().GetSnapshot();
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
    var authOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;
    await DbInitializer.SeedAsync(dbContext, authOptions);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<AuthenticationMiddleware>();
app.MapControllers();
app.Run();

