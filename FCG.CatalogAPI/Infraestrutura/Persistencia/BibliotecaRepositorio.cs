using FCG.CatalogAPI.Domain.Entidades;
using FCG.CatalogAPI.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FCG.CatalogAPI.Infraestrutura.Persistencia;

public class BibliotecaRepositorio : IBibliotecaRepositorio
{
    private readonly CatalogDbContext _context;

    public BibliotecaRepositorio(CatalogDbContext context) => _context = context;

    public async Task<IEnumerable<Biblioteca>> ListarPorUsuarioAsync(Guid usuarioId)
        => await _context.Biblioteca
            .AsNoTracking()
            .Where(b => b.UsuarioId == usuarioId)
            .ToListAsync();

    public async Task<bool> JogoJaAdquiridoAsync(Guid usuarioId, Guid jogoId)
        => await _context.Biblioteca
            .AnyAsync(b => b.UsuarioId == usuarioId && b.JogoId == jogoId);

    // Evita processar o mesmo pagamento duas vezes
    // caso o evento seja entregue mais de uma vez pelo broker
    public async Task<bool> OrderJaProcessadaAsync(Guid orderId)
        => await _context.Biblioteca
            .AnyAsync(b => b.OrderId == orderId);

    public async Task AdicionarAsync(Biblioteca biblioteca)
        => await _context.Biblioteca.AddAsync(biblioteca);

    public async Task<int> SalvarAsync()
        => await _context.SaveChangesAsync();
}