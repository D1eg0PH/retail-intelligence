/*
==================================================
Retail Intelligence
Archivo: 02_seed_data.sql
Versión: 1.0
Autor: Diego Armando Pérez Huerta
Descripción:
Datos iniciales del sistema.
==================================================
*/

BEGIN;

-- ==========================================
-- CONFIGURACIÓN DEL SISTEMA
-- ==========================================

INSERT INTO ConfiguracionSistema (
    id_configuracion,
    semanas_promedio,
    dias_inventario_objetivo,
    dias_producto_muerto
)
VALUES (
    1,
    4,
    14,
    60
);

-- ==========================================
-- ESTADOS DE PEDIDO
-- ==========================================

INSERT INTO EstadoPedido (nombre)
VALUES
('Generado'),
('Revisado'),
('Enviado'),
('Recibido'),
('Cancelado');

-- ==========================================
-- DEPARTAMENTOS
-- ==========================================

INSERT INTO Departamentos (nombre)
VALUES
('- Sin Departamento -'),
('Abarrotera El Gallo'),
('Abarrotera Hidalgo'),
('Aceite patrona'),
('Acsapack'),
('Alcohol'),
('Alpura'),
('Barcel'),
('Bimbo'),
('Boing'),
('Bonafont'),
('Botanas Homi'),
('Chedraui'),
('CHIDAS'),
('Cigarro Link'),
('Cigarro Marlboro'),
('CocaCola'),
('Corona'),
('Deposito Juquilita'),
('DistribuidoraCoyotes'),
('Dulces palanqueta'),
('Farmacia'),
('Ferreteria'),
('Gamesa'),
('Huevo'),
('Jarceria'),
('La Corona Mayoreo'),
('la purisima'),
('Lala'),
('las autenticas'),
('mapetos'),
('Marinela'),
('Merca Menudeo'),
('Merceria Leon'),
('MezcalOax'),
('Papeleria'),
('Pepsi'),
('Peñafiel'),
('Quesos Gabriel'),
('Quesos La Soledad'),
('Quesos Miahuatlan'),
('Ricolino'),
('Sabormex'),
('Sabritas'),
('Ta barato'),
('tia rosa'),
('tostadas don beto'),
('Vinos y licores');

COMMIT;