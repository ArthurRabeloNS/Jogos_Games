namespace SteveCube
{
    // Skin 64x64 do Steve (PNG em base64) embutida na DLL: funciona sem nenhum arquivo extra.
    // Um steve.png em BepInEx\\plugins continua tendo prioridade sobre esta.
    internal static class SteveSkinData
    {
        public const string Base64 =
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAANj0lEQVR4nO2bTawlx1XHf1Vd/X3v87yZAWM8xIodxZGihA3xAtsL" +
            "h4ABB4WE4CBASKxQLEUIgYAF7Ek2ZAcLBGwIsiXCBlt8SKBIGIdIycYSKMQYJ3YwjvF45r7+rqouFt2n5r6g+I24YyaLd6Sn0dyv" +
            "7nPqnP/5n49WnCGPvPeuME0TZVnSdR1FUeC9ZxgGqqrioffcS5ZlDMNAkiSkaYr3Hu89WZbxu3/+D+qsa9xO0Wd+QGuyLOPk5ASA" +
            "YRiYpol5nhmGgXmeATDGkOc54zgSQiDLMrz3b+/d3wIxZ37AGJxzaK2pqgrnHOM4cnx8DCwG8t4TQmCeZ/I8B8B7T5qmb+/d3wI5" +
            "0wN2ux273Q7nHE3TRKWcc7Rti1IKYxY7WmuZ5zn+K97x3Sw3FQIARVGQJAnjOLLZbJimic1mEz1E4n6eZ4wxKKWw1r7tChwqNxUC" +
            "WmvKssQ5R9/3EQAlNMT15cSnaYqvf7fLmR5QFAVFUeCcI0kSAC5cuMA4jmito7LOOQDmeSbLMrIsoyiKt/fub4GcaYB5nrl27RpK" +
            "Kdq2xTnHyckJSimapolIL+CX5znTNKGUYhiGt/fub4GoD73/SmiahqOjI65du0ZZlmRZtrypFCGEqHxVVYQQGIaBPM/ja13XUZZl" +
            "9IRhGEjTNKbPzWYTPWMcR/I8Z7fboZTiiy9eu6084X9hgPeeruvQWkfA01rzGx//8RjXIQT+8K//kbIsadsWYwzee/q+RylFWZaM" +
            "44i1ljzP6fs+pkmA69evY4yJIXU7RTdNw3a7xXsfY9s5R5ZllGVJCIFP/sTD5HmOtTbG9q88+iAhBLTWFEURlSuKImaBEALee6qq" +
            "IsuymDEuXLhAXddUVXWb1Qez3W6BheFprTk6OkJrjVIKpRS/+pEf4S++8GUAPvLgDwLw1N9/CYBf++iP8uknnyaEELPEPM/xu8YY" +
            "iqLAWss0TcDiYU3TUBQFu93uNqh8WmII1HWN955P/dQjGGOw1kZC8ydfeJ5HH/wDjPlnuq7jP90T/M2zn+Tzv/3z/M4v/nSkxGVZ" +
            "8pmnnolcQHiDGBcWXNFa07ZtxJrbKeqhd18OSqnI3H7z8Z8kTdNIbl574zrfc7xFKcW3ri4n9o7v/17GceS1N65z952XIl5orfn0" +
            "k08zzzPHx8d472nblrqu2e12VFVFnuc457DW4pzjuX9/8/aC4G994jGUWu5B0F4KIInrPM/5yr+9xNX/bgD41q7hvffcBUCSJKdc" +
            "X8BSvOgzTz2D1joWStba6BlCoW+nGElrxph4Q9M0RSAE+NK/vLDgw4UiGutfX36Ny5uKvu+BGzxARAqkX/rgD0dMsdZGIBT5pxf+" +
            "9v9Dz+8o6gMf+P2QJAkhBIwxtO03CSEsb+6RGSlvN5srEb2HYeC5Jy7APINSJGVJkiRM40i2Zg2tNX6a0GnKPE2YosCNIzgHeQ5S" +
            "L2gNSQLjCMbAaui/fP5p8jynaRryPOfxH/oY41qakyT81Vf/Lnqq1hqtNX3fM88zSZLwsd/73FuGmJa8nWVZPHnnHCGEGNdCeb33" +
            "kfBIGayyDNIUtMZ33eIh88zUdQTv8cMAzjE7B0mC67pF0TVVJkWxKLsSrnSzWYzjHKzcYZomsiwjSZIbBZbWsDZmBITFO0WfmynH" +
            "jcSw1prtdkvTvAIQPUIqPJG+7yOBqeuaNE2ZxnFRIsuWG3QOsgymaTlNrUnSFD+O8aSCUqRpunCFEEhW/uC9J13Jl4TMOI5Rudl7" +
            "2OMUVVXRti3e+xhaYiwJz7c0gPc+gpLQXlHYWrucSpoyzzNpmsayWIw2jSNpXWPHEbNmD7SGecbUdTwZPwwwz2Qr75DXrbUkRYF3" +
            "LioJCw6FaYrYJKQL50ApnLVgDE3TxFPvVg9UStF13U0RLVOWJcaYyNXn+W7quqZtWwQbtNakaYpSKoaIxFpeXGSaJtI1vWmtMXXN" +
            "Z5/5o1MX+tSjv0wI4YYiECvMEAJ4vyi14g9akxQFj737Q+Acpq4XDrF2miRtP/7+D0MIZNstU9cteLTnIfCnb2kAbYzhtde+Gl0o" +
            "SRLmeaaqquj+0gcQBa5f/zrjOC6e0/eEccROU7wxay1PPPyJU8rHE+/7hW6PI77rotGYZ0xVkR8doYqCfC2g0qKALFu+uxZZ4zAs" +
            "XuU9KEV+dLQwzRAgBNI0JUmSm0qzpu97jo/fibU2NjOttaRpGtNX07zCxYv3Rk+p67uBJQvgapKyXMCqaTCr27k854lHfoG0KPBt" +
            "S7bZMK0xaYxBpSm278E5gjEkK5WW9DsOA1iLdY5s/f20KBibBp3ny2lrTVbXjH0fs0i6Xt9ay3wT5biWEABOnezVqy8SQiDPc+r6" +
            "7hsAtRrGGMNms4nuptcbUErhVjKVCJnSmqlp0GkKIeCmCTsMJHmOWZsmfm2saK2Z2nYB0CxDGcPUdUt4rDJP05JJVrYqqVSt2GTH" +
            "kXkcl+x0lgd0qxsCXLp0X0w1d955f3R76QhdvfoiFy/eS1VVC0iFAOPICOA9SZZh2xZT17hVQXFTs4YUWi9/3Ogc27Yl326X37R2" +
            "eX81/ue++NTKPzaxD+G9jylaYl0pRVEUEfnl9Z85ywOWdPEqff8qxhhOTk4YhoHdbhdTibTCN5srjOPIbrcjW+OSPF/Iy6oQLGlO" +
            "rQMSVqXdMJBlGXlVLUi+nrhtW5B4hiWdrn+ShUR5wSQ5lBACzrnogfM8x4GMUO4zQ2CZ+tzFxYv3Mk0TVVUhRhE2JVw+z3OyLIsN" +
            "0ddf/9qSc6sKZQx4v5xk0ywXn+cFmJQir2sAxnFc3DeExU1DgJUPhBDi6ZuyZF57E3IIRVHEdN33PWVZ4r2P5Mh7T13XOOcoy/Km" +
            "utJ6s9nEDg4Qpz7b7Q/EuM+yLMbnPlcoy7uYxhFvLWGlw+PJCdlmg5umJTbXFDqu4UQIYC350dESGnUNa2FkjFmMBsv312uFFdml" +
            "pyB9BiB2o+WwFidSTCuHOEuM9OiEEEnfThqbVVXFKk4Ul0InEg2lyFYeMDvHNAzkZcmoFGmWYYeBrCyZTk4WZgiMux3Z2olijVdj" +
            "DG41JABrX1FKZzG+0HLpPEnzRbiJGOLbC7TvGAJN0xBCIEkSvPfkeU5VVbEGkPQndYPMAZ1zqNVlp65bLmwMaZ4zDgN5UWCbhnSN" +
            "16SqQGvy7Zb86Cg2XOSkx5MTmGfyusbsjdiqqopETDKTlOvyfwkL8VIx3Jke8NBD36Dv+wWN1+pNRC4AUFVVjDtYQiVJEp4Nl9Br" +
            "98d2HUlRxIJo3OP+sDA/nGNsmiVFSUgotVSISYJZvTGEACsJ26/2SuEEK26Ip07TFFvydV3TNM3NhcALL7wQTxiIniAMTfi6lMMS" +
            "KtLaSr65lM/WOUyW4azFL9YDpVBJgn/jDdw4LkA5z6j1lFgpsN5vjWmNsxa9gqhcy1pLURS89Mp/Rbd3znHP3XfS933EEKkbxEDn" +
            "ci7nci7nci7nci7nci7nci7nci7nci7fLgevpzzwwGeDbIAppXjzzf9YxuZ7TQuZNC9LWO+IPf2yLPn1h5+N/b15nvnwe36MfO3o" +
            "ztby+eefBogdq/222DiOZ87/z5IzN0XP/IF1ODEMA9baU91lMYS0poZhiM1U6S7Ja7KDnK+refM8w17rS3aWZIIsWy2HysG/II1T" +
            "6R/Kzcp6jSgjrTStdVy7H8cxnmye58uyVdeh5OELbhhNPGjfs/bH6f9XOdgA0neT3QG5UVlwEKXFOMMwUKyNUxnDzfNMJ11l6RB3" +
            "HTpN47rdtE6fZTNls9kcrDzcAgPkeR5H1ctE5i7S9cZl3C6dWji9Sh9C4Oce+PjSvl4HJszzMjFKEmbv+ej7HgPnliWMFQNgfVKl" +
            "64A/Puj+D8aAaZp4/fWvRTev6zqu1cqqHNx41kjm9kmSLLghY7R1QJtvt5i19Z5mGSbLUHm+zBD3VuuMMcsazoFysAF2u2+w2VyJ" +
            "oCYoLQMUwQIZVIzjiPf+xuLTOoABQDCk69Brexu4sey0zh5keiVjtEPkYAMcH7/zlEvLaqysqg3DEFfvRCFr7akRt9Z6WWxY4zut" +
            "62XmkCS4YWBqGrLNBrRGy5Mp3t8SAxyMARLrMmAVlA4hxOcIttv7OTk5iftARVEwrGsuf/bckxHQJCvIkoZkiP3nEGWGKKH1s4fe" +
            "/4Hfj1taWmtOTl4G4I477iFN07i/J2Ao22hN08TFK5k4ySRKMoZzLk6DZRdAQmr/9UPl4BDo+1fJsozr17/O5cvvYrO5QlEU0YXl" +
            "1NM05dq1l6iqCmMMZVlGUBTeIOttQATL/Vll3/e0bQvc2AU8VA42wKVL95GmaVyvaZpXYkaQrTFjDFevvsilS/dFBifDyyRJYird" +
            "H84Kh5Bskec5ZVnGsJDdhdtuAHFVWbO9fPld8TEaeVAihMAdd9wTx+tA5A6CFfK8wf74e/+RPNkMke/IVtttN4CcnuwWtG0bYxY4" +
            "5QXCCodhYLPZkGVZnOxaa2nbNo68ZSNEUmtVVdFo8vCW1B2HyMEgeP/9X455X54BkGVLOSVBdCByeKkA9z8v9YAYSZYw5DvCHZxz" +
            "t+y55IMN8PLLL5+Kx/0CRVKiLF6K+0qF6L3ng++7I3rLNE28/ubJqarvyvddjgWVLEVJDXErMOB/AFSfddiOw5D4AAAAAElFTkSu" +
            "QmCC";
    }
}
