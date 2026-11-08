-- 0502-lineage-spouse-npc.sql — Add npc_id reference to ln_spouse for marriage with NPCs.
ALTER TABLE ln_spouse ADD COLUMN npc_id INTEGER;
