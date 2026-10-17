namespace Pso2MapGen.Core;

/// <summary>Um alvo de encaixe: a celula, a altura, e a direccao de onde a porta vem.</summary>
public readonly record struct Target(int X, int Y, int H, int Dir);

/// <summary>O que o <c>0x00641420</c> devolve: o marcador de entrada e o lado que saiu.</summary>
public readonly record struct StartTarget(int X, int Y, int H, int Side, string? Name);

/// <summary>Uma candidata da fronteira: a celula-ancora, a altura, e o lado para onde sai.</summary>
public readonly record struct Cand(int X, int Y, int H, int Side);

/// <summary>Uma peca colocada (ou um marcador de entrada/saida, que nao chega a ser escrito).</summary>
public sealed class Placed
{
    public int X, Y, H, Rot;
    public Chunk? Ch;
    /// <summary>
    /// O <c>+0xC</c> do registo de 0x28 bytes do cliente: a direccao que vai da celula de
    /// dentro para o marcador.  So' os marcadores de <c>gen-&gt;0x28</c> a usam, e e' ela
    /// que o <c>0x00646CE0</c> inverte para achar a celula por onde re-numerar (37.2).
    /// </summary>
    public int? Dir;
    public string? Nome;

    public Placed(int x, int y, int h, int rot, Chunk? ch, int? dir = null, string? nome = null)
    {
        X = x; Y = y; H = h; Rot = rot; Ch = ch;
        Dir = dir;
        Nome = nome ?? ch?.Short;
    }

    public override string ToString() => $"{Ch?.Short}@({X},{Y})r{Rot}";
}

public sealed class Grid
{
    public int W, H;
    public uint[] Info;
    public string?[] Name;
    public int[] Rot;
    public int[] Height;

    /// <summary>
    /// <c>grid-&gt;0x88</c>: a PROFUNDIDADE de cada celula, em pecas desde o arranque.  O
    /// <c>0x00645560</c> le-a no topo da pilha, soma 1 e passa-a ao <c>0x0063CE90</c>, que a
    /// escreve nas celulas da peca nova.  E' o que o <c>0x0063E4D0</c> usa como comprimento
    /// de partida do corredor, e portanto o que faz o minLen da saida significar "longe da
    /// entrada" (FINDINGS 32).  Arranca toda a -1.
    /// </summary>
    public int[] Depth;

    /// <summary>
    /// <c>grid-&gt;0x7C</c>: o NUMERO DA PECA que ocupa cada celula (o <c>gen-&gt;0xB8</c> do
    /// momento).  O <c>0x005EC5E0</c> usa-o para saber se duas celulas vizinhas sao o mesmo
    /// chunk.  Arranca todo a <b>zero</b> (memset de 0x005EB17E), ao contrario do +0x88.
    /// </summary>
    public int[] Piece;

    /// <summary>
    /// <c>cell+0x0A</c> e <c>cell+0x0C</c>: os ids das variantes **ICE** e **<c>_ob0*</c>** de cada
    /// celula.  Arrancam ambos a <c>0xFFFF</c>, que e' o valor por omissao do construtor do cliente
    /// e quer dizer "sem variante".  Quem os escreve e' o <see cref="MapGen.PasseVariantes"/>, e
    /// isso corre no FIM do encadeamento.
    /// </summary>
    public int[] Ice, Ob;

    /// <summary>
    /// As celulas cujo ICE saiu de um <b>sorteio que nao se pode reproduzir</b>: so' acontece no
    /// caminho do mapa fixo, onde o <c>mt1</c> e' estado de sessao, e ali escreve-se o primeiro
    /// candidato (FINDINGS 56).  Vazio em todo o caminho gerado.
    /// </summary>
    public HashSet<int>? IceSorteada;

    // Ha **duas** caixas, e sao coisas diferentes (FINDINGS 32.2):
    //   Min*/Max*   = gen->0x40..0x4C, a caixa que os testes de encaixe usam.  No rio o
    //                 esqueleto do .lat NAO a semeia (24.5).
    //   BMin*/BMax* = grid->0x18..0x24, a caixa de tudo o que esta escrito na grelha, .lat
    //                 incluido.  E esta que o 0x0063E4D0 usa para montar a linha da
    //                 fronteira e que o 0x005EAF50 usa para recortar a grelha final.
    public int MinX, MinY, MaxX, MaxY;
    public int BMinX, BMinY, BMaxX, BMaxY;

    public Grid(int w, int h)
    {
        W = w; H = h;
        int n = w * h;
        Info = new uint[n];
        Name = new string?[n];
        Rot = new int[n];
        Height = new int[n];
        Depth = new int[n];
        Array.Fill(Depth, -1);
        Piece = new int[n];
        Ice = new int[n];
        Ob = new int[n];
        Array.Fill(Ice, 0xFFFF);
        Array.Fill(Ob, 0xFFFF);
        MinX = w; MinY = h; MaxX = -1; MaxY = -1;
        BMinX = w; BMinY = h; BMaxX = -1; BMaxY = -1;
    }

    public int Idx(int x, int y) => y * W + x;
    public bool Inside(int x, int y) => x >= 0 && x < W && y >= 0 && y < H;
    public uint At(int x, int y) => Inside(x, y) ? Info[y * W + x] : 0u;

    public bool Occupied(int x, int y)
    {
        uint v = At(x, y);
        return (v & 0x0F) != 0 || ((v >> 8) & 0xFF) != 0;
    }

    /// <summary>
    /// Escreve uma celula.  A profundidade <b>nao</b> se escreve aqui: o <c>0x0063C620</c>
    /// nao toca no <c>grid-&gt;0x88</c>.  Quem o escreve, em toda a geracao, e' so' o
    /// <c>0x005EC410</c> (37.1).
    /// </summary>
    public void Write(int x, int y, uint info, string? name, int rot, int h, int piece = 0)
    {
        int i = Idx(x, y);
        Info[i] = info;
        Name[i] = name;
        Rot[i] = rot;
        Height[i] = h;
        Piece[i] = piece;
        if (x < MinX) MinX = x;
        if (y < MinY) MinY = y;
        if (x > MaxX) MaxX = x;
        if (y > MaxY) MaxY = y;
        if (x < BMinX) BMinX = x;
        if (y < BMinY) BMinY = y;
        if (x > BMaxX) BMaxX = x;
        if (y > BMaxY) BMaxY = y;
    }

    /// <summary>
    /// <c>0x005EC5E0</c>: da' para passar de (x, y) para o vizinho do lado d?
    /// 0 = nao; 1 = porta com porta (custa uma peca); 2 = a MESMA peca (custa zero).
    ///
    /// O byte 1 do dword de info e' o que marca as celulas de um chunk de varias celulas que
    /// nao tem porta propria; o +0x7C diz qual foi a peca que as escreveu.  E' por isso que
    /// um `q4` 2x2 fica com a mesma profundidade nas quatro celulas (36.1).
    /// </summary>
    public int Ligacao(int x, int y, int d)
    {
        int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
        uint v = At(x, y);
        if (((v >> d) & 1) != 0 && ((At(nx, ny) >> MapGen.Opposite(d)) & 1) != 0)
            return 1;
        if (((v >> 8) & 0xFF) == 0)
            return 0;
        int p1 = Inside(x, y) ? Piece[Idx(x, y)] : 0;
        int p2 = Inside(nx, ny) ? Piece[Idx(nx, ny)] : 0;
        return p1 == p2 ? 2 : 0;
    }

    /// <summary>
    /// <c>0x005EC410</c>: a unica escrita do <c>grid-&gt;0x88</c> em toda a geracao.
    /// Poe <c>d</c> em (x, y) e alastra pelas portas abertas, sempre a <b>descer</b>:
    /// celula vazia e sem nome nao faz nada; <c>d &lt; 0</c> nao faz nada (e' o que faz o
    /// look-ahead do 0x0063BAB0, que chama com -1, nao mexer em nada); um valor ja' la'
    /// que seja <c>&gt;= 0</c> e <c>&lt;= d</c> nao faz nada; senao escreve e repete nos
    /// quatro vizinhos, com <c>d+1</c> quando a ligacao e' de porta e com o <b>mesmo d</b>
    /// quando as duas celulas sao a mesma peca.
    /// </summary>
    public void Espalha(int x, int y, int d)
    {
        if (!Inside(x, y))
            return;
        int i = Idx(x, y);
        uint v = Info[i];
        if ((v & 0x0F) == 0 && ((v >> 8) & 0xFF) == 0 && string.IsNullOrEmpty(Name[i]))
            return;                       // 0x005EC48E: celula vazia e sem nome
        if (d < 0)
            return;                       // 0x005EC4BF
        int cur = Depth[i];
        if (cur >= 0 && cur <= d)
            return;                       // 0x005EC4CD: ja' tem um valor melhor
        Depth[i] = d;                     // 0x005EC5D1
        for (int dd = 0; dd < 4; dd++)
        {
            int r = Ligacao(x, y, dd);
            if (r == 1)
                Espalha(x + MapGen.DX[dd], y + MapGen.DY[dd], d + 1);
            else if (r == 2)
                Espalha(x + MapGen.DX[dd], y + MapGen.DY[dd], d);
        }
    }

    public Grid Clone()
    {
        var g = new Grid(W, H);
        Array.Copy(Info, g.Info, Info.Length);
        Array.Copy(Name, g.Name, Name.Length);
        Array.Copy(Rot, g.Rot, Rot.Length);
        Array.Copy(Height, g.Height, Height.Length);
        Array.Copy(Depth, g.Depth, Depth.Length);
        Array.Copy(Piece, g.Piece, Piece.Length);
        Array.Copy(Ice, g.Ice, Ice.Length);
        Array.Copy(Ob, g.Ob, Ob.Length);
        g.MinX = MinX; g.MinY = MinY; g.MaxX = MaxX; g.MaxY = MaxY;
        g.BMinX = BMinX; g.BMinY = BMinY; g.BMaxX = BMaxX; g.BMaxY = BMaxY;
        return g;
    }
}

/// <summary>
/// O gerador de mapas do cliente PSO2.  Espelha, tao de perto quanto a analise estatica
/// permite:
///   <c>0x00646320</c> gerar -> <c>0x00645560</c> ciclo do labirinto -> <c>0x00640800</c>
///   escolher peca -> <c>0x0063F0B0</c> encaixe -> <c>0x0063C430</c>/<c>0x00648C60</c> regra
///   das portas -> <c>0x0063CE90</c> escrita.
/// Porto directo de <c>tools/mapgen.py</c>; a proveniencia de cada regra esta' no FINDINGS.
/// </summary>
public sealed class Gen
{
    public Area AreaRef;
    public int Seed;
    public Mt19937 Mt1, Mt2;
    public int SizeX, SizeY;
    public bool River;
    public bool HasHeight;
    public int MaxHeight, MaxHdiff;
    public Grid? G;
    public List<Placed> Placed = new();
    public List<Placed> Trace = new();

    /// <summary>gen-&gt;0xB4</summary>
    public int Branch;

    /// <summary>
    /// gen-&gt;0x90 decide se o contador de ramos continua a crescer depois de deixar de ser
    /// zero: <c>if (gen-&gt;0xB4 == 0 || gen-&gt;0x90 &gt;= 0) gen-&gt;0xB4 += ...</c>.  Medido
    /// nas 34 capturas (FINDINGS 21.5): e' 5 quando <c>InfoTable3.Unk4 == 1</c> e -1 quando e'
    /// 0, sem uma excepcao — o packet decide-o.
    /// </summary>
    public int P90 = -1;

    /// <summary>gen-&gt;0x94: escolhe a LINHA das tabelas do sorteio de rampa e a altura de arranque.</summary>
    public int P94;

    /// <summary>
    /// gen-&gt;0x7C so' fica a 1 quando o bloco de inicializacao de <c>0x00647BB0</c> corre.
    /// Quando nao corre, <c>0x00640835</c> salta para <c>0x0064137A</c> e a piscina vai
    /// directa a' mascara mista, <b>sem o sorteio de despromocao</b> dos big chunks (26.3).
    /// </summary>
    public int P7c = 1;

    /// <summary>gen-&gt;0xC4</summary>
    public int Bigs;

    public int PieceNo = 1;
    /// <summary>A profundidade a escrever na proxima peca (0x0064569A).</summary>
    public int Prof = 1;
    /// <summary>gen-&gt;0xBC</summary>
    public int ProfMax = 1;

    /// <summary>As pecas de entrada e de saida: registadas em gen-&gt;0x28, nunca escritas na grelha.</summary>
    public List<Placed> Doors = new();
    /// <summary>0x00638C70: os marcadores de teleporte (x, y, idA, idB).</summary>
    public List<(int X, int Y, int A, int B)> Marks = new();

    /// <summary>Qual das cinco tentativas produziu este estado.</summary>
    public int Attempt;

    public Gen(Area area, int seed, int sizeX, int sizeY, bool river = false)
    {
        AreaRef = area;
        Seed = seed;
        Mt1 = new Mt19937((uint)seed);
        Mt2 = new Mt19937((uint)seed);
        SizeX = sizeX;
        SizeY = sizeY;
        River = river;
        HasHeight = area.HasHeight;
        // grid->0x44 e grid->0x48 vem de gen->0x88 / gen->0x8C quando a grelha e' criada.
        // Medido: p88 e' 10 nas areas com altura e **0** nas planas.
        MaxHeight = area.HasHeight ? area.MaxHeight : 0;
        MaxHdiff = area.MaxHdiff;
    }

    // ---- 0x0063C430: o teste de celula ---------------------------------------------
    /// <summary>
    /// <c>0x0063C430</c>: dentro da grelha, dentro da caixa, e depois DUAS regras de encaixe
    /// — o <c>0x00648C60</c> (portas) e o <c>0x00648830</c> (alturas) tem <b>ambas</b> de
    /// devolver 0.
    /// </summary>
    public bool CellOk(int x, int y, uint info, int h = 0)
    {
        var g = G!;
        if (!g.Inside(x, y) || g.Occupied(x, y))
            return false;
        if (!River)
        {
            if (Math.Max(g.MaxY, y) - Math.Min(g.MinY, y) >= SizeY)
                return false;
            if (Math.Max(g.MaxX, x) - Math.Min(g.MinX, x) >= SizeX)
                return false;
        }
        uint paths = info & 0xF;
        for (int d = 0; d < 4; d++)
        {
            int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
            if (!g.Occupied(nx, ny))
                continue;                      // vizinho vazio nao impoe nada
            uint nb = g.At(nx, ny);
            uint mine = (paths >> d) & 1;
            uint his = (nb >> MapGen.Opposite(d)) & 1;
            if (mine == his)
                continue;
            if (mine != 0 && his == 0 && MapGen.RioPermeavel)
            {
                // 0x005E7E10 (a regra da grelha NORMAL, vtable 0x65A01FC): por cada
                // direccao em que EU tenho porta e o vizinho nao tem a de volta, pergunta
                // ao 0x005EA610 o nome da celula do vizinho e, **so se for exactamente
                // `ci`** (o literal em 0x05A10AD4), limpa esse bit da mascara.  Numa area
                // sem rio nunca dispara, porque nenhum `c*` chega a ser colocado.
                int i = g.Idx(nx, ny);
                if ((g.Name[i] ?? "") == "ci"
                    && (!MapGen.ParidadeCi || ((g.Rot[i] - d) & 1) != 0))
                    continue;
            }
            return false;
        }
        return HeightOk(x, y, info, h);
    }

    // ---- 0x00648830: a regra das alturas --------------------------------------------
    /// <summary>
    /// <c>0x00648830</c>, lida instrucao a instrucao.  <c>grid-&gt;0x44</c> e
    /// <c>grid-&gt;0x48</c> sao copiados de <c>gen-&gt;0x88</c>/<c>0x8C</c> quando a grelha e'
    /// criada, ou seja o max_height e o max_hdiff do `.lps`.  Numa area plana
    /// <c>gen-&gt;0x88</c> e' <b>zero</b>, portanto so' a altura 0 passa — e como tudo esta' a
    /// 0, e' trivialmente satisfeita.
    /// </summary>
    public bool HeightOk(int x, int y, uint info, int h)
    {
        var g = G!;
        int maxh = MaxHeight, hdiff = MaxHdiff;
        if (h < 0 || h > maxh)
            return false;
        uint paths = info & 0xF;
        for (int d = 0; d < 4; d++)
        {
            int meu = h + (int)((info >> (16 + d)) & 1);
            int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
            if (!g.Occupied(nx, ny))          // fora da grelha conta como vazio
            {
                if (meu < 0 || meu > maxh)
                    return false;
                continue;
            }
            uint nb = g.At(nx, ny);
            int back = MapGen.Opposite(d);
            int dele = g.Height[g.Idx(nx, ny)] + (int)((nb >> (16 + back)) & 1);
            if (((paths >> d) & 1) == 0)
            {
                if (((nb >> back) & 1) != 0)
                    continue;
                if (Math.Abs(meu - dele) < hdiff)
                    continue;
                return false;
            }
            if (meu != dele)
                return false;
        }
        return true;
    }

