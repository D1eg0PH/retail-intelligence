/*
==================================================
Retail Intelligence
Archivo: 01_create_tables.sql
Versión: 1.0
Autor: Diego Armando Pérez Huerta
Descripción:
Creación de la estructura principal de la base de datos.
==================================================
*/

-- ==========================================
-- CONFIGURACIÓN DEL SISTEMA
-- ==========================================

BEGIN;


CREATE TABLE ConfiguracionSistema (
    id_configuracion SMALLINT PRIMARY KEY CHECK (id_configuracion = 1),

    semanas_promedio SMALLINT NOT NULL DEFAULT 4,
    dias_inventario_objetivo SMALLINT NOT NULL DEFAULT 14,
    dias_producto_muerto SMALLINT NOT NULL DEFAULT 60,
    porcentaje_colchon NUMERIC(10,2) NOT NULL DEFAULT 0.00 CHECK (porcentaje_colchon >= 0),
    porcentaje_umbral_reposicion NUMERIC(5,2) NOT NULL DEFAULT 75.00
        CHECK (porcentaje_umbral_reposicion BETWEEN 0 AND 100),

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- DEPARTAMENTOS
-- ==========================================

CREATE TABLE Departamentos (
    id_departamento SERIAL PRIMARY KEY,

    nombre VARCHAR(100) NOT NULL UNIQUE,

    activo BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- PROVEEDORES
-- ==========================================

CREATE TABLE Proveedores (
    id_proveedor SERIAL PRIMARY KEY,

    nombre VARCHAR(150) NOT NULL,

    contacto VARCHAR(100),
    telefono VARCHAR(30),
    correo VARCHAR(100),

    dia_pedido SMALLINT CHECK (dia_pedido BETWEEN 1 AND 7),
    dia_entrega SMALLINT CHECK (dia_entrega BETWEEN 1 AND 7),

    activo BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- ESTADO DE PEDIDOS
-- ==========================================

CREATE TABLE EstadoPedido (
    id_estado SERIAL PRIMARY KEY,

    nombre VARCHAR(30) NOT NULL UNIQUE,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- ==========================================
-- PRODUCTOS
-- ==========================================

CREATE TABLE Productos (
    id_producto SERIAL PRIMARY KEY,

    codigo VARCHAR(30) NOT NULL UNIQUE,
    codigo_barras VARCHAR(30) UNIQUE,

    nombre VARCHAR(250) NOT NULL,

    id_departamento INT NOT NULL,
    id_proveedor INT NOT NULL,

    costo_actual NUMERIC(10,2) NOT NULL CHECK (costo_actual >= 0),
    precio_venta_actual NUMERIC(10,2) NOT NULL CHECK (precio_venta_actual >= 0),

    stock_minimo NUMERIC(10,2) NOT NULL DEFAULT 0 CHECK (stock_minimo >= 0),
    stock_maximo NUMERIC(10,2) NOT NULL DEFAULT 0 CHECK (stock_maximo >= 0),

    unidad_medida VARCHAR(30) NOT NULL DEFAULT 'Pieza',

    es_esencial BOOLEAN NOT NULL DEFAULT FALSE,
    descontinuado BOOLEAN NOT NULL DEFAULT FALSE,

    activo BOOLEAN NOT NULL DEFAULT TRUE,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_stock
    CHECK (stock_maximo >= stock_minimo),

    CONSTRAINT fk_productos_departamento
        FOREIGN KEY (id_departamento)
        REFERENCES Departamentos(id_departamento),

    CONSTRAINT fk_productos_proveedor
        FOREIGN KEY (id_proveedor)
        REFERENCES Proveedores(id_proveedor)
);


-- SEG BLOQUE
-- ==========================================
-- IMPORTACIONES
-- ==========================================

CREATE TABLE Importaciones (
    id_importacion SERIAL PRIMARY KEY,

    tipo_archivo VARCHAR(30) NOT NULL,
    nombre_archivo VARCHAR(255) NOT NULL,

    fecha_inicio_periodo DATE NOT NULL,
    fecha_fin_periodo DATE NOT NULL,

    CONSTRAINT uq_importacion_periodo
    UNIQUE (
        tipo_archivo,
        fecha_inicio_periodo,
        fecha_fin_periodo
    ),

    CONSTRAINT chk_periodo
    CHECK (fecha_inicio_periodo <= fecha_fin_periodo),

    hash_archivo VARCHAR(64) NOT NULL UNIQUE,

    registros_importados INTEGER NOT NULL CHECK (registros_importados >= 0),

    observaciones TEXT,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- VENTAS POR PERIODO
-- ==========================================

CREATE TABLE VentasPeriodo (
    id_venta SERIAL PRIMARY KEY,

    id_producto INT NOT NULL,
    id_importacion INT NOT NULL,

    cantidad_vendida NUMERIC(10,2) NOT NULL CHECK (cantidad_vendida >= 0),

    precio_usado NUMERIC(10,2) NOT NULL CHECK (precio_usado >= 0),
    precio_costo NUMERIC(10,2) NOT NULL CHECK (precio_costo >= 0),

    importe NUMERIC(10,2) NOT NULL CHECK (importe >= 0),

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_ventas_producto
        FOREIGN KEY (id_producto)
        REFERENCES Productos(id_producto),

    CONSTRAINT fk_ventas_importacion
        FOREIGN KEY (id_importacion)
        REFERENCES Importaciones(id_importacion)
);

-- ==========================================
-- INVENTARIOS
-- ==========================================

CREATE TABLE Inventarios (
    id_inventario SERIAL PRIMARY KEY,

    id_producto INT NOT NULL,
    id_importacion INT NOT NULL,

    existencia NUMERIC(10,2) NOT NULL CHECK (existencia >= 0),

    precio_costo NUMERIC(10,2) NOT NULL CHECK (precio_costo >= 0),

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_inventarios_producto
        FOREIGN KEY (id_producto)
        REFERENCES Productos(id_producto),

    CONSTRAINT fk_inventarios_importacion
        FOREIGN KEY (id_importacion)
        REFERENCES Importaciones(id_importacion)
);


-- TERCER BLOQUE

-- ==========================================
-- PEDIDOS
-- ==========================================

CREATE TABLE Pedidos (
    id_pedido SERIAL PRIMARY KEY,

    id_proveedor INT NOT NULL,
    id_estado INT NOT NULL,

    fecha_pedido DATE NOT NULL,
    fecha_entrega DATE,

    observaciones TEXT,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_fechas_pedido
    CHECK (
        fecha_entrega IS NULL
        OR fecha_entrega >= fecha_pedido
    ),

    CONSTRAINT fk_pedidos_proveedor
        FOREIGN KEY (id_proveedor)
        REFERENCES Proveedores(id_proveedor),

    CONSTRAINT fk_pedidos_estado
        FOREIGN KEY (id_estado)
        REFERENCES EstadoPedido(id_estado)
);


-- ==========================================
-- DETALLE DE PEDIDOS
-- ==========================================

CREATE TABLE PedidoDetalle (
    id_detalle SERIAL PRIMARY KEY,

    id_pedido INT NOT NULL,
    id_producto INT NOT NULL,

    cantidad_sugerida NUMERIC(10,2) NOT NULL CHECK (cantidad_sugerida >= 0),
    cantidad_pedida NUMERIC(10,2) CHECK (cantidad_pedida >= 0),

    costo_unitario NUMERIC(10,2) NOT NULL CHECK (costo_unitario >= 0),

    motivo_ajuste TEXT,

    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_detalle_pedido
        FOREIGN KEY (id_pedido)
        REFERENCES Pedidos(id_pedido),

    CONSTRAINT fk_detalle_producto
        FOREIGN KEY (id_producto)
        REFERENCES Productos(id_producto)
);


COMMIT;
