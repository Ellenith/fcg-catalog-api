// ============================================================
// JogoController.cs — CRUD de jogos + início de compra
// ============================================================

using FCG.Contratos;
using FCG.CatalogAPI.Domain.Entidades;
using FCG.CatalogAPI.Domain.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FCG.CatalogAPI.Controllers;

[ApiController]
[Route("jogos")]
public class JogoController : ControllerBase
{
    private readonly IJogoRepositorio  _jogoRepositorio;
    private readonly IPublishEndpoint  _publishEndpoint;

    // Fuso de referência para as datas enviadas pelo Admin.
    // "America/Sao_Paulo" resolve em Windows e Linux a partir do
    // .NET 6 (usa a base ICU/IANA), então independe do fuso
    // configurado na máquina/servidor onde a API estiver rodando.
    private static readonly TimeZoneInfo FusoBrasilia =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public JogoController(
        IJogoRepositorio jogoRepositorio,
        IPublishEndpoint publishEndpoint)
    {
        _jogoRepositorio = jogoRepositorio;
        _publishEndpoint = publishEndpoint;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Cadastrar jogo")]
    [EndpointDescription("Requer role Admin.")]
    public async Task<IActionResult> Cadastrar([FromBody] CadastrarJogoRequest request)
    {
        if (await _jogoRepositorio.TituloExisteAsync(request.Titulo))
            throw new DomainException("Já existe um jogo com este título.");

        var jogo = Jogo.Criar(request.Titulo, request.Descricao, request.Preco);
        await _jogoRepositorio.AdicionarAsync(jogo);
        await _jogoRepositorio.SalvarAsync();

        return CreatedAtAction(nameof(BuscarPorId),
            new { id = jogo.Id },
            new JogoResponse(jogo, "Jogo cadastrado com sucesso!"));
    }

    [HttpGet]
    [EndpointSummary("Listar jogos")]
    [EndpointDescription("Retorna todos os jogos com preço atual.")]
    public async Task<IActionResult> ListarTodos()
    {
        var jogos = await _jogoRepositorio.ListarTodosComPromocoesAsync();
        return Ok(jogos.Select(j => new JogoResponse(j)));
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Buscar jogo por Id")]
    public async Task<IActionResult> BuscarPorId(Guid id)
    {
        var jogo = await _jogoRepositorio.BuscarPorIdComPromocoesAsync(id);
        if (jogo is null) return NotFound("Jogo não encontrado.");
        return Ok(new JogoResponse(jogo));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Atualizar preço")]
    public async Task<IActionResult> AtualizarPreco(Guid id, [FromBody] AtualizarPrecoRequest request)
    {
        var jogo = await _jogoRepositorio.BuscarPorIdAsync(id);
        if (jogo is null) return NotFound("Jogo não encontrado.");

        jogo.AtualizarPreco(request.Preco);
        _jogoRepositorio.Atualizar(jogo);
        await _jogoRepositorio.SalvarAsync();

        return Ok(new JogoResponse(jogo, "Preço atualizado com sucesso!"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Remover jogo")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var jogo = await _jogoRepositorio.BuscarPorIdAsync(id);
        if (jogo is null) return NotFound("Jogo não encontrado.");

        _jogoRepositorio.Remover(jogo);
        await _jogoRepositorio.SalvarAsync();

        return Ok(new { Mensagem = "Jogo removido com sucesso!" });
    }

    [HttpPost("{id:guid}/promocoes")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Criar promoção")]
    [EndpointDescription("Datas no horário de Brasília (UTC-3). Requer Admin.")]
    public async Task<IActionResult> CriarPromocao(Guid id, [FromBody] CriarPromocaoRequest request)
    {
        var jogo = await _jogoRepositorio.BuscarPorIdComPromocoesAsync(id);
        if (jogo is null) return NotFound("Jogo não encontrado.");

        var promocao = Promocao.Criar(
            id,
            request.PercentualDesconto,
            request.Inicio.ToUniversalTime(),
            request.Fim.ToUniversalTime());

        jogo.AdicionarPromocao(promocao);
        // Removida a chamada a _jogoRepositorio.Atualizar(jogo) —
        // o jogo já está sendo rastreado pelo EF Core (via Include),
        // então adicionar à coleção é suficiente para o EF detectar
        // e inserir a nova Promocao ao salvar
        await _jogoRepositorio.SalvarAsync();

        return Ok(new { Mensagem = "Promoção criada com sucesso!" });
    }

    [HttpGet("promocoes")]
    [EndpointSummary("Listar promoções ativas")]
    public async Task<IActionResult> ListarPromocoesAtivas()
    {
        var jogos = await _jogoRepositorio.ListarTodosComPromocoesAsync();
        var comPromocao = jogos.Where(j => j.Promocoes.Any(p => p.EstaAtiva()));

        if (!comPromocao.Any())
            return Ok(new { Mensagem = "Nenhuma promoção ativa.", Promocoes = Array.Empty<object>() });

        return Ok(comPromocao.Select(j => new
        {
            JogoId             = j.Id,
            j.Titulo,
            PrecoOriginal      = j.Preco,
            PrecoComDesconto   = j.GetCurrentPrice(),
            PercentualDesconto = j.Promocoes.First(p => p.EstaAtiva()).PercentualDesconto,
            Fim                = j.Promocoes.First(p => p.EstaAtiva()).Fim.ToLocalTime()
        }));
    }

    // ── FLUXO DE COMPRA ──────────────────────────────────
    // Inicia a compra publicando OrderPlacedEvent.
    // O jogo NÃO é adicionado à biblioteca aqui —
    // isso só acontece após o PaymentsAPI aprovar o pagamento.

    [HttpPost("{id:guid}/comprar")]
    [Authorize]
    [EndpointSummary("Comprar jogo")]
    [EndpointDescription("Inicia o fluxo de compra. O jogo é adicionado à biblioteca após aprovação do pagamento.")]
    public async Task<IActionResult> Comprar(Guid id)
    {
        // Extrai o UsuarioId do token JWT
        var sub = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(sub!);

        var jogo = await _jogoRepositorio.BuscarPorIdComPromocoesAsync(id);
        if (jogo is null)
            return NotFound("Jogo não encontrado.");

        // Publica OrderPlacedEvent para o PaymentsAPI
        var orderId = Guid.NewGuid();
        await _publishEndpoint.Publish(new OrderPlacedEvent(
            orderId,
            usuarioId,
            jogo.Id,
            jogo.Titulo,
            jogo.GetCurrentPrice(),
            DateTime.UtcNow));

        return Accepted(new
        {
            OrderId  = orderId,
            Mensagem = "Pedido recebido! O pagamento está sendo processado.",
            JogoId   = jogo.Id,
            Titulo   = jogo.Titulo,
            Preco    = jogo.GetCurrentPrice()
        });
    }
}

// DTOs
public record CadastrarJogoRequest(string Titulo, string Descricao, decimal Preco);
public record AtualizarPrecoRequest(decimal Preco);
public record CriarPromocaoRequest(decimal PercentualDesconto, DateTime Inicio, DateTime Fim);
public record JogoResponse(Guid Id, string Titulo, string Descricao, decimal Preco, decimal PrecoAtual, bool TemPromocaoAtiva, DateTime CriadoEm, string? Mensagem = null)
{
    public JogoResponse(Jogo j, string? mensagem = null)
        : this(j.Id, j.Titulo, j.Descricao, j.Preco, j.GetCurrentPrice(),
               j.Promocoes.Any(p => p.EstaAtiva()), j.CriadoEm.ToLocalTime(), mensagem) { }
}