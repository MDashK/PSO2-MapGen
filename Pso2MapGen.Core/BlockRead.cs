using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pso2MapGen.Core;

/// <summary>
/// Le' os `block.json` do Starlight e devolve as zonas na mesma forma que o leitor de packets
/// — e' o caminho inverso do <see cref="BlockJson.Escreve"/>, para se poder ver o mapa que um
/// conjunto de blocos gera sem abrir o jogo.
///
/// <para><b>Um bloco nao chega.</b>  O `block.json` traz o <c>TeleportData</c> da sua zona (os
/// warps que <b>saem</b> dela) e <b>uma</b> entrada do <c>zoneUnknown1</c>, repartida
/// posicionalmente (FINDINGS 40.3).  O mapA de uma zona precisa dos <c>BackDoorId</c> dos warps
/// que <b>chegam</b> a ela, que vivem nos outros blocos, e da lista inteira do
/// <c>zoneUnknown1</c>, que so' se reconstroi juntando todos.  Por isso o que se abre e' sempre
/// o <b>conjunto</b>: dado um ficheiro, carregam-se os irmaos `b*/block.json` ao lado dele.</para>
///
/// <para><b>E o conjunto pode nao ser o packet.</b>  Se a pasta tiver menos blocos do que o
/// packet original tinha entradas de <c>zoneUnknown1</c>, a lista reconstruida fica mais curta
/// e o mapA sai diferente — o `ruins/3` e' o exemplo: o packet traz
/// <c>[(0,3), (0,2), (0,1)]</c> e os dois blocos do disco so' guardam as duas primeiras.  O
/// mapa que sai daqui e' o que <b>este</b> conjunto de ficheiros produz, que e' precisamente o
/// que se quer ver quando se edita um `block.json`.</para>
/// </summary>
public static class BlockRead
{
    private static readonly JsonDocumentOptions Solto = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Os ficheiros que formam o conjunto.  Com um ficheiro, sao os `block.json` das pastas
    /// irmas (`b1`, `b2`, ...); com uma pasta, os que estiverem debaixo dela.  A ordem e' a
    /// natural do nome da pasta, que e' a ordem dos blocos.
    /// </summary>
    public static List<string> Ficheiros(string caminho)
    {
        var lista = new List<string>();
        if (File.Exists(caminho))
        {
            string? pasta = Path.GetDirectoryName(Path.GetFullPath(caminho));
            string? pai = pasta is null ? null : Path.GetDirectoryName(pasta);
            if (pai is not null)
                lista = Directory.GetDirectories(pai)
                                 .Select(d => Path.Combine(d, "block.json"))
                                 .Where(File.Exists)
                                 .ToList();
            if (!lista.Any(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(caminho),
                                              StringComparison.OrdinalIgnoreCase)))
                lista = new List<string> { caminho };   // um ficheiro solto: so' ele
        }
        else if (Directory.Exists(caminho))
        {
            lista = Directory.GetDirectories(caminho)
                             .Select(d => Path.Combine(d, "block.json"))
                             .Where(File.Exists)
                             .ToList();
            string solto = Path.Combine(caminho, "block.json");
            if (lista.Count == 0 && File.Exists(solto))
                lista.Add(solto);
        }
        return lista.OrderBy(f => Path.GetFileName(Path.GetDirectoryName(f)) ?? "", Natural.Ordem)
                    .ToList();
    }

    /// <summary>Le' o conjunto e devolve as zonas, ja' com o mapA e o mapB montados.</summary>
    public static List<ZoneInput> Le(string caminho)
        => Le(Ficheiros(caminho), caminho);

    /// <summary>
    /// O mesmo, mas com os blocos <b>dados</b> e por <b>aquela</b> ordem.
    ///
    /// <para>E' o que a abertura por `quest.json` usa: a quest diz que blocos e' que carrega, e
    /// isso nem sempre e' o que esta' na pasta — ha' quests no servidor com um `b4` ou um `.7z`
    /// ao lado que elas nao carregam.  E a ordem importa, porque a lista de entradas e' repartida
    /// por <b>posicao</b>.</para>
    /// </summary>
    public static List<ZoneInput> Le(List<string> ficheiros, string caminho)
    {
        if (ficheiros.Count == 0)
            throw new FileNotFoundException($"no block.json in {caminho}");

        var zonas = new List<ZoneInput>();
        var zu1 = new List<(int, int)>();
        var warps = new List<Warp>();

        for (int i = 0; i < ficheiros.Count; i++)
        {
            JsonObject o;
            try
            {
                o = Raiz(ficheiros[i]);
            }
            catch (Exception) when (!string.Equals(Path.GetFullPath(ficheiros[i]),
                                                   Path.GetFullPath(caminho),
                                                   StringComparison.OrdinalIgnoreCase))
            {
                // um bloco irmao ilegivel nao deita o conjunto abaixo, mas o seu lugar conta:
                // o `zoneUnknown1` e' repartido por **posicao**, e saltar o lugar desalinhava
                // os que vem a seguir
                zu1.Add((0, 0));
                continue;
            }
            var z = Zona(o);
            z.OrdemBloco = i;
            z.Ficheiro = ficheiros[i];
            zonas.Add(z);

            // o `zoneUnknown1` do packet reconstroi-se juntando o par de cada bloco, pela ordem
            // dos blocos — e' a reparticao posicional do StarlightPacketMan ao contrario.  Um
            // bloco sem o campo ocupa na mesma o seu lugar, com `(0,0)`, que e' o que o
            // PacketMan escreve nos blocos que sobram.
            var pares = Lista(o["ZoneUnknown1"]).ToList();
            if (pares.Count == 0)
                zu1.Add((0, 0));
            foreach (var n in pares)
                zu1.Add((I(n?["unk1"] ?? n?["Unk1"]), I(n?["unk2"] ?? n?["Unk2"])));

            // o `TeleportData` de um bloco sao os warps que **saem** da zona dele
            foreach (var n in Lista(o["TeleportData"]))
            {
                var w = new Warp
                {
                    Unka = I(n?["UnkA"]),
                    ZoneIndex = z.ZoneIndex,
                    DoorId = I(n?["DoorId"], -6),
                    DstZone = I(n?["DstZone"]),
                    BackDoorId = I(n?["BackDoorId"], -6),
                };
                // Um bloco sem warp nenhum leva na mesma um registo, todo a zeros, e ele **nao
                // e' um warp**: nos 1032 pares do disco aparecem 357 registos `(0,0,0,0)` que o
                // packet nao tem, e nenhum dos 1059 warps reais e' todo a zeros.  Sem este
                // filtro o `(0,0,0,0)` de uma zona terminal entrava como "warp que chega a' zona
                // 0 pelo lado 0" e estragava o mapA da primeira zona.
                if (w.Unka == 0 && w.DoorId == 0 && w.DstZone == 0 && w.BackDoorId == 0)
                    continue;
                warps.Add(w);
            }
        }

        // A lista de entradas passou para o `quest.json` da quest, sob `ZoneEntrances`, porque o
        // numero de entradas nao acompanha o numero de blocos.  Quando ela esta' la', e' ela que
        // manda — os pares que ainda estejam nos blocos sao de um ficheiro por converter.
        var daQuest = EntradasDaQuest(ficheiros.Count > 0 ? ficheiros[0] : caminho);
        if (daQuest is not null && daQuest.Count > 0)
            zu1 = daQuest;

        zu1 = SemEnchimento(zu1);
        // o modelo de cada zona lida: e' a chave do `paramsC`/`paramsD` do `.lps` que da' o
        // nome das pecas de entrada e de saida (41.17)
        var modeloPorZona = new Dictionary<int, int>();
        // e o tamanho, para se saber se a zona de destino e' um mapa FIXO (55.8).  Constroi-se
        // com o indexador e nao com o `ToDictionary`: um conjunto pode ter dois blocos com o
        // MESMO `ZoneIndex` (o `lobby` tem, no indice -14) e o `ToDictionary` rebenta.
        var tamanhoPorZona = new Dictionary<int, (int X, int Y)>();
        foreach (var z in zonas)
        {
            modeloPorZona[z.ZoneIndex] = z.Model;
            tamanhoPorZona[z.ZoneIndex] = (z.SizeX, z.SizeY);
        }
        foreach (var z in zonas)
        {
            var (la, lb) = Lados(zu1, warps, z.ZoneIndex);
            z.LadosA = la.Count > 0 ? la : null;
            z.LadosB = lb;
            z.NA = Math.Max(1, la.Count);
            z.Zu1 = new List<(int, int)>(zu1);
            z.Warps = warps.Where(w => w.ZoneIndex == z.ZoneIndex).ToList();
            var (mOrg, mDst) = MapGen.ModelosVizinhos(warps, modeloPorZona, z.ZoneIndex);
            z.ModeloDeOndeVem = mOrg;
            z.ModeloParaOndeVai = mDst;
            // o destino e' um mapa fixo? (55.8) - os blocos trazem o tamanho de cada zona
            z.DestinoEhMapaFixo = MapGen.DestinoEhMapaFixo(warps, tamanhoPorZona, z.ZoneIndex);
        }
        // guarda-se o estado de partida de cada zona: e' contra ele que o `BlockJson.Actualiza`
        // decide o que e' preciso escrever de volta no ficheiro e o que fica intocado
        foreach (var z in zonas)
        {
            var copia = z.Clone();
            copia.ComoVeio = null;
            z.ComoVeio = copia;
        }
        return zonas;
    }

    /// <summary>
    /// Tira o enchimento do fim da lista.  O <c>zoneUnknown1</c> do packet e' mais curto do que
    /// o numero de blocos, e os blocos que sobram levam <c>(0,0)</c> (FINDINGS 40.3) — se esses
    /// pares entrassem na regra dos lados, a zona 0 ganhava uma entrada pelo lado 0 que nao
    /// existe.  Tiram-se os <c>(0,0)</c> <b>do fim</b>, que e' onde o enchimento esta': das sete
    /// formas que o corpus tem, nenhuma leva um <c>(0,0)</c> pelo meio, e a que e' so'
    /// <c>((0,0),)</c> — 82 packets — fica salva pela ultima linha.
    /// </summary>
    public static List<(int, int)> SemEnchimento(List<(int, int)> zu1)
    {
        int n = zu1.Count;
        while (n > 0 && zu1[n - 1] == (0, 0))
            n--;
        if (n == 0 && zu1.Count > 0)
            n = 1;                      // a lista era so' enchimento: o primeiro par e' real
        return zu1.Take(n).ToList();
    }

    /// <summary>A regra da FINDINGS 37.6, sobre a lista reconstruida.</summary>
    public static (List<int> A, List<int> B) Lados(List<(int Zona, int Lado)> zu1,
                                                   List<Warp> warps, int z)
    {
        var a = new SortedSet<int>();
        foreach (var (zi, s) in zu1)
            if (zi == z && s >= 0 && s <= 3)
                a.Add(s);
        foreach (var w in warps)
            if (w.DstZone == z && w.BackDoorId >= 0 && w.BackDoorId <= 3)
                a.Add(w.BackDoorId);
        var b = new SortedSet<int>();
        foreach (var w in warps)
            if (w.ZoneIndex == z && w.DoorId >= 0 && w.DoorId <= 3 && !a.Contains(w.DoorId))
                b.Add(w.DoorId);
        return (a.ToList(), b.ToList());
    }

    // ---- um ficheiro ------------------------------------------------------------------

    /// <summary>
    /// A lista de entradas do `quest.json` da quest a que este bloco pertence, ou null quando nao
    /// se encontra nenhum.
    ///
    /// <para>A arvore e' `.../world/&lt;quest&gt;/variant/&lt;n&gt;/b1/block.json` e o `quest.json`
    /// esta' na pasta da quest, tres niveis acima da do bloco.  Procura-se para cima porque o
    /// nome do ficheiro nao e' garantido — vale qualquer `.json` que traga a lista.</para>
    /// </summary>
    public static List<(int, int)>? EntradasDaQuest(string caminhoDoBloco)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(caminhoDoBloco));
        for (int i = 0; i < 5 && dir is not null; i++)
        {
            foreach (string f in Directory.GetFiles(dir, "*.json"))
            {
                if (string.Equals(Path.GetFileName(f), "block.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    var raiz = JsonNode.Parse(File.ReadAllText(f), null, Solto)?.AsObject();
                    var n = raiz?["ZoneEntrances"] ?? raiz?["ZoneUnknown1"];
                    if (n is not JsonArray arr)
                        continue;
                    var fora = new List<(int, int)>();
                    foreach (var par in arr)
                        if (par is JsonArray p2 && p2.Count >= 2)
                            fora.Add((I(p2[0]), I(p2[1])));
                    if (fora.Count > 0)
                        return fora;
                }
                catch (Exception)
                {
                    // um json qualquer da pasta que nao e' a quest; segue-se para o seguinte
                }
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>
    /// O campo pelo nome de agora, e se nao estiver la', pelo nome antigo (ver Chaves).  E' o que
    /// deixa a ferramenta abrir tanto um `block.json` ja' convertido pela Starlight como um que
    /// ainda esteja na forma antiga.
    /// </summary>
    private static JsonNode? K(JsonObject o, string agora)
    {
        JsonNode? t = o[agora];
        if (t is not null)
            return t;
        string? antigo = Chaves.Antigo(agora);
        return antigo is null ? null : o[antigo];
    }

    /// <summary>O mesmo, para um campo de dentro da <c>MapGenTable</c>.</summary>
    private static JsonNode? KG(JsonObject o, string agora)
    {
        JsonNode? t = o[agora];
        if (t is not null)
            return t;
        string? antigo = Chaves.AntigoNaMapGen(agora);
        return antigo is null ? null : o[antigo];
    }

    /// <summary>
    /// O `+0x18`.  Na forma antiga era um dword so', o `UnkO`; agora sao dois bytes com nome, o
    /// `HeightMode` (byte 0, com sinal) e o `MultiPartyArea` (byte 1).
    /// </summary>
    private static int LeUnkO(JsonObject o)
    {
        if (o["UnkO"] is not null)
            return I(o["UnkO"]);
        int modo = I(o["HeightMode"]) & 0xFF;
        int multi = I(o["MultiPartyArea"]) & 0xFF;
        return modo | (multi << 8);
    }

    private static JsonObject Raiz(string f)
    {
        // os `block.json` da Starlight trazem comentarios `//` e virgulas a mais; os despejos
        // do PacketMan sao JSON estrito.  Este leitor aceita os dois — e as opcoes tem de ir
        // para o `JsonNode.Parse`, porque o `GetRawText` de um `JsonDocument` devolveria o
        // texto original, comentarios inclusive.
        return JsonNode.Parse(File.ReadAllText(f), null, Solto)!.AsObject();
    }

    private static ZoneInput Zona(JsonObject o)
    {
        int unkN = I(K(o, "ZoneFlags"));
        var e = new ZoneExtra
        {
            ZoneIndex = I(o["ZoneIndex"]),
            ModelId = I(o["ModelId"]),
            Arguments = I(K(o, "LatNumber"), -1),
            // o `UnkAC` e' um float guardado pelos bits: `1133903872` sao os bits de `300.0f`.
            // Le-se como dword, que e' a forma que os ficheiros usam (FINDINGS 5.4).
            UnkAC = I(K(o, "Unknown14")),
            UnkO = LeUnkO(o),
            UnkP = I(K(o, "BigChunk1")), UnkQ = I(K(o, "BigChunk1Variant")),
            UnkW = I(K(o, "BigChunk2")), UnkY = I(K(o, "BigChunk2Variant")),
            UnkK = I(K(o, "Unknown88")), UnkL = I(K(o, "Unknown90")),
            UnkAA = I(K(o, "UnknownA0")), UnkAB = I(K(o, "UnknownA8")),
            UnkR = I(K(o, "UnknownB0")), UnkS = I(K(o, "UnknownB8")), UnkT = I(K(o, "UnknownC0")),
            UnkM = I(K(o, "UnknownC8")), UnkU = I(K(o, "UnknownD0")), UnkN = unkN,
            // os dwords do registo que nem o PacketMan nem o Starlight escreviam: nos ficheiros
            // que ja' existem nao estao, e nesse caso ficam no valor de repouso deles (5.4)
            Raw04 = I(K(o, "ExtraUnknown04"), -1), Raw10 = I(K(o, "Unknown10")), Raw98 = I(K(o, "Unknown98"), -1),
            RawD4 = I(K(o, "UnknownD4"), -1), RawD8 = I(K(o, "UnknownD8"), -1), RawDC = I(K(o, "UnknownDC"), -1),
            RawE0 = I(K(o, "UnknownE0")),
        };
        var s = new ZoneSettings
        {
            UnkA = I(K(o, "Unknown04")),
            ZoneIndex = I(o["ZoneIndex"]),
            ModelId = I(o["ModelId"]),
            ZoneType = I(o["ZoneType"]),
            Seed = I(o["Seed"]),
            Arguments = I(K(o, "LatNumber"), -1),
            SizeX = I(K(o, "MapWidth")),
            SizeY = I(K(o, "MapHeight")),
            UnkB = I(K(o, "MapType"), 1),
            AreaIndex = I(o["AreaIndex"]),
            SubArea = I(o["SubArea"], -1),
            UnkC = I(K(o, "MapConfig")),
        };
        var t3 = Tabela3(K(o, "MapGenTable"), s.ZoneIndex);
        return new ZoneInput
        {
            ZoneIndex = s.ZoneIndex,
            Model = s.ModelId,
            Seed = s.Seed,
            Arguments = s.Arguments,
            SizeX = s.SizeX,
            SizeY = s.SizeY,
            UnkB = s.UnkB,
            UnkP = e.UnkP,
            UnkW = e.UnkW,
            UnkQ = e.UnkQ,
            UnkY = e.UnkY,
            UnkN = unkN,
            A0 = (unkN >> 5) & 1,
            UnkO = e.UnkO,
            TemUnkO = o["UnkO"] is not null || o["HeightMode"] is not null,
            Unk5 = t3.Unk5,
            Unk3 = t3.Unk[2],
            Unk4 = t3.Unk[3],
            Settings = s,
            Extra = e,
            It1 = Tabela(K(o, "DecorationTable"), s.ZoneIndex),
            It2 = Tabela(K(o, "SetFileTable"), s.ZoneIndex),
            It3 = t3,
        };
    }

    private static InfoTableRow? Tabela(JsonNode? n, int zona)
    {
        if (n is not JsonObject o)
            return null;
        var linha = new InfoTableRow { ZoneIndex = zona };
        for (int i = 1; o[$"Field{i}"] is not null; i++)
            linha.Fields.Add(Campo(o, i));
        return linha;
    }

    private static InfoTable3Row Tabela3(JsonNode? n, int zona)
    {
        var linha = new InfoTable3Row { Z1 = zona, Z2 = zona };
        linha.Unk[2] = -1;
        if (n is not JsonObject o)
            return linha;
        linha.Unk[0] = I(KG(o, "MapWidth"));
        linha.Unk[1] = I(KG(o, "MapHeight"));
        linha.Unk[2] = I(KG(o, "ExitMinDistance"), -1);
        linha.Unk[3] = I(KG(o, "OpenBranchCap"));
        linha.Unk5 = I(KG(o, "ExtraPassFlag"));      // o `+0x1C` da linha; nos ficheiros de hoje nao esta'
        for (int i = 1; o[$"Field{i}"] is not null; i++)
            linha.Fields.Add(Campo(o, i));
        return linha;
    }

    private static InfoField Campo(JsonObject o, int i)
    {
        var c = new InfoField { Id = I(o[$"Field{i}"], -1) };
        if (o[$"Field{i}Value"] is JsonArray v)
            for (int k = 0; k < 10 && k < v.Count; k++)
                c.Values[k] = I(v[k]);
        return c;
    }

    /// <summary>
    /// Um objecto ou uma lista: os ficheiros reais usam as duas formas.  Qualquer outra coisa
    /// (ausente, `null`, um numero) da' uma lista vazia — este leitor **ignora tudo o que nao
    /// reconhece** em vez de se queixar, que e' o que deixa abrir um `block.json` do servidor
    /// com os seus `DropTables`, `EnemyID`, `Entrances`, `Warps`, `Forecast` e o resto.
    /// </summary>
    private static IEnumerable<JsonNode?> Lista(JsonNode? n) => n switch
    {
        JsonArray a => a,
        JsonObject o => new JsonNode?[] { o },
        _ => Array.Empty<JsonNode?>(),
    };

    /// <summary>Um real do ficheiro; o que nao for numero vale zero.</summary>
    /// <summary>
    /// Um inteiro do ficheiro.  Os negativos aparecem ora com sinal ora como u32 (o `-6` do
    /// `DoorId` e' sempre 4294967290), por isso le'-se em `long` e corta-se para 32 bits.
    /// </summary>
    private static int I(JsonNode? n, int omissao = 0)
    {
        if (n is null)
            return omissao;
        try
        {
            var v = n.GetValue<JsonElement>();
            if (v.ValueKind != JsonValueKind.Number)
                return omissao;
            if (v.TryGetInt64(out long l))
                return unchecked((int)l);
            return v.TryGetDouble(out double d) ? unchecked((int)(long)d) : omissao;
        }
        catch
        {
            return omissao;
        }
    }

    /// <summary>`b2` antes de `b10`, que e' a ordem dos blocos e nao a alfabetica.</summary>
    internal sealed class Natural : IComparer<string>
    {
        public static readonly Natural Ordem = new();

        public int Compare(string? a, string? b)
        {
            a ??= ""; b ??= "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int i0 = i, j0 = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    long na = long.Parse(a[i0..i]), nb = long.Parse(b[j0..j]);
                    if (na != nb)
                        return na.CompareTo(nb);
                }
                else
                {
                    int c = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (c != 0)
                        return c;
                    i++; j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }
}
