// ============================================================
// PaymentProcessedEvent.cs — Evento consumido pelo CatalogAPI
// após o PaymentsAPI processar o pagamento.
// Se Approved → adiciona jogo à biblioteca do usuário.
// ============================================================

namespace FCG.CatalogAPI.Contratos;

public record PaymentProcessedEvent(
    Guid    OrderId,
    Guid    UsuarioId,
    Guid    JogoId,
    decimal Preco,
    string  Status,     // "Approved" ou "Rejected"
    string  Mensagem,
    DateTime ProcessadoEm);