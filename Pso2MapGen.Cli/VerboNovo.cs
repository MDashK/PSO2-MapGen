using Pso2MapGen.Core;

/// <summary>
/// Um mapa feito de raiz, sem packet nenhum.
///
///     Pso2MapGen.Cli new &lt;zones&gt; --model N [--seed N --sx N --sy N --unkb N
///                                 --sides-a 2 --sides-b 0] [--out &lt;folder&gt;]
///
/// Sem <c>--out</c> desenha os mapas; com <c>--out</c> escreve `b1/block.json`,
/// `b2/block.json`, ... como as pastas do servidor se chamam.  As zonas sao ligadas em cadeia
/// pelo <see cref="BlockJson.LigaZonas"/>, que e' o mesmo caminho que a GUI usa.
/// </summary>
internal static class VerboNovo
{
    public static int Novo(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int quantas) || quantas < 1)
        {
            Console.Error.WriteLine("usage: new <zones> --model N [--seed N --sx N --sy N " +
                                    "--unkb N --args N --sides-a 2 --sides-b 0] [--out <folder>]");
            return 1;
        }
        var zonas = new List<ZoneInput>();
        for (int i = 0; i < quantas; i++)
            zonas.Add(new ZoneInput
            {
                ZoneIndex = i,
                Model = Opt(args, "--model", 0),
                Seed = Opt(args, "--seed", 0),
                SizeX = Opt(args, "--sx", 3),
                SizeY = Opt(args, "--sy", 3),
                UnkB = Opt(args, "--unkb", 1),
                Arguments = Opt(args, "--args", -1),
                UnkP = Opt(args, "--unkp", 0),
                UnkW = Opt(args, "--unkw", 0),
                Unk3 = Opt(args, "--unk3", -1),
                Unk4 = Opt(args, "--unk4", 0),
                // sem `--unkn` o campo nao fica a zero: zero nao e' um valor que alguma das
                // 1641 zonas do corpus tenha.  Cai no valor habitual da area, como na janela.
                UnkN = UnkN(args, i),
                A0 = (UnkN(args, i) >> 5) & 1,
                UnkO = InfoTables.ExtraCampo(Opt(args, "--model", 0), Opt(args, "--unkb", 1), i,
                                             "UnkO", 0),
                TemUnkO = true,
                LadosA = Lados(args, "--sides-a", new List<int> { 2 }),
                LadosB = Lados(args, "--sides-b", new List<int> { 0 }) ?? new List<int>(),
                NA = 1,
                OrdemBloco = i,
            });
        BlockJson.LigaZonas(zonas);

        string? pasta = Str(args, "--out");
        foreach (var z in zonas)
        {
            if (pasta is null)
            {
                var p = Previsao.Gera(z);
                Console.WriteLine($"--- zone {z.ZoneIndex}: model {z.Model}, " +
                                  $"{z.SizeX}x{z.SizeY}, UnkB={z.UnkB}, {p.Nota}");
                Console.WriteLine(p.Ok
                    ? Previsao.Desenha(p.Grelha!, args.Contains("--doors"))
                    : "  (no map)");
                Console.WriteLine();
            }
            else
            {
                string d = Path.Combine(pasta, $"b{z.OrdemBloco + 1}");
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, "block.json"), BlockJson.Escreve(z));
            }
        }
        if (pasta is not null)
            Console.WriteLine($"wrote {zonas.Count} blocks to {pasta}");
        return 0;
    }

    /// <summary>O <c>--unkn</c>, ou o valor habitual daquela area.</summary>
    private static int UnkN(string[] args, int zona)
    {
        int k = Array.IndexOf(args, "--unkn");
        if (k >= 0 && k + 1 < args.Length && int.TryParse(args[k + 1], out int v))
            return v;
        return InfoTables.ExtraCampo(Opt(args, "--model", 0), Opt(args, "--unkb", 1), zona,
                                     "UnkN", 0);
    }

    private static List<int>? Lados(string[] args, string nome, List<int> omissao)
    {
        string? s = Str(args, nome);
        if (s is null)
            return omissao;
        return s.Length == 0 ? new List<int>()
                             : s.Split(',').Select(int.Parse).ToList();
    }

    private static string? Str(string[] args, string nome)
    {
        int i = Array.IndexOf(args, nome);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Opt(string[] args, string nome, int omissao)
        => Str(args, nome) is string s && int.TryParse(s, out int v) ? v : omissao;
}
