// ============================================================
// TestePromocao.cs — Testes unitários da entidade Promocao
//
// Valida as regras de negócio de promoções:
// - Criação com dados válidos e inválidos
// - Verificação de promoção ativa
// ============================================================

using FCG.CatalogAPI.Domain.Entidades;
using FluentAssertions;

namespace FCG.CatalogAPI.Tests;

public class TestePromocao
{
    // ----------------------------------------------------------
    // Constantes reutilizadas nos testes
    // ----------------------------------------------------------
    private static readonly Guid JogoIdValido = Guid.NewGuid();

    // Período válido — começa no passado e termina no futuro
    private static readonly DateTime InicioValido = DateTime.UtcNow.AddDays(-1);
    private static readonly DateTime FimValido    = DateTime.UtcNow.AddDays(7);

    // ==========================================================
    // TESTES DE CRIAÇÃO — Promocao.Criar()
    // ==========================================================

    [Fact]
    public void Criar_ComDadosValidos_DeveRetornarPromocaoCorreta()
    {
        // Act
        var promocao = Promocao.Criar(
            JogoIdValido,
            20m,
            InicioValido,
            FimValido);

        // Assert
        promocao.Id.Should().NotBeEmpty();
        promocao.JogoId.Should().Be(JogoIdValido);
        promocao.PercentualDesconto.Should().Be(20m);
        promocao.Inicio.Should().Be(InicioValido);
        promocao.Fim.Should().Be(FimValido);
    }

    // ----------------------------------------------------------
    // Testes de validação do desconto
    // ----------------------------------------------------------

    [Fact]
    public void Criar_ComDescontoZero_DeveLancarDomainException()
    {
        // Act
        var acao = () => Promocao.Criar(JogoIdValido, 0m, InicioValido, FimValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Desconto deve ser entre 1% e 100%.");
    }

    [Fact]
    public void Criar_ComDescontoNegativo_DeveLancarDomainException()
    {
        // Act
        var acao = () => Promocao.Criar(JogoIdValido, -10m, InicioValido, FimValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Desconto deve ser entre 1% e 100%.");
    }

    [Fact]
    public void Criar_ComDescontoAcimaDecem_DeveLancarDomainException()
    {
        // Act
        var acao = () => Promocao.Criar(JogoIdValido, 101m, InicioValido, FimValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Desconto deve ser entre 1% e 100%.");
    }

    [Fact]
    public void Criar_ComDescontoCem_DevePermitirDescontoTotal()
    {
        // Desconto de 100% é válido — jogo gratuito temporariamente
        // Act
        var promocao = Promocao.Criar(JogoIdValido, 100m, InicioValido, FimValido);

        // Assert
        promocao.PercentualDesconto.Should().Be(100m);
    }

    // ----------------------------------------------------------
    // Testes de validação do período
    // ----------------------------------------------------------

    [Fact]
    public void Criar_ComFimAnteriorAoInicio_DeveLancarDomainException()
    {
        // Arrange — fim antes do início
        var inicio = DateTime.UtcNow.AddDays(5);
        var fim    = DateTime.UtcNow.AddDays(1);

        // Act
        var acao = () => Promocao.Criar(JogoIdValido, 20m, inicio, fim);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("A data de fim deve ser posterior à data de início.");
    }

    [Fact]
    public void Criar_ComFimIgualAoInicio_DeveLancarDomainException()
    {
        // Arrange — fim igual ao início
        var data = DateTime.UtcNow.AddDays(1);

        // Act
        var acao = () => Promocao.Criar(JogoIdValido, 20m, data, data);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("A data de fim deve ser posterior à data de início.");
    }

    // ==========================================================
    // TESTES DE VERIFICAÇÃO — Promocao.EstaAtiva()
    // ==========================================================

    [Fact]
    public void EstaAtiva_DentroDoperiodo_DeveRetornarTrue()
    {
        // Arrange — promoção ativa agora
        var promocao = Promocao.Criar(
            JogoIdValido,
            20m,
            DateTime.UtcNow.AddDays(-1),    // começou ontem
            DateTime.UtcNow.AddDays(7));     // termina em 7 dias

        // Act & Assert
        promocao.EstaAtiva().Should().BeTrue();
    }

    [Fact]
    public void EstaAtiva_AntesDoInicio_DeveRetornarFalse()
    {
        // Arrange — promoção ainda não começou
        var promocao = Promocao.Criar(
            JogoIdValido,
            20m,
            DateTime.UtcNow.AddDays(1),     // começa amanhã
            DateTime.UtcNow.AddDays(7));     // termina em 7 dias

        // Act & Assert
        promocao.EstaAtiva().Should().BeFalse();
    }

    [Fact]
    public void EstaAtiva_AposOFim_DeveRetornarFalse()
    {
        // Arrange — promoção já expirou
        var promocao = Promocao.Criar(
            JogoIdValido,
            20m,
            DateTime.UtcNow.AddDays(-10),   // começou há 10 dias
            DateTime.UtcNow.AddDays(-1));    // terminou ontem

        // Act & Assert
        promocao.EstaAtiva().Should().BeFalse();
    }

    // ==========================================================
    // TESTES DE INTEGRAÇÃO COM JOGO — Jogo.GetCurrentPrice()
    // ==========================================================

    [Fact]
    public void GetCurrentPrice_SemPromocaoAtiva_DeveRetornarPrecoOriginal()
    {
        // Arrange
        var jogo = Jogo.Criar("Minecraft", "Jogo de blocos", 100m);

        // Act & Assert
        jogo.GetCurrentPrice().Should().Be(100m);
    }

    [Fact]
    public void GetCurrentPrice_ComPromocaoAtiva_DeveRetornarPrecoComDesconto()
    {
        // Arrange
        var jogo = Jogo.Criar("Minecraft", "Jogo de blocos", 100m);

        var promocao = Promocao.Criar(
            jogo.Id,
            20m,                            // 20% de desconto
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(7));

        jogo.AdicionarPromocao(promocao);

        // Act
        var precoAtual = jogo.GetCurrentPrice();

        // Assert — 100 * (1 - 0.20) = 80
        precoAtual.Should().Be(80m);
    }

    [Fact]
    public void GetCurrentPrice_ComPromocaoExpirada_DeveRetornarPrecoOriginal()
    {
        // Arrange
        var jogo = Jogo.Criar("Minecraft", "Jogo de blocos", 100m);

        var promocaoExpirada = Promocao.Criar(
            jogo.Id,
            20m,
            DateTime.UtcNow.AddDays(-10),   // começou há 10 dias
            DateTime.UtcNow.AddDays(-1));    // terminou ontem

        jogo.AdicionarPromocao(promocaoExpirada);

        // Act
        var precoAtual = jogo.GetCurrentPrice();

        // Assert — promoção expirada, retorna preço original
        precoAtual.Should().Be(100m);
    }
}