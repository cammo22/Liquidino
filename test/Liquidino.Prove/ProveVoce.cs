using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

public class ProveVoce
{
    [Fact]
    public void Prima_omni_poi_windows_poi_il_solo_testo()
    {
        Assert.Equal(MotoreVoce.Omni, SceltaVoce.Scegli(true, true, true, true));
        Assert.Equal(MotoreVoce.Windows, SceltaVoce.Scegli(true, false, true, true));
        Assert.Equal(MotoreVoce.Windows, SceltaVoce.Scegli(false, true, true, true));
        Assert.Equal(MotoreVoce.Nessuno, SceltaVoce.Scegli(false, false, false, true));
        Assert.Equal(MotoreVoce.Nessuno, SceltaVoce.Scegli(false, false, true, false));
        Assert.Equal(MotoreVoce.Windows, SceltaVoce.DopoErrore(true, true));
        Assert.Equal(MotoreVoce.Nessuno, SceltaVoce.DopoErrore(false, true));
    }

    [Fact]
    public void Sceglie_il_genere_e_se_manca_va_bene_l_altro()
    {
        var voci = new[] { "Cosimo:m", "Elsa:f" };
        bool Donna(string v) => v.EndsWith(":f");
        Assert.Equal("Elsa:f", SceltaVoce.Preferita(voci, Donna, "female"));
        Assert.Equal("Cosimo:m", SceltaVoce.Preferita(voci, Donna, "male"));
        Assert.Equal("Elsa:f", SceltaVoce.Preferita(new[] { "Elsa:f" }, Donna, "male"));
        Assert.Null(SceltaVoce.Preferita(Array.Empty<string>(), Donna, "female"));
    }

    [Fact]
    public void La_voce_di_windows_e_accesa_di_serie_e_si_salva()
    {
        var radice = Aiuti.Temporanea();
        var imp = ImpostazioniUtente.Carica(radice);
        Assert.True(imp.VoceWindows);
        imp.VoceWindows = false;
        imp.Salva();
        Assert.False(ImpostazioniUtente.Carica(radice).VoceWindows);
    }
}
