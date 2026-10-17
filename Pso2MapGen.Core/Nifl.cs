using System.Text;

namespace Pso2MapGen.Core;

/// <summary>
/// O contentor NIFL/REL0 em que o PSO2 guarda os `.lps` e os `.lat`.
///
///     "NIFL" | niflLen unk0 rel0Off nof0Off niflSize nof0Size pad     (7 u32)
///     em rel0Off: "REL0" | rel0Size dataStart rel0Pad
///     em niflSize: "NOF0" | tamanho quantos inicio | uma lista de offsets
///
/// Os ponteiros sao relativos ao inicio do REL0 (a <see cref="Base"/>), e a NOF0 diz
/// quais dos dwords do ficheiro sao mesmo ponteiros — e' o que distingue um parametro
/// que e' texto de um que e' numero.  Porto directo de <c>tools/lps.py</c>.
/// </summary>
public sealed class Nifl
{
    public byte[] D { get; }
    public int Base { get; }
    public int Root { get; }
    private readonly HashSet<int> _ptrLocs = new();

    public Nifl(byte[] data)
    {
        D = data;
        if (data.Length < 4 || Encoding.ASCII.GetString(data, 0, 4) != "NIFL")
            throw new InvalidDataException("nao e' NIFL");
        int rel0Off = (int)U32(4 + 2 * 4);
        int niflSize = (int)U32(4 + 4 * 4);
        if (Encoding.ASCII.GetString(data, rel0Off, 4) != "REL0")
            throw new InvalidDataException($"sem REL0 em 0x{rel0Off:x}");
        int dataStart = (int)U32(rel0Off + 8);
        Base = rel0Off;
        Root = dataStart + Base;
        if (niflSize + 4 <= data.Length && Encoding.ASCII.GetString(data, niflSize, 4) == "NOF0")
        {
            int cnt = (int)U32(niflSize + 8);
            for (int i = 0; i < cnt; i++)
                _ptrLocs.Add((int)U32(niflSize + 0x10 + i * 4) + Base);
        }
    }

    /// <summary>
    /// Abre um NIFL de um ficheiro.  O construtor toma os <b>bytes</b>, e e' por ai' que entra
    /// quem ja' os tem — este atalho e' so' a leitura do disco.
    /// </summary>
    public static Nifl Load(string path) => new(File.ReadAllBytes(path));

    public uint U32(int off) => BitConverter.ToUInt32(D, off);
    public int I32(int off) => BitConverter.ToInt32(D, off);
    public float F32(int off) => BitConverter.ToSingle(D, off);
    public ushort U16(int off) => BitConverter.ToUInt16(D, off);

    /// <summary>O ponteiro em <paramref name="off"/>, ja' somado a' base; <c>null</c> se for zero.</summary>
    public int? Ptr(int off)
    {
        uint v = U32(off);
        return v == 0 ? null : (int)v + Base;
    }

    public bool IsPtr(int off) => _ptrLocs.Contains(off);

    public string? CStr(int? off)
    {
        if (off is null)
            return null;
        int e = Array.IndexOf(D, (byte)0, off.Value);
        if (e < 0)
            e = D.Length;
        return Encoding.ASCII.GetString(D, off.Value, e - off.Value);
    }
}
