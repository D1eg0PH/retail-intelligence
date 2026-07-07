using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Entities;

namespace RetailIntelligence.Api.Data;

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options)
		: base(options)
	{
	}

	public DbSet<Departamento> Departamentos => Set<Departamento>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Departamento>()
			.ToTable("departamentos");

		modelBuilder.Entity<Departamento>()
			.HasKey(d => d.IdDepartamento);

		modelBuilder.Entity<Departamento>()
			.Property(d => d.IdDepartamento)
			.HasColumnName("id_departamento");

		modelBuilder.Entity<Departamento>()
			.Property(d => d.Nombre)
			.HasColumnName("nombre");

		modelBuilder.Entity<Departamento>()
			.Property(d => d.Activo)
			.HasColumnName("activo");

		modelBuilder.Entity<Departamento>()
			.Property(d => d.CreatedAt)
			.HasColumnName("created_at");

		modelBuilder.Entity<Departamento>()
			.Property(d => d.UpdatedAt)
			.HasColumnName("updated_at");
	}
}