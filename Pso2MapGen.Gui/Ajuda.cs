using Pso2MapGen.Core;

namespace Pso2MapGen.Gui;

/// <summary>
/// O texto do botao [i] de cada campo.  A regra e' dizer o que o campo <b>faz ao mapa</b>, e
/// dizer tambem quando nao se sabe — ha' campos cujo nome e' so' o deslocamento no registo
/// precisamente porque nem o Starlight sabe o que sao, e so' uma parte deles e' que a geracao
/// usa.
///
/// <para>Os nomes sao os da source do Starlight, nao os `Unk*` de antes; o nome antigo de cada
/// campo aparece no fim do respectivo texto, tirado da <see cref="Chaves"/> para nao haver duas
/// listas a divergir.</para>
///
/// A proveniencia esta' na seccao 39.2 do FINDINGS.  Em ingles, como o resto da interface.
/// </summary>
internal static class Ajuda
{
    /// <summary>
    /// Mostra o texto de um campo numa janela com barra de deslocamento.
    ///
    /// <para>Era um <c>MessageBox</c>, que se dimensiona pelo texto e nao tem como deslocar:
    /// o do <c>ZoneFlags</c> tem 58 linhas e fazia uma janela maior do que o ecra, mesmo a
    /// 1920x1080.  Aqui a janela tem tecto, e o que nao couber desliza.</para>
    ///
    /// <para><paramref name="chave"/> e' o nome do campo no <c>block.json</c>; serve para ir
    /// buscar a <see cref="Chaves"/> o nome que ele tinha antes e acrescenta-lo no fim, para
    /// quem conhece os ficheiros antigos se situar.  Passar null quando a linha nao
    /// corresponde a um campo do ficheiro.</para>
    /// </summary>
    public static void Mostra(Form pai, string titulo, string texto, string? chave = null)
    {
        using var f = Monta(pai, titulo, texto, chave);
        f.ShowDialog(pai);
    }

