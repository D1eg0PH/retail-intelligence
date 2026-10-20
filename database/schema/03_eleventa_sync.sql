BEGIN;

CREATE TABLE IF NOT EXISTS eleventa_sources (
    source_id UUID PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    credential_sha256 CHAR(64) NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_confirmed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS eleventa_sync_batches (
    id BIGSERIAL PRIMARY KEY,
    source_id UUID NOT NULL REFERENCES eleventa_sources(source_id),
    batch_id UUID NOT NULL,
    contract_version SMALLINT NOT NULL,
    batch_kind VARCHAR(24) NOT NULL,
    payload_sha256 CHAR(64) NOT NULL,
    record_count INTEGER NOT NULL,
    accepted_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_eleventa_batch UNIQUE(source_id, batch_id),
    CONSTRAINT ck_eleventa_batch_count CHECK(record_count BETWEEN 0 AND 5000)
);

CREATE TABLE IF NOT EXISTS eleventa_products (
    source_id UUID NOT NULL REFERENCES eleventa_sources(source_id),
    source_product_id INTEGER NOT NULL,
    code_original VARCHAR(50) NOT NULL,
    description_original VARCHAR(255) NOT NULL,
    cost NUMERIC(19,4),
    sale_price NUMERIC(19,4),
    wholesale_price NUMERIC(19,4),
    source_department_id INTEGER,
    source_supplier_id INTEGER,
    checked_at TIMESTAMP,
    deleted_at TIMESTAMP,
    is_kit BOOLEAN NOT NULL,
    tracks_inventory BOOLEAN NOT NULL,
    created_at_origin TIMESTAMP,
    created_at_source VARCHAR(40),
    created_at_confidence VARCHAR(20),
    first_sale_at TIMESTAMP,
    first_inventory_movement_at TIMESTAMP,
    observed_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY(source_id, source_product_id)
);

CREATE TABLE IF NOT EXISTS eleventa_inventory_snapshots (
    source_id UUID NOT NULL REFERENCES eleventa_sources(source_id),
    snapshot_id UUID NOT NULL,
    extracted_at TIMESTAMPTZ NOT NULL,
    expected_record_count INTEGER,
    received_record_count INTEGER NOT NULL DEFAULT 0,
    is_complete BOOLEAN NOT NULL DEFAULT FALSE,
    validation_status VARCHAR(20) NOT NULL DEFAULT 'pending',
    completed_at TIMESTAMPTZ,
    PRIMARY KEY(source_id, snapshot_id)
);

CREATE TABLE IF NOT EXISTS eleventa_inventory_rows (
    source_id UUID NOT NULL,
    snapshot_id UUID NOT NULL,
    source_product_id INTEGER NOT NULL,
    source_warehouse_id INTEGER NOT NULL,
    quantity NUMERIC(24,10) NOT NULL,
    PRIMARY KEY(source_id, snapshot_id, source_product_id, source_warehouse_id),
    FOREIGN KEY(source_id, snapshot_id)
      REFERENCES eleventa_inventory_snapshots(source_id, snapshot_id)
);

CREATE TABLE IF NOT EXISTS eleventa_sales (
    source_id UUID NOT NULL REFERENCES eleventa_sources(source_id),
    source_ticket_id INTEGER NOT NULL,
    folio INTEGER,
    created_at_origin TIMESTAMP,
    sold_at TIMESTAMP,
    paid_at TIMESTAMP,
    total NUMERIC(19,4),
    profit NUMERIC(19,4),
    is_open BOOLEAN NOT NULL,
    is_cancelled BOOLEAN NOT NULL,
    currency CHAR(3),
    observed_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY(source_id, source_ticket_id)
);

CREATE TABLE IF NOT EXISTS eleventa_sale_lines (
    source_id UUID NOT NULL,
    source_line_id INTEGER NOT NULL,
    source_ticket_id INTEGER NOT NULL,
    source_product_id INTEGER,
    product_code_original VARCHAR(20) NOT NULL,
    product_name_original VARCHAR(255) NOT NULL,
    quantity NUMERIC(24,10) NOT NULL,
    returned_quantity NUMERIC(24,10) NOT NULL,
    used_price NUMERIC(19,4),
    final_price NUMERIC(19,4),
    line_total NUMERIC(19,4),
    added_at TIMESTAMP,
    is_returned BOOLEAN NOT NULL,
    present_in_latest_source BOOLEAN NOT NULL DEFAULT TRUE,
    observed_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY(source_id, source_line_id),
    FOREIGN KEY(source_id, source_ticket_id)
      REFERENCES eleventa_sales(source_id, source_ticket_id)
);

CREATE TABLE IF NOT EXISTS eleventa_returns (
    source_id UUID NOT NULL REFERENCES eleventa_sources(source_id),
    source_return_id INTEGER NOT NULL,
    source_ticket_id INTEGER,
    returned_at TIMESTAMP,
    paid_at TIMESTAMP,
    return_type CHAR(1),
    cashier VARCHAR(50),
    register_name VARCHAR(50),
    amount NUMERIC(19,4) NOT NULL,
    detail_count INTEGER NOT NULL,
    detail_complete BOOLEAN NOT NULL,
    observed_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY(source_id, source_return_id)
);

CREATE TABLE IF NOT EXISTS eleventa_return_lines (
    source_id UUID NOT NULL,
    source_return_line_id INTEGER NOT NULL,
    source_return_id INTEGER NOT NULL,
    source_ticket_id INTEGER,
    product_code_original VARCHAR(20) NOT NULL,
    product_description_original VARCHAR(255) NOT NULL,
    returned_quantity NUMERIC(24,10) NOT NULL,
    amount NUMERIC(19,4) NOT NULL,
    PRIMARY KEY(source_id, source_return_line_id),
    FOREIGN KEY(source_id, source_return_id)
      REFERENCES eleventa_returns(source_id, source_return_id)
);

CREATE INDEX IF NOT EXISTS ix_eleventa_sales_sold_at
  ON eleventa_sales(source_id, sold_at);
CREATE INDEX IF NOT EXISTS ix_eleventa_sale_lines_ticket
  ON eleventa_sale_lines(source_id, source_ticket_id);
CREATE INDEX IF NOT EXISTS ix_eleventa_returns_ticket
  ON eleventa_returns(source_id, source_ticket_id);
CREATE INDEX IF NOT EXISTS ix_eleventa_inventory_latest
  ON eleventa_inventory_snapshots(source_id, is_complete, extracted_at DESC);

COMMIT;
