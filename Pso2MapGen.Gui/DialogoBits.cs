using Pso2MapGen.Core;

namespace Pso2MapGen.Gui;

/// <summary>
/// O <c>UnkN</c> bit a bit.
///
/// <para>Escrever `4119` no campo e' escrever cinco booleanos de uma vez, e nao ha forma de
/// adivinhar qual deles se queria mexer.  O cliente nunca le' o campo inteiro: o
/// <c>0x007194F0</c> e' um testador de bits generico e ha sete sitios a chama-lo, cada um com o
/// seu bit (FINDINGS 41.2).  Por isso o campo edita-se aqui, um bit de cada vez, com o que se
/// sabe de cada um a' frente.</para>
///
/// <para>Os bits acima do 12 nunca apareceram nas 1641 zonas medidas; se algum vier ligado num
/// packet, fica como esta' — a caixa so' mexe nos treze de baixo.</para>
/// </summary>
internal sealed class DialogoBits : Form
{
    /// <summary>
    /// O que se sabe de cada bit.  O <c>Nota</c> e' a afirmacao mais forte que as 1641 zonas do
    /// corpus sustentam, e nao uma correlacao solta (FINDINGS 41.10); o <c>Aviso</c> marca os
    /// bits que <b>nao</b> se deviam escolher a' mao porque outra coisa os decide.
    /// </summary>
    private static readonly (int Bit, string Nota, bool Aviso)[] Sabido =
    {
        (0, "read at 0x0078DB8C - agrees with zoneType & 1 in 95% (1566 of 1641)", false),
        (1, "read at 0x04B4F366 - set in ALL 1132 generated zones, no exception", true),
        (2, "read at 0x04B4D657 - set in every one of the 1641 zones", true),
        (3, "read at 0x0072EA61 (the IsMultiMap check) - and NEVER set in the corpus", false),
        (4, "read at 0x049C0B7D (landform_root) - stored, not branched on. 1117 of 1635", false),
        (5, "read at 0x00647F03 -> gen->0xA0: one leftover side becomes an exit. 95 of 95", false),
        (6, "no reader found - only ever in FIXED maps, all 41 of them", true),
        (7, "no reader found - an exact function of zoneType: 1 for types 6 and 7", true),
        (8, "never seen set", false),
        (9, "never seen set", false),
        (10, "read at 0x00EC36F8 (LobbyEffectPlayTime) - all 52 Shironia zones", false),
        (11, "never seen set", false),
        (12, "no reader found - set in 1640 of 1641 (the one gap is an Ultimate)", true),
    };

    private readonly CheckBox[] _bits = new CheckBox[Sabido.Length];
    private readonly Label _valor = new() { AutoSize = true, Font = new Font("Consolas", 10f) };
    private bool _aPor;

    public int Valor { get; private set; }

