import { api } from "../api/axios";
import type { DepuracionProductosResponse } from "../types/productoDepuracion";

export const getProductos = async () => {
    const response = await api.get("/productos");

    return response.data;
};

export const getProductosDepuracion = async (): Promise<DepuracionProductosResponse> => {
    const response = await api.get<DepuracionProductosResponse>("/productos/depuracion");

    return response.data;
};

export const generarCodigoBarras =
    async (
        idProducto: number
    ) => {

    const response =
        await api.post(
            `/productos/${idProducto}/barcode`
        );

    return response.data;
};
