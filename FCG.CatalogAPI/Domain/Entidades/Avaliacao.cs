using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace FCG.CatalogAPI.Domain.Entidades;

public class Avaliacao
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; private set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid JogoId { get; private set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UsuarioId { get; private set; }
    public int Nota { get; private set; }
    public string Comentario { get; private set; } = string.Empty;
    public DateTime CriadoEm { get; private set; }

    private Avaliacao() { }

    public static Avaliacao Criar(Guid jogoId, Guid usuarioId, int nota, string comentario)
    {
        if (nota < 1 || nota > 5)
            throw new DomainException("A nota deve estar entre 1 e 5.");

        if (string.IsNullOrWhiteSpace(comentario))
            throw new DomainException("O comentário não pode ser vazio.");

        return new Avaliacao
        {
            JogoId = jogoId,
            UsuarioId = usuarioId,
            Nota = nota,
            Comentario = comentario.Trim(),
            CriadoEm = DateTime.UtcNow
        };
    }
}