    /// <summary>
    /// Monta a janela sem a mostrar.  Separado da <see cref="Mostra"/> por uma razao so': e'
    /// esta janela que faz mais contas de medida de todas — mede a linha mais comprida do texto
    /// para se dimensionar — e assim o `Pso2MapGen.ConfereDpi` pode construi-la e medi-la a
    /// 150% e a 200% como mede as outras.  Uma janela que se mostra sozinha nao se consegue
    /// conferir.
    /// </summary>
    internal static Form Monta(Form pai, string titulo, string texto, string? chave = null)
    {
        string? antes = chave == null ? null : Chaves.AntigoNoCaminho(chave, chave.Split('/').Length - 1);
        string corpo = texto.TrimEnd();
        if (antes != null)
            corpo += Environment.NewLine + Environment.NewLine
                   + new string('-', 60) + Environment.NewLine
                   + $"Old name: {antes}   (block.json files that still use it open fine)";

        var caixa = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            WordWrap = true, Dock = DockStyle.Fill,
            // uma TextBox do WinForms so' quebra a linha no `\r\n`; um `\n` sozinho
            // — que e' o que um literal de varias linhas traz — e' engolido, e o texto
            // sairia todo seguido
            Text = corpo.ReplaceLineEndings("\r\n"),
            Font = new Font(FontFamily.GenericSansSerif, 9f),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0),
        };
        var fechar = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 30 };
        var ecra = Screen.FromControl(pai).WorkingArea;
        var painel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 6) };
        painel.Controls.Add(caixa);

        // Os textos vem quebrados a mao e a largura era 720 fixos, o que so' os acomodava na
        // fonte e no DPI em que foram escritos — noutro, a linha mais comprida passava para fora
        // e a WordWrap re-quebrava tudo de maneira irregular.  Mede-se a linha mais comprida com
        // a fonte que vai mesmo ser usada, e junta-se o espaco da barra e das margens.
        int maisLonga = 0;
        foreach (var l in corpo.ReplaceLineEndings("\n").Split('\n'))
            maisLonga = Math.Max(maisLonga, TextRenderer.MeasureText(l, caixa.Font).Width);
        // A medida do texto e o tamanho do monitor vem em pixeis do ecra; o resto dos numeros
        // desta janela esta' em pixeis logicos, de 96 DPI, porque e' o WinForms que os escala
        // (`AutoScaleMode.Dpi`, mais abaixo).  As duas medidas passam a logicos para entrarem
        // na mesma escala — sem isto, a 200% a janela ficava com metade da largura que o texto
        // pede.  O DPI vem da janela que chamou, que e' a que sabe em que monitor esta'.
        int largura = pai.Logico(maisLonga + SystemInformation.VerticalScrollBarWidth) + 60;
        int larguraDoEcra = pai.Logico(ecra.Width);
        int alturaDoEcra = pai.Logico(ecra.Height);
        largura = Math.Min(largura, larguraDoEcra - 80);

        var f = new Form
        {
            Text = titulo,
            // A altura e' o que obriga o ZoneFlags, com as suas 58 linhas, a deslizar em vez
            // de crescer.
            AutoScaleMode = AutoScaleMode.Dpi,
            Width = Math.Max(480, largura),
            Height = Math.Min(560, alturaDoEcra - 80),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
        };
        fechar.Click += (_, _) => f.Close();
        // a dimensao de referencia tem de ser posta com o layout suspenso e **antes** de os
        // controlos entrarem: posta com o layout a correr, a escala dava-se por feita com a
        // janela ainda vazia e o que entrasse depois ficava sem ela
        f.SuspendLayout();
        f.AutoScaleDimensions = new SizeF(Escala.DpiBase, Escala.DpiBase);
        f.Controls.Add(painel);
        f.Controls.Add(fechar);
        f.AcceptButton = fechar;
        f.CancelButton = fechar;
        f.ResumeLayout(performLayout: true);
        return f;
    }

    public const string ZoneIndex = """
        Which zone (block) of the map this is.

        The generator itself never reads it: it is how warps and ZoneUnknown1 refer to this
        zone, and it names the exported block folder (b1, b2, b3...).

        A zone with a negative index is usually a camp or a lobby sub-area and gets no block.
        """;

    public const string ModelId = """
        Which area this zone is built from.

        It picks the piece catalogue, extracted/landform_common/sn_<ModelId>.lps, and the
        folder of .lat files. Everything the generator can place comes from that one file, so
        two zones with the same ModelId draw from exactly the same set of pieces.

        Examples: 311 = Forest 1, 330 = Ruins, 530 = Sanctum, 620/630 = river areas.

        The names in the list come from models.txt, beside the executable, and it is meant to
        be edited: one line per id, "330 = Ruins", with # for comments. An id that is not in
        the file simply shows its number. The name is a label and nothing else - what the tool
        generates and exports is always the number.
        """;

    public const string Seed = """
        Seeds the Mersenne Twister that drives every random choice.

        The same seed with the same fields always produces exactly the same map — that is the
        whole reason this tool can predict what the client will build.

        The field takes digits only, and always shows the value unsigned - the way Starlight's
        block.json files write it, where 797 of the 1587 seeds in the corpus are above 0x7FFFFFFF
        and not one is negative. Only the 32 bits matter, so -687271813 and 3607695483 are the same
        seed; paste either and the field settles on the positive one.

        The client uses the seed the packet carries and nothing else: change it and the map
        changes. So if your server sends the value written in the block.json unchanged, that
        value IS the map you get, and hunting for a seed works exactly as you would expect.

        What is not guaranteed is that a Seed found in some block.json was the one the client
        received. A server may draw a fresh seed per instance instead of sending the file's,
        and in the probe captures on disk the seed in the file is usually not the one the
        dumps were generated from - the file and the dumps come from different runs.
        """;

    public const string MapSize = """
        MapWidth and MapHeight are a CEILING for the content bounding box, not the size of the map.

        The generator refuses any placement that would make the box reach these values, so a
        map almost always comes out smaller. That is why "the map does not respect the size"
        looked wrong at first.

        They also set the working grid: an entrance on the north or south side gives a grid of
        (MapWidth*2-1) x MapHeight, and one on the east or west gives MapWidth x (MapHeight*2-1).
        The final picture is the bounding box of what was written, plus a one-cell border.
        """;

    public const string MapType = """
        What kind of map this zone gets.

        0 - FIXED. No generation at all: the map is the file sn_<ModelId>_<LatNumber>.lat,
            exactly as it was authored. Half of all zones in the packets are like this.
        1 - RANDOM. The generator builds the map from the seed.
        2 - RIVER. The river .lat is cloned as a skeleton and the generator fills around it,
            crossing the river cells where a corridor cuts across one.
        """;

    public const string LatNumber = """
        Which .lat file to use.

        With MapType = 0 it is the fixed map: sn_<ModelId>_<LatNumber>.lat.
        With MapType = 2 it is the river skeleton: sn_<ModelId>_river<LatNumber>.lat.
        With MapType = 1 (random) it is unused and normally -1.

        The client opens the .lat this number names, so what you put here is what you get.

        Careful when comparing against a capture, though: in the four river captures on disk
        the LatNumber in the block.json is NOT the .lat the client had loaded - identifying
        the skeleton from the dumped map gives river0004, river0004, river0003 and river0004
        where the files say 3, 1, 4 and 2. Those zones have around twenty skeletons each and
        the server that served them picked one per instance, so the file was the mould and
        not what went out.
        """;

    public const string BigChunkVariant = """
        Which model file the big chunk beside it loads: BigChunk1Variant goes with BigChunk1,
        BigChunk2Variant with BigChunk2.

        The client opens stage/sn_<ModelId>/ln_<ModelId>_<chunk>_<variant>.ice, so this
        number decides what the piece LOOKS LIKE and not where it goes. The generator only ever
        reads the chunk's name, so two designs with the same name and different variants are one
        chunk for the candidate pool - changing this can never change the layout.

        The list offers the variants that really exist for that chunk in that area. It was
        measured against the client's own data files, not guessed, and the answer is narrow:
        only 80 and 81 exist anywhere. Every chunk has 80; seven have 81 as well (220 q5,
        311 q5, 411 q5, 430 q0, 431 q0, 711 q0 and 2200 q5). The packets agree - of the 998
        pairs in the 583 captures, 969 are 80 and 29 are 81, all of those on 311 q5.

        The list lives in bigchunks.txt beside the executable and can be edited by hand, the
        same way models.txt can.
        """;

    public const string BigChunk = """
        The two "big chunk" designs the zone is allowed to use.

        Each value is the chunk's two-character short name as ASCII little-endian: 13681 = 0x3571 =
        "q5", 13425 = 0x3471 = "q4". Zero means "this design is not set". The companion fields
        BigChunk1Variant and BigChunk2Variant pick the ICE variant of the model and do not change
        the map.

        This matters more than it looks: only the declared designs go into the candidate
        pool. The others are not weighed and rejected, they are not offered at all, so the
        pool is a different size and every later draw shifts. Most zones declare none.
        """;

    public const string ExitMinDistance = """
        MapGenTable.ExitMinDistance - the minimum corridor length for the EXIT door.

        When the exit is placed, every candidate cell on the border is measured, and the
        measurement does not start at zero: it starts at that cell's distance in pieces from
        the ENTRANCE. So this field really means "put the exit at least this far from the way
        in", not "leave this much room in front of it".

        A zone whose exit cannot reach the distance fails and the generator starts over.
        """;

    public const string OpenBranchCap = """
        MapGenTable.OpenBranchCap - the cap on open branches.

        1 gives a cap of 5, 2 gives a cap of 2, and anything else means no cap at all. With a
        cap the generator refuses any piece that would leave more open ends than the cap
        allows, which is what keeps some maps from sprawling.

        It also changes the ramp draw slightly, because the cap feeds the index into the ramp
        tables.
        """;

    public const string ZoneFlags = """
        ZoneFlags is a BIT FIELD, not a number with a meaning of its own, and only ONE of its bits
        is known to touch the map: bit 5, worth 32.

        4119 = 0x1017 = bits 0,1,2,4,12 -> bit 5 is OFF
        4151 = 0x1037 = the same plus bit 5 -> ON

        So the switch is ZoneFlags xor 32: 4119 <-> 4151, 4118 <-> 4150, 4102 <-> 4134.

        With bit 5 set, every side of the zone that was given no type at all goes into a list,
        one of them is drawn, and it is added to mapB with the n0 chunk - an extra door on the
        boundary. Three cases:

          - the zone has no declared exit (doorId = -6, or no departing warp): this is how it
            gets a way out at all;
          - the zone already has an exit: it gets a SECOND door;
          - no side is left over (mapA = {1,2,3} and mapB = {0}): nothing happens, and no word
            is spent.

        The draw costs one MT word whenever more than one side is left over - so flipping this
        bit does not just add a door, it shifts the whole stream and changes the ENTIRE map.

        Measured: the rule matches 81 of 81 captured generations. Bit 5 is set in 260 of the
        1641 zones of the corpus - and in 95 of the 95 generated Sanctum zones, which is the one
        area that has start chunks and no goal chunk, so it is exactly the area that cannot get
        an exit any other way.

        THE CLIENT NEVER READS THE FIELD WHOLE. 0x007194F0 is a generic bit tester and seven
        different places call it, one bit each (bits 0, 1, 2, 3, 4, 5 and 10), so writing 4119
        is writing five booleans at once. Use the Bits... button beside the field: it shows one
        checkbox per bit with what is known about each, and keeps the others where they were.

        WHAT THE OTHER BITS ARE. Naming them from the code is out of reach - the binary carries
        no RTTI for game code, none of the reader functions is virtual, and the subsystems they
        sit in are thousands of bytes each. But the corpus gives implications, which is what
        matters when setting the field (FINDINGS 41.10):

          bit 1   set in ALL 1132 generated zones, without exception. A server that sets it on
                  every random or river zone is right by construction
          bit 2   set in every one of the 1641 zones
          bit 6   only ever appears on FIXED maps, all 41 of them - setting it on a generated
                  zone is certainly wrong
          bit 7   an exact function of zoneType: 1 for types 6 and 7, 0 for 2 and 3. Never
                  choose it by hand
          bit 12  set in 1640 of 1641; the one gap is zone 1 of an Ultimate quest
          bit 0   agrees with zoneType & 1 in 95%, with 75 exceptions - so not a rule
          bit 3   never set anywhere, and the client tests it anyway
          bit 4   its value is STORED in a field rather than branched on; 1074 of the 1132
                  generated zones
          bit 10  a lobby-effect flag: all 52 Shironia zones and 2 Facility ones, nowhere else
          8, 9, 11  never seen set, and no reader found

        The five bits that something else decides - 1, 2, 6, 7 and 12 - are greyed out in the
        Bits box. They can still be changed; it is just not where to start.

        A zone made by hand no longer starts at 0 either - zero is not a value ANY of the 1641
        zones of the corpus has. It starts at what that area usually carries, from the `x =`
        line of infotables.txt.
        """;

    public const string MapA = """
        The ENTRANCE sides of this zone. 0 = North, 1 = East, 2 = South, 3 = West.

        They come from two places in the packet: the ZoneUnknown1 entries that point at this
        zone, and the BackDoorId of every warp that ARRIVES here.

        One of them is drawn to be the starting side, and that choice decides the shape of the
        working grid and where the first piece goes. The others get an entrance door placed
        afterwards, like any other door. The draw costs ONE MT word when there is more than one
        side, and NONE at all when there is only one.

        The NAME of the entrance piece is not drawn and does not come from the model. It is
        read out of the area's .lps: paramsC, keyed by the four-digit model number of the zone
        you came FROM. When that key is missing - or when the entrance was declared by
        ZoneUnknown1 instead of arriving from a warp - the client writes the literal s0. The
        exit works the same way through paramsD, with g0 as its literal, and a name that is not
        a piece of the model falls back to n0, which is what the Sanctum does.

        The exit has one rule on top of that: when it leads to the BOSS area - a zone whose map
        is fixed - the name comes from paramsB's goal_to_boss key, if the model declares it.
        Only two of the 220 models do, sn_0620 and sn_0621, both g1, and the Seabed's two areas
        of that same model are what separate the rule from its condition: area 1 leaves for a
        normal area and the client writes g0, area 2 leaves for the boss and it writes g1.

        And when ZoneFlags bit 5 is set, one side that got no type at all is moved into the
        exit map, and THAT one carries the literal n0 rather than the exit's name.

        Measured over 96 generations with no exception, and the pieces of all 20 end-to-end
        grids come out with the right names.
        """;

    public const string MapB = """
        The EXIT sides of this zone. 0 = North, 1 = East, 2 = South, 3 = West.

        They come from the DoorId of every warp that LEAVES this zone. A DoorId of -6 is not a
        side: it marks a zone with no declared exit door, and in that case the exit comes from
        the ZoneFlags bit 5 mechanism instead.

        A side can only have one type. If a side is claimed as both an entrance and an exit,
        the entrance wins.
        """;

    public const string HeightMode = """
        HeightMode - THIS DOES NOT COME FROM THE PACKET as a field of its own.

        It picks which row of the two ramp-draw tables is used, and it sets the height the map
        starts at (0 gives half the maximum height, 2 and 4 give the maximum, anything else
        gives zero).

        It was searched for in all 12 ZoneSettings fields, in the 110 InfoTable values and in
        the .lps header, and it is in none of them. But it is constant per area: across 78
        measured generations the 21 areas never varied. "Automatic" uses the measured value
        (1 for models 330, 520 and 521, and 0 for every other area measured so far).

        It only has any effect in areas that use height at all.

        Where the name comes from: Starlight's UnkO was two bytes doing two unrelated jobs,
        and it was split. Byte 0 is this, HeightMode - what the generator reads through
        gen->0x94. Byte 1 became MultiPartyArea. So there is no single old field to point at:
        the old UnkO is now these two.
        """;

    public const string Init = """
        Whether the client's initialisation block at 0x00647BB0 ran - ALSO NOT IN THE PACKET.

        When it does not run, three things stay at their defaults: gen->0x94, the exit minimum
        length and the branch cap. On top of that the big-chunk demotion draw is skipped
        entirely, and the big chunks are never registered, so group 11 is empty.

        It happened in 5 of the 83 measured captures, and in the same zone it sometimes runs
        and sometimes does not, so it is a runtime condition and not a property of the zone.
        Leave it ticked unless you are reproducing a capture where it did not run.
        """;

    public const string Attempts = """
        How many times the generator may try before giving up.

        The client's driver allows 5. The attempts all share the SAME random stream - a failed
        attempt does not reseed, it just carries on consuming words - so raising or lowering
        this changes which map you get, not just whether one is produced.

        About nine out of ten zones succeed on the first attempt.
        """;

    public const string Zones = """
        The zones of the map.

        Loading a packet fills this with the zones the server sent. To build a map from
        scratch, use "New map..." and say how many zones you want, then fill in the fields of
        each one and switch between them with this list.

        Each zone is exported as its own block.json, in a folder named b1, b2, b3...

        For a hand-made map the tool writes the TeleportData chain for you: each zone exits
        through the first of its exit sides into the next zone, which is entered through the
        first of its entrance sides. Zones that came from a packet keep the packet's own
        warps untouched.
        """;

    public const string Preview = """
        The map as the client would build it, using the game's own internal piece names.

        Each cell is the chunk's short name plus its rotation: i00, l03, t02, x01, q51, cx0.
        The dashes and bars are drawn only between cells that REALLY connect - both of them
        have to have a door facing the other. Two pieces side by side with no dash between
        them are two different branches that happen to be neighbours.

        Columns are lettered and rows numbered the same way as the in-game map grid, so the
        two can be compared square by square.

        The last two boxes add stages of the client's POST-PROCESSING. Neither touches the map
        that was generated - both are applied to a copy, for the drawing only, because the
        generated grid is what the checkers compare against the client's own dumps and those
        are taken before post-processing runs.

        "apply the d* reskin" follows MapGenTable.ExtraPassFlag: when that bit is set the
        client renames every placed piece to the model's d* family, matching on the piece's
        info dword and door. The box is ticked from the zone's own data; forcing it on a zone
        that does not carry the flag is allowed, and the log below says so. Seven of the ten
        originals map to exactly one d* piece; for i0, l0 and t0 the family offers more than
        one and the log names the choice.

        "fill unreachable cells" is the z0 sweep: every empty cell of the final grid that has
        at least one of its 8 neighbours occupied gets a z0, with its rotation drawn from the
        same MT stream the generation left behind. Right in 274 of 274 cells, and in 186 of 186
        rotations. In models that own the group 9 set (zn zi zl zt zx) a SECOND pass then
        reshapes part of the fill into fake corridors, flooding from each cell on a third MT
        stream and opening each link with a 0.7 chance. That one is modelled too now: 70 of 70
        cells over the four probe-R zones that have the set, name and rotation, and the log
        says how many cells it turned into corridors.

        "assign the ICE / _ob0* variants" is the last pass that changes the grid - the sixth
        and final stage only hands the result to the loader - and it needs the fill, because it
        runs after it on the SAME MT stream. Which cells get a variant is settled: a cell gets
        an ICE id when its chunk declares maxIce >= 1 and an _ob0 id when it declares
        maxOb > 1, right in 306 of 306 cells over the thirteen probe-R zones with no exception,
        and that asymmetry is what leaves the z* cells with an ICE id and no _ob0 one. A
        multi-cell chunk gets an ICE word too, but over the "80" range and only on the piece's
        ANCHOR cell - one draw per instance - so its other cells stay empty. The _ob0 id follows
        the same cells: maxOb > 1 and the piece's anchor, multi-cell chunks included.

        With the box ticked, a table opens under the preview: one line per cell, with the chunk,
        the rotation, the ICE id, the _ob0 id and the .set files stage 6 hands the loader for it.

        The number at the end of a .set name is the VALUE of a SetFileTable field - 0012, 0013,
        0031, 0111 - not an index of 1 to 5. (It printed 000N here for weeks because the hooked
        dumps carry two kinds of line: the real requests, and the client trying its luck with the
        five indices, which never exist.)

        A cell asks for several .set files - it does not pick one of them. 699 of the 774
        piece instances in the dumps ask for more than one, and it cannot be a choice: 1227 of
        the 1678 SetFileTable rows in the corpus have two or more fields at chance 100000, which
        under a pick-one rule would be entries that can never be reached. The five fields are
        five independent slots.

        (The hooked lines are file-open attempts, not proof of a load: the dumps also carry
        _0001.._0005 for every cell and none of those exist. Those come from another code path -
        the roller never produced them in any of the 182 calls the probe caught. What this table
        reproduces exactly is the list THIS path asks for.) Every field at chance 100000 is loaded by
        every cell; the ones below that are drawn per cell by the client when it loads, and show
        in [brackets] with the chance, so the table reads

            sn_0330_i0_00_ob00_*.set   loads 0012 0013 [0014 30%] [0015 30%]

        and the line above it lists what the zone declares. That draw is per cell and not a
        property of the chunk: of the 133 chunk+ICE+_ob0 groups seen more than once in a run, 89
        have different lists between instances.

        Whether the SAME packet gives the same lists twice is untested - the corpus has one
        capture per area, so two runs of the same zone were never compared. It changes no map
        either way: stage 6 stores nothing in the grid, and the pieces, rotations, ICE and _ob0
        were all decided before it out of the other two Twisters. The tool draws nothing; it
        shows what is certain and the chance of the rest.

        On a FIXED map the name is the zone form instead, sn_<model>_<.lat number>_000N.set with
        N running 1 to 5, and there the trailing number really is an index. The variant pass runs
        on that path too, with the same rule: the ids the .lat declares stay, the rest are drawn -
        but a fixed map never ran the generator, so the Twister is in whatever state the session
        left it. Cells whose candidate list had two or more entries are marked with a ? and show
        the first candidate, which is right for 451 of the 471 dumped cells.

        The two values come from different places. The ICE id comes from a candidate list the
        pass builds: the chunk's own range, 0 to maxIce-1 - and maxIce is exactly how many
        variant files exist, checked against the live client's 93,132 data files for every
        single-cell chunk of twelve models, no exception. Then, for a corridor (i*) or a corner
        (l*) with three or more candidates, the pass REMOVES the variant of every neighbour of
        the same chunk it shares a door with, so two adjoining corridor pieces do not wear the
        same skin. What gets written is a value of that list, not the index into the full range,
        and with a single candidate left it does not draw at all. That is measured in game, with
        both probes armed at once: 894 of 894 values and 870 of 870 lists over 15 zones and 70
        grids, and the words spent match in all 70. The _ob0 id is ONE value for the whole
        zone and it is deterministic: it is the id of DecorationTable.Field1 for that zone.
        The pass does roll a word from its own Twister and takes the id if the chance beats
        rnd % 100000 - but across the 783 captured packets a field's ten chances are always 0 or
        100000, so the roll never decides anything.
        """;

    /// <summary>
    /// O [i] da caixa das variantes.  O texto estava no cabecalho da tabela e enchia a janela:
    /// ali so' deve ficar o que muda de zona para zona.
    /// </summary>
    public const string Variantes = """
        The last pass that changes the grid. A cell gets a variant when it is OCCUPIED, when it
        is the piece's ANCHOR cell, and when its ICE word is still unset. The value is not a
        plain roll over the chunk's range: the pass builds a CANDIDATE LIST - the chunk's own
        0..maxIce-1, or, for a multi-cell chunk, the zone's two big chunks with their variants -
        and for a corridor (i*) or a corner (l*) with three or more candidates it REMOVES the
        variant of every same-chunk neighbour it shares a door with, so two adjoining corridor
        pieces do not wear the same skin. With one candidate left it writes that one and spends
        no word; with two or more it draws. Measured in game with both probes armed: 894 of 894
        values and 870 of 870 lists over 15 zones, and the words spent match in all 70 grids.

        The _ob0* is a different thing: ONE value for the whole zone, and deterministic - the id
        of DecorationTable.Field1. The pass does roll a word and take the id only if the chance
        beats rnd % 100000, but across 783 captured packets a field's ten chances are always 0
        or 100000, so the roll never decides anything.


        THE .set FILE NAMES

        The .set name is what stage 6 (0x005EDA70) hands the loader. The number at the end is
        the VALUE of a SetFileTable field - 0012, 0013, 0031, 0111 - and NOT an index of 1 to 5.
        (The hooked dumps carry two kinds of line: the real requests, and the client trying its
        luck with five indices, which never exist. Reading the second kind is what put a literal
        000N in this window for weeks.)

        A cell LOADS every field whose chance passes - it does not pick one of them. 699 of the
        774 piece instances in the dumps ask for more than one. A field at chance 100000 is
        loaded by every cell; the ones below that are drawn per cell and show in [brackets] with
        the chance.

        None of this changes the map. Stage 6 stores nothing in the grid, and measured over
        three runs of one packet - the third after restarting the client - it hands the loader
        an identical descriptor every time, so the hand-off is a function of the packet. Which
        of the declared files the loader then asks for is decided downstream of it, and that one
        is still untested. The tool draws nothing; it shows what the zone declares.


        ON A FIXED MAP

        The name is the zone form instead, sn_<model>_<.lat number>_000N.set with N running 1 to
        5, and there the trailing number really is an index. The variant pass runs on that path
        too with the same rule - the ids the .lat declares stay, the rest are drawn - but a fixed
        map never ran the generator, so the Twister is in whatever state the session left it.
        Cells whose candidate list had two or more entries are marked with a ? and show the first
        candidate, which is right for 451 of the 471 dumped cells.
        """;
}