    // ---- 0x0063F0B0: esta (peca, rotacao) encaixa no alvo? --------------------------
    /// <summary>
    /// Lida instrucao a instrucao em <c>0x0063F0B0</c> (FINDINGS 22).  Por cada porta da
    /// (peca, rotacao) cuja direccao seja a do alvo, a ancora cai em <c>alvo - porta</c>, e a
    /// colocacao so' e' oferecida se TRES testes passarem: todas as celulas passam o
    /// <see cref="CellOk"/>, todas as portas passam o <see cref="DoorsOk"/>, e a peca nao
    /// estoira o tecto de ramos.
    /// </summary>
    public List<Placed> Fits(Target target, Chunk chunk, int rot)
    {
        var rv = chunk.Rots[rot];
        int ax = rv.Anchor % rv.Cols, ay = rv.Anchor / rv.Cols;
        var outp = new List<Placed>();
        foreach (var (ox, oy, d, st) in rv.Doors)
        {
            if (d != target.Dir)
                continue;
            int px = target.X - ox, py = target.Y - oy;   // onde a ancora cai
            int ph = target.H - st;                       // h = alvo.h - degrau da porta
            bool good = true;
            for (int cy = 0; cy < rv.Rows && good; cy++)
                for (int cx = 0; cx < rv.Cols; cx++)
                {
                    uint v = rv.Info[cy * rv.Cols + cx];
                    if (!CellOk(px + cx - ax, py + cy - ay, v, ph))
                    {
                        good = false;
                        break;
                    }
                }
            if (!good || !DoorsOk(px, py, rot, chunk))
                continue;
            var rec = new Placed(px, py, ph, rot, chunk);
            if (P90 >= 0 && P90 < Branch + CountBranches(rec))
                continue;                                 // 0x0063F6FE: excede o tecto de ramos
            outp.Add(rec);
        }
        return outp;
    }

    /// <summary>
    /// <c>0x0063F5C1..0x0063F6A8</c>: percorre TODAS as portas da peca, incluindo a que a liga
    /// ao alvo, mantendo uma caixa acumulada.
    ///
    /// Por porta, com (nx, ny) a celula de la': se estiver fora da grelha de trabalho, so' e'
    /// permitida quando bate com um registo de <c>gen-&gt;0x28..0x2C</c> (os marcadores de
    /// entrada/saida, que vivem fora da grelha); sem marcadores nenhuns e' recusa imediata.
    /// A celula entra na caixa — incluindo as que vieram de um marcador, porque o cliente
    /// salta de volta para a actualizacao da caixa — e a caixa tem de ficar estritamente
    /// abaixo de <c>gen-&gt;0x50</c>/<c>0x54</c>.  <b>Nao ha isencao de rio aqui</b>.
    ///
    /// Duas coisas que isto acerta: a caixa e' CUMULATIVA entre as portas, e a porta de
    /// ligacao nao e' isenta — e' so' a que costuma apontar para a peca de onde viemos, e na
    /// primeira peca aponta para o marcador de entrada, que e' o que a torna legal.
    /// </summary>
    public bool DoorsOk(int px, int py, int rot, Chunk chunk)
    {
        var g = G!;
        int minx = g.MinX, miny = g.MinY, maxx = g.MaxX, maxy = g.MaxY;
        foreach (var (ox, oy, d, _) in chunk.Rots[rot].Doors)
        {
            int nx = px + ox + MapGen.DX[d], ny = py + oy + MapGen.DY[d];
            if (!g.Inside(nx, ny))
            {
                bool achou = false;
                foreach (var m in Doors)
                    if (m.X == nx && m.Y == ny) { achou = true; break; }
                if (!achou)
                    return false;
            }
            if (ny < miny) miny = ny;
            if (nx < minx) minx = nx;
            if (maxy <= ny) maxy = ny;
            if (maxx <= nx) maxx = nx;
            if (maxy - miny >= SizeY || maxx - minx >= SizeX)
                return false;
        }
        return true;
    }

    /// <summary>
    /// <c>0x006465E1</c>: a peca de entrada vai para <c>gen-&gt;0x28..0x2C</c>, NAO para a
    /// grelha.  Fica uma celula fora da grelha de trabalho, e e' a unica razao por que a porta
    /// de ligacao da primeira peca e' legal.  O <c>0x00643820</c> empurra depois as pecas de
    /// saida para o mesmo vector.
    /// </summary>
    public Placed MarkEntrance(int x, int y, int h, string? name = null, int? dir = null)
    {
        Chunk? ch = name is not null && AreaRef.Chunks.TryGetValue(name, out var c) ? c : null;
        // A rotacao do marcador e' a que poe a porta virada **para dentro**, a mesma regra do
        // `0x0063B600` em `PlaceDoors` (`want = Opposite(d)`).  So conta para o desenho — o
        // `DoorsOk` casa os marcadores pela posicao e o `0x006468E4` pelo nome —, mas sem ela a
        // peca de arranque saia sempre com a porta virada a sul e a pre-visualizacao nao
        // desenhava a ligacao ao mapa em tres dos quatro lados.
        int rot = 0;
        if (ch is not null && dir is not null)
        {
            int want = 1 << MapGen.Opposite(dir.Value);
            for (int r = 0; r < 4; r++)
                if ((ch.Rots[r].Info[0] & 0xF) == want) { rot = r; break; }
        }
        var p = new Placed(x, y, h, rot, ch, dir, name);
        Doors.Add(p);
        return p;
    }

