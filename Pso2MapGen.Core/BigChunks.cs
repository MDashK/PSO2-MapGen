namespace Pso2MapGen.Core;

/// <summary>
/// Os big chunks do packet: o nome que o <c>UnkP</c>/<c>UnkW</c> codifica e as variantes de
/// modelo que o <c>UnkQ</c>/<c>UnkY</c> pode escolher.
///
/// <para>O nome sao <b>dois caracteres ASCII little-endian</b> — <c>13425 = 0x3471 = "q4"</c>
/// (FINDINGS 5.2) — e e' so' ele que conta para a geracao: a pool do <c>0x00640800</c> junta os
/// chunks pelo <b>nome</b>, por isso dois designs com o mesmo nome e variantes diferentes sao um
/// chunk so' (13.3). A variante decide o <c>.ice</c> que o cliente carrega, ou seja o aspecto da
/// peca, e nao onde ela vai parar.</para>
///
/// <para>As variantes que existem estao no <c>bigchunks.txt</c>, ao lado do executavel, no mesmo
/// espirito do <see cref="ModelNames"/>: uma linha por <c>modelo chunk = n, n</c>, editavel a'
/// mao. A lista que vai no pacote foi medida contra os ficheiros do cliente.</para>
/// </summary>
public static class BigChunks
{
    public const string Ficheiro = "bigchunks.txt";

    private static Dictionary<(int Modelo, string Chunk), List<int>>? _tabela;

    private static string _caminho = "";

    /// <summary>Onde a tabela foi encontrada.  Le'-la se' ainda nao o foi.</summary>
    public static string Caminho { get { _ = Tabela; return _caminho; } }

    private static Dictionary<(int, string), List<int>> Tabela => _tabela ??= Le();

    /// <summary>O nome de duas letras que este valor codifica; vazio quando e' 0.</summary>
    public static string Nome(int valor)
    {
        if (valor == 0)
            return "";
        char a = (char)(valor & 0xFF), b = (char)((valor >> 8) & 0xFF);
        return char.IsLetterOrDigit(a) && char.IsLetterOrDigit(b) ? $"{a}{b}" : "";
    }

    /// <summary>O valor que codifica um nome de duas letras.</summary>
    public static int Valor(string nome)
        => nome.Length == 2 ? nome[0] | (nome[1] << 8) : 0;

    /// <summary>`13425 - q4`, ou `0 - (none)`.</summary>
    public static string Etiqueta(int valor)
    {
        if (valor == 0)
            return "0 - (none)";
        string n = Nome(valor);
        return n.Length > 0 ? $"{valor} - {n}" : valor.ToString();
    }

    /// <summary>As variantes que existem para este chunk desta area.</summary>
    public static IReadOnlyList<int> Variantes(int modelo, string chunk)
        => chunk.Length > 0 && Tabela.TryGetValue((modelo, chunk), out var v)
           ? v : Array.Empty<int>();

    /// <summary>As variantes do chunk que este <c>UnkP</c>/<c>UnkW</c> nomeia.</summary>
    public static IReadOnlyList<int> VariantesDe(int modelo, int valor)
        => Variantes(modelo, Nome(valor));

    public static void Recarrega() => _tabela = Le();

    private static Dictionary<(int, string), List<int>> Le()
    {
        var fora = new Dictionary<(int, string), List<int>>();
        string? f = Procura();
        _caminho = f ?? Path.Combine(AppContext.BaseDirectory, Ficheiro);
        if (f is null)
            return fora;
        foreach (string bruto in File.ReadLines(f))
        {
            string linha = bruto;
            int c = linha.IndexOf('#');
            if (c >= 0)
                linha = linha[..c];
            linha = linha.Trim();
            if (linha.Length == 0)
                continue;
            // <modelo> <chunk> = <n>[, <n>...]
            int igual = linha.IndexOf('=');
            if (igual < 0)
                continue;
            var cabeca = linha[..igual].Split(new[] { ' ', '\t' },
                                              StringSplitOptions.RemoveEmptyEntries);
            if (cabeca.Length < 2 || !int.TryParse(cabeca[0], out int modelo))
                continue;
            var ns = new List<int>();
            foreach (string p in linha[(igual + 1)..].Split(',', ' ', '\t'))
                if (int.TryParse(p.Trim(), out int n) && !ns.Contains(n))
                    ns.Add(n);
            if (ns.Count > 0)
                fora[(modelo, cabeca[1])] = ns;
        }
        return fora;
    }

    private static string? Procura()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            string cand = Path.Combine(dir, Ficheiro);
            if (File.Exists(cand))
                return cand;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        string naRaiz = Path.Combine(GameData.Root, Ficheiro);
        return File.Exists(naRaiz) ? naRaiz : null;
    }
}
