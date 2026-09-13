using FCG.CatalogAPI.Domain.Entidades;

namespace FCG.CatalogAPI.Domain.Interfaces;

public interface IAvaliacaoRepositorio
{
    Task AdicionarAsync(Avaliacao avaliacao);
    Task<IEnumerable<Avaliacao>> ListarPorJogoAsync(Guid jogoId);
}