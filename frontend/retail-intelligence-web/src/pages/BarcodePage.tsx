import { useEffect, useState } from "react";
import BarcodeLabel from "../components/BarcodeLabel";
import html2canvas from "html2canvas";
import { getProductos } from "../services/productoService";
import type { Producto } from "../types/producto";


export default function BarcodePage() {
    const [productos, setProductos] = useState<Producto[]>([]);
    const [busqueda, setBusqueda] = useState("");


    const [productoSeleccionado,
    setProductoSeleccionado]
    = useState<Producto | null>(null);


    useEffect(() => {
        cargarProductos();
    }, []);

const cargarProductos = async () => {
    try {
        const data = await getProductos();

        console.log("Productos recibidos:", data);

        setProductos(data);
    } catch (error) {
        console.error(error);
    }
};

    const descargarPNG = async () => {
    const elemento =
        document.getElementById(
            "barcode-label"
        );

    if (!elemento) {
        alert(
            "No se encontró la etiqueta."
        );
        return;
    }

    const canvas =
        await html2canvas(
            elemento,
            {
                scale: 6,
                backgroundColor: "#ffffff"
            }
        );

    const link =
        document.createElement("a");

    link.download =
        `${productoSeleccionado?.nombre}.png`;

    link.href =
        canvas.toDataURL(
            "image/png"
        );

    document.body.appendChild(link);

    link.click();

    document.body.removeChild(link);
};

    const productosFiltrados = productos.filter(
        (p) =>
            p.nombre
                .toLowerCase()
                .includes(busqueda.toLowerCase()) ||
            p.codigo
                .toString()
                .includes(busqueda)
    );

    return (
        <div className="p-8">
            <h1 className="text-3xl font-bold mb-8">
                Generador de códigos de barras
            </h1>

            <input
                type="text"
                placeholder="Buscar por nombre o código..."
                value={busqueda}
                onChange={(e) =>
                    setBusqueda(e.target.value)
                }
                className="
                    w-full
                    border
                    rounded-xl
                    px-4
                    py-3
                    mb-6
                "
            />

            <div className="overflow-x-auto">
                <table className="w-full bg-white rounded-xl shadow">
                    <thead>
                        <tr className="border-b">
                            <th className="p-4 text-left">
                                Código
                            </th>

                            <th className="p-4 text-left">
                                Producto
                            </th>

                            <th className="p-4 text-left">
                                Acción
                            </th>
                        </tr>
                    </thead>

                    <tbody>
                        {productosFiltrados.map(
                            (producto) => (
                                <tr
                                    key={
                                        producto.idProducto
                                    }
                                    className="
                                        border-b
                                        hover:bg-gray-50
                                    "
                                >
                                    <td className="p-4">
                                        {producto.codigo}
                                    </td>

                                    <td className="p-4">
                                        {producto.nombre}
                                    </td>

                                    <td className="p-4">
                                <button
                                    onClick={() => {
                                        setProductoSeleccionado(producto);
                                    }}
                                    className="
                                        bg-green-600
                                        text-white
                                        px-4
                                        py-2
                                        rounded-lg
                                    "
                                >
                                    Ver etiqueta
                                </button>
                            </td>
                                                    
                                </tr>
                            )
                        )
                        }
                        
                    </tbody>

                    

                
                    
                </table>

                {
                productoSeleccionado && (
                    <div
                        className="
                            mt-10
                            bg-white
                            rounded-xl
                            shadow
                            p-6
                        "
                    >
                        <h2
                            className="
                                text-xl
                                font-bold
                                mb-6
                            "
                        >
                            Vista previa
                        </h2>

                        <div className="flex flex-col items-center gap-6">
                            <BarcodeLabel nombre={productoSeleccionado.nombre}codigoBarras={productoSeleccionado.codigo}/>
                            <button onClick={descargarPNG} className=" bg-green-600 text-white px-6 py-3 rounded-lg">
                                Descargar PNG
                            </button>
                        </div>
                    </div>
                )
            }
            </div>
        </div>
    );
}