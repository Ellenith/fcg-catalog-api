// ============================================================
// PaymentProcessedConsumer.cs — Consumidor do CatalogAPI
//
// Consome PaymentProcessedEvent publicado pelo PaymentsAPI.
// Se Approved → adiciona jogo à biblioteca do usuário.
// Se Rejected → apenas loga — nenhuma ação necessária.
// ============================================================

using FCG.CatalogAPI.Contratos;
using FCG.CatalogAPI.Domain.Entidades;
using FCG.CatalogAPI.Domain.Interfaces;
using MassTransit;

namespace FCG.CatalogAPI.Infraestrutura.Mensageria;

public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly IBibliotecaRepositorio _bibliotecaRepositorio;
    private readonly ILogger<PaymentProcessedConsumer> _logger;

    public PaymentProcessedConsumer(
        IBibliotecaRepositorio bibliotecaRepositorio,
        ILogger<PaymentProcessedConsumer> logger)
    {
        _bibliotecaRepositorio = bibliotecaRepositorio;
        _logger                = logger;
    }

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var evento = context.Message;

        _logger.LogInformation(
            "Pagamento recebido: OrderId={OrderId} Status={Status}",
            evento.OrderId,
            evento.Status);

        // Ignora se pagamento foi rejeitado
        if (evento.Status != "Approved")
        {
            _logger.LogWarning(
                "Pagamento rejeitado: OrderId={OrderId} Motivo={Mensagem}",
                evento.OrderId,
                evento.Mensagem);
            return;
        }

        // Idempotência — evita duplicar caso evento chegue duas vezes
        if (await _bibliotecaRepositorio.OrderJaProcessadaAsync(evento.OrderId))
        {
            _logger.LogWarning(
                "Order já processada (idempotência): OrderId={OrderId}",
                evento.OrderId);
            return;
        }

        // Verifica duplicata de jogo
        if (await _bibliotecaRepositorio.JogoJaAdquiridoAsync(evento.UsuarioId, evento.JogoId))
        {
            _logger.LogWarning(
                "Usuário já possui o jogo: UsuarioId={UsuarioId} JogoId={JogoId}",
                evento.UsuarioId,
                evento.JogoId);
            return;
        }

        // Registra a aquisição na biblioteca
        var aquisicao = Biblioteca.Registrar(
            evento.UsuarioId,
            evento.JogoId,
            evento.Preco,
            evento.OrderId);

        await _bibliotecaRepositorio.AdicionarAsync(aquisicao);
        await _bibliotecaRepositorio.SalvarAsync();

        _logger.LogInformation(
            "Jogo adicionado à biblioteca: UsuarioId={UsuarioId} JogoId={JogoId}",
            evento.UsuarioId,
            evento.JogoId);
    }
}