namespace DotaHighlights.Client.Editing;

/// <summary>Catálogo de presets de edición. Añadir un estilo nuevo = añadir aquí.</summary>
public static class EditPresets
{
    private static string F(double v) => EditContext.F(v);

    public static IReadOnlyList<EditPreset> All { get; } = new[]
    {
        new EditPreset("promontage", "🔥 Pro Montage", Rank: 0, ProMontage),
        new EditPreset("slowmo", "🐢 Slow-mo", Rank: 1, Slowmo),
        new EditPreset("zoom", "🔍 Zoom épico", Rank: 2, Zoom),
        new EditPreset("cinematic", "🎞️ Cinematic", Rank: 3, Cinematic),
    };

    /// <summary>
    /// Estrella: speed-ramp a slow suave (blend) en el kill + zoom con shake +
    /// flash blanco + grade teal-orange + viñeta + grano + barras + fundidos.
    /// </summary>
    private static string ProMontage(EditContext c)
    {
        double s0 = c.SlowStart, s1 = c.SlowEnd, dur = c.Duration;
        double proTotal = s0 + (s1 - s0) / 0.4 + (dur - s1);
        double fo = Math.Max(0.2, proTotal - 0.5);

        return
            $"[0:v]trim=0:{F(s0)},setpts=PTS-STARTPTS[a];" +
            $"[0:v]trim={F(s0)}:{F(s1)},setpts=(PTS-STARTPTS)/0.4," +
            "minterpolate=fps=60:mi_mode=blend," +
            "scale=w=iw*1.35:h=ih*1.35," +
            "crop=iw/1.35:ih/1.35:x='(in_w-out_w)/2+8*sin(25*t)':y='(in_h-out_h)/2+8*cos(23*t)'," +
            "eq=saturation=1.35:contrast=1.1," +
            "fade=t=in:st=0:d=0.12:color=white[b];" +
            $"[0:v]trim={F(s1)},setpts=PTS-STARTPTS[c];" +
            "[a][b][c]concat=n=3:v=1[cat];" +
            "[cat]colorbalance=rs=-0.08:bs=0.08:rh=0.10:bh=-0.08," +
            "eq=contrast=1.12:saturation=1.2:gamma=0.98," +
            "vignette=PI/5,noise=alls=6:allf=t," +
            "drawbox=y=0:w=iw:h=ih*0.10:color=black@1:t=fill," +
            "drawbox=y=ih*0.90:w=iw:h=ih*0.10:color=black@1:t=fill," +
            $"fade=t=in:st=0:d=0.5,fade=t=out:st={F(fo)}:d=0.5[vbase]";
    }

    private static string Slowmo(EditContext c) =>
        $"[0:v]trim=0:{F(c.SlowStart)},setpts=PTS-STARTPTS[a];" +
        $"[0:v]trim={F(c.SlowStart)}:{F(c.SlowEnd)},setpts=(PTS-STARTPTS)/0.35,eq=saturation=1.2:contrast=1.05[bb];" +
        $"[0:v]trim={F(c.SlowEnd)},setpts=PTS-STARTPTS[c];" +
        "[a][bb][c]concat=n=3:v=1[vbase]";

    private static string Zoom(EditContext c) =>
        $"[0:v]trim=0:{F(c.SlowStart)},setpts=PTS-STARTPTS,eq=saturation=1.25:contrast=1.08[a];" +
        $"[0:v]trim={F(c.SlowStart)}:{F(c.ZoomEnd)},setpts=PTS-STARTPTS,scale=w=iw*1.3:h=ih*1.3,crop=iw/1.3:ih/1.3,eq=saturation=1.4:contrast=1.12[bb];" +
        $"[0:v]trim={F(c.ZoomEnd)},setpts=PTS-STARTPTS,eq=saturation=1.25:contrast=1.08[c];" +
        "[a][bb][c]concat=n=3:v=1[vbase]";

    private static string Cinematic(EditContext c) =>
        "[0:v]eq=contrast=1.12:saturation=1.15:gamma=0.95," +
        "drawbox=y=0:w=iw:h=ih*0.10:color=black@1:t=fill," +
        "drawbox=y=ih*0.90:w=iw:h=ih*0.10:color=black@1:t=fill," +
        $"fade=t=in:st=0:d=0.6,fade=t=out:st={F(c.FadeOut)}:d=0.6[vbase]";
}
