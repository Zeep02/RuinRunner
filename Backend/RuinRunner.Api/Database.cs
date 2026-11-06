using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace RuinRunner.Api;

public sealed class Database(NpgsqlDataSource source)
{
    public NpgsqlDataSource Source { get; } = source;
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static NpgsqlCommand Command(NpgsqlConnection connection, string sql, params object?[] args)
    {
        var cmd = new NpgsqlCommand(sql, connection);
        foreach (var arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg ?? DBNull.Value });
        return cmd;
    }
    public static async Task<int> Exec(NpgsqlConnection c, string sql, params object?[] args)
    {
        await using var cmd = Command(c, sql, args);
        return await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<object?> Scalar(NpgsqlConnection c, string sql, params object?[] args)
    {
        await using var cmd = Command(c, sql, args);
        return await cmd.ExecuteScalarAsync();
    }
    // One connection/transaction and one owner lock: concurrent retries serialize before receipt lookup.
    // No side effect is visible unless both the mutation and its exact response receipt commit.
    public async Task<string> Mutate<T>(Guid player, Guid operation, string kind, T request, Func<NpgsqlConnection, Task<object>> apply)
    {
        Wire.Require(operation != Guid.Empty, "operation_required", "처리 식별자가 필요합니다.");
        await using var c = await Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await Exec(c, "SELECT pg_advisory_xact_lock(hashtextextended($1,0))", player.ToString());
        var hash = Hash(Wire.Encode(request));
        await using (var cmd = Command(c, "SELECT kind,request_hash,response::text FROM operations WHERE player_id=$1 AND operation_id=$2", player, operation))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                Wire.Require(reader.GetString(0) == kind && reader.GetString(1) == hash, "operation_reused", "같은 처리 식별자에 다른 요청을 사용할 수 없습니다.", 409);
                return reader.GetString(2);
            }
        }
        var result = Wire.Encode(await apply(c));
        await Exec(c, "INSERT INTO operations(player_id,operation_id,kind,request_hash,response) VALUES($1,$2,$3,$4,$5::jsonb)", player, operation, kind, hash, result);
        await tx.CommitAsync();
        return result;
    }
    public static async Task Migrate(string connectionString, string directory)
    {
        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        await Exec(c, "SELECT pg_advisory_xact_lock(86269420261004)");
        await Exec(c, "CREATE TABLE IF NOT EXISTS schema_migrations(version text PRIMARY KEY, sha256 text NOT NULL, applied_utc timestamptz NOT NULL DEFAULT now())");
        foreach (var path in Directory.GetFiles(directory, "*.sql").Order())
        {
            var sql = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var version = Path.GetFileName(path);
            var hash = Hash(sql);
            var previous = await Scalar(c, "SELECT sha256 FROM schema_migrations WHERE version=$1", version);
            if (previous is string old)
            {
                if (old != hash)
                    throw new InvalidOperationException("Applied migration changed: " + version);
                continue;
            }
            await Exec(c, sql);
            await Exec(c, "INSERT INTO schema_migrations(version,sha256) VALUES($1,$2)", version, hash);
        }
        await Exec(c, "GRANT USAGE ON SCHEMA public TO rr_app; GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO rr_app; REVOKE ALL ON schema_migrations FROM rr_app");
        await tx.CommitAsync();
    }
}
