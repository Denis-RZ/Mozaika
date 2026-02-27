using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mozaika.Api.Options;

namespace Mozaika.Api.Services;

public sealed class AppSettingsDatabaseConfigWriter(IHostEnvironment hostEnvironment)
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task PersistAsync(DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        var files = ResolveConfigFiles();
        if (files.Count == 0)
        {
            throw new InvalidOperationException("Не найден appsettings.json для сохранения параметров базы данных.");
        }

        foreach (var filePath in files)
        {
            await UpdateFileAsync(filePath, options, cancellationToken);
        }
    }

    private List<string> ResolveConfigFiles()
    {
        var basePath = hostEnvironment.ContentRootPath;
        var result = new List<string>();

        var appSettingsPath = Path.Combine(basePath, "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            result.Add(appSettingsPath);
        }

        if (!string.IsNullOrWhiteSpace(hostEnvironment.EnvironmentName))
        {
            var envPath = Path.Combine(basePath, $"appsettings.{hostEnvironment.EnvironmentName}.json");
            if (File.Exists(envPath))
            {
                result.Add(envPath);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task UpdateFileAsync(
        string filePath,
        DatabaseOptions options,
        CancellationToken cancellationToken
    )
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Файл '{Path.GetFileName(filePath)}' содержит некорректный JSON: {ex.Message}");
        }

        var rootObject = rootNode as JsonObject ?? new JsonObject();

        var mozaikaNode = rootObject["Mozaika"] as JsonObject;
        if (mozaikaNode is null)
        {
            mozaikaNode = new JsonObject();
            rootObject["Mozaika"] = mozaikaNode;
        }

        var databaseNode = mozaikaNode["Database"] as JsonObject;
        if (databaseNode is null)
        {
            databaseNode = new JsonObject();
            mozaikaNode["Database"] = databaseNode;
        }

        databaseNode["Provider"] = options.Provider;
        databaseNode["ConnectionString"] = options.ConnectionString;
        databaseNode["Echo"] = options.Echo;

        var updated = rootObject.ToJsonString(JsonWriteOptions) + Environment.NewLine;
        await File.WriteAllTextAsync(filePath, updated, Utf8NoBom, cancellationToken);
    }
}
