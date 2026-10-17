using System.Text;

namespace Pso2MapGen.Core;

/// <summary>Os 13 i32 do <c>ZoneSettings</c>.</summary>
public sealed class ZoneSettings
{
    public int WorldId, UnkA, ZoneIndex, ModelId, ZoneType, Seed, Arguments;
    public int SizeX, SizeY, UnkB, AreaIndex, SubArea, UnkC;
}

/// <summary>Um warp do <c>TeleportData</c>.</summary>
public sealed class Warp
{
    public int Unka, ZoneIndex, DoorId, DstZone, BackDoorId;
}

/// <summary>
/// O <c>ZoneSettingsExtra</c>, um registo de <b>0xF0 bytes</b>.  Da' geracao so' interessam o
/// <c>UnkP</c>/<c>UnkW</c> (os dois designs de big chunk) e o <c>UnkN</c> (cujo bit 5 e' o
/// <c>gen-&gt;0xA0</c>); os outros ficam guardados para a exportacao poder escrever um
/// `block.json` completo — e agora <b>todos</b> ficam, incluindo os que nem o PacketMan nem o
/// Starlight escrevem (FINDINGS 5.4).
/// </summary>
public sealed class ZoneExtra
{
    public int ZoneIndex, ModelId, Arguments;
    /// <summary>
    /// <c>+0x14</c>.  E' um <b>float</b> (0, 200, 300, 400, 500, 800), mas guarda-se e escreve-se
    /// como o <b>dword em bruto</b>, que e' a convencao dos ficheiros: nos `block.json` do
    /// servidor aparece `1133903872`, os bits de `300.0f`, e nao `300` (FINDINGS 5.4).
    /// </summary>
    public int UnkAC;
    public int UnkO;
    public int UnkP, UnkQ, UnkW, UnkY;
    public int UnkK, UnkL, UnkAA, UnkAB, UnkR, UnkS, UnkT, UnkM, UnkU, UnkN;

    // --- os dwords que o registo traz e que nem o PacketMan nem o Starlight escrevem.  Levam o
    // nome do **deslocamento** deles dentro do registo, que e' o que serve a quem tiver de
    // corrigir os dois; o que cada um faz nao se sabe.  Medidos nas 1641 zonas dos 583 packets.
    /// <summary><c>+0x04</c>, a seguir ao <c>ZoneIndex</c>: 1..21 em 41 zonas, todas de mapa fixo.</summary>
    public int Raw04 = -1;
    /// <summary><c>+0x10</c>, a seguir ao <c>Arguments</c>: <b>12</b> em 18 zonas (3140, 820, 821).</summary>
    public int Raw10;
    /// <summary><c>+0x98</c>: -1 em todas as 1641.</summary>
    public int Raw98 = -1;
    /// <summary><c>+0xD4</c>: 1, 6, 7, 8, 12 nos modelos 510/511/212 (33 zonas).</summary>
    public int RawD4 = -1;
    /// <summary><c>+0xD8</c>: 0 nas mesmas 33 zonas.</summary>
    public int RawD8 = -1;
    /// <summary><c>+0xDC</c>: 0, 4, 6..11, 13, 14 — so' no Sanctum (530), 122 zonas.</summary>
    public int RawDC = -1;
    /// <summary>
    /// <c>+0xE0</c>, logo antes do <c>UnkN</c>.  E' um <b>float</b> (30, 50, 60, 80, 100), so' no
    /// Sanctum, e como o <c>UnkAC</c> guarda-se pelo dword em bruto.
    /// </summary>
    public int RawE0;

    /// <summary>
    /// Os dwords do registo que estao a <b>zero</b> nas 1641 zonas medidas — os 6 de enchimento
    /// do <c>+0x1C</c>, os 68 bytes do <c>+0x44</c>, os nove que seguem cada `Unk`, e os dois do
    /// fim.  Guarda-se so' o que <b>fuja</b> do zero, pelo deslocamento: assim a exportacao nao
    /// leva 34 linhas de zeros e tambem nao perde nada se algum dia um deles falar.
    /// </summary>
    public List<(int Off, int Valor)> Sobras = new();

