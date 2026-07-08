using FCG.CatalogAPI.Domain.Entidades;

namespace FCG.CatalogAPI.Domain.Interfaces;

public interface IBibliotecaRepositorio
{
    Task<IEnumerable<Biblioteca>> ListarPorUsuarioAsync(Guid usuarioId);
    Task<bool>                    JogoJaAdquiridoAsync(Guid usuarioId, Guid jogoId);
    Task<bool>                    OrderJaProcessadaAsync(Guid orderId);
    Task                          AdicionarAsync(Biblioteca biblioteca);
    Task<int>                     SalvarAsync();
}