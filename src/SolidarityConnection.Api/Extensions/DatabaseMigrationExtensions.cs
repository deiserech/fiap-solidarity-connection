using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SolidarityConnection.Infrastructure.Data;
using System.Data;

namespace SolidarityConnection.Api.Extensions;

public static class DatabaseMigrationExtensions
{
    public static async Task EnsureDatabaseAndMigrationsAsync(this WebApplication app, string connectionString)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseBootstrap");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("No SQL connection string configured. Skipping database bootstrap.");
            return;
        }

        SqlConnectionStringBuilder connectionStringBuilder;

        try
        {
            connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid SQL connection string. Skipping database bootstrap.");
            return;
        }

        var targetDatabase = connectionStringBuilder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            logger.LogWarning("No database name configured in the connection string. Skipping database bootstrap.");
            return;
        }

        var masterConnectionStringBuilder = new SqlConnectionStringBuilder(connectionStringBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        const int maxRetries = 10;
        const int retryDelaySeconds = 2;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(masterConnectionStringBuilder.ConnectionString);
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = @"
IF DB_ID(@dbName) IS NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@dbName);
    EXEC(@sql);
END";
                var databaseNameParameter = command.Parameters.Add("@dbName", SqlDbType.NVarChar, 128);
                databaseNameParameter.Value = targetDatabase;

                await command.ExecuteNonQueryAsync();
                break;
            }
            catch (SqlException ex) when (attempt < maxRetries)
            {
                logger.LogWarning(
                    ex,
                    "Failed to initialize database '{Database}' (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                    targetDatabase,
                    attempt,
                    maxRetries,
                    retryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }

        logger.LogInformation("Applying EF Core migrations to database '{Database}'.", targetDatabase);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        logger.LogInformation("Database bootstrap completed for '{Database}'.", targetDatabase);
    }
}
