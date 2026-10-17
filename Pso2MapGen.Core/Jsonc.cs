using System.Globalization;
using System.Text;

namespace Pso2MapGen.Core;

/// <summary>
/// Um JSON com comentarios visto como <b>texto</b>: sabe onde e' que o valor de cada campo
/// comeca e acaba, e troca-o sem tocar em mais nada.
///
/// <para>E' o que permite <b>guardar por cima</b> de um `block.json` do Starlight.  Os ficheiros
/// reais nao sao JSON limpo: trazem comentarios `//` a explicar cada valor, blocos inteiros
/// comentados, virgulas a mais, indentacao a' mao com tabulacoes, e dezenas de campos que a
/// ferramenta nem le' (`DropTables`, `EnemyID`, `Entrances`, `Weather`, `ForecastSFX`, ...).
/// Reescrever o ficheiro a partir de um <c>JsonObject</c> deitava tudo isso fora.  Aqui
/// substitui-se <b>so' o pedaco de texto do valor</b>, e o resto do ficheiro fica byte a byte
/// como estava.</para>
///
/// <para>Os campos nomeiam-se por caminho, com `/` a separar: <c>ModelId</c>,
/// <c>InfoTable3/Unk3</c>, <c>TeleportData/0/DoorId</c>.</para>
/// </summary>
public sealed class Jsonc
{
    private string _texto;
    private readonly bool _bom;
    private Dictionary<string, (int Ini, int Fim)> _sitios = new(StringComparer.Ordinal);
    private Dictionary<string, int> _tamanhos = new(StringComparer.Ordinal);
    private int _p;

    private Jsonc(string texto, bool bom)
    {
        _texto = texto;
        _bom = bom;
        Varre();
    }

    public static Jsonc De(string texto) => new(texto, false);

    /// <summary>Le' o ficheiro e guarda se ele tinha BOM, para o `Grava` o repor.</summary>
    public static Jsonc Le(string ficheiro)
    {
        byte[] bs = File.ReadAllBytes(ficheiro);
        bool bom = bs.Length >= 3 && bs[0] == 0xEF && bs[1] == 0xBB && bs[2] == 0xBF;
        return new Jsonc(new UTF8Encoding(false).GetString(bs, bom ? 3 : 0,
                                                           bs.Length - (bom ? 3 : 0)), bom);
    }

    public void Grava(string ficheiro)
        => File.WriteAllText(ficheiro, _texto, new UTF8Encoding(_bom));

    public string Texto => _texto;

    /// <summary>A quebra de linha que o ficheiro usa.</summary>
    public string Quebra => _texto.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    public bool Tem(string caminho) => _sitios.ContainsKey(caminho);

    /// <summary>Quantos elementos tem esta lista; -1 se o caminho nao for uma lista.</summary>
    public int Quantos(string caminho) => _tamanhos.TryGetValue(caminho, out int n) ? n : -1;

    /// <summary>O texto do valor tal e qual ele esta' no ficheiro, ou null se nao existir.</summary>
    public string? Valor(string caminho)
        => _sitios.TryGetValue(caminho, out var s) ? _texto[s.Ini..s.Fim] : null;

