using Pso2MapGen.Core;

/// <summary>
/// O despejo de uma arvore inteira de packets num so' ficheiro, para comparar o port contra o
/// Python sem lancar um processo por zona.
///
///     Pso2MapGen.Cli dump &lt;folder&gt; [--limit N]
/// </summary>
internal static class VerboDespejo
{
    public static int Despeja(string[] args)
    {
        string raiz = args.Length > 1 ? args[1] : ".";
        int limite = 0;
        int li = Array.IndexOf(args, "--limit");
        if (li >= 0 && li + 1 < args.Length)
            limite = int.Parse(args[li + 1]);

        var bins = Directory.GetFiles(raiz, "*.bin", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(raiz, f).Replace(Path.DirectorySeparatorChar, '/'))
                            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (limite > 0)
            bins = bins.Take(limite).ToList();

        var saida = Console.Out;
        foreach (string rel in bins)
        {
            Packet0324 pk;
            try { pk = Packet0324.Load(Path.Combine(raiz, rel)); }
            catch (Exception e)
            {
                saida.WriteLine($"== {rel} UNREADABLE {e.GetType().Name}");
                continue;
            }
            foreach (var z in pk.Zonas())
            {
                saida.WriteLine($"== {rel} {z.ZoneIndex}");
                if (z.Model is 0 or -1)
                {
                    saida.WriteLine("skipped no-model");
                    continue;
                }
                if (z.UnkB != 0 && (z.SizeX <= 0 || z.SizeY <= 0))
                {
                    saida.WriteLine("skipped no-size");
                    continue;
                }
                if (z.UnkB != 0 && !File.Exists(GameData.LpsPath(z.Model)))
                {
                    saida.WriteLine("skipped no-lps");
                    continue;
                }
                Previsto p;
                try { p = Previsao.Gera(z); }
                catch (Exception e)
                {
                    saida.WriteLine($"exception {e.GetType().Name}: {e.Message}");
                    continue;
                }
                saida.WriteLine($"note {p.Nota}");
                saida.WriteLine($"pieces {(p.G is null ? 0 : p.G.Trace.Count + 1)}");
                saida.WriteLine($"words {(p.G is null ? 0 : p.G.Mt1.Draws)}");
                if (p.Grelha is null)
                {
                    saida.WriteLine("grid -");
                    continue;
                }
                var gr = p.Grelha;
                saida.WriteLine($"grid {gr.W} {gr.H}");
                for (int y = 0; y < gr.H; y++)
                    for (int x = 0; x < gr.W; x++)
                    {
                        int i = gr.Idx(x, y);
                        if (gr.Name[i] is null)
                            continue;
                        saida.WriteLine($"cell {x} {y} {gr.Name[i]} {gr.Rot[i]} " +
                                        $"{gr.Height[i]} {gr.Info[i]:x8}");
                    }
            }
        }
        return 0;
    }
}
