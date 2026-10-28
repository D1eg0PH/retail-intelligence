using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Entities;
using RetailIntelligence.Api.Integrations.Eleventa;

namespace RetailIntelligence.Api.Data;

public partial class AppDbContext : DbContext
{
    // Constructor que recibe la configuración de la conexión
    // desde Program.cs mediante inyección de dependencias.
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Representa la tabla "departamentos" en PostgreSQL.
    // Entity Framework permitirá hacer consultas como:
    // _context.Departamentos.ToListAsync()
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    
    public DbSet<Producto> Productos => Set<Producto>();
    
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    
    public DbSet<Importacion> Importaciones => Set<Importacion>();
    
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    
    public DbSet<VentaPeriodo> VentasPeriodo => Set<VentaPeriodo>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<PedidoDetalle> PedidoDetalles => Set<PedidoDetalle>();

    public DbSet<EstadoPedido> EstadosPedido => Set<EstadoPedido>();

    public DbSet<ConfiguracionSistema> ConfiguracionSistema => Set<ConfiguracionSistema>();

    // Más adelante agregaremos:
    // public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ejecuta primero la configuración base de EF Core
        base.OnModelCreating(modelBuilder);
        EleventaEntityConfiguration.Configure(modelBuilder);
        EleventaFinanceConfiguration.Configure(modelBuilder);

        // ==========================================
        // CONFIGURACIÓN DE LA ENTIDAD DEPARTAMENTO
        // ==========================================

        // La entidad Departamento se almacenará
        // en la tabla "departamentos"
        modelBuilder.Entity<Departamento>()
            .ToTable("departamentos");

        // Define la llave primaria
        modelBuilder.Entity<Departamento>()
            .HasKey(d => d.IdDepartamento);

        // Mapea la propiedad IdDepartamento
        // a la columna id_departamento
        modelBuilder.Entity<Departamento>()
            .Property(d => d.IdDepartamento)
            .HasColumnName("id_departamento");

        // Mapea Nombre -> nombre
        modelBuilder.Entity<Departamento>()
            .Property(d => d.Nombre)
            .HasColumnName("nombre");

        // Mapea Activo -> activo
        modelBuilder.Entity<Departamento>()
            .Property(d => d.Activo)
            .HasColumnName("activo");

        // Mapea CreatedAt -> created_at
        modelBuilder.Entity<Departamento>()
            .Property(d => d.CreatedAt)
            .HasColumnName("created_at");

        // Mapea UpdatedAt -> updated_at
        modelBuilder.Entity<Departamento>()
            .Property(d => d.UpdatedAt)
            .HasColumnName("updated_at");

        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD PRODUCTO
        // ======================================

        // Indica el nombre de la tabla
        modelBuilder.Entity<Producto>()
            .ToTable("productos");

        // Define explícitamente la llave primaria
        modelBuilder.Entity<Producto>()
            .HasKey(p => p.IdProducto);

        // Mapea IdProducto -> id_producto
        modelBuilder.Entity<Producto>()
            .Property(p => p.IdProducto)
            .HasColumnName("id_producto");

        // Mapea Codigo -> codigo
        modelBuilder.Entity<Producto>()
            .Property(p => p.Codigo)
            .HasColumnName("codigo");

        // Mapea Nombre -> nombre
        modelBuilder.Entity<Producto>()
            .Property(p => p.Nombre)
            .HasColumnName("nombre");

        modelBuilder.Entity<Producto>()
        .Property(p => p.Activo)
        .HasColumnName("activo");

        modelBuilder.Entity<Producto>()
            .Property(p => p.CostoActual)
            .HasColumnName("costo_actual");

        modelBuilder.Entity<Producto>()
            .Property(p => p.PrecioVentaActual)
            .HasColumnName("precio_venta_actual");

        modelBuilder.Entity<Producto>()
            .Property(p => p.StockMinimo)
            .HasColumnName("stock_minimo");

        modelBuilder.Entity<Producto>()
            .Property(p => p.StockMaximo)
            .HasColumnName("stock_maximo");

        modelBuilder.Entity<Producto>()
            .Property(p => p.UnidadMedida)
            .HasColumnName("unidad_medida");

        modelBuilder.Entity<Producto>()
            .Property(p => p.EsEsencial)
            .HasColumnName("es_esencial");

        modelBuilder.Entity<Producto>()
            .Property(p => p.Descontinuado)
            .HasColumnName("descontinuado");

        modelBuilder.Entity<Producto>()
            .Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<Producto>()
            .Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        modelBuilder.Entity<Producto>()
            .Property(p => p.IdProveedor)
            .HasColumnName("id_proveedor");

