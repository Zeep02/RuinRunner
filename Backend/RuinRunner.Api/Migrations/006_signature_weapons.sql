-- campaign-v3 replaces every weapon. Each earlier starter maps to the nearest new one, so nothing already paid for is lost.
-- Bumps revision so outstanding old configuration requests conflict. Runs stay pinned to the catalog they started on.
WITH map(old_id,new_id) AS (VALUES
    ('rust_sword','trainee_blade'),
    ('training_katana','trainee_blade'),
    ('worn_dagger','alley_awl'),
    ('scrap_pistol','scrap_revolver'),
    ('iron_mace','merc_slab'),
    ('scrap_shotgun','gate_shotgun'))
UPDATE profiles p SET state = p.state || jsonb_build_object(
    'startingWeaponId', COALESCE((SELECT new_id FROM map WHERE old_id=p.state->>'startingWeaponId'), 'trainee_blade'),
    'unlockedWeaponIds', (SELECT jsonb_agg(DISTINCT id) FROM (
        SELECT 'trainee_blade' AS id
        UNION SELECT COALESCE(m.new_id,w.value) FROM jsonb_array_elements_text(COALESCE(p.state->'unlockedWeaponIds','[]'::jsonb)) w
            LEFT JOIN map m ON m.old_id=w.value) ids)),
    revision=revision+1, updated_utc=now()
WHERE EXISTS (SELECT 1 FROM map WHERE old_id=p.state->>'startingWeaponId');
