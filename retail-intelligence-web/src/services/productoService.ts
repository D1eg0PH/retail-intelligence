import { api } from "../api/axios";

export const getProductos = async () => {
    const response = await api.get("/productos");

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