    /// <summary>
    /// <c>0x00643490</c>, chamada logo a seguir a clonar o `.lat` (o caminho de rio da fabrica
    /// da grelha).  Percorre a grelha toda e, por cada porta de uma celula do rio que aponte
    /// para <b>fora</b> da grelha de trabalho, regista um marcador na celula de fora.  O nome
    /// e' `cs` quando o bit da direccao esta' no byte 3 do dword de info e `cg` quando nao
    /// esta' (literais em 0x05A10AE4 e 0x05A10AE8), ou seja a nascente e a foz do rio.
    ///
    /// E' isto que torna legal, no modo rio, uma porta virada para fora da grelha.  Sem isto a
    /// `facility` oferecia uma colocacao onde o cliente oferecia duas.
    /// </summary>
    public List<Placed> MarcarFronteiraDoRio()
    {
        var g = G!;
        for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                if (!g.Occupied(x, y))
                    continue;
                uint v = g.At(x, y);
                for (int d = 0; d < 4; d++)
                {
                    if (((v >> d) & 1) == 0)
                        continue;
                    int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
                    if (g.Inside(nx, ny))
                        continue;
                    MarkEntrance(nx, ny, 0, ((v >> (24 + d)) & 1) != 0 ? "cs" : "cg", d);
                }
            }
        return Doors;
    }

    // ---- 0x00640800: escolher a peca -------------------------------------------------
    public List<Chunk> BuildFilters()
    {
        var a = AreaRef;
        bool hasOpen = Branch > 0;
        var outp = new List<Chunk>();
        int n = 1 << Bigs;
        if (P7c != 0 && n > 1 && Rng.UniformA(Mt1, n) != 0)
        {
            var seg = a.GroupList(hasOpen ? 6 : 2);
            Rng.Shuffle(Mt1, seg);
            outp.AddRange(seg);
            seg = a.GroupList(0x800);
            Rng.Shuffle(Mt1, seg);
            outp.AddRange(seg);
        }
        else
        {
            var seg = a.GroupList(hasOpen ? 0x806 : 0x802);
            Rng.Shuffle(Mt1, seg);
            outp.AddRange(seg);
        }
        if (!hasOpen)
            outp.AddRange(a.GroupList(4));            // acrescentado sem baralhar
        if (HasHeight)
        {
            var seg = a.GroupList(River ? 0 : 8);
            Rng.Shuffle(Mt1, seg);
            outp.AddRange(seg);
        }
        return outp;
    }

    /// <summary>
    /// <c>0x00640800</c>: monta a piscina e percorre-a ate' uma candidata ser ACEITE.
    ///
    /// Medido com a sonda E (FINDINGS 15.5): o cliente <b>nao</b> para no primeiro chunk com
    /// candidatas geometricas.  Baralha as candidatas desse chunk, testa cada uma, e passa ao
    /// chunk seguinte quando todas forem recusadas; esgotados todos devolve 0 e o ciclo do
    /// labirinto tenta outra vez.  Um chunk recusado gasta na mesma o baralhar das candidatas.
    ///
    /// O teste de aceitacao so' existe em areas com altura e no modo rio, e custa <b>zero</b>
    /// palavras — uma resposta imperfeita custa a peca certa, nunca o alinhamento do fluxo.
    /// </summary>
    public Placed? ChoosePiece(Target target)
    {
        bool testa = HasHeight || River;
        foreach (var ch in BuildFilters())
        {
            var cands = new List<Placed>();
            for (int rot = 0; rot < 4; rot++)         // 0x00640AB9: as quatro rotacoes, sempre
                cands.AddRange(Fits(target, ch, rot));
            MapGen.Traco?.Invoke("chunk", ch.Short, cands);
            if (cands.Count == 0)
                continue;
            Rng.Shuffle(Mt1, cands);
            var rampas = RampDraw(ch, target);        // 0x00640B6E -> 0x0063D5C0
            if (rampas is not null)
                Rng.Shuffle(Mt1, rampas);             // 0x00641195, registos de 0x28 bytes
            // 0x00641150: as rampas sao embaralhadas a' parte e depois **inseridas no
            // inicio** da piscina principal, ambos os caminhos com `pos = vec.begin`
            // (FINDINGS 30.1).  Nao ha atalho nenhum: a lista fundida e' percorrida por
            // ordem e fica a **primeira que o 0x0063C300 aceitar**.
            var piscina = new List<Placed>();
            if (rampas is not null)
                piscina.AddRange(rampas);
            piscina.AddRange(cands);
            MapGen.Traco?.Invoke("piscina", ch.Short, piscina);
            if (!testa)
                return piscina[0];
            foreach (var c in piscina)
                if (Accepts(c))
                    return c;
        }
        return null;
    }

    /// <summary>
    /// <c>0x0063D5C0</c>, chamada em <c>0x00640B6E</c> uma vez por entrada da piscina que
    /// produziu candidatas, logo a seguir ao baralhar delas.
    ///
    /// Sai sem sortear em tres sitios: area plana, nome que nao comeca por `i` ou `l`, e chunk
    /// do grupo 3 (as proprias rampas).  Depois gasta <b>uma</b> palavra no
    /// <c>uniform_a(mt, 100)</c> de sempre, e o veredicto e' <c>r &lt; f(col) + g(col)</c>, com
    /// a LINHA em <c>gen-&gt;0x94</c> e a coluna em <c>9 - p</c>.  As tabelas foram lidas do
    /// cliente (FINDINGS 26.1) e f+g e' constante por linha, logo a coluna nao decide nada.
    ///
    /// Se aceitar, <b>nao devolve nomes: devolve colocacoes</b>.  Por cada nome (`ia` para o
    /// `i0`, `la` e depois `lb` para o `l0`) refaz o encaixe no alvo com as quatro rotacoes e
    /// fica com as candidatas cuja altura chegue ao nivel do vizinho.
    /// </summary>
    public List<Placed>? RampDraw(Chunk chunk, Target target)
    {
        if (!HasHeight)
            return null;
        string nome = chunk.Short;
        if (nome.Length == 0 || (nome[0] != 'i' && nome[0] != 'l') || chunk.Group == 3)
            return null;
        int r = Rng.UniformA(Mt1, 100);
        int col = IndiceRampa();
        int linha = (P94 >= 0 && P94 < 5) ? P94 : 0;
        // As duas tabelas nao dao so' o limiar: dao o **sentido** da rampa.  Em 0x0063DEA5 o
        // sinal escolhe qual dos dois sitios de append corre, e os dois comparam a altura ao
        // contrario um do outro.  Pelo `f` ficam as rampas que SOBEM, pelo `g` as que DESCEM.
        double d = r - MapGen.RampaF[linha][col];
        bool sobe;
        if (d < 0)
            sobe = true;
        else if (d < MapGen.RampaG[linha][col])
            sobe = false;
        else
            return null;
        var g = G!;
        int nx = target.X + MapGen.DX[target.Dir], ny = target.Y + MapGen.DY[target.Dir];
        int nivel = 0;
        if (g.Inside(nx, ny))
        {
            uint v = g.At(nx, ny);
            nivel = g.Height[g.Idx(nx, ny)]
                  + (((v >> (16 + MapGen.Opposite(target.Dir))) & 1) != 0 ? 1 : 0);
        }
        var lista = new List<Placed>();
        foreach (string n in nome == "i0" ? new[] { "ia" } : new[] { "la", "lb" })
        {
            if (!AreaRef.Chunks.TryGetValue(n, out var ch))
                continue;
            for (int rot = 0; rot < 4; rot++)
                foreach (var c in Fits(target, ch, rot))
                    if (sobe ? c.H >= nivel : c.H < nivel)
                        lista.Add(c);
        }
        // O 0x0063D5C0 acaba aqui: **nao embaralha**.  O embaralhar e' do 0x00641150, que
        // so' corre no caminho do 0x00640800; o corredor do 0x0063A800 usa a mesma funcao e
        // escolhe por sorteio (37.7).
        return lista.Count > 0 ? lista : null;
    }

    /// <summary>
    /// A caixa de conteudo depois de <c>0x0063CE90</c> escrever `rec`: as celulas da peca mais
    /// a celula a seguir a cada porta que caia dentro da grelha (22.3).
    /// </summary>
    public (int MinX, int MinY, int MaxX, int MaxY) CaixaCom(Placed rec)
    {
        var g = G!;
        var rv = rec.Ch!.Rots[rec.Rot];
        int ax = rv.Anchor % rv.Cols, ay = rv.Anchor / rv.Cols;
        int minx = g.MinX, miny = g.MinY, maxx = g.MaxX, maxy = g.MaxY;
        for (int cy = 0; cy < rv.Rows; cy++)
            for (int cx = 0; cx < rv.Cols; cx++)
            {
                int x = rec.X + cx - ax, y = rec.Y + cy - ay;
                minx = Math.Min(minx, x); maxx = Math.Max(maxx, x);
                miny = Math.Min(miny, y); maxy = Math.Max(maxy, y);
            }
        foreach (var (ox, oy, d, _) in rv.Doors)
        {
            int nx = rec.X + ox + MapGen.DX[d], ny = rec.Y + oy + MapGen.DY[d];
            if (g.Inside(nx, ny))
            {
                minx = Math.Min(minx, nx); maxx = Math.Max(maxx, nx);
                miny = Math.Min(miny, ny); maxy = Math.Max(maxy, ny);
            }
        }
        return (minx, miny, maxx, maxy);
    }

    /// <summary>
    /// <c>0x00638A10</c>: a celula a seguir a (x, y) na direccao d aceita um passo — esta'
    /// dentro da grelha de trabalho, (fora do rio) apanha-la nao poe a caixa em
    /// <c>gen-&gt;0x50</c>/<c>0x54</c>, e esta' livre <b>ou</b>, so' no modo rio, e' um `ci`
    /// que o corredor atravessa de traves (as direccoes tem de ser perpendiculares).  E' a
    /// travessia do rio, e e' o que promove a celula a `cx`.
    /// </summary>
    public bool StepOk(int x, int y, int d, (int MinX, int MinY, int MaxX, int MaxY) caixa)
    {
        var g = G!;
        int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
        if (!g.Inside(nx, ny))
            return false;
        if (!River)
        {
            if (Math.Max(caixa.MaxY, ny) - Math.Min(caixa.MinY, ny) >= SizeY)
                return false;
            if (Math.Max(caixa.MaxX, nx) - Math.Min(caixa.MinX, nx) >= SizeX)
                return false;
        }
        if (!g.Occupied(nx, ny))
            return true;
        // 0x00638B24: ocupada, so' passa se for um `ci` atravessado de traves.  E so' isso:
        // a condicao sobre a celula do outro lado nao esta' aqui — esta' no look-ahead.
        int i = g.Idx(nx, ny);
        if (River && (g.Name[i] ?? "") == "ci")
            return ((g.Rot[i] - d) & 1) != 0;
        return false;
    }

    /// <summary>
    /// O <c>9 - p</c> com que <c>0x0063D5C0</c> indexa as duas tabelas.  O <c>gen-&gt;0xBC</c> e
    /// o contador por celula de <c>grid-&gt;0x88</c> ficam a <b>zero</b>: nao estao decifrados e
    /// so' mexem no indice, nunca no numero de palavras.
    /// </summary>
    public int IndiceRampa()
    {
        var g = G!;
        int alt = g.MaxY >= g.MinY ? Math.Max(0, g.MaxY - g.MinY + 1) : 0;
        int larg = g.MaxX >= g.MinX ? Math.Max(0, g.MaxX - g.MinX + 1) : 0;
        int folgaY = Math.Max(1, SizeY - alt);
        int folgaX = Math.Max(1, SizeX - larg);
        double k = P90 < 0 ? 0.4 : 1.0 - P90 / 10.0;
        int n = Math.Max(1, (int)(k * (folgaX * folgaY)));
        int p = (int)((1.0 - 1.0 / n) * 10.0);
        p = p < 0 ? 0 : (p > 8 ? 9 : p);
        return 9 - p;
    }

    // ---- 0x0063C300 / 0x0063BAB0: o look-ahead, deterministico ----------------------
    /// <summary>
    /// <c>0x0063BAB0</c>, lida instrucao a instrucao.  Gasta <b>zero</b> palavras (16.2).
    /// A ordem e': o <c>0x0063CE90</c> escreve a candidata na copia da grelha e mexe na caixa;
    /// se a caixa passou a atingir <c>gen-&gt;0x50</c>/<c>0x54</c> recusa (este teste
    /// <b>nao</b> tem isencao de rio); enumera as portas e fica com as que dao um passo legal;
    /// para cada uma recorre com a piscina 0x806 (mais o grupo 3 com altura e sem rio) para
    /// ver se ha peca que a feche.
    /// </summary>
    public bool Accepts(Placed rec)
    {
        var (minx, miny, maxx, maxy) = CaixaCom(rec);
        if (maxy - miny >= SizeY || maxx - minx >= SizeX)
            return false;
        return RecursaoOk(rec);
    }

    /// <summary>
    /// A metade recursiva do <c>0x0063BAB0</c>.  Primeira passagem: guarda as portas cujo passo
    /// o <c>0x00638A10</c> aceita; se nenhuma aceitar, <b>aceita a candidata</b> — uma peca que
    /// nao abre nada nao tem nada para fechar.  Segunda passagem, so' sobre essas: no rio, se o
    /// alvo for um `ci`, olha-se a celula a seguir ao `ci` no mesmo sentido (fora da grelha
    /// recusa; vazia resolve a porta; senao tem de ter a porta de volta).  Nos outros casos
    /// monta-se a piscina e juntam-se todas as candidatas das quatro rotacoes de todos os
    /// chunks; lista vazia recusa.  Nada disto gasta palavras.
    /// </summary>
    public bool RecursaoOk(Placed rec)
    {
        var g = G!;
        // o cliente clona a grelha e faz roll-back; aqui guarda-se e repoe-se o mesmo que o
        // Python guarda — a profundidade, o numero de peca e a caixa `b*` ficam alterados,
        // tal como no modelo que reproduz as 64 grelhas.
        var salvoInfo = (uint[])g.Info.Clone();
        var salvoName = (string?[])g.Name.Clone();
        var salvoRot = (int[])g.Rot.Clone();
        var salvoHeight = (int[])g.Height.Clone();
        int sMinX = g.MinX, sMinY = g.MinY, sMaxX = g.MaxX, sMaxY = g.MaxY;
        int nPecas = Placed.Count, nBigs = Bigs, nPecaNo = PieceNo;
        Place(rec);
        try
        {
            if (g.MaxY - g.MinY >= SizeY || g.MaxX - g.MinX >= SizeX)
                return false;
            var caixa = (g.MinX, g.MinY, g.MaxX, g.MaxY);
            var legais = new List<ChunkDoor>();
            foreach (var pd in rec.Ch!.Rots[rec.Rot].Doors)
                if (StepOk(rec.X + pd.Cx, rec.Y + pd.Cy, pd.D, caixa))
                    legais.Add(pd);
            if (legais.Count == 0)
                return true;               // 0x0063BB8D: nenhuma porta legal -> aceita
            int mascara = 0x806 | ((HasHeight && !River) ? 8 : 0);
            List<Chunk>? piscina = null;
            foreach (var (ox, oy, d, st) in legais)
            {
                int nx = rec.X + ox + MapGen.DX[d], ny = rec.Y + oy + MapGen.DY[d];
                if (River && (g.Name[g.Idx(nx, ny)] ?? "") == "ci")
                {
                    int x2 = nx + MapGen.DX[d], y2 = ny + MapGen.DY[d];
                    if (!g.Inside(x2, y2))
                        return false;      // a travessia nao tem para onde sair
                    uint v = g.At(x2, y2);
                    if ((v & 0xF) == 0 && ((v >> 8) & 0xFF) == 0)
                        continue;          // do outro lado esta' vazio: serve
                    if (((v >> MapGen.Opposite(d)) & 1) != 0)
                        continue;          // ou tem a porta de volta
                    return false;
                }
                piscina ??= AreaRef.GroupList(mascara);
                var alvo = new Target(nx, ny, rec.H + st, MapGen.Opposite(d));
                bool fechou = false;
                foreach (var ch in piscina)
                {
                    for (int r = 0; r < 4; r++)
                        if (Fits(alvo, ch, r).Count > 0) { fechou = true; break; }
                    if (fechou)
                        break;
                }
                if (!fechou)
                    return false;
            }
            return true;
        }
        finally
        {
            g.Info = salvoInfo; g.Name = salvoName; g.Rot = salvoRot; g.Height = salvoHeight;
            g.MinX = sMinX; g.MinY = sMinY; g.MaxX = sMaxX; g.MaxY = sMaxY;
            Placed.RemoveRange(nPecas, Placed.Count - nPecas);
            Bigs = nBigs; PieceNo = nPecaNo;
        }
    }

    // ---- 0x0063CE90: escrever --------------------------------------------------------
    public void Place(Placed rec)
    {
        var rv = rec.Ch!.Rots[rec.Rot];
        int ax = rv.Anchor % rv.Cols, ay = rv.Anchor / rv.Cols;
        int prof = Prof;
        var g = G!;
        for (int cy = 0; cy < rv.Rows; cy++)
            for (int cx = 0; cx < rv.Cols; cx++)
                g.Write(rec.X + cx - ax, rec.Y + cy - ay, rv.Info[cy * rv.Cols + cx],
                        rec.Ch.Short, rec.Rot, rec.H, PieceNo);
        // 0x0063D4A4: **depois** de escrever as celulas, o 0x0063CE90 alastra a profundidade
        // a partir da ancora.  E' a unica escrita do grid->0x88 (37.1).
        g.Espalha(rec.X, rec.Y, prof);
        // gen->0x40..0x4C nao e' a caixa das celulas escritas: tambem apanha a celula a
        // seguir a cada porta da peca, que e' exactamente a uniao contra a qual o
        // 0x0063F0B0 testou a candidata (FINDINGS 22.4).  So' as portas que caem DENTRO da
        // grelha contam; a porta de ligacao da primeira peca aponta para o marcador de
        // entrada, e apanha-la estragava todos os mapas.
        foreach (var (ox, oy, d, _) in rv.Doors)
        {
            int nx = rec.X + ox + MapGen.DX[d], ny = rec.Y + oy + MapGen.DY[d];
            if (g.Inside(nx, ny))
            {
                g.MinX = Math.Min(g.MinX, nx);
                g.MinY = Math.Min(g.MinY, ny);
                g.MaxX = Math.Max(g.MaxX, nx);
                g.MaxY = Math.Max(g.MaxY, ny);
            }
        }
        Placed.Add(rec);
        PieceNo++;
        if (rec.Ch.Group == 11)
            Bigs++;
    }

    /// <summary>
    /// A travessia do rio: um `ci` atravessado de traves passa a `cx`.  `ci` e' 0x1000005
    /// (duas portas, N e S) e `cx` e' 0x100000F (as quatro), por isso a promocao abre a celula
    /// nos dois sentidos — e e' o que impede uma segunda travessia da mesma celula.
    /// </summary>
    public Placed? PromoveRio(int x, int y, int d)
    {
        if (!River)
            return null;
        var g = G!;
        if (!g.Inside(x, y))
            return null;
        int i = g.Idx(x, y);
        if ((g.Name[i] ?? "") != "ci")
            return null;
        if (((g.Rot[i] - d) & 1) == 0)
            return null;                        // so' de traves (0x00638A10)
        if (!AreaRef.Chunks.TryGetValue("cx", out var cx))
            return null;
        g.Name[i] = "cx";
        g.Info[i] = (g.Info[i] & 0xFFFFFFF0) | (cx.BaseInfo[0] & 0xF);
        if (MapGen.CaixaNaTravessia)
        {
            g.MinX = Math.Min(g.MinX, x); g.MaxX = Math.Max(g.MaxX, x);
            g.MinY = Math.Min(g.MinY, y); g.MaxY = Math.Max(g.MaxY, y);
        }
        return new Placed(x, y, 0, g.Rot[i], cx);
    }

    /// <summary>
    /// Quando o alvo de arranque cai numa celula do rio, o cliente atravessa-a logo (visto na
    /// `shironia`, onde antes do ciclo do labirinto aparece um `cx` com retorno de dentro do
    /// 0x00646320).  Devolve null quando o alvo nao e' uma celula de rio atravessavel.
    /// </summary>
    public Placed? ArranqueNoRio(Target alvo)
    {
        if (!River)
            return null;
        var g = G!;
        if (!g.Inside(alvo.X, alvo.Y) || !g.Occupied(alvo.X, alvo.Y))
            return null;
        var rec = PromoveRio(alvo.X, alvo.Y, MapGen.Opposite(alvo.Dir));
        if (rec is null)
            return null;
        g.Espalha(alvo.X, alvo.Y, 1);           // 0x00646234: `push 1` -> 0x00639C00
        Placed.Add(rec);
        return rec;
    }

    /// <summary>
    /// <c>0x0063EE90</c>: por cada porta da peca monta o registo
    /// <c>{x+dx, y+dy, h+degrau, direccao}</c> e guarda-o se o <c>0x00638A10</c> disser que o
    /// passo e' legal.  No rio um `ci` atravessavel de traves conta como porta aberta embora a
    /// celula esteja ocupada (FINDINGS 30.2).
    /// </summary>
    public List<Target> OpenDoors(Placed rec)
    {
        var g = G!;
        var caixa = (g.MinX, g.MinY, g.MaxX, g.MaxY);
        var outp = new List<Target>();
        foreach (var (ox, oy, d, st) in rec.Ch!.Rots[rec.Rot].Doors)
        {
            int x = rec.X + ox, y = rec.Y + oy;
            if (StepOk(x, y, d, caixa))
                outp.Add(new Target(x, y, rec.H + st, d));   // a porta pode subir um nivel
        }
        return outp;
    }

    /// <summary><c>0x00635E20</c>: os ramos abertos a mais de uma peca acabada de colocar.</summary>
    public int CountBranches(Placed rec)
    {
        int n = 0;
        foreach (var (ox, oy, d, _) in rec.Ch!.Rots[rec.Rot].Doors)
        {
            int nx = rec.X + ox + MapGen.DX[d], ny = rec.Y + oy + MapGen.DY[d];
            if (G!.Inside(nx, ny) && !G.Occupied(nx, ny))
                n++;
        }
        return Math.Max(0, n - 1);
    }

    /// <summary>
    /// <c>0x0063E2F0</c>: fora do modo rio devolve logo 0.  No rio, mal encontra a primeira
    /// porta que da' para um `ci` poe a flag e <b>recomeca do inicio</b>, e dai em diante apaga
    /// da lista todas as que nao sao `ci`.  Ou seja: <b>havendo travessia do rio disponivel,
    /// so' se consideram as travessias</b>.
    /// </summary>
    public List<Target>? FiltroDoRio(List<Target> doors)
    {
        if (!River)
            return null;
        var g = G!;
        var outp = new List<Target>();
        foreach (var t in doors)
        {
            int nx = t.X + MapGen.DX[t.Dir], ny = t.Y + MapGen.DY[t.Dir];
            if (g.Inside(nx, ny) && (g.Name[g.Idx(nx, ny)] ?? "") == "ci")
                outp.Add(t);
        }
        return outp.Count > 0 ? outp : null;
    }

    /// <summary><c>gen-&gt;0xB4 += 0x00635E20(...)</c>, com a guarda do proprio cliente.</summary>
    public void BumpBranch(Placed rec)
    {
        if (Branch == 0 || P90 >= 0)
            Branch += CountBranches(rec);
    }

    // ---- 0x00645560: o ciclo do labirinto --------------------------------------------
    /// <summary>
    /// Um giro do ciclo de <c>0x00645560</c>, a partir da peca no topo da pilha.
    ///
    /// O <c>0x0063EE90</c> monta as portas abertas do topo; sem nenhuma, o topo sai da pilha.
    /// O <c>0x0063E2F0</c> pergunta se alguma delas da' para um `ci`: se der, <b>a jogada e' so'
    /// de travessia</b> — a lista fica reduzida a essas, baralhada quando ha mais do que uma, e
    /// atravessam-se <b>todas</b>, sem colocar peca nenhuma neste giro.  So' quando nao ha
    /// travessia nenhuma e' que se sorteia uma porta (uma palavra) e se chama o
    /// <c>0x00640800</c>.
    /// </summary>
    public (string Tipo, List<Placed> Recs) Turno(Placed top)
    {
        var g = G!;
        Prof = g.Depth[g.Idx(top.X, top.Y)] + 1;      // 0x00645613 + 0x0064569A
        if (Prof > ProfMax)
            ProfMax = Prof;                           // gen->0xBC
        var doors = OpenDoors(top);
        if (doors.Count == 0)
            return ("nada", new List<Placed>());
        var rio = FiltroDoRio(doors);
        if (rio is not null)
        {
            if (rio.Count > 1)
                Rng.Shuffle(Mt1, rio);                // 0x00645CB3
            var novos = new List<Placed>();
            foreach (var t in rio)
            {
                var rec = Travessia(t.X, t.Y, t.Dir);
                if (rec is null)
                    continue;
                // A celula atravessada tambem leva profundidade: a sonda J mostra que o
                // grid->0x88 do `seabed` conta as travessias como pecas (FINDINGS 34).
                g.Espalha(rec.X, rec.Y, Prof);        // 0x00645820 -> 0x0063A0F3
                Placed.Add(rec);
                novos.Add(rec);
            }
            // Se nenhuma travessia pegar, o cliente ficaria a girar sobre o mesmo topo; como
            // nao fica, ou o 0x00639C00 nunca falha aqui ou a condicao ja' esta' no step_ok.
            return novos.Count > 0 ? ("rio", novos) : ("nada", new List<Placed>());
        }
        Target porta = doors.Count == 1 ? doors[0] : doors[Rng.UniformA(Mt1, doors.Count)];
        var alvo = new Target(porta.X + MapGen.DX[porta.Dir], porta.Y + MapGen.DY[porta.Dir],
                              porta.H, MapGen.Opposite(porta.Dir));
        var peca = ChoosePiece(alvo);
        if (peca is null)
            return ("nada", new List<Placed>());
        Place(peca);
        BumpBranch(peca);
        return ("peca", new List<Placed> { peca });
    }

    /// <summary><c>0x00639C00</c>: atravessa o `ci` que esta' a seguir a' porta (x, y, d).</summary>
    public Placed? Travessia(int x, int y, int d)
        => PromoveRio(x + MapGen.DX[d], y + MapGen.DY[d], d);

    public int Grow(int limit = 400)
    {
        int top = Placed.Count - 1;
        int steps = 0;
        while (top >= 0 && steps < limit)
        {
            steps++;
            var (tipo, recs) = Turno(Placed[top]);
            if (tipo == "nada")
            {
                top--;
                continue;
            }
            if (tipo == "peca")
                Trace.Add(recs[0]);
            top = Placed.Count - 1;
        }
        return steps;
    }

    // ---- 0x0063E4D0: os pontos de encosto na fronteira da caixa ----------------------
    /// <summary>
    /// <c>0x0063E4D0</c>: as celulas da fronteira onde uma porta pode encostar.  Comeca pela
    /// <b>linha exterior</b> da caixa da grelha no lado pedido e, se ela nao der nenhuma
    /// candidata, anda uma linha para dentro e repete.  Dentro de cada linha percorre x por
    /// dentro de y.
    ///
    /// Uma celula serve quando tem portas e os bytes 1, 2 e 3 do dword de info estao a
    /// <b>zero</b> (sem degraus e sem bits de rio), o nome <b>nao comeca por `c`</b> (as
    /// celulas do rio nao servem de ancora), e o corredor que dela sai chega ao comprimento
    /// pedido.  O comprimento comeca em <c>grid-&gt;0x88[celula] + 1</c> — e' por isso que o
    /// minLen da saida quer dizer <b>"longe da entrada"</b> e nao "com muito espaco a' frente".
    /// </summary>
    public List<Cand> BoundaryCandidates(int side, int minLen = 0)
    {
        var g = G!;
        int dx = MapGen.DX[side], dy = MapGen.DY[side];
        int x0 = g.BMinX, y0 = g.BMinY, x1 = g.BMaxX, y1 = g.BMaxY;
        if (side == 0) y1 = y0;
        else if (side == 1) x0 = x1;
        else if (side == 2) y0 = y1;
        else x1 = x0;
        for (int passo = 0; passo < 64; passo++)
        {
            var outp = new List<Cand>();
            for (int y = y0; y <= y1; y++)                 // 0x0063E9D5: `y` por fora
                for (int x = x0; x <= x1; x++)             // 0x0063E9D0: `x` por dentro
                {
                    if (!g.Inside(x, y))
                        continue;
                    uint v = g.At(x, y);
                    if ((v & 0xF) == 0 || (v >> 8) != 0)
                        continue;
                    string nm = g.Name[g.Idx(x, y)] ?? "";
                    if (nm.Length == 0 || nm[0] == 'c')
                        continue;
                    if (Corredor(x, y, side).N >= minLen)   // 0x0063E902
                        outp.Add(new Cand(x, y, g.Height[g.Idx(x, y)], side));
                }
            if (outp.Count > 0)
                return outp;
            x0 -= dx; x1 -= dx; y0 -= dy; y1 -= dy;
            if (!g.Inside(x0, y0) && !g.Inside(x1, y1))
                return new List<Cand>();
        }
        return new List<Cand>();
    }

    /// <summary>
    /// O comprimento do corredor que sai de (x, y) para `side`, e ate' onde chega.  Comeca na
    /// profundidade da celula mais um e anda enquanto o passo seguinte ficar dentro da grelha
    /// e, fora do rio, nao estourar <c>gen-&gt;0x50</c>/<c>0x54</c>.  Fora do rio, uma celula
    /// ocupada no caminho <b>rejeita a candidata</b> (devolve -1).
    /// </summary>
    public (int N, int X, int Y) Corredor(int x, int y, int side)
    {
        var g = G!;
        int dx = MapGen.DX[side], dy = MapGen.DY[side];
        int n = g.Depth[g.Idx(x, y)] + 1;          // 0x0063E7F9 + 0x0063E805
        int cx = x, cy = y;
        while (true)
        {
            int nx = cx + dx, ny = cy + dy;
            if (!g.Inside(nx, ny))
                break;                             // 0x0063E832: sai da grelha -> aceita
            if (!River)
            {
                if (Math.Max(g.MaxY, ny) - Math.Min(g.MinY, ny) >= SizeY)
                    break;                         // 0x0063EC0F: a caixa trava -> aceita
                if (Math.Max(g.MaxX, nx) - Math.Min(g.MinX, nx) >= SizeX)
                    break;                         // 0x0063EC33
            }
            n++;                                   // 0x0063E868
            if (g.Occupied(nx, ny))
            {
                // 0x0063E88D: a celula seguinte esta' ocupada.  Fora do rio isso **rejeita a
                // candidata**; no rio o 0x0063EAA8 pergunta se e' um `ci` atravessavel.
                if (!River)
                    return (-1, cx, cy);
                int i = g.Idx(nx, ny);
                if ((g.Name[i] ?? "") != "ci" || ((g.Rot[i] - side) & 1) == 0)
                    return (-1, cx, cy);
            }
            cx = nx; cy = ny;
        }
        return (n, cx, cy);
    }

    /// <summary>
    /// <c>0x00640502</c>, ainda dentro do <c>0x0063F970</c>: com <c>gen-&gt;0xA0</c> posto, um
    /// dos lados que <b>nao</b> receberam tipo nenhum vai para o mapB com o chunk `n0`.  O
    /// sorteio custa <b>uma palavra</b>, e so' quando sobra mais do que um lado (38.8).
    /// E' este o mecanismo que da' saida as zonas cujo warp tem <c>doorId = -6</c>.
    /// </summary>
    public (List<(int Side, string? Name)> A, List<(int Side, string? Name)> B) SobrasA0(
        List<(int Side, string? Name)> mapa, List<(int Side, string? Name)> mapb, int flagA0)
    {
        if (flagA0 == 0)
            return (mapa, mapb);
        var usados = new HashSet<int>(mapa.Select(t => t.Side));
        usados.UnionWith(mapb.Select(t => t.Side));
        var sobras = Enumerable.Range(0, 4).Where(d => !usados.Contains(d)).ToList();
        if (sobras.Count == 0)
            return (mapa, mapb);
        int k = sobras.Count == 1 ? 0 : Rng.UniformA(Mt1, sobras.Count);
        var novo = new List<(int, string?)>(mapb) { (sobras[k], "n0") };
        return (mapa, novo);
    }

    // ---- 0x00641420: onde o mapa comeca ---------------------------------------------
    /// <summary>
    /// <c>0x00641420</c> no caso normal (plano ou com altura): escolhe uma entrada do mapA ao
    /// acaso — UMA palavra, nenhuma quando o mapa tem uma entrada so', que e' o caso normal — e
    /// deriva o alvo de encaixe do lado dela.  A posicao <b>nao</b> e' aleatoria: e' um canto
    /// fixo da caixa permitida, e como a grelha de trabalho duplica o eixo perpendicular,
    /// <c>size_x - 1</c> e' exactamente a coluna do meio.
    ///
    /// A lista <paramref name="mapa"/> e' <b>consumida</b>, tal como o mapA do cliente encolhe
    /// antes do 0x00643820.
    /// </summary>
    public StartTarget? StartTarget(List<(int Side, string? Name)> mapa)
    {
        if (mapa.Count == 0)
            return null;
        int k = Rng.UniformA(Mt1, mapa.Count);
        var (side, name) = mapa[k];
        mapa.RemoveAt(k);
        if (River)
            return StartTargetRio(side, name);
        int x, y;
        if (side == 3) { x = 0; y = SizeY - 1; }
        else if (side == 0) { x = SizeX - 1; y = 0; }
        else { x = SizeX - 1; y = SizeY - 1; }
        return new StartTarget(x + MapGen.DX[side], y + MapGen.DY[side],
                               AlturaDeArranque(), side, name);
    }

    /// <summary>
    /// A altura com que a entrada entra, quando a area tem altura: <c>gen-&gt;0x94 == 0</c> da'
    /// <c>gen-&gt;0x88 / 2</c>, 2 ou 4 dao <c>gen-&gt;0x88</c>, o resto da' 0.  Medido nas 32
    /// capturas com altura: o `h=` da linha `PECA ret=6465E1` bate sempre.
    /// </summary>
    public int AlturaDeArranque()
    {
        if (!HasHeight)
            return 0;
        if (P94 == 0)
            return MaxHeight / 2;
        if (P94 == 2 || P94 == 4)
            return MaxHeight;
        return 0;
    }

    /// <summary>
    /// O terceiro ramo do <c>0x00641420</c> (0x00641620), o do modo rio.  Em vez do canto fixo,
    /// monta o <b>rectangulo da aresta de entrada</b> sobre a grelha-template (o `.lat` clonado),
    /// <b>embaralha-o</b> (n-1 palavras: e' dai que vinham as "4 palavras a mais no rio" de
    /// 23.5) e fica com a primeira celula que sirva — vazia, ou um `ci` que o corredor de
    /// entrada atravessa de traves.
    /// </summary>
    public StartTarget? StartTargetRio(int side, string? name)
    {
        var g = G!;
        int w = g.W, h = g.H;
        List<(int X, int Y)> cells;
        if (side == 0)
            cells = Enumerable.Range(0, w).Select(x => (x, 0)).ToList();
        else if (side == 1)
            cells = Enumerable.Range(0, h).Select(y => (w - 1, y)).ToList();
        else if (side == 2)
            cells = Enumerable.Range(0, w).Select(x => (x, h - 1)).ToList();
        else
            cells = Enumerable.Range(0, h).Select(y => (0, y)).ToList();
        Rng.Shuffle(Mt1, cells);                      // 0x0064170F
        int d = MapGen.Opposite(side);
        foreach (var (x, y) in cells)
        {
            bool serve = !g.Occupied(x, y);
            if (!serve)
            {
                int i = g.Idx(x, y);
                serve = (g.Name[i] ?? "") == "ci" && ((g.Rot[i] - d) & 1) != 0;
            }
            if (serve)
                return new StartTarget(x + MapGen.DX[side], y + MapGen.DY[side], 0, side, name);
        }
        return null;
    }

    // ---- 0x0063A800: andar num corredor para fora da fronteira -----------------------
    /// <summary>
    /// A peca 1x1 (e a rotacao) cujo nibble de portas e' exactamente <paramref name="mascara"/>.
    /// Com <paramref name="rotPref"/> prefere-se essa rotacao quando ela serve: e' o que o
    /// cliente faz nos corredores do <c>0x0063A800</c>, onde o `i0` sai sempre com a rotacao do
    /// sentido da marcha.
    /// </summary>
    public (Chunk? Ch, int Rot) Peca1x1(uint mascara, int? rotPref = null)
    {
        if (rotPref is int rp)
            foreach (var ch in AreaRef.ChunkList)
            {
                if (ch.Group != 1 && ch.Group != 2)
                    continue;
                var rv = ch.Rots[rp];
                if (rv.Rows == 1 && rv.Cols == 1 && (rv.Info[0] & 0xF) == mascara
                    && (rv.Info[0] & 0x00FF0000) == 0)
                    return (ch, rp);
            }
        foreach (var ch in AreaRef.ChunkList)
        {
            if (ch.Group != 1 && ch.Group != 2)
                continue;
            for (int r = 0; r < 4; r++)
            {
                var rv = ch.Rots[r];
                if (rv.Rows != 1 || rv.Cols != 1)
                    continue;
                if ((rv.Info[0] & 0xF) == mascara && (rv.Info[0] & 0x00FF0000) == 0)
                    return (ch, r);
            }
        }
        return (null, 0);
    }

    /// <summary>
    /// Acrescenta a porta d a' celula (x, y), trocando a peca pela que tem exactamente esse
    /// nibble.  E' o que se ve no despejo do cliente: um `n0` que ganha a saida passa a `i0`,
    /// um `l0` passa a `t0`, um `t0` passa a `x0`.
    /// </summary>
    public bool AbrePorta(int x, int y, int d)
    {
        var g = G!;
        int i = g.Idx(x, y);
        uint mascara = (g.Info[i] & 0xF) | (1u << d);
        if (mascara == (g.Info[i] & 0xF))
            return true;
        var (ch, r) = Peca1x1(mascara);
        if (ch is null)
            return false;
        g.Info[i] = (g.Info[i] & 0xFFFFFFF0) | mascara;
        g.Name[i] = ch.Short;
        g.Rot[i] = r;
        return true;
    }

    /// <summary>
    /// <c>0x0063A800</c>: anda para fora, celula a celula, enquanto a caixa aguentar.  Por cada
    /// passo abre a porta na celula onde esta' e escreve na seguinte uma peca com a porta de
    /// volta.  <b>O corredor tambem sorteia rampa</b>, com a mesma <c>0x0063D5C0</c> do ciclo
    /// (37.7).  Devolve a celula onde o corredor acabou.
    /// </summary>
    public Target ExtendCorridor(Cand cand)
    {
        int x = cand.X, y = cand.Y, h = cand.H, d = cand.Side;
        var g = G!;
        // 0x0063AA3D: o contador arranca na profundidade da **ancora** e sobe um por cada
        // celula nova; e' ele que vai parar ao grid->0x88 de cada uma (0x0063AF79).
        int n = g.Depth[g.Idx(x, y)];
        while (true)
        {
            int nx = x + MapGen.DX[d], ny = y + MapGen.DY[d];
            if (!g.Inside(nx, ny))
                break;
            bool travessia = false;
            if (g.Occupied(nx, ny))
            {
                // No rio o corredor da saida **atravessa** um `ci` como qualquer outro
                // corredor: ve-se no despejo da `shironia` (dois `ci` promovidos a `cx` que o
                // ciclo do labirinto nao tocou) e no da `facility` (um).
                int i = g.Idx(nx, ny);
                if (!(River && (g.Name[i] ?? "") == "ci" && ((g.Rot[i] - d) & 1) != 0))
                    break;
                travessia = true;
            }
            if (!River)
            {
                if (Math.Max(g.MaxY, ny) - Math.Min(g.MinY, ny) >= SizeY)
                    break;
                if (Math.Max(g.MaxX, nx) - Math.Min(g.MinX, nx) >= SizeX)
                    break;
            }
            if (!AbrePorta(x, y, d))
                break;
            n++;
            if (travessia)
            {
                if (PromoveRio(nx, ny, d) is null)
                    break;
                g.Espalha(nx, ny, n);          // 0x0063ACAF -> 0x00639C00 -> 0x005EC410
            }
            else
            {
                var (ch, r) = Peca1x1((1u << d) | (1u << MapGen.Opposite(d)), d);
                if (ch is null)
                    break;
                // 0x0063AF5B: o corredor sorteia rampa com a mesma 0x0063D5C0 do ciclo, o
                // nome que a mascara deu (`i0`) e o alvo {proxima celula, altura, direccao de
                // volta}.  Se ela devolver colocacoes, o 0x0063B1A6 sorteia uma (uma palavra
                // quando ha mais do que uma) e e' essa que se escreve — e dai que saem os
                // `ia` do meio dos corredores do `tundra` (37.7).
                Placed? rec = null;
                var rampas = RampDraw(ch, new Target(nx, ny, h, MapGen.Opposite(d)));
                if (rampas is not null)
                    rec = rampas[rampas.Count == 1 ? 0 : Rng.UniformA(Mt1, rampas.Count)];
                if (rec is not null)
                {
                    var rv = rec.Ch!.Rots[rec.Rot];
                    int ax = rv.Anchor % rv.Cols, ay = rv.Anchor / rv.Cols;
                    for (int cy = 0; cy < rv.Rows; cy++)
                        for (int cx = 0; cx < rv.Cols; cx++)
                            g.Write(rec.X + cx - ax, rec.Y + cy - ay,
                                    rv.Info[cy * rv.Cols + cx], rec.Ch.Short, rec.Rot,
                                    rec.H, PieceNo);
                }
                else
                {
                    g.Write(nx, ny, ch.Rots[r].Info[0], ch.Short, r, h, PieceNo);
                }
                PieceNo++;                     // 0x0063AF8C: gen->0xB8++
                g.Espalha(nx, ny, n);          // 0x0063AF92 -> 0x0063C8F0 -> 0x005EC410
                // 0x0063B055: a altura do passo seguinte sobe um se a celula que acabou de
                // ser escrita tiver o bit do degrau na direccao da marcha
                h = g.Height[g.Idx(nx, ny)] + (((g.At(nx, ny) >> (16 + d)) & 1) != 0 ? 1 : 0);
            }
            x = nx; y = ny;
        }
        AbrePorta(x, y, d);
        return new Target(x, y, h, d);
    }

    // ---- 0x006468E4: entre as duas chamadas do 0x00643820 ---------------------------
    /// <summary>
    /// Apaga o <c>grid-&gt;0x88</c> inteiro e re-numera-o a partir da <b>entrada</b>.  Corre uma
    /// vez por tentativa, entre a chamada do <c>0x00643820</c> que poe as portas de entrada e a
    /// que poe a saida: <c>vector::assign(w*h, -1)</c>, e depois percorre o <c>gen-&gt;0x28</c> e
    /// alastra com profundidade <b>1</b> a partir da celula que esta' no sentido oposto ao `dir`
    /// de cada registo que o <c>0x0062FBD0</c> aceite.
    ///
    /// E' isto que a sonda K viu em 36.2.  A partir daqui o minLen da saida ja' nao significa
    /// "longe do arranque do labirinto" mas <b>"longe da entrada"</b>.
    /// </summary>
    public void RenumeraDoArranque()
    {
        var g = G;
        if (g is null)
            return;
        Array.Fill(g.Depth, -1);
        foreach (var rec in Doors.ToList())
        {
            string nome = rec.Nome ?? rec.Ch?.Short ?? "";
            if (nome.Length == 0 || !(nome[0] == 's' || nome == "ds"))   // 0x0062FBD0
                continue;
            if (rec.Dir is not int d)
                continue;
            int k = MapGen.Opposite(d);                 // 0x00646CE3: (dir + 2) & 3
            g.Espalha(rec.X + MapGen.DX[k], rec.Y + MapGen.DY[k], 1);
        }
    }

    // ---- 0x00638C70: os marcadores de teleporte, o ultimo sorteio de uma tentativa ---
    /// <summary>
    /// <c>0x00638C70</c>.  <paramref name="entries"/> e' a tabela de duas ranhuras em +0x24 do
    /// objecto que a vcall +0x1C devolve, cada uma <c>(nome, idA, idB)</c>.  Agrupa as pecas
    /// colocadas por nome, sorteia uma por ranhura, escreve os dois ids nessa celula e tira-a do
    /// balde para a segunda ranhura nao cair na mesma peca.  Devolve false quando um nome pedido
    /// nao existe — o que faz a tentativa inteira falhar.  Gasta 0, 1 ou 2 palavras.
    /// </summary>
    public bool MarkTeleports(IList<(string Name, int A, int B)> entries)
    {
        var byName = new Dictionary<string, List<int>>();
        for (int i = 0; i < Placed.Count; i++)
        {
            string s = Placed[i].Ch?.Short ?? "";
            if (!byName.TryGetValue(s, out var l))
                byName[s] = l = new List<int>();
            l.Add(i);
        }
        foreach (var (name, a, b) in entries.Take(2))
        {
            if (!byName.TryGetValue(name, out var bucket) || bucket.Count == 0)
                return false;
            int k = Rng.UniformA(Mt1, bucket.Count);
            var p = Placed[bucket[k]];
            bucket.RemoveAt(k);
            Marks.Add((p.X, p.Y, a, b));
        }
        return true;
    }

    // ---- 0x00643820: colocar as pecas de entrada / saida -----------------------------
    /// <summary>
    /// <c>0x00643820</c>, uma chamada por mapa: o mapA com <c>minLen = 0</c> e o mapB com
    /// <c>minLen = gen-&gt;0x98</c>.  Por cada entrada do mapa, <b>por ordem crescente de
    /// lado</b> (e' um <c>std::map</c>): o <c>0x0063E4D0</c> monta as candidatas da fronteira;
    /// sem nenhuma, a tentativa inteira falha; sorteia-se uma (<b>uma palavra</b>, e so' quando
    /// ha mais do que uma); o <c>0x0063A800</c> estica o corredor para fora; e o
    /// <c>0x0063B600</c> guarda a peca uma celula alem do fim do corredor, virada para dentro.
    /// Essa peca <b>nao</b> e' escrita na grelha de trabalho — e' por isso que no despejo final
    /// ela aparece na moldura.
    /// </summary>
    public bool PlaceDoors(IEnumerable<(int Side, string Name)> entries, int minLen = 0)
    {
        foreach (var (side, name) in entries.OrderBy(e => e.Side).ThenBy(e => e.Name, StringComparer.Ordinal))
        {
            if (!AreaRef.Chunks.TryGetValue(name, out var ch))
                return false;
            var cands = BoundaryCandidates(side, minLen);
            if (cands.Count == 0)
                return false;
            int i = cands.Count <= 1 ? 0 : Rng.UniformA(Mt1, cands.Count);
            var fim = ExtendCorridor(cands[i]);
            int tx = fim.X + MapGen.DX[fim.Dir], ty = fim.Y + MapGen.DY[fim.Dir];
            int want = MapGen.Opposite(fim.Dir);
            bool posto = false;
            for (int rot = 0; rot < 4; rot++)
                if ((ch.Rots[rot].Info[0] & 0xF) == (1u << want))
                {
                    Doors.Add(new Placed(tx, ty, fim.H, rot, ch, fim.Dir, name));
                    posto = true;
                    break;
                }
            if (!posto)
                return false;
        }
        return true;
    }
}

