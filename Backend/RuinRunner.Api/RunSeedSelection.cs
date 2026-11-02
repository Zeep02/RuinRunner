using Npgsql;
using RuinRunner.Generation;
using System.Security.Cryptography;

namespace RuinRunner.Api;

public sealed record RunGenerationView(string[] StageSignatures, int CandidateAttempts, bool RepeatedRecentStructure, string SelectionMode);
public sealed record SelectedRunSeed(int Seed, RunGenerationView Generation);

/// <summary>One persisted seed generates all three regions. Explicit seeds never consult random selection.</summary>
public static class RunSeedSelection
{
    public const int RecentRunLimit = 16;
    public const int CandidateLimit = 8;

    public static string[] Signatures(int seed) => Enumerable.Range(0, 3)
        .Select(stage => ProceduralStageGenerator.Generate(seed, stage).StructureSignature).ToArray();

    public static SelectedRunSeed Choose(int? requestedSeed, IReadOnlyList<string[]> recent, Func<int>? nextSeed = null)
    {
        nextSeed ??= () => RandomNumberGenerator.GetInt32(int.MaxValue);
        var history = recent.Take(RecentRunLimit).ToArray();
        bool Repeats(string[] signatures) => history.Any(previous =>
            Enumerable.Range(0, 3).Any(stage => previous[stage] == signatures[stage]));
        if (requestedSeed is int explicitSeed)
        {
            var signatures = Signatures(explicitSeed);
            return new(explicitSeed, new(signatures, 1, Repeats(signatures), "explicit"));
        }
        SelectedRunSeed? selected = null;
        for (var attempt = 1; attempt <= CandidateLimit; attempt++)
        {
            var seed = nextSeed();
            var signatures = Signatures(seed);
            var repeats = Repeats(signatures);
            selected = new(seed, new(signatures, attempt, repeats, "ordinary"));
            if (!repeats) return selected;
        }
        // Bounded work: keep the final validated candidate and disclose the repeated structure.
        return selected!;
    }

    public static async Task<string[][]> ReadRecent(NpgsqlConnection c, Guid profile)
    {
        var rows = new List<(Guid Id, int Seed, string[]? Signatures)>();
        await using (var cmd = Database.Command(c,
            "SELECT r.id,r.seed,g.stage_signatures::text FROM runs r LEFT JOIN run_generation g ON g.run_id=r.id WHERE r.profile_id=$1 AND r.generator_version=$2 ORDER BY r.started_utc DESC,r.id DESC LIMIT 16",
            profile, ProceduralStageGenerator.Version))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync()) rows.Add((reader.GetGuid(0), reader.GetInt32(1),
                reader.IsDBNull(2) ? null : Wire.Decode<string[]>(reader.GetString(2))));
        var recent = new List<string[]>();
        foreach (var row in rows)
        {
            var signatures = row.Signatures ?? Signatures(row.Seed);
            // Existing supported-version runs are incorporated without changing their seed or receipt.
            if (row.Signatures == null) await Persist(c, row.Id, new(signatures, 1, false, "historical"));
            recent.Add(signatures);
        }
        return recent.ToArray();
    }

    public static Task<int> Persist(NpgsqlConnection c, Guid run, RunGenerationView generation) => Database.Exec(c,
        "INSERT INTO run_generation(run_id,stage_signatures,candidate_attempts,repeated_recent_structure,selection_mode) VALUES($1,$2::jsonb,$3,$4,$5)",
        run, Wire.Encode(generation.StageSignatures), generation.CandidateAttempts, generation.RepeatedRecentStructure, generation.SelectionMode);
}
