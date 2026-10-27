import axios from "axios";
import { useCallback, useEffect, useMemo, useState } from "react";
import { RefreshCw, Search } from "lucide-react";
import Card from "../components/ui/Card";
import Loading from "../components/ui/Loading";
import PageHeader from "../components/ui/PageHeader";
import { getProductosDepuracion } from "../services/productoService";
import type {
    DepuracionProductosResponse,
    ProductoDepuracion
} from "../types/productoDepuracion";

const clasificaciones = [
    "Todas",
    "Posible producto muerto",
    "Con movimiento",
    "Información insuficiente",
    "Sin datos de ventas"
];

function mensajeError(error: unknown) {
    if (axios.isAxiosError(error)) {
        const detalle = error.response?.data?.message ?? error.response?.data?.title;
        if (typeof detalle === "string") return detalle;
    }

    return error instanceof Error
        ? error.message
        : "No se pudo cargar la depuración de productos.";
}

function formatoNumero(valor: number) {
    return new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 }).format(valor);
}

function formatoMoneda(valor: number) {
    return new Intl.NumberFormat("es-MX", {
        style: "currency",
        currency: "MXN",
        maximumFractionDigits: 2
    }).format(valor);
}

function formatoFecha(fecha: string) {
    const [anio, mes, dia] = fecha.slice(0, 10).split("-").map(Number);
    return new Intl.DateTimeFormat("es-MX").format(new Date(anio, mes - 1, dia));
}

function formatoFechaHora(fecha: string) {
    return new Intl.DateTimeFormat("es-MX", {
        dateStyle: "short",
        timeStyle: "short"
    }).format(new Date(fecha));
}

