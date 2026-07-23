import { api } from "../api/axios";

export const generarPedidos = async () => {
    const response = await api.post("/pedidos/generar");
    return response.data;
};

export const getPedidos = async () => {
    const response = await api.get("/pedidos");
    return response.data;
};

export const getPedido = async (id: number) => {
    const response = await api.get(`/pedidos/${id}`);
    return response.data;
};

export const agregarProducto = async (
    idPedido: number,
    idProducto: number
) => {
    await api.post(
        `/pedidos/${idPedido}/productos/${idProducto}`
    );
};

export const quitarProductos = async (
    detalles: number[]
) => {

    await api.post(
        "/pedidos/quitar",
        detalles
    );
};

export const guardarCantidades = async (
    idPedido: number,
    cantidades: {
        idDetalle: number;
        cantidadPedida: number;
    }[]
) => {
    await api.put(
        `/pedidos/${idPedido}/cantidades`,
        cantidades
    );
};

export const exportarPedidoExcel = async (
    idPedido: number
) => {

    const response = await api.get(
        `/pedidos/${idPedido}/excel`,
        {
            responseType: "blob"
        }
    );

    return response.data;
};