    /// <summary>O <c>UnkAC</c> lido como o float que ele e'.</summary>
    public float UnkACFloat => BitConverter.Int32BitsToSingle(UnkAC);
}

/// <summary>Um campo de uma InfoTable: o id e os dez valores.</summary>
public sealed class InfoField
{
    public int Id;
    public int[] Values = new int[10];
}

/// <summary>Uma linha da <c>InfoTable1</c> ou da <c>InfoTable2</c>.</summary>
public sealed class InfoTableRow
{
    public int ZoneIndex;
    public List<InfoField> Fields = new();
}

/// <summary>Uma linha da <c>InfoTable3</c>: o <c>Unk[2]</c> e o <c>Unk[3]</c> decidem o mapa.</summary>
public sealed class InfoTable3Row
{
    public int Z1, Z2;
    public int[] Unk = new int[4];
    /// <summary>
    /// O quinto dword da linha, que o leitor antigo saltava como enchimento.  Na memoria do
    /// cliente e' o <c>+0x1C</c> da linha, e <b>nao e' zero</b>: vale <c>1</c> em 15 das 1151
    /// linhas do corpus, todas do modelo <b>720 (Kuron)</b> com mapa aleatorio, nas tres zonas
    /// da quest `40. SUPRESS KODOTTA IDIHTA`.  E' o dword que ficou marcado para investigar.
    /// </summary>
    public int Unk5;
    public List<InfoField> Fields = new();
}

/// <summary>
/// Leitor do packet PSO2 <c>0x03-0x24</c> (WorldInit), a partir de um `.bin` em bruto.
/// Porto de <c>tools/pkt0324.py</c>: le' so' ate' a `InfoTable3`/`ZoneExtra`, que e' o que a
/// geracao precisa, e guarda o offset onde ficou para quem quiser continuar.
/// </summary>
public sealed class Packet0324
{
    private const uint Xor = 0x7542, Sub = 0x5E;

    public string FileName = "";
    public int Size;
    public (byte Cmd, byte Sub) Cmd;
    public string WorldName = "";
    public ZoneSettings? CurrentZone;
    public List<ZoneSettings> Zones { get; } = new();
    public List<Warp> Warps { get; } = new();
    public List<(int ZoneIndex, int Side)> ZoneUnknown1 { get; } = new();
    public List<InfoTableRow> InfoTable1 { get; } = new();
    public List<InfoTableRow> InfoTable2 { get; } = new();
    public List<InfoTable3Row> InfoTable3 { get; } = new();
    public List<ZoneExtra> ZoneExtra { get; } = new();

    private sealed class R
    {
        private readonly byte[] _d;
        public int P;
        public R(byte[] d) { _d = d; P = 0; }
        public uint U32() { uint v = BitConverter.ToUInt32(_d, P); P += 4; return v; }
        public int I32() { int v = BitConverter.ToInt32(_d, P); P += 4; return v; }
        public float F32() { float v = BitConverter.ToSingle(_d, P); P += 4; return v; }
        public byte U8() => _d[P++];
        public byte[] Raw(int n) { var v = _d[P..(P + n)]; P += n; return v; }
        /// <summary>As contagens viajam ofuscadas: <c>(v ^ 0x7542) - 0x5E</c>.</summary>
        public uint Magic() => (U32() ^ Xor) - Sub;
        public string Ascii()
        {
            uint m = Magic();
            if (m == 0)
                return "";
            int n = (int)m - 1;
            string s = Encoding.ASCII.GetString(Raw(n));
            Raw(4 - (n & 3));
            return s;
        }
        public void ObjHdr() { U32(); U32(); U32(); }
        public ZoneSettings Zs() => new()
        {
            WorldId = I32(), UnkA = I32(), ZoneIndex = I32(), ModelId = I32(),
            ZoneType = I32(), Seed = I32(), Arguments = I32(), SizeX = I32(),
            SizeY = I32(), UnkB = I32(), AreaIndex = I32(), SubArea = I32(), UnkC = I32(),
        };
    }

