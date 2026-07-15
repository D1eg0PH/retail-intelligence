import { api } from "../api/axios.ts";

export const importInventory = async (file: File) => {
    const formData = new FormData();

    formData.append("file", file);

    const response = await api.post(
        "/import/inventory",
        formData,
        {
            headers: {
                "Content-Type": "multipart/form-data"
            }
        }
    );

    return response.data;
};


export const importSales = async (
    file: File,
    fechaInicio: string,
    fechaFin: string
) => {

    const formData = new FormData();

    formData.append("file", file);

    const response = await api.post(
    `/import/sales?fechaInicioPeriodo=${fechaInicio}&fechaFinPeriodo=${fechaFin}`,
    formData,
        {
            headers: {
                "Content-Type": "multipart/form-data"
            }
        }
    );

    return response.data;
};

