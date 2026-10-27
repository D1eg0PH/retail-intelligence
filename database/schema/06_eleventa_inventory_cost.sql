-- Apply before deploying the API; then update the Windows agent.
-- Historical snapshots and older agents retain NULL: the original cost is unknown.
-- Do not backfill from the current catalog.
ALTER TABLE eleventa_inventory_rows
    ADD COLUMN IF NOT EXISTS unit_cost NUMERIC(19,4) NULL;