    public static Packet0324 Load(string path) => Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    public static Packet0324 Parse(byte[] d, string nome = "")
    {
        var r = new R(d);
        var o = new Packet0324 { FileName = nome, Size = (int)r.U32() };
        byte cmd = r.U8(), sub = r.U8();
        r.U8(); r.U8();
        o.Cmd = (cmd, sub);
        r.ObjHdr();                       // zoneHeader
        r.ObjHdr();                       // clientHeader
        o.CurrentZone = r.Zs();
        r.ObjHdr();                       // world
        r.ObjHdr();                       // quest
        r.ObjHdr();                       // party
        o.WorldName = r.Ascii();

        uint n = r.Magic();
        for (uint i = 0; i < n; i++)
            o.Zones.Add(r.Zs());

        n = r.Magic();
        for (uint i = 0; i < n; i++)
            o.Warps.Add(new Warp
            {
                Unka = r.I32(), ZoneIndex = r.I32(), DoorId = r.I32(),
                DstZone = r.I32(), BackDoorId = r.I32(),
            });

        n = r.Magic();
        for (uint i = 0; i < n; i++)
            o.ZoneUnknown1.Add((r.I32(), r.I32()));

        n = r.Magic();
        for (uint i = 0; i < n; i++) o.InfoTable1.Add(LeTabela(r, 3));
        n = r.Magic();
        for (uint i = 0; i < n; i++) o.InfoTable2.Add(LeTabela(r, 5));

        n = r.Magic();
        for (uint i = 0; i < n; i++)
        {
            var t = new InfoTable3Row { Z1 = r.I32(), Z2 = r.I32() };
            for (int k = 0; k < 4; k++)
                t.Unk[k] = r.I32();
            t.Unk5 = r.I32();              // o `+0x1C` da linha, nao e' enchimento
            for (int f = 0; f < 3; f++)
                t.Fields.Add(LeCampo(r));
            o.InfoTable3.Add(t);
        }

        n = r.Magic();
        for (uint i = 0; i < n; i++)
        {
            // o registo tem 0xF0 bytes e le-se de ponta a ponta; o que nao tem nome vai para as
            // `Sobras` quando nao estiver a zero, para nada se perder na exportacao
            int inicio = r.P;
            var e = new ZoneExtra { ZoneIndex = r.I32() };       // +0x00
            e.Raw04 = r.I32();                                   // +0x04
            e.ModelId = r.I32();                                 // +0x08
            e.Arguments = r.I32();                               // +0x0C
            e.Raw10 = r.I32();                                   // +0x10
            e.UnkAC = r.I32();                                   // +0x14  (um float, em bruto)
            e.UnkO = r.I32();                                    // +0x18
            for (int k = 0; k < 6; k++) Sobra(e, r, inicio);      // +0x1C .. +0x30
            e.UnkP = r.I32(); e.UnkQ = r.I32();                   // +0x34, +0x38
            e.UnkW = r.I32(); e.UnkY = r.I32();                   // +0x3C, +0x40
            for (int k = 0; k < 17; k++) Sobra(e, r, inicio);     // +0x44 .. +0x84  (os 68 bytes)
            e.UnkK = r.I32(); Sobra(e, r, inicio);                // +0x88, +0x8C
            e.UnkL = r.I32(); Sobra(e, r, inicio);                // +0x90, +0x94
            e.Raw98 = r.I32(); Sobra(e, r, inicio);               // +0x98, +0x9C
            e.UnkAA = r.I32(); Sobra(e, r, inicio);               // +0xA0, +0xA4
            e.UnkAB = r.I32(); Sobra(e, r, inicio);               // +0xA8, +0xAC
            e.UnkR = r.I32(); Sobra(e, r, inicio);                // +0xB0, +0xB4
            e.UnkS = r.I32(); Sobra(e, r, inicio);                // +0xB8, +0xBC
            e.UnkT = r.I32(); Sobra(e, r, inicio);                // +0xC0, +0xC4
            e.UnkM = r.I32(); Sobra(e, r, inicio);                // +0xC8, +0xCC
            e.UnkU = r.I32();                                     // +0xD0
            e.RawD4 = r.I32(); e.RawD8 = r.I32(); e.RawDC = r.I32();  // +0xD4, +0xD8, +0xDC
            e.RawE0 = r.I32();                                    // +0xE0  (um float, em bruto)
            e.UnkN = r.I32();                                     // +0xE4
            Sobra(e, r, inicio);                                  // +0xE8  (um float, sempre 0)
            Sobra(e, r, inicio);                                  // +0xEC
            o.ZoneExtra.Add(e);
        }
        return o;
    }