/// <summary>Constantes, tabelas e as funcoes de modulo do gerador.</summary>
public static class MapGen
{
    public static readonly int[] DX = { 0, 1, 0, -1 };   // 0=N 1=E 2=S 3=O
    public static readonly int[] DY = { -1, 0, 1, 0 };

    public static int Opposite(int d) => (d + 2) & 3;

    /// <summary>Gancho de diagnostico: (evento, chunk, lista).</summary>
    public static Action<string, string, List<Placed>>? Traco;

    // As duas tabelas do sorteio de rampa vivem em dados do jogo (0x00793670 e 0x007936C0),
    // nao no executavel.  LIDAS DO CLIENTE com a sonda H (FINDINGS 26): os 18 despejos de
    // out/probe_H sao byte a byte iguais.  O veredicto e' `r < f(col) + g(col)`, e **f+g e'
    // constante em todas as colunas de cada linha** — logo o `9 - p` nao mexe no resultado,
    // so' na reparticao entre as duas tabelas.  A linha e' gen->0x94.
    public static readonly double[][] RampaF =
    {
        Enumerable.Repeat(15.0, 10).ToArray(),
        Enumerable.Repeat(60.0, 10).ToArray(),
        Enumerable.Repeat(0.0, 10).ToArray(),
        Enumerable.Repeat(60.0, 5).Concat(Enumerable.Repeat(0.0, 5)).ToArray(),
        Enumerable.Repeat(0.0, 5).Concat(Enumerable.Repeat(60.0, 5)).ToArray(),
    };

