export interface Producto {
    idProducto: number;
    codigo: string;
    nombre: string;
    costoActual: number;
    precioVentaActual: number;

    proveedor: string;
    departamento: string;
}