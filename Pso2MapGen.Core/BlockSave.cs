using System.Globalization;
using System.Text;

namespace Pso2MapGen.Core;

/// <summary>Um campo que a gravacao mexeu — ou que quis mexer e nao teve onde.</summary>
public sealed class Alteracao
{
    /// <summary>O caminho do campo, como o `Jsonc` o nomeia (`InfoTable3/Unk3`).</summary>
    public string Campo = "";
    public string Antes = "";
    public string Depois = "";
    /// <summary>False quando o ficheiro nao tem esse campo e por isso nada foi escrito.</summary>
    public bool Escrita = true;

    public override string ToString()
        => Escrita ? $"{Campo}: {Antes} -> {Depois}"
                   : $"{Campo} = {Depois}  (the file has no such field)";
}

/// <summary>
/// Guarda por cima de um `block.json` que ja' existe, em vez de escrever um de raiz.
///
/// <para>E' o caminho que a ferramenta usa quando o mapa <b>veio de `block.json`</b>.  A
/// diferenca para o <see cref="BlockJson.Escreve"/> e' toda: aquele monta um ficheiro novo a
/// partir dos campos da zona, e aqui so' se troca o <b>valor</b> dos campos que alguem mexeu.
/// Tudo o resto do ficheiro — os comentarios `//`, os blocos comentados, a indentacao a' mao, e
/// os campos que a ferramenta nem le' (`DropTables`, `EnemyID`, `Entrances`, `Weather`,
/// `Forecast`, `ForecastSFX`, `hasDayCycle`, ...) — fica byte a byte como estava.</para>
///
/// <para>O que se compara e' o estado <b>de partida</b> da zona (<see cref="ZoneInput.ComoVeio"/>,
/// que o <see cref="BlockRead.Le"/> guarda) com o estado actual.  Um campo que ninguem mexeu nao
/// e' sequer tocado, mesmo que o ficheiro o escreva de forma pouco habitual — e' o que evita
/// que o `DoorId: 4294967290` de um ficheiro antigo vire `-6` sem ninguem pedir.</para>
/// </summary>
public static class BlockSave
{
    /// <summary>
    /// Aplica as alteracoes desta zona ao texto do ficheiro.  Devolve o que mexeu; a lista
    /// vazia quer dizer que o ficheiro ja' esta' como deve estar e nao vale a pena reescreve-lo.
    /// </summary>
    public static List<Alteracao> Actualiza(ZoneInput z, Jsonc j)
    {
        var mudou = new List<Alteracao>();
        var v = z.ComoVeio;

        // --- os campos do ZoneSettings e do ZoneSettingsExtra que a ferramenta deixa editar.
        // Os outros (`UnkA`, `ZoneType`, `AreaIndex`, `SubArea`, `UnkC`, `UnkO`, `UnkK`, ...)
        // nao tem campo na janela, por isso nao ha nada que possa ter mudado neles.
        Num(j, mudou, "ZoneIndex", z.ZoneIndex, v?.ZoneIndex);
        Num(j, mudou, "ModelId", z.Model, v?.Model);
        // o `Seed` e' o unico que os ficheiros reais escrevem sempre sem sinal (797 dos 1587
        // valores passam de 0x7FFFFFFF e nenhum e' negativo)
        Num(j, mudou, "Seed", z.Seed, v?.Seed, unchecked((uint)z.Seed).ToString(CultureInfo.InvariantCulture));
        Num(j, mudou, "LatNumber", z.Arguments, v?.Arguments);
        Num(j, mudou, "MapWidth", z.SizeX, v?.SizeX);
        Num(j, mudou, "MapHeight", z.SizeY, v?.SizeY);
        Num(j, mudou, "MapType", z.UnkB, v?.UnkB);
        Num(j, mudou, "BigChunk1", z.UnkP, v?.UnkP);
        Num(j, mudou, "BigChunk1Variant", z.UnkQ, v?.UnkQ);
        Num(j, mudou, "BigChunk2", z.UnkW, v?.UnkW);
        Num(j, mudou, "BigChunk2Variant", z.UnkY, v?.UnkY);
        Num(j, mudou, "ZoneFlags", z.UnkN, v?.UnkN);

        Num(j, mudou, "MapGenTable/ExitMinDistance", z.Unk3, v?.Unk3);
        Num(j, mudou, "MapGenTable/OpenBranchCap", z.Unk4, v?.Unk4);
        // o `Unk1`/`Unk2` da InfoTable3 sao o `sizeX`/`sizeY` — bate nas 1151 zonas do corpus
        // sem uma excepcao —, por isso acompanham o tamanho.  So' quando o tamanho muda: o
        // ficheiro de quem nao lhe mexeu fica como estava.
        if (v is null || v.SizeX != z.SizeX)
            Num(j, mudou, "MapGenTable/MapWidth", z.SizeX, v?.It3?.Unk[0]);
        if (v is null || v.SizeY != z.SizeY)
            Num(j, mudou, "MapGenTable/MapHeight", z.SizeY, v?.It3?.Unk[1]);

        Lista(z, v, j, mudou);
        Teleportes(z, v, j, mudou);
        return mudou;
    }

