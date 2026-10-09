using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Omni.Infrastructure.Database;

/// <summary>
/// Applies the incremental SQL scripts embedded in this assembly at startup, so a deploy no longer needs
/// someone to run them with sqlcmd. Each script is idempotent (IF ... IS NULL guards) and is recorded in
/// schema_migrations once it succeeds, so it runs once per database. InitialSchema.sql and one-off data
/// fixes (MergeDuplicateCustomers.sql) are deliberately not in the list.
/// </summary>
public sealed class DatabaseMigrator
{
    /// <summary>Applied in this order. Append new scripts at the end; never reorder or rename.</summary>
    public static readonly IReadOnlyList<string> Scripts = new[]
    {
        "OtpMigration.sql",
        "NumberKeyMigration.sql",
        "TemplateSendMigration.sql"
    };

    private static readonly Regex BatchSeparator = new(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private const string EnsureHistoryTable = @"
        IF OBJECT_ID('schema_migrations', 'U') IS NULL
            CREATE TABLE schema_migrations (
                name NVARCHAR(200) NOT NULL PRIMARY KEY,
                appliedat DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
            );";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IDbConnectionFactory connectionFactory, ILogger<DatabaseMigrator> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>Returns false (after logging why) if a script failed. Later scripts are skipped so they never run out of order.</summary>
    public async Task<bool> MigrateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            await connection.ExecuteAsync(EnsureHistoryTable);
            var applied = (await connection.QueryAsync<string>("SELECT name FROM schema_migrations;")).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var script in Scripts.Where(s => !applied.Contains(s)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var batch in SplitBatches(ReadScript(script)))
                {
                    await connection.ExecuteAsync(new CommandDefinition(batch, commandTimeout: 300, cancellationToken: cancellationToken));
                }
                await connection.ExecuteAsync("INSERT INTO schema_migrations (name) VALUES (@Name);", new { Name = script });
                _logger.LogInformation("Applied database migration {Script}.", script);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Database migration failed. Run the scripts in Omni.Infrastructure/Database manually, or give the app's SQL login ALTER/CREATE TABLE rights.");
            return false;
        }
    }

    public static IReadOnlyList<string> SplitBatches(string script) =>
        BatchSeparator.Split(script).Select(b => b.Trim()).Where(b => b.Length > 0).ToList();

    private static string ReadScript(string name)
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Migration script {name} isn't embedded in {assembly.GetName().Name}.");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
