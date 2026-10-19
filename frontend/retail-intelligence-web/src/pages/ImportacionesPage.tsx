import { useState } from "react";
import axios from "axios";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
    importInventory,
    importSales
} from "../services/importService";
import {
    generarPedidos
} from "../services/pedidoService";

function obtenerMensajeError(error: unknown, respaldo: string) {
    if (axios.isAxiosError(error)) {
        const data: unknown = error.response?.data;

        if (typeof data === "string" && data.trim()) {
            return data;
        }

        if (data && typeof data === "object") {
            const response = data as Record<string, unknown>;
            if (typeof response.mensaje === "string") {
                return response.mensaje;
            }
            if (typeof response.title === "string") {
                return response.title;
            }
        }
    }

    return error instanceof Error && error.message
        ? error.message
        : respaldo;
}

export default function ImportacionesPage() {
    const navigate = useNavigate();

    const [inventoryFile, setInventoryFile] =
        useState<File | null>(null);

    const [salesFile, setSalesFile] =
        useState<File | null>(null);


    const [fechaInicio, setFechaInicio] = useState("");
    const [fechaFin, setFechaFin] = useState("");
    const [generandoPedidos, setGenerandoPedidos] = useState(false);
    
    const handleImportInventory = async () => {

        if (!inventoryFile) {
            alert("Selecciona un archivo primero.");
            return;
        }

        try {

            await importInventory(inventoryFile);

            alert(
                "Inventario importado correctamente."
            );

            setInventoryFile(null);
        }
        catch (error: unknown) {
            console.error(error);
            alert(obtenerMensajeError(
                error,
                "No se pudo importar el inventario."
            ));
        }
    };

    const handleImportSales = async () => {
        if (!salesFile) {
            alert("Selecciona un archivo de ventas.");
            return;
        }

        if (!fechaInicio || !fechaFin) {
            alert("Selecciona la fecha de inicio y la fecha de fin.");
            return;
        }

        if (fechaInicio > fechaFin) {
            alert("La fecha de inicio no puede ser posterior a la fecha de fin.");
            return;
        }

        try {
            await importSales(
                salesFile,
                undefined,
                fechaInicio,
                fechaFin
            );

            alert("Ventas importadas correctamente.");

            setSalesFile(null);
        } catch (error: unknown) {
            console.error(error);
            alert(obtenerMensajeError(
                error,
                "No se pudieron importar las ventas."
            ));
        }
    };



    const handleGenerarPedidos = async () => {
        const confirmado = window.confirm(
            "Generar pedidos reemplazará todos los pedidos existentes. ¿Deseas continuar?"
        );

        if (!confirmado) {
            return;
        }

        setGenerandoPedidos(true);
        try {
            await generarPedidos();
            toast.success("Pedidos generados correctamente.");
            navigate("/pedidos");
        }
        catch (error: unknown) {
            console.error(error);
            toast.error(obtenerMensajeError(
                error,
                "No se pudieron generar los pedidos."
            ));
        }
        finally {
            setGenerandoPedidos(false);
        }
    };

    return (
        <div className="max-w-7xl mx-auto p-8">

            <h1 className="text-4xl font-bold mb-2">
                Importaciones
            </h1>

            <p className="text-gray-500 mb-8">
                Carga el inventario actual y las ventas
                del periodo para generar automáticamente
                los pedidos sugeridos.
            </p>

            <div className="
                grid
                grid-cols-1
                lg:grid-cols-2
                gap-6
                mb-8
            ">

                {/* INVENTARIO */}
                <div className="
                    bg-white
                    rounded-2xl
                    shadow-md
                    border
                    border-gray-200
                    p-6
                ">

                    <h2 className="
                        text-2xl
                        font-semibold
                        mb-4
                    ">
                        Inventario
                    </h2>

                    <p className="
                        text-gray-500
                        mb-4
                    ">
                        Selecciona el archivo de
                        inventario actual.
                    </p>

                    <input
                        type="file"
                        className="
                            w-full
                            mb-4
                            border
                            rounded-lg
                            p-2
                        "
                        onChange={(e) =>
                            setInventoryFile(
                                e.target.files?.[0] ??
                                null
                            )
                        }
                    />

                    {
                        inventoryFile && (
                            <div className="
                                bg-gray-50
                                rounded-lg
                                p-3
                                mb-4
                                border
                            ">
                                <p className="
                                    text-sm
                                    text-gray-600
                                ">
                                    Archivo seleccionado:
                                </p>

                                <p className="
                                    font-medium
                                    text-gray-800
                                ">
                                    {inventoryFile.name}
                                </p>
                            </div>
                        )
                    }

                    <button
                        onClick={
                            handleImportInventory
                        }
                        className="
                            w-full
                            bg-blue-600
                            hover:bg-blue-700
                            text-white
                            py-3
                            rounded-xl
                            font-semibold
                            transition
                        "
                    >
                        Importar Inventario
                    </button>

                </div>

                {/* VENTAS */}
                <div className="bg-white rounded-2xl shadow-md border border-gray-200 p-6">

                    <h2 className="text-2xl font-semibold mb-4">
                        Ventas
                    </h2>

                    <p className="text-gray-500 mb-4">
                        Selecciona el archivo y el
                        periodo de ventas.
                    </p>

                    <input type="file"className="w-full mb-4 border rounded-lg p-2"
                        onChange={(e) =>
                            setSalesFile(
                                e.target.files?.[0] ??
                                null
                            )
                        }
                    />

                  {
                    salesFile && (
                        <div className="bg-gray-50 rounded-lg p-3 mb-4 border">
                            <p className="text-sm text-gray-600">
                                Archivo seleccionado:
                            </p>

                            <p className="font-medium text-gray-800">
                                {salesFile.name}
                            </p>
                        </div>
                    )
                }

 
                <div className="mb-4">
                    <label
                        htmlFor="fechaInicio"
                        className="block text-sm text-gray-500 mb-2"
                    >
                        Fecha de inicio
                    </label>

                    <input
                        id="fechaInicio"
                        type="date"
                        value={fechaInicio}
                        onChange={(e) => setFechaInicio(e.target.value)}
                        className="border p-3 rounded-lg w-full"
                    />
                </div>

                <div className="mb-6">
                    <label
                        htmlFor="fechaFin"
                        className="block text-sm text-gray-500 mb-2"
                    >
                        Fecha de fin
                    </label>

                    <input
                        id="fechaFin"
                        type="date"
                        value={fechaFin}
                        onChange={(e) => setFechaFin(e.target.value)}
                        min={fechaInicio || undefined}
                        className="border p-3 rounded-lg w-full"
                    />
                </div>



                <button
                    onClick={handleImportSales}
                    className="
                        w-full
                        bg-green-600
                        hover:bg-green-700
                        text-white
                        py-3
                        rounded-xl
                        font-semibold
                        transition
                    "
                >
                    Importar Ventas
                </button>
                </div>

            </div>

            {/* GENERAR PEDIDOS */}

            <div className="
                bg-white
                rounded-2xl
                shadow-md
                border
                border-gray-200
                p-8
            ">

                <h2 className="
                    text-2xl
                    font-semibold
                    mb-2
                ">
                    Generación de Pedidos
                </h2>

                <p className="
                    text-gray-500
                    mb-6
                ">
                    Utiliza el inventario y las
                    ventas importadas para calcular
                    automáticamente los pedidos
                    sugeridos para cada proveedor.
                </p>

                <button
                    onClick={
                        handleGenerarPedidos
                    }
                    disabled={generandoPedidos}
                    aria-busy={generandoPedidos}
                    className="
                        w-full
                        bg-purple-600
                        hover:bg-purple-700
                        text-white
                        py-4
                        rounded-xl
                        font-semibold
                        text-lg
                        transition
                        disabled:cursor-not-allowed
                        disabled:opacity-60
                    "
                >
                    {generandoPedidos ? "Generando pedidos..." : "Generar Pedidos"}
                </button>

            </div>

        </div>
    );
}
