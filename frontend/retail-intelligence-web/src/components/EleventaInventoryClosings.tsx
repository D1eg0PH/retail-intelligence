import { useEffect, useState } from "react";
import { api } from "../api/axios";

type Closing = { closingDate: string; weekly: boolean; monthly: boolean; status: string;
    snapshot: { extractedAtUtc: string; totalCostValue: number | null } | null };
const money = new Intl.NumberFormat("es-MX", { style: "currency", currency: "MXN" });
const capturedAt = new Intl.DateTimeFormat("es-MX", { dateStyle: "short", timeStyle: "short", timeZone: "America/Mexico_City" });
const statuses: Record<string, string> = { complete: "Confirmado", late: "Captura tardía", missing: "Sin captura", "missing-cost": "Costo incompleto" };

export default function EleventaInventoryClosings({ token }: { token: string }) {
    const [closings, setClosings] = useState<Closing[] | null>(null);
    const [error, setError] = useState("");
    const [revision, setRevision] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        api.get<{ closings: Closing[] }>("/integrations/eleventa/v1/inventory/closings", {
            headers: { Authorization: `Bearer ${token}` }, signal: controller.signal
        }).then(response => { if (!controller.signal.aborted) setClosings(response.data.closings); })
            .catch(() => { if (!controller.signal.aborted) setError("No se pudieron consultar los cierres."); });
        return () => controller.abort();
    }, [token, revision]);
    return <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 p-5">
            <div><h2 className="text-lg font-bold text-slate-900">Cierres semanales y mensuales</h2>
                <p className="mt-1 text-sm text-slate-500">Domingos y último día del mes a las 10:35 p. m., hora de Ciudad de México. Últimos 90 días.</p></div>
            <button onClick={() => { setClosings(null); setError(""); setRevision(value => value + 1); }} className="text-sm text-blue-700">Actualizar</button>
        </div>
        {error && <p role="alert" className="p-5 text-red-700">{error}</p>}
        <div className="overflow-x-auto"><table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-slate-500"><tr><th className="px-5 py-3">Fecha de cierre</th><th className="px-5 py-3">Período</th><th className="px-5 py-3">Estado</th><th className="px-5 py-3">Captura real</th><th className="px-5 py-3 text-right">Inventario a costo</th></tr></thead>
            <tbody className="divide-y divide-slate-100">{closings?.map(closing => <tr key={closing.closingDate}>
                <td className="px-5 py-3">{closing.closingDate}</td><td className="px-5 py-3">{[closing.weekly && "Semanal", closing.monthly && "Mensual"].filter(Boolean).join(" y ")}</td>
                <td className="px-5 py-3">{statuses[closing.status] ?? closing.status}</td>
                <td className="px-5 py-3">{closing.snapshot ? capturedAt.format(new Date(closing.snapshot.extractedAtUtc)) : "—"}</td>
                <td className="px-5 py-3 text-right tabular-nums">{closing.snapshot?.totalCostValue == null ? "—" : money.format(closing.snapshot.totalCostValue)}</td>
            </tr>)}
                {!error && !closings?.length && <tr><td colSpan={5} className="px-5 py-8 text-center text-slate-500">{closings ? "No hay cierres vencidos en este período." : "Cargando…"}</td></tr>}
            </tbody></table></div>
        <p className="border-t border-slate-100 px-5 py-3 text-xs text-slate-500">El cierre mensual es el inventario inicial del siguiente mes. Una captura sirve para ambos cierres cuando coinciden. Las fechas sin captura o sin costo histórico se muestran pendientes; no se reconstruyen con inventario de otro día.</p>
    </section>;
}
