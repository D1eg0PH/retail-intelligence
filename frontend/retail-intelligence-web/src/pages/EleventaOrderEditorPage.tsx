import { useEffect, useMemo, useState } from "react";
import { ArrowLeft, Minus, Plus, Search, Trash2 } from "lucide-react";
import { Link } from "react-router-dom";
import { api } from "../api/axios";

type DraftItem = {
    sourceProductId: number;
    code: string;
    description: string;
    departmentName: string | null;
    supplierName: string | null;
    quantity: number;
    cost: number | null;
    suggestedQuantity: number;
    currentInventory: number;
    addedManually: boolean;
};
type CatalogProduct = {
    sourceProductId: number;
    code: string;
    description: string;
    cost: number | null;
    departmentName: string | null;
    supplierName: string | null;
    deletedAt: string | null;
    tracksInventory: boolean;
};
type CatalogResponse = { skip: number; take: number; total: number; products: CatalogProduct[] };

const tokenKey = "retail-intelligence-eleventa-token";
const draftKey = "retail-intelligence-eleventa-order-draft";
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const number = new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 });

function readDraft(): DraftItem[] {
    try {
        const value = JSON.parse(sessionStorage.getItem(draftKey) ?? "[]") as DraftItem[];
        return Array.isArray(value) ? value.filter(item => Number.isFinite(item.sourceProductId) && Number.isFinite(item.quantity)) : [];
    } catch { return []; }
}

