import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { getPedido } from "../services/pedidoService";

export default function PedidoDetallePage() {

    const { id } = useParams();

    const [pedido, setPedido] = useState<any>(null);

    useEffect(() => {
        cargarPedido();
    }, []);

    const cargarPedido = async () => {
        try {

            const data = await getPedido(Number(id));

            setPedido(data);

            const cantidadesIniciales:
            Record<number, number> = {};

            data.detalles.forEach(
                (detalle: any) => {

                    cantidadesIniciales[
                        detalle.idDetalle
                    ] =
                        detalle.cantidadPedida ??
                        detalle.cantidadSugerida;
                }
            );

            setCantidades(
                cantidadesIniciales
            );

        }
        catch (error) {
            console.error(error);
        }
    };
    
    const [cantidades, setCantidades] = useState<
    Record<number, number>
    >({});


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

    const totalCalculado =
    pedido?.detalles.reduce(
        (acc: number, detalle: any) =>
            acc +
            (
                (
                    cantidades[
                        detalle.idDetalle
                    ] ??
                    detalle.cantidadSugerida
                ) *
                detalle.costoUnitario
            ),
        0
    ) ?? 0;

    if (!pedido) {
        return (
            <div className="p-8">
                Cargando pedido...
            </div>
        );
    }

    return (
        <div className="max-w-7xl mx-auto p-8">

            {/* HEADER */}

            <div className="
                bg-white
                rounded-2xl
                shadow-sm
                border
                border-gray-200
                p-8
                mb-8">

                <div className="flex justify-between items-start mb-6">

                    <div>
                        <p className="text-sm text-gray-500">
                            Pedido # {pedido.idPedido}
                        </p>

                        <h1 className="text-4xl font-bold">
                            {pedido.proveedor}
                        </h1>

                    </div>

                    <span className={`px-5 py-2 rounded-fullfont-semibold
                    ${getEstadoColor(
                        pedido.estado
                    )}
                    `}>
                        {pedido.estado}
                    </span>

                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">

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
                            {pedido.detalles.length}
                        </p>
                    </div>

                    <div className="bg-gray-50 rounded-xl p-4">
                        <p className="text-sm text-gray-500">
                            Monto estimado
                        </p>

                        <p className="text-2xl font-bold text-green-700">
                            ${pedido.montoTotal.toFixed(2)}
                        </p>
                    </div>

                </div>

            </div>

            {/* TABLA */}

            <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden">

                <table className="w-full">

                    <thead className="bg-gray-100">

                        <th className="text-left p-4">Producto</th>
                    
                        <th className="text-center p-4">Existencia</th>

                        <th className="text-center p-4">Vendido</th>
              
                        <th className="text-center p-4">Sugerido</th>

                        <th className="text-center p-4">Pedido</th>

                        <th className="text-right p-4">Costo</th>

                        <th className="text-right p-4">Subtotal</th>

                
                    </thead>

                    <tbody>

                        {
                            pedido.detalles.map(
                                (detalle: any) => (

                                    <tr
                                        key={
                                            detalle.idDetalle
                                        }
                                        className="border-t hover:bg-gray-50">

                                        <td className="p-4">
                                            {
                                                detalle.producto
                                            }
                                        </td>
                                    <td
                                        className={`text-center p-4 font-semibold
                                            ${
                                                detalle.existencia === 0
                                                    ? "text-red-600"
                                                    : detalle.existencia <= 5
                                                    ? "text-yellow-600"
                                                    : "text-green-600"
                                            }
                                        `}>
                                            {detalle.existencia}
                                        </td>

                                    <td className="text-center p-4">
                                        {detalle.vendidoPeriodo            
                                        }
                                    </td>

                                    <td className="text-center p-4 font-semibold">
                                    {detalle.cantidadSugerida}
                                    </td>

                                <td className="text-center p-4">
                                    <input
                                        type="number"
                                        min="0"
                                        value={
                                            cantidades[detalle.idDetalle] ??
                                            detalle.cantidadPedida ??
                                            detalle.cantidadSugerida
                                        }
                                        onChange={(e) =>
                                            setCantidades({
                                                ...cantidades,
                                                [detalle.idDetalle]:
                                                    Number(e.target.value)
                                            })
                                        }
                                        className="
                                            w-20
                                            border
                                            rounded-lg
                                            p-2
                                            text-center
                                        "
                                    />
                                </td>


                                    <td className="text-right p-4">
                                            $
                                            {
                                                detalle.costoUnitario
                                                    .toFixed(2)
                                            }
                                     </td>

                                    <td className="text-right p-4 font-semibold">
                                          $
                                            {(
                                                (
                                                    cantidades[detalle.idDetalle] ??
                                                    detalle.cantidadSugerida
                                                ) * detalle.costoUnitario
                                            ).toFixed(2)}
                                        </td>

                                    </tr>
                                )
                            )
                        }

                    </tbody>

                </table>

            </div>

            {/* PRODUCTOS DISPONIBLES */}
            {
                pedido.productosDisponibles?.length > 0 && (

                    <div className="
                        mt-8
                        bg-white
                        rounded-2xl
                        shadow-sm
                        border
                        border-gray-200
                        overflow-hidden
                    ">

                        <div className="p-6 border-b">
                            <h2 className="text-2xl font-bold">
                                Otros productos del proveedor
                            </h2>

                            <p className="text-gray-500 mt-1">
                                Productos no sugeridos automáticamente
                            </p>
                        </div>

                        <table className="w-full">

                            <thead className="bg-gray-100">

                                <tr>
                                    <th className="text-left p-4">
                                        Producto
                                    </th>

                                    <th className="text-left p-4">
                                        Existencia
                                    </th>
                                    
                                    <th className="text-left p-4">
                                        Vendido
                                    </th>

                                

                                    <th className="text-right p-4">
                                        Costo
                                    </th>

                                    <th className="text-center p-4">
                                        Acción
                                    </th>
                                </tr>

                            </thead>

                            <tbody>
                                {
                                    pedido.productosDisponibles.map(
                                        (producto: any) => (
                                            <tr
                                                key={producto.idProducto}
                                                className="border-t hover:bg-gray-50">
                                                <td className="p-4">
                                                    {producto.nombre}
                                                </td>
                                                <td>{producto.existencia}</td>
                                                <td>{producto.vendidoPeriodo}</td>

                                                <td className="text-right p-4">
                                                    $
                                                    {producto.costoActual.toFixed(2)}
                                                </td>

                                                <td className="text-center p-4">
                                                    <button className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700">
                                                            Agregar
                                                    </button>
                                                </td>
                                            </tr>
                                        )
                                    )
                                }

                            </tbody>

                        </table>

                    </div>
                )
            }

            {/* TOTAL */}

            <div className="
                mt-8
                flex
                justify-end
            ">

                <div className="
                    bg-green-50
                    border
                    border-green-200
                    rounded-2xl
                    px-8
                    py-6
                ">

                    <p className="
                        text-gray-600
                    ">
                        Total estimado
                    </p>

                    <p className="
                        text-4xl
                        font-bold
                        text-green-700
                    ">
                        $
                            {totalCalculado.toFixed(2)}
                    </p>

                </div>

            </div>

        </div>
    );
}