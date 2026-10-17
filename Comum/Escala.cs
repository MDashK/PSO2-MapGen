namespace Pso2.Interface;

/// <summary>
/// A escala por DPI das interfaces, e a regra que a faz funcionar.
///
/// <para>Este ficheiro e' <b>compilado nas duas ferramentas</b> — a `Pso2MapGen.Gui` e a
/// `SlMissionImporter.Gui` — por `Compile Include` a partir da pasta `Comum`, para a doutrina
/// da escala existir num sitio so'.</para>
///
/// <para>Todos os numeros em pixeis escritos a' mao nestas janelas foram escritos a <b>96
/// DPI</b>, que e' a escala de 100% do Windows.  Num ecra 4K a 150% ou a 200% o DPI e' 144 ou
/// 192: as letras crescem sozinhas (a fonte vai em <i>pontos</i> e o GDI converte-a com o DPI
/// do sistema), mas um <c>Width = 54</c> continuava a valer 54 pixeis — ficava com metade do
/// tamanho relativo que tinha, e o texto saia cortado.  E' o que o colega ve' no 4K.</para>
///
/// <para><b>A regra:</b> todas as janelas declaram <c>AutoScaleMode.Dpi</c> com a dimensao de
/// referencia nos 96 DPI, e e' o WinForms que multiplica tudo de uma vez — tamanhos, posicoes,
/// margens, enchimentos e as colunas absolutas dos <c>TableLayoutPanel</c>.  Nenhuma janela faz
/// essa conta a' mao: as duas juntas davam o quadrado da escala.  <i>Nao misturar modos de
/// escala</i> e' a recomendacao da Microsoft e aqui e' cumprida a' risca.</para>
///
/// <para>Para isso ser verdade, <b>todo o numero que entre no layout tem de estar em pixeis
/// logicos</b> — pixeis de 96 DPI.  Duas coisas nao estao, e e' para elas que serve a
/// <see cref="Logico(System.Windows.Forms.Control, int)"/>:</para>
/// <list type="bullet">
/// <item>o que a <c>TextRenderer.MeasureText</c> devolve, que sao pixeis do ecra: a caixa de
/// ajuda e a dos bits medem o texto para se dimensionarem (foi assim que deixaram de cortar as
/// notas mais compridas) e tem de converter a medida antes de a usar;</item>
/// <item>o que o <c>Screen.WorkingArea</c> devolve, que tambem sao pixeis do ecra, e serve de
/// tecto para nenhuma janela passar do monitor.</item>
/// </list>
///
/// <para><b>O que isto nao resolve:</b> o WinForms nao re-escala a fonte que foi posta num
/// controlo filho (os cabecalhos de seccao a 7.5pt, o `Consolas` da pre-visualizacao, o negrito
/// do bit 5).  Ao <i>abrir</i> a janela isso nao se nota — essas fontes vao em pontos e o GDI
/// ja' as converte com o DPI — mas se a janela for <i>arrastada</i> para um monitor de outra
/// escala, essas tres ficam no tamanho antigo.  E' cosmetico e esta' medido no
/// `Pso2MapGen.ConfereDpi`.</para>
/// </summary>
internal static class Escala
{
    /// <summary>O DPI em que os numeros destas janelas foram escritos: 100% do Windows.</summary>
    public const int DpiBase = 96;

    /// <summary>
    /// Pixeis logicos para pixeis do ecra, para um DPI dado.
    ///
    /// <para><b>Isto quase nunca se usa</b>, e de proposito: o AutoScale ja' faz esta conta a
    /// tudo o que seja uma medida de um controlo, e fazê-la outra vez dava o quadrado da
    /// escala.  Serve so' para as medidas que o AutoScale <b>nao alcanca</b> porque nao sao do
    /// controlo — hoje, a largura das colunas de uma <c>ListView</c>, que e' propriedade do
    /// cabecalho.  Medido no `Pso2MapGen.ConfereDpi`: a 192 DPI as colunas ficavam todas na
    /// largura de 96 enquanto a letra duplicava.</para>
    /// </summary>
    public static int Px(int logico, int dpi)
        => (int)Math.Round(logico * (double)dpi / DpiBase, MidpointRounding.AwayFromZero);

    /// <summary>Um comprimento logico em pixeis do ecra onde este controlo esta'.</summary>
    public static int Px(this Control c, int logico) => Px(logico, c.DeviceDpi);

    /// <summary>
    /// Pixeis do ecra para pixeis logicos, para um DPI dado.  Separado da versao que le' o DPI
    /// ao controlo para poder ser conferido sem abrir janela nenhuma.
    /// </summary>
    public static int Logico(int ecra, int dpi)
        => (int)Math.Round(ecra * (double)DpiBase / dpi, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Uma medida em pixeis do ecra posta em pixeis logicos, para poder entrar no layout de uma
    /// janela que escala por DPI.
    ///
    /// <para>Antes de a janela ter alca o <c>DeviceDpi</c> e' o DPI do sistema — que e'
    /// exactamente o que a <c>TextRenderer.MeasureText</c> tambem usa, por isso as duas contas
    /// ficam na mesma escala.</para>
    /// </summary>
    public static int Logico(this Control c, int ecra) => Logico(ecra, c.DeviceDpi);
}
