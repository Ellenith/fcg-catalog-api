using FCG.CatalogAPI.Domain.Entidades;

namespace FCG.CatalogAPI.Domain.Interfaces;

public record ResultadoBusca(
    Guid Id, string Titulo, string Descricao,
    decimal Preco, decimal PrecoAtual, bool TemPromocaoAtiva, double Score);

public interface IBuscaJogoRepositorio
{
    Task IndexarAsync(Jogo jogo);
    Task RemoverAsync(Guid id);
    Task<int> ReindexarTodosAsync(IEnumerable<Jogo> jogos);
    Task<IReadOnlyList<ResultadoBusca>> BuscarAsync(string termo, int tamanho = 10);
}