    /// <summary>Le' um dword sem nome: guarda-o so' se ele nao estiver a zero.</summary>
    private static void Sobra(ZoneExtra e, R r, int inicio)
    {
        int off = r.P - inicio;
        int v = r.I32();
        if (v != 0)
            e.Sobras.Add((off, v));
    }

    private static InfoTableRow LeTabela(R r, int nfields)
    {
        var t = new InfoTableRow { ZoneIndex = r.I32() };
        for (int f = 0; f < nfields; f++)
            t.Fields.Add(LeCampo(r));
        return t;
    }

    private static InfoField LeCampo(R r)
    {
        var c = new InfoField { Id = r.I32() };
        for (int k = 0; k < 10; k++)
            c.Values[k] = r.I32();
        return c;
    }

    /// <summary>
    /// Os lados de entrada e de saida da zona Z, lidos do packet (FINDINGS 37.8):
    /// <list type="bullet">
    /// <item>mapA = os lados do <c>zoneUnknown1</c> desta zona mais o <c>backDoorId</c> de cada
    /// warp que <b>chega</b> aqui;</item>
    /// <item>mapB = o <c>doorId</c> de cada warp que <b>parte</b> daqui.</item>
    /// </list>
    /// O <c>doorId</c> -6 nao e' lado nenhum: aparece em 84 zonas dos 583 packets e marca uma
    /// saida que nao e' uma porta do gerador.
    ///
    /// <b>Um lado so' pode ter um tipo.</b>  O <c>0x0063F970</c> percorre os quatro lados e
    /// pergunta a cada um o que e' (1 ou 3 -&gt; mapA, 2 -&gt; mapB), portanto nenhum lado pode
    /// estar nos dois.  Nos packets dos `.pak` ha 21 zonas em que o <c>doorId</c> coincide com um
    /// lado que o <c>zoneUnknown1</c> ja' reclamou — sempre com <c>unka = 0</c>.  Nesse caso fica
    /// o <b>mapA</b>, e a saida sai do <c>gen-&gt;0xA0</c> (38.16).
    /// </summary>
    public (List<int> A, List<int> B) Lados(int z)
    {
        var a = new SortedSet<int>();
        foreach (var (zi, s) in ZoneUnknown1)
            if (zi == z && s >= 0 && s <= 3)
                a.Add(s);
        foreach (var w in Warps)
            if (w.DstZone == z && w.BackDoorId >= 0 && w.BackDoorId <= 3)
                a.Add(w.BackDoorId);
        var b = new SortedSet<int>();
        foreach (var w in Warps)
            if (w.ZoneIndex == z && w.DoorId >= 0 && w.DoorId <= 3 && !a.Contains(w.DoorId))
                b.Add(w.DoorId);
        return (a.ToList(), b.ToList());
    }

