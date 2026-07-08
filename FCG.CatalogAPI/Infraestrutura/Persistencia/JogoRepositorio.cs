using FCG.CatalogAPI.Domain.Entidades;
using FCG.CatalogAPI.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FCG.CatalogAPI.Infraestrutura.Persistencia;

public class JogoRepositorio : IJogoRepositorio
{
    private readonly CatalogDbContext _context;

    public JogoRepositorio(CatalogDbContext context) => _context = context;

    public async Task<Jogo?> BuscarPorIdAsync(Guid id)
        => await _context.Jogos.FindAsync(id);

    public async Task<Jogo?> BuscarPorIdComPromocoesAsync(Guid id)
        => await _context.Jogos
            .Include(j => j.Promocoes)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<IEnumerable<Jogo>> ListarTodosComPromocoesAsync()
        => await _context.Jogos
            .Include(j => j.Promocoes)
            .AsNoTracking()
            .ToListAsync();

    public async Task<bool> TituloExisteAsync(string titulo)
        => await _context.Jogos
            .AnyAsync(j => j.Titulo.ToLower() == titulo.ToLower());

    public async Task AdicionarAsync(Jogo jogo)
        => await _context.Jogos.AddAsync(jogo);

    public void Atualizar(Jogo jogo) => _context.Jogos.Update(jogo);
    public void Remover(Jogo jogo)   => _context.Jogos.Remove(jogo);

    public async Task<int> SalvarAsync()
        => await _context.SaveChangesAsync();

}