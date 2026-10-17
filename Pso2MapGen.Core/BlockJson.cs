using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pso2MapGen.Core;

/// <summary>
/// Escreve o `block.json` do Starlight a partir dos campos de uma zona.
///
/// Os campos que <b>decidem o mapa</b> sao os da FINDINGS 39.9 — os outros (inimigos, tempo,
/// musica) nao entram na geracao, e por isso so' sao escritos quando vieram de um packet:
///
///     ZoneIndex, ModelId, Seed, Arguments, SizeX, SizeY, UnkB
///     UnkP, UnkW                       (os designs de big chunk)
///     InfoTable3: { Unk3, Unk4 }       (o minLen da saida, o tecto de ramos)
///     UnkN                             (o bit 5 e' o gen-&gt;0xA0)
///     ZoneUnknown1: { unk1, unk2 }     (um par por bloco; o `unk2` e' o LADO)
///     TeleportData: [ { UnkA, DoorId, DstZone, BackDoorId, ... } ]
///
/// <b>Atencao ao `Seed`</b>: quem o sorteia por instancia e' o <b>servidor oficial</b>, por isso
/// dois packets da mesma area capturados em alturas diferentes trazem sementes diferentes e
/// comparar um com a geracao do outro nao prova nada (38.3).  O <b>Starlight</b> nao o sorteia:
/// manda a semente do `block.json` tal e qual, e os unicos outros sitios onde ela e' escrita sao
/// dois comandos de depuracao.  Ou seja, a semente que aqui ficar e' <b>mesmo</b> o mapa que a
/// area vai gerar.
/// </summary>
public static class BlockJson
{
    private static readonly JsonSerializerOptions Bonito = new() { WriteIndented = true };