    public static readonly double[][] RampaG =
    {
        Enumerable.Repeat(15.0, 10).ToArray(),
        Enumerable.Repeat(0.0, 10).ToArray(),
        Enumerable.Repeat(60.0, 10).ToArray(),
        Enumerable.Repeat(0.0, 5).Concat(Enumerable.Repeat(60.0, 5)).ToArray(),
        Enumerable.Repeat(60.0, 5).Concat(Enumerable.Repeat(0.0, 5)).ToArray(),
    };

    /// <summary>
    /// <c>gen-&gt;0x94</c> escolhe a LINHA das tabelas do sorteio de rampa (25.2) e a altura de
    /// arranque (31.3).  Sai de <c>(char)tabelaA[8]</c> do bloco de <c>0x00647BB0</c> (26.3), que
    /// vem do objecto da zona e <b>nao do packet</b> — procurei-o nos 12 campos do ZoneSettings,
    /// nos 110 valores das InfoTable e no cabecalho do `.lps`, e nao esta' la' (39.2).
    ///
    /// Mas e' <b>constante por area</b>: nas 78 geracoes em que o bloco de init correu, os 21
    /// modelos medidos nao variam nem uma vez.  Fica a tabela do que esta' medido, com 0 por
    /// omissao.  <b>Quando se medir uma area nova, e' aqui que se acrescenta.</b>
    /// </summary>
    public static readonly Dictionary<int, int> P94PorArea = new() { [330] = 1, [520] = 1, [521] = 1 };

    /// <summary>
    /// O <c>gen-&gt;0x94</c> <b>vem do packet</b>: e' o byte 0 do <c>UnkO</c>, com sinal.
    ///
    /// <para>O <c>0x00647EE5</c> le'-o com um <c>MOVSX EAX, BYTE PTR [EBX+0x18]</c>, onde o
    /// <c>EBX</c> e' o registo do <c>ZoneSettingsExtra</c> e o <c>+0x18</c> e' o <c>UnkO</c>;
    /// a instrucao seguinte e' o <c>MOV [EDI+0x94], EAX</c>.  Ou seja o <c>UnkO</c> nao e' um
    /// numero: e' quatro bytes, e o primeiro e' o <b>modo de altura</b> — 0 poe a altura
    /// inicial a meio, 1 no minimo, 2 no maximo (41.7).</para>
    ///
    /// <para>Conferido contra o cliente: das 137 geracoes que as sondas registaram, 108 tem a
    /// semente numa zona do corpus, e nas <b>108</b> o <c>p94</c> medido e' exactamente este
    /// byte — sem uma excepcao.  A <see cref="P94PorArea"/> fica como recurso para quando nao
    /// ha' <c>UnkO</c> nenhum, que e' o caso de uma zona feita a' mao sem linha no
    /// `infotables.txt`.</para>
    /// </summary>
    public static int P94DoUnkO(int unkO) => unchecked((sbyte)(unkO & 0xFF));

    /// <summary>
    /// As areas em que o <c>gen-&gt;0x94</c> foi mesmo <b>medido</b> no cliente (as que
    /// aparecem nas capturas das sondas).  Serve so' para a ferramenta poder dizer a
    /// diferenca entre "medido e deu 0" e "nunca foi medido, assume-se 0" — a geracao nao
    /// le' isto.  Das 12 areas com altura, seis estao aqui: 320, 321, 510 e 511 deram 0 e
    /// 520 e 521 deram 1; a setima e' a 720 (Kuron), medida na sonda M e tambem 0 (40.16).
    /// </summary>
    public static readonly HashSet<int> P94Medidos = new()
    {
        310, 311, 320, 321, 330, 410, 411, 420, 421, 430, 431,
        510, 511, 520, 521, 530, 610, 611, 620, 630, 710, 720,
    };

    /// <summary>
    /// A linha das tabelas de rampa.  Zero quando o bloco de <c>0x00647BB0</c> nao correu — nesse
    /// caso <c>gen-&gt;0x94</c> fica no valor por omissao, tal como o 0x98 e o 0x9C (26.3).
    /// </summary>
    public static int P94DaArea(int modelo, bool initCorreu = true)
        => initCorreu && P94PorArea.TryGetValue(modelo, out int v) ? v : 0;

    // --- o modo rio ---------------------------------------------------------------------
    // O esqueleto do `.lat` NAO semeia gen->0x40..0x4C: a fabrica da grelha so' escreve a
    // caixa nos dois caminhos sem rio (24.5).
    public const bool CaixaNaTravessia = true;   // a travessia poe a celula na caixa de conteudo
    public const bool RioCaixaLimpa = true;
    // 0x005E7E10 perdoa um desacordo de porta quando o vizinho e' um `ci` (24.2).  A paridade
    // nao esta' la' — so' no 0x00638A10 — e exigi-la tambem aqui nao muda nada no corpus.
    public const bool RioPermeavel = true;
    public const bool ParidadeCi = false;

    /// <summary>
    /// O nome que o <c>0x0063F970</c> escreve numa entrada do mapA ou do mapB (41.17).
    ///
    /// <para>Nao vem do modelo nem de preferencia nenhuma: vem do `.lps`, de uma tabela
    /// <c>modelo -&gt; nome de peca</c> com o modelo da zona do outro lado da porta a quatro
    /// digitos como chave.  Quando a chave falta — ou quando nao se sabe que zona esta' do outro
    /// lado — o cliente escreve o literal, e se o literal nao for peca do modelo cai no
    /// `n0`.</para>
    /// </summary>
    private static string? NomeDeParam(Area area, Dictionary<string, LpsParam> tabela,
                                      int? modeloVizinho, string omissao)
    {
        if (modeloVizinho is int m
            && tabela.TryGetValue(m.ToString("0000"), out var p)
            && p.Text is string nome
            && area.Chunks.ContainsKey(nome))
            return nome;
        foreach (string n in new[] { omissao, "n0" })
            if (area.Chunks.ContainsKey(n))
                return n;
        return null;
    }

    /// <summary>
    /// O chunk com que o <c>0x00643820</c> escreve a peca de saida.  <b>Duas fontes, por esta
    /// ordem:</b> o <c>paramsB["goal_to_boss"]</c> quando a saida da' para a area de <b>boss</b>
    /// (uma zona de mapa fixo) e o modelo o declarar; senao o <c>paramsD[destino]</c>, com o
    /// literal `g0` quando a chave falta.
    ///
    /// <para><b>O Sanctum (modelo 530) nao tem `g0` nem `g1`</b> e o cliente usa o `n0` — ve-se
    /// na sonda L, <c>MAPA lado=0 nome="n0" minLen=4</c>.  Enquanto eu tratava a falta do `g*`
    /// como "nao ha porta de saida", os mapas do Sanctum saiam uma celula mais pequenos do que os
    /// do cliente (38.7).  No modo rio o nome vem do proprio `.lat` clonado e nao desta tabela,
    /// e o <see cref="RiverGrid"/> ja' copia os nomes das celulas (41.17).</para>
    /// </summary>
    public static string? NomeDaSaida(Area area, int? modeloDestino = null,
                                      bool destinoFixo = false)
    {
        // Se a saida der para a area de BOSS — uma zona de mapa FIXO — e o modelo declarar
        // `goal_to_boss` no `paramsB`, e' esse o nome.  So' dois dos 220 modelos o declaram, o
        // `sn_0620` e o `sn_0621`, ambos com `g1`.  A regra saiu do teste ponta-a-ponta (55.8):
        // a area 2 do Seabed sai para o boss e o cliente escreve `g1`, enquanto a area 1 do
        // MESMO modelo sai para uma area normal e escreve `g0` — e o `paramsD` do `sn_0620`
        // esta' vazio, logo sem isto as duas davam `g0`.
        if (destinoFixo
            && area.Lps.ParamsB.TryGetValue("goal_to_boss", out var alvo)
            && alvo.Text is string nomeBoss
            && area.Chunks.ContainsKey(nomeBoss))
            return nomeBoss;
        return NomeDeParam(area, area.Lps.ParamsD, modeloDestino, "g0");
    }

    /// <summary>
    /// O chunk da peca de entrada: <c>paramsC[origem]</c>, senao o literal `s0`, senao `n0`.
    ///
    /// <para><paramref name="modeloOrigem"/> e' o modelo da zona de <b>onde se vem</b>;
    /// <c>null</c> quer dizer que a entrada foi declarada pelo <c>zoneUnknown1</c> e nao por um
    /// warp, que e' o caso que leva o literal.  Conferido em 83 de 83 capturas das sondas pelo
    /// `tools/confereentrada.py` (41.17), contra as 31 em 84 da preferencia fixa que estava
    /// aqui antes.</para>
    /// </summary>
    public static string? NomeDaEntrada(Area area, int? modeloOrigem = null)
        => NomeDeParam(area, area.Lps.ParamsC, modeloOrigem, "s0");

    // ---- a familia de reskin do pos-processamento (41.18) -----------------------------
    // O `0x00633D30`, que o `ExtraPassFlag` abre, troca o nome de TODAS as celulas colocadas
    // por uma peca da familia `d*`.  A escolha nao e' preferencia nem sorteio: a familia tem,
    // para cada forma, a peca com o MESMO dword de `info` e o nibble de cima posto.

    /// <summary>Os chunks da familia de reskin <c>letra*</c> do modelo.</summary>
    public static List<Chunk> MembrosDaFamilia(Area area, char letra = 'd')
        => area.Chunks.Values
               .Where(c => c.Short.Length == 2 && c.Short[0] == letra && c.Short[1] != '0')
               .ToList();

    /// <summary>
    /// A peca da familia <c>letra*</c> que substitui <paramref name="nome"/>, ou <c>null</c>.
    ///
    /// <para>A regra, medida nas 10 celulas do despejo do Kodotta: a candidata e' a peca da
    /// familia cujo <c>info</c> tem a mesma parte baixa — mascara de caminhos, degraus e o
    /// indice de sub-celula que distingue o `la` do `lb` — e a mesma <b>porta</b>, e cujo
    /// nibble de cima e' um sub-conjunto dessa mascara.  Havendo mais do que uma, fica a de
    /// nibble <b>cheio</b> (<c>info | ((info &amp; 0xF) &lt;&lt; 28)</c>), que e' a que o
    /// cliente escolheu nas tres celulas em que havia escolha.  Nada disto roda: o
    /// pos-processamento so' troca o nome.</para>
    /// </summary>
    public static string? ReskinDe(Area area, string nome, char letra = 'd')
    {
        if (!area.Chunks.TryGetValue(nome, out var c) || c.Cols * c.Rows != 1)
            return null;
        uint baseInfo = c.BaseInfo[0];
        var cands = MembrosDaFamilia(area, letra)
            .Where(f => f.Cols * f.Rows == 1
                        && (f.BaseInfo[0] & 0x0FFFFFFFu) == (baseInfo & 0x0FFFFFFFu)
                        && f.Door.SequenceEqual(c.Door))
            .ToList();
        if (cands.Count == 0)
            return null;
        uint cheio = baseInfo | ((baseInfo & 0xFu) << 28);
        var exacto = cands.FirstOrDefault(f => f.BaseInfo[0] == cheio);
        if (exacto is not null)
            return exacto.Short;
        return cands.Count == 1 ? cands[0].Short : null;
    }

    /// <summary>
    /// Corre a substituicao sobre a grelha, no sitio, e devolve as celulas trocadas.
    ///
    /// <para><b>Nao consome palavras do MT</b>: o passe corre dentro do pos-processamento,
    /// depois do ponto em que a sonda B despeja a grelha e em que o custo em palavras e'
    /// conferido, por isso nao pode entrar nessa contabilidade (41.18).</para>
    /// </summary>
    public static List<(int X, int Y, string De, string Para)> AplicaReskin(
        Grid gr, Area area, char letra = 'd')
    {
        var trocas = new List<(int, int, string, string)>();
        for (int i = 0; i < gr.Name.Length; i++)
        {
            string? nome = gr.Name[i];
            if (string.IsNullOrEmpty(nome))
                continue;
            string? novo = ReskinDe(area, nome, letra);
            if (novo is not null && novo != nome)
            {
                gr.Name[i] = novo;
                trocas.Add((i % gr.W, i / gr.W, nome, novo));
            }
        }
        return trocas;
    }

