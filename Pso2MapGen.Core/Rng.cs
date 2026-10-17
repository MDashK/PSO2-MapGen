namespace Pso2MapGen.Core;

/// <summary>
/// MT19937 classico, semeado com <c>init_genrand(seed)</c>.
///
/// O cliente guarda o estado em duplo (1248 palavras) mas a sequencia que sai e' a do
/// MT19937 normal, por isso aqui esta' a implementacao simples.  Porto directo de
/// <c>tools/pso2rng.py</c>.
/// </summary>
public sealed class Mt19937
{
    private const int N = 624;
    private const int M = 397;
    private const uint MatrixA = 0x9908B0DFu;
    private const uint Upper = 0x80000000u;
    private const uint Lower = 0x7FFFFFFFu;

    private readonly uint[] _mt = new uint[N];
    private int _mti;

    /// <summary>Quantas palavras ja' foram consumidas — e' o custo que os conferidores medem.</summary>
    public long Draws { get; private set; }

    public Mt19937(uint seed)
    {
        _mt[0] = seed;
        for (int i = 1; i < N; i++)
            _mt[i] = unchecked(1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i);
        _mti = N;
        Draws = 0;
    }

    private void Twist()
    {
        for (int i = 0; i < N; i++)
        {
            uint y = (_mt[i] & Upper) | (_mt[(i + 1) % N] & Lower);
            _mt[i] = _mt[(i + M) % N] ^ (y >> 1) ^ ((y & 1) != 0 ? MatrixA : 0u);
        }
        _mti = 0;
    }

    public uint NextU32()
    {
        if (_mti >= N)
            Twist();
        uint y = _mt[_mti];
        _mti++;
        Draws++;
        y ^= y >> 11;
        y ^= (y << 7) & 0x9D2C5680u;
        y ^= (y << 15) & 0xEFC60000u;
        y ^= y >> 18;
        return y;
    }
}

/// <summary>Os dois sorteios que o gerador de mapas usa, e o baralhar que assenta no segundo.</summary>
public static class Rng
{
    private const uint M32 = 0xFFFFFFFFu;

    /// <summary>
    /// <c>(temper(y) / d) % n</c> — a variante de divisao e modulo, sem rejeicao
    /// (<c>0x0062A830</c>).  E' a que escolhe a porta aberta, a candidata da entrada
    /// (embutida no <c>0x00643820</c>) e a despromocao de big chunk (<c>0x0062F380</c>).
    /// Espelha o <c>0x005ED0F6..0x005ED545</c> a' letra.
    /// </summary>
    public static int UniformA(Mt19937 mt, int n)
    {
        if (n <= 1)
            return 0;
        ulong limit = 0x80000000UL;
        ulong un = (ulong)n;
        ulong q = limit / un;
        ulong r = limit % un;
        ulong d;
        if (r == 0)
            d = q;
        else if (un > (q >> 5))
            d = 2;
        else
        {
            d = 2;
            while (true)
            {
                limit >>= 1;
                d *= 2;
                if (un > ((limit / un) >> 5))
                    break;
            }
        }
        return (int)((mt.NextU32() / d) % un);
    }

    /// <summary>
    /// A variante com rejeicao, intervalo <b>fechado</b> <c>[lo, hi]</c> (<c>0x004C23A0</c>).
    /// E' a que todos os <c>std::random_shuffle</c> do gerador usam.
    /// </summary>
    public static long UniformB(Mt19937 mt, long lo, long hi)
    {
        uint rng = (uint)((hi - lo) & M32);
        if (rng == 0)
            return lo;
        if (rng == M32)
            return (long)((mt.NextU32() + (ulong)lo) & M32);
        ulong n = (ulong)rng + 1;
        ulong scale = M32 / n;
        if (rng == M32 % n)
            scale += 1;
        while (true)
        {
            ulong v = mt.NextU32() / scale;
            if (v <= rng)
                return (long)v + lo;
        }
    }

    /// <summary>
    /// <c>std::random_shuffle</c> sobre <c>seq[start..end)</c>, exactamente como o cliente:
    /// para <c>i = start+1 .. end-1</c>, trocar <c>seq[i]</c> com <c>seq[uniform_b(0, i-start)]</c>.
    /// Custa <c>end-start-1</c> palavras.
    /// </summary>
    public static void Shuffle<T>(Mt19937 mt, IList<T> seq, int start = 0, int end = -1)
    {
        if (end < 0)
            end = seq.Count;
        for (int i = start + 1; i < end; i++)
        {
            int j = start + (int)UniformB(mt, 0, i - start);
            (seq[i], seq[j]) = (seq[j], seq[i]);
        }
    }
}