    /// <summary>Os campos de geracao de cada zona do packet, ja' cruzados com as tabelas.</summary>
    public List<ZoneInput> Zonas()
    {
        var extra = new Dictionary<int, ZoneExtra>();
        foreach (var e in ZoneExtra)
            extra.TryAdd(e.ZoneIndex, e);
        var it3 = new Dictionary<int, InfoTable3Row>();
        foreach (var t in InfoTable3)
            it3.TryAdd(t.Z1, t);
        var zu = new Dictionary<int, int>();
        foreach (var (zi, _) in ZoneUnknown1)
            zu[zi] = zu.GetValueOrDefault(zi) + 1;

        var it1 = new Dictionary<int, InfoTableRow>();
        foreach (var t in InfoTable1)
            it1.TryAdd(t.ZoneIndex, t);
        var it2 = new Dictionary<int, InfoTableRow>();
        foreach (var t in InfoTable2)
            it2.TryAdd(t.ZoneIndex, t);

        // o modelo de cada zona: e' a chave do `paramsC`/`paramsD` do `.lps` que da' o nome
        // das pecas de entrada e de saida (41.17)
        var modeloPorZona = new Dictionary<int, int>();
        // e o tamanho, para se saber se a zona de destino e' um mapa FIXO (55.8)
        var tamanhoPorZona = new Dictionary<int, (int X, int Y)>();
        foreach (var z in Zones)
        {
            modeloPorZona[z.ZoneIndex] = z.ModelId;
            tamanhoPorZona[z.ZoneIndex] = (z.SizeX, z.SizeY);
        }

        var outp = new List<ZoneInput>();
        int ordem = 0;   // so as zonas com indice >= 0 recebem bloco
        foreach (var z in Zones)
        {
            extra.TryGetValue(z.ZoneIndex, out var e);
            it3.TryGetValue(z.ZoneIndex, out var t);
            var (la, lb) = Lados(z.ZoneIndex);
            var (mOrg, mDst) = MapGen.ModelosVizinhos(Warps, modeloPorZona, z.ZoneIndex);
            bool destinoFixo = MapGen.DestinoEhMapaFixo(Warps, tamanhoPorZona, z.ZoneIndex);
            int unkN = e?.UnkN ?? 0;
            outp.Add(new ZoneInput
            {
                ZoneIndex = z.ZoneIndex,
                Model = z.ModelId,
                Seed = z.Seed,
                Arguments = z.Arguments,
                SizeX = z.SizeX,
                SizeY = z.SizeY,
                UnkB = z.UnkB,
                UnkP = e?.UnkP ?? 0,
                UnkW = e?.UnkW ?? 0,
                UnkQ = e?.UnkQ ?? 0,
                UnkY = e?.UnkY ?? 0,
                UnkN = unkN,
                // gen->0xA0 e' o **bit 5 do UnkN** do ZoneSettingsExtra: bate em 81 de 81
                // geracoes medidas (FINDINGS 38.8).
                A0 = (unkN >> 5) & 1,
                // o `UnkO` traz o gen->0x94 no byte 0, e o `Unk5` da InfoTable3 o gen->0xA4
                UnkO = e?.UnkO ?? 0,
                TemUnkO = e is not null,
                Unk5 = t?.Unk5 ?? 0,
                Unk3 = t?.Unk[2] ?? 0,
                Unk4 = t?.Unk[3] ?? 0,
                NA = Math.Max(1, zu.GetValueOrDefault(z.ZoneIndex)),
                LadosA = la.Count > 0 ? la : null,
                LadosB = lb,
                ModeloDeOndeVem = mOrg,
                ModeloParaOndeVai = mDst,
                DestinoEhMapaFixo = destinoFixo,
                Settings = z,
                Extra = e,
                It1 = it1.GetValueOrDefault(z.ZoneIndex),
                It2 = it2.GetValueOrDefault(z.ZoneIndex),
                It3 = t,
                Warps = Warps.Where(w => w.ZoneIndex == z.ZoneIndex).ToList(),
                Zu1 = ZoneUnknown1.Select(p => (p.ZoneIndex, p.Side)).ToList(),
                OrdemBloco = z.ZoneIndex >= 0 ? ordem++ : -1,
            });
        }
        return outp;
    }
}
