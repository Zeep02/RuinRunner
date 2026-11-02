-- Reset keeps profile identity, legacy imports and historical receipts. No history is deleted.
CREATE TABLE profile_reset_challenges (
 id uuid PRIMARY KEY,
 profile_id uuid NOT NULL REFERENCES profiles(id),
 player_id uuid NOT NULL REFERENCES players(id),
 expected_revision bigint NOT NULL CHECK(expected_revision >= 0),
 expires_utc timestamptz NOT NULL,
 created_utc timestamptz NOT NULL DEFAULT now(),
 consumed_utc timestamptz
);
CREATE INDEX profile_reset_challenges_profile ON profile_reset_challenges(profile_id,created_utc DESC);
CREATE TABLE profile_resets (
 id uuid PRIMARY KEY REFERENCES profile_reset_challenges(id),
 profile_id uuid NOT NULL REFERENCES profiles(id),
 player_id uuid NOT NULL REFERENCES players(id),
 operation_id uuid NOT NULL,
 previous_revision bigint NOT NULL,
 resulting_revision bigint NOT NULL,
 previous_profile jsonb NOT NULL,
 reset_utc timestamptz NOT NULL DEFAULT now(),
 UNIQUE(player_id,operation_id)
);
