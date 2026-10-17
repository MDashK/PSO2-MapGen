using Pso2MapGen.Core;

/// <summary>
/// Os verbos de geracao: o `gera`, que despeja tudo o que interessa comparar contra o Python,
/// e o `varre`, que corre uma arvore inteira de packets.
/// </summary>
internal static class VerbosGera
{
    /// <summary>
    ///     gen --packet &lt;bin&gt; --zone N
    ///     gen --model N --seed N --sx N --sy N [--unkb N --args N ...]
    /// </summary>
    public static int Gera(string[] args)
    {
        var z = LeArgumentos(args, out string cabecalho);
        if (z is null)
            return 1;
        var p = Previsao.Gera(z);
        if (cabecalho.Length > 0)
            Console.WriteLine(cabecalho);
        Console.WriteLine($"note {p.Nota}");
        // o aviso vai para o **stderr**: o stdout e' o resultado da geracao, e e' isso
        // que os conferidores comparam
        if (p.Aviso is not null)
            Console.Error.WriteLine("warning: " + p.Aviso);
        Console.WriteLine($"pieces {(p.G is null ? 0 : p.G.Trace.Count + 1)}");
        Console.WriteLine($"words {(p.G is null ? 0 : p.G.Mt1.Draws)}");
        if (p.G is not null)
            for (int i = 0; i < p.G.Placed.Count; i++)
            {
                var r = p.G.Placed[i];
                Console.WriteLine($"piece {i} {r.Ch?.Short ?? "-"} {r.X} {r.Y} {r.Rot} {r.H}");
            }
        if (p.Grelha is null)
        {
            Console.WriteLine("grid -");
            return 1;
        }
        var gr = p.Grelha;
        // O pos-processamento corre sobre uma COPIA e por ordem do cliente — primeiro o reskin
        // do `ExtraPassFlag`, depois a varredura dos `z0` (41.17, 41.18).  Sem estas opcoes a
        // saida e' a de sempre, que e' o que os conferidores comparam com os despejos da
        // sonda B (tirados em `0x00648829`, antes do pos-processamento).
        // O `--variants` implica o `--fill`: as variantes sao atribuidas depois do enchimento e
        // do segundo passe, e gastam palavras do mesmo `mt1` — por isso tem de vir no fim.
        bool variantes = args.Contains("--variants");
        if (args.Contains("--reskin") || args.Contains("--fill") || variantes)
        {
            gr = gr.Clone();
            var area = GameData.GetArea(z.Model);
            if (args.Contains("--reskin"))
                MapGen.AplicaReskin(gr, area);
            if (args.Contains("--fill") || variantes)
            {
                // O MESMO `mt1` serve os passes seguidos, porque o fluxo e' continuo: o
                // enchimento deixa-o adiantado e as variantes continuam dali (47.10).
                var mt1 = MtDepoisDaGeracao(z, p);
                MapGen.EnchimentoZ0(gr, area, mt1);
                // e o segundo passe, que reesculpe parte do enchimento (46)
                MapGen.SegundoPasseZ(gr, area, z.Seed);
                if (variantes && mt1 is not null)
                    MapGen.PasseVariantes(gr, area, mt1, MapGen.ObIdDaZona(z),
                                          MapGen.GrandesDaZona(z));
            }
        }
        Console.WriteLine($"grid {gr.W} {gr.H}");
        for (int y = 0; y < gr.H; y++)
            for (int x = 0; x < gr.W; x++)
            {
                int i = gr.Idx(x, y);
                if (gr.Name[i] is null)
                    continue;
                // Sem o `--variants` a linha e' **exactamente** a de sempre: os conferidores
                // comparam-na com os despejos da sonda B e nao devem ver uma coluna nova.
                Console.WriteLine($"cell {x} {y} {gr.Name[i]} {gr.Rot[i]} {gr.Height[i]} " +
                                  $"{gr.Info[i]:x8}"
                                  + (variantes ? $" {gr.Ice[i]:x4} {gr.Ob[i]:x4}" : ""));
            }
        return 0;
    }


