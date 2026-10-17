namespace Pso2MapGen.Core;

/// <summary>Uma porta de uma variante rodada: celula relativa a' ancora, direccao, e se sobe um nivel.</summary>
public readonly record struct ChunkDoor(int Cx, int Cy, int D, int Degrau);

/// <summary>Uma das quatro rotacoes pre-calculadas de uma peca.</summary>
public sealed class RotVariant
{
    public int Cols { get; }
    public int Rows { get; }
    public uint[] Info { get; }
    /// <summary>Indice linear onde a celula 0 do original foi parar.</summary>
    public int Anchor { get; }
    public ChunkDoor[] Doors { get; }

    public RotVariant(int cols, int rows, uint[] info, int anchor)
    {
        Cols = cols;
        Rows = rows;
        Info = info;
        Anchor = anchor;
        int ax = anchor % cols, ay = anchor / cols;
        var portas = new List<ChunkDoor>();
        for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
            {
                uint v = info[cy * cols + cx];
                for (int d = 0; d < 4; d++)
                    if ((((v & 0xF) >> d) & 1) != 0)
                        portas.Add(new ChunkDoor(cx - ax, cy - ay, d, (int)((v >> (16 + d)) & 1)));
            }
        Doors = portas.ToArray();
    }
}

/// <summary>Uma peca do catalogo, com as quatro rotacoes ja' montadas.</summary>
public sealed class Chunk
{
    public string Short { get; }
    public string Full { get; }
    public int Group { get; }
    public int Cols { get; }
    public int Rows { get; }
    public uint[] BaseInfo { get; }

    /// <summary>
    /// O <c>maxIce</c> e o <c>maxOb</c> do `.lps`: quantas variantes **ICE** e **<c>_ob0*</c>** a
    /// peca declara.  Sao o que decide se a celula recebe variante (<c>maxIce &gt;= 1</c> para o
    /// ICE, <c>maxOb &gt; 1</c> para o <c>_ob0*</c>) — ver
    /// <see cref="MapGen.PasseVariantes"/>.  Atencao: para o **valor** do ICE o
    /// <c>MaxIce</c> da' o <b>comprimento da lista de candidatos</b>, e o modulo do sorteio e' o
    /// comprimento da lista **depois** de a regra do <c>i*</c>/<c>l*</c> lhe tirar o que tirar.
    /// </summary>
    public int MaxIce { get; }
    public int MaxOb { get; }

    /// <summary>
    /// As outras DUAS contagens de variante do `.lps` — o parse do colega chama-lhes `Zero` e
    /// `Multi80`.  O <see cref="Multi80"/> e' a contagem das variantes da gama dos <c>80</c>, e
    /// <b>bate exactamente</b> com o numero de
    /// <c>stage/sn_&lt;mod&gt;/ln_&lt;mod&gt;_&lt;chunk&gt;_8N.ice</c> que existem no cliente: 45 de
    /// 45 chunks de varias celulas, zero excepcoes.  E' o que o
    /// <see cref="MapGen.PasseVariantes"/> usa nesses chunks (FINDINGS 47.12).
    /// </summary>
    public int Zero { get; }
    public int Multi80 { get; }

    /// <summary>
    /// O <c>door</c> do `.lps`, um por sub-celula.  Nao entra na geracao — distingue as pecas
    /// que tem a mesma forma mas papel diferente (`n0` = 0, `g*` = 1, `s*` = 2), e e' o que
    /// desempata a familia de reskin do pos-processamento (41.18).
    /// </summary>
    public uint[] Door { get; }
    public RotVariant[] Rots { get; } = new RotVariant[4];

    public Chunk(LpsChunk rec)
    {
        Short = rec.Short;
        Full = rec.Full;
        Group = rec.Group;
        Cols = rec.Cols;
        Rows = rec.Rows;
        BaseInfo = rec.Info;
        MaxIce = rec.MaxIce;
        MaxOb = rec.MaxOb;
        Zero = rec.Unk8;
        Multi80 = rec.Unk10;
        Door = rec.Door;
        for (int r = 0; r < 4; r++)
        {
            (int nc, int nr) = r % 2 == 0 ? (Cols, Rows) : (Rows, Cols);
            var info = new uint[nc * nr];
            int anchor = 0;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    var (nx, ny) = ChunkMath.RotCell(x, y, Cols, Rows, r);
                    info[ny * nc + nx] = ChunkMath.RotInfo(BaseInfo[y * Cols + x], r);
                    if (y * Cols + x == 0)
                        anchor = ny * nc + nx;
                }
            Rots[r] = new RotVariant(nc, nr, info, anchor);
        }
    }

    public override string ToString() => $"<Chunk {Short} g{Group} {Cols}x{Rows}>";
}