    /// <summary>
    /// O enchimento inacessivel do pos-processamento: o <c>z0</c> do <c>0x005E9A30</c> (41.18).
    ///
    /// <para><b>A regra, medida nas duas grelhas pos-pos-processamento do colega e certa em 30
    /// de 30 celulas:</b> uma celula vazia da grelha final leva <c>z0</c> quando <b>ao menos um
    /// dos 8 vizinhos</b> esta' ocupado por uma peca a serio.  As que nao tocam o mapa nem de
    /// lado nem de esquina ficam vazias — numa 6x6 foram quatro, os cantos que a moldura deixa
    /// isolados.</para>
    ///
    /// <para><b>A rotacao sai do MT1</b>, <c>UniformA(mt, 4)</c> por celula, pela mesma
    /// varredura row-major, a continuar de onde a geracao o deixou — conferido em <b>174 de
    /// 174</b> celulas nas nove zonas da sonda R cujo modelo nao tem o grupo 9 (41.20).
    /// Passa-se <paramref name="mt"/> para a ter; sem ele fica a 0.  Nos modelos <b>com</b>
    /// grupo 9 o segundo passe reescreve as rotacoes e esta conta deixa de bater (5 em 13, que
    /// e' o acaso), por isso ai' nao se sorteia.</para>
    ///
    /// <para>O cliente poe tambem <c>flags |= 4</c> (a marca de inacessivel), que esta grelha
    /// nao guarda.</para>
    ///
    /// <para><b>Nao consome palavras do MT1 alem destas</b> — corre depois do ponto que os
    /// conferidores medem.  Nos modelos com o grupo 9 (<c>zn zi zl zt zx</c>) corre a seguir o
    /// <see cref="SegundoPasseZ"/>, que reesculpe parte destes <c>z0</c> em corredores falsos
    /// com um TERCEIRO fluxo de MT.</para>
    /// </summary>
    public static List<(int X, int Y)> EnchimentoZ0(Grid gr, Area? area = null,
                                                   Mt19937? mt = null)
    {
        var alvo = new List<(int X, int Y, int I)>();
        for (int i = 0; i < gr.Name.Length; i++)
        {
            if (!string.IsNullOrEmpty(gr.Name[i]))
                continue;
            int x = i % gr.W, y = i / gr.W;
            bool toca = false;
            for (int dy = -1; dy <= 1 && !toca; dy++)
                for (int dx = -1; dx <= 1 && !toca; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    int vx = x + dx, vy = y + dy;
                    if (vx >= 0 && vx < gr.W && vy >= 0 && vy < gr.H
                        && !string.IsNullOrEmpty(gr.Name[vy * gr.W + vx]))
                        toca = true;
                }
            if (toca)
                alvo.Add((x, y, i));
        }
        // Sorteia-se **sempre** que haja MT.  Durante algum tempo nao se sorteava nos modelos
        // com o grupo 9, porque la' a conta dava 5 em 13 e parecia acaso; com o segundo passe
        // modelado sabe-se porque e': ele reescreve a rotacao das celulas que renomeia, e as
        // que deixa `z0` ficam com esta.
        foreach (var (_, _, i) in alvo)
        {
            gr.Name[i] = "z0";
            gr.Rot[i] = mt is not null ? Rng.UniformA(mt, 4) : 0;
        }
        return alvo.Select(a => (a.X, a.Y)).ToList();
    }

    /// <summary>
    /// O modelo tem o grupo 9 (<c>zn zi zl zt zx</c>)?
    ///
    /// <para>E' a condicao de entrada do <see cref="SegundoPasseZ"/>: o cliente procura os
    /// cinco nomes no catalogo e sai se faltar um.  O `sn_0311` nao tem nenhum `z*` — nem o
    /// proprio `z0` — e foi ai' que o cliente deixou os 12 `z0` como estavam, o que e' o que
    /// permitiu separar os dois passes um do outro.</para>
    /// </summary>
    public static bool TemZModelados(Area area)
        => area.ByGroup.TryGetValue(9, out var cs) && cs.Count >= 5;

    /// <summary>
    /// A rotacao por mascara do segundo passe, lida da tabela de 16 entradas em
    /// <c>0x05CD5F20</c> (oito bytes cada: o nome, que o carregador relocaliza, e a rotacao).
    /// O nome deduz-se da mascara — e' a peca do grupo 9 cuja mascara base, rodada por este
    /// valor, da' a mascara pedida — e esta' conferido nas 16 entradas.
    /// </summary>
    private static readonly int[] RotZ2 = { 0, 2, 1, 0, 0, 0, 3, 1, 3, 1, 1, 2, 2, 3, 0, 0 };

    /// <summary>
    /// O sorteio do <c>0x005E8D30</c> com a gama <c>{0.0f, 1.0f}</c> que o chamador lhe poe na
    /// pilha: <c>float(palavra) / float(0xFFFFFFFF)</c>, com os dois lados arredondados para
    /// float de 32 bits antes da divisao (o denominador da' 4294967296f).
    /// </summary>
    public static float Uniforme01(Mt19937 mt) => (float)mt.NextU32() / (float)4294967295.0;

    /// <summary>
    /// O nome e a rotacao que a tabela do segundo passe da' a uma mascara de saidas.
    ///
    /// <para>Para as mascaras 1..15 e' a peca do grupo 9 com aquela forma; para a mascara
    /// <b>0</b> e' o literal <c>z0</c>, e a escrita so' acontece se o catalogo tiver mesmo essa
    /// peca — o `sn_0630` tem `z3` no lugar dela, a procura falha, o ponteiro da variante sai
    /// nulo e a celula fica como o primeiro passe a deixou.</para>
    /// </summary>
    public static (string? Nome, int Rot) PecaZ2(Area area, int mascara)
    {
        int rot = RotZ2[mascara & 0xF];
        if (mascara == 0)
            return (area.Chunks.ContainsKey("z0") ? "z0" : null, rot);
        if (area.ByGroup.TryGetValue(9, out var cs))
            foreach (var c in cs)
                if (ChunkMath.RotPaths(c.BaseInfo[0] & 0xF, rot) == (uint)mascara)
                    return (c.Short, rot);
        return (null, rot);
    }

    /// <summary>
    /// O segundo passe dos <c>z*</c>: o <c>0x005E8030</c>, lido do binario e conferido na sonda R.
    ///
    /// <para>O <c>0x006476A7</c> chama-o uma vez por celula, em varredura row-major, com um
    /// <b>terceiro</b> MT semeado com a semente da zona (<c>gen-&gt;0x84</c>).  Cada chamada e'
    /// um alagamento:</para>
    /// <code>
    /// exige que o catalogo tenha os cinco nomes do grupo 9, senao sai logo
    /// pilha = [a celula de partida]
    /// enquanto houver pilha:
    ///     (x, y) = pilha.pop()            // LIFO: o ultimo que entrou
    ///     so' interessa se o nome for `z0`
    ///     marca a celula com 0x20000000
    ///     por cada lado N, E, S, O:
    ///         o vizinho tem de estar na grelha e **a' mesma altura**
    ///         se for enchimento ainda por marcar: sorteia, abre com 0,7 e empilha-o
    ///         se for peca do grupo 9 que abra para mim: liga sem sortear
    ///     escreve a peca que a mascara pedir
    /// </code>
    ///
    /// <para><b>Nao ha teste de ja-visitado na celula que sai da pilha</b>: uma que fique
    /// <c>z0</c> volta a ser processada quando a varredura de fora lhe chegar, e dessa vez os
    /// vizinhos ja' estao marcados, por isso nao se sorteia nada.</para>
    ///
    /// <para>Medido contra a sonda R, nas quatro zonas cujo modelo tem o grupo 9: <b>70 de 70
    /// celulas</b> com o nome <b>e</b> a rotacao certos.</para>
    /// </summary>
    /// <summary>
    /// O ICE <b>nao tem tecto</b>: o <c>maxIce</c> do <c>.lps</c> <b>e'</b> o numero de variantes
    /// que existem.
    /// <para>Conferido contra o cliente vivo (93 132 ficheiros em <c>data/win32</c>): para cada
    /// chunk de uma celula dos 12 modelos da sonda R, o numero de
    /// <c>stage/sn_&lt;mod&gt;/ln_&lt;mod&gt;_&lt;chunk&gt;_&lt;NN&gt;.ice</c> que existem e'
    /// <b>exactamente</b> o <c>maxIce</c>, sem uma excepcao.  O nome em <c>data/win32</c> e' o md5
    /// do caminho logico.</para>
    /// <para>Aqui esteve um <c>IceTecto = 4</c>.  <b>Era um artefacto de um modelo mal
    /// atribuido:</b> a pasta <c>tunnels_a1</c> da sonda R nao e' o <c>sn_0411</c> (que declara
    /// <c>bgm = mu_11_bgm_desert</c>) mas o <c>sn_0420</c>/<c>sn_0421</c>, e com o modelo certo a
    /// regra sem tecto reproduz as 38 celulas dessa zona com uma expectativa de falso positivo de
    /// 9e-19.  Ver FINDINGS 47.10.</para>
    /// </summary>
    /// Atribui as variantes <b>ICE</b> (<c>cell+0x0A</c>) e <b><c>_ob0*</c></b>
    /// (<c>cell+0x0C</c>).  Porto directo de <c>mapgen.passe_variantes</c>.
    /// <para><b>Quem entra no passe do ICE</b> — lido no <c>0x005ECBD0</c>, medido na corrida
    /// AJ+AK: a celula esta' ocupada, o byte 1 do <c>Info</c> nao tem bits acima do 0 (e' a
    /// <b>ancora</b> da peca), e o ICE lido com sinal e' negativo (ainda esta' a
    /// <c>FFFF</c>).</para>
    /// <para><b>O valor</b> nao e' <c>UniformA(mt1, MaxIce)</c>: e' um <b>valor de uma lista de
    /// candidatos</b> que a funcao monta.  A lista arranca no vector de recursos do chunk
    /// (<c>0..MaxIce-1</c>, ou os sufixos <c>_8N</c> nos chunks de varias celulas, que trazem
    /// <c>MaxIce = 0</c>), e nos corredores (<c>i*</c>) e nas curvas (<c>l*</c>) com tres ou mais
    /// candidatos <b>tira de la' a variante de cada vizinho do mesmo chunk ligado por porta</b>.
    /// Com a lista vazia nao se escreve nada, com um candidato escreve-se esse e nao se gasta
    /// palavra, e com dois ou mais sorteia-se um indice e escreve-se o <b>valor</b>.</para>
    /// <para>E' essa remocao que explica as seis celulas que o modelo antigo errava: o
    /// <c>UniformA</c> com o <c>MaxIce</c> acertava o numero de palavras mas, numa lista
    /// encurtada, o indice aponta para outro valor.  Com a regra toda: <b>894/894 valores e
    /// 870/870 listas</b> na corrida AJ+AK, e as palavras gastas batem nos 70 segmentos.</para>
    /// <para><b>O <c>_ob0*</c> e' outra coisa:</b> escreve-se quando o chunk tem
    /// <c>MaxOb &gt; 1</c>, so' em chunks de uma celula, e o valor e' <b>um por zona</b> e
    /// <b>determinista</b> — ver <see cref="ObIdDaZona"/>.  Entra por argumento
    /// (<paramref name="obId"/>) para o passe nao ter de conhecer o packet.</para>
    /// <para><b>Este passe gasta palavras do <c>mt1</c> partilhado</b>, por isso corre no FIM do
    /// encadeamento, depois do <see cref="SegundoPasseZ"/> — como no cliente.</para>
    /// </summary>
    /// <summary>
    /// O id da variante <c>_ob0*</c> de uma zona: o <b>id do campo 1 da <c>DecorationTable</c></b>
    /// (a antiga <c>InfoTable1</c>, a <c>MAGIC_5</c> em memoria).
    /// <para><b>Nao ha sorteio.</b>  O cliente tira uma palavra do Twister proprio do passe e usa o
    /// id do campo se <c>chance &gt; rnd % 100000</c> — mas varrendo os 783 packets as dez chances
    /// de um campo sao <b>sempre</b> `0` ou `100000`, e 3531 de 3531 campos sao planos.  Com
    /// <c>rnd % 100000</c> em <c>[0, 99999]</c>, o `100000` passa sempre e o `0` nunca passa, logo o
    /// resultado e' o id e mais nada (FINDINGS 48.5).  Nas 1177 zonas os ids sao so' `-1`, `0` e
    /// `1`, e so' o <b>slot 0</b> e' usado.</para>
    /// <para>Isto confere com a medicao independente do lado da grelha: nas 13 zonas da sonda R o
    /// <c>_ob0*</c> tem, em cada uma, <b>exactamente um valor</b> (47.10).</para>
    /// <para>Devolve <c>0</c> quando a zona nao traz tabela ou o id e' negativo — e' o que o
    /// construtor deixa.</para>
    /// </summary>
    public static int ObIdDaZona(ZoneInput z)
    {
        var campos = z.It1?.Fields;
        if (campos is null || campos.Count == 0)
            return 0;
        int id = campos[0].Id;
        return id < 0 ? 0 : id;
    }

    /// <summary>
    /// Os dois <b>big chunks</b> da zona com a variante de cada um: <c>(BigChunk1,
    /// BigChunk1Variant)</c> e <c>(BigChunk2, BigChunk2Variant)</c>, ou seja o
    /// <c>UnkP</c>/<c>UnkQ</c> e o <c>UnkW</c>/<c>UnkY</c>.  O nome vem dos dois bytes baixos da
    /// palavra, em ASCII little-endian (<c>13681 = 0x3571 = "q5"</c>).
    /// <para>E' esta a lista de candidatos de um chunk de <b>varias</b> celulas: o vector de
    /// recursos dele vem vazio (<c>MaxIce = 0</c>) e a lista sai inteira destes pares.  Medido
    /// 13/13 na corrida AJ+AK, contra 12/13 do <c>Multi80</c> do `.lps` (FINDINGS 55.7).</para>
    /// </summary>
    public static List<(string Nome, int Valor)> GrandesDaZona(ZoneInput z)
    {
        var fora = new List<(string, int)>();
        foreach (var (palavra, variante) in new[] { (z.UnkP, z.UnkQ), (z.UnkW, z.UnkY) })
        {
            if (palavra == 0)
                continue;
            string nome = new string(new[] { (char)(palavra & 0xFF), (char)((palavra >> 8) & 0xFF) });
            fora.Add((nome, variante));
        }
        return fora;
    }

    /// <summary>
    /// Uma celula depois do passe das variantes: o que ela ficou a ser, e o nome do `.set` que o
    /// cliente lhe vai pedir.
    /// </summary>
    public readonly record struct CelulaVariante(
        int X, int Y, string Chunk, int Rot, int Ice, int Ob, string SetBase, bool Sorteada = false);

    /// <summary>
    /// Um dos (ate') cinco ficheiros de `.set` que a <c>SetFileTable</c> da zona declara: o
    /// <b>numero</b> que vai para o fim do nome do `.set` e a <b>chance</b> de a celula o pedir.
    ///
    /// <para><b>O numero e' o VALOR do campo, nao o indice dele</b> (FINDINGS 56).  Eu tinha-o
    /// escrito como «`N` corre de 0001 a 0005, os cinco campos», e por isso a ferramenta mostrava
    /// o literal <c>000N</c>; o que o cliente pede e' o valor:</para>
    /// <code>
    ///     SetFileTable.Field1 = 12   ->   sn_0330_i0_00_ob00_0012.set
    /// </code>
    /// <para>Os <c>_0001</c> a <c>_0005</c> que tambem aparecem nos despejos de hook sao a
    /// <b>outra</b> forma do nome, em que o cliente tenta os cinco indices a' sorte; foi essa que
    /// me enganou.  Num mapa fixo a forma e' a da zona — <c>sn_&lt;modelo&gt;_&lt;numero do
    /// `.lat`&gt;_000N.set</c>, com o <c>N</c> a correr mesmo de 1 a 5.</para>
    /// <para>A <b>chance</b> e' a mesma regra do <c>_ob0*</c> (<c>chance &gt; rnd % 100000</c>,
    /// 48.5), so' que aqui as chances <b>nao</b> sao planas: no corpus ha' <c>100000</c> (sempre),
    /// <c>0</c> (nunca) e ainda <c>20000</c>, <c>30000</c>, <c>50000</c> e <c>70000</c>.  Uma
    /// celula pede o campo se o sorteio dela passar, e e' por isso que no despejo do Ruins todas
    /// as celulas pedem o <c>0012</c> e o <c>0013</c> (chance 100000) mas so' 27 de 71 pedem o
    /// <c>0014</c> e 20 de 71 o <c>0015</c> (chance 30000).</para>
    /// <para><see cref="Certo"/> e' verdade quando a chance e' <c>100000</c> em todas as dez
    /// entradas do vector, que e' o caso em 3087 dos 8390 campos do corpus: so' desses e' que o
    /// nome se sabe sem sortear.  Em 105 campos o vector <b>nem sequer e' plano</b> (o `14` do
    /// Seabed e' <c>70000</c> excepto nas entradas 2, 5 e 8, onde e' <c>100000</c>), e e' para
    /// isso que serve o <c>SetChanceIdx</c> do registo da grelha — qual das dez entradas conta
    /// e' o que falta medir.</para>
    /// </summary>
    public readonly record struct SetFile(int Numero, int ChanceMin, int ChanceMax,
                                         int[]? Chances = null)
    {
        /// <summary>A chance na entrada <paramref name="slot"/> do vector de dez.</summary>
        public int ChanceNoSlot(int slot)
            => Chances is null || Chances.Length == 0
                ? ChanceMax
                : Chances[slot >= 0 && slot < Chances.Length ? slot : 0];

        /// <summary>A celula pede sempre este `.set`: a chance e' 100000 em todas as entradas.</summary>
        public bool Certo => ChanceMin >= 100000;
        /// <summary>As dez entradas do vector de chances sao todas iguais.</summary>
        public bool Plana => ChanceMin == ChanceMax;
        /// <summary>A chance em percentagem, para mostrar.</summary>
        public int Percentagem => ChanceMax / 1000;
    }