    public static string Escreve(ZoneInput z)
    {
        var o = new JsonObject
        {
            ["ZoneIndex"] = z.ZoneIndex,
            ["Unknown04"] = z.Settings?.UnkA ?? 0,
            ["ModelId"] = z.Model,
            ["ZoneType"] = z.Settings?.ZoneType ?? 0,
            // Cada campo segue a convencao do ficheiro real, que **nao e' uniforme**: medido
            // nos 1587 `block.json` do disco, o `Seed` sai sempre sem sinal (797 valores
            // >= 0x80000000 e nenhum negativo) e o `Arguments` e o `SubArea` saem com sinal
            // (922 e 1180 negativos, contra 42 e 62 escritos como u32 por um escritor antigo).
            ["Seed"] = unchecked((uint)z.Seed),
            ["LatNumber"] = z.Arguments,
            ["MapWidth"] = z.SizeX,
            ["MapHeight"] = z.SizeY,
            ["MapType"] = z.UnkB,
            ["AreaIndex"] = z.Settings?.AreaIndex ?? 0,
            ["SubArea"] = z.Settings?.SubArea ?? -1,
            ["MapConfig"] = z.Settings?.UnkC ?? 0,
        };

        // As InfoTable de uma zona que veio de um packet sao as dela.  Uma zona feita a' mao nao
        // tem nenhumas, e escreve-las a zeros obriga a corrigir o ficheiro depois — por isso
        // caem nos valores habituais da area, do `infotables.txt` (ver InfoTables).  O `Unk1` e
        // o `Unk2` nao vem de tabela nenhuma: sao o `sizeX` e o `sizeY`, o que bate nas 1151
        // zonas do corpus sem uma excepcao.
        Poe(o, "DecorationTable", Tabela(z, z.It1?.Fields, 3, null, 1));
        Poe(o, "SetFileTable", Tabela(z, z.It2?.Fields, 5, null, 2));
        // O `Unk5` e' o quinto dword da linha (o `+0x1C` na memoria do cliente), que o leitor
        // antigo saltava como enchimento.  Vale 1 nas tres zonas de Kuron da `SUPRESS KODOTTA
        // IDIHTA` e 0 nas outras 1136 linhas; escreve-se sempre, para o servidor o poder mandar.
        Poe(o, "MapGenTable", Tabela(z, z.It3?.Fields, 3, new[]
        {
            ("MapWidth", z.It3?.Unk[0] ?? z.SizeX), ("MapHeight", z.It3?.Unk[1] ?? z.SizeY),
            ("ExitMinDistance", z.Unk3), ("OpenBranchCap", z.Unk4),
            ("ExtraPassFlag", z.It3?.Unk5 ?? 0),
        }, 3));

        // O `TeleportData` aparece nos `block.json` reais nas duas formas: um objecto quando ha
        // um warp so' e uma lista quando ha varios.  Escreve-se a lista, que e' a forma que
        // 466 dos 526 ficheiros usam.
        var warps = new JsonArray();
        foreach (var w in z.Warps)
            warps.Add(new JsonObject
            {
                ["UnkA"] = w.Unka,
                // O `-6` ("esta zona nao tem peca de saida", 5.3) escreve-se **com sinal**.  Nos
                // despejos do PacketMan ele aparece como 4294967290 — as duas formas andam pelo
                // corpus —, mas com sinal e' a que se le' sem ter de saber que sao 32 bits.
                ["DoorId"] = w.DoorId,
                ["DstZone"] = w.DstZone,
                ["BackDoorId"] = w.BackDoorId,
                ["DstEntrance"] = 0,
                ["FromEntrance"] = 0,
            });
        // uma zona terminal nao tem warp nenhum, e entao o campo tambem nao se escreve: um
        // `[]` nao diz nada e 372 dos 942 blocos do servidor nao o tem
        if (warps.Count > 0)
            o["TeleportData"] = warps;

        // A lista das entradas **nao vem para aqui**.  Era um par por bloco, e o numero de
        // entradas nao tem nada a ver com o numero de blocos: a Facility usa dois blocos e tres
        // entradas, e um par `(0,0)` — que e' enchimento de um bloco sem entrada propria — nem
        // se conseguia escrever.  Agora ela e' da quest, no `quest.json` sob `ZoneEntrances`, e
        // escreve-se inteira e por ordem.  Ver `BlockJson.Entradas`.

        // Os campos do `ZoneSettingsExtra`.  Vindo de um packet sao os **do packet**, sem
        // excepcao e sem media nenhuma; feitos a' mao caem no que aquela area costuma ter, da
        // linha `x =` do `infotables.txt` — e' o que evita exportar um registo inteiro a zeros
        // que nenhuma zona real tem (o `UnkN` a zero, por exemplo, nao aparece em nenhuma das
        // 1641 zonas do corpus).
        var e = z.Extra;
        int Campo(string nome, int doPacket, int repouso)
            => e is not null ? doPacket
                             : InfoTables.ExtraCampo(z.Model, z.UnkB, z.ZoneIndex, nome, repouso);

        // O `UnkAC` e' um float e escreve-se pelo **dword em bruto**: os `block.json` do
        // servidor levam `1133903872`, que sao os bits de `300.0f`.  Escrever `300` fazia o
        // cliente ler 4e-43 (FINDINGS 5.4).
        o["Unknown14"] = Campo("UnkAC", e?.UnkAC ?? 0, 0);
        // O `+0x18` sao quatro bytes e nao um numero: o byte 0 e' o modo de altura, que vira o
        // `gen->0x94` e e' lido com sinal, e o byte 1 e' o `zoneType & 1` nas 1641 zonas medidas.
        // Os bytes 2 e 3 estao a zero em todas.
        int unkO = Campo("UnkO", e?.UnkO ?? 0, 0);
        o["HeightMode"] = unchecked((sbyte)(unkO & 0xFF));
        o["MultiPartyArea"] = (unkO >> 8) & 0xFF;
        o["BigChunk1"] = z.UnkP;
        o["BigChunk1Variant"] = z.UnkQ;
        o["BigChunk2"] = z.UnkW;
        o["BigChunk2Variant"] = z.UnkY;
        o["Unknown88"] = Campo("UnkK", e?.UnkK ?? 0, 0);
        o["Unknown90"] = Campo("UnkL", e?.UnkL ?? 0, 0);
        o["UnknownA0"] = Campo("UnkAA", e?.UnkAA ?? 0, 0);
        o["UnknownA8"] = Campo("UnkAB", e?.UnkAB ?? 0, 0);
        o["UnknownB0"] = Campo("UnkR", e?.UnkR ?? 0, 0);
        o["UnknownB8"] = Campo("UnkS", e?.UnkS ?? 0, 0);
        o["UnknownC0"] = Campo("UnkT", e?.UnkT ?? 0, 0);
        o["UnknownC8"] = Campo("UnkM", e?.UnkM ?? 0, 0);
        o["UnknownD0"] = Campo("UnkU", e?.UnkU ?? 0, 0);
        // o `UnkN` vem sempre da zona: e' editavel e o bit 5 dele decide o mapa (38.8).  Uma
        // zona feita a' mao ja' nasce com o valor habitual da area, posto pela janela.
        o["ZoneFlags"] = z.UnkN;

        // Os dwords do registo que nem o PacketMan nem o Starlight escrevem, pelo deslocamento
        // deles dentro dos 0xF0 bytes.  Sao os da tabela da 5.4 e ficavam por escrever: o
        // `Raw04` diz 1..21 em 41 zonas, o `Raw10` diz 12 em 18, o `RawD4`/`RawD8` falam nos
        // modelos 510/511 e o `RawDC`/`RawE0` sao exclusivos do Sanctum.
        o["ExtraUnknown04"] = Campo("Raw04", e?.Raw04 ?? -1, -1);
        o["Unknown10"] = Campo("Raw10", e?.Raw10 ?? 0, 0);
        o["Unknown98"] = Campo("Raw98", e?.Raw98 ?? -1, -1);
        o["UnknownD4"] = Campo("RawD4", e?.RawD4 ?? -1, -1);
        o["UnknownD8"] = Campo("RawD8", e?.RawD8 ?? -1, -1);
        o["UnknownDC"] = Campo("RawDC", e?.RawDC ?? -1, -1);
        o["UnknownE0"] = Campo("RawE0", e?.RawE0 ?? 0, 0);

        // O registo Extra tem o seu **proprio** `Arguments`, no `+0x0C`, e ele nao acompanha
        // sempre o da zona: em 157 das 1641 zonas fica -1 enquanto a zona diz outra coisa (5.4).
        // So' se escreve quando divergem, que e' quando ele diz algo de novo.
        if (e is not null && e.Arguments != z.Arguments)
            o["Unknown0C"] = e.Arguments;

        // e o que resta do registo, que esta' a zero nas 1641 zonas medidas: so' se escreve o
        // que fuja disso, para o ficheiro nao levar 34 linhas de zeros e nao se perder nada
        foreach (var (off, valor) in e?.Sobras ?? new List<(int, int)>())
            o[Chaves.PorDeslocamento(off)] = valor;

        return o.ToJsonString(Bonito);
    }