    /// <summary>Le' o ficheiro da zona, aplica-lhe o que mudou e grava-o — se algo mudou.</summary>
    public static List<Alteracao> Grava(ZoneInput z)
    {
        if (z.Ficheiro is null)
            throw new InvalidOperationException("this zone did not come from a block.json");
        var j = Jsonc.Le(z.Ficheiro);
        var mudou = Actualiza(z, j);
        if (mudou.Any(a => a.Escrita))
            j.Grava(z.Ficheiro);
        // a zona passa a ser o seu proprio ponto de partida: gravar outra vez nao mexe em nada.
        // Ficando algum campo por escrever, o ponto de partida **nao** se mexe — assim o aviso
        // de que ele nao coube no ficheiro volta a aparecer na gravacao seguinte, em vez de
        // desaparecer depois de se ver uma vez.
        if (mudou.All(a => a.Escrita))
        {
            var copia = z.Clone();
            copia.ComoVeio = null;
            z.ComoVeio = copia;
        }
        return mudou;
    }

    // ---------------------------------------------------------------- os campos compostos

    /// <summary>
    /// A entrada deste bloco, quando o ficheiro ainda a tem dentro dele.
    ///
    /// <para>A lista passou para o `quest.json` da quest, sob <c>ZoneEntrances</c>, porque o
    /// numero de entradas nao acompanha o numero de blocos.  Um `block.json` que ainda a tenha
    /// e' emendado no sitio — nao se lhe tira o campo, porque isso e' trabalho da migracao da
    /// Starlight e nao de uma gravacao —, e um que ja' nao a tenha e' deixado em paz.</para>
    /// </summary>
    private static void Lista(ZoneInput z, ZoneInput? v, Jsonc j, List<Alteracao> mudou)
    {
        if (!j.Tem("ZoneUnknown1"))
            return;                       // ja' foi para o quest.json; nao ha nada a emendar
        var agora = Par(z);
        if (v is not null && Par(v) == agora)
            return;
        // os ficheiros escrevem `unk1`/`unk2`, mas ha' despejos com maiuscula
        string a = j.Tem("ZoneUnknown1/unk1") ? "ZoneUnknown1/unk1" : "ZoneUnknown1/Unk1";
        string b = j.Tem("ZoneUnknown1/unk2") ? "ZoneUnknown1/unk2" : "ZoneUnknown1/Unk2";
        Num(j, mudou, a, agora.Item1, null);
        Num(j, mudou, b, agora.Item2, null);
    }

    private static (int, int) Par(ZoneInput z)
    {
        var lista = z.Zu1.Count > 0
            ? z.Zu1
            : (z.LadosA ?? new List<int>()).Select(s => (z.ZoneIndex, s)).ToList();
        return z.OrdemBloco >= 0 && z.OrdemBloco < lista.Count ? lista[z.OrdemBloco] : (0, 0);
    }

    /// <summary>
    /// O `TeleportData`.  Enquanto os warps forem os mesmos nao se toca no campo — nem na forma
    /// em que ele esta' escrito, que nos ficheiros reais tanto e' uma lista como um objecto so',
    /// e que costuma levar o registo `(0,0,0,0)` de enchimento que o leitor deita fora.
    ///
    /// <para>Mudando os warps ha' dois caminhos: se o numero de registos bater, trocam-se os
    /// valores um a um e os comentarios de cada linha ficam onde estao; se nao bater, a lista
    /// inteira e' reescrita — a estrutura mudou, e nao ha forma de a remendar sem a refazer.</para>
    /// </summary>
    private static void Teleportes(ZoneInput z, ZoneInput? v, Jsonc j, List<Alteracao> mudou)
    {
        if (v is not null && Iguais(z.Warps, v.Warps))
            return;

        int quantos = j.Quantos("TeleportData");
        // a forma de objecto so': um warp escrito sem lista a' volta
        bool objecto = quantos < 0 && j.Tem("TeleportData/UnkA");
        if (objecto && z.Warps.Count == 1)
        {
            Warp(j, mudou, "TeleportData", z.Warps[0]);
            return;
        }
        if (quantos == z.Warps.Count && quantos > 0)
        {
            for (int i = 0; i < quantos; i++)
                Warp(j, mudou, $"TeleportData/{i}", z.Warps[i]);
            return;
        }

        string texto = Escreve(z.Warps, j.Indentacao("TeleportData"), j.Quebra);
        var alt = new Alteracao
        {
            Campo = "TeleportData",
            Antes = $"{Math.Max(quantos, objecto ? 1 : 0)} warp(s)",
            Depois = $"{z.Warps.Count} warp(s), rewritten",
            Escrita = j.Poe("TeleportData", texto),
        };
        mudou.Add(alt);
    }

