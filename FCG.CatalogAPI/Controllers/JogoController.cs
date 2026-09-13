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
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;


namespace FCG.CatalogAPI.Controllers;

[ApiController]
[Route("jogos")]
public class JogoController : ControllerBase
{
    private readonly IJogoRepositorio  _jogoRepositorio;
    private readonly IBibliotecaRepositorio _bibliotecaRepositorio;
    private readonly IAvaliacaoRepositorio _avaliacaoRepositorio;
    private readonly IPublishEndpoint  _publishEndpoint;
    private readonly IDistributedCache _cache;

    // Fuso de referência para as datas enviadas pelo Admin.
    // "America/Sao_Paulo" resolve em Windows e Linux a partir do
    // .NET 6 (usa a base ICU/IANA), então independe do fuso
    // configurado na máquina/servidor onde a API estiver rodando.
    private static readonly TimeZoneInfo FusoBrasilia =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public JogoController(
        IJogoRepositorio jogoRepositorio,
        IBibliotecaRepositorio bibliotecaRepositorio,
        IAvaliacaoRepositorio avaliacaoRepositorio,
        IDistributedCache cache,
        IPublishEndpoint publishEndpoint)
    {
        _jogoRepositorio = jogoRepositorio;
        _bibliotecaRepositorio = bibliotecaRepositorio;
        _avaliacaoRepositorio = avaliacaoRepositorio;
        _cache = cache;
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
    [EndpointDescription("Retorna todos os jogos com preço atual. Resultado em cache (Redis) por 30s.")]
    public async Task<IActionResult> ListarTodos()
    {
        const string chaveCache = "jogos:todos";

        // Cache-aside: tenta o cache primeiro; se falhar, segue pro banco.
        // O try garante que uma indisponibilidade do Redis degrade a
        // performance, mas não derrube o endpoint.
        try
        {
            var cacheado = await _cache.GetStringAsync(chaveCache);
            if (cacheado is not null)
            {
                Response.Headers.Append("X-Cache", "HIT");
                return Content(cacheado, "application/json");
            }
        }
        catch { /* cache indisponível — segue para o banco */ }

        var jogos = await _jogoRepositorio.ListarTodosComPromocoesAsync();
        var resposta = jogos.Select(j => new JogoResponse(j));
        var json = JsonSerializer.Serialize(resposta);

        try
        {
            await _cache.SetStringAsync(chaveCache, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            });
        }
        catch { /* falha ao gravar no cache não deve quebrar a resposta */ }

        Response.Headers.Append("X-Cache", "MISS");
        return Content(json, "application/json");
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
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(sub!);

        var jogo = await _jogoRepositorio.BuscarPorIdComPromocoesAsync(id);
        if (jogo is null)
            return NotFound("Jogo não encontrado.");

        // Validação prévia — evita publicar OrderPlacedEvent e disparar
        // todo o fluxo de pagamento para uma compra que sabemos, de
        // antemão, que será recusada por duplicidade.
        //
        // IMPORTANTE: isso reduz a janela de corrida mas não a elimina
        // por completo. Duas requisições quase simultâneas ainda podem
        // passar por esta validação antes que a primeira seja concluída
        // (condição de corrida clássica). Ver README para detalhes
        // sobre essa limitação conhecida e a defesa em profundidade
        // que existe no banco de dados (índice único).
        if (await _bibliotecaRepositorio.JogoJaAdquiridoAsync(usuarioId, id))
            throw new DomainException("Você já possui este jogo na sua biblioteca.");

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
    [HttpPost("{id:guid}/avaliacoes")]
    [Authorize]
    [EndpointSummary("Avaliar jogo")]
    [EndpointDescription("Registra uma avaliação (nota de 1 a 5 e comentário) para o jogo.")]
    public async Task<IActionResult> Avaliar(Guid id, [FromBody] CriarAvaliacaoRequest request)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(sub!);

        var avaliacao = Avaliacao.Criar(id, usuarioId, request.Nota, request.Comentario);
        await _avaliacaoRepositorio.AdicionarAsync(avaliacao);

        return Created($"/jogos/{id}/avaliacoes", new
        {
            avaliacao.Id,
            avaliacao.JogoId,
            avaliacao.Nota,
            avaliacao.Comentario,
            avaliacao.CriadoEm,
            mensagem = "Avaliação registrada com sucesso!"
        });
    }

    [HttpGet("{id:guid}/avaliacoes")]
    [EndpointSummary("Listar avaliações do jogo")]
    [EndpointDescription("Retorna todas as avaliações de um jogo, das mais recentes para as mais antigas.")]
    public async Task<IActionResult> ListarAvaliacoes(Guid id)
    {
        var avaliacoes = await _avaliacaoRepositorio.ListarPorJogoAsync(id);
        return Ok(avaliacoes);
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

public record CriarAvaliacaoRequest(int Nota, string Comentario);