public static class ChunkMath
{
    /// <summary>Uma porta no lado d passa para o lado (d - r) mod 4.</summary>
    public static uint RotPaths(uint p, int r)
    {
        r &= 3;
        return ((p >> r) | (p << (4 - r))) & 0xF;
    }

    /// <summary>
    /// Roda o dword de info.  O byte 0 sao as portas e o <b>byte 2 sao os degraus</b>, com o
    /// mesmo layout por direccao (o 0x00648830 le (byte2 shr d) and 1), por isso roda tambem.
    /// Sem isto o `ia` (0x10005: corredor N-S que sobe a norte) mantinha o degrau a norte em
    /// todas as rotacoes, e o alvo seguinte saia com a altura errada — media-se no
    /// `volcanic` b2, onde o cliente diz alvo=(6 3 h6 d0) e o modelo dizia h5.
    /// </summary>
    public static uint RotInfo(uint v, int r)
    {
        uint baixo = RotPaths(v & 0xF, r);
        uint degrau = RotPaths((v >> 16) & 0xF, r);
        return (v & 0xFFF0FFF0u) | baixo | (degrau << 16);
    }

    /// <summary>Onde a celula (x,y) de uma peca cols x rows vai parar depois de rodar r.</summary>
    public static (int X, int Y) RotCell(int x, int y, int cols, int rows, int r) => (r & 3) switch
    {
        0 => (x, y),
        1 => (y, cols - 1 - x),
        2 => (cols - 1 - x, rows - 1 - y),
        _ => (rows - 1 - y, x),
    };
}

/// <summary>
/// O catalogo de uma area: o que o cliente monta uma vez por `.lps`.
/// Porto directo de tools/chunks.py.
/// </summary>
public sealed class Area
{
    public int Model { get; }
    public LpsFile Lps { get; }
    public Dictionary<string, Chunk> Chunks { get; } = new();

    /// <summary>
    /// As mesmas pecas por <b>ordem de insercao</b>, que e' a do `.lps`.  O `dict` do Python
    /// preserva essa ordem e o <c>peca_1x1</c> depende dela (e' o que faz o `i0` do corredor
    /// sair com a rotacao da marcha), por isso um <c>Dictionary</c> sozinho nao servia.
    /// </summary>
    public List<Chunk> ChunkList { get; } = new();

    public Dictionary<int, List<Chunk>> ByGroup { get; } = new();

    // os "designs" (nomes comecados por `q`) separados do resto, por grupo, tal como
    // estavam antes de qualquer SetBigChunks — a filtragem aplica-se sempre ao grupo
    // inteiro, nunca ao resultado de uma chamada anterior.
    private readonly Dictionary<int, List<Chunk>> _desenhos = new();
    private readonly Dictionary<int, List<Chunk>> _semDesenhos = new();

    public Area(int model, LpsFile lps)
    {
        Model = model;
        Lps = lps;
        foreach (var rec in lps.Chunks)
        {
            var c = new Chunk(rec);
            if (Chunks.ContainsKey(c.Short))
                ChunkList[ChunkList.FindIndex(k => k.Short == c.Short)] = c;
            else
                ChunkList.Add(c);
            Chunks[c.Short] = c;
            if (!ByGroup.TryGetValue(c.Group, out var lista))
                ByGroup[c.Group] = lista = new List<Chunk>();
            lista.Add(c);
        }
        // Um "design" e' qualquer chunk cujo nome comeca por `q`, esteja no grupo que
        // estiver.  Normalmente e' o grupo 11, mas o `skyscape` (sn_0520 e sn_0521) tem o
        // `qg` no grupo **3** — e o cliente trata-o do mesmo modo: so' entra na piscina se
        // o packet o declarar (FINDINGS 26.4).
        foreach (var (g, cs) in ByGroup)
        {
            _desenhos[g] = cs.Where(c => c.Short.StartsWith('q')).ToList();
            _semDesenhos[g] = cs.Where(c => !c.Short.StartsWith('q')).ToList();
        }
    }

    public float Param(string nome, float omissao)
        => Lps.ParamsA.TryGetValue(nome, out var v) && !v.IsText ? v.F : omissao;

    public int MaxHeight => (int)Param("max_height", 10);
    public int MaxHdiff => (int)Param("max_hdiff", 3);

    /// <summary>
    /// gen-&gt;0x74: a area usa variacao vertical.  Medido no cliente (sonda A, FINDINGS
    /// 10.7) em cinco areas: e' exactamente "a area tem pecas do grupo 3" (as rampas
    /// `ia`/`la`/`lb`), e <b>nao</b> "o max_height esta' nos paramsA".  O Sanctum (530)
    /// decide: max_height = 10, max_hdiff = 7, sem grupo 3, e o cliente diz gen-&gt;0x74 = 0.
    /// </summary>
    public bool HasHeight => ByGroup.ContainsKey(3);

