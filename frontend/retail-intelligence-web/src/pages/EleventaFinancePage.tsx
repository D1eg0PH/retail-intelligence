import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api } from "../api/axios";

const tokenKey = "retail-intelligence-eleventa-token";
const root = "/integrations/eleventa/v1/finance";
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const dateTime = new Intl.DateTimeFormat("es-MX", { dateStyle: "short", timeStyle: "short", timeZone: "America/Mexico_City" });
const amount = (value: number | null | undefined) => value == null ? "No disponible" : money.format(value);
const kinds: Record<string, { label: string; hint: string }> = {
    purchase: { label: "Compra de mercancía recibida", hint: "Registra el valor de la mercancía que entró, aunque todavía no la pagues. El pago se captura por separado." },
    "supplier-payment": { label: "Pago a proveedor", hint: "Registra el dinero que realmente pagaste. Si también recibiste mercancía, registra su compra por separado." },
    expense: { label: "Gasto del negocio pagado", hint: "Nómina, servicios, renta y otros gastos ya pagados. Reduce la utilidad estimada y el flujo registrado." },
    "owner-withdrawal": { label: "Retiro personal de dinero", hint: "Dinero del negocio que tomas para uso personal. Reduce el flujo, sin restarlo como gasto del negocio." },
    "customer-collection": { label: "Cobro recibido de clientes", hint: "Dinero recibido por ventas o abonos. Puedes capturar el total cobrado del día; no dupliques esos cobros. Las ventas de Eleventa no se agregan automáticamente al flujo." },
    "purchase-return": { label: "Devolución de compra a proveedor", hint: "Valor a costo de la mercancía que devolviste. Si recibiste dinero, registra también el reembolso del proveedor." },
    "supplier-refund": { label: "Reembolso recibido de proveedor", hint: "Dinero devuelto por un proveedor. La devolución de mercancía se registra por separado." },
    "customer-refund": { label: "Reembolso pagado a cliente", hint: "Dinero que entregaste por una devolución. La devolución de venta debe estar registrada en Eleventa para ajustar las ventas." },
    "owner-contribution": { label: "Aportación de dinero del dueño", hint: "Dinero personal que entra al negocio. Aumenta el flujo registrado, sin contarlo como una venta o ganancia." },
    "stock-withdrawal": { label: "Mercancía retirada para uso personal", hint: "Registra su valor a costo y descuéntala también del inventario de Eleventa. La separaremos del costo de lo vendido; no representa una salida de efectivo." }
};
const categories: Record<string, string> = { payroll: "Nómina", services: "Servicios", rent: "Renta", transport: "Transporte", maintenance: "Mantenimiento", other: "Otros" };
type Entry = { entryId: string; kind: string; occurredOn: string; amount: number; category: string; description: string; counterparty: string; reference: string; notes: string; voidedAtUtc: string | null; voidReason: string | null };
type Closing = { date: string; status: string; capturedAtUtc: string | null; value: number | null };
type Summary = { ticketCount: number; sales: number; returns: number; netSales: number; ticketMargin: number | null; purchases: number; supplierPayments: number; expenses: number; ownerWithdrawals: number; stockWithdrawals: number; cashIn: number; cashOut: number; recordedCashFlow: number; openingInventory: number | null; closingInventory: number | null; merchandiseCost: number | null; estimatedProfit: number | null; profitBasis: string; lastSyncAtUtc: string | null; opening: Closing | null; closing: Closing | null; expensesByCategory: { category: string; amount: number }[]; warnings: string[] };
type Page = { total: number; entries: Entry[] };
type Draft = { entryId: string; kind: string; occurredOn: string; amount: string; category: string; description: string; counterparty: string; reference: string; notes: string };