    /// <summary>
    /// Os ficheiros de `.set` que a zona declara, na ordem dos campos da <c>SetFileTable</c>.
    /// Ficam de fora os campos sem id (<c>&lt;= 0</c>) e os de chance zero, que a celula nunca
    /// pede.  Ver <see cref="SetFile"/> para o que o numero e' e de onde veio.
    /// </summary>
    public static List<SetFile> SetFilesDaZona(ZoneInput z)
    {
        var fora = new List<SetFile>();
        var campos = z.It2?.Fields;
        if (campos is null)
            return fora;
        foreach (var f in campos.Take(5))
        {
            if (f.Id <= 0 || f.Values is null || f.Values.Length == 0)
                continue;
            int lo = f.Values.Min(), hi = f.Values.Max();
            if (hi <= 0)
                continue;
            fora.Add(new SetFile(f.Id, lo, hi, (int[])f.Values.Clone()));
        }
        return fora;
    }

    /// <summary>
    /// O nome do `.set` de um <b>mapa fixo</b>, que nao e' o das celulas: num mapa fixo o cliente
    /// pede a forma da <b>zona</b>,
    /// <code>
    ///     sn_&lt;modelo&gt;_&lt;numero do `.lat`&gt;_000N.set        N = 1..5
    /// </code>
    /// <para>— com o numero do `.lat` no meio e, <b>ai sim</b>, um indice de 1 a 5 no fim.  Nos
    /// treze despejos de hook a forma aparece sempre e sempre com os cinco: `sn_0330_0002_0001`
    /// a `_0005` no Ruins, `sn_0690_0111_*` no Coast, `sn_0150_0000_*` na campship, e os ficheiros
    /// `sn_0330_0002.lat`, `sn_0690_0111.lat` e `sn_0150_0000.lat` existem todos (FINDINGS 56).
    /// A <c>SetFileTable</c> da zona nao entra neste nome.</para>
    /// </summary>
    public static string NomeDeSetDeMapaFixo(ZoneInput z)
        => $"sn_{z.Model:0000}_{z.Arguments:0000}_000N.set";

    /// <summary>
    /// Que ficheiros `.set` uma celula carrega, com a regra do cliente (FINDINGS 56.8).
    ///
    /// <para>Lida de ponta a ponta no <c>0x00792D80</c>, e e' <b>a mesma do `_ob0*`</b>:</para>
    /// <code>
    ///     por cada um dos cinco campos:
    ///         r      = next_u32(mt)
    ///         chance = campos[k].chances[slot]
    ///         v      = (r / d) % 100000            &lt;- o uniform_a de sempre
    ///         out[k] = campos[k].id   se  chance &gt; v,  senao 0
    /// </code>
    /// <para>Gasta <b>cinco palavras por celula</b>, uma por campo, mesmo nos de chance 0 ou
    /// 100000 — o <c>0x00792D80</c> nao salta nenhum.  O <paramref name="slot"/> e' o
    /// <c>SetChanceIdx</c> do registo da grelha, o <c>[rec+0xCC]</c>.</para>
    /// <para><b>O MT nao e' o mt1/mt2/mt3 do gerador:</b> e' um <c>init_genrand(semente)</c>
    /// construido na pilha do <c>0x005E4C90</c> (ctor <c>0x00792CA0</c>).  <b>[measured] E a
    /// semente e' a da propria ZONA</b> — o <c>mt[0]</c> do rolador despejado pela sonda AM e' o
    /// <c>Seed</c> do packet, nas tres zonas com tabela, nas duas corridas.  Ha' <b>um rolador
    /// por zona</b>, nao por celula: o indice do despejo e' <c>624 + 5*(celulas ja' feitas)</c>
    /// em todas elas.</para>
    /// <para>A regra confere com as proporcoes observadas (<b>57 de 57</b> campos nas treze areas
    /// dos despejos de hook) e, melhor do que isso, reproduz <b>182 de 182</b> chamadas da sonda
    /// AM — os cinco numeros exactamente como o cliente os escreveu, nas cinco zonas e nas duas
    /// corridas (<c>tools/setsorteio.py</c>).</para>
    /// </summary>
    public static List<int> SorteiaSets(Mt19937 mt, IReadOnlyList<SetFile> campos, int slot = 0)
    {
        var fora = new List<int>(5);
        for (int k = 0; k < 5; k++)
        {
            int id = k < campos.Count ? campos[k].Numero : 0;
            int ch = k < campos.Count ? campos[k].ChanceNoSlot(slot) : 0;
            int v = Rng.UniformA(mt, 100000);
            fora.Add(ch > v ? id : 0);
        }
        return fora;
    }

    /// <summary>
    /// O sorteio dos `.set` de uma <b>grelha inteira</b>, celula a celula, como o cliente o faz.
    ///
    /// <para>Um rolador por zona, semeado com o <c>Seed</c> da zona, e cinco palavras por celula
    /// na ordem da varredura — a mesma ordem do <see cref="VariantesDaGrelha"/>.  Devolve, por
    /// celula, os numeros que ela pede (os zeros ja' fora).</para>
    /// <para><paramref name="slots"/> e' o <c>SetChanceIdx</c> de cada celula (o
    /// <c>[rec+0xCC]</c>), que a sonda AM viu variar de 0 a 9.  Nao se sabe ainda o que o
    /// escreve, mas so' muda o resultado nos <b>105 de 8390</b> campos cujo vector de chances
    /// nao e' plano; com um vector plano o slot e' indiferente e o sorteio sai certo na mesma.
    /// Sem <paramref name="slots"/> usa-se 0 em todas.</para>
    /// </summary>
    public static List<List<int>> SorteiaSetsDaGrelha(
        IReadOnlyList<CelulaVariante> celulas, IReadOnlyList<SetFile> sets, int seed,
        IReadOnlyList<int>? slots = null)
    {
        var mt = new Mt19937(unchecked((uint)seed));
        var fora = new List<List<int>>(celulas.Count);
        for (int i = 0; i < celulas.Count; i++)
        {
            int slot = slots is not null && i < slots.Count ? slots[i] : 0;
            fora.Add(SorteiaSets(mt, sets, slot).Where(n => n > 0).ToList());
        }
        return fora;
    }

    /// <summary>
    /// Os nomes de `.set` de uma celula: a base dela mais o numero de cada campo declarado.
    /// Devolve o nome e se ele e' <b>certo</b> (chance 100000) ou um sorteio por celula.
    /// </summary>
    public static List<(string Nome, SetFile Campo)> NomesDeSet(
        CelulaVariante c, IReadOnlyList<SetFile> sets)
    {
        var fora = new List<(string, SetFile)>();
        if (c.SetBase.Length == 0)
            return fora;
        foreach (var sf in sets)
            fora.Add(($"{c.SetBase}_{sf.Numero:0000}.set", sf));
        return fora;
    }

    /// <summary>
    /// O que o passe das variantes decidiu, celula a celula, com o nome do `.set` de cada uma.
    ///
    /// <para><b>O nome do `.set` e' o que o estagio 6 entrega ao carregador</b> (FINDINGS 55):
    /// o `0x005EDA70` nao escreve nada na grelha, monta um descritor por celula-ancora e
    /// chama o sumidouro, e o nome que sai dessa entrega e'</para>
    /// <code>
    ///     sn_&lt;modelo&gt;_&lt;chunk&gt;_&lt;ICE com 2 digitos&gt;_ob0&lt;ob&gt;_&lt;numero 4 digitos&gt;.set
    /// </code>
    /// <para>conferido contra <b>10 274 das 10 334</b> linhas dos despejos de hook.  O que sai
    /// daqui e' a <b>base</b> do nome, sem o numero final: o numero e' o <b>valor</b> de um campo
    /// da <c>SetFileTable</c> da zona (12, 13, 15, 31, 111...) e nao um indice de 1 a 5, e as
    /// chances desses campos dizem quais e' que a celula pede.  Ver <see cref="SetFile"/> e
    /// <see cref="NomesDeSet"/>, e a seccao <b>56</b> dos FINDINGS.</para>
    /// <para>Uma celula cujo <c>_ob0*</c> saia negativo <b>nao leva o segmento `_ob0*`</b>
    /// (`sn_0330_z0_00_0001.set`), que e' o ramo <c>n &lt;= 0</c> do <c>0x005EE34E</c>; e um
    /// chunk com nome de mais de dois caracteres mantem o nome todo (o `river0001` do
    /// Seabed).</para>
    /// <para>So' aparecem as celulas que o passe trata: as ocupadas, que sejam a <b>ancora</b> da
    /// peca, e cujo ICE ainda estivesse por atribuir. E' a mesma condicao do
    /// <see cref="PasseVariantes"/>.</para>
    /// </summary>
    public static List<CelulaVariante> VariantesDaGrelha(Grid gr, Area area)
    {
        var outp = new List<CelulaVariante>();
        for (int y = 0; y < gr.H; y++)
        for (int x = 0; x < gr.W; x++)
        {
            int i = y * gr.W + x;
            string? nome = gr.Name[i];
            if (nome is null || !area.Chunks.ContainsKey(nome))
                continue;
            if (((gr.Info[i] >> 8) & 0xFE) != 0)
                continue;                       // nao e' a ancora da peca
            if (gr.Ice[i] == 0xFFFF && gr.Ob[i] == 0xFFFF)
                continue;                       // o passe nao lhe tocou
            int ice = gr.Ice[i], ob = gr.Ob[i];
            string seg = ob == 0xFFFF ? "" : $"_ob0{ob}";
            // A BASE do nome, sem o numero: o numero vem da `SetFileTable` da zona, que este
            // metodo nao ve' (`SetFilesDaZona` + `NomesDeSet`).  Era aqui que estava o `000N`.
            string baseSet = ice == 0xFFFF
                ? ""
                : $"sn_{area.Model:0000}_{nome}_{ice:00}{seg}";
            outp.Add(new CelulaVariante(x, y, nome, gr.Rot[i], ice, ob, baseSet,
                                        gr.IceSorteada?.Contains(i) ?? false));
        }
        return outp;
    }

    /// <summary>
    /// <b>Num MAPA FIXO corre a MESMA regra</b>, e o que muda sao os argumentos (FINDINGS 56).
    /// Nos 46 segmentos do caminho `lat` da corrida AJ+AK as listas e os valores escritos
    /// obedecem a' regra toda — 64 listas de um candidato escreveram esse sem gastar palavra, e
    /// as de dois ou mais sortearam mesmo —, so' que o passe gasta <b>0 palavras em 41 dos 46
    /// segmentos</b> e 19 a 30 nos outros cinco.  E o <c>_ob0*</c> <b>nao se escreve</b> neste
    /// caminho: onde escreviamos <c>0</c> o cliente deixa <c>FFFF</c> em 30 celulas.
    /// <para>Como num mapa fixo nao houve geracao, o <c>mt1</c> esta' no estado em que a
    /// <b>sessao</b> o deixou e nao e' derivavel (a mesma conclusao do enchimento, 55.11).  Com
    /// <c>mt1 = null</c> escreve-se o <b>primeiro candidato</b> nas listas de dois ou mais e
    /// marca-se a celula em <see cref="Grid.IceSorteada"/>, para a tabela poder dize-lo.
    /// Medido nas 28 grelhas de mapa fixo: <b>451 de 471</b> celulas certas, contra 320 sem o
    /// passe e 434 com um <c>mt1</c> novo da semente.</para>
    /// </summary>
    public static List<(int X, int Y, string Nome, int Ice, int Ob)> PasseVariantes(
        Grid gr, Area area, Mt19937? mt1, int obId = 0,
        IReadOnlyList<(string Nome, int Valor)>? grandes = null, bool escreveOb = true)
    {
        var escritas = new List<(int, int, string, int, int)>();
        gr.IceSorteada = new HashSet<int>();
        for (int y = 0; y < gr.H; y++)
        for (int x = 0; x < gr.W; x++)
        {
            int i = y * gr.W + x;
            string? nome = gr.Name[i];
            if (nome is null)
                continue;
            if (!area.Chunks.TryGetValue(nome, out var c))
                continue;
            // A ELEGIBILIDADE, lida no `0x005ECBD0` e medida na corrida AJ+AK:
            //
            //   1. a celula tem de ser a **ancora** da peca.  O `0x005ECD3A..0x005ECD47` le' o
            //      byte 1 do `Info` e salta a celula se ele tiver qualquer bit acima do 0 — ou
            //      seja, passa com `0` ou `1`.  Os chunks de uma celula declaram `0`; os 178 de
            //      varias tem **exactamente uma** celula com `1` e as outras com `3`, `5`, ...,
            //      sem excepcao nos 220 modelos.  Logo um chunk de varias celulas sorteia **uma
            //      vez por instancia**, na ancora, e as outras celulas dele ficam a `FFFF`.
            //   2. o ICE lido com **sinal** tem de ser negativo (`0x005ECD79 movsx` + `test/jl`).
            //      O `FFFF` de origem e' `-1` e passa; uma celula que ja' traga valor (as do
            //      `.lat`) nao passa — 134 celulas nessas condicoes na corrida AJ+AK e
            //      **nenhuma** delas sorteou.
            if (((gr.Info[i] >> 8) & 0xFE) != 0)
                continue;
            if (gr.Ice[i] < 0x8000)
                continue;
            // A LISTA DE CANDIDATOS.  O `0x005ECF32` copia o vector de recursos do chunk (tem
            // `MaxIce` entradas, `0..MaxIce-1`) e o `0x005ED94F` acrescenta-lhe o valor de cada
            // um de ate' 10 pares `(nome, valor)` cujo nome bata.  Nos chunks de varias celulas o
            // vector vem vazio (`MaxIce = 0` no `.lps`) e a lista sai inteira dos pares: sao os
            // sufixos `_8N`, e a contagem deles e' o `Multi80`.
            var lista = new List<int>();
            if (c.Cols * c.Rows != 1)
            {
                // A lista de um chunk de VARIAS celulas sai dos **big chunks da zona** (os 10
                // pares `(nome, valor)` do `0x005ED94F`), e nao do `Multi80` do `.lps`: medido
                // 13/13 contra 12/13 (FINDINGS 55.7).  Sem os campos do packet — uma zona feita
                // a' mao — fica-se pelo `Multi80`.
                if (grandes is not null && grandes.Count > 0)
                {
                    foreach (var (nm, v) in grandes)
                        if (string.Equals(nm, nome, StringComparison.Ordinal))
                            lista.Add(v);
                }
                else
                {
                    for (int k = 0; k < c.Multi80; k++)
                        lista.Add(80 + k);
                }
            }
            else
            {
                for (int k = 0; k < c.MaxIce; k++)
                    lista.Add(k);
            }
            // A REGRA DO `i`/`l` (corrida AJ+AK, 870/870 listas).  Um corredor (`i*`) ou uma
            // curva (`l*`) com tres ou mais candidatos **nao repete a variante de um vizinho do
            // mesmo chunk ligado por porta**: o `0x005ED5E5` compara o primeiro caracter do nome
            // com `'i'` e `'l'`, o `0x005ED5FF` exige `>= 3` candidatos, o `0x005EC5E0` devolve 1
            // so' quando as duas celulas tem porta uma para a outra, o `0x005ED8ED` exige o mesmo
            // nome, e o `0x005ED845` faz o `std::remove` do ICE do vizinho.
            //
            // O `movsx` do `0x005ED746` e' o que torna isto inofensivo nos vizinhos ainda por
            // preencher: `FFFF` com sinal e' `-1`, que nao esta' na lista.
            if ((nome[0] == 'i' || nome[0] == 'l') && lista.Count >= 3)
            {
                int portaI = (int)(gr.Info[i] & 0xF);
                for (int d = 0; d < 4; d++)
                {
                    if (((portaI >> d) & 1) == 0)
                        continue;
                    int nx = x + DX[d], ny = y + DY[d];
                    if (nx < 0 || nx >= gr.W || ny < 0 || ny >= gr.H)
                        continue;
                    int j = ny * gr.W + nx;
                    if (!string.Equals(gr.Name[j], nome, StringComparison.Ordinal))
                        continue;
                    int portaJ = (int)(gr.Info[j] & 0xF);
                    if (((portaJ >> Opposite(d)) & 1) == 0)
                        continue;
                    int v = gr.Ice[j] >= 0x8000 ? gr.Ice[j] - 0x10000 : gr.Ice[j];
                    lista.RemoveAll(q => q == v);
                }
            }
            // O SORTEIO.  Com a lista vazia nao se escreve nada (fica o `FFFF`); com um candidato
            // escreve-se esse e **nao se gasta palavra** (`0x005ED0ED cmp edi,1 / je`); com dois
            // ou mais o escrito e' um **valor** da lista, nao o indice.
            if (lista.Count == 1)
                gr.Ice[i] = lista[0];
            else if (lista.Count > 1 && mt1 is not null)
                gr.Ice[i] = lista[Rng.UniformA(mt1, lista.Count)];
            else if (lista.Count > 1)
            {
                // Sem MT (mapa fixo): escreve-se o primeiro candidato e marca-se a celula,
                // porque ali o cliente sorteou mesmo e o estado do `mt1` nao se sabe.
                gr.Ice[i] = lista[0];
                gr.IceSorteada.Add(i);
            }
            if (escreveOb && c.MaxOb > 1 && gr.Ob[i] == 0xFFFF)
                gr.Ob[i] = obId;
            if (gr.Ice[i] != 0xFFFF || gr.Ob[i] != 0xFFFF)
                escritas.Add((x, y, nome, gr.Ice[i], gr.Ob[i]));
        }
        return escritas;
    }