    /// <summary>
    /// Grupo 11 = os nomes distintos que o packet declara no UnkP e no UnkW.
    ///
    /// O packet traz <b>dois</b> designs de big chunk, UnkP/UnkQ e UnkW/UnkY: a primeira
    /// palavra de cada par e' o nome curto em ASCII little-endian, a segunda escolhe a
    /// variante ICE.  Qualquer dos pares pode ser zero.  Medido sobre 1641 zonas de packets
    /// reais: <b>0 designs em 1035 zonas</b>, 1 em 214, 2 em 392 — a maioria das zonas nao
    /// tem big chunk nenhum na piscina.  O mesmo nome pode aparecer nos dois pares com
    /// variantes diferentes (q5/80 e q5/81), e para a piscina isso e' uma peca, nao duas.
    /// </summary>
    public List<Chunk> SetBigChunks(IEnumerable<string> nomes)
    {
        var querer = nomes.Where(n => !string.IsNullOrEmpty(n)).ToHashSet();
        foreach (var (g, cs) in _desenhos)
            if (cs.Count > 0)
                ByGroup[g] = _semDesenhos[g].Concat(cs.Where(c => querer.Contains(c.Short))).ToList();
        return ByGroup.TryGetValue(11, out var g11) ? g11 : new List<Chunk>();
    }

    /// <summary>0x006355B0: concatena os baldes cujo bit de grupo esta' posto, por ordem de grupo.</summary>
    public List<Chunk> GroupList(int mask)
    {
        var o = new List<Chunk>();
        for (int g = 0; g < 16; g++)
            if ((mask & (1 << g)) != 0 && ByGroup.TryGetValue(g, out var cs))
                o.AddRange(cs);
        return o;
    }

    /// <summary>
    /// Descodifica as duas palavras de design do packet em nomes curtos.  Cada uma e' o nome
    /// de 2 caracteres em ASCII little-endian: 13681 = 0x3571 = "q5".  Zero quer dizer "este
    /// par nao esta' definido".
    /// </summary>
    public static List<string> BigChunkNames(int unkP, int unkW)
    {
        var o = new List<string>();
        foreach (int v in new[] { unkP, unkW })
        {
            if (v == 0)
                continue;
            string nome = new string(new[] { (char)(v & 0xFF), (char)((v >> 8) & 0xFF) });
            if (!o.Contains(nome))
                o.Add(nome);
        }
        return o;
    }
}

/// <summary>Onde estao os dados extraidos do jogo, e a cache das areas.</summary>
public static class GameData
{
    /// <summary>A raiz do extracted/.  A GUI poe isto a apontar para onde o utilizador tiver os dados.</summary>
    public static string Root
    {
        get => _root;
        set { _root = value; LimpaCache(); }
    }

    private static string _root = DescobreRaiz();

    public static string LpsDir => Path.Combine(Root, "landform_common");
    public static string LatDir => Path.Combine(Root, "lat");

    private static readonly Dictionary<int, Area> _cache = new();

    private static string DescobreRaiz()
    {
        // sobe a partir do executavel a' procura de um extracted/landform_common
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            string cand = Path.Combine(dir, "extracted");
            if (Directory.Exists(Path.Combine(cand, "landform_common")))
                return cand;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return Path.Combine(AppContext.BaseDirectory, "extracted");
    }

    public static string LpsPath(int model) => Path.Combine(LpsDir, $"sn_{model:D4}.lps");

    public static string LatPath(int model, int arguments, bool river = false)
        => Path.Combine(LatDir, $"{model:D4}",
                        river ? $"sn_{model:D4}_river{arguments:D4}.lat"
                              : $"sn_{model:D4}_{arguments:D4}.lat");

    public static Area GetArea(int model)
    {
        if (!_cache.TryGetValue(model, out var a))
            _cache[model] = a = new Area(model, LpsFile.Load(LpsPath(model)));
        return a;
    }

    /// <summary>Esquece as areas em cache — a filtragem de big chunks e' feita em sitio.</summary>
    public static void LimpaCache() => _cache.Clear();

    public static IEnumerable<int> AvailableModels()
        => Directory.Exists(LpsDir)
            ? Directory.GetFiles(LpsDir, "sn_*.lps")
                       .Select(f => int.TryParse(Path.GetFileNameWithoutExtension(f)[3..], out int m) ? m : -1)
                       .Where(m => m >= 0).OrderBy(m => m)
            : Enumerable.Empty<int>();
}