    /// <summary>
    /// Uma das InfoTable, com tres casos:
    ///
    /// <list type="bullet">
    /// <item>a zona <b>traz</b> a tabela (veio de um packet): escreve-se essa;</item>
    /// <item>a zona foi feita a' <b>mao</b>: caem os valores habituais da area, do
    /// `infotables.txt` — e' o que evita exportar tudo a zeros e ter de corrigir depois;</item>
    /// <item>a zona <b>veio de um packet</b> mas o packet nao tem linha nenhuma para ela — as
    /// zonas de mapa fixo nao a tem na InfoTable1 nem na InfoTable3 — e entao escreve-se o que
    /// os ficheiros reais escrevem: os ids a <b>zero</b> e os valores a <b>null</b>, que e' a
    /// forma que 263 dos 263 blocos do disco nesse caso usam.</item>
    /// </list>
    /// </summary>
    private static JsonObject? Tabela(ZoneInput z, List<InfoField>? campos, int quantos,
                                      (string Nome, int Valor)[]? cabeca, int qual)
    {
        if (campos is not null)
            return Tabela(campos, quantos, cabeca);
        if (z.Settings is null)
            return Tabela(InfoTables.Campos(z.Model, z.UnkB, z.ZoneIndex, qual), quantos, cabeca);
        // A zona veio de um packet e o packet **nao tem linha** para ela nesta tabela — e' o que
        // acontece aos mapas fixos, que nao a tem na InfoTable1 nem na InfoTable3 (41.1).  Nao se
        // escreve nada: a forma vazia (`Field1: 0, Field1Value: null`) nao esta' no packet e so'
        // obriga a abrir o ficheiro depois para a apagar a' mao.  E' tambem o que os blocos do
        // servidor fazem — 295 dos 942 nao tem `InfoTable1`, e os de Kuron trazem-na comentada.
        return null;
    }

