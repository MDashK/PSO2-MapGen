namespace Pso2MapGen.Core;

/// <summary>
/// Os nomes dos campos no `block.json`, num sitio so'.
///
/// <para>A Starlight deixou de usar os nomes `Unk*`: cada campo passou a chamar-se pelo que faz
/// quando se sabe, e pelo <b>deslocamento</b> dele no registo quando nao se sabe.  Esta tabela e'
/// o contrato entre as duas pontas — a ferramenta escreve os nomes de agora, e le' tanto esses
/// como os antigos, para os ficheiros que ainda nao foram convertidos continuarem a abrir.</para>
///
/// <para>Um aviso sobre o <c>Unknown04</c>: e' o <c>+0x04</c> do <b>ZoneSettings</b> (era o
/// `UnkA`).  O <c>+0x04</c> do registo <c>ZoneSettingsExtra</c> e' outro campo e chama-se
/// <c>ExtraUnknown04</c>.  Os restantes <c>Unknown&lt;off&gt;</c> sao todos do registo Extra.</para>
/// </summary>
public static class Chaves
{
    /// <summary>O nome de agora e o que o campo tinha antes; vazio quando nunca mudou.</summary>
    public static readonly (string Agora, string Antes)[] Todas =
    {
        // --- ZoneSettings
        ("ZoneIndex",        ""),
        ("Unknown04",        "UnkA"),
        ("ModelId",          ""),
        ("ZoneType",         ""),
        ("Seed",             ""),
        ("LatNumber",        "Arguments"),
        ("MapWidth",         "SizeX"),
        ("MapHeight",        "SizeY"),
        ("MapType",          "UnkB"),
        ("AreaIndex",        ""),
        ("SubArea",          ""),
        ("MapConfig",        "UnkC"),
        // --- as tres tabelas
        ("DecorationTable",  "InfoTable1"),
        ("SetFileTable",     "InfoTable2"),
        ("MapGenTable",      "InfoTable3"),
        // --- ZoneSettingsExtra
        ("Unknown14",        "UnkAC"),
        ("ExtraUnknown04",   "Raw04"),
        ("Unknown0C",        "Raw0C"),
        ("Unknown10",        "Raw10"),
        ("HeightMode",       ""),
        ("MultiPartyArea",   ""),
        ("BigChunk1",        "UnkP"),
        ("BigChunk1Variant", "UnkQ"),
        ("BigChunk2",        "UnkW"),
        ("BigChunk2Variant", "UnkY"),
        ("Unknown88",        "UnkK"),
        ("Unknown90",        "UnkL"),
        ("Unknown98",        "Raw98"),
        ("UnknownA0",        "UnkAA"),
        ("UnknownA8",        "UnkAB"),
        ("UnknownB0",        "UnkR"),
        ("UnknownB8",        "UnkS"),
        ("UnknownC0",        "UnkT"),
        ("UnknownC8",        "UnkM"),
        ("UnknownD0",        "UnkU"),
        ("UnknownD4",        "RawD4"),
        ("UnknownD8",        "RawD8"),
        ("UnknownDC",        "RawDC"),
        ("UnknownE0",        "RawE0"),
        ("ZoneFlags",        "UnkN"),
    };

    /// <summary>Os cinco valores da <c>MapGenTable</c>, pela ordem em que estao na linha.</summary>
    public static readonly (string Agora, string Antes)[] DaMapGen =
    {
        ("MapWidth",        "Unk1"),
        ("MapHeight",       "Unk2"),
        ("ExitMinDistance", "Unk3"),
        ("OpenBranchCap",   "Unk4"),
        ("ExtraPassFlag",   "Unk5"),
    };

    // Os dois niveis tem de ser separados: o `MapWidth` do topo era o `SizeX`, e o `MapWidth`
    // de dentro da MapGenTable era o `Unk1`.  Um dicionario so' nao consegue dizer os dois.
    private static readonly Dictionary<string, string> _noTopo =
        Todas.Where(p => p.Antes.Length > 0)
             .ToDictionary(p => p.Agora, p => p.Antes, StringComparer.Ordinal);

    private static readonly Dictionary<string, string> _naMapGen =
        DaMapGen.Where(p => p.Antes.Length > 0)
                .ToDictionary(p => p.Agora, p => p.Antes, StringComparer.Ordinal);

    /// <summary>O nome antigo de um campo do <b>topo</b> do ficheiro, ou null.</summary>
    public static string? Antigo(string agora) => _noTopo.TryGetValue(agora, out var v) ? v : null;

    /// <summary>O nome antigo de um campo <b>de dentro da MapGenTable</b>, ou null.</summary>
    public static string? AntigoNaMapGen(string agora)
        => _naMapGen.TryGetValue(agora, out var v) ? v : null;

    /// <summary>
    /// O nome antigo de um campo dado pelo caminho (<c>MapGenTable/MapWidth</c>), escolhendo o
    /// nivel certo.  Devolve null quando esse campo nunca teve outro nome.
    /// </summary>
    public static string? AntigoNoCaminho(string caminho, int parte)
    {
        var partes = caminho.Split('/');
        if (parte < 0 || parte >= partes.Length)
            return null;
        bool dentroDaTabela = parte > 0
            && (partes[parte - 1] == "MapGenTable" || partes[parte - 1] == "InfoTable3");
        return dentroDaTabela ? AntigoNaMapGen(partes[parte]) : Antigo(partes[parte]);
    }

    /// <summary>
    /// Um dword sem nome do registo Extra, nomeado pelo deslocamento: o <c>+0x1C</c> fica
    /// <c>Unknown1C</c>.  Sao os que estao a zero nas 1641 zonas medidas e que so' se escrevem
    /// quando fogem disso (ver BlockJson).
    /// </summary>
    public static string PorDeslocamento(int off) => $"Unknown{off:X2}";

    /// <summary>O nome antigo desses, que era `Raw&lt;deslocamento&gt;`.</summary>
    public static string PorDeslocamentoAntigo(int off) => $"Raw{off:X2}";
}
