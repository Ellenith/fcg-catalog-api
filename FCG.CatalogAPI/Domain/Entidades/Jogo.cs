// ============================================================
// Jogo.cs — Agregado do contexto Catálogo
// Copiado da Fase 1 e adaptado para microsserviço
// ============================================================

namespace FCG.CatalogAPI.Domain.Entidades;

public class Jogo
{
    public Guid     Id        { get; private set; }
    public string   Titulo    { get; private set; } = string.Empty;
    public string   Descricao { get; private set; } = string.Empty;
    public decimal  Preco     { get; private set; }
    public DateTime CriadoEm  { get; private set; }

    private readonly List<Promocao> _promocoes = new();
    public IReadOnlyCollection<Promocao> Promocoes => _promocoes;

    private Jogo() { }

    public static Jogo Criar(string titulo, string descricao, decimal preco)
    {
        ValidarTitulo(titulo);
        ValidarDescricao(descricao);
        ValidarPreco(preco);

        return new Jogo
        {
            Id        = Guid.NewGuid(),
            Titulo    = titulo.Trim(),
            Descricao = descricao.Trim(),
            Preco     = preco,
            CriadoEm  = DateTime.UtcNow
        };
    }

    public void AtualizarPreco(decimal novoPreco)
    {
        ValidarPreco(novoPreco);
        Preco = novoPreco;
    }

    public decimal GetCurrentPrice()
    {
        var promo = Promocoes.FirstOrDefault(p => p.EstaAtiva());
        return promo is null ? Preco : Preco * (1 - promo.PercentualDesconto / 100);
    }

    private static void ValidarTitulo(string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("Título é obrigatório.");
        if (titulo.Trim().Length < 2)
            throw new DomainException("Título deve ter pelo menos 2 caracteres.");
        if (titulo.Trim().Length > 200)
            throw new DomainException("Título deve ter no máximo 200 caracteres.");
    }

    private static void ValidarDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("Descrição é obrigatória.");
        if (descricao.Trim().Length > 2000)
            throw new DomainException("Descrição deve ter no máximo 2000 caracteres.");
    }

    private static void ValidarPreco(decimal preco)
    {
        if (preco < 0)
            throw new DomainException("Preço não pode ser negativo.");
    }

    // ----------------------------------------------------------
    // Adiciona uma promoção ao jogo
    // Encapsula a regra no domínio ao invés de expor a lista
    // ----------------------------------------------------------
    public void AdicionarPromocao(Promocao promocao)
    {
        _promocoes.Add(promocao);
    }
}