    /// <summary>
    /// A lista de entradas deste mapa, na forma que o `quest.json` da Starlight quer:
    /// <c>ZoneEntrances</c>, um par <c>[zona, lado]</c> por entrada, por ordem.
    ///
    /// <para>Sai daqui e nao de dentro dos blocos porque o numero de entradas nao acompanha o
    /// numero de blocos.  Um par <c>(0,0)</c> e' enchimento de um bloco sem entrada propria e vai
    /// escrito como qualquer outro: tira-lo empurrava as entradas seguintes para o bloco
    /// errado.</para>
    /// </summary>
    public static string Entradas(IList<ZoneInput> zonas)
    {
        var lista = zonas.FirstOrDefault(z => z.Zu1.Count > 0)?.Zu1;
        if (lista is null || lista.Count == 0)
        {
            // um mapa feito a' mao: uma entrada por zona, o primeiro lado do mapA de cada uma
            lista = zonas.Where(z => z.OrdemBloco >= 0)
                         .SelectMany(z => (z.LadosA ?? new List<int>())
                                          .Where(s => s >= 0 && s <= 3)
                                          .OrderByDescending(s => s)
                                          .Select(s => (z.ZoneIndex, s)))
                         .ToList();
        }
        if (lista.Count == 0)
            lista = new List<(int, int)> { (0, 0) };
        return "\"ZoneEntrances\": [ "
               + string.Join(", ", lista.Select(p => $"[{p.Item1}, {p.Item2}]"))
               + " ],";
    }

    /// <summary>
    /// O ficheiro `ZoneEntrances.json` que sai ao lado dos blocos exportados.
    ///
    /// <para>E' um fragmento e nao uma quest: leva so' a lista, para se colar no `quest.json` a
    /// serio.  Sai em `.json` de proposito — o leitor da ferramenta procura a lista para cima a
    /// partir da pasta do bloco, por isso um conjunto exportado volta a abrir com os lados de
    /// entrada certos sem precisar de mais nada.</para>
    /// </summary>
    public static string FragmentoDasEntradas(string linha)
        => "{" + Environment.NewLine
           + "  // Not a quest. Paste the line below into the quest's own quest.json, beside"
           + Environment.NewLine
           + "  // \"Blocks\". Starlight reads the entrance list from there now, so the old limit"
           + Environment.NewLine
           + "  // - one entry per block, which lost the tail of a longer list - is gone." + Environment.NewLine
           + "  " + linha.TrimEnd(',') + Environment.NewLine
           + "}" + Environment.NewLine;

    /// <summary>Poe o campo, se ele existir.</summary>
    private static void Poe(JsonObject o, string nome, JsonObject? valor)
    {
        if (valor is not null)
            o[nome] = valor;
    }

    private static JsonObject Tabela(List<InfoField>? campos, int quantos,
                                     (string Nome, int Valor)[]? cabeca)
    {
        var t = new JsonObject();
        foreach (var (nome, valor) in cabeca ?? Array.Empty<(string, int)>())
            t[nome] = valor;
        for (int i = 0; i < quantos; i++)
        {
            var c = campos is not null && i < campos.Count ? campos[i] : null;
            t[$"Field{i + 1}"] = c?.Id ?? (i == 0 ? 0 : -1);
            var vals = new JsonArray();
            for (int k = 0; k < 10; k++)
            {
                // `JsonArray.Add<T>` com um `int` inferiria `int?` e criaria um no' que o
                // serializador nao sabe escrever sem resolver; o `JsonValue.Create` tira a duvida.
                int v = c?.Values[k] ?? 0;
                vals.Add(JsonValue.Create(v));
            }
            t[$"Field{i + 1}Value"] = vals;
        }
        return t;
    }

    private static JsonObject Tabela(InfoTableRow? linha, int quantos,
                                     (string Nome, int Valor)[]? cabeca)
        => Tabela(linha?.Fields, quantos, cabeca);

