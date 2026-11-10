-- 0503-lineage-generation-and-alliances.sql: Generational succession chronicle and political marriages

CREATE TABLE IF NOT EXISTS ln_generation_log (
    generation_number INTEGER PRIMARY KEY,
    patriarch_name_vi TEXT NOT NULL,
    heir_child_id INTEGER NOT NULL,
    ascended_day_index INTEGER NOT NULL,
    inherited_prestige INTEGER NOT NULL,
    chronicle_vi TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ln_political_marriage (
    child_id INTEGER PRIMARY KEY,
    faction_id INTEGER NOT NULL,
    faction_name_vi TEXT NOT NULL,
    spouse_title_vi TEXT NOT NULL,
    married_day_index INTEGER NOT NULL,
    tariff_discount_permille INTEGER NOT NULL DEFAULT 50,
    prestige_bonus INTEGER NOT NULL DEFAULT 35
);
