namespace Pso2MapGen.Core;

/// <summary>Um parametro do `.lps`: ou e' texto, ou e' um dword que se le como int e como float.</summary>
public readonly struct LpsParam
{
    public string? Text { get; init; }
    public int I { get; init; }
    public float F { get; init; }
    public bool IsText => Text is not null;
}

/// <summary>Um registo de peca no `.lps`, ainda por rodar.</summary>
public sealed class LpsChunk
{
    public int Index;
    public string Short = "";
    public string Full = "";
    public int MaxIce, MaxOb, Unk8, Unk10;
    public int Group, Cols, Rows;
    public uint[] Info = Array.Empty<uint>();
    public uint[] Door = Array.Empty<uint>();
}

/// <summary>
/// Leitor do `.lps` (landform piece set), o catalogo de pecas de uma area.
/// Porto directo de <c>tools/lps.py</c>, mas a ler o binario em vez do JSON intermedio.
/// </summary>
public sealed class LpsFile
{
    public string FileName = "";
    public List<LpsChunk> Chunks { get; } = new();
    public Dictionary<string, LpsParam> ParamsA { get; } = new();
    public Dictionary<string, LpsParam> ParamsB { get; } = new();
    public Dictionary<string, LpsParam> ParamsC { get; } = new();
    public Dictionary<string, LpsParam> ParamsD { get; } = new();

    /// <summary>
    /// Le' o `.lps` de um ficheiro.  E' so' a leitura do disco mais a <see cref="Parse(byte[],
    /// string)"/> — quem ja' tem os bytes (de um ICE aberto em memoria, de um pacote, de uma
    /// captura) chama directamente a outra e nao passa por aqui.
    /// </summary>
    public static LpsFile Load(string path)
        => Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    /// <summary>
    /// Le' o `.lps` de um bloco de bytes.  O <paramref name="nome"/> e' so' para o
    /// <see cref="FileName"/>, que nada na leitura usa.
    /// </summary>
    public static LpsFile Parse(byte[] dados, string nome = "")
        => Parse(new Nifl(dados), nome);

    /// <summary>
    /// Le' o `.lps` de um contentor NIFL ja' aberto — serve para quem queira abrir o NIFL uma
    /// vez e ler dele mais do que uma coisa.
    /// </summary>
    public static LpsFile Parse(Nifl n, string nome = "")
    {
        int r = n.Root;
        var o = new LpsFile { FileName = nome };

        // 0x0C..0x18 sao as quatro contagens de parametros, 0x1C..0x28 os quatro ponteiros
        var contagens = new[] { n.U32(r + 0x0C), n.U32(r + 0x10), n.U32(r + 0x14), n.U32(r + 0x18) };
        var ponteiros = new[] { n.Ptr(r + 0x1C), n.Ptr(r + 0x20), n.Ptr(r + 0x24), n.Ptr(r + 0x28) };
        var destinos = new[] { o.ParamsA, o.ParamsB, o.ParamsC, o.ParamsD };
        for (int k = 0; k < 4; k++)
        {
            if (ponteiros[k] is not int p)
                continue;
            for (int i = 0; i < contagens[k]; i++)
            {
                int e = p + i * 8;
                // a chave do parametro; o `nome` do metodo e' o do ficheiro
                string chave = n.CStr(n.Ptr(e)) ?? "";
                destinos[k][chave] = n.IsPtr(e + 4)
                    ? new LpsParam { Text = n.CStr(n.Ptr(e + 4)) }
                    : new LpsParam { I = n.I32(e + 4), F = n.F32(e + 4) };
            }
        }

        int chunkCnt = (int)n.U32(r + 0x2C);
        int? chunkPtr = n.Ptr(r + 0x30);
        for (int i = 0; i < chunkCnt && chunkPtr is int cp; i++)
        {
            int e = cp + i * 0x24;
            var c = new LpsChunk
            {
                Index = i,
                Short = n.CStr(n.Ptr(e)) ?? "",
                MaxIce = n.U16(e + 4),
                MaxOb = n.U16(e + 6),
                Unk8 = n.U16(e + 8),
                Unk10 = n.U16(e + 10),
                Full = n.CStr(n.Ptr(e + 0x0C)) ?? "",
                Group = n.I32(e + 0x10),
                Cols = n.I32(e + 0x14),
                Rows = n.I32(e + 0x18),
            };
            int cells = c.Cols * c.Rows;
            if (n.Ptr(e + 0x1C) is int p1)
            {
                c.Info = new uint[cells];
                for (int k = 0; k < cells; k++)
                    c.Info[k] = n.U32(p1 + k * 4);
            }
            if (n.Ptr(e + 0x20) is int p2)
            {
                c.Door = new uint[cells];
                for (int k = 0; k < cells; k++)
                    c.Door[k] = n.U32(p2 + k * 4);
            }
            o.Chunks.Add(c);
        }
        return o;
    }
}
