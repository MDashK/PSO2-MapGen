namespace Pso2MapGen.Core;

/// <summary>
/// Uma variante de configuracao a experimentar para uma tentativa medida: o `block.json`
/// emparelhado, ou uma alternativa identificada pelo despejo (o `.lat` do rio, a composicao do
/// grupo 11).  Os nomes do grupo 11 ja' vem resolvidos.
/// </summary>
public sealed class ReplayVariante
{
    public int Model { get; set; }
    public int UnkB { get; set; }
    public int Args { get; set; }
    public int Unk4 { get; set; }
    public List<string> Bigs { get; set; } = new();
}

/// <summary>Uma tentativa medida no cliente, com os campos que a sonda reportou.</summary>
public sealed class ReplayRun
{
    public string Nome { get; set; } = "";
    public int Seed { get; set; }
    public int Sx { get; set; }
    public int Sy { get; set; }
    public int NA { get; set; } = 1;
    public int P94 { get; set; }
    public int P98 { get; set; }
    /// <summary>O indice do MT a' entrada do <c>0x00641420</c>: uma tentativa nova nao ressemeia.</summary>
    public int I1420 { get; set; }
    public int? P90 { get; set; }
    public int A0 { get; set; }

    /// <summary>O indice do MT no fim da tentativa, medido no cliente (a linha `FIM`).</summary>
    public int? IFim { get; set; }

    // a linha `PECA ret=6465E1`: o marcador de entrada que o cliente escreveu
    public string InicioNome { get; set; } = "";
    public int Ex { get; set; }
    public int Ey { get; set; }
    public int H0 { get; set; }
    public int Side { get; set; }

    /// <summary>A grelha final que o cliente despejou, `nome,rotacao` por celula.</summary>
    public int W { get; set; }
    public int H { get; set; }
    public List<string> Cliente { get; set; } = new();

    public List<ReplayVariante> Variantes { get; set; } = new();
}

/// <summary>
/// Reproduz uma tentativa <b>medida no cliente</b>, com os parametros que a sonda reportou em
/// vez dos que se derivariam do packet.  Porto de <c>valida.replay_gen</c> e
/// <c>valida.grelha_final</c>, que e' o que os conferidores da FINDINGS 39.8 usam.
/// </summary>
public static class Replay
{
    /// <summary>
    /// A grelha de trabalho duplica o eixo <b>perpendicular</b> ao lado de entrada, menos 1.
    /// Medido no <c>FIM ... trab=WxH</c> das 21 capturas sem rio, todas.  Nas de rio a grelha e'
    /// a do proprio `.lat`, sem duplicar eixo nenhum.
    /// </summary>
    public static (int W, int H) GrelhaTrabalho(ReplayRun r)
        => (r.Side == 0 || r.Side == 2) ? (r.Sx * 2 - 1, r.Sy) : (r.Sx, r.Sy * 2 - 1);

    /// <summary>O corpo do replay: o ciclo do labirinto, ate' antes das portas.</summary>
    public static Gen? ReplayGen(ReplayVariante b, ReplayRun r)
    {
        Area a;
        try { a = GameData.GetArea(b.Model); }
        catch { return null; }
        // Sem o bloco de init de 0x00647BB0 (que e' o mesmo que deixa gen->0x7C a zero) o
        // grupo 11 fica vazio: os big chunks sao registados la'.
        a.SetBigChunks(b.Bigs);

        bool rio = b.UnkB == 2;
        var g = new Gen(a, r.Seed, r.Sx, r.Sy, rio)
        {
            P90 = r.P90 ?? (b.Unk4 == 1 ? 5 : (b.Unk4 == 2 ? 2 : -1)),
            P94 = r.P94,
            P7c = r.P98 == -1 ? 0 : 1,
        };
        if (rio)
        {
            g.G = MapGen.RiverGrid(b.Model, b.Args);
            if (g.G is null)
                return null;
            g.MarcarFronteiraDoRio();      // 0x00643490 -> gen->0x28
        }
        else
        {
            var (w, h) = GrelhaTrabalho(r);
            g.G = new Grid(w, h);
        }

        // Uma nova tentativa NAO ressemeia: o `idx` da linha TENTATIVA vem > 624 e o MT
        // continua de onde a tentativa anterior o deixou (FINDINGS 21.4).
        for (int i = 624; i < r.I1420; i++)
            g.Mt1.NextU32();

        // 0x00641420: o **lado** e' dado do packet, nao do MT, e por isso e' o unico campo
        // que ainda vem do log.
        var lados = Enumerable.Range(0, 4).Where(d => d != 0).Take(r.NA).ToList();
        if (!lados.Contains(r.Side))
            lados = lados.Take(Math.Max(0, r.NA - 1)).Append(r.Side).Distinct()
                         .OrderBy(v => v).ToList();
        var tgt = g.StartTarget(lados.Select(d => (d, (string?)r.InicioNome)).ToList());
        if (tgt is not StartTarget st)
            return null;
        int back = MapGen.Opposite(st.Side);
        int sx0 = st.X + MapGen.DX[back], sy0 = st.Y + MapGen.DY[back];
        if (!g.G!.Inside(sx0, sy0))
            return null;
        g.MarkEntrance(st.X, st.Y, st.H, st.Name, st.Side);   // 0x006465E1 -> gen->0x28

        var alvo = new Target(sx0, sy0, st.H, st.Side);
        if (g.ArranqueNoRio(alvo) is null)
        {
            var rec = g.ChoosePiece(alvo);
            if (rec is null)
                return null;
            g.Place(rec);
            g.BumpBranch(rec);
        }
        g.Grow();
        return g;
    }