function todayMexico() {
    const parts = new Intl.DateTimeFormat("en-US", { year: "numeric", month: "2-digit", day: "2-digit", timeZone: "America/Mexico_City" }).formatToParts(new Date());
    const part = (type: string) => parts.find(item => item.type === type)?.value;
    return `${part("year")}-${part("month")}-${part("day")}`;
}
function period(anchor: string, mode: string) {
    const date = new Date(`${anchor}T12:00:00Z`);
    let start: Date, end: Date;
    if (mode === "month") {
        start = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1, 12));
        end = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0, 12));
    } else {
        start = new Date(date); start.setUTCDate(start.getUTCDate() - (start.getUTCDay() + 6) % 7);
        end = new Date(start); end.setUTCDate(end.getUTCDate() + 6);
    }
    return { from: start.toISOString().slice(0, 10), to: end.toISOString().slice(0, 10) };
}
function newDraft(): Draft { return { entryId: crypto.randomUUID(), kind: "purchase", occurredOn: todayMexico(), amount: "", category: "", description: "", counterparty: "", reference: "", notes: "" }; }
function errorText(error: unknown) {
    const err = error as { response?: { status?: number; data?: { detail?: string; title?: string } } };
    return err.response?.status === 401 ? "El token no es válido o fue rotado. Vuelve a conectar con el token de Eleventa."
        : err.response?.data?.detail ?? err.response?.data?.title ?? "No se pudo confirmar la operación. Puedes reintentar sin duplicar el movimiento.";
}

