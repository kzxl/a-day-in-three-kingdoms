-- Migration 0604: Tenant Farmer Sharecropping (Tá Điền Tự Canh) on Rented Plots
ALTER TABLE fm_plot ADD COLUMN tenant_crop_code TEXT;
