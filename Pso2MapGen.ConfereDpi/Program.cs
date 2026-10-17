using System.Globalization;
using System.Runtime.InteropServices;
using Pso2.Interface;
using Pso2MapGen.Gui;

/// <summary>
/// O conferidor da escala por DPI da interface.
///
/// <para>A janela do MapGen e' montada em codigo e nao pelo desenhador, e os pixeis escritos a'
/// mao la' dentro foram escritos a 96 DPI.  Num ecra 4K a 150% ou a 200% as letras crescem
/// sozinhas — a fonte vai em pontos — e os pixeis nao, e o texto acabava cortado.  A correccao
/// foi declarar <c>AutoScaleMode.Dpi</c> em todas as janelas e passar a pixeis logicos as duas
/// medidas que vem do ecra (a do texto e a do monitor), com a <c>Escala.Logico</c>.</para>
///
/// <para><b>Como e' que isto se confere sem ter um ecra 4K:</b> o Windows avisa uma janela de
/// que o DPI mudou com a mensagem <c>WM_DPICHANGED</c>.  Mandando-lhe essa mensagem a' mao, com
/// 144 ou 192 no <c>wParam</c>, o WinForms corre exactamente o mesmo caminho que correria ao
/// arrastar a janela para um monitor a 150% ou a 200%: escala a fonte e escala a geometria.
/// Depois mede-se controlo a controlo.</para>
///
/// <para>O que se exige nao e' um numero absoluto — e' que <b>nada que estava bem a 96 fique
/// mal a 144 ou a 192</b>.  Posto assim, o conferidor nao se queixa de folgas que a interface
/// sempre teve e so' acusa a regressao que interessa.</para>
///
/// <para>Com <c>CONFEREDPI_DIAG=1</c> no ambiente imprime tambem as medidas da janela — serve
/// para ver porque e' que uma linha falhou.</para>
/// </summary>
internal static class Program
{
    private const int WmDpiChanged = 0x02E0;

    /// <summary>As escalas que se conferem: 150% e 200%, que e' o caso do ecra 4K.</summary>
    private static readonly int[] Dpis = { 144, 192 };

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (!Application.SetHighDpiMode(HighDpiMode.PerMonitorV2))
        {
            Console.WriteLine("nao foi possivel por o processo em PerMonitorV2 - "
                              + "sem isso o WinForms ignora o WM_DPICHANGED e o teste nao vale");
            return 2;
        }

        Console.WriteLine();
        Console.WriteLine("ConfereDpi - a interface do MapGen a 96, 144 e 192 DPI "
                          + "(100%, 150% e 200%)");

        // a janela de ajuda precisa de uma janela-mae, de quem tira o DPI e o monitor
        using var mae = new JanelaPrincipal();
        _ = mae.Handle;

        var janelas = new (string Nome, Func<Form> Faz)[]
        {
            ("a janela principal", () => new JanelaPrincipal()),
            ("a caixa dos bits", () => new DialogoBits(4119, 330, 1, 1)),
            // o texto do ZoneFlags e' o pior caso: 58 linhas, e e' o que obrigou a janela de
            // ajuda a medir o texto em vez de ter largura escrita
            ("a ajuda do ZoneFlags", () => Ajuda.Monta(mae, "ZoneFlags", Ajuda.ZoneFlags,
                                                       "ZoneFlags")),
        };

        int bons = 0, total = 0;
        foreach (int dpi in Dpis)
        {
            Console.WriteLine();
            Console.WriteLine("  --- a " + dpi + " DPI (" + dpi * 100 / Escala.DpiBase
                              + "%) ---");
            foreach (var (nome, faz) in janelas)
            {
                // uma janela nova para cada escala: a mensagem do DPI so' se da' uma vez
                using var f = faz();
                Confere(nome, f, dpi, ref bons, ref total);
            }
        }

        // A aritmetica da `Escala.Logico`, que e' o que converte as medidas que vem do ecra.
        // Nao precisa de janela nenhuma: e' so' a conta, e e' bom que esteja escrita aqui.
        total++;
        var contas = new (int Ecra, int Dpi, int Espera)[]
        {
            (54, 96, 54), (108, 192, 54), (81, 144, 54),
            (956, 192, 478), (35, 144, 23), (750, 240, 300),
        };
        var erradas = contas.Where(c => Escala.Logico(c.Ecra, c.Dpi) != c.Espera).ToList();
        // e a direccao directa, que e' a que as colunas das listas usam
        erradas.AddRange(contas.Where(c => Escala.Px(c.Espera, c.Dpi) != c.Ecra));
        if (erradas.Count == 0)
            bons++;
        Console.WriteLine();
        Linha("  a conta da Logico", erradas.Count == 0
            ? contas.Length + " contas certas"
            : string.Join(", ", erradas.Select(
                c => "Logico(" + c.Ecra + "," + c.Dpi + ")=" + Escala.Logico(c.Ecra, c.Dpi)
                     + " e nao " + c.Espera)),
            erradas.Count == 0);