export default function EleventaFinancePage() {
    const [token, setToken] = useState(() => sessionStorage.getItem(tokenKey) ?? "");
    const [tokenInput, setTokenInput] = useState("");
    const [mode, setMode] = useState("month");
    const [anchor, setAnchor] = useState(todayMexico);
    const { from, to } = useMemo(() => period(anchor, mode), [anchor, mode]);
    const [skip, setSkip] = useState(0);
    const [revision, setRevision] = useState(0);
    const [result, setResult] = useState<{ key: string; summary: Summary; page: Page } | null>(null);
    const [readError, setReadError] = useState<{ key: string; message: string } | null>(null);
    const [writeError, setWriteError] = useState("");
    const [success, setSuccess] = useState("");
    const [draft, setDraft] = useState<Draft>(newDraft);
    const [saving, setSaving] = useState(false);
    const [voiding, setVoiding] = useState<string | null>(null);
    const take = 50;
    const requestKey = `${token}|${from}|${to}|${skip}|${revision}`;
    const current = result?.key === requestKey ? result : null;
    const error = readError?.key === requestKey ? readError.message : "";
    const loading = !!token && !current && !error;
    useEffect(() => {
        if (!token) return;
        const controller = new AbortController();
        const config = { headers: { Authorization: `Bearer ${token}` }, signal: controller.signal };
        Promise.all([
            api.get<Summary>(`${root}/summary`, { ...config, params: { from, to } }),
            api.get<Page>(`${root}/entries`, { ...config, params: { from, to, skip, take } })
        ]).then(([summary, page]) => { if (!controller.signal.aborted) setResult({ key: requestKey, summary: summary.data, page: page.data }); })
            .catch(err => { if (!controller.signal.aborted) setReadError({ key: requestKey, message: errorText(err) }); });
        return () => controller.abort();
    }, [token, from, to, skip, requestKey]);

    function connect(event: FormEvent) {
        event.preventDefault(); const value = tokenInput.trim(); if (!value) return;
        sessionStorage.setItem(tokenKey, value); setToken(value); setTokenInput(""); setRevision(value => value + 1);
    }
    function field<K extends keyof Draft>(name: K, value: Draft[K]) { setDraft(old => ({ ...old, [name]: value })); }
    async function save(event: FormEvent) {
        event.preventDefault(); if (saving) return;
        setSaving(true); setWriteError(""); setSuccess("");
        try {
            const response = await api.post<Entry>(`${root}/entries`, { ...draft, amount: Number(draft.amount), category: draft.kind === "expense" ? draft.category : "" }, { headers: { Authorization: `Bearer ${token}` } });
            setSuccess(response.data.voidedAtUtc ? "Este movimiento ya estaba registrado y anulado. Se conservó su historial." : "Movimiento guardado.");
            setAnchor(draft.occurredOn); setSkip(0); setRevision(value => value + 1); setDraft(newDraft());
        } catch (err) { setWriteError(errorText(err)); setRevision(value => value + 1); }
        finally { setSaving(false); }
    }
    async function voidEntry(entry: Entry) {
        const reason = window.prompt(`Motivo para anular “${entry.description}”. El registro se conservará en el historial:`);
        if (!reason?.trim() || voiding) return;
        setVoiding(entry.entryId); setWriteError(""); setSuccess("");
        try {
            await api.post(`${root}/entries/${entry.entryId}/void`, { reason }, { headers: { Authorization: `Bearer ${token}` } });
            setSuccess("Movimiento anulado; se conserva su registro original."); setRevision(value => value + 1);
        } catch (err) { setWriteError(errorText(err)); }
        finally { setVoiding(null); }
    }
    const inputClass = "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-blue-500 outline-none";
    const summary = current?.summary;
    return <div className="space-y-6">
        <header><p className="text-sm font-semibold uppercase tracking-wide text-blue-700">Eleventa · Control del negocio</p>
            <h1 className="mt-1 text-3xl font-bold text-slate-900">Gastos y ganancias</h1>
            <p className="mt-2 text-slate-600">Registra compras, pagos y gastos. Consulta la utilidad estimada y el dinero que entra y sale.</p></header>
        {!token && <form onSubmit={connect} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <label htmlFor="finance-token" className="mb-2 block font-semibold">Conectar con Eleventa</label>
            <div className="flex gap-3"><input id="finance-token" type="password" required autoComplete="off" placeholder="Token de integración" className={inputClass} value={tokenInput} onChange={event => setTokenInput(event.target.value)} />
                <button className="rounded-lg bg-blue-600 px-5 py-2 text-white">Conectar</button></div></form>}
        {token && <>
            <section className="flex flex-wrap items-end gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <label className="space-y-1 text-sm">Período<select aria-label="Período" className={inputClass} value={mode} onChange={event => { setMode(event.target.value); setSkip(0); }}><option value="month">Mes</option><option value="week">Semana de lunes a domingo</option></select></label>
                <label className="space-y-1 text-sm">Fecha dentro del período<input aria-label="Fecha dentro del período" type="date" min="2000-01-03" max={todayMexico()} required className={inputClass} value={anchor} onChange={event => { if (event.target.value) { setAnchor(event.target.value); setSkip(0); } }} /></label>
                <p className="py-2 text-sm text-slate-600">{from} al {to}</p>
                <button disabled={loading} onClick={() => setRevision(value => value + 1)} className="rounded-lg border border-slate-300 px-4 py-2 text-sm disabled:opacity-50">Actualizar</button>
                <button onClick={() => { sessionStorage.removeItem(tokenKey); setToken(""); setResult(null); }} className="py-2 text-sm text-slate-500 underline">Desconectar</button>
            </section>
            {(error || writeError) && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-800">{writeError || error}</p>}
            {success && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-emerald-800">{success}</p>}
            {loading && <p role="status" className="p-4 text-slate-500">Consultando movimientos, ventas y cierres…</p>}
            {summary && <>
                <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    <Metric label="Ventas netas de Eleventa" value={summary.netSales} detail={`${summary.ticketCount} tickets · devoluciones ${amount(summary.returns)}`} />
                    <Metric label="Gastos del negocio pagados" value={summary.expenses} detail="Excluye compras y retiros personales" />
                    <Metric label="Utilidad estimada" value={summary.estimatedProfit} detail={summary.profitBasis === "inventory" ? "Con inventario inicial y final" : summary.profitBasis === "tickets" ? "Provisional · margen Eleventa menos gastos" : "Faltan datos para calcularla"} />
                    <Metric label="Flujo registrado" value={summary.recordedCashFlow} detail="Solo cobros y pagos capturados; no es saldo de caja" />
                </div>
                <details className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950"><summary className="cursor-pointer font-semibold">Cómo interpretar el resultado y datos a revisar</summary>
                    <ul className="mt-3 list-disc space-y-2 pl-5">{summary.warnings.map(warning => <li key={warning}>{warning}</li>)}</ul></details>
                <div className="grid gap-5 lg:grid-cols-2">
                    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="mb-4 text-lg font-bold">Cálculo por inventario</h2>
                        <dl className="space-y-3 text-sm"><Line label="Inventario inicial" value={summary.openingInventory} /><Line label="Compras netas recibidas" value={summary.purchases} />
                            <Line label="Menos inventario final" value={summary.closingInventory} /><Line label="Menos mercancía retirada para uso personal" value={summary.stockWithdrawals} />
                            <Line label="Costo de mercancía y diferencias de inventario" value={summary.merchandiseCost} /><Line label="Ventas netas menos ese costo y gastos pagados" value={summary.profitBasis === "inventory" ? summary.estimatedProfit : null} /></dl>
                        <p className="mt-4 text-xs text-slate-500">Inicial: {summary.opening?.capturedAtUtc ? dateTime.format(new Date(summary.opening.capturedAtUtc)) : "Sin cierre disponible"}. Final: {summary.closing?.capturedAtUtc ? dateTime.format(new Date(summary.closing.capturedAtUtc)) : "Sin cierre disponible"}.</p>
                    </section>
                    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="mb-4 text-lg font-bold">Entradas y salidas registradas</h2>
                        <dl className="space-y-3 text-sm"><Line label="Cobros, reembolsos recibidos y aportaciones" value={summary.cashIn} /><Line label="Pagos, gastos, reembolsos y retiros" value={summary.cashOut} />
                            <Line label="Pagos a proveedores" value={summary.supplierPayments} /><Line label="Retiros personales de dinero" value={summary.ownerWithdrawals} /><Line label="Flujo del período" value={summary.recordedCashFlow} /></dl>
                        {summary.expensesByCategory.length > 0 && <div className="mt-5 border-t border-slate-100 pt-4"><h3 className="mb-3 text-sm font-semibold">Gastos por categoría</h3><dl className="space-y-2 text-sm">{summary.expensesByCategory.map(category => <Line key={category.category} label={categories[category.category] ?? category.category} value={category.amount} />)}</dl></div>}
                    </section>
                </div>
                <p className="text-xs text-slate-500">Último lote de Eleventa confirmado: {summary.lastSyncAtUtc ? dateTime.format(new Date(summary.lastSyncAtUtc)) : "Sin dato"}. Los datos sincronizados pueden llevar retraso.</p>
            </>}
            <form onSubmit={save} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <h2 className="text-lg font-bold">Registrar movimiento</h2>
                <fieldset disabled={saving} className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3 disabled:opacity-60">
                    <label className="space-y-1 text-sm">Tipo<select className={inputClass} value={draft.kind} onChange={event => setDraft(old => ({ ...old, kind: event.target.value, category: event.target.value === "expense" ? "other" : "" }))}>{Object.entries(kinds).map(([value, kind]) => <option key={value} value={value}>{kind.label}</option>)}</select></label>
                    <label className="space-y-1 text-sm">Fecha<input type="date" required min="2000-01-01" max={todayMexico()} className={inputClass} value={draft.occurredOn} onChange={event => field("occurredOn", event.target.value)} /></label>
                    <label className="space-y-1 text-sm">Importe en MXN<input type="number" inputMode="decimal" required min="0.01" max="999999999.99" step="0.01" className={inputClass} value={draft.amount} onChange={event => field("amount", event.target.value)} placeholder="0.00" /></label>
                    {draft.kind === "expense" && <label className="space-y-1 text-sm">Categoría<select required className={inputClass} value={draft.category} onChange={event => field("category", event.target.value)}>{Object.entries(categories).map(([value, name]) => <option key={value} value={value}>{name}</option>)}</select></label>}
                    <label className="space-y-1 text-sm">Descripción<input required maxLength={250} className={inputClass} value={draft.description} onChange={event => field("description", event.target.value)} placeholder="Qué compraste, pagaste o retiraste" /></label>
                    <label className="space-y-1 text-sm">Proveedor o persona · opcional<input maxLength={150} className={inputClass} value={draft.counterparty} onChange={event => field("counterparty", event.target.value)} /></label>
                    <label className="space-y-1 text-sm">Referencia · opcional<input maxLength={100} className={inputClass} value={draft.reference} onChange={event => field("reference", event.target.value)} placeholder="Nota, factura o comprobante" /></label>
                    <label className="space-y-1 text-sm md:col-span-2">Notas · opcional<textarea maxLength={1000} rows={2} className={inputClass} value={draft.notes} onChange={event => field("notes", event.target.value)} /></label>
                </fieldset>
                <p className="mt-4 rounded-lg bg-blue-50 p-3 text-sm text-blue-900">{kinds[draft.kind].hint}</p>
                <button disabled={saving || !!voiding} className="mt-4 rounded-lg bg-blue-600 px-5 py-2 font-semibold text-white hover:bg-blue-700 disabled:opacity-50">{saving ? "Guardando…" : "Guardar movimiento"}</button>
            </form>
            {current && <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                <div className="p-5"><h2 className="text-lg font-bold">Movimientos del período</h2><p className="mt-1 text-sm text-slate-500">Para corregir uno, anúlalo y registra el correcto. El original se conserva.</p></div>
                <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50 text-slate-500"><tr><th className="px-5 py-3">Fecha</th><th className="px-5 py-3">Movimiento</th><th className="px-5 py-3">Descripción</th><th className="px-5 py-3">Proveedor / referencia</th><th className="px-5 py-3 text-right">Importe</th><th className="px-5 py-3">Estado</th></tr></thead>
                    <tbody className="divide-y divide-slate-100">{current.page.entries.map(entry => <tr key={entry.entryId} className={entry.voidedAtUtc ? "bg-slate-50 text-slate-400" : ""}>
                        <td className="whitespace-nowrap px-5 py-3">{entry.occurredOn}</td><td className="px-5 py-3">{kinds[entry.kind]?.label ?? entry.kind}{entry.category && <span className="block text-xs">{categories[entry.category]}</span>}</td>
                        <td className="px-5 py-3"><span className="break-words">{entry.description}</span>{entry.notes && <p className="mt-1 text-xs text-slate-500">{entry.notes}</p>}</td><td className="px-5 py-3">{entry.counterparty || "—"}{entry.reference && <p className="text-xs">{entry.reference}</p>}</td>
                        <td className="px-5 py-3 text-right tabular-nums">{amount(entry.amount)}</td><td className="px-5 py-3">{entry.voidedAtUtc ? <span title={entry.voidReason ?? ""}>Anulado{entry.voidReason && <span className="block text-xs">{entry.voidReason}</span>}</span> : <button disabled={!!voiding || saving} onClick={() => void voidEntry(entry)} className="text-sm text-red-700 disabled:opacity-50">{voiding === entry.entryId ? "Anulando…" : "Anular"}</button>}</td>
                    </tr>)}{!current.page.entries.length && <tr><td colSpan={6} className="px-5 py-10 text-center text-slate-500">No hay movimientos capturados en este período.</td></tr>}</tbody></table></div>
                <div className="flex justify-between border-t border-slate-100 p-5 text-sm"><button disabled={skip === 0} onClick={() => setSkip(value => Math.max(0, value - take))} className="text-blue-700 disabled:opacity-40">Anteriores</button><span>{current.page.total} movimientos</span><button disabled={skip + take >= current.page.total} onClick={() => setSkip(value => value + take)} className="text-blue-700 disabled:opacity-40">Siguientes</button></div>
            </section>}
        </>}
    </div>;
}

function Line({ label, value }: { label: string; value: number | null }) { return <div className="flex justify-between gap-4"><dt className="text-slate-600">{label}</dt><dd className="text-right font-medium tabular-nums">{amount(value)}</dd></div>; }
function Metric({ label, value, detail }: { label: string; value: number | null; detail: string }) { return <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="text-sm text-slate-500">{label}</h2><p className="mt-3 text-2xl font-bold tabular-nums">{amount(value)}</p><p className="mt-2 text-xs text-slate-500">{detail}</p></section>; }
