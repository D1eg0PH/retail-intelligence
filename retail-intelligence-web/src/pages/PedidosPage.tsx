import { useEffect, useState } from "react";
import { getPedidos } from "../services/pedidoService";
import { Link } from "react-router-dom";

export default function PedidosPage() {

    const [pedidos, setPedidos] =
        useState<any[]>([]);

    useEffect(() => {
        cargarPedidos();
    }, []);

    const cargarPedidos = async () => {
        try {
            const data = await getPedidos();
            setPedidos(data);
        }
        catch (error) {
            console.error(error);
        }
    };

    const getEstadoColor = (estado: string) => {

        switch (estado) {
            case "Generado":
                return "bg-yellow-100 text-yellow-800";

            case "Solicitado":
                return "bg-blue-100 text-blue-800";

            case "Recibido":
                return "bg-green-100 text-green-800";

            case "Cancelado":
                return "bg-red-100 text-red-800";

            default:
                return "bg-gray-100 text-gray-800";
        }
    };

    return (
        <div className="max-w-7xl mx-auto p-8">

            <div className="mb-8">

                <h1 className="text-4xl font-bold">
                    Pedidos
                </h1>

                <p className="text-gray-500 mt-2">
                    Pedidos sugeridos agrupados
                    automáticamente por proveedor.
                </p>

            </div>

            <div className="grid gap-6">

                {
                    pedidos.map((pedido) => (

                        <div
                            key={pedido.idPedido}
                            className="
                                bg-white
                                border
                                border-gray-200
                                rounded-2xl
                                shadow-sm
                                p-6
                                hover:shadow-md
                                transition
                            "
                        >

                            <div className="
                                flex
                                justify-between
                                items-start
                                mb-4
                            ">

                                <div>

                                    <p className="
                                        text-sm
                                        text-gray-500
                                    ">
                                        Pedido #
                                        {pedido.idPedido}
                                    </p>

                                    <h2 className="
                                        text-2xl
                                        font-bold
                                    ">
                                        {pedido.proveedor}
                                    </h2>

                                </div>

                                <span
                                    className={`
                                        px-4
                                        py-2
                                        rounded-full
                                        text-sm
                                        font-semibold
                                        ${getEstadoColor(
                                            pedido.estado
                                        )}
                                    `}
                                >
                                    {pedido.estado}
                                </span>

                            </div>

                            <div className="
                                grid
                                grid-cols-1
                                md:grid-cols-3
                                gap-4
                                mb-6
                            ">

                                <div className="
                                    bg-gray-50
                                    rounded-xl
                                    p-4
                                ">
                                    <p className="
                                        text-sm
                                        text-gray-500
                                    ">
                                        Productos
                                    </p>

                                    <p className="
                                        text-2xl
                                        font-bold
                                    ">
                                        {pedido.productos}
                                    </p>
                                </div>

                                <div className="
                                    bg-gray-50
                                    rounded-xl
                                    p-4
                                ">
                                    <p className="
                                        text-sm
                                        text-gray-500
                                    ">
                                        Monto estimado
                                    </p>

                                    <p className="
                                        text-2xl
                                        font-bold
                                        text-green-700
                                    ">
                                        $
                                        {pedido.montoEstimado
                                            .toFixed(2)}
                                    </p>
                                </div>

                                <div className="
                                    bg-gray-50
                                    rounded-xl
                                    p-4
                                ">
                                    <p className="
                                        text-sm
                                        text-gray-500
                                    ">
                                        Fecha
                                    </p>

                                    <p className="
                                        text-lg
                                        font-semibold
                                    ">
                                        {pedido.fechaPedido}
                                    </p>
                                </div>

                            </div>

                            <Link
                                to={`/pedidos/${pedido.idPedido}`}
                                className="
                                    inline-flex
                                    items-center
                                    justify-center
                                    bg-blue-600
                                    hover:bg-blue-700
                                    text-white
                                    px-5
                                    py-3
                                    rounded-xl
                                    font-semibold
                                    transition
                                "
                            >
                                Ver detalle
                            </Link>

                        </div>

                    ))
                }

            </div>

        </div>
    );
}