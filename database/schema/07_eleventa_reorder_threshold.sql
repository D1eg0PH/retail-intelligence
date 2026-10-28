BEGIN;

ALTER TABLE public.configuracionsistema
    ADD COLUMN IF NOT EXISTS porcentaje_umbral_reposicion NUMERIC(5,2) NOT NULL DEFAULT 75.00;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'configuracionsistema_porcentaje_umbral_reposicion_check'
          AND conrelid = 'public.configuracionsistema'::regclass
    ) THEN
        ALTER TABLE public.configuracionsistema
            ADD CONSTRAINT configuracionsistema_porcentaje_umbral_reposicion_check
            CHECK (porcentaje_umbral_reposicion BETWEEN 0 AND 100);
    END IF;
END
$$;

COMMIT;