        Console.WriteLine();
        Console.WriteLine(bons + " / " + total);
        return bons == total ? 0 : 1;
    }

    /// <summary>Mede uma janela, passa-a ao DPI pedido e compara.</summary>
    private static void Confere(string nome, Form f, int dpi, ref int bons, ref int total)
    {
        Console.WriteLine("  " + nome);
        _ = f.Handle;                       // a alca basta: nao e' preciso mostra'-la
        Application.DoEvents();

        var antes = Mede(f);
        var colunasAntes = Colunas(f);
        int dpiAntes = f.DeviceDpi;
        // Com `CONFEREDPI_DESPEJA=1` sai a medicao inteira a 96 DPI, uma linha por controlo.
        // Serve para comparar o antes e o depois de uma mexida no layout: a escala por DPI nao
        // deve mudar nada na escala de 100%, e a maneira de ter certeza disso e' diferir os
        // dois despejos.
        if (Environment.GetEnvironmentVariable("CONFEREDPI_DESPEJA") == "1" && dpi == Dpis[0])
            foreach (var (caminho, m) in antes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                Console.WriteLine("      96 " + nome + "  " + caminho + "  "
                                  + m.X + "," + m.Y + " " + m.Largura + "x" + m.Altura);
        MudaDpi(f, dpi);
        Application.DoEvents();
        var depois = Mede(f);

        if (Environment.GetEnvironmentVariable("CONFEREDPI_DIAG") == "1")
            Console.WriteLine("      DIAG bounds=" + f.Bounds + " client=" + f.ClientSize
                              + " min=" + f.MinimumSize
                              + " ecra=" + Screen.FromControl(f).WorkingArea
                              + " fonte=" + f.Font.SizeInPoints);

        // 0. a janela esta' bem na escala de 100%?  Esta e' a unica medida absoluta do
        //    conferidor e corre uma vez por janela: nenhum controlo com medida escrita deve
        //    cortar o seu texto nem passar para fora do pai ja' a 96 DPI.  Se isto falhar, o
        //    layout esta' mal antes de a escala entrar ao barulho.
        if (dpi == Dpis[0])
        {
            total++;
            var malDeRaiz = antes.Where(kv => !kv.Value.Cabe || !kv.Value.Dentro)
                                 .Select(kv => kv.Key).ToList();
            if (malDeRaiz.Count == 0)
                bons++;
            Linha("    bem a 96 DPI", malDeRaiz.Count == 0
                ? antes.Count + " controlos, nenhum a cortar nem a sair do pai"
                : malDeRaiz.Count + " mal ja' a 96: " + string.Join("; ", malDeRaiz.Take(6)),
                malDeRaiz.Count == 0);
        }

        // 1. o teste vale? se o WinForms ignorou a mensagem, o resto nao quer dizer nada
        total++;
        bool valeu = f.DeviceDpi == dpi;
        if (valeu)
            bons++;
        Linha("    o teste vale", "DeviceDpi " + dpiAntes + " -> " + f.DeviceDpi, valeu);
        if (!valeu)
            return;

        double razao = (double)dpi / dpiAntes;

        // O ecra desta maquina pode nao ter tamanho para a janela a esta escala, e nesse caso o
        // Windows nao a deixa crescer — a medida da janela deixa de ser uma promessa que se
        // possa exigir.  O que esta' dentro dela continua a ser.
        var util = Screen.FromControl(f).WorkingArea;
        bool cabeNoEcra = antes["(a janela)"].Altura * razao <= util.Height
                          && antes["(a janela)"].Largura * razao <= util.Width;
        if (!cabeNoEcra)
            Console.WriteLine("      (o ecra desta maquina, " + util.Width + "x" + util.Height
                              + ", nao tem tamanho para esta janela a " + dpi
                              + " DPI: a medida da janela nao se exige, a do conteudo sim)");

        // 2. a geometria acompanhou a escala.
        //
        //    So' se exige isto a quem tem a medida **escrita a' mao** — e' essa que nao
        //    acompanhava o DPI e e' essa que o AutoScale tem de multiplicar.  Quem tem
        //    `AutoSize` fica do tamanho do seu texto e quem esta' encostado ou a preencher
        //    segue o pai: a medida deles e' consequencia, nao promessa.  E a **altura** de uma
        //    caixa de texto, de uma lista ou de um contador sai da fonte (o WinForms poe-lhes
        //    a altura que a letra pede), por isso dessas so' se exige a largura.
        total++;
        var fora = new List<string>();
        int olhados = 0;
        foreach (var (caminho, a) in antes)
        {
            if (!depois.TryGetValue(caminho, out var d) || !a.Fixo)
                continue;
            if (!cabeNoEcra && caminho.StartsWith('('))
                continue;
            olhados++;
            if (a.Largura >= 4 && !Acompanha(a.Largura, d.Largura, razao))
                fora.Add(caminho + ": largura " + a.Largura + " -> " + d.Largura);
            else if (a.Altura >= 4 && !a.AlturaDaFonte
                     && !Acompanha(a.Altura, d.Altura, razao))
                fora.Add(caminho + ": altura " + a.Altura + " -> " + d.Altura);
        }
        if (fora.Count == 0)
            bons++;
        Linha("    escalou", fora.Count == 0
            ? olhados + " medidas escritas a' mao, todas a " + razao.ToString("0.##") + "x"
            : fora.Count + " de " + olhados + " fora da escala: "
              + string.Join("; ", fora.Take(6)),
            fora.Count == 0);

        // 3. as posicoes acompanharam a escala.
        //
        //    E' o que importa numa caixa posta por coordenadas: se as linhas nao se afastarem
        //    na mesma proporcao, ficam uma por cima da outra.  Nas que vivem dentro de um
        //    `TableLayoutPanel` a posicao e' calculada por ele e escala com as colunas.
        total++;
        var torto = new List<string>();
        int postos = 0;
        foreach (var (caminho, a) in antes)
        {
            if (!depois.TryGetValue(caminho, out var d) || !a.PosicaoContada
                || (a.X < 4 && a.Y < 4))
                continue;
            postos++;
            if (!Acompanha(a.X, d.X, razao) || !Acompanha(a.Y, d.Y, razao))
                torto.Add(caminho + ": " + a.X + "," + a.Y + " -> " + d.X + "," + d.Y);
        }
        if (torto.Count == 0)
            bons++;
        Linha("    ficou no lugar", torto.Count > 0
            ? torto.Count + " de " + postos + " fora do lugar: "
              + string.Join("; ", torto.Take(6))
            : postos == 0
                ? "nao ha posicoes postas a' mao nesta janela"
                : postos + " posicoes, todas a " + razao.ToString("0.##") + "x",
            torto.Count == 0);

        // 3b. as colunas das listas.
        //
        //     A largura de uma coluna de `ListView` e' escrita como as outras medidas, mas e'
        //     uma propriedade do cabecalho e nao do controlo — ha' que medi-la a' parte para
        //     saber se o AutoScale lhe chega.  Sem escala, a 200% o titulo da coluna fica
        //     cortado com o texto a cheio por baixo.
        if (colunasAntes.Count > 0)
        {
            total++;
            var colunasDepois = Colunas(f);
            var mal = new List<string>();
            foreach (var (nome2, largura) in colunasAntes)
            {
                var dnome = colunasDepois.FirstOrDefault(x => x.Nome == nome2);
                if (dnome.Nome is null)
                    continue;
                if (!Acompanha(largura, dnome.Largura, razao))
                    mal.Add(nome2 + ": " + largura + " -> " + dnome.Largura);
            }
            if (mal.Count == 0)
                bons++;
            Linha("    colunas", mal.Count == 0
                ? colunasAntes.Count + " colunas, todas a " + razao.ToString("0.##") + "x"
                : mal.Count + " de " + colunasAntes.Count + " fora da escala: "
                  + string.Join("; ", mal.Take(6)),
                mal.Count == 0);
        }

        // 4. o texto que cabia continua a caber
        total++;
        var cortou = antes.Where(kv => depois.ContainsKey(kv.Key)
                                       && kv.Value.Cabe && !depois[kv.Key].Cabe)
                          .Select(kv => kv.Key).ToList();
        if (cortou.Count == 0)
            bons++;
        Linha("    o texto cabe", cortou.Count == 0
            ? "nenhum controlo passou a cortar o texto"
            : cortou.Count + " passaram a cortar: " + string.Join("; ", cortou.Take(6)),
            cortou.Count == 0);

        // 5. quem estava dentro do pai continua dentro
        total++;
        var saiu = antes.Where(kv => depois.ContainsKey(kv.Key)
                                     && kv.Value.Dentro && !depois[kv.Key].Dentro)
                        .Select(kv => kv.Key).ToList();
        if (saiu.Count == 0)
            bons++;
        Linha("    dentro do pai", saiu.Count == 0
            ? "nenhum controlo passou a sair do pai"
            : saiu.Count + " passaram a sair: " + string.Join("; ", saiu.Take(6)),
            saiu.Count == 0);
        Console.WriteLine();
    }

    /// <summary>As colunas de todas as `ListView` da janela, com a largura de cada uma.</summary>
    private static List<(string Nome, int Largura)> Colunas(Control raiz)
    {
        var fora = new List<(string, int)>();
        void Anda2(Control c, string caminho)
        {
            if (c is ListView lv)
                foreach (ColumnHeader h in lv.Columns)
                    fora.Add((caminho + "/" + h.Text, h.Width));
            foreach (Control f in c.Controls)
                Anda2(f, caminho + "/" + f.GetType().Name);
        }
        Anda2(raiz, "");
        return fora;
    }

    private readonly record struct Medida(int Largura, int Altura, int X, int Y,
                                          bool Cabe, bool Dentro,
                                          bool Fixo, bool AlturaDaFonte, bool PosicaoContada);

    /// <summary>A medida nova e' a antiga vezes a escala, com folga para o arredondamento?</summary>
    private static bool Acompanha(int antes, int depois, double razao)
    {
        double espera = antes * razao;
        return Math.Abs(depois - espera) <= Math.Max(2.0, espera * 0.06);
    }

    /// <summary>
    /// A medida deste controlo foi escrita a' mao?  So' essa e' que o AutoScale tem de
    /// multiplicar: com <c>AutoSize</c> a medida vem do texto, e encostado ou a preencher vem
    /// do pai.
    /// </summary>
    private static bool MedidaEscrita(Control c)
        => !c.AutoSize && c.Dock == DockStyle.None
           && (c.Anchor & (AnchorStyles.Left | AnchorStyles.Right))
              != (AnchorStyles.Left | AnchorStyles.Right)
           && !DoWinForms(c);

    /// <summary>
    /// A posicao deste controlo conta?  So' a de quem esta' posto por coordenadas: um controlo
    /// a preencher nao tem posicao propria.
    /// </summary>
    private static bool PosicaoContada(Control c)
        => c.Dock == DockStyle.None && !DoWinForms(c)
           // dentro de uma tabela ou de uma fila, a posicao e' calculada: as linhas da tabela
           // crescem com o que tem dentro, e o que tem dentro cresce com a fonte, que nao
           // cresce exactamente na mesma proporcao.  Exigir a razao da escala a uma posicao
           // calculada seria exigir o que o WinForms nunca prometeu.
           && c.Parent is not (TableLayoutPanel or FlowLayoutPanel);

    /// <summary>
    /// Os dois botoes de seta de um contador nao sao nossos: o WinForms e' que os poe e e' ele
    /// que lhes da' a medida — e nao lha muda ao mudar o DPI, que e' um defeito dele e nao do
    /// nosso layout.  Ao <i>abrir</i> a janela ja' vem com a medida certa.
    /// </summary>
    private static bool DoWinForms(Control c)
        => c.Parent is UpDownBase
           // os dois paineis de um divisor tambem nao: a medida deles sai da posicao do
           // divisor, que o WinForms escala a' sua maneira
           || c is SplitterPanel;

    /// <summary>
    /// A altura deste controlo sai da fonte e nao do que se lhe escreveu: o WinForms poe a uma
    /// caixa de texto, a uma lista ou a um contador a altura que a letra pede.
    /// </summary>
    private static bool AlturaVemDaFonte(Control c)
        => c is TextBoxBase or ComboBox or UpDownBase || c.Parent is UpDownBase;

    /// <summary>
    /// Mede a janela e tudo o que tem dentro.  A chave e' o caminho na arvore, para as duas
    /// medicoes casarem controlo a controlo.
    /// </summary>
    private static Dictionary<string, Medida> Mede(Form f)
    {
        var fora = new Dictionary<string, Medida>
        {
            ["(a janela)"] = new Medida(f.Width, f.Height, 0, 0,
                                        true, true, true, false, false),
            ["(o interior)"] = new Medida(f.ClientSize.Width, f.ClientSize.Height, 0, 0,
                                          true, true, true, false, false),
        };
        Anda(f, "", fora);
        return fora;
    }

    private static void Anda(Control pai, string prefixo, Dictionary<string, Medida> fora)
    {
        var contas = new Dictionary<string, int>();
        foreach (Control c in pai.Controls)
        {
            // a chave e' o tipo mais o texto, que e' o que identifica uma linha da interface
            // sem depender da ordem por que os controlos foram criados
            string etiqueta = c.GetType().Name;
            string texto = (c.Text ?? "").Split('\n')[0].Trim();
            if (texto.Length > 0)
                etiqueta += " \"" + (texto.Length > 28 ? texto[..28] : texto) + "\"";
            string chave = prefixo + "/" + etiqueta;
            contas[chave] = contas.TryGetValue(chave, out int n) ? n + 1 : 1;
            if (contas[chave] > 1)
                chave += "#" + contas[chave];

            fora[chave] = new Medida(c.Width, c.Height, c.Left, c.Top,
                                     TextoCabe(c), DentroDoPai(c),
                                     MedidaEscrita(c), AlturaVemDaFonte(c), PosicaoContada(c));
            Anda(c, chave, fora);
        }
    }

    /// <summary>
    /// O texto cabe no controlo?  So' interessa em quem tem medida fixa: um controlo com
    /// <c>AutoSize</c> ja' e' do tamanho do seu texto por construcao.
    /// </summary>
    private static bool TextoCabe(Control c)
    {
        if (c.AutoSize)
            return true;
        if (c is Button b)
        {
            // Um botao pede sempre mais altura no `PreferredSize` do que os 23 pixeis que o
            // WinForms lhe da' de origem: o que ele pede e' enchimento folgado, nao e' o que a
            // letra precisa — se fosse, todos os botoes do Windows estariam cortados.  O que
            // corta mesmo o texto de um botao e' a largura, e essa mede-se a' letra, com as
            // bordas de fora.
            var t = TextRenderer.MeasureText(b.Text, b.Font);
            return t.Width <= b.Width - 6 && t.Height <= b.Height;
        }
        if (c is Label or CheckBox)
        {
            // numa etiqueta o `PreferredSize` e' exacto, e com a largura dada ja' conta com a
            // quebra de linha — e' o que apanhou a nota da caixa dos bits a cortar a 200%
            var quer = c.GetPreferredSize(new Size(c.Width, 0));
            return quer.Width <= c.Width + 1 && quer.Height <= c.Height + 1;
        }
        return true;
    }

    /// <summary>
    /// O controlo esta' dentro da area util do pai?  Num painel com <c>AutoScroll</c> passar
    /// para fora e' normal — e' para isso que a barra la' esta' — e a area util dele ja' conta
    /// com o que tem dentro.
    /// </summary>
    private static bool DentroDoPai(Control c)
    {
        var pai = c.Parent;
        if (pai is null || c.Dock != DockStyle.None)
            return true;
        var util = pai.DisplayRectangle;
        bool rola = pai is ScrollableControl { AutoScroll: true };
        return c.Right <= util.Right + 1 && (rola || c.Bottom <= util.Bottom + 1);
    }

    /// <summary>
    /// O aviso do Windows de que o DPI mudou.  O <c>wParam</c> leva o DPI novo nas duas
    /// metades e o <c>lParam</c> aponta para o rectangulo que o Windows sugere para a janela —
    /// o WinForms le'-o, por isso tem de ser um ponteiro valido.
    /// </summary>
    private static void MudaDpi(Form f, int dpi)
    {
        double razao = (double)dpi / f.DeviceDpi;
        var sugerido = new Rect
        {
            Left = f.Left,
            Top = f.Top,
            Right = f.Left + (int)Math.Round(f.Width * razao),
            Bottom = f.Top + (int)Math.Round(f.Height * razao),
        };
        IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf<Rect>());
        try
        {
            Marshal.StructureToPtr(sugerido, p, false);
            SendMessageW(f.Handle, WmDpiChanged, (dpi << 16) | dpi, p);
        }
        finally
        {
            Marshal.FreeHGlobal(p);
        }
    }

    private static void Linha(string nome, string nota, bool bom)
        => Console.WriteLine("  " + nome.PadRight(20) + (bom ? " OK   " : " MAL  ") + nota);
}