    /// <summary>
    /// `sets --packet &lt;bin&gt; --zone N --seed S [--slot K] [--cells N]`
    ///
    /// <para>Com a <b>semente</b> do rolador, a ferramenta faz o que o cliente faz: sorteia, por
    /// celula, quais dos cinco campos da <c>SetFileTable</c> passam a chance, e escreve os nomes
    /// de `.set` que essa celula vai pedir (FINDINGS 56.8).  Sem a semente nao ha' sorteio
    /// nenhum, e por isso a tabela do `show --variants` mostra o declarado e a chance.</para>
    /// <para>A semente e' o que a sonda AM mede.  Este verbo existe para dois fins: conferir a
    /// regra contra o Python (`csharp/confere_sorteio.py`) e, quando a semente estiver medida,
    /// ser o sitio por onde ela entra.</para>
    /// </summary>
    public static int Sets(string[] args)
    {
        string? caminho = Str(args, "--packet");
        int zona = Opt(args, "--zone", 0);
        int slot = Opt(args, "--slot", 0);
        int quantas = Opt(args, "--cells", 10);
        string? sem = Str(args, "--seed");
        if (caminho is null || sem is null || !long.TryParse(sem, out long semente))
        {
            Console.Error.WriteLine(
                "usage: sets --packet <bin> --zone N --seed S [--slot K] [--cells N]");
            return 2;
        }

        var pk = Packet0324.Load(caminho);
        var z = pk.Zonas().FirstOrDefault(x => x.ZoneIndex == zona);
        if (z is null)
        {
            Console.Error.WriteLine($"the packet has no zone {zona}");
            return 2;
        }
        var sets = MapGen.SetFilesDaZona(z);
        Console.WriteLine($"packet {Path.GetFileName(caminho)} zone {zona}: "
            + (sets.Count == 0 ? "this zone declares no set file"
                               : DescreveSetFiles(sets)));
        Console.WriteLine($"roller seed {semente}, chance slot {slot} "
            + "(the SetChanceIdx, [rec+0xCC])");
        var mt = new Mt19937(unchecked((uint)semente));
        for (int i = 0; i < quantas; i++)
        {
            var nums = MapGen.SorteiaSets(mt, sets, slot);
            Console.WriteLine($"  cell {i,-3} " + string.Join(" ",
                nums.Select(n => n > 0 ? $"{n:0000}" : "----")));
        }
        Console.WriteLine($"words spent: {5 * quantas} ({quantas} cells x 5 fields)");
        return 0;
    }

    /// <summary>
    /// A tabela do que as variantes deram: uma linha por celula, com o nome do `.set` que o
    /// estagio 6 entrega ao carregador (55) — e com o <b>numero</b> a serio, que e' o valor de um
    /// campo da <c>SetFileTable</c> da zona e nao um indice de 1 a 5 (56).
    /// </summary>
    private static void EscreveTabelaDeVariantes(Grid grelha, Area area, ZoneInput z,
                                                bool mapaFixo = false)
    {
        // Num mapa fixo o nome e' o da zona (`NomeDeSetDeMapaFixo`) e a `SetFileTable` nao entra
        // nele, por isso nao se mostra uma lista de numeros que ali nao e' usada.
        var sets = mapaFixo ? new List<MapGen.SetFile>() : MapGen.SetFilesDaZona(z);
        if (!mapaFixo)
        {
            Console.WriteLine("  " + DescreveSetFiles(sets));
            Console.WriteLine(ExplicaSetFiles());
        }
        var celulas = MapGen.VariantesDaGrelha(grelha, area);
        // O sorteio dos `.set`: um rolador por zona, semeado com o Seed dela (FINDINGS 56.9).
        var sorteadas = mapaFixo || sets.Count == 0
            ? null
            : MapGen.SorteiaSetsDaGrelha(celulas, sets, z.Seed);
        Console.WriteLine(mapaFixo
            ? "   cell  chunk  rot   ICE  _ob0   (the .set name on this path is the zone form above)"
            : "   cell  chunk  rot   ICE  _ob0   the .set files this cell loads");
        for (int k = 0; k < celulas.Count; k++)
        {
            var c = celulas[k];
            // O `?` diz que o ICE daquela celula saiu de um sorteio que nao se reproduz
            // (so' acontece no mapa fixo): o que esta' ali e' o primeiro candidato.
            string sIce = c.Ice == 0xFFFF ? "  -" : $"{c.Ice:00}" + (c.Sorteada ? "?" : "");
            string sOb = c.Ob == 0xFFFF ? "  -" : $"{c.Ob:00}";
            Console.WriteLine($"   {Previsao.Coluna(c.X)}{c.Y + 1,-4} {c.Chunk,-6} "
                + $"{c.Rot,-4} {sIce,4} {sOb,5}"
                + (mapaFixo ? "" : "   " + NomesDeUmaCelula(c, sets, sorteadas?[k])));
        }
    }

