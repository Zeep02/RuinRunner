-- campaign-v4 replaces every talent. Legacy already paid for an old talent comes back, and every profile starts on the new free set.
-- Bumps revision so outstanding old configuration requests conflict. Runs stay pinned to the catalog they started on.
WITH paid(id,cost) AS (VALUES
    ('stored_guard',5),('cross_chamber',5),('turning_dash',3),('brake_dash',3),('return_dash',5),
    ('supply_choice',3),('return_trail',3),('merchant_barter',5),('reserve_offer',5)),
refund AS (
    SELECT p.id, COALESCE(SUM(paid.cost),0)::bigint AS amount
    FROM profiles p
    LEFT JOIN LATERAL jsonb_array_elements_text(COALESCE(p.state->'unlockedTalentIds','[]'::jsonb)) owned(id) ON true
    LEFT JOIN paid ON paid.id=owned.id
    GROUP BY p.id),
updated AS (
    UPDATE profiles p SET
        state = p.state || jsonb_build_object(
            'unlockedTalentIds','["corpse_burst","chain_chant","ghost_mine","miser"]'::jsonb,
            'activeTalentIds','["corpse_burst","chain_chant","ghost_mine"]'::jsonb),
        legacy_shards = p.legacy_shards + refund.amount,
        revision = p.revision + 1, updated_utc = now()
    FROM refund WHERE refund.id = p.id
    RETURNING p.id, p.legacy_shards, refund.amount)
INSERT INTO currency_ledger(id,profile_id,operation_id,reason,delta,balance_after)
SELECT gen_random_uuid(), id, gen_random_uuid(), 'talent.rework-refund', amount, legacy_shards FROM updated WHERE amount > 0;
