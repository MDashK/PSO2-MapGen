using Pso2MapGen.Core;

namespace Pso2MapGen.Gui;

internal static class Program
{
    /// <summary>
    ///     Pso2MapGen.Gui.exe [packet.bin | quest.json | block.json] [zona | variante]
    ///
    /// Sem argumentos abre vazia; com um `.bin`, um `quest.json` ou um `block.json` enche logo
    /// os campos — e' o que permite largar qualquer um deles em cima do executavel.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var janela = new JanelaPrincipal();
        if (args.Length > 0 && File.Exists(args[0]))
        {
            bool eBloco = Path.GetExtension(args[0])
                              .Equals(".json", StringComparison.OrdinalIgnoreCase);
            int zona = args.Length > 1 && int.TryParse(args[1], out int z)
                       ? z : (eBloco ? int.MinValue : 0);
            bool eQuest = eBloco && Quest.Parece(args[0]);
            janela.Shown += (_, _) =>
            {
                if (eQuest)
                    janela.CarregaQuest(args[0], args.Length > 1 ? args[1] : null);
                else if (eBloco)
                    janela.CarregaBlocos(args[0], zona);
                else
                    janela.CarregaPacket(args[0], zona);
            };
        }
        Application.Run(janela);
    }
}