export default function DepuracionProductosPage() {
    const [productos, setProductos] = useState<ProductoDepuracion[]>([]);
    const [resumen, setResumen] = useState<DepuracionProductosResponse | null>(null);
    const [cargando, setCargando] = useState(true);
    const [error, setError] = useState("");
    const [busqueda, setBusqueda] = useState("");
    const [clasificacion, setClasificacion] = useState("Todas");
    const [proveedor, setProveedor] = useState("");

    const cargarProductos = useCallback(async () => {
        setCargando(true);
        setError("");

        try {
            const respuesta = await getProductosDepuracion();
            setResumen(respuesta);
            setProductos(respuesta.productos);
        } catch (err) {
            setError(mensajeError(err));
        } finally {
            setCargando(false);
        }
    }, []);

    useEffect(() => {
        let activo = true;

        async function cargarInicial() {
            try {
                const datos = await getProductosDepuracion();
                if (activo) {
                    setResumen(datos);
                    setProductos(datos.productos);
                }
            } catch (err) {
                if (activo) setError(mensajeError(err));
            } finally {
                if (activo) setCargando(false);
            }
        }

        void cargarInicial();
        return () => { activo = false; };
    }, []);

    const productosFiltrados = useMemo(() => {
        const termino = busqueda.trim().toLocaleLowerCase("es-MX");

        return productos.filter((producto) => {
            const coincideProveedor = !proveedor || producto.proveedor === proveedor;
            const coincideClasificacion = clasificacion === "Todas"
                || producto.clasificacion === clasificacion;
            const coincideBusqueda = !termino || [
                producto.codigo,
                producto.nombre,
                producto.proveedor
            ].some((campo) => campo.toLocaleLowerCase("es-MX").includes(termino));

            return coincideProveedor && coincideClasificacion && coincideBusqueda;
        });
    }, [busqueda, clasificacion, productos, proveedor]);

    const productosProveedor = proveedor
        ? productos.filter((producto) => producto.proveedor === proveedor)
        : productos;
    const candidatos = productosProveedor.filter((producto) =>
        producto.clasificacion === "Posible producto muerto");
    const valorCandidato = candidatos.reduce(
        (total, producto) => total + producto.valorInventario,
        0
    );
    const proveedores = [...new Set(productos.map((producto) => producto.proveedor))]
        .filter(Boolean)
        .sort((a, b) => a.localeCompare(b, "es-MX"));

    if (cargando && productos.length === 0) {
        return <Loading message="Analizando productos..." />;
    }

    return (
        <div className="space-y-6">
            <PageHeader
                title="Depuración de productos"
                subtitle="Identifica productos sin movimiento y revisa el inventario asociado antes de tomar una decisión."
            />

            {error && (
                <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                    <p>{error}</p>
                    <button onClick={() => void cargarProductos()} className="mt-2 font-semibold underline">
                        Reintentar
                    </button>
                </div>
            )}

            <div className="grid gap-4 md:grid-cols-3">
                <Card>
                    <p className="text-sm text-slate-500">Productos analizados</p>
                    <p className="mt-2 text-2xl font-bold text-slate-900">{productosProveedor.length}</p>
                </Card>
                <Card>
                    <p className="text-sm text-slate-500">Posibles productos muertos</p>
                    <p className="mt-2 text-2xl font-bold text-amber-700">{candidatos.length}</p>
                </Card>
                <Card>
                    <p className="text-sm text-slate-500">Valor de inventario candidato</p>
                    <p className="mt-2 text-2xl font-bold text-slate-900">{formatoMoneda(valorCandidato)}</p>
                </Card>
            </div>

            {resumen && (
                <Card>
                    <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                        <div>
                            <h2 className="font-semibold text-slate-900">Período del análisis</h2>
                            <p className="mt-1 text-sm text-slate-600">
                                {formatoFecha(resumen.fechaInicioAnalisis)} al {formatoFecha(resumen.fechaFinAnalisis)}
                                <span className="ml-2 text-slate-500">
                                    ({resumen.diasPeriodo} días efectivos; mínimo configurado: {resumen.diasObjetivo})
                                </span>
                            </p>
                            <p className="mt-1 text-xs text-slate-500">
                                {resumen.periodosVentas.length > 0
                                    ? "La ventana termina en la última fecha de ventas importada."
                                    : "No se encontró una fecha de corte válida; se usa hoy."}
                            </p>
                            <p className={`mt-2 text-sm font-medium ${resumen.coberturaCompleta ? "text-green-700" : "text-amber-700"}`}>
                                Cobertura de ventas: {resumen.diasCubiertos} de {resumen.diasPeriodo} días efectivos
                                {resumen.coberturaCompleta ? " · completa" : " · incompleta"}
                            </p>
                        </div>
                        <div className="min-w-64 rounded-lg bg-slate-50 p-3 text-sm">
                            <p className="font-semibold text-slate-800">Inventario usado</p>
                            {resumen.fuenteInventario ? (
                                <>
                                    <p className="mt-1 text-slate-600">{resumen.fuenteInventario.nombreArchivo}</p>
                                    <p className="text-slate-500">
                                        Fecha: {formatoFecha(resumen.fuenteInventario.fechaInventario)} · importado {formatoFechaHora(resumen.fuenteInventario.importadoEl)}
                                    </p>
                                </>
                            ) : (
                                <p className="mt-1 text-amber-700">No hay una importación de inventario disponible.</p>
                            )}
                        </div>
                    </div>

                    <div className="mt-5">
                        <h3 className="text-sm font-semibold text-slate-800">Períodos de ventas considerados</h3>
                        {resumen.periodosVentas.length > 0 ? (
                            <div className="mt-2 overflow-x-auto">
                                <table className="w-full min-w-[640px] text-left text-sm">
                                    <thead className="border-b border-slate-200 text-xs uppercase text-slate-500">
                                        <tr>
                                            <th className="px-3 py-2">Archivo</th>
                                            <th className="px-3 py-2">Fechas del archivo</th>
                                            <th className="px-3 py-2 text-right">Registros</th>
                                            <th className="px-3 py-2">Importado</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-slate-100">
                                        {resumen.periodosVentas.map((periodo) => (
                                            <tr key={`${periodo.nombreArchivo}-${periodo.fechaInicio}-${periodo.fechaFin}`}>
                                                <td className="px-3 py-2 text-slate-700">{periodo.nombreArchivo}</td>
                                                <td className="px-3 py-2 text-slate-700">{formatoFecha(periodo.fechaInicio)} al {formatoFecha(periodo.fechaFin)}</td>
                                                <td className="px-3 py-2 text-right tabular-nums">{formatoNumero(periodo.registrosImportados)}</td>
                                                <td className="px-3 py-2 text-slate-500">{formatoFechaHora(periodo.importadoEl)}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        ) : (
                            <p className="mt-2 text-sm text-amber-700">No hay importaciones de ventas que cubran el período analizado.</p>
                        )}
                    </div>
                </Card>
            )}

            <Card>
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div className="relative w-full lg:max-w-md">
                        <Search size={18} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                        <input
                            aria-label="Buscar producto"
                            value={busqueda}
                            onChange={(event) => setBusqueda(event.target.value)}
                            placeholder="Buscar por código, nombre o proveedor"
                            className="w-full rounded-lg border border-slate-300 py-2 pl-10 pr-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                        />
                    </div>
                    <div className="flex flex-col gap-3 sm:flex-row">
                        <select
                            aria-label="Filtrar por proveedor"
                            value={proveedor}
                            onChange={(event) => setProveedor(event.target.value)}
                            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
                        >
                            <option value="">Todos los proveedores</option>
                            {proveedores.map((opcion) => (
                                <option key={opcion} value={opcion}>{opcion}</option>
                            ))}
                        </select>
                        <select
                            aria-label="Filtrar por clasificación"
                            value={clasificacion}
                            onChange={(event) => setClasificacion(event.target.value)}
                            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
                        >
                            {clasificaciones.map((opcion) => <option key={opcion}>{opcion}</option>)}
                        </select>
                        <button
                            onClick={() => void cargarProductos()}
                            disabled={cargando}
                            className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                        >
                            <RefreshCw size={16} className={cargando ? "animate-spin" : ""} />
                            Actualizar
                        </button>
                    </div>
                </div>

                <div className="mt-5 overflow-x-auto">
                    <table className="w-full min-w-[900px] text-left text-sm">
                        <thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500">
                            <tr>
                                <th className="px-3 py-3">Producto</th>
                                <th className="px-3 py-3">Proveedor</th>
                                <th className="px-3 py-3 text-right">Existencia</th>
                                <th className="px-3 py-3 text-right">Vendido ({productos[0]?.diasPeriodo ?? 0} días)</th>
                                <th className="px-3 py-3 text-right">Valor inventario</th>
                                <th className="px-3 py-3">Clasificación</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                            {productosFiltrados.map((producto) => (
                                <tr key={producto.idProducto} className="hover:bg-slate-50">
                                    <td className="px-3 py-3">
                                        <p className="font-medium text-slate-900">{producto.nombre}</p>
                                        <p className="text-xs text-slate-500">{producto.codigo}</p>
                                    </td>
                                    <td className="px-3 py-3 text-slate-600">{producto.proveedor}</td>
                                    <td className="px-3 py-3 text-right tabular-nums">{formatoNumero(producto.existencia)}</td>
                                    <td className="px-3 py-3 text-right tabular-nums">{formatoNumero(producto.vendidoPeriodo)}</td>
                                    <td className="px-3 py-3 text-right tabular-nums">{formatoMoneda(producto.valorInventario)}</td>
                                    <td className="px-3 py-3">
                                        <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${producto.clasificacion === "Posible producto muerto" ? "bg-amber-100 text-amber-800" : producto.clasificacion === "Con movimiento" ? "bg-green-100 text-green-800" : "bg-slate-100 text-slate-700"}`}>
                                            {producto.clasificacion}
                                        </span>
                                    </td>
                                </tr>
                            ))}
                            {productosFiltrados.length === 0 && (
                                <tr>
                                    <td colSpan={6} className="px-3 py-10 text-center text-slate-500">
                                        {productos.length === 0
                                            ? "No hay productos para analizar."
                                            : "No hay productos que coincidan con los filtros."}
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
                <p className="mt-3 text-xs text-slate-500">
                    Mostrando {productosFiltrados.length} de {productos.length} productos. La clasificación es una referencia para revisión y no modifica ni elimina productos.
                </p>
            </Card>
        </div>
    );
}
