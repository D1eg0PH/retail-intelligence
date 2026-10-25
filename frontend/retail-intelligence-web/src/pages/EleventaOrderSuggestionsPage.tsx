import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { AlertTriangle, Boxes, ClipboardList, RefreshCw, Search } from "lucide-react";
import { api } from "../api/axios";

type Suggestion = {
    sourceProductId: number; code: string; description: string; departmentId: number | null;
    departmentName: string | null; supplierId: number | null; supplierName: string | null;
    salesInPeriod: number; averageDailySales: number;
    currentInventory: number; targetQuantity: number; suggestedQuantity: number;
    cost: number | null; estimatedCost: number | null; lastSoldAt: string | null;
};
type SuggestionsData = {
    inventoryAtUtc: string | null; salesWindowStart: string; salesThrough: string | null;
    weeksAverage: number; targetInventoryDays: number; bufferPercent: number;
    productsWithSuggestions: number; estimatedCost: number; products: Suggestion[];
};

const tokenStorageKey = "retail-intelligence-eleventa-token";
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const number = new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 });

export default function EleventaOrderSuggestionsPage() {
    const [token, setToken] = useState(() => sessionStorage.getItem(tokenStorageKey) ?? "");
    const [tokenInput, setTokenInput] = useState("");
    const [data, setData] = useState<SuggestionsData | null>(null);
    const [search, setSearch] = useState("");
    const [department, setDepartment] = useState("all");
    const [supplier, setSupplier] = useState("all");
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const load = useCallback(async (apiToken: string) => {
        setLoading(true); setError("");
        try {
            const response = await api.get<SuggestionsData>("/integrations/eleventa/v1/order-suggestions", {
                headers: { Authorization: `Bearer ${apiToken}` }
            });
            setData(response.data);
        } catch (err: unknown) {
            const requestError = err as { response?: { status?: number }; message?: string };
            const status = requestError.response?.status;
            setError(status === 401 ? "El token no es válido o fue rotado. Conecta con el token actual del agente Eleventa."
                : status ? `La API respondió con error HTTP ${status}. Intenta de nuevo más tarde.`
                    : `No se recibió respuesta de la API (${requestError.message ?? "error de red o CORS"}).`);
        } finally { setLoading(false); }
    }, []);

    useEffect(() => { if (token) void load(token); }, [token, load]);

    function connect(event: FormEvent<HTMLFormElement>) {
        event.preventDefault(); const value = tokenInput.trim(); if (!value) return;
        sessionStorage.setItem(tokenStorageKey, value); setData(null); setToken(value);
    }
    function disconnect() {
        sessionStorage.removeItem(tokenStorageKey); setToken(""); setTokenInput(""); setData(null);
    }

    const departments = useMemo(() => [...new Map((data?.products ?? [])
        .filter(product => product.departmentId != null)
        .map(product => [product.departmentId!, product.departmentName || `Departamento ${product.departmentId}`])).entries()]
        .sort((a, b) => a[1].localeCompare(b[1], "es-MX")), [data]);
    const suppliers = useMemo(() => [...new Map((data?.products ?? [])
        .filter(product => product.supplierId != null)
        .map(product => [product.supplierId!, product.supplierName || `Proveedor ${product.supplierId}`])).entries()]
        .sort((a, b) => a[1].localeCompare(b[1], "es-MX")), [data]);
    const visible = useMemo(() => (data?.products ?? []).filter(product => {
        const term = search.trim().toLocaleLowerCase();
        return (!term || product.code.toLocaleLowerCase().includes(term) || product.description.toLocaleLowerCase().includes(term))
            && (department === "all" || product.departmentId === Number(department))
            && (supplier === "all" || product.supplierId === Number(supplier));
    }), [data, search, department, supplier]);

    return <div className="space-y-6">
        <header>
            <p className="text-sm font-semibold uppercase tracking-wide text-blue-700">Eleventa · solo consulta</p>
            <h1 className="mt-1 text-3xl font-bold text-slate-900">Sugerencias de pedido</h1>
            <p className="mt-2 text-slate-600">Estimación por ventas y existencia actuales. No crea ni modifica pedidos.</p>
        </header>

        {!token && <form onSubmit={connect} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <label htmlFor="suggestions-token" className="mb-2 block font-semibold text-slate-800">Token de integración</label>
            <p className="mb-3 text-sm text-slate-600">Se conserva en esta pestaña mientras dure la sesión del navegador.</p>
            <div className="flex flex-col gap-3 sm:flex-row">
                <input id="suggestions-token" type="password" autoComplete="off" value={tokenInput} onChange={event => setTokenInput(event.target.value)}
                    className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2" placeholder="Token del agente" />
                <button className="rounded-lg bg-blue-600 px-4 py-2 font-semibold text-white hover:bg-blue-700">Conectar</button>
            </div>
        </form>}

        {error && <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-rose-800">{error}</div>}
        {token && data && <>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <Metric icon={ClipboardList} label="Productos sugeridos" value={number.format(data.productsWithSuggestions)} />
                <Metric icon={Boxes} label="Compra estimada a costo" value={money.format(data.estimatedCost)} />
                <Metric icon={RefreshCw} label="Promedio de ventas" value={`${data.weeksAverage} semanas`} detail={`Desde ${new Date(`${data.salesWindowStart}T12:00:00`).toLocaleDateString("es-MX")}`} />
                <Metric icon={AlertTriangle} label="Cobertura objetivo" value={`${data.targetInventoryDays} días`} detail={`Colchón ${number.format(data.bufferPercent)}%`} />
            </div>

            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
                <p className="font-semibold">Revisa estas cantidades antes de comprar.</p>
                <p className="mt-1">La estimación usa la configuración general y las ventas recibidas. El espejo no informa la cobertura histórica completa ni tiempos de entrega o múltiplos del proveedor; por eso estas cantidades son una referencia, no una orden de compra.</p>
            </div>

            <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                <div className="flex flex-col gap-4 border-b border-slate-100 p-5">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                        <div>
                            <h2 className="text-lg font-bold text-slate-900">Productos por resurtir</h2>
                            <p className="mt-1 text-sm text-slate-500">
                                Inventario: {data.inventoryAtUtc ? new Date(data.inventoryAtUtc).toLocaleString("es-MX") : "sin captura"}
                                {data.salesThrough ? ` · Última venta incluida: ${new Date(data.salesThrough).toLocaleDateString("es-MX")}` : " · Sin ventas en el período"}
                            </p>
                        </div>
                        <button onClick={() => void load(token)} disabled={loading} className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                            <RefreshCw size={16} className={loading ? "animate-spin" : ""} /> Actualizar
                        </button>
                    </div>
                    <div className="flex flex-col gap-3 lg:flex-row">
                        <label className="flex min-w-0 flex-1 items-center gap-2 rounded-lg border border-slate-300 px-3">
                            <Search size={17} className="shrink-0 text-slate-400" />
                            <input value={search} onChange={event => setSearch(event.target.value)} placeholder="Buscar código o producto" className="min-w-0 flex-1 py-2 text-sm outline-none" />
                        </label>
                        <select aria-label="Filtrar por departamento" value={department} onChange={event => setDepartment(event.target.value)} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm">
                            <option value="all">Todos los departamentos</option>{departments.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
                        </select>
                        <select aria-label="Filtrar por proveedor" value={supplier} onChange={event => setSupplier(event.target.value)} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm">
                            <option value="all">Todos los proveedores</option>{suppliers.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
                        </select>
                    </div>
                </div>
                <div className="overflow-x-auto">
                    <table className="w-full min-w-[1050px] text-left text-sm">
                        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr>
                            <th className="px-5 py-3">Código / producto</th><th className="px-5 py-3">Departamento / proveedor</th>
                            <th className="px-5 py-3 text-right">Venta promedio/día</th><th className="px-5 py-3 text-right">Existencia</th>
                            <th className="px-5 py-3 text-right">Cantidad sugerida</th><th className="px-5 py-3 text-right">Costo estimado</th>
                        </tr></thead>
                        <tbody className="divide-y divide-slate-100">
                            {visible.map(product => <tr key={product.sourceProductId} className="hover:bg-slate-50">
                                <td className="px-5 py-3"><span className="block font-mono text-xs text-slate-500">{product.code || "—"}</span><span className="font-medium text-slate-900">{product.description || "Sin descripción"}</span></td>
                                <td className="px-5 py-3 text-slate-600">{product.departmentName || "Sin departamento"} · {product.supplierName || "Sin proveedor"}</td>
                                <td className="px-5 py-3 text-right tabular-nums">{number.format(product.averageDailySales)}</td>
                                <td className="px-5 py-3 text-right tabular-nums">{number.format(product.currentInventory)}</td>
                                <td className="px-5 py-3 text-right font-semibold tabular-nums text-blue-700">{number.format(product.suggestedQuantity)}</td>
                                <td className="px-5 py-3 text-right tabular-nums">{product.estimatedCost == null ? "—" : money.format(product.estimatedCost)}</td>
                            </tr>)}
                            {visible.length === 0 && <tr><td colSpan={6} className="px-5 py-12 text-center text-slate-500">{loading ? "Cargando…" : "No hay sugerencias para estos filtros."}</td></tr>}
                        </tbody>
                    </table>
                </div>
                <div className="border-t border-slate-100 px-5 py-3 text-sm text-slate-500">Mostrando {visible.length} de {data.products.length} productos sugeridos.</div>
            </section>
            <button onClick={disconnect} className="text-sm text-slate-500 underline hover:text-slate-800">Desconectar token</button>
        </>}
    </div>;
}

function Metric({ icon: Icon, label, value, detail }: { icon: typeof Boxes; label: string; value: string; detail?: string }) {
    return <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center gap-2 text-sm text-slate-500"><Icon size={17} />{label}</div>
        <div className="mt-2 text-2xl font-bold text-slate-900">{value}</div>
        {detail && <div className="mt-1 text-xs text-slate-500">{detail}</div>}
    </div>;
}
