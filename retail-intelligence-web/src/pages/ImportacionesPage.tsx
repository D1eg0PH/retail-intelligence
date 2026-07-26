import { useState } from "react";
import {
    importInventory,
    importSales
} from "../services/importService";
import {
    generarPedidos
} from "../services/pedidoService";

export default function ImportacionesPage() {

    const [inventoryFile, setInventoryFile] =
        useState<File | null>(null);

    const [salesFile, setSalesFile] =
        useState<File | null>(null);

    const [fechaInicio, setFechaInicio] =
        useState("");

    const [fechaFin, setFechaFin] =
        useState("");

    const [diasPeriodo, setDiasPeriodo] = useState(30);

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
        catch (error: any) {

            console.error(error);

            if (error.response) {
                alert(
                    `Error ${error.response.status}: ${
                        JSON.stringify(
                            error.response.data
                        )
                    }`
                );
            }
            else {
                alert(error.message);
            }
        }
    };

    const handleImportSales = async () => {

        if (!salesFile) {
            alert(
                "Selecciona un archivo de ventas."
            );
            return;
        }


        try {

           await importSales(
                salesFile,
                diasPeriodo
            );

            alert(
                "Ventas importadas correctamente."
            );

            setSalesFile(null);
        }
        catch (error: any) {

            console.error(error);

            if (error.response) {
                alert(
                    JSON.stringify(
                        error.response.data
                    )
                );
            }
            else {
                alert(error.message);
            }
        }
    };

    const handleGenerarPedidos = async () => {

        try {

            await generarPedidos();

            alert(
                "Pedidos generados correctamente."
            );
        }
        catch (error: any) {

            console.error(error);

            if (error.response) {
                alert(
                    JSON.stringify(
                        error.response.data
                    )
                );
            }
            else {
                alert(error.message);
            }
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

                <p className="text-sm text-gray-500 mb-2">
                    Días del periodo de ventas
                </p>

                <input
                    type="number"
                    min={1}
                    value={diasPeriodo}
                    onChange={(e) =>
                        setDiasPeriodo(Number(e.target.value))
                    }
                    className="
                        border
                        p-3
                        rounded-lg
                        mb-6
                        w-full
                    "
                />

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
                    "
                >
                    Generar Pedidos
                </button>

            </div>

        </div>
    );
}