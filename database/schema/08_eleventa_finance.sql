-- Execute before deploying the finance API. Existing Eleventa data is unchanged.
CREATE TABLE IF NOT EXISTS public.eleventa_finance_entries (
    source_id UUID NOT NULL REFERENCES public.eleventa_sources(source_id) ON DELETE RESTRICT,
    entry_id UUID NOT NULL,
    kind VARCHAR(30) NOT NULL CHECK (kind IN ('purchase','purchase-return','supplier-payment','supplier-refund',
        'expense','owner-withdrawal','owner-contribution','customer-collection','customer-refund','stock-withdrawal')),
    occurred_on DATE NOT NULL CHECK (occurred_on >= DATE '2000-01-01'),
    amount NUMERIC(19,2) NOT NULL CHECK (amount > 0 AND amount <= 999999999.99),
    category VARCHAR(30) NOT NULL DEFAULT '',
    description VARCHAR(250) NOT NULL CHECK (length(trim(description)) > 0),
    counterparty VARCHAR(150) NOT NULL DEFAULT '',
    reference VARCHAR(100) NOT NULL DEFAULT '',
    notes VARCHAR(1000) NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL,
    voided_at TIMESTAMPTZ NULL,
    void_reason VARCHAR(300) NULL,
    PRIMARY KEY(source_id, entry_id),
    CHECK ((kind = 'expense' AND category IN ('payroll','services','rent','transport','maintenance','other')) OR
           (kind <> 'expense' AND category = '')),
    CHECK ((voided_at IS NULL AND void_reason IS NULL) OR (voided_at IS NOT NULL AND length(trim(void_reason)) > 0))
);
CREATE INDEX IF NOT EXISTS ix_eleventa_finance_entries_source_date
    ON public.eleventa_finance_entries(source_id, occurred_on);
ALTER TABLE public.eleventa_finance_entries ENABLE ROW LEVEL SECURITY;
-- Access is through the existing authenticated API; no direct anon/browser policies.
REVOKE ALL ON public.eleventa_finance_entries FROM anon, authenticated;