    private static void Warp(Jsonc j, List<Alteracao> mudou, string raiz, Warp w)
    {
        Num(j, mudou, raiz + "/UnkA", w.Unka, null);
        Num(j, mudou, raiz + "/DoorId", w.DoorId, null);
        Num(j, mudou, raiz + "/DstZone", w.DstZone, null);
        Num(j, mudou, raiz + "/BackDoorId", w.BackDoorId, null);
    }

    private static bool Iguais(List<Warp> a, List<Warp> b)
        => a.Count == b.Count
           && a.Zip(b).All(p => p.First.Unka == p.Second.Unka
                                && p.First.DoorId == p.Second.DoorId
                                && p.First.DstZone == p.Second.DstZone
                                && p.First.BackDoorId == p.Second.BackDoorId);

    /// <summary>Uma lista de `TeleportData` de raiz, indentada como o campo que substitui.</summary>
    private static string Escreve(List<Warp> warps, string indentacao, string quebra)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < warps.Count; i++)
        {
            var w = warps[i];
            sb.Append(quebra).Append(indentacao).Append("  {");
            foreach (var (nome, valor) in new[]
                     {
                         ("UnkA", w.Unka), ("DoorId", w.DoorId), ("DstZone", w.DstZone),
                         ("BackDoorId", w.BackDoorId), ("DstEntrance", 0), ("FromEntrance", 0),
                     })
            {
                sb.Append(quebra).Append(indentacao).Append("    \"").Append(nome)
                  .Append("\": ").Append(valor.ToString(CultureInfo.InvariantCulture));
                if (nome != "FromEntrance")
                    sb.Append(',');
            }
            sb.Append(quebra).Append(indentacao).Append("  }");
            if (i + 1 < warps.Count)
                sb.Append(',');
        }
        if (warps.Count > 0)
            sb.Append(quebra).Append(indentacao);
        return sb.Append(']').ToString();
    }

    /// <summary>
    /// O caminho que este ficheiro realmente usa: o nome de agora se ele la' estiver, senao o
    /// antigo (ver Chaves).  Assim o `Save` emenda tanto um `block.json` ja' convertido pela
    /// Starlight como um que ainda esteja na forma antiga, sem o converter pelas costas de
    /// ninguem.
    /// </summary>
    private static string Resolve(Jsonc j, string caminho)
    {
        if (j.Tem(caminho))
            return caminho;
        var partes = caminho.Split('/');
        for (int i = 0; i < partes.Length; i++)
        {
            string? antigo = Chaves.AntigoNoCaminho(caminho, i);
            if (antigo is null)
                continue;
            var tentativa = (string[])partes.Clone();
            tentativa[i] = antigo;
            string alt = string.Join('/', tentativa);
            if (j.Tem(alt))
                return Resolve(j, alt);
        }
        return caminho;
    }

    // ---------------------------------------------------------------- um numero

    /// <summary>
    /// Escreve um numero, se ele tiver mudado.  <paramref name="antes"/> e' o que a zona tinha
    /// quando se leu o ficheiro; null quer dizer "nao se sabe, tenta escrever".
    ///
    /// <para>Mesmo tendo mudado na zona, se o que esta' no ficheiro ja' <b>vale</b> o mesmo
    /// numero nao se mexe nele: o `4294967290` e o `-6` sao o mesmo `DoorId`, e trocar um pelo
    /// outro seria uma alteracao que ninguem pediu.</para>
    /// </summary>
    private static void Num(Jsonc j, List<Alteracao> mudou, string caminho, int agora,
                            int? antes, string? literal = null)
    {
        if (antes is int a && a == agora)
            return;
        // O ficheiro pode estar na forma antiga; nesse caso emenda-se a chave que ele tem.
        caminho = Resolve(j, caminho);
        if (j.Inteiro(caminho) == agora)
            return;
        literal ??= agora.ToString(CultureInfo.InvariantCulture);
        mudou.Add(new Alteracao
        {
            Campo = caminho,
            Antes = j.Valor(caminho) ?? "-",
            Depois = literal,
            Escrita = j.Poe(caminho, literal),
        });
    }
}
