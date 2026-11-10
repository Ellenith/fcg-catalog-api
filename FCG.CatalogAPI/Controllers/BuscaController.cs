using FCG.CatalogAPI.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.CatalogAPI.Controllers;

[ApiController]
[Route("search")]
public class BuscaController : ControllerBase
{
    private readonly IBuscaJogoRepositorio _busca;
    private readonly IJogoRepositorio _jogoRepositorio;
    private readonly ILogger<BuscaController> _logger;

    public BuscaController(IBuscaJogoRepositorio busca, IJogoRepositorio jogoRepositorio,
        ILogger<BuscaController> logger)
    {
        _busca = busca;
        _jogoRepositorio = jogoRepositorio;
        _logger = logger;
    }

    [HttpGet]
    [EndpointSummary("Buscar jogos")]
    [EndpointDescription("Busca textual no Elasticsearch com tolerância a erros de digitação (fuzzy), ordenada por relevância.")]
    public async Task<IActionResult> Buscar([FromQuery] string? q, [FromQuery] int tamanho = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { Mensagem = "Informe o termo de busca no parâmetro 'q'." });

        tamanho = Math.Clamp(tamanho, 1, 50);

        try
        {
            var resultados = await _busca.BuscarAsync(q.Trim(), tamanho);
            return Ok(new { Termo = q.Trim(), Total = resultados.Count, Resultados = resultados });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na busca por '{Termo}'", q);
            return StatusCode(503, new { Mensagem = "Serviço de busca indisponível no momento." });
        }
    }

    [HttpPost("reindexar")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Reindexar todos os jogos")]
    [EndpointDescription("Recria o índice, se necessário, e envia todos os jogos do banco para o Elasticsearch. Requer Admin.")]
    public async Task<IActionResult> Reindexar()
    {
        try
        {
            var jogos = await _jogoRepositorio.ListarTodosComPromocoesAsync();
            var total = await _busca.ReindexarTodosAsync(jogos);
            return Ok(new { Mensagem = "Reindexação concluída.", Total = total });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na reindexação");
            return StatusCode(502, new { Mensagem = "Falha ao reindexar no Elasticsearch." });
        }
    }
}