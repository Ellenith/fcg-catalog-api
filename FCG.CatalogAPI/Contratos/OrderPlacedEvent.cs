// Namespace padronizado — igual em todos os serviços
namespace FCG.Contratos;

public record OrderPlacedEvent(
    Guid     OrderId,
    Guid     UsuarioId,
    Guid     JogoId,
    string   TituloJogo,
    decimal  Preco,
    DateTime CriadoEm);