    /// <summary>
    /// A grelha FINAL do modelo, para comparar com o despejo da sonda B.  Corre tambem as duas
    /// chamadas do <c>0x00643820</c>, montando o mapA pela aproximacao antiga (os lados que nao
    /// sao o da saida).  Com <c>gen-&gt;0xA0</c> posto ha um segundo lado no mapB, sorteado de
    /// entre os que sobraram — o sorteio esta' dentro das palavras que o log ja' conta, por isso
    /// aqui nao se sorteia: o lado vem identificado do despejo, como o da saida.
    /// </summary>
    public static (Grid? Grelha, int Idx) GrelhaFinal(ReplayVariante b, ReplayRun r,
                                                     int saidaLado, int? extraA0)
    {
        var g = ReplayGen(b, r);
        if (g is null)
            return (null, 0);
        int entrada = r.Side;
        var mapaA = Enumerable.Range(0, 4).Where(d => d != saidaLado).Take(r.NA).ToList();
        if (!mapaA.Contains(entrada))
            mapaA = mapaA.Take(Math.Max(0, r.NA - 1)).Append(entrada).Distinct()
                         .OrderBy(v => v).ToList();
        var resto = mapaA.Where(d => d != entrada).ToList();
        string nomeEnt = MapGen.NomeDaEntrada(g.AreaRef) ?? "n0";
        string nomeSai = MapGen.NomeDaSaida(g.AreaRef) ?? nomeEnt;
        if (!g.PlaceDoors(resto.Select(d => (d, nomeEnt)), 0))
            return (null, 0);
        g.RenumeraDoArranque();            // 0x006468E4, entre as duas chamadas
        int p98 = Math.Max(0, r.P98);
        var saidasB = new List<(int, string)> { (saidaLado, nomeSai) };
        if (extraA0 is int ex && ex != saidaLado)
            saidasB.Add((ex, "n0"));
        if (!g.PlaceDoors(saidasB, p98))
            return (null, 0);
        // 624 e' o indice com que o MT arranca; e' assim que a sonda o conta.
        return (MapGen.FinalGrid(g.G!), 624 + (int)g.Mt1.Draws);
    }

    /// <summary>
    /// Corre uma tentativa medida contra o despejo do cliente e devolve quantas celulas diferem
    /// na melhor das combinacoes.  O <b>lado da saida</b> nao se sabe calcular a partir do log
    /// (e' dado do packet), por isso experimentam-se os quatro e fica o que der menos diferencas
    /// — e' identificacao a partir do despejo, como a do `.lat` do rio, e nao ajuste: so' um
    /// deles da' zero.
    /// </summary>
    public static (int Dif, int Saida, bool CustoOk, List<string> Detalhe) Confere(ReplayRun r)
    {
        int melhorDif = int.MaxValue, melhorSaida = -1, melhorIdx = -1;
        var melhorDetalhe = new List<string>();
        var extras = new List<int?> { null };
        if (r.A0 != 0)
            extras.AddRange(Enumerable.Range(0, 4).Select(v => (int?)v));
        foreach (var b in r.Variantes)
            for (int saida = 0; saida < 4; saida++)
            {
                if (saida == r.Side)
                    continue;
                foreach (var ex in extras)
                {
                    if (ex is int e && (e == saida || e == r.Side))
                        continue;
                    Grid? meu;
                    int idx;
                    try { (meu, idx) = GrelhaFinal(b, r, saida, ex); }
                    catch { continue; }
                    if (meu is null || meu.W != r.W || meu.H != r.H)
                        continue;
                    var dif = new List<string>();
                    for (int i = 0; i < r.W * r.H; i++)
                    {
                        string mn = meu.Name[i] is null ? "." : meu.Name[i]!;
                        int mr = meu.Name[i] is null ? 0 : meu.Rot[i];
                        if ($"{mn},{mr}" != r.Cliente[i])
                            dif.Add($"({i % r.W},{i / r.W}) modelo={mn}{mr} cliente={r.Cliente[i]}");
                    }
                    if (dif.Count < melhorDif)
                    {
                        melhorDif = dif.Count;
                        melhorSaida = saida;
                        melhorIdx = idx;
                        melhorDetalhe = dif;
                    }
                }
            }
        return (melhorDif == int.MaxValue ? -1 : melhorDif, melhorSaida,
                r.IFim is int f && melhorIdx == f, melhorDetalhe);
    }
}
