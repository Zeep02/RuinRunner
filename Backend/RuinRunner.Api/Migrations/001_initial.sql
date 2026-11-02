CREATE TABLE players (
 id uuid PRIMARY KEY, installation_id uuid NOT NULL UNIQUE,
 guest_secret_hash bytea NOT NULL, created_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE sessions (
 token_hash bytea PRIMARY KEY, player_id uuid NOT NULL REFERENCES players(id),
 expires_utc timestamptz NOT NULL, created_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX sessions_player ON sessions(player_id);
CREATE TABLE content_catalogs (
 version text PRIMARY KEY, sha256 text NOT NULL UNIQUE, payload jsonb NOT NULL,
 published_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE profiles (
 id uuid PRIMARY KEY, player_id uuid NOT NULL REFERENCES players(id),
 slot integer NOT NULL CHECK(slot BETWEEN 1 AND 3), name text NOT NULL,
 revision bigint NOT NULL DEFAULT 0 CHECK(revision >= 0),
 legacy_shards bigint NOT NULL DEFAULT 3 CHECK(legacy_shards >= 0),
 state jsonb NOT NULL, created_utc timestamptz NOT NULL DEFAULT now(), updated_utc timestamptz NOT NULL DEFAULT now(),
 UNIQUE(player_id,slot)
);
CREATE TABLE runs (
 id uuid PRIMARY KEY, profile_id uuid NOT NULL REFERENCES profiles(id),
 revision bigint NOT NULL DEFAULT 0, status text NOT NULL CHECK(status IN ('active','victory','defeat','abandoned')),
 seed integer NOT NULL, content_version text NOT NULL REFERENCES content_catalogs(version),
 generator_version text NOT NULL, build_id text NOT NULL, loadout jsonb NOT NULL,
 started_utc timestamptz NOT NULL DEFAULT now(), settled_utc timestamptz,
 result jsonb, legacy_reward bigint NOT NULL DEFAULT 0,
 trust_origin text NOT NULL DEFAULT 'client-reported-singleplayer'
);
CREATE UNIQUE INDEX one_active_run_per_profile ON runs(profile_id) WHERE status='active';
CREATE TABLE operations (
 player_id uuid NOT NULL REFERENCES players(id), operation_id uuid NOT NULL,
 kind text NOT NULL, request_hash text NOT NULL, response jsonb NOT NULL,
 created_utc timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(player_id,operation_id)
);
CREATE TABLE legacy_imports (
 profile_id uuid PRIMARY KEY REFERENCES profiles(id), player_id uuid NOT NULL REFERENCES players(id),
 source_sha256 text NOT NULL, migration_version integer NOT NULL, refund bigint NOT NULL,
 original_snapshot jsonb NOT NULL, imported_utc timestamptz NOT NULL DEFAULT now(),
 trust_origin text NOT NULL DEFAULT 'legacy-local-untrusted',
 UNIQUE(player_id,source_sha256,migration_version)
);
CREATE TABLE currency_ledger (
 id uuid PRIMARY KEY, profile_id uuid NOT NULL REFERENCES profiles(id), operation_id uuid NOT NULL,
 reason text NOT NULL, delta bigint NOT NULL, balance_after bigint NOT NULL CHECK(balance_after>=0),
 created_utc timestamptz NOT NULL DEFAULT now(), UNIQUE(profile_id,operation_id,reason)
);
