using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Dtos;
using RetailIntelligence.Api.Entities;

namespace RetailIntelligence.Api.Services;

public class ConfiguracionService : IConfiguracionService
{
    private readonly AppDbContext _context;

    public ConfiguracionService(AppDbContext context)
    {
        _context = context;
    }


    public async Task UpdateAsync(ConfiguracionDto configuracion)
    {
        var configuracionActual = await _context.ConfiguracionSistema
            .SingleAsync();

        configuracionActual.SemanasPromedio = configuracion.SemanasPromedio;
        configuracionActual.DiasInventarioObjetivo = configuracion.DiasInventarioObjetivo;
        configuracionActual.DiasProductoMuerto = configuracion.DiasProductoMuerto;
        configuracionActual.PorcentajeColchon = configuracion.PorcentajeColchon;

        configuracionActual.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<ConfiguracionDto> GetAsync()
    {
        var configuracion = await _context.ConfiguracionSistema
            .SingleAsync();

        return new ConfiguracionDto
        {
            SemanasPromedio = configuracion.SemanasPromedio,
            DiasInventarioObjetivo = configuracion.DiasInventarioObjetivo,
            DiasProductoMuerto = configuracion.DiasProductoMuerto,
            PorcentajeColchon = configuracion.PorcentajeColchon
        };
    }
}