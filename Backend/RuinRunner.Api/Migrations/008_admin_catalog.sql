-- Admin-published catalogs stay immutable rows in content_catalogs; this records which one new expeditions use.
-- configured_version is the package's RR_CONTENT_VERSION at activation, so a newer shipped catalog wins over it.
CREATE TABLE catalog_activation (
 id integer PRIMARY KEY CHECK(id = 1),
 version text NOT NULL REFERENCES content_catalogs(version),
 configured_version text NOT NULL,
 activated_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE admin_catalog_history (
 version text PRIMARY KEY REFERENCES content_catalogs(version),
 base_version text NOT NULL REFERENCES content_catalogs(version),
 note text NOT NULL DEFAULT '',
 created_utc timestamptz NOT NULL DEFAULT now()
);
-- Audit rows carry no sequence so the application role needs no extra grant.
CREATE TABLE admin_audit (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 action text NOT NULL,
 version text NOT NULL,
 remote_address text NOT NULL DEFAULT '',
 created_utc timestamptz NOT NULL DEFAULT now()
);
