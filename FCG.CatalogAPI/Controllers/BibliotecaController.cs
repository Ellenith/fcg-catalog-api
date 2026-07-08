// ============================================================
// BibliotecaController.cs — Consulta da biblioteca do usuário
// GET /biblioteca → lista jogos adquiridos (autenticado)
// ============================================================

using FCG.CatalogAPI.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FCG.CatalogAPI.Controllers;

[ApiController]
[Route("biblioteca")]
[Authorize]
public class BibliotecaController : ControllerBase
{
    private readonly IBibliotecaRepositorio _repositorio;

    public BibliotecaController(IBibliotecaRepositorio repositorio)
        => _repositorio = repositorio;

    [HttpGet]
    [EndpointSummary("Listar jogos adquiridos")]
    [EndpointDescription("Retorna os jogos da biblioteca do usuário autenticado.")]
    public async Task<IActionResult> ListarJogosAdquiridos()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(sub!);

        var aquisicoes = await _repositorio.ListarPorUsuarioAsync(usuarioId);

        return Ok(aquisicoes.Select(a => new
        {
            a.Id,
            a.JogoId,
            a.PrecoPago,
            AdquiridoEm = a.AdquiridoEm.ToLocalTime()
        }));
    }
}