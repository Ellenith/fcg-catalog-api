// ============================================================
// Biblioteca.cs — Registro de aquisição de jogo
// ============================================================

namespace FCG.CatalogAPI.Domain.Entidades;

public class Biblioteca
{
    public Guid     Id          { get; private set; }
    public Guid     UsuarioId   { get; private set; }
    public Guid     JogoId      { get; private set; }
    public decimal  PrecoPago   { get; private set; }
    public DateTime AdquiridoEm { get; private set; }
    public Guid     OrderId     { get; private set; }

    private Biblioteca() { }

    public static Biblioteca Registrar(
        Guid    usuarioId,
        Guid    jogoId,
        decimal precoPago,
        Guid    orderId)
    {
        if (usuarioId == Guid.Empty)
            throw new DomainException("UsuarioId é obrigatório.");
        if (jogoId == Guid.Empty)
            throw new DomainException("JogoId é obrigatório.");
        if (precoPago < 0)
            throw new DomainException("Preço pago não pode ser negativo.");

        return new Biblioteca
        {
            Id          = Guid.NewGuid(),
            UsuarioId   = usuarioId,
            JogoId      = jogoId,
            PrecoPago   = precoPago,
            AdquiridoEm = DateTime.UtcNow,
            OrderId     = orderId
        };
    }
}