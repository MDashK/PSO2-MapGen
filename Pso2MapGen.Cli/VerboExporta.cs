using Pso2MapGen.Core;

/// <summary>
///     Pso2MapGen.Cli export --packet &lt;bin&gt; --zone N [--sides-a 2 --sides-b 0] [--out &lt;file&gt;]
///     Pso2MapGen.Cli export --block &lt;block.json&gt; [--zone N] [--out &lt;file&gt;]
///
/// Escreve o `block.json` do Starlight para uma zona do packet — ou reescreve o de um conjunto
/// de blocos, que e' o ida-e-volta que mostra o que o leitor percebeu do ficheiro.
///
/// Com `--sides-a`/`--sides-b` os lados daquela zona sao trocados antes de escrever, e o
/// `zoneUnknown1` e o `TeleportData` sao **reconstruidos** a partir deles (BlockJson.Reconcilia).
/// </summary>
internal static class VerboExporta
{
    public static int Exporta(string[] args)
    {
        int i = Array.IndexOf(args, "--packet");
        int b = Array.IndexOf(args, "--block");
        if ((i < 0 || i + 1 >= args.Length) && (b < 0 || b + 1 >= args.Length))
        {
            Console.Error.WriteLine("usage: export --packet <bin> --zone N [--out <file>]");
            Console.Error.WriteLine("       export --block <block.json> [--zone N] [--out <file>]");
            return 1;
        }
        int zi = 0;
        int j = Array.IndexOf(args, "--zone");
        if (j >= 0 && j + 1 < args.Length)
            zi = int.Parse(args[j + 1]);
        ZoneInput? z;
        List<ZoneInput> zonas;
        if (b >= 0)
        {
            zonas = BlockRead.Le(args[b + 1]);
            z = j >= 0 ? zonas.FirstOrDefault(x => x.ZoneIndex == zi)
                       : zonas.FirstOrDefault(x => File.Exists(args[b + 1])
                             && string.Equals(Path.GetFullPath(x.Ficheiro ?? ""),
                                              Path.GetFullPath(args[b + 1]),
                                              StringComparison.OrdinalIgnoreCase))
                         ?? zonas.FirstOrDefault();
        }
        else
        {
            zonas = Packet0324.Load(args[i + 1]).Zonas();
            z = zonas.FirstOrDefault(x => x.ZoneIndex == zi);
        }
        int od = Array.IndexOf(args, "--out-dir");
        bool mapaInteiro = od >= 0 && od + 1 < args.Length;
        if (z is null && !mapaInteiro)
        {
            // com `--out-dir` escrevem-se todas, por isso a zona indicada nao faz falta: ha
            // packets cujas zonas sao -2, 3 e 4 e que nao tem zona 0 nenhuma
            Console.Error.WriteLine($"there is no zone {zi} there");
            return 1;
        }
        int sa = Array.IndexOf(args, "--sides-a");
        if (sa >= 0 && sa + 1 < args.Length && z is not null)
            z.LadosA = Lados(args[sa + 1]);
        int sb = Array.IndexOf(args, "--sides-b");
        if (sb >= 0 && sb + 1 < args.Length && z is not null)
            z.LadosB = Lados(args[sb + 1]);
        // os lados mandam: se alguem lhes mexeu, o `zoneUnknown1` e o `TeleportData` que vieram
        // do packet deixaram de os descrever e sao reconstruidos
        BlockJson.Reconcilia(zonas);
        int sobram = BlockJson.NaoCabem(zonas);
        if (sobram > 0)
            Console.Error.WriteLine(
                $"warning: {sobram} ZoneUnknown1 entry(ies) do not fit - the file holds one per " +
                "block and this map needs more, so that many entrance sides are lost. Add a " +
                "block, or write them in by hand.");
        // o mapa inteiro, como a GUI o escreve: uma pasta `bN` por bloco
        if (mapaInteiro)
        {
            int n = 0;
            foreach (var zona in zonas.Where(x => x.OrdemBloco >= 0))
            {
                // as pastas do servidor comecam no `b1`: o `b0` era a convencao dos despejos do
                // PacketMan, e nao a da arvore que o Starlight le
                string pasta = Path.Combine(args[od + 1], $"b{zona.OrdemBloco + 1}");
                Directory.CreateDirectory(pasta);
                File.WriteAllText(Path.Combine(pasta, "block.json"), BlockJson.Escreve(zona));
                n++;
            }
            // a lista de entradas e' da quest e nao dos blocos
            string entradas = BlockJson.Entradas(zonas);
            File.WriteAllText(Path.Combine(args[od + 1], "ZoneEntrances.json"),
                BlockJson.FragmentoDasEntradas(entradas));
            Console.WriteLine($"{n} block(s) written to {args[od + 1]}");
            Console.WriteLine("the entrance list belongs to the quest, not to a block - put this "
                              + "in its quest.json:");
            Console.WriteLine("    " + entradas);
            return 0;
        }
        string texto = BlockJson.Escreve(z!);
        int k = Array.IndexOf(args, "--out");
        if (k >= 0 && k + 1 < args.Length)
        {
            File.WriteAllText(args[k + 1], texto);
            Console.WriteLine($"written to {args[k + 1]}");
        }
        else
        {
            Console.WriteLine(texto);
        }
        return 0;
    }

    private static List<int> Lados(string s)
        => s.Length == 0 ? new List<int>()
                         : s.Split(',').Select(int.Parse).ToList();
}
