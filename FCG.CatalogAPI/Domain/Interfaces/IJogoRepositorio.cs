using FCG.CatalogAPI.Domain.Entidades;

namespace FCG.CatalogAPI.Domain.Interfaces;

public interface IJogoRepositorio
{
    Task<Jogo?>             BuscarPorIdAsync(Guid id);
    Task<Jogo?>             BuscarPorIdComPromocoesAsync(Guid id);
    Task<IEnumerable<Jogo>> ListarTodosComPromocoesAsync();
    Task<bool>              TituloExisteAsync(string titulo);
    Task                    AdicionarAsync(Jogo jogo);
    void                    Atualizar(Jogo jogo);
    void                    Remover(Jogo jogo);
    Task<int>               SalvarAsync();
}