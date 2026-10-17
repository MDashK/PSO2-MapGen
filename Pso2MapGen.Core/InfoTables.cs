namespace Pso2MapGen.Core;

/// <summary>
/// O conteudo habitual das <c>InfoTable1</c>/<c>2</c>/<c>3</c> de cada area, lido do
/// <c>infotables.txt</c> ao lado do executavel.
///
/// <para>Uma zona que veio de um packet traz as suas tabelas e exporta-as tal e qual.  Uma zona
/// feita a' mao nao tem de onde as tirar, e escrever tudo a zeros obriga a corrigir o
/// `block.json` depois — estes campos nao mexem no mapa mas mexem no que se ve' la' dentro.  E'
/// para esse caso que esta tabela existe.</para>
///
/// <para>A chave e' <c>modelo unkB zona</c> e a procura cai para <c>modelo unkB</c> e depois
/// para <c>modelo</c>, por isso apagar as linhas mais finas so' torna a resposta mais grosseira.
/// Medida nos 583 packets, a forma dominante cobre 94% das zonas na InfoTable2 e 86% na
/// InfoTable1 — e' um ponto de partida, nao uma regra.</para>
/// </summary>
public static class InfoTables
{
    public const string Ficheiro = "infotables.txt";

    /// <summary>modelo -> unkB -> zona -> tabela (1, 2, 3) -> campos.</summary>
    private static Dictionary<(int Modelo, int UnkB, int Zona), Dictionary<int, List<InfoField>>>? _t;

    /// <summary>A linha `x =`: os campos do <c>ZoneSettingsExtra</c> daquela area.</summary>
    private static Dictionary<(int Modelo, int UnkB, int Zona), Dictionary<string, int>>? _x;

    private static string _caminho = "";

    public static string Caminho { get { _ = Tabela; return _caminho; } }

    private static Dictionary<(int Modelo, int UnkB, int Zona),
                              Dictionary<int, List<InfoField>>> Tabela
    {
        get
        {
            if (_t is null)
                Le();
            return _t!;
        }
    }

    private static Dictionary<(int Modelo, int UnkB, int Zona), Dictionary<string, int>> Extras
    {
        get
        {
            if (_x is null)
                Le();
            return _x!;
        }
    }

    public static void Recarrega() => Le();

    /// <summary>
    /// Os campos do <c>ZoneSettingsExtra</c> que aquela area costuma ter — o <c>UnkAC</c>, o
    /// <c>UnkO</c>, os <c>UnkK</c>..<c>UnkU</c>, o <c>UnkN</c> e os `Raw*` sem nome.  Serve as
    /// zonas feitas a' mao, que os escreviam todos a zero; uma zona vinda de um packet usa os
    /// dela.  Mesma cadeia de procura das tabelas.
    /// </summary>
    public static Dictionary<string, int>? Extra(int modelo, int unkB, int zona)
    {
        if (Extras.TryGetValue((modelo, unkB, zona), out var exacta))
            return exacta;
        foreach (var (k, v) in Extras)
            if (k.Modelo == modelo && k.UnkB == unkB)
                return v;
        foreach (var (k, v) in Extras)
            if (k.Modelo == modelo)
                return v;
        return null;
    }

    /// <summary>Um campo do <c>ZoneSettingsExtra</c> daquela area, ou o valor de repouso dele.</summary>
    public static int ExtraCampo(int modelo, int unkB, int zona, string nome, int omissao)
        => Extra(modelo, unkB, zona) is { } d && d.TryGetValue(nome, out int v) ? v : omissao;

    /// <summary>
    /// Os campos daquela tabela para aquela zona, ou <c>null</c> quando nao ha nada que sirva.
    /// Tenta <c>(modelo, unkB, zona)</c>, depois a primeira entrada de <c>(modelo, unkB)</c> e
    /// por fim a primeira de <c>(modelo)</c>.
    /// </summary>
    public static List<InfoField>? Campos(int modelo, int unkB, int zona, int tabela)
    {
        if (Tabela.TryGetValue((modelo, unkB, zona), out var exacta)
            && exacta.TryGetValue(tabela, out var c))
            return c;
        foreach (var (k, v) in Tabela)
            if (k.Modelo == modelo && k.UnkB == unkB && v.TryGetValue(tabela, out var c2))
                return c2;
        foreach (var (k, v) in Tabela)
            if (k.Modelo == modelo && v.TryGetValue(tabela, out var c3))
                return c3;
        return null;
    }

    private static void Le()
    {
        var fora = new Dictionary<(int Modelo, int UnkB, int Zona),
                                  Dictionary<int, List<InfoField>>>();
        var extras = new Dictionary<(int Modelo, int UnkB, int Zona), Dictionary<string, int>>();
        _t = fora;
        _x = extras;
        string? f = Procura();
        _caminho = f ?? Path.Combine(AppContext.BaseDirectory, Ficheiro);
        if (f is null)
            return;

        (int, int, int)? chave = null;
        foreach (string bruto in File.ReadLines(f))
        {
            string linha = bruto;
            int h = linha.IndexOf('#');
            if (h >= 0)
                linha = linha[..h];
            if (linha.Trim().Length == 0)
                continue;
            bool recuada = char.IsWhiteSpace(bruto[0]);
            if (!recuada)
            {
                // `<modelo> <unkB> <zona>`
                var p = linha.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                chave = p.Length >= 3 && int.TryParse(p[0], out int m)
                        && int.TryParse(p[1], out int b) && int.TryParse(p[2], out int z)
                        ? (m, b, z) : null;
                continue;
            }
            if (chave is null)
                continue;
            // `t2 = 12:100000, 13:30000, ...`
            int igual = linha.IndexOf('=');
            if (igual < 0)
                continue;
            string nome = linha[..igual].Trim();
            // `x = UnkO=256, UnkN=4119, RawE0=0, ...` — os campos do ZoneSettingsExtra
            if (nome == "x")
            {
                var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string parte in linha[(igual + 1)..].Split(','))
                {
                    var kv = parte.Split('=', 2);
                    if (kv.Length == 2 && int.TryParse(kv[1].Trim(), out int n))
                        d[kv[0].Trim()] = n;
                }
                if (d.Count > 0)
                    extras[chave.Value] = d;
                continue;
            }
            if (nome.Length < 2 || nome[0] != 't' || !int.TryParse(nome[1..], out int qual))
                continue;
            var campos = new List<InfoField>();
            foreach (string parte in linha[(igual + 1)..].Split(','))
            {
                string p = parte.Trim();
                if (p.Length == 0)
                    continue;
                int dp = p.IndexOf(':');
                if (dp < 0 || !int.TryParse(p[..dp].Trim(), out int id))
                    continue;
                var campo = new InfoField { Id = id };
                var vs = p[(dp + 1)..].Split('/');
                for (int i = 0; i < 10; i++)
                {
                    string v = vs.Length == 1 ? vs[0] : (i < vs.Length ? vs[i] : "0");
                    campo.Values[i] = int.TryParse(v.Trim(), out int n) ? n : 0;
                }
                campos.Add(campo);
            }
            if (campos.Count > 0)
            {
                if (!fora.TryGetValue(chave.Value, out var d))
                    fora[chave.Value] = d = new Dictionary<int, List<InfoField>>();
                d[qual] = campos;
            }
        }
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
