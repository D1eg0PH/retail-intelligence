export interface ProductoDepuracion {
    idProducto: number;
    codigo: string;
    nombre: string;
    proveedor: string;
    existencia: number;
    costoActual: number;
    valorInventario: number;
    vendidoPeriodo: number;
    diasPeriodo: number;
    clasificacion: string;
}

export interface PeriodoVentasDepuracion {
    nombreArchivo: string;
    fechaInicio: string;
    fechaFin: string;
    registrosImportados: number;
    importadoEl: string;
}

export interface FuenteInventarioDepuracion {
    nombreArchivo: string;
    fechaInventario: string;
    registrosImportados: number;
    importadoEl: string;
}

export interface DepuracionProductosResponse {
    fechaInicioAnalisis: string;
    fechaFinAnalisis: string;
    diasPeriodo: number;
    diasObjetivo: number;
    diasCubiertos: number;
    coberturaCompleta: boolean;
    fuenteInventario: FuenteInventarioDepuracion | null;
    periodosVentas: PeriodoVentasDepuracion[];
    productos: ProductoDepuracion[];
}