export default function EleventaOrderEditorPage() {
    const token = sessionStorage.getItem(tokenKey) ?? "";
    const [items, setItems] = useState<DraftItem[]>(readDraft);
    const [search, setSearch] = useState("");
    const [catalog, setCatalog] = useState<CatalogProduct[]>([]);
    const [catalogTotal, setCatalogTotal] = useState(0);
    const [catalogPage, setCatalogPage] = useState(0);
    const [catalogLoading, setCatalogLoading] = useState(false);
    const [catalogError, setCatalogError] = useState("");
    const [itemError, setItemError] = useState("");

    useEffect(() => { sessionStorage.setItem(draftKey, JSON.stringify(items)); }, [items]);

    useEffect(() => {
        const term = search.trim();
        if (!token || term.length < 2) {
            setCatalog([]); setCatalogTotal(0); setCatalogError(""); setCatalogLoading(false);
            return;
        }
        let cancelled = false;
        const timer = window.setTimeout(async () => {
            setCatalogLoading(true); setCatalogError("");
            try {
                const response = await api.get<CatalogResponse>("/integrations/eleventa/v1/catalog/products", {
                    params: { skip: catalogPage * 50, take: 50, search: term },
                    headers: { Authorization: `Bearer ${token}` }
                });
                if (cancelled) return;
                const available = response.data.products.filter(product => !product.deletedAt && product.tracksInventory);
                setCatalog(current => catalogPage === 0 ? available : [...current, ...available.filter(product => !current.some(existing => existing.sourceProductId === product.sourceProductId))]);
                setCatalogTotal(response.data.total);
            } catch (error: unknown) {
                if (!cancelled) {
                    const requestError = error as { response?: { status?: number }; message?: string };
                    setCatalogError(requestError.response?.status === 401 ? "El token Eleventa no es válido." : `No se pudo buscar el catálogo (${requestError.message ?? "error de red"}).`);
                }
            } finally { if (!cancelled) setCatalogLoading(false); }
        }, 300);
        return () => { cancelled = true; window.clearTimeout(timer); };
    }, [search, catalogPage, token]);

    const totalUnits = useMemo(() => items.reduce((total, item) => total + item.quantity, 0), [items]);
    const knownCost = useMemo(() => items.reduce((total, item) => total + (item.cost == null ? 0 : item.cost * item.quantity), 0), [items]);
    const itemsWithoutCost = items.filter(item => item.cost == null).length;

    function updateQuantity(productId: number, rawValue: string) {
        const quantity = Number(rawValue);
        if (!Number.isFinite(quantity) || quantity <= 0) {
            setItemError("La cantidad debe ser mayor que cero.");
            return;
        }
        setItemError("");
        setItems(current => current.map(item => item.sourceProductId === productId ? { ...item, quantity } : item));
    }

    function addProduct(product: CatalogProduct) {
        if (items.some(item => item.sourceProductId === product.sourceProductId)) return;
        setItems(current => [...current, {
            sourceProductId: product.sourceProductId,
            code: product.code,
            description: product.description,
            departmentName: product.departmentName,
            supplierName: product.supplierName,
            quantity: 1,
            cost: product.cost,
            suggestedQuantity: 0,
            currentInventory: 0,
            addedManually: true
        }]);
    }

    return <div className="space-y-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
                <Link to="/dashboard/eleventa/pedidos" className="inline-flex items-center gap-2 text-sm font-medium text-blue-700 hover:text-blue-900"><ArrowLeft size={16} /> Volver a sugerencias</Link>
                <p className="mt-4 text-sm font-semibold uppercase tracking-wide text-blue-700">Eleventa · borrador local</p>
                <h1 className="mt-1 text-3xl font-bold text-slate-900">Editar pedido</h1>
                <p className="mt-2 text-slate-600">Ajusta cantidades y agrega productos al borrador. Estos cambios no crean ni envían un pedido a Eleventa.</p>
            </div>
        </header>

        {!token && <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-900">Falta conectar el token del agente. <Link className="font-semibold underline" to="/dashboard/eleventa/pedidos">Vuelve a sugerencias para conectarlo.</Link></div>}
        {itemError && <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-rose-800">{itemError}</div>}

        <section className="grid gap-4 sm:grid-cols-3">
            <Metric label="Productos en el borrador" value={number.format(items.length)} />
            <Metric label="Cantidad total" value={number.format(totalUnits)} />
            <Metric label={itemsWithoutCost ? "Costo conocido" : "Costo total estimado"} value={money.format(knownCost)} detail={itemsWithoutCost ? `${itemsWithoutCost} producto(s) sin costo registrado` : "A costo de Eleventa"} />
        </section>

        <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
            <div className="border-b border-slate-100 p-5">
                <h2 className="text-lg font-bold text-slate-900">Productos del pedido</h2>
                <p className="mt-1 text-sm text-slate-500">Cambia la cantidad o quita productos que no vas a comprar.</p>
            </div>
            <div className="overflow-x-auto">
                <table className="w-full min-w-[850px] text-left text-sm">
                    <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr>
                        <th className="px-5 py-3">Producto</th><th className="px-5 py-3">Proveedor</th><th className="px-5 py-3 text-right">Costo unitario</th><th className="px-5 py-3 text-right">Cantidad</th><th className="px-5 py-3 text-right">Subtotal</th><th className="px-5 py-3">Acción</th>
                    </tr></thead>
                    <tbody className="divide-y divide-slate-100">
                        {items.map(item => <tr key={item.sourceProductId}>
                            <td className="px-5 py-3"><span className="block font-mono text-xs text-slate-500">{item.code || "—"}</span><span className="font-medium text-slate-900">{item.description || "Sin descripción"}</span>{item.addedManually && <span className="ml-2 rounded-full bg-violet-100 px-2 py-0.5 text-xs text-violet-700">Agregado</span>}</td>
                            <td className="px-5 py-3 text-slate-600">{item.supplierName || "Sin proveedor"}</td>
                            <td className="px-5 py-3 text-right tabular-nums">{item.cost == null ? "Sin costo" : money.format(item.cost)}</td>
                            <td className="px-5 py-3 text-right"><div className="inline-flex items-center gap-2">
                                <button aria-label={`Restar una unidad a ${item.description}`} onClick={() => updateQuantity(item.sourceProductId, String(Math.max(0.01, Math.round((item.quantity - 1) * 100) / 100)))} className="rounded border border-slate-300 p-1 hover:bg-slate-50"><Minus size={14} /></button>
                                <input aria-label={`Cantidad de ${item.description}`} type="number" min="0.01" step="0.01" value={item.quantity} onChange={event => updateQuantity(item.sourceProductId, event.target.value)} className="w-24 rounded-lg border border-slate-300 px-2 py-1.5 text-right tabular-nums" />
                                <button aria-label={`Sumar una unidad a ${item.description}`} onClick={() => updateQuantity(item.sourceProductId, String(Math.round((item.quantity + 1) * 100) / 100))} className="rounded border border-slate-300 p-1 hover:bg-slate-50"><Plus size={14} /></button>
                            </div></td>
                            <td className="px-5 py-3 text-right font-medium tabular-nums">{item.cost == null ? "—" : money.format(item.cost * item.quantity)}</td>
                            <td className="px-5 py-3"><button onClick={() => setItems(current => current.filter(line => line.sourceProductId !== item.sourceProductId))} className="inline-flex items-center gap-1 rounded-lg px-2 py-1.5 text-rose-700 hover:bg-rose-50"><Trash2 size={15} /> Quitar</button></td>
                        </tr>)}
                        {items.length === 0 && <tr><td colSpan={6} className="px-5 py-10 text-center text-slate-500">El borrador está vacío. Busca un producto abajo para agregarlo.</td></tr>}
                    </tbody>
                </table>
            </div>
        </section>

        <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-lg font-bold text-slate-900">Agregar productos del catálogo</h2>
            <p className="mt-1 text-sm text-slate-500">Busca por código o nombre; solo se muestran productos activos que controlan inventario.</p>
            <label className="mt-4 flex items-center gap-2 rounded-lg border border-slate-300 px-3">
                <Search size={17} className="shrink-0 text-slate-400" />
                <input value={search} onChange={event => { setSearch(event.target.value); setCatalogPage(0); }} placeholder="Escribe al menos 2 caracteres" className="min-w-0 flex-1 py-2.5 text-sm outline-none" />
            </label>
            {catalogError && <p role="alert" className="mt-3 text-sm text-rose-700">{catalogError}</p>}
            {catalogLoading && <p className="mt-3 text-sm text-slate-500">Buscando en el catálogo…</p>}
            {search.trim().length >= 2 && !catalogLoading && catalog.length === 0 && !catalogError && <p className="mt-3 text-sm text-slate-500">No se encontraron productos activos para esa búsqueda.</p>}
            <div className="mt-3 divide-y divide-slate-100">
                {catalog.map(product => {
                    const added = items.some(item => item.sourceProductId === product.sourceProductId);
                    return <div key={product.sourceProductId} className="flex flex-col gap-3 py-3 sm:flex-row sm:items-center sm:justify-between">
                        <div><span className="block font-mono text-xs text-slate-500">{product.code || "—"}</span><span className="font-medium text-slate-900">{product.description || "Sin descripción"}</span><span className="ml-2 text-sm text-slate-500">{product.supplierName || "Sin proveedor"} · {product.cost == null ? "Sin costo" : money.format(product.cost)}</span></div>
                        <button disabled={added} onClick={() => addProduct(product)} className="inline-flex items-center justify-center gap-2 rounded-lg border border-blue-200 px-3 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50 disabled:cursor-not-allowed disabled:opacity-50"><Plus size={15} /> {added ? "Agregado" : "Agregar"}</button>
                    </div>;
                })}
            </div>
            {catalog.length < catalogTotal && catalog.length > 0 && <button disabled={catalogLoading} onClick={() => setCatalogPage(page => page + 1)} className="mt-3 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50">Cargar más resultados</button>}
        </section>
    </div>;
}

function Metric({ label, value, detail }: { label: string; value: string; detail?: string }) {
    return <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="text-sm text-slate-500">{label}</div>
        <div className="mt-2 text-2xl font-bold text-slate-900">{value}</div>
        {detail && <div className="mt-1 text-xs text-slate-500">{detail}</div>}
    </div>;
}