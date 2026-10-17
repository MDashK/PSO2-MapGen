using System.Text;

namespace Pso2MapGen.Core;

/// <summary>Os campos de entrada de uma zona — o que a GUI pede e o que o packet da'.</summary>
public sealed class ZoneInput
{
    public int ZoneIndex;
    public int Model;
    public int Seed;
    public int SizeX = 1, SizeY = 1;
    /// <summary>0 = mapa fixo (`.lat`), 1 = aleatorio, 2 = rio.</summary>
    public int UnkB = 1;
    public int Arguments = -1;
    public int UnkP, UnkW;
    /// <summary>A variante ICE de cada design (`UnkQ`/`UnkY`).  Nao mexe no mapa: a pool
    /// junta os chunks pelo nome, por isso isto so' decide o aspecto da peca (5.2, 13.3).</summary>
    public int UnkQ, UnkY;
    /// <summary>InfoTable3.Unk3: o minLen da saida (gen-&gt;0x98).</summary>
    public int Unk3 = -1;
    /// <summary>InfoTable3.Unk4: o tecto de ramos (gen-&gt;0x9C -&gt; gen-&gt;0x90).</summary>
    public int Unk4;
    /// <summary>gen-&gt;0xA0, o bit 5 do UnkN do ZoneSettingsExtra.</summary>
    public int A0;
    public int UnkN;

    /// <summary>Os lados de entrada (mapA).  Vazio deixa cair na aproximacao antiga.</summary>
    public List<int>? LadosA;
    /// <summary>Os lados de saida (mapB).</summary>
    public List<int>? LadosB;

    /// <summary>
    /// O modelo da zona de <b>onde se vem</b>, que e' a chave do <c>paramsC</c> do `.lps` para o
    /// nome da peca de entrada (41.17).  <c>null</c> quando a zona nao recebe warp nenhum — e'
    /// a entrada declarada pelo <c>zoneUnknown1</c>, e essa leva o literal `s0`.
    /// </summary>
    public int? ModeloDeOndeVem;

    /// <summary>
    /// O modelo da zona <b>para onde se vai</b>, a chave do <c>paramsD</c> para o nome da peca
    /// de saida (41.17).  <c>null</c> quando a zona nao tem warp de saida.
    /// </summary>
    public int? ModeloParaOndeVai;

    /// <summary>
    /// A zona <b>para onde se vai</b> e' um mapa <b>fixo</b> (a area de boss)?  E' o que decide
    /// se o nome da peca de saida vem do <c>paramsB["goal_to_boss"]</c> em vez do
    /// <c>paramsD</c> (55.8).  Mede-se pelo <c>SizeX</c>/<c>SizeY</c> da zona de destino a zero,
    /// que e' a assinatura de uma zona cujo mapa vem do `.lat`.
    /// </summary>
    public bool DestinoEhMapaFixo;

    /// <summary>Quantas entradas o mapA tem, quando os lados nao vem do packet.</summary>
    public int NA = 1;
    /// <summary>Lado de entrada forcado (modo a' mao); null deixa o sorteio decidir.</summary>
    public int? Entrada;
    /// <summary>Lado de saida (modo a' mao).</summary>
    public int Saida;

    /// <summary>
    /// O <c>UnkO</c> do <c>ZoneSettingsExtra</c>, e <b>nao e' um numero</b>: sao quatro bytes, e
    /// o primeiro e' o <c>gen-&gt;0x94</c> — o modo de altura (41.7).
    /// </summary>
    public int UnkO;
    /// <summary>A zona sabe o seu <c>UnkO</c>?  Uma feita a' mao sem tabela nao sabe.</summary>
    public bool TemUnkO;

    /// <summary>
    /// <c>InfoTable3.Unk5</c>, o quinto dword da linha.  O bit 0 dele e' o <c>gen-&gt;0xA4</c>,
    /// que liga a passagem <c>0x00633D30</c> — que a ferramenta <b>nao reproduz</b> (41.7).
    /// </summary>
    public int Unk5;

    /// <summary>gen-&gt;0x94 forcado a' mao; null tira-o do <c>UnkO</c>.</summary>
    public int? P94;
    /// <summary>O bloco de init de 0x00647BB0 correu?  Ver FINDINGS 39.2.</summary>
    public bool InitCorreu = true;

