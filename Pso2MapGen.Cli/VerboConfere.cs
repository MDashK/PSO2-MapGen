using System.Text.Json;
using Pso2MapGen.Core;

/// <summary>
/// O conferidor do port contra os <b>despejos do cliente</b>: le' os casos que o
/// `casos_export.py` extraiu das sondas (parametros medidos + a grelha que o cliente mostrou) e
/// compara celula a celula.  E' o equivalente em C# do `conferegrelha.py` da FINDINGS 39.8.
///
///     Pso2MapGen.Cli confere &lt;casos.json&gt; [-v]
/// </summary>
internal static class VerboConfere
{
    public static int Confere(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: check <cases.json> [-v]");
            return 1;
        }
        bool verboso = args.Contains("-v");
        var opcoes = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var casos = JsonSerializer.Deserialize<List<ReplayRun>>(File.ReadAllText(args[1]), opcoes)
                    ?? new List<ReplayRun>();
        int iguais = 0, custoOk = 0;
        foreach (var r in casos)
        {
            var (dif, saida, custo, detalhe) = Replay.Confere(r);
            if (dif == 0)
                iguais++;
            if (custo)
                custoOk++;
            Console.WriteLine($"{r.Nome,-26} nA={r.NA} entrance={r.Side} p98={r.P98,-3} " +
                              $"exit={saida,-4} cells: " +
                              (dif == 0 ? "OK" : dif < 0 ? "no grid" : $"{dif} diff") +
                              "  words: " + (custo ? "OK" : "diff"));
            if (verboso)
                foreach (string d in detalhe.Take(12))
                    Console.WriteLine("      " + d);
        }
        Console.WriteLine();
        Console.WriteLine($"final grids identical: {iguais} of {casos.Count}   " +
                          $"word cost correct: {custoOk} of {casos.Count}");
        return iguais == casos.Count && custoOk == casos.Count ? 0 : 1;
    }
}
