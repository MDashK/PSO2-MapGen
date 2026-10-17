using System.Text;

namespace Pso2MapGen.Core;

/// <summary>Uma celula do `.lat`: o nome curto da peca, a rotacao, e o resto do registo de 0x24.</summary>
public sealed class LatCell
{
    public int I;
    public string Short = "";
    public int Rot;
    public int? Ice;
    /// <summary>O `+0x08`: a ALTURA da celula (FINDINGS 41.8).  Nao nula em 34% das celulas
    /// das areas com altura contra 0.6% nas outras, e 3089 dos 3156 valores nao nulos cabem
    /// dentro do `max_height` do proprio modelo.</summary>
    public int Altura;
    public int D0C, D10, D14, Type, Door, Last;
}

/// <summary>
/// Leitor do `.lat` (landform authored layout) — o mapa desenhado a' mao que as areas de
/// <c>unkB = 0</c> usam tal e qual, e que o modo rio usa como esqueleto.
/// Porto directo de <c>tools/lat.py</c>.
/// </summary>
public sealed class LatFile
{
    public const int CellSize = 0x24;

    public string FileName = "";
    public int A, B, W, H;
    public List<LatCell> Cells { get; } = new();
    public uint[] Info = Array.Empty<uint>();

    /// <summary>
    /// Le' o `.lat` de um ficheiro.  E' so' a leitura do disco mais a <see cref="Parse(byte[],
    /// bool, string)"/> — quem ja' tem os bytes chama directamente a outra.
    /// </summary>
    public static LatFile Load(string path, bool swap = false)
        => Parse(File.ReadAllBytes(path), swap, Path.GetFileName(path));

    /// <summary>
    /// Le' o `.lat` de um bloco de bytes.  O <paramref name="nome"/> e' so' para o
    /// <see cref="FileName"/>, que nada na leitura usa.
    /// </summary>
    public static LatFile Parse(byte[] dados, bool swap = false, string nome = "")
        => Parse(new Nifl(dados), swap, nome);

    /// <summary>Le' o `.lat` de um contentor NIFL ja' aberto.</summary>
    public static LatFile Parse(Nifl n, bool swap = false, string nome = "")
    {
        int r = n.Root;
        var m = new LatFile
        {
            FileName = nome,
            A = (int)n.U32(r + 0),
            B = (int)n.U32(r + 4),
        };
        // o `a` e o `b` vem trocados face ao que se espera: a largura e' o **segundo**
        (m.W, m.H) = swap ? (m.A, m.B) : (m.B, m.A);
        int cnt = m.W * m.H;
        int? infoP = n.Ptr(r + 0x0C);
        int? cellP = n.Ptr(r + 0x10);
        for (int i = 0; i < cnt && cellP is int cp; i++)
        {
            int e = cp + i * CellSize;
            int fim = e;
            while (fim < e + 4 && n.D[fim] != 0)
                fim++;
            ushort ice = n.U16(e + 0x0A);
            m.Cells.Add(new LatCell
            {
                I = i,
                Short = Encoding.ASCII.GetString(n.D, e, fim - e),
                Rot = n.I32(e + 4),
                Altura = n.U16(e + 0x08),
                Ice = ice == 0xFFFF ? null : ice,
                D0C = n.I32(e + 0x0C),
                D10 = n.I32(e + 0x10),
                D14 = n.I32(e + 0x14),
                Type = n.I32(e + 0x18),
                Door = n.I32(e + 0x1C),
                Last = n.I32(e + 0x20),
            });
        }
        m.Info = new uint[cnt];
        if (infoP is int ip)
            for (int i = 0; i < cnt; i++)
                m.Info[i] = n.U32(ip + i * 4);
        return m;
    }
}