    public int Tentativas = 5;

    // --- o que veio do packet, quando veio de um: e' o que permite exportar um
    // `block.json` completo em vez de so' os campos que decidem o mapa (FINDINGS 39.9).
    public ZoneSettings? Settings;
    public ZoneExtra? Extra;
    public InfoTableRow? It1, It2;
    public InfoTable3Row? It3;
    public List<Warp> Warps = new();

    /// <summary>O <c>zoneUnknown1</c> <b>inteiro</b> do packet, pela ordem em que la' vem.</summary>
    public List<(int Unk1, int Unk2)> Zu1 = new();

    /// <summary>
    /// A ordem desta zona entre as que recebem bloco (as de <c>ZoneIndex &gt;= 0</c>); -1 nas
    /// outras.  O StarlightPacketMan reparte o <c>zoneUnknown1</c> <b>posicionalmente</b> — o
    /// bloco de ordem <c>i</c> leva a entrada <c>i</c> e os que sobram levam <c>(0,0)</c>.
    /// Medido nos 1032 pares <c>block.json</c>: bate nos 1032, enquanto "cada bloco leva um par
    /// da sua zona" falha em 263 e "o bloco N leva a entrada N" falha em 5 (a
    /// `RARE ORE EXCAVATION`, cujas zonas sao -2, 3 e 4: o primeiro bloco e' o `b1` e leva na
    /// mesma a entrada 0).  Nao e' a leitura do <b>cliente</b> — para ele o <c>unk1</c> e' mesmo
    /// a zona (37.6) — e' so' a arrumacao do ficheiro, e e' o que faz o ida-e-volta reconstruir
    /// a lista original por ordem.
    /// </summary>
    public int OrdemBloco;

    /// <summary>De que ficheiro veio, quando veio de um `block.json`.</summary>
    public string? Ficheiro;

    /// <summary>
    /// Como a zona estava <b>quando se leu o ficheiro</b>.  E' o que permite guardar por cima
    /// so' os campos que alguem mexeu: o que nao mudou nem se toca, e o ficheiro fica byte a
    /// byte como estava nessa parte (ver BlockJson.Actualiza).  Null numa zona feita a' mao.
    /// </summary>
    public ZoneInput? ComoVeio;

    /// <summary>Uma copia com as listas proprias — senao editar a copia mexia no original.</summary>
    public ZoneInput Clone()
    {
        var c = (ZoneInput)MemberwiseClone();
        c.LadosA = LadosA is null ? null : new List<int>(LadosA);
        c.LadosB = LadosB is null ? null : new List<int>(LadosB);
        c.DestinoEhMapaFixo = DestinoEhMapaFixo;
        c.Warps = new List<Warp>(Warps);
        c.Zu1 = new List<(int, int)>(Zu1);
        return c;
    }
}

/// <summary>O resultado de uma geracao: a grelha final, o estado, e como se la' chegou.</summary>
public sealed class Previsto
{
    public Grid? Grelha;
    public Gen? G;
    public string Nota = "";
    /// <summary>
    /// O que a ferramenta <b>nao</b> sabe fazer nesta zona.  Nao entra na <see cref="Nota"/>:
    /// a nota e' resultado da geracao e os conferidores comparam-na, um aviso nao e'.
    /// </summary>
    public string? Aviso;
    public bool Ok => Grelha is not null;
    /// <summary>Quantas pecas o ciclo do labirinto colocou (o arranque conta).</summary>
    public int Pecas => G is null ? 0 : G.Trace.Count + 1;
}

/// <summary>
/// Pre-visualizacao de uma zona, a partir dos campos do packet <c>0x03-0x24</c>.
/// A ordem e' a do <c>0x00646320</c>, com o ciclo de tentativas do <c>0x00648680</c> a
/// partilhar o fluxo do Mersenne Twister (FINDINGS 7c).  Porto de <c>tools/previsao.py</c>.
/// </summary>
public static class Previsao
{
    /// <summary>
    /// O <c>gen-&gt;0x94</c> desta zona: o <b>byte 0 do <c>UnkO</c></b>, com sinal, que e' o que
    /// o <c>0x00647EE5</c> la' poe (41.7).  Sem <c>UnkO</c> — uma zona feita a' mao sem linha no
    /// `infotables.txt` — cai na tabela medida por area, que era o que se usava antes.
    ///
    /// <para>Zero quando o bloco de <c>0x00647BB0</c> nao correu: ai o campo fica no valor por
    /// omissao, como o 0x98 e o 0x9C (26.3).</para>
    /// </summary>
    public static int P94De(ZoneInput z)
    {
        if (!z.InitCorreu)
            return 0;
        return z.TemUnkO ? MapGen.P94DoUnkO(z.UnkO) : MapGen.P94DaArea(z.Model, true);
    }

