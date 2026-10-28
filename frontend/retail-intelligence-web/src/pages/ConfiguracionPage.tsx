import { useEffect, useState } from "react";

import type { Configuracion } from "../types/configuracion";

import {
    getConfiguracion,
    updateConfiguracion
} from "../services/configuracionService";

import PageHeader from "../components/ui/PageHeader";
import Card from "../components/ui/Card";
import Input from "../components/ui/Input";
import Button from "../components/ui/Button";
import Loading from "../components/ui/Loading";


export default function ConfiguracionPage() {
    const [configuracion, setConfiguracion] = useState<Configuracion>({
        semanasPromedio: 0,
        diasInventarioObjetivo: 0,
        diasProductoMuerto: 0,
        porcentajeColchon: 0,
        porcentajeUmbralReposicion: 75
    });

    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        cargarConfiguracion();
    }, []);

    async function cargarConfiguracion() {
        try {
            const data = await getConfiguracion();

            setConfiguracion(data);
        }
        finally {
            setLoading(false);
        }
    }

    async function guardarConfiguracion() {
        try {
            setSaving(true);

            await updateConfiguracion(configuracion);

            alert("Configuración actualizada correctamente.");
        }
        finally {
            setSaving(false);
        }
    }

    if (loading) {
        return (
            <Loading message="Cargando configuración..." />
        );
    }

    return (
        <div className="space-y-6">

            <PageHeader
                title="Configuración"
                subtitle="Parámetros generales del algoritmo de pedidos"
            />

            <Card className="max-w-2xl">

                <div className="grid gap-5">

                    <Input
                        label="Semanas promedio"
                        type="number"
                        value={configuracion.semanasPromedio}
                        onChange={(e) =>
                            setConfiguracion({
                                ...configuracion,
                                semanasPromedio: Number(e.target.value)
                            })
                        }
                    />

                    <Input
                        label="Días de inventario objetivo"
                        type="number"
                        value={configuracion.diasInventarioObjetivo}
                        onChange={(e) =>
                            setConfiguracion({
                                ...configuracion,
                                diasInventarioObjetivo: Number(e.target.value)
                            })
                        }
                    />

                    <Input
                        label="Días para producto muerto"
                        type="number"
                        value={configuracion.diasProductoMuerto}
                        onChange={(e) =>
                            setConfiguracion({
                                ...configuracion,
                                diasProductoMuerto: Number(e.target.value)
                            })
                        }
                    />

                    <Input
                        label="Porcentaje de colchón (%)"
                        type="number"
                        value={configuracion.porcentajeColchon}
                        onChange={(e) =>
                            setConfiguracion({
                                ...configuracion,
                                porcentajeColchon: Number(e.target.value)
                            })
                        }
                    />

                    <Input
                        label="Umbral para pedir (% del STOP)"
                        type="number"
                        min={0}
                        max={100}
                        step={0.01}
                        value={configuracion.porcentajeUmbralReposicion}
                        onChange={(e) =>
                            setConfiguracion({
                                ...configuracion,
                                porcentajeUmbralReposicion: Number(e.target.value)
                            })
                        }
                    />

                    <div className="flex justify-end">

                        <Button
                            onClick={guardarConfiguracion}
                            disabled={saving}
                        >
                            {saving
                                ? "Guardando..."
                                : "Guardar cambios"}
                        </Button>

                    </div>

                </div>

            </Card>

        </div>
    );
}