    public DialogoBits(int valor, int modelo, int unkB, int zona)
    {
        Valor = valor;
        Text = "ZoneFlags, bit by bit";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        Font = new Font("Segoe UI", 9f);
        // A escala por DPI.  Esta caixa e' posta por coordenadas — a 200% um `Point(12, 40)`
        // punha uma linha por cima da outra, que ja' tinha o dobro da altura — e e' o WinForms
        // que multiplica estes numeros todos, ao sair do construtor.  Por isso os pixeis daqui
        // para baixo estao todos em **pixeis logicos**, de 96 DPI, e as duas medidas que vem
        // do ecra (a do texto e a do monitor) sao convertidas com a `Escala.Logico`.
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(Escala.DpiBase, Escala.DpiBase);

        // A largura era 478 fixos, que so' acomodava as notas na fonte e no DPI em que foram
        // escritas — noutro, as mais compridas ("agrees with zoneType & 1 in 95% (1566 of
        // 1641)") saiam cortadas pela direita.  Mede-se a mais larga com a fonte que vai mesmo
        // ser usada: a caixa de verificacao comeca em x=12 e gasta cerca de 20 px no quadrado.
        int maisLarga = 0;
        foreach (var (bit, nota, _) in Sabido)
        {
            var fonte = bit == 5 ? new Font(Font, FontStyle.Bold) : Font;
            maisLarga = Math.Max(maisLarga,
                TextRenderer.MeasureText($"bit {bit,-2}  {nota}", fonte).Width);
        }
        var ecra = Screen.FromPoint(Cursor.Position).WorkingArea;
        // as duas medidas que vem em pixeis do ecra passam a logicos, para entrarem na mesma
        // escala que o resto dos numeros desta caixa
        int maisLargaLog = this.Logico(maisLarga);
        int larguraDoEcra = this.Logico(ecra.Width);
        int largura = Math.Min(Math.Max(478, maisLargaLog + 12 + 24 + 16), larguraDoEcra - 80);
        // o passo entre linhas serve duas vezes: aqui, para a altura, e no ciclo que as poe
        const int passo = 24;
        // A altura tambem tem o tecto do monitor, e nao tinha: a largura sempre foi limitada ao
        // ecra mas a altura ia inteira, e num portatil a 200% a caixa ficava mais alta do que o
        // ecra com os botoes de fora.  Com o tecto posto, o que nao couber desliza — e' o que o
        // `AutoScroll` faz, e e' por isso que ele aqui esta'.
        int alturaPedida = passo * Sabido.Length + 142;
        AutoScroll = true;
        ClientSize = new Size(largura, Math.Min(alturaPedida, this.Logico(ecra.Height) - 60));

        int y = 8;
        for (int i = 0; i < Sabido.Length; i++)
        {
            var (bit, nota, aviso) = Sabido[i];
            _bits[i] = new CheckBox
            {
                Text = $"bit {bit,-2}  {nota}",
                AutoSize = true,
                Location = new Point(12, y),
                Checked = ((valor >> bit) & 1) != 0,
                // o bit 5 e' o unico que mexe no mapa, por isso vai a cheio
                Font = bit == 5 ? new Font(Font, FontStyle.Bold) : Font,
                // os bits que outra coisa decide (o mapa ser gerado, o zoneType) ficam a cinza:
                // continuam a poder mexer-se, mas nao e' por ali que se comeca
                ForeColor = aviso ? SystemColors.GrayText : SystemColors.ControlText,
            };
            _bits[i].CheckedChanged += (_, _) => Recalcula();
            Controls.Add(_bits[i]);
            y += passo;
        }

        y += 6;
        // A etiqueta ocupa mais do que os 48 px que havia ate' ao valor, e tapava-lhe o
        // principio: via-se `0x1016` e nao `4118   0x1016`, o que fazia parecer que a caixa
        // editavel e a janela discordavam quando dizem o mesmo numero em bases diferentes.
        // Medir a etiqueta em vez de adivinhar resolve-o a qualquer DPI.
        var etiqueta = new Label { Text = "ZoneFlags =", AutoSize = true,
                                   Location = new Point(12, y + 3) };
        Controls.Add(etiqueta);
        int xValor = 12
                   + this.Logico(TextRenderer.MeasureText(etiqueta.Text, etiqueta.Font).Width)
                   + 8;
        _valor.Location = new Point(xValor, y + 3);
        _valor.AutoSize = true;
        Controls.Add(_valor);

        // o valor habitual da area: uma zona feita a' mao nao tem packet de onde o tirar, e o
        // zero nao serve — nao e' um valor que alguma das 1641 zonas tenha
        int habitual = InfoTables.ExtraCampo(modelo, unkB, zona, "UnkN", 0);
        if (habitual != 0)
        {
            var b = new Button
            {
                Text = $"use this area's usual value ({habitual}  0x{habitual:X})",
                Location = new Point(Math.Max(230, xValor + 150), y),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            b.Click += (_, _) => Poe(habitual);
            Controls.Add(b);
        }

        y += 26;
        // A nota tinha altura escrita (30 px, que davam as duas linhas exactas em que ela
        // quebra).  Era justo de mais: a 200% a mesma quebra pedia um pixel a mais do que o
        // dobro e a segunda linha ficava cortada pelo meio.  Com `AutoSize` e um tecto so' na
        // largura, ela quebra onde tem de quebrar e fica com a altura que pedir, a qualquer DPI.
        Controls.Add(new Label
        {
            Text = "Greyed-out bits are decided by something else: 1 by the map being generated, "
                   + "2 and 12 by nothing (always on), 6 by the map being fixed, 7 by zoneType.",
            Location = new Point(12, y),
            AutoSize = true,
            MaximumSize = new Size(largura - 24, 0),
            ForeColor = SystemColors.GrayText,
        });

        y += 40;
        // a altura do botao vai escrita: a que ele traz de origem sao 23 pixeis que nao
        // acompanham a fonte, e assim fica claro que sao 23 logicos e que escalam com o resto
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK,
                              Location = new Point(largura - 172, y),
                              Size = new Size(76, 23) };
        var cancelar = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel,
                                    Location = new Point(largura - 88, y),
                                    Size = new Size(76, 23) };
        Controls.Add(ok);
        Controls.Add(cancelar);
        AcceptButton = ok;
        CancelButton = cancelar;

        // aqui e' que a escala por DPI corre, com a caixa ja' montada
        ResumeLayout(performLayout: true);
        Recalcula();
    }

    private void Poe(int v)
    {
        _aPor = true;
        for (int i = 0; i < Sabido.Length; i++)
            _bits[i].Checked = ((v >> Sabido[i].Bit) & 1) != 0;
        _aPor = false;
        // os bits de cima que o valor novo traga entram tambem
        Valor = v;
        Recalcula(guardaAltos: false);
    }

    private void Recalcula(bool guardaAltos = true)
    {
        if (_aPor)
            return;
        // os bits que a caixa nao mostra ficam como estavam
        int altos = guardaAltos ? Valor & ~Mascara() : 0;
        int v = altos;
        for (int i = 0; i < Sabido.Length; i++)
            if (_bits[i].Checked)
                v |= 1 << Sabido[i].Bit;
        Valor = v;
        _valor.Text = altos != 0
            ? $"{v}   0x{v:X}   (and bits above the list, kept)"
            : $"{v}   0x{v:X}";
    }

    private static int Mascara()
    {
        int m = 0;
        foreach (var (bit, _, _) in Sabido)
            m |= 1 << bit;
        return m;
    }
}
