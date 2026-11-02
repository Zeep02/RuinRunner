CREATE TABLE run_economy (
 run_id uuid PRIMARY KEY REFERENCES runs(id), revision bigint NOT NULL DEFAULT 0 CHECK(revision >= 0),
 gold bigint NOT NULL CHECK(gold >= 0), updated_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE run_items (
 id uuid PRIMARY KEY, run_id uuid NOT NULL REFERENCES runs(id), content_id text NOT NULL,
 kind text NOT NULL CHECK(kind IN ('weapon','accessory')), sell_price integer NOT NULL CHECK(sell_price>=0),
 origin text NOT NULL, removed boolean NOT NULL DEFAULT false, created_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX run_items_owned ON run_items(run_id) WHERE NOT removed;
CREATE TABLE run_shops (
 run_id uuid NOT NULL REFERENCES runs(id), shop_id text NOT NULL, stage_index integer NOT NULL,
 barter_used boolean NOT NULL DEFAULT false, PRIMARY KEY(run_id,shop_id)
);
CREATE TABLE run_shop_offers (
 run_id uuid NOT NULL, shop_id text NOT NULL, offer_id text NOT NULL, content_id text NOT NULL,
 kind text NOT NULL CHECK(kind IN ('weapon','accessory','heal')), price integer NOT NULL CHECK(price>=0),
 stock integer NOT NULL CHECK(stock>=0), PRIMARY KEY(run_id,shop_id,offer_id),
 FOREIGN KEY(run_id,shop_id) REFERENCES run_shops(run_id,shop_id)
);
CREATE TABLE run_reward_events (
 run_id uuid NOT NULL REFERENCES runs(id), event_id text NOT NULL, request_hash text NOT NULL,
 operation_id uuid NOT NULL, receipt jsonb NOT NULL, created_utc timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(run_id,event_id)
);
CREATE TABLE run_gold_ledger (
 run_id uuid NOT NULL REFERENCES runs(id), operation_id uuid NOT NULL, reason text NOT NULL,
 delta bigint NOT NULL, balance_after bigint NOT NULL CHECK(balance_after>=0),
 created_utc timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(run_id,operation_id)
);
CREATE TABLE run_used_objects (
 run_id uuid NOT NULL REFERENCES runs(id), object_id text NOT NULL, kind text NOT NULL,
 created_utc timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(run_id,object_id)
);
