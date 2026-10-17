using Pso2MapGen.Core;

// Arreio de verificacao do port.  Cada verbo despeja em texto o que o modulo
// correspondente do Python despeja, para se poder comparar linha a linha.
//
//     Pso2MapGen.Cli rng <seed> <count>
//     Pso2MapGen.Cli lps <model>
//     Pso2MapGen.Cli lat <model> <arguments> [--river]   |   lat <path.lat>
//     Pso2MapGen.Cli gen   --packet <bin> --zone N   |   --model N --seed N --sx N --sy N
//     Pso2MapGen.Cli show  ... (o mesmo, mas desenhado)
//     Pso2MapGen.Cli sweep <folder> [--limit N]
//     Pso2MapGen.Cli dump  <folder> [--limit N]
//     Pso2MapGen.Cli check <cases.json> [-v]
//     Pso2MapGen.Cli export --packet <bin> --zone N [--out <file>]
//     Pso2MapGen.Cli save   --block <block.json> [--zone N] [--dry-run]

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: Pso2MapGen.Cli <rng|lps|lat|models|new|gen|show|sweep|dump|check|quest|export|save> ...");
    return 1;
}

// a raiz dos dados pode vir por variavel de ambiente, para os testes
if (Environment.GetEnvironmentVariable("PSO2_EXTRACTED") is { Length: > 0 } raiz)
    GameData.Root = raiz;

return args[0] switch
{
    "rng" => Verbos.VerboRng(args),
    "lps" => Verbos.VerboLps(args),
    "lat" => Verbos.VerboLat(args),
    "models" => Verbos.VerboModelos(args),
    "bigchunks" => Verbos.VerboBigChunks(args),
    "height" => Verbos.VerboAltura(args),
    "gen" => VerbosGera.Gera(args),
    "show" => VerbosGera.Mostra(args),
    "sweep" => VerbosGera.Varre(args),
    "sets" => VerbosGera.Sets(args),
    "dump" => VerboDespejo.Despeja(args),
    "check" => VerboConfere.Confere(args),
    "quest" => VerboQuest.Corre(args),
    "export" => VerboExporta.Exporta(args),
    "save" => VerboGuarda.Guarda(args),
    "new" => VerboNovo.Novo(args),
    _ => Erro($"unknown verb: {args[0]}"),
};

static int Erro(string msg)
{
    Console.Error.WriteLine(msg);
    return 1;
}

static class Verbos
{
    public static int VerboRng(string[] args)
    {
        uint semente = args.Length > 1 ? uint.Parse(args[1]) : 1u;
        int quantos = args.Length > 2 ? int.Parse(args[2]) : 64;

        // 1. a sequencia em bruto
        var mt = new Mt19937(semente);
        for (int i = 0; i < quantos; i++)
            Console.WriteLine($"raw {i} {mt.NextU32()}");
        Console.WriteLine($"draws {mt.Draws}");

        // 2. o uniform_a, com um n que passa pelos tres ramos do calculo do divisor
        mt = new Mt19937(semente);
        foreach (int n in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 16, 17, 31, 32, 33,
                                  63, 64, 100, 255, 256, 1000, 65536, 100000, 0x40000000 })
            Console.WriteLine($"ua {n} {Rng.UniformA(mt, n)} {mt.Draws}");

        // 3. o uniform_b, incluindo os limites que tem ramo proprio
        mt = new Mt19937(semente);
        foreach (var (lo, hi) in new (long, long)[] { (0, 0), (0, 1), (0, 2), (0, 3), (0, 7),
                                                      (0, 8), (1, 6), (-4, 4), (0, 255),
                                                      (0, 65535), (10, 10), (0, 0xFFFFFFFEL),
                                                      (0, 0xFFFFFFFFL) })
            Console.WriteLine($"ub {lo} {hi} {Rng.UniformB(mt, lo, hi)} {mt.Draws}");

        // 4. o baralhar, que e' o que custa palavras a serio
        mt = new Mt19937(semente);
        foreach (int len in new[] { 1, 2, 3, 4, 5, 8, 13, 20 })
        {
            var seq = Enumerable.Range(0, len).ToList();
            Rng.Shuffle(mt, seq);
            Console.WriteLine($"sh {len} {string.Join(",", seq)} {mt.Draws}");
        }

