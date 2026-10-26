import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import { AlertTriangle, Boxes, Clock3, PackageSearch, RefreshCw, Search } from "lucide-react";
import { api } from "../api/axios";

type CleanupProduct = {
    sourceProductId: number; code: string; description: string; departmentId: number | null; departmentName: string | null; quantity: number | null;
    cost: number | null; salePrice: number | null; lastSoldAt: string | null;
    daysSinceSale: number | null; classification: string; inventoryCostValue: number | null; isDeleted: boolean;
};
type CleanupDepartment = { departmentId: number; name: string; productCount: number };
type CleanupData = {
    inventoryAtUtc: string | null; salesThrough: string | null; inactiveDaysThreshold: number;
    totalProducts: number; candidateCount: number; candidateInventoryCostValue: number;
    recentMovementCount: number; noSalesObservedCount: number; noInventoryCount: number;
    deletedCount: number; missingInventoryCount: number; skip: number; take: number;
    filteredCount: number; departments: CleanupDepartment[]; products: CleanupProduct[];
    classificationChart: { classification: string; label: string; productCount: number }[];
    departmentValueChart: { departmentId: number | null; departmentName: string; candidateCount: number; inventoryCostValue: number }[];
    salesAgeChart: { bucket: string; label: string; productCount: number }[];
};
type CleanupFilter = "all" | "candidate" | "recent-movement" | "no-sales-observed" | "no-inventory" | "deleted" | "missing-inventory";

const tokenStorageKey = "retail-intelligence-eleventa-token";
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const number = new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 });
const pageSize = 100;
const classifications: Record<string, { label: string; classes: string }> = {
    candidate: { label: "Candidato a revisión", classes: "bg-amber-100 text-amber-800" },
    "recent-movement": { label: "Movimiento reciente", classes: "bg-emerald-100 text-emerald-800" },
    "no-sales-observed": { label: "Sin venta observada", classes: "bg-slate-100 text-slate-700" },
    "no-inventory": { label: "Sin existencia", classes: "bg-blue-100 text-blue-800" },
    deleted: { label: "Eliminado en Eleventa", classes: "bg-rose-100 text-rose-800" },
    "missing-inventory": { label: "Sin dato de existencia", classes: "bg-violet-100 text-violet-800" }
};

