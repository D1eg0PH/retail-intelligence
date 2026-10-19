import { api } from "../api/axios";
import type { Configuracion } from "../types/configuracion";

export const getConfiguracion = async (): Promise<Configuracion> => {
    const response = await api.get("/configuracion");

    return response.data;
};

export const updateConfiguracion = async (
    configuracion: Configuracion
): Promise<void> => {
    await api.put("/configuracion", configuracion);
};