        // 5. um baralhar parcial, que o gerador usa nas rampas
        mt = new Mt19937(semente);
        var p = Enumerable.Range(0, 10).ToList();
        Rng.Shuffle(mt, p, 2, 8);
        Console.WriteLine($"shp {string.Join(",", p)} {mt.Draws}");
        return 0;
    }

    public static int VerboLps(string[] args)
    {
        if (args.Length < 2)
            return Erro("usage: lps <model>");
        var a = GameData.GetArea(int.Parse(args[1]));
        Console.WriteLine($"area {a.Model} chunks={a.Lps.Chunks.Count} altura={(a.HasHeight ? 1 : 0)} " +
                          $"maxh={a.MaxHeight} hdiff={a.MaxHdiff}");
        foreach (var g in a.ByGroup.Keys.OrderBy(k => k))
            Console.WriteLine($"grupo {g} {string.Join(",", a.ByGroup[g].Select(c => c.Short))}");
        foreach (var rec in a.Lps.Chunks)
        {
            var c = a.Chunks[rec.Short];
            Console.WriteLine($"chunk {c.Short} {c.Full} g{c.Group} {c.Cols}x{c.Rows} " +
                              string.Join(",", c.BaseInfo.Select(v => v.ToString("x8"))));
            for (int r = 0; r < 4; r++)
            {
                var rv = c.Rots[r];
                Console.WriteLine($"  rot {r} {rv.Cols}x{rv.Rows} anc={rv.Anchor} " +
                                  string.Join(",", rv.Info.Select(v => v.ToString("x8"))));
                Console.WriteLine("  doors " + string.Join(" ",
                    rv.Doors.Select(d => $"{d.Cx}:{d.Cy}:{d.D}:{d.Degrau}")));
            }
        }
        return 0;
    }

    public static int VerboLat(string[] args)
    {
        if (args.Length < 2)
            return Erro("usage: lat <model> <arguments> [--river]   |   lat <path.lat>");
        // com um caminho le-se o ficheiro tal e qual; com modelo+arguments monta-se o nome
        string caminho;
        if (args[1].EndsWith(".lat", StringComparison.OrdinalIgnoreCase))
            caminho = args[1];
        else
            caminho = GameData.LatPath(int.Parse(args[1]), int.Parse(args[2]),
                                       args.Contains("--river"));
        var m = LatFile.Load(caminho);
        Console.WriteLine($"lat {m.FileName} a={m.A} b={m.B} w={m.W} h={m.H}");
        for (int i = 0; i < m.Cells.Count; i++)
        {
            var c = m.Cells[i];
            Console.WriteLine($"cell {i} {(c.Short.Length == 0 ? "-" : c.Short)} rot={c.Rot} " +
                              $"ice={(c.Ice?.ToString() ?? "-")} type={c.Type} door={c.Door} " +
                              $"last={c.Last} d0C={c.D0C} d10={c.D10} d14={c.D14} " +
                              $"info={m.Info[i]:x8}");
        }
        return 0;
    }

    /// <summary>
    ///     bigchunks [model]
    ///
    /// Os designs de big chunk de uma area e as variantes ICE que existem para cada um — a
    /// tabela do `bigchunks.txt`, que e' a que enche as caixas do UnkQ/UnkY na GUI.
    /// </summary>
    public static int VerboBigChunks(string[] args)
    {
        var modelos = args.Length > 1
            ? new[] { int.Parse(args[1]) }
            : GameData.AvailableModels().ToArray();
        Console.WriteLine($"table: {BigChunks.Caminho}");
        foreach (int m in modelos)
        {
            List<string> nomes = new();
            try
            {
                foreach (var nome in GameData.GetArea(m).Chunks.Keys)
                    if (nome.StartsWith("q", StringComparison.Ordinal))
                        nomes.Add(nome);
            }
            catch (Exception)
            {
                continue;
            }
            nomes.Sort(StringComparer.Ordinal);
            foreach (string nome in nomes)
            {
                var vs = BigChunks.Variantes(m, nome);
                Console.WriteLine($"{m} {nome} = {BigChunks.Valor(nome)} " +
                                  $"variants: {(vs.Count > 0 ? string.Join(", ", vs) : "-")}");
            }
        }
        return 0;
    }

    /// <summary>
    ///     height
    ///
    /// As areas **com altura** — as unicas onde o `gen->0x94` faz alguma coisa.  Uma area tem
    /// altura quando o seu `.lps` tem chunks do **grupo 3** (as rampas `ia`/`la`/`lb`), que e' o
    /// que o cliente reporta como `gen->0x74`.
    ///
    /// O `gen->0x94` **nao** vem aqui porque nao e' da area: e' de cada zona, do byte 0 do
    /// `UnkO` dela (41.7).  Duas zonas da mesma area podem ter modos de altura diferentes, e
    /// tem: no corpus a 320 tem zonas com 0, com 1 e com 4.
    /// </summary>
    public static int VerboAltura(string[] args)
    {
        Console.WriteLine($"{"model",-8}{"max h",-7}{"h diff",-8}group 3");
        int n = 0;
        foreach (int m in GameData.AvailableModels())
        {
            Area a;
            try
            {
                a = GameData.GetArea(m);
            }
            catch (Exception)
            {
                continue;
            }
            if (!a.HasHeight)
                continue;
            n++;
            string g3 = string.Join(" ", a.ByGroup.TryGetValue(3, out var cs)
                ? cs.Select(c => c.Short).OrderBy(x => x, StringComparer.Ordinal)
                : Enumerable.Empty<string>());
            Console.WriteLine($"{m,-8}{a.MaxHeight,-7}{a.MaxHdiff,-8}{g3}");
        }
        Console.WriteLine();
        Console.WriteLine($"{n} area(s) with height, of {GameData.AvailableModels().Count()} " +
                          "with a .lps. In every other area gen->0x94 has no effect at all.");
        return 0;
    }

    public static int VerboModelos(string[] args)
    {
        // os nomes, quando existem, vem do `models.txt` ao lado do executavel
        foreach (int m in GameData.AvailableModels())
            Console.WriteLine(ModelNames.Etiqueta(m));
        return 0;
    }

    private static int Erro(string msg)
    {
        Console.Error.WriteLine(msg);
        return 1;
    }
}
