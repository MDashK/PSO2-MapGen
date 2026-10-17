using System.Text;
using Pso2MapGen.Core;

namespace Pso2MapGen.Gui;

/// <summary>
/// A janela da ferramenta.  Os tres botoes ficam presos no topo (numa resolucao de 1366x768 o
/// painel dos campos nao cabe inteiro, e era o "Generate map" que ficava fora do scroll), o
/// resto do painel esquerdo rola, e a pre-visualizacao fica a' direita.
///
/// A interface esta' em **ingles**; os comentarios do codigo ficam em portugues, como o
/// FINDINGS para onde apontam.
///
/// Os campos sao os da FINDINGS 39.2 e cada um tem um botao [i] com o que ele faz ao mapa —
/// incluindo os dois que <b>nao saem do packet</b>, que e' preciso poder mexer.
/// </summary>
public sealed class JanelaPrincipal : Form
{
    // --- os campos da zona
    private readonly NumericUpDown _zoneIndex = Numero(-8, 64, 0);
    private readonly ComboBox _model = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _seed = new();
    // o minimo e' **zero** e nao um: as zonas de mapa fixo vem do packet com `sizeX = sizeY = 0`
    // (o mapa e' o `.lat`, o tecto nao serve para nada), e um minimo de 1 fazia so' escolhe-las
    // na lista transforma'-las em 1x1 — e depois a exportacao escrevia esse 1 no ficheiro
    private readonly NumericUpDown _sizeX = Numero(0, 64, 3);
    private readonly NumericUpDown _sizeY = Numero(0, 64, 3);
    private readonly ComboBox _unkB = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _arguments = new() { Text = "-1" };
    private readonly ComboBox _unkP = new() { DropDownStyle = ComboBoxStyle.DropDown, Text = "0" };
    private readonly ComboBox _unkQ = new() { DropDownStyle = ComboBoxStyle.DropDown, Text = "0" };
    private readonly ComboBox _unkW = new() { DropDownStyle = ComboBoxStyle.DropDown, Text = "0" };
    private readonly ComboBox _unkY = new() { DropDownStyle = ComboBoxStyle.DropDown, Text = "0" };
    private readonly NumericUpDown _unk3 = Numero(-1, 64, -1);
    private readonly NumericUpDown _unk4 = Numero(0, 64, 0);
    private readonly TextBox _unkN = new() { Text = "0" };
    private readonly Button _unkNBits = new() { Text = "Bits...", Width = 54 };
    private readonly Label _a0 = new() { AutoSize = true, Text = "bit 5 (gen->0xA0) = 0" };
    private readonly CheckBox[] _sidesA = Lados();
    private readonly CheckBox[] _sidesB = Lados();
    private readonly ComboBox _p94 = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _init = new() { Text = "initialisation block ran", Checked = true,
                                              AutoSize = true };
    private readonly NumericUpDown _attempts = Numero(1, 20, 5);
    private readonly CheckBox _showDoors = new() { Text = "show each cell's doors",
                                                   AutoSize = true };
    private readonly CheckBox _showLinks = new() { Text = "show connections between cells",
                                                   Checked = true, AutoSize = true };
    /// <summary>
    /// O reskin `d*` do pos-processamento (41.18).  Fica desactivada nos modelos que nao tem
    /// familia nenhuma, e e' ligada sozinha quando a zona traz o `ExtraPassFlag` — que e'
    /// quando o cliente o faz.  Nao mexe na grelha gerada: aplica-se a uma copia, so' para o
    /// desenho, porque o passe corre depois do ponto que os conferidores medem.
    /// </summary>
    private readonly CheckBox _reskin = new() { Text = "apply the d* reskin (follows ExtraPassFlag)",
                                                AutoSize = true };
    /// <summary>
    /// O enchimento `z0` do pos-processamento (41.18).  Como o reskin, vai numa copia da
    /// grelha: a que a `Gera` devolve e' a que os conferidores medem.
    /// </summary>
    private readonly CheckBox _fill = new() { Text = "fill unreachable cells (z0)",
                                              AutoSize = true };

    /// <summary>
    /// As variantes **ICE** (`cell+0x0A`) e **`_ob0*`** (`cell+0x0C`), o ultimo passe do
    /// pos-processamento.  Implica o enchimento, porque corre depois dele e **no mesmo `mt1`**: o
    /// enchimento deixa o fluxo adiantado e as variantes continuam dali.
    /// <para>A elegibilidade esta' medida em 306 de 306 celulas das 13 zonas da sonda R (`maxIce
    /// &gt;= 1` para o ICE, `maxOb &gt; 1` para o `_ob0*`, e so' em chunks de uma celula).  O
    /// **valor** do ICE e' um valor de uma lista de candidatos — `0..maxIce-1` menos a variante
    /// dos vizinhos do mesmo chunk ligados por porta, num `i*`/`l*` com tres ou mais candidatos —
    /// e esta' medido em 894 de 894 celulas na corrida AJ+AK; o do `_ob0*` e' um por zona e
    /// **determinista** — o `id` do campo 1 da `DecorationTable`, ver
    /// <see cref="MapGen.ObIdDaZona"/>.
    /// Ver o <see cref="MapGen.PasseVariantes"/>.</para>
    /// <para><b>Com a opcao ligada abre a tabela</b> debaixo da pre-visualizacao: uma linha por
    /// celula, com o chunk, a rotacao, o ICE, o `_ob0*` e o nome do `.set` que o estagio 6
    /// entrega ao carregador.  Sem ela a opcao nao se via — o mapa desenha nome e rotacao e as
    /// variantes vivem noutras duas words, por isso parecia igual ao enchimento (55.9).</para>
    /// </summary>
    private readonly CheckBox _variantes = new() { Text = "assign the ICE / _ob0* variants",
                                                   AutoSize = true };

    // --- o mapa e as suas zonas
    private readonly ComboBox _zoneList = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    /// <summary>As variantes, quando o que se abriu foi uma quest.  Escondido nos outros casos.</summary>
    private readonly ComboBox _variantList = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    /// <summary>
    /// A linha das variantes.  Esta e' a unica das tres linhas de cima que mantem a altura
    /// escrita: tem uma lista a <c>Fill</c> dentro, e um painel em <c>AutoSize</c> com um filho
    /// a preencher nao sabe que altura pedir (media-se, e pedia 100 em vez de 28).  Os 28
    /// servem a qualquer escala porque o AutoScale os multiplica e a folga <b>cresce</b>: a
    /// lista tira a altura da fonte, que sobe menos do que a escala.
    /// </summary>
    private readonly Panel _variantRow = new() { Dock = DockStyle.Top, Height = 28,
                                                 Visible = false };
    private Quest? _quest;
    private readonly Label _source = new() { AutoSize = true, ForeColor = SystemColors.GrayText,
                                             Text = "No map loaded - open a packet or start a new map" };

