// ============================================================
// Promocao.cs — Entidade do contexto Catálogo
// ============================================================

namespace FCG.CatalogAPI.Domain.Entidades;

public class Promocao
{
    public Guid     Id                 { get; private set; }
    public Guid     JogoId             { get; private set; }
    public decimal  PercentualDesconto { get; private set; }
    public DateTime Inicio             { get; private set; }
    public DateTime Fim                { get; private set; }

    private Promocao() { }

    public static Promocao Criar(
        Guid jogoId,
        decimal percentualDesconto,
        DateTime inicio,
        DateTime fim)
    {
        if (percentualDesconto <= 0 || percentualDesconto > 100)
            throw new DomainException("Desconto deve ser entre 1% e 100%.");
        if (fim <= inicio)
            throw new DomainException("A data de fim deve ser posterior à data de início.");

        return new Promocao
        {
            Id                 = Guid.NewGuid(),
            JogoId             = jogoId,
            PercentualDesconto = percentualDesconto,
            Inicio             = inicio,
            Fim                = fim
        };
    }

    public bool EstaAtiva()
        => DateTime.UtcNow >= Inicio && DateTime.UtcNow <= Fim;
}