import { useEffect, useState } from "react";
import { api } from "../api/axios";

type Capture = { snapshotId: string; extractedAtUtc: string; totalUnits: number; knownCostValue: number; rowsWithoutCost: number; totalCostValue: number | null };
type History = { total: number; snapshots: Capture[] };
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const number = new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 });
const date = new Intl.DateTimeFormat("es-MX", { dateStyle: "medium", timeStyle: "short", timeZone: "America/Mexico_City" });

export default function EleventaInventoryHistory({ token }: { token: string }) {
    const [skip, setSkip] = useState(0);
    const [revision, setRevision] = useState(0);
    const [data, setData] = useState<History | null>(null);
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(true);
    const take = 30;
    function reload(nextSkip: number) {
        setLoading(true); setError(""); setData(null); setSkip(nextSkip);
        setRevision(value => value + 1);
    }
    useEffect(() => {
        const controller = new AbortController();
        api.get<History>("/integrations/eleventa/v1/inventory/history", {
            headers: { Authorization: `Bearer ${token}` }, params: { skip, take }, signal: controller.signal
        }).then(response => { if (!controller.signal.aborted) setData(response.data); })
            .catch(() => { if (!controller.signal.aborted) setError("No se pudo consultar el historial. Intenta actualizar."); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [token, skip, revision]);
    return <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 p-5">
            <div><h2 className="text-lg font-bold text-slate-900">Historial de inventario a costo</h2>
                <p className="mt-1 text-sm text-slate-500">Capturas completas con el costo de cada fecha. Horario de Ciudad de México.</p></div>
            <button disabled={loading} onClick={() => reload(skip)} className="text-sm text-blue-700 disabled:opacity-50">Actualizar</button>
        </div>
        {error && <p role="alert" className="p-5 text-red-700">{error}</p>}
        <div className="overflow-x-auto"><table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-slate-500"><tr><th className="px-5 py-3">Fecha</th><th className="px-5 py-3 text-right">Unidades</th><th className="px-5 py-3 text-right">Valor a costo</th><th className="px-5 py-3">Cobertura de costos</th></tr></thead>
            <tbody className="divide-y divide-slate-100">{data?.snapshots.map(capture => <tr key={capture.snapshotId}>
                <td className="px-5 py-3">{date.format(new Date(capture.extractedAtUtc))}</td>
                <td className="px-5 py-3 text-right tabular-nums">{number.format(capture.totalUnits)}</td>
                <td className="px-5 py-3 text-right tabular-nums">{capture.totalCostValue == null ? "Sin valor completo" : money.format(capture.totalCostValue)}</td>
                <td className="px-5 py-3 text-slate-600">{capture.rowsWithoutCost === 0 ? "Completa" : `${capture.rowsWithoutCost} registros sin costo válido · valor parcial ${money.format(capture.knownCostValue)}`}</td>
            </tr>)}
                {!error && !data?.snapshots.length && <tr><td colSpan={4} className="px-5 py-8 text-center text-slate-500">{loading ? "Cargando…" : "Todavía no hay capturas completas."}</td></tr>}
            </tbody></table></div>
        <div className="flex items-center justify-between gap-3 border-t border-slate-100 p-5 text-sm">
            <button disabled={loading || skip === 0} onClick={() => reload(Math.max(0, skip - take))} className="text-blue-700 disabled:opacity-40">Anteriores</button>
            <span className="text-slate-500">{data ? `${data.total} capturas` : ""}</span>
            <button disabled={loading || !data || skip + take >= data.total} onClick={() => reload(skip + take)} className="text-blue-700 disabled:opacity-40">Siguientes</button>
        </div>
        <p className="border-t border-slate-100 px-5 py-3 text-xs text-slate-500">Las capturas antiguas sin costo no se recalculan con precios actuales. Este historial no sustituye el conteo físico.</p>
    </section>;
}