    // --- botoes
    private readonly Button _open = new() { Text = "Open BIN/JSON..." };
    private readonly Button _generate = new() { Text = "Generate map" };
    private readonly Button _export = new() { Text = "Export...", Enabled = false };
    private readonly Button _newMap = new() { Text = "New map...", AutoSize = true,
                                              AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _addZone = new() { Text = "+", Width = 28 };
    private readonly Button _delZone = new() { Text = "-", Width = 28 };

    // --- saida
    private readonly Label _status = new() { AutoSize = true, Text = "Ready" };

    /// <summary>
    /// O registo por baixo da pre-visualizacao.  A linha de estado ficava com texto a mais — a
    /// nota da geracao, o aviso do `ExtraPassFlag`, o reskin e o enchimento nao cabem numa
    /// linha so' e saiam fora da janela.  Fica la' em cima so' o essencial de uma linha; tudo
    /// o resto vem para aqui, uma linha por assunto, com a mais recente no topo.
    /// </summary>
    private readonly TextBox _log = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font(FontFamily.GenericMonospace, 8.25f),
        // sem isto as notas longas — o aviso do ExtraPassFlag, a lista de ambiguidades do
        // reskin — corriam para fora da caixa em vez de quebrarem
        WordWrap = true,
        TabStop = false,
    };
    /// <summary>
    /// A tabela do que o passe das variantes decidiu, por baixo da pre-visualizacao: uma linha
    /// por celula, com o ICE, o `_ob0*` e o nome do `.set` que o cliente vai pedir.
    ///
    /// <para>Sem isto a opcao «assign the ICE / _ob0* variants» nao se via: o mapa continuava a
    /// mostrar nome e rotacao, igual ao enchimento, e as duas linhas de registo diziam quantas
    /// celulas tinham recebido um id mas nao <b>quais</b> nem <b>qual</b>.  Esta caixa so'
    /// aparece com a opcao ligada (a linha da tabela vai a zero quando nao ha nada para
    /// mostrar).</para>
    /// </summary>
    private readonly TextBox _variantesTabela = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
        WordWrap = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Consolas", 8.75f), TabStop = false, Visible = false,
    };

    /// <summary>A grelha da direita, para se poder abrir e fechar a linha da tabela.</summary>
    private TableLayoutPanel? _direita;

    private readonly TextBox _preview = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10f),
        BackColor = Color.FromArgb(24, 24, 28),
        ForeColor = Color.FromArgb(220, 220, 210),
        BorderStyle = BorderStyle.FixedSingle,
        TabStop = false,          // senao apanha o foco ao arrancar e sai tudo seleccionado
    };

    private readonly List<ZoneInput> _zones = new();
    private int _atual = -1;
    private bool _aCarregar;

    public JanelaPrincipal()
    {
        // A escala por DPI, para a janela servir num 4K.  Todos os pixeis escritos a' mao
        // aqui dentro — larguras, alturas, margens, enchimentos, as colunas absolutas dos
        // TableLayoutPanel — foram escritos a 96 DPI, a escala de 100% do Windows.  A 150% ou
        // a 200% as letras crescem sozinhas (a fonte vai em pontos) e estes numeros nao, e era
        // isso que cortava o texto dos botoes e das caixas.
        //
        // Com `AutoScaleMode.Dpi` e a dimensao de referencia posta nos 96 DPI, o WinForms faz
        // essa multiplicacao a tudo de uma vez, na primeira passagem de layout: a razao e'
        // `DeviceDpi / 96`.  Dois cuidados que isto obriga:
        //
        //   * tem de ficar **antes** de os controlos entrarem e o layout tem de estar
        //     suspenso.  Pôr a dimensao com o layout a correr escalava o que ja' estivesse
        //     la' (nada) e dava a escala por feita — o resto entrava depois, sem escala.
        //     E' por isso que o `ResumeLayout` esta' no fim do construtor.
        //   * nenhum numero deste ficheiro e' multiplicado a' mao: o AutoScale ja' faz essa
        //     conta, e as duas juntas davam o quadrado da escala.  O que precisa de conversao
        //     e' o contrario — uma medida que venha em pixeis do ecra tem de passar a logica
        //     antes de entrar no layout, e e' para isso que serve a `Escala.Logico`.  Aqui nao
        //     ha nenhuma; nas caixas que medem texto ha (ver `Escala`).
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(Escala.DpiBase, Escala.DpiBase);

        Text = "PSO2 map preview - packet 0x03-0x24";
        // o painel da esquerda leva 430; abaixo disto a pre-visualizacao deixava de caber
        // (em pixeis de 96 DPI: o AutoScale multiplica estes dois como multiplica o resto)
        MinimumSize = new Size(960, 520);
        Size = new Size(1120, 690);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        _unkB.Items.AddRange(new object[] { "0 - fixed (.lat)", "1 - random", "2 - river" });
        _unkB.SelectedIndex = 1;
        _p94.Items.AddRange(new object[] { "automatic (from HeightMode)",
                                           "0", "1", "2", "3", "4" });
        _p94.SelectedIndex = 0;
        // a lista mostra `330 - Ruins`, mas o que se guarda e se exporta e' sempre o numero: os
        // nomes vem do `models.txt`, que se edita a' mao (ver ModelNames)
        foreach (int m in GameData.AvailableModels())
            _model.Items.Add(ModelNames.Etiqueta(m));
        if (_model.Items.Count > 0)
            _model.SelectedIndex = 0;
        _seed.Text = "0";

        MontaLayout();

        _open.Click += (_, _) => Abre();
        _newMap.Click += (_, _) => MapaNovo();
        _addZone.Click += (_, _) => AcrescentaZona();
        _delZone.Click += (_, _) => RemoveZona();
        _generate.Click += (_, _) => Gera();
        _export.Click += (_, _) => Exporta();
        _zoneList.SelectedIndexChanged += (_, _) => TrocaZona();
        _variantList.SelectedIndexChanged += (_, _) => TrocaVariante();
        _unkN.TextChanged += (_, _) => AtualizaA0();
        _unkNBits.Click += (_, _) => MexeNosBits();
        // a semente e' um u32: no campo so' entram digitos, e o que la' estiver e'
        // normalizado para a forma sem sinal mal se sai do campo
        _seed.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        };
        _seed.Leave += (_, _) => _seed.Text = unchecked((uint)Semente(_seed.Text)).ToString();
        _model.TextChanged += (_, _) => { EnchePW(); EncheQY(); };
        _unkP.TextChanged += (_, _) => EncheQY();
        _unkW.TextChanged += (_, _) => EncheQY();
        _showDoors.CheckedChanged += (_, _) => Gera();
        _showLinks.CheckedChanged += (_, _) => Gera();
        _reskin.CheckedChanged += (_, _) => Gera();
        _fill.CheckedChanged += (_, _) => Gera();
        _variantes.CheckedChanged += (_, _) => Gera();
        AcceptButton = _generate;

        // aqui e' que a escala por DPI corre, com tudo ja' montado
        ResumeLayout(performLayout: true);

        MapaNovo(1, perguntar: false);
    }

    // ---------------------------------------------------------------- layout

    private void MontaLayout()
    {
        // 1. os tres botoes, presos no topo e sempre visiveis
        // Tres colunas iguais num painel de 380 davam 121 px a cada botao, e o
        // "Open BIN/JSON..." nao cabe la' — saia cortado a "Open".  Em fluxo, cada botao fica
        // com a largura do seu proprio texto, que e' o que resolve a qualquer DPI.
        // A altura era 34 escritos.  Com a escala por DPI ela passava a 68 a 200%, mas um
        // botao nao cresce so' pela escala — cresce pelo que a **letra** pede, e as duas contas
        // nao dao o mesmo numero.  Em `AutoSize` a fila fica com a altura dos botoes que tem
        // dentro, seja qual for a escala, e e' uma medida a' mao que deixa de existir.
        var botoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
            Padding = new Padding(8, 6, 8, 2),
        };
        foreach (var b in new[] { _open, _generate, _export })
        {
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Margin = new Padding(0, 0, 6, 0);
            botoes.Controls.Add(b);
        }

        // 2. o mapa e as suas zonas, tambem fora do scroll
        // Pela mesma razao da fila dos botoes, a altura nao vai escrita: as duas linhas desta
        // tabela tem dentro uma lista e botoes, cuja altura sai da fonte.  Com os 62 que aqui
        // estavam, a 150% a etiqueta "Zone" ficava com o pe' cortado pela borda do painel —
        // 62 vezes 1,5 nao e' o que a letra a 150% pede.
        var cabeca = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 5, RowCount = 2,
            Padding = new Padding(8, 2, 8, 6),
        };
        cabeca.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        cabeca.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabeca.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cabeca.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cabeca.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cabeca.Controls.Add(_source, 0, 0);
        cabeca.SetColumnSpan(_source, 5);
        cabeca.Controls.Add(new Label { Text = "Zone", AutoSize = true,
                                        Margin = new Padding(0, 6, 4, 0) }, 0, 1);
        _zoneList.Dock = DockStyle.Fill;
        cabeca.Controls.Add(_zoneList, 1, 1);
        cabeca.Controls.Add(_newMap, 2, 1);
        cabeca.Controls.Add(_addZone, 3, 1);
        cabeca.Controls.Add(_delZone, 4, 1);
        _newMap.Margin = _addZone.Margin = _delZone.Margin = new Padding(3, 0, 0, 0);

        // 2b. as variantes da quest, so' quando o que se abriu foi uma quest
        var etiquetaVariante = new Label
        {
            Text = "Variant", AutoSize = true, Dock = DockStyle.Left,
            Padding = new Padding(0, 6, 4, 0),
        };
        _variantList.Dock = DockStyle.Fill;
        _variantRow.Padding = new Padding(8, 0, 8, 4);
        _variantRow.Controls.Add(_variantList);
        _variantRow.Controls.Add(etiquetaVariante);

        // 3. os campos, a rolar
        var campos = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true,
            Padding = new Padding(8, 4, 8, 8),
        };
        // A coluna das etiquetas tinha 116 px fixos e os nomes compridos — `BigChunk1Variant`,
        // `ExitMinDistance`, `OpenBranchCap` — quebravam para duas linhas noutro DPI.  Em
        // AutoSize ela fica com o que a etiqueta mais larga precisa, seja qual for a fonte.
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));

        Seccao(campos, "the zone", Ajuda.Zones);
        Linha(campos, "ZoneIndex", _zoneIndex, Ajuda.ZoneIndex);
        Linha(campos, "ModelId", _model, Ajuda.ModelId);
        Linha(campos, "Seed", _seed, Ajuda.Seed);
        Linha(campos, "MapWidth", _sizeX, Ajuda.MapSize, "MapWidth");
        Linha(campos, "MapHeight", _sizeY, Ajuda.MapSize, "MapHeight");
        Linha(campos, "MapType", _unkB, Ajuda.MapType, "MapType");
        Linha(campos, "LatNumber", _arguments, Ajuda.LatNumber, "LatNumber");
        Linha(campos, "BigChunk1", _unkP, Ajuda.BigChunk, "BigChunk1");
        Linha(campos, "BigChunk1Variant", _unkQ, Ajuda.BigChunkVariant, "BigChunk1Variant");
        Linha(campos, "BigChunk2", _unkW, Ajuda.BigChunk, "BigChunk2");
        Linha(campos, "BigChunk2Variant", _unkY, Ajuda.BigChunkVariant, "BigChunk2Variant");
        Linha(campos, "ExitMinDistance", _unk3, Ajuda.ExitMinDistance, "MapGenTable/ExitMinDistance");
        Linha(campos, "OpenBranchCap", _unk4, Ajuda.OpenBranchCap, "MapGenTable/OpenBranchCap");
        // o `UnkN` leva o botao ao lado: o campo e' um conjunto de flags e nao um numero, por
        // isso quem o quiser mexer mexe-o bit a bit (41.2)
        var comBits = new TableLayoutPanel
        {
            ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0),
            Dock = DockStyle.Fill,
        };
        comBits.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        comBits.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        _unkN.Dock = DockStyle.Fill;
        _unkNBits.Margin = new Padding(4, 0, 0, 0);
        comBits.Controls.Add(_unkN, 0, 0);
        comBits.Controls.Add(_unkNBits, 1, 0);
        Linha(campos, "ZoneFlags", comBits, Ajuda.ZoneFlags, "ZoneFlags");
        Largo(campos, _a0);

        Seccao(campos, "the sides   0=N  1=E  2=S  3=W", Ajuda.MapA);
        Linha(campos, "mapA (in)", Caixa(_sidesA), Ajuda.MapA);
        Linha(campos, "mapB (out)", Caixa(_sidesB), Ajuda.MapB);

        Seccao(campos, "not carried by the packet", Ajuda.HeightMode);
        Linha(campos, "HeightMode", _p94, Ajuda.HeightMode, "HeightMode");
        Linha(campos, "init block", _init, Ajuda.Init);
        Linha(campos, "attempts", _attempts, Ajuda.Attempts);

        Seccao(campos, "preview", Ajuda.Preview);
        Largo(campos, _showLinks);
        Largo(campos, _showDoors);
        Largo(campos, _reskin);
        Largo(campos, _fill);
        LargoComInfo(campos, _variantes, "ICE / _ob0* variants", Ajuda.Variantes);

        // 380 era a conta antiga, com a coluna das etiquetas a 116.  Com ela em AutoSize as
        // etiquetas longas pedem mais, e sem esta folga era o campo que encolhia.
        var esquerda = new Panel { Dock = DockStyle.Left, Width = 430 };
        esquerda.Controls.Add(campos);
        esquerda.Controls.Add(_variantRow);
        esquerda.Controls.Add(cabeca);
        esquerda.Controls.Add(botoes);

        var direita = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(10, 8, 10, 10),
        };
        direita.RowCount = 4;
        direita.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        direita.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // a tabela das variantes: fechada (altura 0) ate' a opcao a pedir
        direita.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        direita.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        _status.Margin = new Padding(3, 3, 3, 8);
        _variantesTabela.Margin = new Padding(3, 6, 3, 0);
        _log.Margin = new Padding(3, 6, 3, 0);
        direita.Controls.Add(_status, 0, 0);
        direita.Controls.Add(_preview, 0, 1);
        direita.Controls.Add(_variantesTabela, 0, 2);
        direita.Controls.Add(_log, 0, 3);
        _direita = direita;

        // o docking resolve-se do fim da coleccao para o principio: o `Fill` entra primeiro
        Controls.Add(direita);
        Controls.Add(esquerda);
    }

    private static NumericUpDown Numero(int min, int max, int valor)
        => new() { Minimum = min, Maximum = max, Value = valor, Width = 90 };

    private static CheckBox[] Lados()
        => new[] { "N", "E", "S", "W" }.Select(s => new CheckBox
        {
            Text = s, AutoSize = true, Margin = new Padding(0, 3, 6, 3)
        }).ToArray();

    private static Control Caixa(CheckBox[] cs)
    {
        var p = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0),
                                      WrapContents = false };
        p.Controls.AddRange(cs);
        return p;
    }

    /// <summary>Um cabecalho de seccao, tambem com o seu [i].</summary>
    private void Seccao(TableLayoutPanel t, string titulo, string ajuda)
    {
        int r = t.RowCount++;
        t.Controls.Add(new Label
        {
            Text = titulo.ToUpperInvariant(),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 12, 0, 4),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
        }, 0, r);
        t.SetColumnSpan(t.GetControlFromPosition(0, r)!, 2);
        t.Controls.Add(BotaoInfo(titulo, ajuda), 2, r);
    }

    /// <summary>
    /// Uma linha de campo.  <paramref name="chave"/> e' o nome do campo no `block.json`
    /// (<c>MapGenTable/ExitMinDistance</c> para os de dentro da tabela); serve so' para o [i]
    /// poder dizer como o campo se chamava antes.  Null nas linhas que nao sao campos do
    /// ficheiro, como o `mapA (in)` ou o `attempts`.
    /// </summary>
    private void Linha(TableLayoutPanel t, string rotulo, Control c, string ajuda,
                       string? chave = null)
    {
        int r = t.RowCount++;
        t.Controls.Add(new Label { Text = rotulo, AutoSize = true, Anchor = AnchorStyles.Left,
                                   Margin = new Padding(0, 6, 6, 3) }, 0, r);
        c.Anchor = c is CheckBox or FlowLayoutPanel
            ? AnchorStyles.Left
            : AnchorStyles.Left | AnchorStyles.Right;
        t.Controls.Add(c, 1, r);
        t.Controls.Add(BotaoInfo(rotulo, ajuda, chave), 2, r);
    }

    private void Largo(TableLayoutPanel t, Control c)
    {
        int r = t.RowCount++;
        c.Anchor = AnchorStyles.Left;
        t.Controls.Add(c, 0, r);
        t.SetColumnSpan(c, 2);
    }

    /// <summary>
    /// O mesmo que o <see cref="Largo"/>, mas com o [i] na terceira coluna.  As caixas da
    /// pre-visualizacao nao tinham ajuda propria e a das variantes precisa de uma: o texto dela
    /// enchia o cabecalho da tabela, que e' o sitio onde so' deve estar o que muda de zona para
    /// zona.
    /// </summary>
    private void LargoComInfo(TableLayoutPanel t, Control c, string titulo, string ajuda)
    {
        int r = t.RowCount++;
        c.Anchor = AnchorStyles.Left;
        t.Controls.Add(c, 0, r);
        t.SetColumnSpan(c, 2);
        t.Controls.Add(BotaoInfo(titulo, ajuda), 2, r);
    }

    /// <summary>
    /// O [i] de cada campo.  E' um botao e nao um tooltip de propósito: o tooltip desaparece ao
    /// fim de uns segundos e estes textos sao para ler com calma.
    /// </summary>
    private Button BotaoInfo(string titulo, string ajuda, string? chave = null)
    {
        var b = new Button
        {
            Text = "i",
            Width = 22,
            Height = 22,
            Margin = new Padding(4, 3, 0, 0),
            Anchor = AnchorStyles.Left,
            FlatStyle = FlatStyle.System,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            TabStop = false,
        };
        // Nao e' um MessageBox: o do ZoneFlags tem 58 linhas e fazia uma janela maior do que
        // o ecra.  O Ajuda.Mostra poe tecto a' janela e desliza o que nao couber.
        b.Click += (_, _) => Ajuda.Mostra(this, titulo.Trim(), ajuda, chave);
        return b;
    }

    // ---------------------------------------------------------------- o mapa

    /// <summary>
    /// Um botao so' para os dois: o packet `0x03-0x24` e o `block.json` do servidor.  Quem
    /// decide e' a extensao do ficheiro escolhido, e nao um botao separado.
    /// </summary>
    private void Abre()
    {
        using var d = new OpenFileDialog
        {
            Title = "Open a 0x03-0x24 capture, a quest.json or a block.json",
            Filter = "packets, quests and blocks (*.bin;*.json)|*.bin;*.json|" +
                     "packet captures (*.bin)|*.bin|Starlight quests and blocks (*.json)|*.json|" +
                     "all files (*.*)|*.*",
        };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        if (!Path.GetExtension(d.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
            CarregaPacket(d.FileName, 0);
        else if (Quest.Parece(d.FileName))
            CarregaQuest(d.FileName);
        else
            CarregaBlocos(d.FileName);
    }

    /// <summary>
    /// Abre uma quest inteira: le' o `quest.json`, enche a lista de variantes e mostra a
    /// primeira que tenha blocos.
    ///
    /// <para>Nao e' o mesmo que abrir a pasta de uma variante.  A quest diz <b>que</b> blocos e'
    /// que carrega e <b>por que ordem</b>, e isso nem sempre e' o que esta' no disco — ha' quests
    /// com um `b4` ou um `.zip` ao lado que elas nao carregam.  Como a lista de entradas e'
    /// repartida por posicao, um bloco a mais desalinha-a.</para>
    /// </summary>
    public void CarregaQuest(string caminho, string? variante = null)
    {
        Quest q;
        try
        {
            q = Quest.Le(caminho);
        }
        catch (Exception e)
        {
            MessageBox.Show(this, "Could not read that quest.json:\n\n" + e.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (q.Variantes.Count == 0)
        {
            MessageBox.Show(this, $"\"{q.Nome}\" has no variant folder beside its quest.json.",
                            "Nothing to show", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _quest = q;
        _aCarregar = true;
        _variantList.Items.Clear();
        foreach (var v in q.Variantes) _variantList.Items.Add(v);
        _aCarregar = false;
        _variantRow.Visible = true;

        int i = variante is null
            ? q.Variantes.FindIndex(v => v.Blocos > 0)
            : q.Variantes.FindIndex(v => string.Equals(v.Nome, variante,
                                                       StringComparison.OrdinalIgnoreCase));
        _variantList.SelectedIndex = Math.Max(0, i);   // dispara o TrocaVariante
    }

    /// <summary>Sai do modo quest: o que se abriu agora nao veio de um `quest.json`.</summary>
    private void EsqueceAQuest()
    {
        _quest = null;
        _variantRow.Visible = false;
        _aCarregar = true;
        _variantList.Items.Clear();
        _aCarregar = false;
    }

    private void TrocaVariante()
    {
        if (_aCarregar || _quest is null || _variantList.SelectedItem is not Variante v)
            return;

        List<ZoneInput> zonas;
        List<string> faltam;
        try
        {
            zonas = _quest.Zonas(v, out faltam);
        }
        catch (Exception e)
        {
            _source.Text = $"{_quest.Nome} - variant {v.Nome}: {e.Message}";
            _zones.Clear();
            RefrescaLista(0);
            return;
        }

        _zones.Clear();
        _zones.AddRange(zonas);
        string aviso = faltam.Count > 0
            ? $"  -  {string.Join(", ", faltam)} missing from this variant"
            : "";
        _source.Text = $"{_quest.Nome} - variant {v.Nome}, {zonas.Count} block(s): "
                       + string.Join(", ", zonas.Select(
                             z => $"{Path.GetFileName(Path.GetDirectoryName(z.Ficheiro ?? ""))}"
                                  + $"=zone {z.ZoneIndex}"))
                       + aviso;
        RefrescaLista(0);
    }

    /// <summary>Le' um packet e enche a lista de zonas.  Tambem serve a linha de comandos.</summary>
    public void CarregaPacket(string caminho, int zona)
    {
        Packet0324 pk;
        try
        {
            pk = Packet0324.Load(caminho);
        }
        catch (Exception e)
        {
            MessageBox.Show(this, "Could not read that packet:\n\n" + e.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var zonas = pk.Zonas();
        if (zonas.Count == 0)
        {
            MessageBox.Show(this, "That packet carries no zones.", "Nothing to show",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        EsqueceAQuest();
        _zones.Clear();
        _zones.AddRange(zonas);
        _source.Text = $"{Path.GetFileName(caminho)} - world \"{pk.WorldName}\", " +
                       $"{zonas.Count} zones";
        int i = _zones.FindIndex(z => z.ZoneIndex == zona);
        RefrescaLista(i >= 0 ? i : 0);
    }

    /// <summary>
    /// Le' um `block.json` <b>e os irmaos ao lado dele</b>, porque um bloco sozinho nao chega:
    /// o mapA de uma zona precisa dos warps que chegam a ela, que estao nos outros blocos, e da
    /// lista inteira do `zoneUnknown1`, que so' se reconstroi juntando todos.
    /// </summary>
    public void CarregaBlocos(string caminho, int zona = int.MinValue)
    {
        List<ZoneInput> zonas;
        try
        {
            zonas = BlockRead.Le(caminho);
        }
        catch (Exception e)
        {
            MessageBox.Show(this, "Could not read that block.json:\n\n" + e.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (zonas.Count == 0)
        {
            MessageBox.Show(this, "No block.json found there.", "Nothing to show",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        EsqueceAQuest();
        _zones.Clear();
        _zones.AddRange(zonas);
        string pasta = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(
                           Path.GetFullPath(zonas[0].Ficheiro ?? caminho))) ?? "") ;
        _source.Text = $"{(pasta.Length > 0 ? pasta + " - " : "")}{zonas.Count} block(s): " +
                       string.Join(", ", zonas.Select(
                           z => $"{Path.GetFileName(Path.GetDirectoryName(z.Ficheiro ?? ""))}" +
                                $"=zone {z.ZoneIndex}"));
        int i = zona != int.MinValue ? _zones.FindIndex(z => z.ZoneIndex == zona)
                                     : _zones.FindIndex(z => File.Exists(caminho)
                                           && string.Equals(Path.GetFullPath(z.Ficheiro ?? ""),
                                                            Path.GetFullPath(caminho),
                                                            StringComparison.OrdinalIgnoreCase));
        RefrescaLista(i >= 0 ? i : 0);
    }

    private void MapaNovo() => MapaNovo(0, perguntar: true);

    private void MapaNovo(int quantas, bool perguntar)
    {
        if (perguntar)
        {
            if (!PedeNumero("New map", "How many zones should the map have?", 1, 32, 3,
                            out quantas))
                return;
        }
        EsqueceAQuest();
        _zones.Clear();
        int modelo = ModeloPorOmissao();
        for (int i = 0; i < Math.Max(1, quantas); i++)
            _zones.Add(new ZoneInput
            {
                ZoneIndex = i,
                Model = modelo,
                Seed = 0,
                SizeX = 3,
                SizeY = 3,
                UnkB = 1,
                Arguments = -1,
                Unk3 = -1,
                // o `UnkN` nasce com o valor habitual da area e nao a zero: zero nao e' um valor
                // que alguma das 1641 zonas do corpus tenha, e o bit 5 dele decide o mapa (41.2)
                UnkN = InfoTables.ExtraCampo(modelo, 1, i, "UnkN", 0),
                A0 = (InfoTables.ExtraCampo(modelo, 1, i, "UnkN", 0) >> 5) & 1,
                // o `UnkO` traz o gen->0x94 no byte 0 (41.7), por isso tem de estar posto antes
                // de se desenhar o mapa e nao so' na exportacao
                UnkO = InfoTables.ExtraCampo(modelo, 1, i, "UnkO", 0),
                TemUnkO = true,
                LadosA = new List<int> { 2 },
                LadosB = new List<int> { 0 },
                NA = 1,
                OrdemBloco = i,
            });
        _source.Text = perguntar
            ? $"New map - {_zones.Count} zones, nothing loaded from a packet"
            : "No map loaded - open a packet or start a new map";
        RefrescaLista(0);
    }

    /// <summary>
    /// O modelo com que uma zona nova arranca.  O primeiro `.lps` da pasta e' o `sn_0007`, que
    /// nao gera mapa nenhum — abrir a ferramenta com "no map" era mau cartao de visita, por isso
    /// prefere-se uma area normal.
    /// </summary>
    private int ModeloPorOmissao()
    {
        var ids = _model.Items.Cast<object>().Select(o => ModelNames.Id(o.ToString(), -1)).ToList();
        foreach (int m in new[] { 330, 311, 310 })
            if (ids.Contains(m))
                return m;
        return ids.Count > 0 ? ids[0] : 0;
    }

    private void AcrescentaZona()
    {
        GuardaCampos();
        var nova = (_atual >= 0 ? _zones[_atual].Clone() : new ZoneInput());
        // uma zona nova nao herda o que veio do packet: e' feita a' mao.  Nem o ficheiro de
        // onde a outra veio — senao gravar escrevia-a por cima do bloco vizinho.
        nova.Ficheiro = null;
        nova.ComoVeio = null;
        nova.Settings = null;
        nova.Extra = null;
        nova.It1 = nova.It2 = null;
        nova.It3 = null;
        nova.Warps = new List<Warp>();
        nova.Zu1 = new List<(int, int)>();
        nova.ZoneIndex = _zones.Count == 0 ? 0 : _zones.Max(z => z.ZoneIndex) + 1;
        nova.OrdemBloco = _zones.Count;
        _zones.Add(nova);
        RefrescaLista(_zones.Count - 1);
    }

    private void RemoveZona()
    {
        if (_zones.Count <= 1 || _atual < 0)
            return;
        _zones.RemoveAt(_atual);
        RefrescaLista(Math.Min(_atual, _zones.Count - 1));
    }

    /// <summary>
    /// O mapa veio de `block.json`?  Entao o botao <b>guarda</b> por cima desses ficheiros, em
    /// vez de exportar para outro lado: exportar e' o caminho de um packet para um `block.json`
    /// novo, e um mapa que ja' tem ficheiros nao precisa dele.
    /// </summary>
    private bool ModoGuardar => _zones.Any(z => z.Ficheiro is not null);

    private void RefrescaLista(int escolher)
    {
        _export.Text = ModoGuardar ? "Save" : "Export...";
        _aCarregar = true;
        _zoneList.Items.Clear();
        foreach (var z in _zones)
            _zoneList.Items.Add($"Zone {z.ZoneIndex} - model {z.Model}, {z.SizeX}x{z.SizeY}, " +
                                $"MapType={z.UnkB}");
        _aCarregar = false;
        _atual = -1;
        if (_zones.Count > 0)
            _zoneList.SelectedIndex = Math.Clamp(escolher, 0, _zones.Count - 1);
        _delZone.Enabled = _zones.Count > 1;
    }

    private void TrocaZona()
    {
        if (_aCarregar || _zoneList.SelectedIndex < 0)
            return;
        GuardaCampos();
        _atual = _zoneList.SelectedIndex;
        PoeNosCampos(_zones[_atual]);
        Gera();
    }

    // ---------------------------------------------------------------- campos

    private void PoeNosCampos(ZoneInput z)
    {
        _aCarregar = true;
        _zoneIndex.Value = Math.Clamp(z.ZoneIndex, (int)_zoneIndex.Minimum, (int)_zoneIndex.Maximum);
        _model.Text = ModelNames.Etiqueta(z.Model);
        _seed.Text = unchecked((uint)z.Seed).ToString();     // sempre sem sinal
        _sizeX.Value = Math.Clamp(z.SizeX, (int)_sizeX.Minimum, (int)_sizeX.Maximum);
        _sizeY.Value = Math.Clamp(z.SizeY, (int)_sizeY.Minimum, (int)_sizeY.Maximum);
        _unkB.SelectedIndex = Math.Clamp(z.UnkB, 0, 2);
        _arguments.Text = z.Arguments.ToString();
        EnchePW();
        _unkP.Text = BigChunks.Etiqueta(z.UnkP);
        _unkW.Text = BigChunks.Etiqueta(z.UnkW);
        EncheQY();
        _unkQ.Text = z.UnkQ.ToString();
        _unkY.Text = z.UnkY.ToString();
        _unk3.Value = Math.Clamp(z.Unk3, (int)_unk3.Minimum, (int)_unk3.Maximum);
        _unk4.Value = Math.Clamp(z.Unk4, (int)_unk4.Minimum, (int)_unk4.Maximum);
        _unkN.Text = z.UnkN.ToString();
        for (int d = 0; d < 4; d++)
        {
            _sidesA[d].Checked = z.LadosA?.Contains(d) ?? false;
            _sidesB[d].Checked = z.LadosB?.Contains(d) ?? false;
        }
        _p94.SelectedIndex = z.P94 is int p && p >= 0 && p <= 4 ? p + 1 : 0;
        _init.Checked = z.InitCorreu;
        // O reskin NAO e' uma preferencia de quem ve: o cliente so' o faz quando o bit 0 do
        // `MapGenTable.ExtraPassFlag` esta' posto, e esse vem do packet ou do `block.json`.  A
        // caixa passa a seguir o dado da zona em vez de ficar onde o utilizador a deixou na
        // zona anterior; continua a poder ser desligada e ligada a' mao, e a linha de estado
        // diz quando o que esta' na caixa discorda dos dados.
        _reskin.Checked = (z.Unk5 & 1) != 0;
        _attempts.Value = Math.Clamp(z.Tentativas, (int)_attempts.Minimum, (int)_attempts.Maximum);
        _aCarregar = false;
        AtualizaA0();
    }

    /// <summary>Escreve o que esta' nos campos de volta na zona seleccionada.</summary>
    private void GuardaCampos()
    {
        if (_atual < 0 || _atual >= _zones.Count || _aCarregar)
            return;
        var z = _zones[_atual];
        z.ZoneIndex = (int)_zoneIndex.Value;
        z.Model = ModelNames.Id(_model.Text, 0);
        z.Seed = Semente(_seed.Text);
        z.SizeX = (int)_sizeX.Value;
        z.SizeY = (int)_sizeY.Value;
        z.UnkB = _unkB.SelectedIndex;
        z.Arguments = Inteiro(_arguments.Text, -1);
        z.UnkP = ModelNames.Id(_unkP.Text, 0);
        z.UnkW = ModelNames.Id(_unkW.Text, 0);
        z.UnkQ = Inteiro(_unkQ.Text, 0);
        z.UnkY = Inteiro(_unkY.Text, 0);
        z.Unk3 = (int)_unk3.Value;
        z.Unk4 = (int)_unk4.Value;
        z.UnkN = Inteiro(_unkN.Text, 0);
        z.A0 = (z.UnkN >> 5) & 1;
        var la = Enumerable.Range(0, 4).Where(d => _sidesA[d].Checked).ToList();
        var lb = Enumerable.Range(0, 4).Where(d => _sidesB[d].Checked).ToList();
        z.LadosA = la.Count > 0 ? la : null;
        z.LadosB = lb;
        z.NA = Math.Max(1, la.Count);
        z.P94 = _p94.SelectedIndex <= 0 ? null : _p94.SelectedIndex - 1;
        z.InitCorreu = _init.Checked;
        z.Tentativas = (int)_attempts.Value;
    }

    /// <summary>
    /// Enche as caixas do `UnkP`/`UnkW` com os big chunks que esta area tem.  A etiqueta e'
    /// `13425 - q4`; o que se guarda e' o numero, como no ModelId.
    /// </summary>
    private void EnchePW()
    {
        int modelo = ModelNames.Id(_model.Text, 0);
        var nomes = new List<string>();
        try
        {
            foreach (var nome in GameData.GetArea(modelo).Chunks.Keys)
                if (nome.StartsWith("q", StringComparison.Ordinal))
                    nomes.Add(nome);
        }
        catch (Exception)
        {
            // sem `.lps` na pasta nao ha' sugestoes; a caixa continua a aceitar o que se escreva
        }
        nomes.Sort(StringComparer.Ordinal);
        foreach (var caixa in new[] { _unkP, _unkW })
        {
            string texto = caixa.Text;
            caixa.BeginUpdate();
            caixa.Items.Clear();
            caixa.Items.Add(BigChunks.Etiqueta(0));
            foreach (var nome in nomes)
                caixa.Items.Add(BigChunks.Etiqueta(BigChunks.Valor(nome)));
            caixa.EndUpdate();
            caixa.Text = texto;
        }
    }

    /// <summary>
    /// Enche as caixas do `UnkQ`/`UnkY` com as variantes que existem para o chunk que o
    /// `UnkP`/`UnkW` ao lado nomeia — a lista sai do `bigchunks.txt`.
    /// </summary>
    private void EncheQY()
    {
        int modelo = ModelNames.Id(_model.Text, 0);
        foreach (var (design, variante) in new[] { (_unkP, _unkQ), (_unkW, _unkY) })
        {
            var vs = BigChunks.VariantesDe(modelo, ModelNames.Id(design.Text, 0));
            string texto = variante.Text;
            variante.BeginUpdate();
            variante.Items.Clear();
            foreach (int v in vs)
                variante.Items.Add(v.ToString());
            variante.EndUpdate();
            variante.Text = texto;
            variante.Enabled = ModelNames.Id(design.Text, 0) != 0;
        }
    }

    /// <summary>Abre a caixa que mexe no <c>UnkN</c> bit a bit (ver DialogoBits).</summary>
    private void MexeNosBits()
    {
        int modelo = ModelNames.Id(_model.Text, 0);
        using var d = new DialogoBits(Inteiro(_unkN.Text, 0), modelo, _unkB.SelectedIndex,
                                     (int)_zoneIndex.Value);
        if (d.ShowDialog(this) != DialogResult.OK || d.Valor == Inteiro(_unkN.Text, 0))
            return;
        _unkN.Text = d.Valor.ToString();
        Gera();                     // o bit 5 muda o mapa, por isso vale a pena redesenhar
    }

    private void AtualizaA0()
    {
        int n = Inteiro(_unkN.Text, 0);
        _a0.Text = $"bit 5 (gen->0xA0) = {(n >> 5) & 1}" +
                   (((n >> 5) & 1) != 0 ? "  - one leftover side becomes an exit" : "");
    }

    // ---------------------------------------------------------------- gerar

    private void Gera()
    {
        if (_aCarregar)
            return;
        GuardaCampos();
        if (_atual < 0 || _atual >= _zones.Count)
            return;
        var z = _zones[_atual];
        Previsto p;
        try
        {
            p = Previsao.Gera(z);
        }
        catch (Exception e)
        {
            _preview.Text = "";
            Regista(new[] { e.ToString() });
            _status.Text = "Failed: " + e.Message;
            _export.Enabled = true;
            return;
        }
        _export.Enabled = true;
        // a lista mostra o modelo e o tamanho, por isso acompanha as alteracoes
        _aCarregar = true;
        _zoneList.Items[_atual] = $"Zone {z.ZoneIndex} - model {z.Model}, " +
                                  $"{z.SizeX}x{z.SizeY}, MapType={z.UnkB}";
        _aCarregar = false;
        if (!p.Ok)
        {
            _preview.Text = "";
            Regista(new[] { "no map: " + p.Nota });
            _status.Text = "No map: " + p.Nota;
            return;
        }
        // O reskin do pos-processamento vai numa COPIA da grelha: a que a `Gera` devolve e' a
        // que os conferidores comparam com os despejos do cliente, e esses sao tirados em
        // `0x00648829`, antes do passe (41.18).
        var paraDesenhar = p.Grelha!;
        var notas = new List<string>();
        // o texto da tabela das variantes; fica vazio quando a opcao esta desligada, e e' isso
        // que fecha a linha dela no layout
        string tabela = "";
        if (p.Aviso is not null)
            notas.Add(p.Aviso);
        Area? area = null;
        try { area = GameData.GetArea(z.Model); } catch { }
        bool temFamilia = area is not null && MapGen.MembrosDaFamilia(area).Count > 0;
        bool peloDado = (z.Unk5 & 1) != 0;
        _reskin.Enabled = temFamilia;
        if (!temFamilia && _reskin.Checked)
        {
            _aCarregar = true;
            _reskin.Checked = false;
            _aCarregar = false;
        }
        if ((temFamilia && _reskin.Checked) || _fill.Checked || _variantes.Checked)
            paraDesenhar = p.Grelha!.Clone();
        if (temFamilia && _reskin.Checked && area is not null)
        {
            var trocas = MapGen.AplicaReskin(paraDesenhar, area);
            var ambi = MapGen.AmbiguidadesDoReskin(area);
            notas.Add($"d* reskin: {trocas.Count} cell(s) renamed"
                      + (peloDado ? "" : " - FORCED ON; ExtraPassFlag is not set on this zone"));
            var tocadas = trocas.Select(t => t.De).Distinct().Where(ambi.ContainsKey)
                                .OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (tocadas.Count > 0)
                notas.Add("   picked among several for " + string.Join(", ",
                    tocadas.Select(n => $"{n} -> {string.Join("/", ambi[n])}")));
        }
        else if (peloDado && temFamilia)
            notas.Add("ExtraPassFlag IS set on this zone, but the d* reskin is turned off");
        // O enchimento vem DEPOIS do reskin, que e' a ordem do cliente: o passe do
        // `ExtraPassFlag` corre antes da varredura dos `z0` (41.17).
        if (_fill.Checked || _variantes.Checked)
        {
            // A rotacao do `z0` continua o MT1 de onde a geracao o deixou (41.20).  Usa-se um MT
            // novo avancado ate' ao mesmo ponto para nao mexer no estado do gerador.  E' **o
            // mesmo** `mt` que as variantes depois continuam: o fluxo e' continuo (47.10).
            Mt19937? mt = null;
            if (p.G is not null)
            {
                mt = new Mt19937(unchecked((uint)z.Seed));
                for (long i = 0; i < p.G.Mt1.Draws; i++)
                    mt.NextU32();
            }
            var cheias = MapGen.EnchimentoZ0(paraDesenhar, area, mt);
            notas.Add($"z0 fill: {cheias.Count} cell(s)"
                      + (mt is null ? " (fixed map: no MT, rotations left at 0)" : ""));
            // O colega notou isto: com o enchimento DESLIGADO e as variantes ligadas, as celulas
            // do enchimento e do segundo passe aparecem na mesma.  Nao e' defeito — e' a cadeia
            // do cliente: as variantes gastam palavras do mesmo `mt1` e tem de vir depois.  Mas
            // a janela nao o dizia, e ver os `z*` a aparecer com a caixa desmarcada parece um.
            if (_variantes.Checked && !_fill.Checked)
                notas.Add("   (run by the variants option - the client's own order; see its [i])");
            if (area is not null && MapGen.TemZModelados(area))
            {
                // o segundo passe, 0x005E8030: um terceiro MT, semeado com a semente da zona,
                // reesculpe parte do enchimento em corredores falsos (46)
                var mudadas = MapGen.SegundoPasseZ(paraDesenhar, area, z.Seed);
                notas.Add($"   second z pass (0x005E8030): {mudadas.Count} of them became "
                          + "false corridors - this model has the group 9 set (zn zi zl zt zx)");
            }
            // E por ultimo as variantes, que gastam palavras do mesmo `mt1` e por isso tem de
            // vir depois de tudo (47.10).
            if (_variantes.Checked && area is not null)
            {
                if (mt is null)
                {
                    // Num mapa fixo o passe corre igual no cliente e salta todas as celulas: o
                    // `.lat` ja' lhes declarou o ICE e o `_ob0*`.  Os ids existem e sao os do
                    // ficheiro, e e' isso que a tabela mostra (55.9).
                    MapGen.PasseVariantes(paraDesenhar, area, null, 0,
                                          MapGen.GrandesDaZona(z), escreveOb: false);
                    int sort = paraDesenhar.IceSorteada?.Count ?? 0;
                    notas.Add("ICE / _ob0* variants: fixed map - the ids come from the .lat, and "
                              + $"{sort} cell(s) marked ? were drawn (not reproducible); see [i]");
                    tabela = TabelaDeVariantes(paraDesenhar, area, z, mapaFixo: true);
                }
                else
                {
                    var escritas = MapGen.PasseVariantes(paraDesenhar, area, mt,
                                                         MapGen.ObIdDaZona(z),
                                                         MapGen.GrandesDaZona(z));
                    int nice = escritas.Count(e => e.Ice != 0xFFFF);
                    int nob = escritas.Count(e => e.Ob != 0xFFFF);
                    notas.Add($"ICE / _ob0* variants: {nice} cell(s) got an ICE id, "
                              + $"{nob} got an _ob0 id (DecorationTable.Field1 = "
                              + $"{MapGen.ObIdDaZona(z)}) - the table below says which; see [i]");
                    tabela = TabelaDeVariantes(paraDesenhar, area, z);
                }
            }
        }
        // O que o pos-processamento desta zona NAO leva sai sempre, com ou sem os passes
        // pedidos: um buraco conhecido nao deve depender de o utilizador ter marcado a opcao.
        // A lista e' a mesma que a CLI imprime (MapGen.EstadoDoPosProcessamento, 47.5).
        if (area is not null)
            foreach (var linha in MapGen.EstadoDoPosProcessamento(area, p.G is null))
                if (linha.StartsWith("summary:") || linha.StartsWith("not modelled")
                        || linha.StartsWith("CAREFUL"))
                    notas.Add(linha);
        _preview.Text = Previsao.Desenha(paraDesenhar, _showDoors.Checked, _showLinks.Checked)
                                .Replace("\n", "\r\n");
        _preview.SelectionStart = 0;
        _preview.SelectionLength = 0;
        MostraTabela(tabela);
        Regista(notas);
        string comum = $"model {z.Model}   seed {unchecked((uint)z.Seed)}   " +
                       $"{z.SizeX}x{z.SizeY}   {Nota(p.Nota)}";
        _status.Text = p.G is null
            ? $"{comum}   grid {p.Grelha!.W}x{p.Grelha.H}"
            : $"{comum}   {p.Pecas} pieces   grid {p.Grelha!.W}x{p.Grelha.H}   " +
              $"{p.G.Mt1.Draws} MT words";
    }

    /// <summary>A biblioteca ja devolve as notas em ingles (sao comparadas com o Python).</summary>
    private static string Nota(string nota) => nota;

    /// <summary>
    /// Escreve as notas desta geracao no registo de baixo, a mais recente no topo.  Substitui o
    /// bloco anterior em vez de acumular sem fim: cada passagem pela `Gera` descreve o estado
    /// completo, e um historico interminavel so' faria perder a linha que interessa.
    /// </summary>
    /// <summary>
    /// Abre ou fecha a linha da tabela das variantes.  Com a opcao desligada a altura vai a zero
    /// e a pre-visualizacao fica com o espaco todo, como antes de a tabela existir.
    /// </summary>
    private void MostraTabela(string texto)
    {
        bool mostra = texto.Length > 0;
        _variantesTabela.Visible = mostra;
        _variantesTabela.Text = mostra ? texto.Replace("\n", "\r\n") : "";
        _variantesTabela.SelectionStart = 0;
        _variantesTabela.SelectionLength = 0;
        if (_direita is not null && _direita.RowStyles.Count > 2)
            _direita.RowStyles[2].Height = mostra ? 200 : 0;
    }

    /// <summary>
    /// O texto da tabela: uma linha por celula que o passe das variantes tocou, com o ICE, o
    /// `_ob0*` e o nome do `.set` que o cliente lhe vai pedir.
    ///
    /// <para>O nome e' o que o <b>estagio 6</b> entrega ao carregador (FINDINGS 55), conferido
    /// contra 10 274 das 10 334 linhas dos despejos de hook.  O numero do fim e' o <b>valor</b>
    /// de um campo da <c>SetFileTable</c> da zona — 12, 13, 31, 111 — e <b>nao</b> um indice de
    /// 1 a 5, que era o que estava aqui a sair como <c>000N</c> (FINDINGS 56).  Os campos de
    /// chance 100000 a celula pede sempre; os outros sao sorteados celula a celula e aparecem
    /// entre <c>[</c> e <c>]</c>.</para>
    /// </summary>
    private static string TabelaDeVariantes(Grid gr, Area area, ZoneInput z,
                                           bool mapaFixo = false)
    {
        var linhas = MapGen.VariantesDaGrelha(gr, area);
        var sb = new StringBuilder();
        sb.Append($"ICE / _ob0* variants of model sn_{area.Model:0000}, zone {z.ZoneIndex}");
        sb.Append($"   -   {linhas.Count} cell(s)\n");
        // Num mapa fixo o nome e' o da zona (`NomeDeSetDeMapaFixo`) e a `SetFileTable` nao
        // entra nele, por isso nao se mostra uma lista de numeros que ali nao e' usada.
        var sets = mapaFixo ? new List<MapGen.SetFile>() : MapGen.SetFilesDaZona(z);
        // So' o que muda de zona para zona.  O porque' de tudo isto esta' no [i] da caixa.
        if (mapaFixo)
            sb.Append("fixed map: the .set name is the zone form, "
                      + MapGen.NomeDeSetDeMapaFixo(z) + " with N = 1..5.\n");
        else
        {
            // As chavetas faltavam aqui, e a segunda linha saia tambem nos mapas fixos.
            sb.Append(DescreveSetFiles(sets) + "\n");
            sb.Append("each cell draws its OWN list from those - one Twister per zone, seeded"
                      + " with the zone's\nSeed, five words per cell. The table below is that"
                      + " list, cell by cell.\n");
        }
        var grandes = MapGen.GrandesDaZona(z);
        if (grandes.Count > 0)
            sb.Append("big chunks of this zone (they ARE the candidate list of a multi-cell "
                      + "chunk): "
                      + string.Join(", ", grandes.Select(g => $"{g.Nome}={g.Valor}")) + "\n");
        // O sorteio dos `.set`: um rolador por zona, semeado com o Seed dela (FINDINGS 56.9).
        var sorteadas = mapaFixo || sets.Count == 0
            ? null
            : MapGen.SorteiaSetsDaGrelha(linhas, sets, z.Seed);
        sb.Append(mapaFixo
                  ? "\n cell  chunk  rot   ICE  _ob0\n"
                  : "\n cell  chunk  rot   ICE  _ob0   the .set files this cell loads\n");
        for (int k = 0; k < linhas.Count; k++)
        {
            var c = linhas[k];
            // O `?` diz que o ICE daquela celula saiu de um sorteio que nao se reproduz
            // (so' acontece no mapa fixo): o que esta' ali e' o primeiro candidato.
            string ice = c.Ice == 0xFFFF ? "  -" : $"{c.Ice:00}" + (c.Sorteada ? "?" : "");
            string ob = c.Ob == 0xFFFF ? "  -" : $"{c.Ob:00}";
            sb.Append($" {Previsao.Coluna(c.X)}{c.Y + 1,-4} {c.Chunk,-6} {c.Rot,-4} "
                      + $"{ice,4} {ob,5}"
                      + (mapaFixo ? "" : "   " + NomesDeUmaCelula(c, sets, sorteadas?[k]))
                      + "\n");
        }
        if (linhas.Count == 0)
            sb.Append(" (no cell in this zone takes a variant: every chunk here declares "
                      + "maxIce 0 and maxOb 1)\n");
        return sb.ToString();
    }

    /// <summary>
    /// Os nomes de `.set` de uma celula numa linha so'. **Nao e' uma escolha entre eles:** a
    /// celula carrega todos os campos cuja chance passe, e por isso isto e' uma LISTA.  Os que
    /// dependem do sorteio vao entre <c>[</c> e <c>]</c> com a percentagem (FINDINGS 56).
    /// </summary>
    private static string NomesDeUmaCelula(MapGen.CelulaVariante c,
                                           IReadOnlyList<MapGen.SetFile> sets,
                                           IReadOnlyList<int>? numeros = null)
    {
        // Os numeros ja' NAO sao os mesmos em todas as celulas: a semente do rolador e' a da
        // zona e gasta cinco palavras por celula, logo cada uma tem a sua lista (FINDINGS 56.9).
        if (c.SetBase.Length == 0)
            return "-";
        if (numeros is null)
            return sets.Count == 0
                ? c.SetBase + "_????.set   (this zone declares no set file)"
                : c.SetBase + "_";
        return numeros.Count == 0
            ? "(loads none)"
            : string.Join(" ", numeros.Select(n => $"{c.SetBase}_{n:0000}.set"));
    }

    /// <summary>O que a zona declara na <c>SetFileTable</c>, em uma linha.</summary>
    private static string DescreveSetFiles(IReadOnlyList<MapGen.SetFile> sets)
    {
        if (sets.Count == 0)
            return "this zone declares no set file in its SetFileTable.";
        var partes = sets.Select(sf => sf.Certo
            ? $"{sf.Numero:0000} always"
            : $"{sf.Numero:0000} in {sf.Percentagem}% of cells"
              + (sf.Plana ? "" : $" (varies by slot: {sf.ChanceMin / 1000}..{sf.Percentagem}%)"));
        return "SetFileTable of this zone: " + string.Join(", ", partes);
    }

    private void Regista(IEnumerable<string> notas)
    {
        var l = notas.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        _log.Text = l.Count == 0 ? "" : string.Join(Environment.NewLine, l);
        _log.SelectionStart = 0;
        _log.SelectionLength = 0;
    }

    // ---------------------------------------------------------------- exportar

    private void Exporta()
    {
        GuardaCampos();
        if (_atual < 0)
            return;
        if (ModoGuardar)
        {
            Guarda();
            return;
        }
        bool todas = false;
        if (_zones.Count > 1)
        {
            var r = MessageBox.Show(this,
                $"This map has {_zones.Count} zones.\n\n" +
                "Yes - export every zone into a folder (b1, b2, b3...)\n" +
                "No  - export only the zone being shown",
                "Export", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel)
                return;
            todas = r == DialogResult.Yes;
        }

        if (!todas)
        {
            using var d = new SaveFileDialog
            {
                Title = "Save block.json",
                Filter = "block.json|*.json",
                FileName = "block.json",
            };
            if (d.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                BlockJson.Reconcilia(_zones);
                File.WriteAllText(d.FileName, BlockJson.Escreve(_zones[_atual]));
                _status.Text = "Exported to " + d.FileName;
                AvisaEntradas();
            }
            catch (Exception e)
            {
                Falhou(e);
            }
            return;
        }

        using var f = new FolderBrowserDialog
        {
            Description = "Folder to write b1/block.json, b2/block.json, ...",
            UseDescriptionForTitle = true,
        };
        if (f.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            BlockJson.Reconcilia(_zones);
            int n = 0;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z.OrdemBloco < 0)
                    continue;                       // camps and lobby sub-areas get no block
                // as pastas do servidor comecam no `b1`; o `b0` era a convencao dos despejos do
                // PacketMan, e nao a da arvore que o Starlight le'
                string pasta = Path.Combine(f.SelectedPath, $"b{z.OrdemBloco + 1}");
                Directory.CreateDirectory(pasta);
                File.WriteAllText(Path.Combine(pasta, "block.json"), BlockJson.Escreve(z));
                n++;
            }
            // A lista de entradas nao cabe dentro de um bloco - ela e' da quest - por isso sai
            // num ficheiro a' parte, pronto a colar no `quest.json` (ver BlockJson.Entradas).
            string entradas = BlockJson.Entradas(_zones);
            File.WriteAllText(Path.Combine(f.SelectedPath, "ZoneEntrances.json"),
                BlockJson.FragmentoDasEntradas(entradas));
            _status.Text = $"Exported {n} blocks to {f.SelectedPath}";
            MessageBox.Show(this,
                "The entrance list does not go inside a block any more - it belongs to the quest."
                + Environment.NewLine + Environment.NewLine + entradas + Environment.NewLine
                + Environment.NewLine
                + "It is also in ZoneEntrances.txt next to the blocks. Put it in the quest's "
                + "quest.json, beside \"Blocks\". Starlight reads it from there, and the old "
                + "limit - one entry per block, so a longer list lost its tail - is gone.",
                "Entrance list", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e)
        {
            Falhou(e);
        }
    }

    /// <summary>
    /// Guarda por cima dos `block.json` de onde o mapa veio.
    ///
    /// <para>Nao se reescreve o ficheiro: troca-se o <b>valor</b> dos campos que alguem mexeu e
    /// deixa-se o resto como estava — os comentarios, a indentacao a' mao, e os campos que a
    /// ferramenta nem le' (`DropTables`, `EnemyID`, `Entrances`, `Weather`, ...).  Ver
    /// <see cref="BlockSave"/>.</para>
    /// </summary>
    private void Guarda()
    {
        BlockJson.Reconcilia(_zones);
        var relatorio = new List<string>();
        var falhas = new List<string>();
        int escritos = 0;
        foreach (var z in _zones)
        {
            if (z.Ficheiro is null)
            {
                // uma zona acrescentada a' mao nao tem ficheiro nenhum onde caber
                falhas.Add($"zone {z.ZoneIndex} has no file of its own - it was added by hand, " +
                           "so nothing was written for it");
                continue;
            }
            List<Alteracao> mudou;
            try
            {
                mudou = BlockSave.Grava(z);
            }
            catch (Exception e)
            {
                falhas.Add($"{Curto(z.Ficheiro)}: {e.Message}");
                continue;
            }
            if (mudou.Count == 0)
                continue;
            if (mudou.Any(a => a.Escrita))
                escritos++;
            relatorio.Add(Curto(z.Ficheiro));
            foreach (var a in mudou)
                relatorio.Add("    " + a);
            foreach (var a in mudou.Where(a => !a.Escrita))
                falhas.Add($"{Curto(z.Ficheiro)}: {a.Campo} is not in that file, so the new " +
                           $"value {a.Depois} could not be written");
        }

        _status.Text = escritos == 0 ? "Nothing changed - the files are already up to date"
                                     : $"Saved {escritos} file(s)";
        if (relatorio.Count == 0 && falhas.Count == 0)
        {
            MessageBox.Show(this, "The files already hold what is on screen - nothing to save.",
                            "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var texto = new System.Text.StringBuilder();
        if (relatorio.Count > 0)
        {
            texto.AppendLine($"Saved {escritos} file(s):").AppendLine();
            foreach (var l in relatorio)
                texto.AppendLine(l);
        }
        if (falhas.Count > 0)
        {
            texto.AppendLine().AppendLine("Left out:").AppendLine();
            foreach (var l in falhas)
                texto.AppendLine("    " + l);
        }
        MessageBox.Show(this, texto.ToString(), "Save", MessageBoxButtons.OK,
                        falhas.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        AvisaEntradas();
    }

    /// <summary>O caminho do bloco pela pasta dele, que e' o que o distingue (`b1/block.json`).</summary>
    private static string Curto(string caminho)
        => Path.Combine(Path.GetFileName(Path.GetDirectoryName(caminho) ?? ""),
                        Path.GetFileName(caminho));

    /// <summary>
    /// O ficheiro so' tem lugar para **uma** entrada do `zoneUnknown1` por bloco (FINDINGS
    /// 40.3).  Quando o mapa precisa de mais, as ultimas ficam de fora e com elas os lados de
    /// entrada que so' vinham dali — mais vale dize'-lo do que deixar exportar um mapa mudo.
    /// </summary>
    private void AvisaEntradas()
    {
        int sobram = BlockJson.NaoCabem(_zones);
        if (sobram <= 0)
            return;
        MessageBox.Show(this,
            $"{sobram} ZoneUnknown1 entry(ies) did not fit." + Environment.NewLine +
            Environment.NewLine +
            "A block.json holds one per block, and this map needs more than there are blocks, " +
            "so that many entrance sides are missing from what was written. Add a block, or " +
            "write those entries in by hand.",
            "Entrance sides left out", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void Falhou(Exception e)
        => MessageBox.Show(this, "Could not write:\n\n" + e.Message, "Error",
                           MessageBoxButtons.OK, MessageBoxIcon.Warning);

    // ---------------------------------------------------------------- utilitarios

    /// <summary>Uma caixa de dialogo com um numero — o WinForms nao traz nenhuma.</summary>
    private bool PedeNumero(string titulo, string pergunta, int min, int max, int omissao,
                            out int valor)
    {
        valor = omissao;
        // Esta caixa e' posta por coordenadas, e a 200% um `Point(12, 40)` punha a caixa do
        // numero por cima da etiqueta, que ja' tinha o dobro da altura.  Como as outras, escala
        // por DPI: os numeros ficam em pixeis logicos e e' o WinForms que os multiplica, na
        // passagem de layout do `ResumeLayout` (ver `Escala`).
        using var f = new Form
        {
            Text = titulo,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(300, 108),
            MinimizeBox = false,
            MaximizeBox = false,
            Font = Font,
        };
        var lbl = new Label { Text = pergunta, AutoSize = true, Location = new Point(12, 14) };
        var num = new NumericUpDown { Minimum = min, Maximum = max, Value = omissao,
                                      Location = new Point(12, 40), Width = 80 };
        // a altura do botao vai escrita: a que ele traz de origem sao 23 pixeis que nao
        // acompanham a fonte, e assim fica claro que sao 23 logicos e que escalam com o resto
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK,
                              Location = new Point(126, 70), Size = new Size(78, 23) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel,
                                  Location = new Point(210, 70), Size = new Size(78, 23) };
        // a dimensao de referencia tem de ser posta com o layout suspenso e antes de os
        // controlos entrarem, senao a escala da-se por feita com a caixa ainda vazia
        f.SuspendLayout();
        f.AutoScaleDimensions = new SizeF(Escala.DpiBase, Escala.DpiBase);
        f.Controls.AddRange(new Control[] { lbl, num, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        f.ResumeLayout(performLayout: true);
        if (f.ShowDialog(this) != DialogResult.OK)
            return false;
        valor = (int)num.Value;
        return true;
    }

    /// <summary>
    /// A semente e' um <b>u32</b>: o cliente usa os 32 bits tal e qual e o sinal nao lhe diz
    /// nada, e os `block.json` da Starlight escrevem-na sempre sem sinal (550 dos valores do
    /// corpus passam de 0x7FFFFFFF e nenhum e' negativo).  Por isso o campo so' aceita digitos,
    /// e um valor colado com sinal e' convertido para a forma positiva equivalente em vez de
    /// ser recusado — e' o mesmo numero, so' lido de outra maneira.
    /// </summary>
    private static int Semente(string s)
    {
        s = s.Trim();
        if (uint.TryParse(s, out uint u))
            return unchecked((int)u);
        if (int.TryParse(s, out int v))
            return v;                       // veio com sinal: guarda-se o padrao de bits
        return 0;
    }

    private static int Inteiro(string s, int omissao)
    {
        s = s.Trim();
        if (int.TryParse(s, out int v))
            return v;
        // as sementes e o `Arguments` aparecem nos `block.json` em u32; aceitam-se os dois
        if (uint.TryParse(s, out uint u))
            return unchecked((int)u);
        return omissao;
    }
}
