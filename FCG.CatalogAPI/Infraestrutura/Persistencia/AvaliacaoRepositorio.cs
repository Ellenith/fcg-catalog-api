using FCG.CatalogAPI.Domain.Entidades;
using FCG.CatalogAPI.Domain.Interfaces;
using MongoDB.Driver;

namespace FCG.CatalogAPI.Infraestrutura.Persistencia;

public class AvaliacaoRepositorio : IAvaliacaoRepositorio
{
    private readonly IMongoCollection<Avaliacao> _colecao;

    public AvaliacaoRepositorio(IConfiguration configuration)
    {
        var client = new MongoClient(configuration["MongoDB:ConnectionString"]);
        var database = client.GetDatabase(configuration["MongoDB:Database"]);
        _colecao = database.GetCollection<Avaliacao>("avaliacoes");
    }

    public async Task AdicionarAsync(Avaliacao avaliacao)
        => await _colecao.InsertOneAsync(avaliacao);

    public async Task<IEnumerable<Avaliacao>> ListarPorJogoAsync(Guid jogoId)
        => await _colecao.Find(a => a.JogoId == jogoId)
                         .SortByDescending(a => a.CriadoEm)
                         .ToListAsync();
}