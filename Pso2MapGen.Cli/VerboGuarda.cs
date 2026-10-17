using Pso2MapGen.Core;

/// <summary>
///     Pso2MapGen.Cli save --block &lt;block.json&gt; [--zone N] [--sides-a 2 --sides-b 0]
///                        [--seed N] [--size XxY] [--model N] [--dry-run]
///
/// Guarda por cima dos `block.json` que ja' existem, em vez de escrever ficheiros novos: e' o
/// que o botao <b>Save</b> da janela faz.  So' o <b>valor</b> dos campos que mudaram e' trocado
/// — os comentarios, a indentacao e os campos que a ferramenta nao le' ficam como estavam.
///
/// Sem nenhum campo a mudar nao escreve nada, e e' assim que se confere que o caminho de
/// gravacao nao mexe num ficheiro que ja' esta' certo.  Com `--dry-run` diz o que faria e nao
/// toca em nada.
/// </summary>
internal static class VerboGuarda
{
    public static int Guarda(string[] args)
    {
        int b = Array.IndexOf(args, "--block");
        if (b < 0 || b + 1 >= args.Length)
        {
            Console.Error.WriteLine("usage: save --block <block.json> [--zone N] " +
                                    "[--sides-a 2] [--sides-b 0] [--seed N] [--size XxY] " +
                                    "[--model N] [--dry-run]");
            return 1;
        }
        bool aSerio = !args.Contains("--dry-run");
        var zonas = BlockRead.Le(args[b + 1]);

        // a zona sobre a qual os `--...` mexem: a que o `--zone` disser, senao a do ficheiro
        int j = Array.IndexOf(args, "--zone");
        ZoneInput? z = j >= 0 && j + 1 < args.Length
            ? zonas.FirstOrDefault(x => x.ZoneIndex == int.Parse(args[j + 1]))
            : zonas.FirstOrDefault(x => string.Equals(Path.GetFullPath(x.Ficheiro ?? ""),
                                                      Path.GetFullPath(args[b + 1]),
                                                      StringComparison.OrdinalIgnoreCase))
              ?? zonas.FirstOrDefault();
        if (z is null)
        {
            Console.Error.WriteLine("there is no such zone there");
            return 1;
        }

        int k = Array.IndexOf(args, "--seed");
        if (k >= 0 && k + 1 < args.Length)
            z.Seed = unchecked((int)uint.Parse(args[k + 1]));
        k = Array.IndexOf(args, "--model");
        if (k >= 0 && k + 1 < args.Length)
            z.Model = int.Parse(args[k + 1]);
        k = Array.IndexOf(args, "--size");
        if (k >= 0 && k + 1 < args.Length)
        {
            var p = args[k + 1].Split('x');
            z.SizeX = int.Parse(p[0]);
            z.SizeY = int.Parse(p[^1]);
        }
        k = Array.IndexOf(args, "--sides-a");
        if (k >= 0 && k + 1 < args.Length)
            z.LadosA = Lados(args[k + 1]);
        k = Array.IndexOf(args, "--sides-b");
        if (k >= 0 && k + 1 < args.Length)
            z.LadosB = Lados(args[k + 1]);

        // os lados mandam: mexendo-lhes, o `zoneUnknown1` e o `TeleportData` de todos os blocos
        // deixam de os descrever e sao reconstruidos
        BlockJson.Reconcilia(zonas);
        int sobram = BlockJson.NaoCabem(zonas);
        if (sobram > 0)
            Console.Error.WriteLine(
                $"warning: {sobram} ZoneUnknown1 entry(ies) do not fit - the file holds one per " +
                "block and this map needs more, so that many entrance sides are lost.");

        int mexidos = 0;
        foreach (var zona in zonas)
        {
            if (zona.Ficheiro is null)
                continue;
            var jsonc = Jsonc.Le(zona.Ficheiro);
            var mudou = BlockSave.Actualiza(zona, jsonc);
            if (mudou.Count == 0)
                continue;
            Console.WriteLine(zona.Ficheiro);
            foreach (var a in mudou)
                Console.WriteLine("    " + a);
            if (!mudou.Any(x => x.Escrita))
                continue;
            mexidos++;
            if (aSerio)
                jsonc.Grava(zona.Ficheiro);
        }
        Console.WriteLine(mexidos == 0 ? "nothing to save - the files already match"
                                       : $"{mexidos} file(s) {(aSerio ? "saved" : "would be saved")}");
        return 0;
    }

    private static List<int> Lados(string s)
        => s.Length == 0 ? new List<int>() : s.Split(',').Select(int.Parse).ToList();
}