export default function EleventaProductCleanupPage() {
    const [token, setToken] = useState(() => sessionStorage.getItem(tokenStorageKey) ?? "");
    const [tokenInput, setTokenInput] = useState("");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [inactiveDays, setInactiveDays] = useState(60);
    const [classification, setClassification] = useState<CleanupFilter>("all");
    const [departmentId, setDepartmentId] = useState("all");
    const [skip, setSkip] = useState(0);
    const [data, setData] = useState<CleanupData | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const load = useCallback(async (apiToken: string) => {
        setLoading(true); setError("");
        try {
            const response = await api.get<CleanupData>("/integrations/eleventa/v1/products/cleanup", {
                headers: { Authorization: `Bearer ${apiToken}` },
                params: { skip, take: pageSize, inactiveDays, search: search || undefined, classification,
                    departmentId: departmentId === "all" ? undefined : Number(departmentId) }
            });
            setData(response.data);
        } catch (err: unknown) {
            const requestError = err as { response?: { status?: number }; message?: string };
            const status = requestError.response?.status;
            setError(status === 401 ? "El token no es válido o fue rotado. Conecta con el token actual del agente Eleventa."
                : status ? `La API respondió con error HTTP ${status}. Intenta de nuevo más tarde.`
                    : `No se recibió respuesta de la API (${requestError.message ?? "error de red o CORS"}).`);
        } finally { setLoading(false); }
    }, [classification, departmentId, inactiveDays, search, skip]);

    useEffect(() => { if (token) void load(token); }, [token, load]);

    function connect(event: FormEvent<HTMLFormElement>) {
        event.preventDefault(); const value = tokenInput.trim(); if (!value) return;
        sessionStorage.setItem(tokenStorageKey, value); setData(null); setToken(value);
    }
    function submitSearch(event: FormEvent<HTMLFormElement>) {
        event.preventDefault(); setSkip(0); setSearch(searchInput.trim());
    }
    function disconnect() {
        sessionStorage.removeItem(tokenStorageKey); setToken(""); setTokenInput(""); setData(null);
    }

    const totalPages = data ? Math.max(1, Math.ceil(data.filteredCount / pageSize)) : 1;
    const currentPage = Math.floor(skip / pageSize) + 1;
    return <div className="space-y-6">
        <header>
            <p className="text-sm font-semibold uppercase tracking-wide text-blue-700">Eleventa · solo consulta</p>
            <h1 className="mt-1 text-3xl font-bold text-slate-900">Depurador de productos</h1>
            <p className="mt-2 text-slate-600">Encuentra inventario sin movimiento reciente para revisar. El sistema no elimina ni modifica productos.</p>
        </header>

        {!token && <form onSubmit={connect} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <label htmlFor="cleanup-token" className="mb-2 block font-semibold text-slate-800">Token de integración</label>
            <p className="mb-3 text-sm text-slate-600">Se conserva en esta pestaña mientras dure la sesión del navegador.</p>
            <div className="flex flex-col gap-3 sm:flex-row">
                <input id="cleanup-token" type="password" autoComplete="off" value={tokenInput}
                    onChange={event => setTokenInput(event.target.value)} placeholder="Token de API"
                    className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
                <button type="submit" className="rounded-lg bg-blue-600 px-5 py-2 font-semibold text-white hover:bg-blue-700">Conectar</button>
            </div>
        </form>}

        {error && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-800">{error}</div>}
        {data && <>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <Metric icon={PackageSearch} label="Candidatos a revisión" value={number.format(data.candidateCount)} detail={`Sin venta ≥ ${data.inactiveDaysThreshold} días y con existencia`} />
                <Metric icon={Boxes} label="Valor detenido estimado" value={money.format(data.candidateInventoryCostValue)} detail="Existencia × costo registrado" />
                <Metric icon={Clock3} label="Con movimiento reciente" value={number.format(data.recentMovementCount)} />
                <Metric icon={AlertTriangle} label="Sin ventas observadas" value={number.format(data.noSalesObservedCount)} detail="No se clasifican como producto muerto" />
            </div>
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
                <p className="font-semibold">Los candidatos son sugerencias para revisar.</p>
                <p className="mt-1">Se calculan con las ventas recibidas en Retail Intelligence. Mientras termina la carga inicial, las clasificaciones pueden cambiar. “Sin venta observada” no confirma que el producto nunca se haya vendido.</p>
            </div>

            <section aria-label="Gráficos de análisis del catálogo" className="grid gap-4 xl:grid-cols-3">
                <ChartCard title="Productos por clasificación" subtitle="Catálogo completo">
                    <BarList items={data.classificationChart.map(item => ({
                        label: item.label, value: item.productCount,
                        color: item.classification === "candidate" ? "bg-amber-500"
                            : item.classification === "recent-movement" ? "bg-emerald-500"
                                : item.classification === "no-sales-observed" ? "bg-slate-500"
                                    : item.classification === "no-inventory" ? "bg-blue-500"
                                        : item.classification === "deleted" ? "bg-rose-500" : "bg-violet-500"
                    }))} />
                </ChartCard>
                <ChartCard title="Valor candidato por departamento" subtitle="Existencia × costo de productos candidatos">
                    <BarList items={data.departmentValueChart.map(item => ({
                        label: item.departmentName, value: item.inventoryCostValue, detail: `${number.format(item.candidateCount)} productos`, color: "bg-blue-600"
                    }))} formatValue={value => money.format(value)} emptyLabel="No hay candidatos con valor registrado." />
                </ChartCard>
                <ChartCard title="Antigüedad de última venta" subtitle="Productos con existencia positiva; excluye eliminados">
                    <BarList items={data.salesAgeChart.map((item, index) => ({
                        label: item.label, value: item.productCount,
                        color: ["bg-slate-500", "bg-emerald-500", "bg-amber-400", "bg-orange-500", "bg-rose-600"][index]
                    }))} />
                </ChartCard>
            </section>

            <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                <div className="flex flex-col gap-4 border-b border-slate-100 p-5">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                        <div>
                            <h2 className="text-lg font-bold text-slate-900">Revisión de productos</h2>
                            <p className="mt-1 text-sm text-slate-500">
                                {data.inventoryAtUtc ? `Inventario actualizado ${new Date(data.inventoryAtUtc).toLocaleString("es-MX")}` : "Aún no hay una captura completa de inventario."}
                                {data.salesThrough ? ` · Ventas recibidas hasta ${new Date(data.salesThrough).toLocaleDateString("es-MX")}` : " · Sin ventas recibidas"}
                            </p>
                        </div>
                        <button onClick={() => void load(token)} disabled={loading}
                            className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                            <RefreshCw size={16} className={loading ? "animate-spin" : ""} /> Actualizar
                        </button>
                    </div>
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
                        <form onSubmit={submitSearch} className="flex min-w-0 flex-1">
                            <input value={searchInput} onChange={event => setSearchInput(event.target.value)}
                                placeholder="Buscar por código o descripción" aria-label="Buscar producto"
                                className="min-w-0 flex-1 rounded-l-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500" />
                            <button aria-label="Buscar" className="rounded-r-lg border border-l-0 border-slate-300 px-3 text-slate-600 hover:bg-slate-50"><Search size={18} /></button>
                        </form>
                        <label className="flex items-center gap-2 text-sm text-slate-600">Sin venta por
                            <select value={inactiveDays} onChange={event => { setInactiveDays(Number(event.target.value)); setSkip(0); }}
                                className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-800">
                                {[30, 60, 90, 120, 180].map(days => <option key={days} value={days}>{days} días</option>)}
                            </select>
                        </label>
                        <select value={classification} onChange={event => { setClassification(event.target.value as CleanupFilter); setSkip(0); }}
                            aria-label="Filtrar por clasificación" className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800">
                            <option value="all">Todas las clasificaciones</option><option value="candidate">Candidatos a revisión</option>
                            <option value="recent-movement">Movimiento reciente</option><option value="no-sales-observed">Sin ventas observadas</option>
                            <option value="no-inventory">Sin existencia</option><option value="deleted">Eliminados en Eleventa</option>
                            <option value="missing-inventory">Sin dato de existencia</option>
                        </select>
                        <select value={departmentId} onChange={event => { setDepartmentId(event.target.value); setSkip(0); }}
                            aria-label="Filtrar por departamento" className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800">
                            <option value="all">Todos los departamentos</option>
                            {data.departments.map(department => <option key={department.departmentId} value={department.departmentId}>
                                {department.name} ({department.productCount})
                            </option>)}
                        </select>
                    </div>
                </div>
                <div className="overflow-x-auto">
                    <table className="w-full min-w-[1100px] text-left text-sm">
                        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr>
                            <th className="px-5 py-3">Código</th><th className="px-5 py-3">Producto</th><th className="px-5 py-3">Departamento</th>
                            <th className="px-5 py-3 text-right">Existencia</th><th className="px-5 py-3 text-right">Costo</th>
                            <th className="px-5 py-3 text-right">Valor existencia</th><th className="px-5 py-3">Última venta</th>
                            <th className="px-5 py-3">Clasificación</th>
                        </tr></thead>
                        <tbody className="divide-y divide-slate-100">
                            {data.products.map(product => {
                                const badge = classifications[product.classification] ?? classifications["missing-inventory"];
                                return <tr key={product.sourceProductId} className="hover:bg-slate-50">
                                    <td className="px-5 py-3 font-mono text-slate-600">{product.code || "—"}</td>
                                    <td className="px-5 py-3 font-medium text-slate-900">{product.description || "Sin descripción"}</td>
                                    <td className="px-5 py-3 text-slate-600">{product.departmentName || (product.departmentId == null ? "Sin departamento" : `Departamento ${product.departmentId}`)}</td>
                                    <td className="px-5 py-3 text-right tabular-nums">{product.quantity == null ? "—" : number.format(product.quantity)}</td>
                                    <td className="px-5 py-3 text-right tabular-nums">{product.cost == null ? "—" : money.format(product.cost)}</td>
                                    <td className="px-5 py-3 text-right tabular-nums">{product.inventoryCostValue == null ? "—" : money.format(product.inventoryCostValue)}</td>
                                    <td className="px-5 py-3 text-slate-600">{product.lastSoldAt ? `${new Date(product.lastSoldAt).toLocaleDateString("es-MX")} · ${product.daysSinceSale} días` : "Sin venta observada"}</td>
                                    <td className="px-5 py-3"><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${badge.classes}`}>{badge.label}</span></td>
                                </tr>;
                            })}
                            {data.products.length === 0 && <tr><td colSpan={8} className="px-5 py-12 text-center text-slate-500">{loading ? "Cargando…" : "No hay productos para estos filtros."}</td></tr>}
                        </tbody>
                    </table>
                </div>
                <div className="flex flex-col gap-3 border-t border-slate-100 px-5 py-3 text-sm text-slate-500 sm:flex-row sm:items-center sm:justify-between">
                    <span>Mostrando {data.filteredCount === 0 ? 0 : skip + 1}–{Math.min(skip + data.products.length, data.filteredCount)} de {data.filteredCount} productos · {data.totalProducts} en catálogo</span>
                    <div className="flex items-center gap-2">
                        <button onClick={() => setSkip(Math.max(0, skip - pageSize))} disabled={skip === 0 || loading} className="rounded-lg border border-slate-300 px-3 py-1.5 disabled:opacity-40">Anterior</button>
                        <span>Página {currentPage} de {totalPages}</span>
                        <button onClick={() => setSkip(skip + pageSize)} disabled={currentPage >= totalPages || loading} className="rounded-lg border border-slate-300 px-3 py-1.5 disabled:opacity-40">Siguiente</button>
                    </div>
                </div>
            </section>
            <p className="text-xs text-slate-500">El valor usa el costo registrado en catálogo y la existencia de la última captura completa. Los productos sin costo quedan sin valuación.</p>
            <button onClick={disconnect} className="text-sm text-slate-500 underline hover:text-slate-800">Desconectar token</button>
        </>}
    </div>;
}

function Metric({ icon: Icon, label, value, detail }: { icon: typeof PackageSearch; label: string; value: string; detail?: string }) {
    return <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between text-slate-500"><span className="text-sm">{label}</span><Icon size={18} /></div>
        <p className="mt-3 text-2xl font-bold tabular-nums text-slate-900">{value}</p>
        {detail && <p className="mt-1 text-xs text-slate-500">{detail}</p>}
    </div>;
}

type BarItem = { label: string; value: number; detail?: string; color: string };

function ChartCard({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
    return <article className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="font-bold text-slate-900">{title}</h2>
        <p className="mt-1 text-xs text-slate-500">{subtitle}</p>
        <div className="mt-5">{children}</div>
    </article>;
}

function BarList({ items, formatValue = value => number.format(value), emptyLabel = "Sin datos para mostrar." }: {
    items: BarItem[]; formatValue?: (value: number) => string; emptyLabel?: string;
}) {
    const max = Math.max(1, ...items.map(item => item.value));
    if (items.length === 0 || items.every(item => item.value === 0))
        return <p className="py-8 text-center text-sm text-slate-500">{emptyLabel}</p>;
    return <div className="space-y-3">
        {items.map(item => <div key={item.label}>
            <div className="mb-1 flex items-baseline justify-between gap-3 text-xs">
                <span className="truncate text-slate-600" title={item.label}>{item.label}</span>
                <span className="shrink-0 font-semibold tabular-nums text-slate-800">{formatValue(item.value)}</span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-slate-100" role="img" aria-label={`${item.label}: ${formatValue(item.value)}`}>
                <div className={`h-full rounded-full ${item.color}`} style={{ width: `${Math.max(2, item.value / max * 100)}%` }} />
            </div>
            {item.detail && <p className="mt-1 text-[11px] text-slate-400">{item.detail}</p>}
        </div>)}
    </div>;
}
