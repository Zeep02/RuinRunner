-- Signatures are retained with their run, not held in a process-local recent list.
CREATE TABLE run_generation (
 run_id uuid PRIMARY KEY REFERENCES runs(id),
 stage_signatures jsonb NOT NULL CHECK(jsonb_typeof(stage_signatures)='array' AND jsonb_array_length(stage_signatures)=3),
 candidate_attempts integer NOT NULL CHECK(candidate_attempts BETWEEN 1 AND 8),
 repeated_recent_structure boolean NOT NULL,
 selection_mode text NOT NULL CHECK(selection_mode IN ('ordinary','explicit','historical')),
 recorded_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX runs_recent_generation ON runs(profile_id,generator_version,started_utc DESC,id DESC);
