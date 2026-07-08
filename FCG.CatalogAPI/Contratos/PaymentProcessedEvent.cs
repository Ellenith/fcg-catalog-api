namespace FCG.Contratos;

public record PaymentProcessedEvent(
    Guid     OrderId,
    Guid     UsuarioId,
    Guid     JogoId,
    decimal  Preco,
    string   Status,
    string   Mensagem,
    DateTime ProcessadoEm);