    /// <summary>
    /// Os nomes de `.set` de uma celula numa linha so'. **Nao e' uma escolha entre eles:** a
    /// celula carrega todos os campos cuja chance passe, e por isso isto e' uma LISTA.  Os que
    /// dependem do sorteio vao entre <c>[</c> e <c>]</c> com a percentagem (FINDINGS 56).
    /// </summary>
    internal static string NomesDeUmaCelula(MapGen.CelulaVariante c,
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

    /// <summary>A linha de cabecalho que diz o que a zona declara e como se le' a tabela.</summary>
    internal static string DescreveSetFiles(IReadOnlyList<MapGen.SetFile> sets)
    {
        if (sets.Count == 0)
            return "this zone declares no set file in its SetFileTable.";
        var partes = sets.Select(sf => sf.Certo
            ? $"{sf.Numero:0000} always"
            : $"{sf.Numero:0000} in {sf.Percentagem}% of cells"
              + (sf.Plana ? "" : $" (chance varies by slot: {sf.ChanceMin / 1000}..{sf.Percentagem}%)"));
        return "SetFileTable of this zone: " + string.Join(", ", partes);
    }

    /// <summary>
    /// O paragrafo que explica a tabela.  Fica a' parte do <see cref="DescreveSetFiles"/> porque
    /// esse e' uma linha por zona e serve tambem ao verbo `sets`, que nao quer a explicacao.
    /// </summary>
    internal static string ExplicaSetFiles()
        => "  the number at the end of a .set name IS the VALUE of a SetFileTable field, not an"
         + "\n  index 1..5, and a cell LOADS every field whose chance passes - it does not pick"
         + "\n  one. The draw is one Twister per ZONE, seeded with the zone's own Seed, five"
         + "\n  words per cell in sweep order, and the tool reproduces it: 182 of 182 calls the"
         + "\n  probe caught, the five numbers exactly as the client wrote them (FINDINGS 56.9)."
         + "\n  The map does not depend on any of this: stage 6 stores nothing in the grid.";

    /// <summary>
    /// Um MT no estado em que a geracao o deixou, para o enchimento continuar o fluxo (41.20).
    /// E' um MT NOVO avancado ate' ao mesmo ponto, e nao o do gerador, para nao mexer no
    /// estado dele.
    /// </summary>
    private static Mt19937? MtDepoisDaGeracao(ZoneInput z, Previsto p)
    {
        if (p.G is null)
            return null;
        var mt = new Mt19937(unchecked((uint)z.Seed));
        for (long i = 0; i < (long)p.G.Mt1.Draws; i++)
            mt.NextU32();
        return mt;
    }

    /// <summary>O desenho que a ferramenta entrega: nomes internos e rotacao, como no `.lps`.</summary>
    public static int Mostra(string[] args)
    {
        var z = LeArgumentos(args, out string cabecalho);
        if (z is null)
            return 1;
        var p = Previsao.Gera(z);
        if (cabecalho.Length > 0)
            Console.WriteLine(cabecalho);
        if (!p.Ok)
        {
            Console.WriteLine("no map: " + p.Nota);
            return 1;
        }
        Console.WriteLine($"model {z.Model}  seed {unchecked((uint)z.Seed)}  {z.SizeX}x{z.SizeY}  " +
                          $"UnkB={z.UnkB}  {p.Nota}");
        Console.WriteLine(p.G is null
            ? $"grid {p.Grelha!.W}x{p.Grelha.H}"
            : $"{p.Pecas} pieces, final grid {p.Grelha!.W}x{p.Grelha.H}");
        // `--reskin` corre o reskin `d*` do pos-processamento numa copia: o mapa que a `Gera`
        // devolve e' o que os conferidores medem, e o passe corre depois disso (41.18).
        var grelha = p.Grelha!;
        if (args.Contains("--reskin") || args.Contains("--fill"))
            grelha = p.Grelha!.Clone();
        if (args.Contains("--reskin"))
        {
            var area = GameData.GetArea(z.Model);
            if (MapGen.MembrosDaFamilia(area).Count == 0)
                Console.WriteLine($"model {z.Model} has no d* family: nothing to reskin");
            else
            {
                // O cliente so' faz isto quando o bit 0 do `MapGenTable.ExtraPassFlag` esta'
                // posto; a flag da linha de comandos forca, e aqui diz-se quando forcou.
                if ((z.Unk5 & 1) == 0)
                    Console.WriteLine("note: ExtraPassFlag is NOT set on this zone - "
                                      + "--reskin is forcing it");
                var trocas = MapGen.AplicaReskin(grelha, area);
                var ambi = MapGen.AmbiguidadesDoReskin(area);
                Console.WriteLine($"d* reskin: {trocas.Count} cell(s) renamed");
                foreach (var t in trocas)
                    Console.WriteLine($"  ({t.X},{t.Y}) {t.De} -> {t.Para}"
                        + (ambi.TryGetValue(t.De, out var cs)
                           ? $"   [picked among {string.Join("/", cs)}]" : ""));
            }
        }
        // O enchimento vem DEPOIS do reskin: e' a ordem do cliente (41.17).  O `--variants`
        // implica-o, porque as variantes correm depois dele e no mesmo fluxo.
        if (args.Contains("--fill") || args.Contains("--variants"))
        {
            var area = GameData.GetArea(z.Model);
            // Um `mt1` so' para os tres passes: o enchimento adianta-o e as variantes continuam
            // dali.  Criar um segundo a' frente das variantes punha-as no sitio errado do fluxo.
            var mt1 = MtDepoisDaGeracao(z, p);
            var cheias = MapGen.EnchimentoZ0(grelha, area, mt1);
            Console.WriteLine($"z0 fill: {cheias.Count} cell(s)");
            if (MapGen.TemZModelados(area))
            {
                var mudadas = MapGen.SegundoPasseZ(grelha, area, z.Seed);
                Console.WriteLine($"  second z pass (0x005E8030): {mudadas.Count} of them "
                    + $"became false corridors (model {z.Model} has the group 9 set)");
            }
            if (args.Contains("--variants"))
            {
                if (mt1 is null)
                {
                    // Num mapa fixo o passe do ICE corre igual no cliente, mas salta todas as
                    // celulas porque o `.lat` ja' lhes declarou o ICE e o `_ob0*` — e' a guarda
                    // do `0x005ECD79`.  Os ids existem: vem do ficheiro (55.9).
                    MapGen.PasseVariantes(grelha, area, null, 0,
                                          MapGen.GrandesDaZona(z), escreveOb: false);
                    Console.WriteLine("variants: fixed map - the ICE / _ob0* pass runs here TOO, with the same rule: the ids the .lat declares stay (the pass skips a cell that already has one), the rest get theirs - but a fixed map never ran the generator, so mt1 is session state. Cells whose candidate list had 2+ entries are marked (?): the first candidate is shown. No _ob0* is written on this path. 451 of 471 cells match the dumps (FINDINGS 56).");
                    Console.WriteLine("  and on this path the .set name is the ZONE form, not the "
                        + "cell form: " + MapGen.NomeDeSetDeMapaFixo(z) + "  with N = 1..5 (the "
                        + ".lat number in the middle; here the trailing number really is an index, "
                        + "and the SetFileTable does not enter it) - FINDINGS 56.");
                    EscreveTabelaDeVariantes(grelha, area, z, mapaFixo: true);
                }
                else
                {
                    var escritas = MapGen.PasseVariantes(grelha, area, mt1,
                                                         MapGen.ObIdDaZona(z),
                                                         MapGen.GrandesDaZona(z));
                    int nice = escritas.Count(e => e.Ice != 0xFFFF);
                    int nob = escritas.Count(e => e.Ob != 0xFFFF);
                    Console.WriteLine("variants (ICE 0x005ECBD0, _ob0 0x005EC740): "
                        + $"{nice} cell(s) got an ICE id, {nob} got an _ob0 id");
                    Console.WriteLine("  ICE is a value of a candidate list: the chunk's "
                        + "0..maxIce-1, minus the variant of any same-chunk neighbour an i*/l* "
                        + "piece shares a door with; _ob0 is ONE value per zone and is "
                        + $"deterministic - DecorationTable.Field1 = {MapGen.ObIdDaZona(z)}.");
                    // E o que cada celula ficou a ser, com o nome do `.set` que o estagio 6
                    // entrega ao carregador (55).  Sem isto a opcao dizia QUANTAS celulas
                    // levaram id e nao dizia QUAIS nem QUAL.
                    var grandes = MapGen.GrandesDaZona(z);
                    if (grandes.Count > 0)
                        Console.WriteLine("  big chunks of this zone (they ARE the candidate "
                            + "list of a multi-cell chunk): "
                            + string.Join(", ", grandes.Select(g => $"{g.Nome}={g.Valor}")));
                    EscreveTabelaDeVariantes(grelha, area, z);
                }
            }
        }
        // O estado do pos-processamento sai SEMPRE, com ou sem as opcoes: o que a ferramenta
        // nao modela nao deve depender de o utilizador ter pedido um passe (47.5).
        if (!args.Contains("--quiet-post"))
        {
            Console.WriteLine();
            Console.WriteLine("post-processing (0x00646E40), six stages, all accounted for:");
            try
            {
                foreach (var linha in MapGen.EstadoDoPosProcessamento(
                             GameData.GetArea(z.Model), p.G is null))
                    Console.WriteLine("  " + linha);
            }
            catch (Exception e)
            {
                Console.WriteLine($"  (no catalogue for model {z.Model}: {e.Message})");
            }
        }
        Console.WriteLine();
        Console.WriteLine(Previsao.Desenha(grelha, args.Contains("--doors")));
        return 0;
    }

    /// <summary>Varre uma arvore de `.bin` e conta quantas zonas produzem mapa.</summary>
    public static int Varre(string[] args)
    {
        string raiz = args.Length > 1 ? args[1] : ".";
        int limite = Opt(args, "--limit", 0);
        var bins = Directory.GetFiles(raiz, "*.bin", SearchOption.AllDirectories).OrderBy(f => f).ToList();
        if (limite > 0)
            bins = bins.Take(limite).ToList();
        int packets = 0, comMapa = 0, geradas = 0, semMapa = 0, semLps = 0, semTamanho = 0,
            semModelo = 0, exc = 0, ilegivel = 0;
        foreach (string f in bins)
        {
            Packet0324 pk;
            try { pk = Packet0324.Load(f); }
            catch { ilegivel++; continue; }
            packets++;
            foreach (var z in pk.Zonas())
            {
                if (z.Model is 0 or -1) { semModelo++; continue; }
                if (z.UnkB != 0 && (z.SizeX <= 0 || z.SizeY <= 0)) { semTamanho++; continue; }
                comMapa++;
                if (z.UnkB != 0 && !File.Exists(GameData.LpsPath(z.Model))) { semLps++; continue; }
                try
                {
                    var p = Previsao.Gera(z);
                    if (p.Ok) geradas++;
                    else
                    {
                        semMapa++;
                        Console.WriteLine($"NO MAP {Path.GetFileName(f)} zone {z.ZoneIndex} " +
                                          $"model {z.Model} {z.SizeX}x{z.SizeY} UnkB={z.UnkB}: {p.Nota}");
                    }
                }
                catch (Exception e)
                {
                    exc++;
                    Console.WriteLine($"EXCEPTION {Path.GetFileName(f)} zone {z.ZoneIndex} " +
                                      $"model {z.Model}: {e.GetType().Name}: {e.Message}");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine($"packets            {packets}");
        Console.WriteLine($"unreadable packet  {ilegivel}");
        Console.WriteLine($"zones with no model{semModelo,3}");
        Console.WriteLine($"zones with no size {semTamanho}");
        Console.WriteLine($"zones with a map   {comMapa}");
        Console.WriteLine($"no .lps            {semLps}");
        Console.WriteLine($"generated          {geradas}");
        Console.WriteLine($"no map             {semMapa}");
        Console.WriteLine($"exception          {exc}");
        return semMapa + exc + ilegivel == 0 ? 0 : 1;
    }

    private static ZoneInput? LeArgumentos(string[] args, out string cabecalho)
    {
        cabecalho = "";
        string? bloco = Str(args, "--block");
        if (bloco is not null)
        {
            List<ZoneInput> zonas;
            try
            {
                zonas = BlockRead.Le(bloco);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"could not read {bloco}: {e.Message}");
                return null;
            }
            // sem `--zone`, vale a zona do proprio ficheiro que se abriu
            var z = Str(args, "--zone") is not null
                ? zonas.FirstOrDefault(x => x.ZoneIndex == Opt(args, "--zone", 0))
                : zonas.FirstOrDefault(x => File.Exists(bloco)
                        && string.Equals(Path.GetFullPath(x.Ficheiro ?? ""),
                                         Path.GetFullPath(bloco), StringComparison.OrdinalIgnoreCase))
                  ?? zonas.FirstOrDefault();
            if (z is null)
            {
                Console.Error.WriteLine($"no such zone in {bloco}");
                return null;
            }
            cabecalho = $"block {Path.GetFileName(Path.GetDirectoryName(z.Ficheiro) ?? "")} " +
                        $"({zonas.Count} block(s)) zone {z.ZoneIndex}: " +
                        $"mapA=[{string.Join(", ", z.LadosA ?? new List<int>())}] " +
                        $"mapB=[{string.Join(", ", z.LadosB ?? new List<int>())}]";
            if (Str(args, "--p94") is not null)
                z.P94 = Opt(args, "--p94", 0);
            return z;
        }
        string? packet = Str(args, "--packet");
        if (packet is not null)
        {
            int zi = Opt(args, "--zone", 0);
            var pk = Packet0324.Load(packet);
            var z = pk.Zonas().FirstOrDefault(x => x.ZoneIndex == zi);
            if (z is null)
            {
                Console.Error.WriteLine($"the packet has no zone {zi}");
                return null;
            }
            // os lados podem ser trocados a' mao, que e' o que a GUI deixa fazer
            if (Str(args, "--sides-a") is string sa && sa.Length > 0)
                z.LadosA = sa.Split(',').Select(int.Parse).ToList();
            if (Str(args, "--sides-b") is string sb)
                z.LadosB = sb.Length == 0 ? new List<int>()
                                          : sb.Split(',').Select(int.Parse).ToList();
            cabecalho = $"packet {Path.GetFileName(packet)} zone {zi}: " +
                        $"mapA=[{string.Join(", ", z.LadosA ?? new List<int>())}] " +
                        $"mapB=[{string.Join(", ", z.LadosB ?? new List<int>())}]";
            if (Str(args, "--p94") is not null)
                z.P94 = Opt(args, "--p94", 0);
            return z;
        }
        if (Str(args, "--model") is null || Str(args, "--seed") is null
            || Str(args, "--sx") is null || Str(args, "--sy") is null)
        {
            Console.Error.WriteLine("missing --model/--seed/--sx/--sy (or use --packet)");
            return null;
        }
        var zin = new ZoneInput
        {
            Model = Opt(args, "--model", 0),
            Seed = Opt(args, "--seed", 0),
            SizeX = Opt(args, "--sx", 1),
            SizeY = Opt(args, "--sy", 1),
            UnkB = Opt(args, "--unkb", 1),
            Arguments = Opt(args, "--args", -1),
            UnkP = Opt(args, "--unkp", 0),
            UnkW = Opt(args, "--unkw", 0),
            Unk3 = Opt(args, "--unk3", -1),
            Unk4 = Opt(args, "--unk4", 0),
            Saida = Opt(args, "--exit", 0),
            NA = Opt(args, "--na", 1),
            A0 = Opt(args, "--a0", 0),
        };
        if (Str(args, "--entrance") is not null)
            zin.Entrada = Opt(args, "--entrance", 0);
        if (Str(args, "--p94") is not null)
            zin.P94 = Opt(args, "--p94", 0);
        if (Str(args, "--sides-a") is string la2 && la2.Length > 0)
            zin.LadosA = la2.Split(',').Select(int.Parse).ToList();
        if (Str(args, "--sides-b") is string lb2)
            zin.LadosB = lb2.Length == 0 ? new List<int>()
                                         : lb2.Split(',').Select(int.Parse).ToList();
        return zin;
    }

    private static string? Str(string[] args, string nome)
    {
        int i = Array.IndexOf(args, nome);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Opt(string[] args, string nome, int omissao)
    {
        string? s = Str(args, nome);
        if (s is null)
            return omissao;
        // a semente e' um u32 e passa de 0x7FFFFFFF em metade dos casos: aceita-se sem sinal
        if (int.TryParse(s, out int v))
            return v;
        return uint.TryParse(s, out uint u) ? unchecked((int)u) : omissao;
    }
}