    public static Previsto Gera(ZoneInput z)
    {
        if (z.UnkB == 0)
        {
            // mapa FIXO: nao ha geracao nenhuma, o mapa e' o proprio `.lat`
            var gr = MapGen.LatGrid(z.Model, z.Arguments);
            return gr is null
                ? new Previsto { Nota = $"missing file sn_{z.Model:D4}_{z.Arguments:D4}.lat" }
                : new Previsto { Grelha = gr, Nota = $"fixed map (.lat {z.Arguments:D4})" };
        }

        Area area;
        try
        {
            area = GameData.GetArea(z.Model);
        }
        catch (Exception e)
        {
            return new Previsto { Nota = $"no .lps for model {z.Model}: {e.Message}" };
        }
        area.SetBigChunks(Area.BigChunkNames(z.UnkP, z.UnkW));

        bool rio = z.UnkB == 2;
        var mt1 = new Mt19937((uint)z.Seed);
        var mt2 = new Mt19937((uint)z.Seed);

        for (int n = 0; n < z.Tentativas; n++)
        {
            var g = new Gen(area, z.Seed, z.SizeX, z.SizeY, rio)
            {
                Mt1 = mt1,                       // as tentativas NAO ressemeiam (21.4)
                Mt2 = mt2,
                P90 = z.Unk4 == 1 ? 5 : (z.Unk4 == 2 ? 2 : -1),
                P94 = z.P94 ?? P94De(z),
                P7c = z.InitCorreu ? 1 : 0,
            };

            if (rio)
            {
                g.G = MapGen.RiverGrid(z.Model, z.Arguments);
                if (g.G is null)
                    return new Previsto
                    {
                        Nota = $"missing file sn_{z.Model:D4}_river{z.Arguments:D4}.lat"
                    };
                g.MarcarFronteiraDoRio();
            }
            else
            {
                int lado0 = z.Entrada ?? 1;
                g.G = (lado0 == 0 || lado0 == 2)
                    ? new Grid(z.SizeX * 2 - 1, z.SizeY)
                    : new Grid(z.SizeX, z.SizeY * 2 - 1);
            }

            // 0x0063F970: o mapA e o mapB vem do packet (37.8).  Sem eles, cai-se na
            // aproximacao antiga: o mapA sao os lados que nao sao o da saida.
            List<int> mapaA;
            if (z.LadosA is not null && z.LadosA.Count > 0)
            {
                mapaA = z.LadosA.Distinct().OrderBy(v => v).ToList();
            }
            else
            {
                mapaA = Enumerable.Range(0, 4).Where(d => d != z.Saida).Take(z.NA).ToList();
                if (z.Entrada is int e && !mapaA.Contains(e))
                    mapaA = mapaA.Take(Math.Max(0, z.NA - 1)).Append(e)
                                 .Distinct().OrderBy(v => v).ToList();
            }

            // 0x00640502, ainda dentro do 0x0063F970: com gen->0xA0 posto, um dos lados que
            // **nao** receberam tipo nenhum vai para o mapB com o chunk `n0`, e o sorteio
            // custa uma palavra quando sobra mais do que um (38.8).
            var lbSides = (z.LadosB is not null ? z.LadosB.Distinct().OrderBy(v => v).ToList()
                                                : new List<int> { z.Saida });
            // O nome de cada lado do mapB vem COM ele: os lados verdadeiros levam a peca de
            // saida (o `null` abaixo) e o lado EXTRA que o `gen->0xA0` acrescenta leva o literal
            // `n0`.  Perder este nome era o que punha `g1` onde o cliente poe `n0` na area 2 do
            // Quarry (55.8).
            var (_, lbPares) = g.SobrasA0(
                mapaA.Select(d => (d, (string?)null)).ToList(),
                lbSides.Select(d => (d, (string?)null)).ToList(), z.A0);

            // o nome das duas pecas vem do `paramsC`/`paramsD` do `.lps`, com o modelo da zona
            // do outro lado da porta como chave (41.17)
            string? nomeEnt = MapGen.NomeDaEntrada(area, z.ModeloDeOndeVem);
            string? nomeSai = MapGen.NomeDaSaida(area, z.ModeloParaOndeVai,
                                                 z.DestinoEhMapaFixo);

            var resto = new List<int>(mapaA);
            var tgt = g.StartTarget(resto.Select(d => (d, nomeEnt)).ToList());
            if (tgt is not StartTarget st)
                continue;

            if (z.Entrada is null && !rio)
            {
                // a grelha de trabalho depende do lado que saiu, por isso refaz-se
                g.G = (st.Side == 0 || st.Side == 2)
                    ? new Grid(z.SizeX * 2 - 1, z.SizeY)
                    : new Grid(z.SizeX, z.SizeY * 2 - 1);
            }

            g.MarkEntrance(st.X, st.Y, st.H, nomeEnt, st.Side);
            int d0 = MapGen.Opposite(st.Side);
            int sx0 = st.X + MapGen.DX[d0], sy0 = st.Y + MapGen.DY[d0];
            if (!g.G!.Inside(sx0, sy0))
                continue;

            var alvo = new Target(sx0, sy0, st.H, st.Side);
            if (g.ArranqueNoRio(alvo) is null)
            {
                var rec = g.ChoosePiece(alvo);
                if (rec is null)
                    continue;
                g.Place(rec);
                g.BumpBranch(rec);
            }
            g.Grow();

            var portasEnt = resto.Where(s => s != st.Side).ToList();
            if (portasEnt.Count > 0 && nomeEnt is null)
                continue;                    // sem chunk de entrada nao ha porta para por
            if (!g.PlaceDoors(portasEnt.Select(s => (s, nomeEnt!)), 0))
                continue;

            g.RenumeraDoArranque();          // 0x006468E4, entre as duas chamadas

            if (lbPares.Count > 0)
            {
                // `null` quer dizer "a peca de saida desta area"; o `n0` do `gen->0xA0` ja' vem
                // com o nome dele.
                if (nomeSai is null && lbPares.Any(t => t.Name is null))
                    continue;                // a area nao tem peca de saida nenhuma
                if (!g.PlaceDoors(lbPares.Select(t => (t.Side, t.Name ?? nomeSai!)),
                                  Math.Max(0, z.Unk3)))
                    continue;
            }

            g.Attempt = n;
            return new Previsto
            {
                Grelha = MapGen.FinalGrid(g.G, g.Doors),
                G = g,
                Nota = $"attempt {n + 1}",
                // O bit 0 do `InfoTable3.Unk5` e' o `gen->0xA4`, e o `gen->0xA4` liga a passagem
                // `0x00633D30` — 12 KB de codigo que corre **antes** do resto do
                // pos-processamento e que a ferramenta nao reproduz.  Nunca foi vista a correr:
                // das 1151 linhas do corpus so' 15 tem o bit ligado (as tres zonas de Kuron da
                // `SUPRESS KODOTTA IDIHTA`) e nenhuma delas foi capturada por uma sonda.  O
                // labirinto nao e' afectado — a passagem e' posterior —, mas os enchimentos
                // `z*` podem nao ser estes (41.7).
                //
                // Vai no `Aviso` e **nao** na `Nota` de proposito: a nota e' saida da geracao e
                // e' o que os conferidores comparam, e um aviso sobre o que a ferramenta nao
                // sabe fazer nao e' um resultado da geracao.  O packet traz o `Unk5` e o
                // `block.json` do PacketMan nao, por isso metê-lo na nota punha os dois caminhos
                // a discordar em 15 zonas onde o mapa e' o mesmo.
                Aviso = (z.Unk5 & 1) != 0
                    ? "MapGenTable.ExtraPassFlag bit 0 turns on the 0x00633D30 pass. The maze "
                      + "is right; the pass renames every placed piece to the d* family, which "
                      + "the d* reskin option reproduces (41.18)"
                    : null,
            };
        }
        return new Previsto { Nota = $"none of the {z.Tentativas} attempts produced a map" };
    }