    /// <summary>
    /// Monta o <c>TeleportData</c> e o <c>ZoneUnknown1</c> de um mapa feito <b>a' mao</b>, onde
    /// nao ha packet de onde os tirar.  A regra e' a mais simples que reproduz a forma dos
    /// packets reais:
    ///
    /// <list type="bullet">
    /// <item>cada zona sai pelo <b>primeiro</b> dos seus lados de saida para a zona seguinte da
    /// lista, que se entra pelo <b>primeiro</b> dos lados de entrada dela;</item>
    /// <item>o <c>unka</c> do warp e' 2, que e' o que 1056 dos 1059 warps reais usam;</item>
    /// <item>o <c>zoneUnknown1</c> leva uma entrada por bloco, pela ordem dos blocos, e cada
    /// uma e' <c>(zona, primeiro lado de entrada)</c> — assim a regra posicional do
    /// StarlightPacketMan e a leitura do cliente dao a mesma coisa.</item>
    /// </list>
    ///
    /// As zonas que <b>vieram de um packet</b> nao sao tocadas: essas exportam-se tal e qual,
    /// que e' o caminho conferido contra os 1032 `block.json` do disco.
    /// </summary>
    public static void LigaZonas(IList<ZoneInput> zonas) => Reconcilia(zonas);

    /// <summary>
    /// Poe o <c>zoneUnknown1</c> e o <c>TeleportData</c> de acordo com os lados que as zonas
    /// <b>têm agora</b>.
    ///
    /// <para>Enquanto ninguem mexer nos lados, nao faz nada: as zonas que vieram de um packet
    /// exportam-se tal e qual, que e' o caminho conferido contra os 1032 `block.json` do disco e
    /// o unico que preserva as topologias que a cadeia simples nao sabe escrever (as quests de
    /// sub-area, onde tres zonas desaguam na mesma).</para>
    ///
    /// <para>Mas <b>se os lados forem editados</b> — e e' para isso que a ferramenta os deixa
    /// editar — os dados guardados deixam de os descrever, e exportar o que veio no packet
    /// escreveria um mapa diferente do que se esta' a ver.  Nesse caso reconstroi-se tudo:</para>
    ///
    /// <list type="bullet">
    /// <item>os warps sao a cadeia <c>zona i -&gt; zona i+1</c>, com <c>unka = 2</c> (o que 1056
    /// dos 1059 warps reais usam), saindo pelo primeiro lado de saida e entrando pelo primeiro
    /// lado de entrada da seguinte;</item>
    /// <item>o <c>zoneUnknown1</c> leva <b>so' as entradas que nenhum warp ja' da'</b>, por
    /// ordem de zona e por lado descendente — que e' a forma dos packets reais.  Uma zona que
    /// recebe warp nao precisa de entrada nenhuma na lista, e os blocos que sobram levam
    /// <c>(0,0)</c>, como o PacketMan escreve.</item>
    /// </list>
    ///
    /// <para>E' o que faz um mapa com a zona 0 a entrar so' a sul sair como
    /// <c>[(0,2)]</c> — e nao com a lista do packet de onde os campos vieram.</para>
    /// </summary>
    public static void Reconcilia(IList<ZoneInput> zonas)
    {
        // a ordem dos blocos conta-se so' sobre as zonas que recebem bloco
        int ordem = 0;
        foreach (var z in zonas)
            z.OrdemBloco = z.ZoneIndex >= 0 ? ordem++ : -1;

        if (!PrecisaReconstruir(zonas))
            return;

        var comBloco = zonas.Where(z => z.OrdemBloco >= 0).ToList();

        // 1. os warps.  **Preserva-se a topologia**: uma zona que ja' tinha warp continua a
        // levar para o mesmo destino, e so' os lados e' que sao refeitos — senao as quests de
        // sub-area, onde tres zonas desaguam na mesma, viravam uma cadeia em fila indiana.  Uma
        // zona que nao tinha warp liga-se a' seguinte, que e' o unico palpite razoavel.
        var warps = new List<Warp>();
        var ocupados = new Dictionary<int, HashSet<int>>();      // destino -> lados ja' usados
        for (int i = 0; i < comBloco.Count; i++)
        {
            var z = comBloco[i];
            var orig = z.Warps.FirstOrDefault();
            var saidas = (z.LadosB ?? new List<int>()).Where(s => s >= 0 && s <= 3)
                                                      .Distinct().OrderBy(s => s).ToList();
            if (saidas.Count == 0 && orig is null)
                continue;                        // zona terminal, e sempre foi
            int dst = orig?.DstZone
                      ?? (i + 1 < comBloco.Count ? comBloco[i + 1].ZoneIndex : z.ZoneIndex + 1);
            var destino = zonas.FirstOrDefault(x => x.ZoneIndex == dst);
            if (!ocupados.TryGetValue(dst, out var usados))
                ocupados[dst] = usados = new HashSet<int>();

            // a saida do gerador e' o primeiro lado do mapB; sem mapB e' o -6 da 5.3, que e'
            // "esta zona nao tem peca de saida" e nao "esta zona nao leva a lado nenhum"
            int n = Math.Max(1, saidas.Count);
            for (int k = 0; k < n; k++)
            {
                int porta = k < saidas.Count ? saidas[k] : -6;
                int volta = EscolheEntrada(destino, orig, k, usados);
                warps.Add(new Warp
                {
                    Unka = orig?.Unka ?? 2,
                    ZoneIndex = z.ZoneIndex,
                    DoorId = porta,
                    DstZone = dst,
                    BackDoorId = volta,
                });
                if (volta >= 0)
                    usados.Add(volta);
            }
        }

        // 2. a lista, so' com o que os warps nao dao
        var lista = new List<(int, int)>();
        foreach (var z in comBloco)
        {
            var dados = warps.Where(w => w.DstZone == z.ZoneIndex)
                             .Select(w => w.BackDoorId)
                             .ToHashSet();
            foreach (int lado in (z.LadosA ?? new List<int>()).Distinct()
                                                              .Where(s => s >= 0 && s <= 3)
                                                              .OrderByDescending(s => s))
                if (!dados.Contains(lado))
                    lista.Add((z.ZoneIndex, lado));
        }

        foreach (var z in zonas)
        {
            z.Zu1 = new List<(int, int)>(lista);
            z.Warps = warps.Where(w => w.ZoneIndex == z.ZoneIndex).ToList();
        }
    }