        modelBuilder.Entity<Producto>()
            .Property(p => p.IdDepartamento)
            .HasColumnName("id_departamento");

        // Relación Producto -> Departamento
        modelBuilder.Entity<Producto>()
            .HasOne(p => p.Departamento)
            .WithMany(d => d.Productos)
            .HasForeignKey(p => p.IdDepartamento);

        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD PROVEEDOR
        // ======================================


        modelBuilder.Entity<Proveedor>()
            .ToTable("proveedores");

        modelBuilder.Entity<Proveedor>()
            .HasKey(p => p.IdProveedor);

        modelBuilder.Entity<Proveedor>()
            .Property(p => p.IdProveedor)
            .HasColumnName("id_proveedor");

        modelBuilder.Entity<Proveedor>()
            .Property(p => p.Nombre)
            .HasColumnName("nombre");

        modelBuilder.Entity<Proveedor>()
            .Property(p => p.Activo)
            .HasColumnName("activo");

        modelBuilder.Entity<Proveedor>()
            .Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<Proveedor>()
            .Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        modelBuilder.Entity<Producto>()
            .HasOne(p => p.Proveedor)
            .WithMany(p => p.Productos)
            .HasForeignKey(p => p.IdProveedor);


        // CONFIGURACION DE LA ENTIDAD IMPORTACION

        modelBuilder.Entity<Importacion>()
            .ToTable("importaciones");

        modelBuilder.Entity<Importacion>()
            .HasKey(i => i.IdImportacion);

        modelBuilder.Entity<Importacion>()
            .Property(i => i.IdImportacion)
            .HasColumnName("id_importacion")
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Importacion>()
            .Property(i => i.TipoArchivo)
            .HasColumnName("tipo_archivo");


        modelBuilder.Entity<Importacion>()
            .Property(i => i.NombreArchivo)
            .HasColumnName("nombre_archivo");

        modelBuilder.Entity<Importacion>()
            .Property(i => i.FechaInicioPeriodo)
            .HasColumnName("fecha_inicio_periodo");


        modelBuilder.Entity<Importacion>()
            .Property(i => i.FechaFinPeriodo)
            .HasColumnName("fecha_fin_periodo");

        modelBuilder.Entity<Importacion>()
            .Property(i => i.HashArchivo)
            .HasColumnName("hash_archivo");

        modelBuilder.Entity<Importacion>()
            .Property(i => i.RegistrosImportados)
            .HasColumnName("registros_importados");

        modelBuilder.Entity<Importacion>()
            .Property(i => i.Observaciones)
            .HasColumnName("observaciones");

