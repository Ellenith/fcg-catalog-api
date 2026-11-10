// ============================================================
// JogoTests.cs — Testes unitários da entidade Jogo
//
// Valida todas as regras de negócio encapsuladas em Jogo.cs:
// - Criação com dados válidos e inválidos
// - Atualização de preço
// ============================================================

using FCG.CatalogAPI.Domain.Entidades;
using FluentAssertions;

namespace FCG.CatalogAPI.Tests;

public class TesteJogos
{
    // ----------------------------------------------------------
    // Constantes reutilizadas nos testes
    // ----------------------------------------------------------
    private const string TituloValido    = "Minecraft";
    private const string DescricaoValida = "Jogo de construção e sobrevivência.";
    private const decimal PrecoValido    = 99.90m;

    // ==========================================================
    // TESTES DE CRIAÇÃO — Jogo.Criar()
    // ==========================================================

    [Fact]
    public void Criar_ComDadosValidos_DeveRetornarJogoCorreto()
    {
        // Act
        var jogo = Jogo.Criar(TituloValido, DescricaoValida, PrecoValido);

        // Assert
        jogo.Id.Should().NotBeEmpty("deve gerar um Guid automaticamente");
        jogo.Titulo.Should().Be(TituloValido);
        jogo.Descricao.Should().Be(DescricaoValida);
        jogo.Preco.Should().Be(PrecoValido);
        jogo.CriadoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Criar_ComPrecoZero_DevePermitirJogoGratuito()
    {
        // Jogos gratuitos são válidos no domínio FCG
        // Act
        var jogo = Jogo.Criar(TituloValido, DescricaoValida, 0m);

        // Assert
        jogo.Preco.Should().Be(0m);
    }

    // ----------------------------------------------------------
    // Testes de validação do Título
    // ----------------------------------------------------------

    [Fact]
    public void Criar_ComTituloVazio_DeveLancarDomainException()
    {
        // Act
        var acao = () => Jogo.Criar("", DescricaoValida, PrecoValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Título é obrigatório.");
    }

    [Fact]
    public void Criar_ComTituloMuitoCurto_DeveLancarDomainException()
    {
        // Act — título com apenas 1 caractere
        var acao = () => Jogo.Criar("A", DescricaoValida, PrecoValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Título deve ter pelo menos 2 caracteres.");
    }

    [Fact]
    public void Criar_ComTituloMuitoLongo_DeveLancarDomainException()
    {
        // Arrange — gera string com 201 caracteres
        var tituloLongo = new string('A', 201);

        // Act
        var acao = () => Jogo.Criar(tituloLongo, DescricaoValida, PrecoValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Título deve ter no máximo 200 caracteres.");
    }

    // ----------------------------------------------------------
    // Testes de validação da Descrição
    // ----------------------------------------------------------

    [Fact]
    public void Criar_ComDescricaoVazia_DeveLancarDomainException()
    {
        // Act
        var acao = () => Jogo.Criar(TituloValido, "", PrecoValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Descrição é obrigatória.");
    }

    [Fact]
    public void Criar_ComDescricaoMuitoLonga_DeveLancarDomainException()
    {
        // Arrange — gera string com 2001 caracteres
        var descricaoLonga = new string('A', 2001);

        // Act
        var acao = () => Jogo.Criar(TituloValido, descricaoLonga, PrecoValido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Descrição deve ter no máximo 2000 caracteres.");
    }

    // ----------------------------------------------------------
    // Testes de validação do Preço
    // ----------------------------------------------------------

    [Fact]
    public void Criar_ComPrecoNegativo_DeveLancarDomainException()
    {
        // Act
        var acao = () => Jogo.Criar(TituloValido, DescricaoValida, -1m);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Preço não pode ser negativo.");
    }

    // ==========================================================
    // TESTES DE ATUALIZAÇÃO — Jogo.AtualizarPreco()
    // ==========================================================

    [Fact]
    public void AtualizarPreco_ComPrecoValido_DeveAtualizarPreco()
    {
        // Arrange
        var jogo = Jogo.Criar(TituloValido, DescricaoValida, PrecoValido);
        var novoPreco = 79.90m;

        // Act
        jogo.AtualizarPreco(novoPreco);

        // Assert
        jogo.Preco.Should().Be(novoPreco);
    }

    [Fact]
    public void AtualizarPreco_ComPrecoNegativo_DeveLancarDomainException()
    {
        // Arrange
        var jogo = Jogo.Criar(TituloValido, DescricaoValida, PrecoValido);

        // Act
        var acao = () => jogo.AtualizarPreco(-1m);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("Preço não pode ser negativo.");
    }

    [Fact]
    public void AtualizarPreco_ParaZero_DevePermitirJogoGratuito()
    {
        // Arrange
        var jogo = Jogo.Criar(TituloValido, DescricaoValida, PrecoValido);

        // Act
        jogo.AtualizarPreco(0m);

        // Assert
        jogo.Preco.Should().Be(0m);
    }
}