namespace Pso2MapGen.Core;

/// <summary>
/// Os nomes das areas, lidos de um `models.txt` **a' mao**.  So' servem para se ver o que se
/// esta' a escolher: o que a ferramenta usa, guarda e exporta e' sempre o numero.
///
/// <para>O ficheiro procura-se ao lado do executavel e, nao estando la', a subir ate' a' raiz
/// do `extracted/`.  Uma linha por modelo, com `#` ou `//` a comentar:</para>
///
/// <code>
/// 330 = Ruins
/// 311 : Forest 1
/// 310   Forest 2
/// </code>
///
/// <para>O separador pode ser `=`, `:`, `-`, uma virgula, um tab ou espacos.  Um id que nao
/// esteja no ficheiro aparece so' com o numero, e um ficheiro que nao exista nao e' erro
/// nenhum.</para>
/// </summary>
public static class ModelNames
{
    public const string Ficheiro = "models.txt";

    private static Dictionary<int, string>? _nomes;

    private static string _caminho = "";

    /// <summary>Onde o ficheiro foi encontrado, ou onde se procurou.  Para a GUI o dizer.</summary>
    public static string Caminho { get { _ = Todos; return _caminho; } }

    public static IReadOnlyDictionary<int, string> Todos => _nomes ??= Le();

    /// <summary>O nome do modelo, ou vazio se ninguem lho deu.</summary>
    public static string Nome(int id) => Todos.TryGetValue(id, out var n) ? n : "";

    /// <summary>`330 - Ruins`, ou so' `330` quando nao ha nome.</summary>
    public static string Etiqueta(int id)
    {
        string n = Nome(id);
        return n.Length > 0 ? $"{id} - {n}" : id.ToString();
    }

    /// <summary>
    /// O numero que uma etiqueta traz a' frente.  E' o que le' de volta o que a caixa mostra,
    /// e aceita tanto o `330 - Ruins` que vem da lista como um `330` escrito a' mao.
    /// </summary>
    public static int Id(string? etiqueta, int omissao = 0)
    {
        if (string.IsNullOrWhiteSpace(etiqueta))
            return omissao;
        string s = etiqueta.TrimStart();
        int i = 0;
        if (i < s.Length && (s[i] == '-' || s[i] == '+'))
            i++;
        int inicio = i;
        while (i < s.Length && char.IsDigit(s[i]))
            i++;
        return i > inicio && int.TryParse(s[..i], out int v) ? v : omissao;
    }

    /// <summary>Volta a ler o ficheiro (a GUI chama isto quando o utilizador o pede).</summary>
    public static void Recarrega() => _nomes = Le();

    private static Dictionary<int, string> Le()
    {
        var fora = new Dictionary<int, string>();
        string? f = Procura();
        _caminho = f ?? Path.Combine(AppContext.BaseDirectory, Ficheiro);
        if (f is null)
            return fora;
        foreach (string bruto in File.ReadLines(f))
        {
            string linha = bruto.Trim();
            int c = linha.IndexOf('#');
            if (c >= 0)
                linha = linha[..c].Trim();
            c = linha.IndexOf("//", StringComparison.Ordinal);
            if (c >= 0)
                linha = linha[..c].Trim();
            if (linha.Length == 0)
                continue;
            int i = 0;
            while (i < linha.Length && char.IsDigit(linha[i]))
                i++;
            if (i == 0 || !int.TryParse(linha[..i], out int id))
                continue;
            string nome = linha[i..].TrimStart(' ', '\t', '=', ':', '-', ',', '|').Trim();
            if (nome.Length > 0)
                fora[id] = nome;
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
