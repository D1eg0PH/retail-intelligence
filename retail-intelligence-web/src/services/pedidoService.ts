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