BEGIN;

ALTER TABLE public.configuracionsistema ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.departamentos ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.proveedores ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.estadopedido ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.productos ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.importaciones ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ventasperiodo ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.inventarios ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pedidos ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pedidodetalle ENABLE ROW LEVEL SECURITY;

ALTER TABLE public.eleventa_sources ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_sync_batches ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_products ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_inventory_snapshots ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_inventory_rows ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_sales ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_sale_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_returns ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.eleventa_return_lines ENABLE ROW LEVEL SECURITY;

INSERT INTO public.configuracionsistema (
    id_configuracion,
    semanas_promedio,
    dias_inventario_objetivo,
    dias_producto_muerto,
    porcentaje_colchon
)
VALUES (1, 4, 14, 60, 0.00)
ON CONFLICT (id_configuracion) DO NOTHING;

INSERT INTO public.estadopedido (nombre)
VALUES ('Generado'), ('Revisado'), ('Enviado'), ('Recibido'), ('Cancelado')
ON CONFLICT (nombre) DO NOTHING;

COMMIT;