    /// <summary>
    /// Os dados guardados ainda descrevem os lados que as zonas tem?  Uma zona feita a' mao
    /// nunca os tem, e uma zona editada deixa de os ter.
    /// </summary>
    private static bool PrecisaReconstruir(IList<ZoneInput> zonas)
    {
        if (zonas.Any(z => z.Settings is null))
            return true;
        var zu1 = zonas.FirstOrDefault(z => z.Zu1.Count > 0)?.Zu1 ?? new List<(int, int)>();
        var warps = zonas.SelectMany(z => z.Warps).ToList();
        foreach (var z in zonas)
        {
            var (a, b) = BlockRead.Lados(zu1, warps, z.ZoneIndex);
            if (!Mesmos(a, z.LadosA) || !Mesmos(b, z.LadosB))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Por que lado e' que este warp entra no destino.  Fica-se pelo lado que ele ja' usava,
    /// quando ainda e' uma entrada da zona de destino e ninguem o levou — e' o que mantem as
    /// tres entradas distintas das quests de sub-area; senao toma-se a primeira que esteja
    /// livre.
    /// </summary>
    private static int EscolheEntrada(ZoneInput? destino, Warp? orig, int k, HashSet<int> usados)
    {
        var entradas = (destino?.LadosA ?? new List<int>()).Where(s => s >= 0 && s <= 3)
                                                           .Distinct().OrderBy(s => s).ToList();
        if (k == 0 && orig is not null && entradas.Contains(orig.BackDoorId)
            && !usados.Contains(orig.BackDoorId))
            return orig.BackDoorId;
        foreach (int s in entradas)
            if (!usados.Contains(s))
                return s;
        return orig?.BackDoorId ?? -6;
    }

    /// <summary>
    /// <b>Ja' nao ha' entradas que nao caibam: zero, sempre.</b>
    ///
    /// <para>Havia, enquanto a lista vivia dentro dos blocos — o ficheiro tinha lugar para uma
    /// por bloco, e uma lista mais comprida do que o numero de blocos perdia as ultimas.  A lista
    /// passou para o `quest.json`, onde vai inteira, e o problema deixou de existir.  O metodo
    /// fica porque a janela e a linha de comandos o chamam; devolve zero.</para>
    /// </summary>
    public static int NaoCabem(IList<ZoneInput> zonas) => 0;

    private static bool Mesmos(List<int> a, List<int>? b)
        => a.Count == (b?.Count ?? 0) && a.All(x => b!.Contains(x));

    private static int? PrimeiroLado(List<int>? lados)
        => lados is { Count: > 0 } ? lados.Min() : null;
}
