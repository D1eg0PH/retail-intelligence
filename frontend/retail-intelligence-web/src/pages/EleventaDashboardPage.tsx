import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Activity, Boxes, DollarSign, Package, RefreshCw, Search } from "lucide-react";
import { api } from "../api/axios";
import EleventaInventoryHistory from "../components/EleventaInventoryHistory";
import EleventaInventoryClosings from "../components/EleventaInventoryClosings";

type DashboardProduct = {
    sourceProductId: number;
    code: string;
    description: string;
    quantity: number;
    cost: number | null;
    salePrice: number | null;
    lastSoldAt: string | null;
};

type DashboardData = {
    inventoryAtUtc: string | null;
    productCount: number;
    totalUnits: number;
    inventoryCostValue: number;
    salesLast30Days: number;
    salesValueLast30Days: number;
    totalProducts: number;
    products: DashboardProduct[];
};

const tokenStorageKey = "retail-intelligence-eleventa-token";
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const number = new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 });

export default function EleventaDashboardPage() {
    const [token, setToken] = useState(() => sessionStorage.getItem(tokenStorageKey) ?? "");
    const [tokenInput, setTokenInput] = useState("");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [data, setData] = useState<DashboardData | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const loadDashboard = useCallback(async (apiToken: string, query: string) => {
        setLoading(true);
        setError("");
        try {
            const response = await api.get<DashboardData>("/integrations/eleventa/v1/dashboard", {
                headers: { Authorization: `Bearer ${apiToken}` },
                params: { take: 100, search: query || undefined }
            });
            setData(response.data);
        } catch (err: unknown) {
            const requestError = err as { response?: { status?: number }; message?: string };
            const status = requestError.response?.status;
            setError(status === 401
                ? "El token no es válido o ya fue rotado. Pega el token actual del agente Eleventa."
                : status
                    ? `La API respondió con error HTTP ${status}. Intenta de nuevo más tarde.`
                    : `No se recibió respuesta de la API (${requestError.message ?? "error de red o CORS"}).`);
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        if (token) void loadDashboard(token, search);
    }, [token, search, loadDashboard]);

    function connect(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const trimmed = tokenInput.trim();
        if (!trimmed) return;
        sessionStorage.setItem(tokenStorageKey, trimmed);
        setData(null);
        setToken(trimmed);
    }

    function searchProducts(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setSearch(searchInput.trim());
    }

    return (
        <div className="space-y-6">
            <header>
                <p className="text-sm font-semibold uppercase tracking-wide text-blue-700">Datos sincronizados</p>
                <h1 className="mt-1 text-3xl font-bold text-slate-900">Dashboard Eleventa</h1>
                <p className="mt-2 text-slate-600">Inventario y ventas de la base de Eleventa, separados de las importaciones del dashboard anterior.</p>
            </header>

            {!token && (
                <form onSubmit={connect} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                    <label htmlFor="eleventa-token" className="mb-2 block font-semibold text-slate-800">Token de integración</label>
                    <p className="mb-3 text-sm text-slate-600">Pega el token configurado para el agente Eleventa. Se conserva solo mientras esta página está abierta.</p>
                    <div className="flex flex-col gap-3 sm:flex-row">
                        <input id="eleventa-token" type="password" autoComplete="off" value={tokenInput}
                            onChange={event => setTokenInput(event.target.value)} placeholder="Token de API"
                            className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100" />
                        <button type="submit" className="rounded-lg bg-blue-600 px-5 py-2 font-semibold text-white hover:bg-blue-700">Conectar</button>
                    </div>
                </form>
            )}

            {error && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-800">{error}</div>}

            {data && (
                <>
                    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                        <Metric icon={Package} label="Productos en inventario" value={number.format(data.productCount)} />
                        <Metric icon={Boxes} label="Unidades disponibles" value={number.format(data.totalUnits)} />
                        <Metric icon={DollarSign} label="Valor a costo" value={money.format(data.inventoryCostValue)} />
                        <Metric icon={Activity} label="Ventas últimos 30 días" value={money.format(data.salesValueLast30Days)} detail={`${number.format(data.salesLast30Days)} tickets`} />
                    </div>

                    <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                        <div className="flex flex-col gap-3 border-b border-slate-100 p-5 sm:flex-row sm:items-center sm:justify-between">
                            <div>
                                <h2 className="text-lg font-bold text-slate-900">Inventario de Eleventa</h2>
                                <p className="mt-1 text-sm text-slate-500">
                                    {data.inventoryAtUtc ? `Última captura: ${new Date(data.inventoryAtUtc).toLocaleString("es-MX")}` : "Todavía no hay una captura completa de inventario."}
                                    {` · ${data.totalProducts} productos`}
                                </p>
                            </div>
                            <div className="flex gap-2">
                                <form onSubmit={searchProducts} className="flex">
                                    <input value={searchInput} onChange={event => setSearchInput(event.target.value)}
                                        placeholder="Código o descripción" aria-label="Buscar producto"
                                        className="w-48 rounded-l-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500" />
                                    <button aria-label="Buscar" className="rounded-r-lg border border-l-0 border-slate-300 px-3 text-slate-600 hover:bg-slate-50"><Search size={18} /></button>
                                </form>
                                <button onClick={() => void loadDashboard(token, search)} disabled={loading}
                                    className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                                    <RefreshCw size={16} className={loading ? "animate-spin" : ""} /> Actualizar
                                </button>
                            </div>
                        </div>
                        <div className="overflow-x-auto">
                            <table className="w-full min-w-[760px] text-left text-sm">
                                <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                                    <tr><th className="px-5 py-3">Código</th><th className="px-5 py-3">Producto</th><th className="px-5 py-3 text-right">Existencia</th><th className="px-5 py-3 text-right">Costo</th><th className="px-5 py-3 text-right">Precio</th><th className="px-5 py-3">Última venta registrada</th></tr>
                                </thead>
                                <tbody className="divide-y divide-slate-100">
                                    {data.products.map(product => (
                                        <tr key={product.sourceProductId} className="hover:bg-slate-50">
                                            <td className="px-5 py-3 font-mono text-slate-600">{product.code || "—"}</td>
                                            <td className="px-5 py-3 font-medium text-slate-900">{product.description || "Sin descripción"}</td>
                                            <td className="px-5 py-3 text-right tabular-nums">{number.format(product.quantity)}</td>
                                            <td className="px-5 py-3 text-right tabular-nums">{product.cost == null ? "—" : money.format(product.cost)}</td>
                                            <td className="px-5 py-3 text-right tabular-nums">{product.salePrice == null ? "—" : money.format(product.salePrice)}</td>
                                            <td className="px-5 py-3 text-slate-600">{product.lastSoldAt ? new Date(product.lastSoldAt).toLocaleDateString("es-MX") : "Sin dato"}</td>
                                        </tr>
                                    ))}
                                    {data.products.length === 0 && <tr><td colSpan={6} className="px-5 py-12 text-center text-slate-500">{loading ? "Cargando…" : "No hay productos que coincidan con la búsqueda."}</td></tr>}
                                </tbody>
                            </table>
                        </div>
                        <p className="border-t border-slate-100 px-5 py-3 text-xs text-slate-500">El inventario corresponde a la última captura completa recibida. La actividad de ventas resume los últimos 30 días sincronizados.</p>
                    </section>

                    <EleventaInventoryHistory key={token} token={token} />
                    <EleventaInventoryClosings key={`closings-${token}`} token={token} />
                    <button onClick={() => { sessionStorage.removeItem(tokenStorageKey); setToken(""); setTokenInput(""); setData(null); }} className="text-sm text-slate-500 underline hover:text-slate-800">Desconectar token</button>
                </>
            )}
        </div>
    );
}

function Metric({ icon: Icon, label, value, detail }: { icon: typeof Package; label: string; value: string; detail?: string }) {
    return (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between text-slate-500"><span className="text-sm">{label}</span><Icon size={18} /></div>
            <p className="mt-3 text-2xl font-bold tabular-nums text-slate-900">{value}</p>
            {detail && <p className="mt-1 text-xs text-slate-500">{detail}</p>}
        </div>
    );
}