    /// <summary>
    /// Duas celulas vizinhas estao ligadas quando <b>as duas</b> tem a porta virada uma para a
    /// outra — e' a regra do <c>0x005EC5E0</c>.  Estarem lado a lado nao quer dizer passagem
    /// nenhuma: um `n0` pode ter um `l0` de cada lado e nao encostar a nenhum deles.
    /// </summary>
    public static bool Ligadas(Grid gr, int x, int y, int d)
    {
        int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
        if (!gr.Inside(nx, ny))
            return false;
        return ((gr.At(x, y) >> d) & 1) != 0
            && ((gr.At(nx, ny) >> MapGen.Opposite(d)) & 1) != 0;
    }

    /// <summary>
    /// A grelha em texto, com os nomes <b>internos do jogo</b> (`i0`, `l0`, `t0`, `x0`, `n0`,
    /// `q4`, `ci`, `cx`, ...) e a rotacao a seguir, tal como saem do `.lps`.
    ///
    /// Com <paramref name="ligacoes"/> desenha `--` e `|` entre as celulas que <b>encostam
    /// mesmo</b> uma a' outra, que e' o que faz o mapa ler-se: sem isso duas pecas lado a lado
    /// parecem um corredor quando podem ser dois ramos diferentes.  Com
    /// <paramref name="portas"/> acrescenta, por celula, as direccoes em que ela abre.
    ///
    /// As colunas vao A, B, C... e as linhas 1, 2, 3..., como a grelha do mapa do jogo.
    /// </summary>
    public static string Desenha(Grid gr, bool portas = false, bool ligacoes = true,
                                 bool coordenadas = true)
    {
        int largura = portas ? 4 : 3;                  // o campo de cada celula
        string margem = coordenadas ? "    " : "";
        var sb = new StringBuilder();

        if (coordenadas)
        {
            var cab = new StringBuilder(margem);
            for (int x = 0; x < gr.W; x++)
                cab.Append(Centra(Coluna(x), largura)).Append("  ");
            sb.AppendLine(cab.ToString().TrimEnd());
        }

        for (int y = 0; y < gr.H; y++)
        {
            var linha = new StringBuilder(coordenadas ? $"{y + 1,3} " : "");
            for (int x = 0; x < gr.W; x++)
            {
                int i = gr.Idx(x, y);
                string nome = gr.Name[i] is null ? "." : $"{gr.Name[i]}{gr.Rot[i]}";
                linha.Append(Centra(nome, largura));
                if (x + 1 < gr.W)
                    linha.Append(ligacoes && Ligadas(gr, x, y, 1) ? "--" : "  ");
            }
            sb.AppendLine(linha.ToString().TrimEnd());

            if (portas)
            {
                var pl = new StringBuilder(margem);
                for (int x = 0; x < gr.W; x++)
                {
                    uint v = gr.At(x, y);
                    var s = new StringBuilder();
                    for (int k = 0; k < 4; k++)
                        s.Append(((v >> k) & 1) != 0 ? "NESW"[k] : '.');
                    pl.Append(Centra(s.ToString(), largura)).Append("  ");
                }
                sb.AppendLine(pl.ToString().TrimEnd());
            }

            if (ligacoes && y + 1 < gr.H)
            {
                var vl = new StringBuilder(margem);
                for (int x = 0; x < gr.W; x++)
                    vl.Append(Centra(Ligadas(gr, x, y, 2) ? "|" : " ", largura)).Append("  ");
                string s = vl.ToString().TrimEnd();
                if (s.Length > 0)
                    sb.AppendLine(s);
            }
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>A, B, ... Z, AA, AB... como a grelha do mapa do jogo.</summary>
    public static string Coluna(int x)
        => x < 26 ? ((char)('A' + x)).ToString()
                  : $"{(char)('A' + x / 26 - 1)}{(char)('A' + x % 26)}";

    private static string Centra(string s, int largura)
    {
        if (s.Length >= largura)
            return s;
        int esq = (largura - s.Length) / 2;
        return new string(' ', esq) + s + new string(' ', largura - s.Length - esq);
    }
}
