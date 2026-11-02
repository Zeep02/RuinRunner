-- Existing authenticated profiles keep balances and run history. This grants only free defaults,
-- never refunds twice, and bumps revision so outstanding old configuration requests conflict.
UPDATE profiles SET state = state || jsonb_build_object(
 'progressionVersion', 2,
 'legacyRefundVersion', COALESCE((state->>'legacyMigrationVersion')::integer,0),
 'unlockedTalentIds', (SELECT jsonb_agg(DISTINCT talent) FROM jsonb_array_elements(
    COALESCE(state->'unlockedTalentIds','[]'::jsonb) || '["combo_memory","survey_next","chest_intent"]'::jsonb) talent),
 'activeTalentIds', CASE WHEN jsonb_array_length(COALESCE(state->'activeTalentIds','[]'::jsonb))=0
    THEN '["combo_memory","survey_next","chest_intent"]'::jsonb ELSE state->'activeTalentIds' END),
 revision=revision+1, updated_utc=now()
WHERE COALESCE((state->>'progressionVersion')::integer,0)<2;
