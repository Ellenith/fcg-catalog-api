using System.Text;
using System.Text.Json;
using FCG.CatalogAPI.Domain.Entidades;
using FCG.CatalogAPI.Domain.Interfaces;

namespace FCG.CatalogAPI.Infraestrutura.Busca;

// Acesso ao Elasticsearch via API REST.
// Indexação nunca derruba a operação principal: o banco relacional
// é a fonte da verdade e o índice é uma cópia para busca.
public class BuscaJogoRepositorio : IBuscaJogoRepositorio
{
    private const string Indice = "jogos";

    private const string MappingIndice = """
    {
      "settings": { "analysis": { "analyzer": { "folding": {
        "tokenizer": "standard", "filter": ["lowercase", "asciifolding"] } } } },
      "mappings": { "properties": {
        "id":               { "type": "keyword" },
        "titulo":           { "type": "text", "analyzer": "folding",
                              "fields": { "raw": { "type": "keyword" } } },
        "descricao":        { "type": "text", "analyzer": "folding" },
        "preco":            { "type": "double" },
        "precoAtual":       { "type": "double" },
        "temPromocaoAtiva": { "type": "boolean" },
        "criadoEm":         { "type": "date" }
      } }
    }
    """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<BuscaJogoRepositorio> _logger;

    public BuscaJogoRepositorio(HttpClient http, ILogger<BuscaJogoRepositorio> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Sem endpoint/chave configurados (ex.: testes), a busca fica desligada
    private bool Habilitado => _http.BaseAddress is not null;

    private static object ParaDocumento(Jogo j) => new
    {
        id = j.Id,
        titulo = j.Titulo,
        descricao = j.Descricao,
        preco = j.Preco,
        precoAtual = j.GetCurrentPrice(),
        temPromocaoAtiva = j.Promocoes.Any(p => p.EstaAtiva()),
        criadoEm = j.CriadoEm
    };

    public async Task IndexarAsync(Jogo jogo)
    {
        if (!Habilitado) return;
        try
        {
            var resp = await _http.PutAsJsonAsync($"{Indice}/_doc/{jogo.Id}", ParaDocumento(jogo), Json);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Elasticsearch: falha ao indexar o jogo {JogoId} (HTTP {Status})",
                    jogo.Id, (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch indisponível ao indexar o jogo {JogoId}", jogo.Id);
        }
    }

    public async Task RemoverAsync(Guid id)
    {
        if (!Habilitado) return;
        try
        {
            var resp = await _http.DeleteAsync($"{Indice}/_doc/{id}");
            if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 404)
                _logger.LogWarning("Elasticsearch: falha ao remover o jogo {JogoId} (HTTP {Status})",
                    id, (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch indisponível ao remover o jogo {JogoId}", id);
        }
    }

    // Reindexação em lote (bulk): popula o índice com os jogos já existentes
    public async Task<int> ReindexarTodosAsync(IEnumerable<Jogo> jogos)
    {
        if (!Habilitado) throw new InvalidOperationException("Elasticsearch não configurado.");

        await GarantirIndiceAsync();

        var lista = jogos.ToList();
        if (lista.Count == 0) return 0;

        var ndjson = new StringBuilder();
        foreach (var j in lista)
        {
            ndjson.Append(JsonSerializer.Serialize(new { index = new { _index = Indice, _id = j.Id } }, Json)).Append('\n');
            ndjson.Append(JsonSerializer.Serialize(ParaDocumento(j), Json)).Append('\n');
        }

        var resp = await _http.PostAsync("_bulk",
            new StringContent(ndjson.ToString(), Encoding.UTF8, "application/x-ndjson"));
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (doc.RootElement.TryGetProperty("errors", out var erros) && erros.GetBoolean())
            throw new InvalidOperationException("O Elasticsearch retornou erros no bulk.");

        return lista.Count;
    }

    // Fuzzy (tolerância a erro de digitação) + relevância (_score), título com peso 3
    public async Task<IReadOnlyList<ResultadoBusca>> BuscarAsync(string termo, int tamanho = 10)
    {
        if (!Habilitado) throw new InvalidOperationException("Elasticsearch não configurado.");

        var consulta = new
        {
            size = tamanho,
            query = new
            {
                multi_match = new
                {
                    query = termo,
                    fields = new[] { "titulo^3", "descricao" },
                    fuzziness = "AUTO"
                }
            }
        };

        var resp = await _http.PostAsJsonAsync($"{Indice}/_search", consulta, Json);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var resultados = new List<ResultadoBusca>();

        foreach (var hit in doc.RootElement.GetProperty("hits").GetProperty("hits").EnumerateArray())
        {
            var src = hit.GetProperty("_source");
            if (!Guid.TryParse(src.GetProperty("id").GetString(), out var id)) continue;

            resultados.Add(new ResultadoBusca(
                id,
                src.GetProperty("titulo").GetString() ?? "",
                src.GetProperty("descricao").GetString() ?? "",
                src.GetProperty("preco").GetDecimal(),
                src.GetProperty("precoAtual").GetDecimal(),
                src.GetProperty("temPromocaoAtiva").GetBoolean(),
                hit.GetProperty("_score").GetDouble()));
        }

        return resultados;
    }

    // Cria o índice com o mapping se ele ainda não existir
    private async Task GarantirIndiceAsync()
    {
        using var head = new HttpRequestMessage(HttpMethod.Head, Indice);
        var existe = await _http.SendAsync(head);
        if (existe.IsSuccessStatusCode) return;

        var criar = await _http.PutAsync(Indice,
            new StringContent(MappingIndice, Encoding.UTF8, "application/json"));
        criar.EnsureSuccessStatusCode();
    }
}