    /// <summary>
    /// O valor como inteiro de 32 bits, aceitando as duas formas com que os `block.json` reais
    /// escrevem um numero negativo: `-6` e `4294967290`.  Null se nao for um numero.
    /// </summary>
    public int? Inteiro(string caminho)
    {
        string? v = Valor(caminho);
        if (v is null)
            return null;
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            return i;
        if (uint.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint u))
            return unchecked((int)u);
        return null;
    }

    /// <summary>Os brancos da linha onde este valor comeca — para indentar o que se escreve.</summary>
    public string Indentacao(string caminho)
    {
        if (!_sitios.TryGetValue(caminho, out var s))
            return "";
        int linha = _texto.LastIndexOf('\n', Math.Max(0, s.Ini - 1)) + 1;
        int i = linha;
        while (i < s.Ini && (_texto[i] == ' ' || _texto[i] == '\t'))
            i++;
        return _texto[linha..i];
    }

    /// <summary>
    /// Troca o valor deste campo pelo literal dado.  Devolve false quando o campo nao existe no
    /// ficheiro — e nesse caso nada e' escrito, que e' melhor do que inventar estrutura num
    /// ficheiro feito a' mao.
    /// </summary>
    public bool Poe(string caminho, string literal)
    {
        if (!_sitios.TryGetValue(caminho, out var s))
            return false;
        _texto = string.Concat(_texto.AsSpan(0, s.Ini), literal, _texto.AsSpan(s.Fim));
        // as posicoes a seguir a esta andaram todas; varrer outra vez custa menos do que
        // corrigi-las uma a uma, e estes ficheiros tem poucos KB
        Varre();
        return true;
    }

    // ---------------------------------------------------------------- o varrimento

    private void Varre()
    {
        _sitios = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        _tamanhos = new Dictionary<string, int>(StringComparer.Ordinal);
        _p = 0;
        try
        {
            LeValor("");
        }
        catch (FormatException)
        {
            // ficheiro torto a meio: fica-se com os campos que se apanharam ate' ali, e quem
            // pedir um que nao esteja la' recebe o "nao existe" em vez de uma excepcao
        }
    }

    private void LeValor(string caminho)
    {
        Branco();
        if (_p >= _texto.Length)
            throw new FormatException("the file ends where a value was expected");
        int ini = _p;
        char c = _texto[_p];
        if (c == '{')
            LeObjecto(caminho);
        else if (c == '[')
            LeLista(caminho);
        else if (c == '"')
            LeTexto();
        else
            LeSolto();
        _sitios[caminho] = (ini, _p);
    }

    private void LeObjecto(string caminho)
    {
        _p++;                                          // o `{`
        while (true)
        {
            Branco();
            if (_p >= _texto.Length)
                throw new FormatException("an object is never closed");
            if (_texto[_p] == '}')
            {
                _p++;
                return;
            }
            if (_texto[_p] == ',')
            {
                _p++;                                  // virgulas a mais: os ficheiros tem-nas
                continue;
            }
            if (_texto[_p] != '"')
                throw new FormatException("a key was expected");
            int ini = _p;
            LeTexto();
            string chave = SemEscapes(_texto[(ini + 1)..(_p - 1)]);
            Branco();
            if (_p >= _texto.Length || _texto[_p] != ':')
                throw new FormatException("a `:` was expected");
            _p++;
            LeValor(caminho.Length == 0 ? chave : caminho + "/" + chave);
        }
    }

    private void LeLista(string caminho)
    {
        _p++;                                          // o `[`
        int n = 0;
        while (true)
        {
            Branco();
            if (_p >= _texto.Length)
                throw new FormatException("a list is never closed");
            if (_texto[_p] == ']')
            {
                _p++;
                _tamanhos[caminho] = n;
                return;
            }
            if (_texto[_p] == ',')
            {
                _p++;
                continue;
            }
            string ind = n.ToString(CultureInfo.InvariantCulture);
            LeValor(caminho.Length == 0 ? ind : caminho + "/" + ind);
            n++;
        }
    }

    private void LeTexto()
    {
        _p++;                                          // a aspa de abrir
        while (_p < _texto.Length)
        {
            char c = _texto[_p];
            if (c == '\\')
            {
                _p += 2;
                continue;
            }
            _p++;
            if (c == '"')
                return;
        }
        throw new FormatException("a string is never closed");
    }

    /// <summary>Um numero, `true`, `false` ou `null`: vai ate' ao que o possa fechar.</summary>
    private void LeSolto()
    {
        int ini = _p;
        while (_p < _texto.Length)
        {
            char c = _texto[_p];
            if (c == ',' || c == '}' || c == ']' || char.IsWhiteSpace(c))
                break;
            if (c == '/' && _p + 1 < _texto.Length && (_texto[_p + 1] == '/' || _texto[_p + 1] == '*'))
                break;                                 // um `// comentario` colado ao valor
            _p++;
        }
        if (_p == ini)
            throw new FormatException("an empty value");
    }

    /// <summary>Brancos, BOM e comentarios `//` e `/* */`.</summary>
    private void Branco()
    {
        while (_p < _texto.Length)
        {
            char c = _texto[_p];
            if (c == '﻿' || char.IsWhiteSpace(c))
            {
                _p++;
                continue;
            }
            if (c == '/' && _p + 1 < _texto.Length)
            {
                if (_texto[_p + 1] == '/')
                {
                    while (_p < _texto.Length && _texto[_p] != '\n')
                        _p++;
                    continue;
                }
                if (_texto[_p + 1] == '*')
                {
                    int fim = _texto.IndexOf("*/", _p + 2, StringComparison.Ordinal);
                    _p = fim < 0 ? _texto.Length : fim + 2;
                    continue;
                }
            }
            return;
        }
    }

    private static string SemEscapes(string s)
    {
        if (!s.Contains('\\', StringComparison.Ordinal))
            return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length)
            {
                sb.Append(s[i]);
                continue;
            }
            char c = s[++i];
            sb.Append(c switch
            {
                'n' => '\n', 'r' => '\r', 't' => '\t', 'b' => '\b', 'f' => '\f',
                'u' when i + 4 < s.Length => (char)Convert.ToInt32(s.Substring(i + 1, 4), 16),
                _ => c,
            });
            if (c == 'u' && i + 4 < s.Length)
                i += 4;
        }
        return sb.ToString();
    }
}
