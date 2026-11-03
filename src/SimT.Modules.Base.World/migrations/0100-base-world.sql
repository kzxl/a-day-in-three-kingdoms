-- base.world owns settlements and roads. Three citadels is the slice; the wide map is a
-- later DLC and must never be added by editing these rows.

CREATE TABLE IF NOT EXISTS wd_citadel (
    id                    INTEGER PRIMARY KEY,
    code                  TEXT    NOT NULL UNIQUE,
    name_vi               TEXT    NOT NULL,
    name_en               TEXT    NOT NULL,
    region                TEXT    NOT NULL,
    price_factor_permille INTEGER NOT NULL DEFAULT 1000 CHECK (price_factor_permille > 0)
);

CREATE TABLE IF NOT EXISTS wd_route (
    citadel_id  INTEGER NOT NULL REFERENCES wd_citadel(id),
    other_id    INTEGER NOT NULL REFERENCES wd_citadel(id),
    travel_days INTEGER NOT NULL CHECK (travel_days > 0),
    PRIMARY KEY (citadel_id, other_id)
);

INSERT OR IGNORE INTO wd_citadel (id, code, name_vi, name_en, region, price_factor_permille) VALUES
    (1, 'lac_duong', 'Lạc Dương', 'Luoyang',  'trung_nguyen', 1000),
    (2, 'truong_an', 'Trường An', 'Chang''an','quan_trung',   1120),
    (3, 'nghiep_thanh','Nghiệp Thành','Ye',   'ha_bac',        930);

INSERT OR IGNORE INTO wd_route (citadel_id, other_id, travel_days) VALUES
    (1, 2, 3), (2, 1, 3),
    (2, 3, 4), (3, 2, 4);
