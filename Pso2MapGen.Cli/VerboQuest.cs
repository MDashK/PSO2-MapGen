using Pso2MapGen.Core;

/// <summary>
///     Pso2MapGen.Cli quest &lt;quest.json&gt; [--variant N] [--json]
///
/// Abre uma quest do Starlight: diz que blocos e' que ela carrega, que variantes estao no disco,
/// e as zonas de uma delas.
///
/// <para>Nao e' o mesmo que abrir a pasta de uma variante.  A quest diz <b>que</b> blocos carrega
/// e por que ordem, e isso nem sempre e' o que esta' no disco.</para>
/// </summary>
internal static class VerboQuest
{
    public static int Corre(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: quest <quest.json> [--variant N] [--json]");
            return 1;
        }
        string caminho = args[1];
        if (!Quest.Parece(caminho))
        {
            Console.Error.WriteLine($"{caminho} does not look like a quest.json "
                                    + "(a quest.json has \"Blocks\" and no \"ZoneIndex\")");
            return 1;
        }

        Quest q;
        try
        {
            q = Quest.Le(caminho);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }

        int i = Array.IndexOf(args, "--variant");
        string? pedida = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;

        if (pedida is null)
        {
            Console.WriteLine($"{q.Nome}: blocks {string.Join(", ", q.Blocos)}, "
                              + $"{q.Variantes.Count} variant folder(s)");
            foreach (var v in q.Variantes)
            {
                string nota = v.Blocos == q.Blocos.Count ? ""
                            : v.Blocos == 0 ? "   ** none of the quest's blocks are there"
                            : $"   ** only {v.Blocos} of {q.Blocos.Count}";
                Console.WriteLine($"  {v.Nome,-10} {v.Blocos} block(s){nota}");
            }
            return 0;
        }

        var escolhida = q.Variantes.FirstOrDefault(
            v => string.Equals(v.Nome, pedida, StringComparison.OrdinalIgnoreCase));
        if (escolhida is null)
        {
            Console.Error.WriteLine($"{q.Nome} has no variant {pedida}");
            return 1;
        }

        List<ZoneInput> zonas;
        var faltam = new List<string>();
        try
        {
            // `--folder` le' a pasta da variante como ela esta', ignorando a lista da quest.  So'
            // serve para se poder por os dois lado a lado e ver onde e' que discordam
            zonas = args.Contains("--folder")
                ? BlockRead.Le(escolhida.Pasta)
                : q.Zonas(escolhida, out faltam);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }

        if (args.Contains("--json"))
        {
            foreach (var z in zonas) Console.WriteLine(BlockJson.Escreve(z));
            return 0;
        }

        Console.WriteLine($"{q.Nome} variant {escolhida.Nome}: {zonas.Count} zone(s)");
        foreach (var z in zonas)
            Console.WriteLine($"  zone {z.ZoneIndex,2}  model {z.Model,4}  {z.SizeX}x{z.SizeY}  "
                              + $"unkB={z.UnkB}  seed={(uint)z.Seed}  "
                              + $"sidesA=[{string.Join(",", z.LadosA ?? new List<int>())}]  "
                              + $"sidesB=[{string.Join(",", z.LadosB ?? new List<int>())}]  "
                              + Path.GetFileName(Path.GetDirectoryName(z.Ficheiro ?? "")));
        if (faltam.Count > 0)
            Console.Error.WriteLine($"warning: {string.Join(", ", faltam)} missing from that "
                                    + "variant");
        return 0;
    }
}
