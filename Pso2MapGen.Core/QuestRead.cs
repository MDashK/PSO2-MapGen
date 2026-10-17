using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pso2MapGen.Core;

/// <summary>Uma variante de uma quest: a pasta `variant/N`.</summary>
public sealed class Variante
{
    public string Nome = "";      // "0", "7", "disabled"
    public string Pasta = "";
    /// <summary>Quantos dos blocos que a quest pede e' que estao mesmo la'.</summary>
    public int Blocos;
    /// <summary>Esta' arrumada dentro do `variant/disabled` — o servidor nao a sorteia.</summary>
    public bool Desligada;

    public override string ToString()
        => Blocos == 0 ? $"variant {Nome}  (empty)"
         : Desligada ? $"variant {Nome}  ({Blocos} block(s), switched off)"
         : $"variant {Nome}  ({Blocos} block(s))";
}

/// <summary>
/// O `quest.json` do Starlight, lido para se poder abrir uma quest inteira em vez de um bloco.
///
/// <para>Abrir por aqui <b>nao e' o mesmo</b> que abrir a pasta de uma variante.  A quest diz
/// que blocos e' que carrega, e isso nem sempre e' o que esta' no disco: o `dps_rockbear` tem
/// tres pastas e carrega uma, o `Mother_test` tem `b1`, `b2` e `b4` e carrega os dois primeiros,
/// e o `ex_tunnels` tem um `.zip` no meio dos blocos.  Como a lista de entradas e' repartida por
/// <b>posicao</b>, carregar um bloco a mais desalinha-a.</para>
/// </summary>
public sealed class Quest
{
    /// <summary>O `quest.json`.</summary>
    public string Ficheiro = "";
    /// <summary>O nome da quest — normalmente igual ao da pasta.</summary>
    public string Nome = "";
    /// <summary>Os blocos que a quest carrega, por ordem: `b1`, `b2`, `b3`.</summary>
    public List<string> Blocos = new();
    /// <summary>As variantes que estao no disco, pela ordem natural do numero.</summary>
    public List<Variante> Variantes = new();

    public string Pasta => Path.GetDirectoryName(Path.GetFullPath(Ficheiro)) ?? "";

    /// <summary>
    /// Isto parece um `quest.json`?  Distingue-se de um `block.json` por ter <c>Blocks</c> e nao
    /// ter <c>ZoneIndex</c> — e' o suficiente para o botao de abrir escolher sozinho.
    /// </summary>
    public static bool Parece(string caminho)
    {
        if (!File.Exists(caminho)) return false;
        if (Path.GetFileName(caminho).Equals("block.json", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var o = JsonNode.Parse(File.ReadAllText(caminho), null, Solto)?.AsObject();
            return o is not null && o["Blocks"] is not null && o["ZoneIndex"] is null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Quest Le(string caminho)
    {
        var o = JsonNode.Parse(File.ReadAllText(caminho), null, Solto)?.AsObject()
                ?? throw new InvalidDataException("that quest.json is empty");

        var q = new Quest
        {
            Ficheiro = Path.GetFullPath(caminho),
            Nome = o["Name"]?.GetValue<string>()
                   ?? Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(caminho))) ?? "",
        };

        if (o["Blocks"] is JsonArray blocos)
            foreach (var b in blocos)
            {
                string? s = b?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(s)) q.Blocos.Add(s.Trim());
            }
        if (q.Blocos.Count == 0)
            throw new InvalidDataException("that quest.json lists no blocks");

        string raizDasVariantes = Path.Combine(q.Pasta, "variant");
        if (Directory.Exists(raizDasVariantes))
            foreach (string d in Directory.GetDirectories(raizDasVariantes)
                                          .OrderBy(Path.GetFileName, BlockRead.Natural.Ordem))
            {
                string nome = Path.GetFileName(d) ?? "";
                var v = new Variante { Nome = nome, Pasta = d, Blocos = q.Conta(d) };

                // o `variant/disabled` nao e' uma variante: e' onde as variantes desligadas ficam
                // arrumadas, cada uma com o seu numero.  Mostram-se como `disabled/0`, porque
                // poder ver uma que esta' de castigo e' meia razao para ela la' estar
                if (v.Blocos == 0 && nome.Equals("disabled", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string p in Directory.GetDirectories(d)
                                                  .OrderBy(Path.GetFileName, BlockRead.Natural.Ordem))
                    {
                        int n = q.Conta(p);
                        if (n > 0)
                            q.Variantes.Add(new Variante
                            {
                                Nome = $"{nome}/{Path.GetFileName(p)}", Pasta = p, Blocos = n,
                                Desligada = true,
                            });
                    }
                    continue;
                }
                q.Variantes.Add(v);
            }

        return q;
    }

    /// <summary>Quantos dos blocos que esta quest pede estao naquela pasta.</summary>
    private int Conta(string pasta)
        => Blocos.Count(b => File.Exists(Path.Combine(pasta, b, "block.json")));

    /// <summary>
    /// Os `block.json` de uma variante, <b>pela ordem que a quest pede</b>.  Um bloco que a
    /// quest peca e nao esteja la' e' saltado, e isso e' dito em <paramref name="faltam"/> — nao
    /// se inventa um lugar vazio, porque quem manda na reparticao das entradas e' a lista da
    /// propria quest.
    /// </summary>
    public List<string> Ficheiros(Variante v, out List<string> faltam)
    {
        var lista = new List<string>();
        faltam = new List<string>();
        foreach (string b in Blocos)
        {
            string f = Path.Combine(v.Pasta, b, "block.json");
            if (File.Exists(f)) lista.Add(f);
            else faltam.Add(b);
        }
        return lista;
    }

    /// <summary>As zonas de uma variante, prontas para a pre-visualizacao.</summary>
    public List<ZoneInput> Zonas(Variante v, out List<string> faltam)
        => BlockRead.Le(Ficheiros(v, out faltam), v.Pasta);

    private static readonly JsonDocumentOptions Solto = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
