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
    diasPeriodo?: number,
    fechaInicio?: string,
    fechaFin?: string
) => {

    const formData = new FormData();

    formData.append("file", file);

    if (diasPeriodo !== undefined) {
        formData.append("diasPeriodo", diasPeriodo.toString());
    } else {
        if (fechaInicio) {
            formData.append("fechaInicio", fechaInicio);
        }

        if (fechaFin) {
            formData.append("fechaFin", fechaFin);
        }
    }

    const response = await api.post(
        "/import/sales",
        formData,
        {
            headers: {
                "Content-Type": "multipart/form-data"
            }
        }
    );

    return response.data;
};