    public static List<(int X, int Y, string Nome, int Rot)> SegundoPasseZ(
        Grid gr, Area area, int semente)
    {
        var mudadas = new List<(int, int, string, int)>();
        if (!TemZModelados(area))
            return mudadas;

        var mt = new Mt19937(unchecked((uint)semente));
        var marcado = new bool[gr.W * gr.H];
        var g9 = new HashSet<string>(area.ByGroup[9].Select(c => c.Short));
        var pilha = new Stack<(int X, int Y)>();

        for (int y0 = 0; y0 < gr.H; y0++)
        for (int x0 = 0; x0 < gr.W; x0++)
        {
            pilha.Clear();
            pilha.Push((x0, y0));
            while (pilha.Count > 0)
            {
                var (x, y) = pilha.Pop();
                int i = y * gr.W + x;
                if (gr.Name[i] != "z0")
                    continue;
                marcado[i] = true;
                int mascara = 0;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DY[d];
                    if (nx < 0 || nx >= gr.W || ny < 0 || ny >= gr.H)
                        continue;
                    int j = ny * gr.W + nx;
                    if (gr.Height[j] != gr.Height[i])
                        continue;
                    if (gr.Name[j] == "z0")
                    {
                        if (marcado[j])
                            continue;
                        float f = Uniforme01(mt);
                        if (f >= 1.0f)
                            f = 0.0f;
                        if (f < 0.7f)
                        {
                            mascara |= 1 << d;
                            pilha.Push((nx, ny));
                        }
                    }
                    else if (gr.Name[j] is not null && g9.Contains(gr.Name[j]!))
                    {
                        if (((gr.Info[j] >> ((d + 2) & 3)) & 1) != 0)
                            mascara |= 1 << d;
                    }
                }
                var (nome, rot) = PecaZ2(area, mascara);
                if (nome is null)
                    continue;
                gr.Name[i] = nome;
                gr.Rot[i] = rot;
                if (area.Chunks.TryGetValue(nome, out var c))
                    gr.Info[i] = (gr.Info[i] & ~0xFu)
                                 | ChunkMath.RotPaths(c.BaseInfo[0] & 0xF, rot);
                if (nome != "z0")
                    mudadas.Add((x, y, nome, rot));
            }
        }
        return mudadas;
    }

    /// <summary>
    /// Os originais em que a familia oferece mais do que uma candidata — ou seja onde a
    /// ferramenta esta' a escolher e nao a saber.  No `sn_0720` sao tres de dez: o `i0`
    /// (`d1`/`di`), o `l0` (`d2`/`d3`/`dl`) e o `t0` (`d4`/`dt`).  Das tres, o cliente foi
    /// observado em `i0` uma vez e em `t0` duas, sempre no nibble cheio; o `l0` nunca.
    /// </summary>
    public static SortedDictionary<string, List<string>> AmbiguidadesDoReskin(
        Area area, char letra = 'd')
    {
        var fam = MembrosDaFamilia(area, letra);
        var nomes = new HashSet<string>(fam.Select(f => f.Short));
        var outp = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (nome, c) in area.Chunks)
        {
            if (nomes.Contains(nome) || c.Cols * c.Rows != 1)
                continue;
            uint b = c.BaseInfo[0];
            var cands = fam
                .Where(f => f.Cols * f.Rows == 1
                            && (f.BaseInfo[0] & 0x0FFFFFFFu) == (b & 0x0FFFFFFFu)
                            && f.Door.SequenceEqual(c.Door))
                .Select(f => f.Short).OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (cands.Count > 1)
                outp[nome] = cands;
        }
        return outp;
    }

    /// <summary>
    /// A zona para onde a <paramref name="zoneIndex"/> sai e' um mapa <b>fixo</b> (a area de
    /// boss)?  E' o que decide se o nome da peca de saida vem do
    /// <c>paramsB["goal_to_boss"]</c> em vez do <c>paramsD</c> (55.8).  A assinatura de uma zona
    /// de mapa fixo e' o <c>SizeX</c>/<c>SizeY</c> a zero: o mapa dela vem do `.lat`.
    /// </summary>
    public static bool DestinoEhMapaFixo(
        IEnumerable<Warp> warps, IReadOnlyDictionary<int, (int X, int Y)> tamanhoPorZona,
        int zoneIndex)
    {
        foreach (var w in warps)
            if (w.ZoneIndex == zoneIndex && tamanhoPorZona.TryGetValue(w.DstZone, out var t))
                return t.X == 0 && t.Y == 0;
        return false;
    }

    /// <summary>
    /// Os modelos das zonas que estao do outro lado das portas desta: a de onde se vem e a para
    /// onde se vai.  Sao as chaves do <c>paramsC</c>/<c>paramsD</c> (41.17).
    /// </summary>
    public static (int? Origem, int? Destino) ModelosVizinhos(
        IEnumerable<Warp> warps, IReadOnlyDictionary<int, int> modeloPorZona, int zoneIndex)
    {
        int? origem = null, destino = null;
        foreach (var w in warps)
        {
            if (origem is null && w.DstZone == zoneIndex
                && modeloPorZona.TryGetValue(w.ZoneIndex, out int mo))
                origem = mo;
            if (destino is null && w.ZoneIndex == zoneIndex
                && modeloPorZona.TryGetValue(w.DstZone, out int md))
                destino = md;
        }
        return (origem, destino);
    }

    /// <summary>
    /// Clona o `.lat` de rio para uma grelha de trabalho, como o modo rio arranca
    /// (<c>unkB == 2</c>).  A grelha de trabalho e' a do proprio `.lat` — <b>o modo rio nao
    /// duplica nenhum eixo</b> (FINDINGS 15.3).  A grelha do rio e' um clone e herda a caixa do
    /// `.lat`, que e' a extensao inteira dele.
    /// </summary>
    public static Grid? RiverGrid(int model, int arguments)
    {
        string path = GameData.LatPath(model, arguments, river: true);
        if (!File.Exists(path))
            return null;
        var m = LatFile.Load(path);
        var g = new Grid(m.W, m.H);
        for (int i = 0; i < m.Cells.Count; i++)
        {
            var c = m.Cells[i];
            if (c.Short.Length == 0)
                continue;
            g.Write(i % m.W, i / m.W, m.Info[i], c.Short, c.Rot, c.Altura);
        }
        if (RioCaixaLimpa)
        {
            g.MinX = g.W; g.MinY = g.H; g.MaxX = -1; g.MaxY = -1;
        }
        g.BMinX = 0; g.BMinY = 0; g.BMaxX = g.W - 1; g.BMaxY = g.H - 1;
        return g;
    }

    /// <summary>
    /// O mapa FIXO (<c>unkB == 0</c>): o `.lat` <c>sn_&lt;model&gt;_&lt;args:0000&gt;.lat</c> tal e
    /// qual.  Estas zonas nao passam por gerador aleatorio nenhum — a fabrica do
    /// <c>0x005F9880</c> escolhe o gerador de `.lat` (<c>0x005EA780</c>) e o mapa e' o ficheiro.
    /// Sao 1092 das 2224 zonas dos 583 packets.
    /// </summary>
    public static Grid? LatGrid(int model, int arguments)
    {
        string path = GameData.LatPath(model, arguments);
        if (!File.Exists(path))
            return null;
        var m = LatFile.Load(path);
        Area? area = null;
        try { area = GameData.GetArea(model); } catch { }
        var g = new Grid(m.W, m.H);
        for (int i = 0; i < m.Cells.Count; i++)
        {
            var c = m.Cells[i];
            if (c.Short.Length == 0)
                continue;
            uint info = m.Info[i];
            // Um marcador de entrada guardado no `.lat` leva `info = 0`, ou seja porta nenhuma,
            // e por isso a pre-visualizacao nao lhe desenhava ligacao.  E' a MESMA convencao do
            // cliente: nos 32 marcadores que o pos-processamento escreveu nas 13 zonas da sonda
            // R, os 32 ficaram com `info = 0` (e `flags` 2 na entrada, 1 na saida, 0 nos
            // neutros).  Como a nossa grelha final ja' traz os marcadores com a mascara do
            // catalogo no caminho ALEATORIO, deixa-los a zero aqui era o unico sitio onde os
            // dois caminhos discordavam.  Acontece em **13 celulas em 3765 ficheiros**, todas
            // `ds` do modelo 720 (41.19).
            if (info == 0 && area is not null
                && area.Chunks.TryGetValue(c.Short, out var ch)
                && ch.Cols * ch.Rows == 1 && (ch.BaseInfo[0] & 0xF) != 0)
                info = ChunkMath.RotInfo(ch.BaseInfo[0], c.Rot);
            // A altura vem do `+0x08` do registo (41.8); era deitada fora e ia sempre 0.
            g.Write(i % m.W, i / m.W, info, c.Short, c.Rot, c.Altura);
            // O `.lat` **declara** o ICE (`+0x0A`) e o `_ob0*` (`+0x0C`) de cada celula, e e'
            // de la' que eles vem num mapa fixo: o passe do ICE corre tambem neste caminho mas
            // salta as celulas que ja' trazem valor (o `movsx`/`jl` do `0x005ECD79`).  Sem isto
            // a ferramenta mostrava um mapa fixo sem variante nenhuma, quando o cliente tem-nas
            // todas (55.9).
            int k = (i / m.W) * g.W + (i % m.W);
            if (c.Ice is int vice)
                g.Ice[k] = vice;
            int vob = c.D0C & 0xFFFF;          // o `_ob0*` e' o word baixo do dword `+0x0C`
            if (vob != 0xFFFF)
                g.Ob[k] = vob;
        }
        // A grelha que o cliente entrega leva a MOLDURA: o recorte do `0x005EAF50`, igual ao
        // caminho gerado, com a caixa a ser o **ficheiro inteiro** e nao a do conteudo — a mesma
        // convencao do modo rio.  Medido nas 28 grelhas de mapa fixo despejadas: **28 de 28**
        // dimensoes e **471 de 471** nomes com o enchimento e o segundo passe por cima; sem ela
        // nao batia nem uma dimensao (55.10).
        g.BMinX = 0;
        g.BMinY = 0;
        g.BMaxX = g.W - 1;
        g.BMaxY = g.H - 1;
        return FinalGrid(g);
    }

    /// <summary>
    /// <c>0x005EAF50</c>: a caixa <b>da grelha</b> (<c>grid-&gt;0x18..0x24</c>, que inclui o
    /// esqueleto do `.lat`) mais uma moldura de uma celula.  As pecas de entrada e de saida
    /// vivem nessa moldura porque nunca chegaram a ser escritas na grelha de trabalho.
    /// </summary>
    public static Grid? FinalGrid(Grid g, IEnumerable<Placed>? doors = null)
    {
        if (g.BMaxX < g.BMinX)
            return null;
        var outg = new Grid((g.BMaxX - g.BMinX) + 3, (g.BMaxY - g.BMinY) + 3);
        for (int y = g.BMinY; y <= g.BMaxY; y++)
            for (int x = g.BMinX; x <= g.BMaxX; x++)
            {
                int i = g.Idx(x, y);
                if (g.Name[i] is null)
                    continue;
                outg.Write(x - g.BMinX + 1, y - g.BMinY + 1, g.Info[i], g.Name[i], g.Rot[i],
                           g.Height[i]);
                // O recorte tem de levar o ICE e o `_ob0*` ATRAS.  No caminho gerado isto nao se
                // nota — o recorte corre antes do passe das variantes e esta' tudo a `FFFF` —,
                // mas num mapa fixo os ids vem declarados no `.lat` (55.9) e o recorte da moldura
                // (55.11) estava a deita-los fora: a tabela das variantes aparecia vazia e os
                // despejos do cliente mostram-nos la' (`ice = 0` e `80`, `_ob0 = 0` e `1`).
                int o = outg.Idx(x - g.BMinX + 1, y - g.BMinY + 1);
                outg.Ice[o] = g.Ice[i];
                outg.Ob[o] = g.Ob[i];
            }
        foreach (var d in doors ?? Enumerable.Empty<Placed>())
        {
            int nx = d.X - g.BMinX + 1, ny = d.Y - g.BMinY + 1;
            // so' na moldura: a peca de entrada/saida nunca foi escrita na grelha de trabalho,
            // por isso nunca pode tapar uma celula do mapa
            if (outg.Inside(nx, ny) && outg.Name[outg.Idx(nx, ny)] is null && d.Ch is not null)
                outg.Write(nx, ny, d.Ch.Rots[d.Rot].Info[0], d.Ch.Short, d.Rot, d.H);
        }
        return outg;
    }

    /// <summary>
    /// O que o pos-processamento desta zona leva, e o que fica de fora.
    /// </summary>
    /// <remarks>
    /// <para>A cadeia do cliente (<c>0x00646E40</c>) tem seis etapas.  <b>Estao todas
    /// explicadas</b> desde 2026-10-11: as cinco primeiras estao modeladas e a sexta nao mexe na
    /// grelha (FINDINGS 55).  Dizer o que corre <b>nesta zona</b> continua a valer, porque isso
    /// depende do modelo (o segundo passe dos <c>z*</c> so' corre com o grupo 9) e de o mapa ser
    /// gerado ou fixo (FINDINGS 47.5).</para>
    /// <para>A ultima linha comeca por <c>summary:</c> e e' a que diz se ha algum buraco.  Uma
    /// linha <c>CAREFUL</c> fica reservada para quando voltar a haver um.</para>
    /// </remarks>
    /// <param name="area">O catalogo do modelo.</param>
    /// <param name="mapaFixo">Verdadeiro para uma zona de <c>.lat</c> (mapa fixo).</param>
    public static List<string> EstadoDoPosProcessamento(Area area, bool mapaFixo)
    {
        var l = new List<string>();
        bool g9 = TemZModelados(area);
        bool temD = MembrosDaFamilia(area).Count > 0;

        l.Add(temD
            ? "d* reskin (0x00633D30): modelled - use --reskin"
            : "d* reskin (0x00633D30): this model has no d* family, nothing to do");
        l.Add("z0 fill (0x005E9A30): modelled - use --fill");
        l.Add(g9
            ? "second z pass (0x005E8030): modelled, and it RUNS here "
              + "(this model has the group 9 set) - included in --fill"
            : "second z pass (0x005E8030): does not run here (this model has no group 9 set), "
              + "so there is nothing to model");
        l.Add("_ob0* variants (0x005EC740) and ICE variants (0x005ECBD0): modelled - "
              + "use --variants");
        l.Add("set-file hand-off (0x005EDA70): nothing to model - it writes nothing into the "
              + "grid, it only hands the loader a descriptor built from what the stages above "
              + "already decided (FINDINGS 55). Measured over three runs of one packet, the "
              + "third after restarting the client: on the GENERATED path it spends 0 mt2 words "
              + "and hands over an identical descriptor every time, so the hand-off is a function "
              + "of the packet (FINDINGS 56).");
        if (mapaFixo)
            l.Add("  and on the FIXED-MAP path only: stage 6 draws an _ob0* of its own for the "
                  + "cells the .lat does not declare one for (the fill's zi/zl/zn), out of mt2 - "
                  + "which is NOT seeded from the zone seed. Three runs of the same packet gave 4 "
                  + "and 6 different cells out of 25, so that one is not reproducible. It is a "
                  + "local, though: the grid keeps FFFF and only the loader sees it (FINDINGS 56).");
        if (mapaFixo && g9)
            l.Add("fixed (.lat) map on a group-9 model: the client reaches the second z pass by "
                  + "the other call site (0x005EADF2) and that path IS measured - 96/96 cells, "
                  + "42/42 of them with the model the packet names, and the mt3 seed is the "
                  + "zone's own seed (FINDINGS 47.7, 47.9, 55.6). The same model applies here.");
        else if (mapaFixo)
            l.Add("fixed (.lat) map: the client's second z pass (0x005EADF2) would not fire here "
                  + "either, because this model has no group 9 set.");
        l.Add("summary: all six stages of 0x00646E40 are accounted for - the five that change "
              + "the grid are modelled, and the sixth does not change it. Nothing in the "
              + "post-processing is an unmodelled hole (FINDINGS 55).");
        return l;
    }
}
