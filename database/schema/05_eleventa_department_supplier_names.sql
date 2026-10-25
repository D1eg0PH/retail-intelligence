BEGIN;

ALTER TABLE public.eleventa_products
    ADD COLUMN IF NOT EXISTS source_department_name VARCHAR(255);

ALTER TABLE public.eleventa_products
    ADD COLUMN IF NOT EXISTS source_supplier_name VARCHAR(255);

COMMIT;
