using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;

namespace Nexustock.Files.IntegrationTests;

[Trait("Category", "P46BRelational")]
public sealed class ThumbnailBackfillRelationalTests
{
    [Fact]
    public async Task Backfill_TwoInstances_OnlyOneOwnsDeterministicThumbnail()
    {
        var connection = RequireTestConnection();
        var schema = $"p46b_{Guid.NewGuid():N}";
        var provider = new TestObjectStorageProvider(StorageProviderIds.Fake);
        var resolver = new TestObjectStorageResolver(provider);

        await EnsureTestDatabaseAsync(connection);
        await using var setup = CreateDb(connection, schema);
        await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
        try
        {
            var attachment = CreateAttachment(setup.CurrentTenantId);
            setup.FileStorageSettings.Add(CreateSettings(setup.CurrentTenantId));
            setup.FileAttachments.Add(attachment);
            await setup.SaveChangesAsync();
            await provider.PutAsync(attachment.StorageKey, new MemoryStream([1, 2, 3]), attachment.ContentType, CancellationToken.None);

            var firstPut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var putCount = 0;
            provider.AfterPutAsync = async key =>
            {
                if (!key.EndsWith(".thumb.jpg", StringComparison.Ordinal)) return;
                if (Interlocked.Increment(ref putCount) == 1)
                {
                    firstPut.SetResult();
                    await release.Task;
                }
            };

            await using var db1 = CreateDb(connection, schema);
            await using var db2 = CreateDb(connection, schema);
            var run1 = CreateService(db1, resolver).BackfillThumbnailsAsync(CancellationToken.None);
            await firstPut.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var run2 = CreateService(db2, resolver).BackfillThumbnailsAsync(CancellationToken.None);
            release.SetResult();
            var results = await Task.WhenAll(run1, run2);

            await setup.Entry(attachment).ReloadAsync();
            Assert.Equal(1, results.Sum());
            Assert.Equal($"{attachment.StorageKey}.thumb.jpg", attachment.ThumbnailKey);
            Assert.Empty(provider.DeletedKeys);
        }
        finally
        {
            await setup.Database.ExecuteSqlAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    [Fact]
    public async Task Backfill_StorageKeyChangesAfterPut_CleansOrphanAndDoesNotAttach()
    {
        var connection = RequireTestConnection();
        var schema = $"p46b_{Guid.NewGuid():N}";
        var provider = new TestObjectStorageProvider(StorageProviderIds.Fake);
        var resolver = new TestObjectStorageResolver(provider);

        await EnsureTestDatabaseAsync(connection);
        await using var setup = CreateDb(connection, schema);
        await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
        try
        {
            var attachment = CreateAttachment(setup.CurrentTenantId);
            setup.FileStorageSettings.Add(CreateSettings(setup.CurrentTenantId));
            setup.FileAttachments.Add(attachment);
            await setup.SaveChangesAsync();
            await provider.PutAsync(attachment.StorageKey, new MemoryStream([1, 2, 3]), attachment.ContentType, CancellationToken.None);

            var originalStorageKey = attachment.StorageKey;
            provider.AfterPutAsync = async key =>
            {
                if (!key.EndsWith(".thumb.jpg", StringComparison.Ordinal)) return;
                await using var racer = CreateDb(connection, schema);
                await racer.FileAttachments.Where(x => x.Id == attachment.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.StorageKey, "changed/original.webp"));
            };

            await using var workerDb = CreateDb(connection, schema);
            var result = await CreateService(workerDb, resolver).BackfillThumbnailsAsync(CancellationToken.None);

            await setup.Entry(attachment).ReloadAsync();
            Assert.Equal(0, result);
            Assert.Null(attachment.ThumbnailKey);
            Assert.Contains($"{originalStorageKey}.thumb.jpg", provider.DeletedKeys);
        }
        finally
        {
            await setup.Database.ExecuteSqlAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static string RequireTestConnection()
    {
        var connection = Environment.GetEnvironmentVariable("P46B_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("P46B_POSTGRES_CONNECTION is required for strict relational tests.");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database) || !database.StartsWith("p46b_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("P46B_POSTGRES_CONNECTION must target a database starting with 'p46b_test'.");
        return connection;
    }

    private static async Task EnsureTestDatabaseAsync(string connection)
    {
        var target = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        var database = target.Database ?? throw new InvalidOperationException("Test database name is required.");
        var admin = new Npgsql.NpgsqlConnectionStringBuilder(connection) { Database = "postgres" };
        await using var db = new Npgsql.NpgsqlConnection(admin.ConnectionString);
        await db.OpenAsync();

        await using var exists = new Npgsql.NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", db);
        exists.Parameters.AddWithValue("name", database);
        if (await exists.ExecuteScalarAsync() is not null) return;

        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^p46b_test[a-zA-Z0-9_]*$"))
            throw new InvalidOperationException("Unsafe P46B test database name.");

        try
        {
            await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{database}\"", db);
            await create.ExecuteNonQueryAsync();
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04")
        {
            // Database được test worker khác tạo đồng thời.
        }
    }

    private static FilesDbContext CreateDb(string connection, string schema)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, SchemaModelCacheKeyFactory>()
            .Options;
        return new SchemaFilesDbContext(options, schema);
    }

    private static ThumbnailBackfillService CreateService(FilesDbContext db, IObjectStorageResolver resolver)
    {
        var thumbnail = new DeterministicThumbnailService();
        var storage = new FileStorageService(db, resolver, thumbnail, NullLogger<FileStorageService>.Instance);
        return new ThumbnailBackfillService(db, storage, resolver, thumbnail,
            Options.Create(new ThumbnailOptions { Enabled = true, BackfillEnabled = true, BatchSize = 10, MaxRetriesPerRun = 3 }),
            NullLogger<ThumbnailBackfillService>.Instance);
    }

    private static FileAttachment CreateAttachment(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, EntityType = "LOT", EntityId = Guid.NewGuid(),
        FileName = "race.webp", ContentType = "image/webp", SizeBytes = 3, Kind = "IMAGE",
        Provider = StorageProviderIds.Fake, StorageKey = $"{tenantId:N}/{Guid.NewGuid():N}.webp",
        PublicUrl = "/test/race.webp", CreatedAt = DateTimeOffset.UtcNow
    };

    private static FileStorageSettings CreateSettings(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ActiveProvider = StorageProviderIds.Fake,
        IsEnabled = true, UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class SchemaFilesDbContext(DbContextOptions<FilesDbContext> options, string schema) : FilesDbContext(options)
    {
        public string TestSchema { get; } = schema;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(TestSchema);
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                entity.SetSchema(TestSchema);
        }
    }

    private sealed class SchemaModelCacheKeyFactory : Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => context is SchemaFilesDbContext schemaContext
                ? (context.GetType(), schemaContext.TestSchema, designTime)
                : (object)(context.GetType(), designTime);
    }
}