        modelBuilder.Entity<Importacion>()
            .Property(i => i.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<Importacion>()
            .Property(i => i.UpdatedAt)
            .HasColumnName("updated_at");

        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD INVENTARIO
        // ======================================

        modelBuilder.Entity<Inventario>()
            .ToTable("inventarios");

        modelBuilder.Entity<Inventario>()
            .HasKey(i => i.IdInventario);

        modelBuilder.Entity<Inventario>()
            .Property(i => i.IdInventario)
            .HasColumnName("id_inventario");

        modelBuilder.Entity<Inventario>()
            .Property(i => i.IdProducto)
            .HasColumnName("id_producto");

        modelBuilder.Entity<Inventario>()
            .Property(i => i.IdImportacion)
            .HasColumnName("id_importacion");

        modelBuilder.Entity<Inventario>()
            .Property(i => i.Existencia)
            .HasColumnName("existencia");

        modelBuilder.Entity<Inventario>()
            .Property(i => i.PrecioCosto)
            .HasColumnName("precio_costo");

        modelBuilder.Entity<Inventario>()
            .Property(i => i.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<Inventario>()
            .Property(i => i.UpdatedAt)
            .HasColumnName("updated_at");

        modelBuilder.Entity<Inventario>()
            .HasOne(i => i.Producto)
            .WithMany()
            .HasForeignKey(i => i.IdProducto);

        modelBuilder.Entity<Inventario>()
            .HasOne(i => i.Importacion)
            .WithMany()
            .HasForeignKey(i => i.IdImportacion);

        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD VENTAPERIODO
        // ======================================

        modelBuilder.Entity<VentaPeriodo>()
            .ToTable("ventasperiodo");

        modelBuilder.Entity<VentaPeriodo>()
            .HasKey(v => v.IdVenta);

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.IdVenta)
            .HasColumnName("id_venta");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.IdProducto)
            .HasColumnName("id_producto");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.IdImportacion)
            .HasColumnName("id_importacion");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.CantidadVendida)
            .HasColumnName("cantidad_vendida");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.PrecioUsado)
            .HasColumnName("precio_usado");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.PrecioCosto)
            .HasColumnName("precio_costo");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.Importe)
            .HasColumnName("importe");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<VentaPeriodo>()
            .Property(v => v.UpdatedAt)
            .HasColumnName("updated_at");


        modelBuilder.Entity<VentaPeriodo>()
            .HasOne(v => v.Producto)
            .WithMany()
            .HasForeignKey(v => v.IdProducto);

        modelBuilder.Entity<VentaPeriodo>()
            .HasOne(v => v.Importacion)
            .WithMany()
            .HasForeignKey(v => v.IdImportacion);


        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD ESTADOPEDIDO
        // ======================================

        modelBuilder.Entity<EstadoPedido>()
            .ToTable("estadopedido");

        modelBuilder.Entity<EstadoPedido>()
            .HasKey(e => e.IdEstado);

        modelBuilder.Entity<EstadoPedido>()
            .Property(e => e.IdEstado)
            .HasColumnName("id_estado");

        modelBuilder.Entity<EstadoPedido>()
            .Property(e => e.Nombre)
            .HasColumnName("nombre");

        modelBuilder.Entity<EstadoPedido>()
            .Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<EstadoPedido>()
            .Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD PEDIDO
        // ======================================

        modelBuilder.Entity<Pedido>()
            .ToTable("pedidos");

        modelBuilder.Entity<Pedido>()
            .HasKey(p => p.IdPedido);

        modelBuilder.Entity<Pedido>()
            .Property(p => p.IdPedido)
            .HasColumnName("id_pedido");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.IdProveedor)
            .HasColumnName("id_proveedor");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.IdEstado)
            .HasColumnName("id_estado");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.FechaPedido)
            .HasColumnName("fecha_pedido");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.FechaEntrega)
            .HasColumnName("fecha_entrega");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.Observaciones)
            .HasColumnName("observaciones");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<Pedido>()
            .Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.Proveedor)
            .WithMany()
            .HasForeignKey(p => p.IdProveedor);

        modelBuilder.Entity<Pedido>()
            .HasOne(p => p.Estado)
            .WithMany(e => e.Pedidos)
            .HasForeignKey(p => p.IdEstado);

        // ======================================
        // CONFIGURACIÓN DE LA ENTIDAD PEDIDODETALLE
        // ======================================

        modelBuilder.Entity<PedidoDetalle>()
            .ToTable("pedidodetalle");

        modelBuilder.Entity<PedidoDetalle>()
            .HasKey(pd => pd.IdDetalle);

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.IdDetalle)
            .HasColumnName("id_detalle");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.IdPedido)
            .HasColumnName("id_pedido");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.IdProducto)
            .HasColumnName("id_producto");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.CantidadSugerida)
            .HasColumnName("cantidad_sugerida");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.CantidadPedida)
            .HasColumnName("cantidad_pedida");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.CostoUnitario)
            .HasColumnName("costo_unitario");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.MotivoAjuste)
            .HasColumnName("motivo_ajuste");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<PedidoDetalle>()
            .Property(pd => pd.UpdatedAt)
            .HasColumnName("updated_at");

        modelBuilder.Entity<PedidoDetalle>()
            .HasOne(pd => pd.Pedido)
            .WithMany(p => p.PedidoDetalles)
            .HasForeignKey(pd => pd.IdPedido);

        modelBuilder.Entity<PedidoDetalle>()
            .HasOne(pd => pd.Producto)
            .WithMany()
            .HasForeignKey(pd => pd.IdProducto);


        // ======================================
        // CONFIGURACIÓN DE CONFIGURACIÓN SISTEMA
        // ======================================

        modelBuilder.Entity<ConfiguracionSistema>()
            .ToTable("configuracionsistema");

        modelBuilder.Entity<ConfiguracionSistema>()
            .HasKey(c => c.IdConfiguracion);

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.IdConfiguracion)
            .HasColumnName("id_configuracion");

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.SemanasPromedio)
            .HasColumnName("semanas_promedio");

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.DiasInventarioObjetivo)
            .HasColumnName("dias_inventario_objetivo");

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.DiasProductoMuerto)
            .HasColumnName("dias_producto_muerto");

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.PorcentajeColchon)
            .HasColumnName("porcentaje_colchon");

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.CreatedAt)
            .HasColumnName("created_at");

        modelBuilder.Entity<ConfiguracionSistema>()
            .Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");



    }
}
